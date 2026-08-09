"""The release-please config must stay component-free, in both halves at once.

release-please decides whether a merged release PR may be tagged by comparing
two things that are configured in different places:

* the component it parses out of the release branch name
  (`release-please--branches--main` → no component at all), and
* `getBranchComponent()`, which is `component` if set, else a normalized
  `package-name`.

If those disagree, `buildRelease` logs `PR component: undefined does not match
configured component: …` and returns without building anything. No tag, no
release, no APK — and the run still reports success. Worse, release-please then
refuses to open any *further* release PR while a merged-but-untagged one is
outstanding, so one mismatched key stops every release, not just the one.

This is a single root package with `separate-pull-requests: false`, so the
branch never carries a component. Therefore nothing in the config may produce
one. This test asserts both halves together, so they cannot drift apart again:
declaring a component while the patterns interpolate none is the exact failure,
and it is invisible in the config file itself.

See docs/engineering/versioning.md and issue #205.
"""

import json
import unittest
from pathlib import Path

CONFIG_PATH = (Path(__file__).resolve().parents[3]
               / ".github" / "release-please" / "config.json")

# Keys release-please turns into a branch component. `component` is used
# directly; `package-name` is normalized into one when `component` is absent.
COMPONENT_KEYS = ("component", "package-name")

# The interpolation release-please expands into the component when writing a
# branch name, a tag, or a PR title.
COMPONENT_TOKEN = "${component}"


def load_config():
    return json.loads(CONFIG_PATH.read_text())


def component_producing_keys(config):
    """Every place in `config` that gives release-please a branch component.

    Returned as `"<where>.<key>"` so a failure names the line to delete.
    """
    found = []

    for key in COMPONENT_KEYS:
        if config.get(key):
            found.append(f"(root).{key}")

    for name, package in (config.get("packages") or {}).items():
        for key in COMPONENT_KEYS:
            if package.get(key):
                found.append(f"{name}.{key}")

    if config.get("include-component-in-tag"):
        found.append("(root).include-component-in-tag")

    return sorted(found)


def patterns_using_component(config, path="(root)"):
    """Every string in `config` that interpolates `${component}`.

    Recursive rather than a list of known pattern keys: release-please has
    several (`pull-request-title-pattern`, its group variant, and more added
    between versions), and a check that only knows today's names would quietly
    stop seeing tomorrow's.
    """
    found = []

    if isinstance(config, dict):
        for key, value in config.items():
            found.extend(patterns_using_component(value, f"{path}.{key}"))
    elif isinstance(config, list):
        for index, value in enumerate(config):
            found.extend(patterns_using_component(value, f"{path}[{index}]"))
    elif isinstance(config, str) and COMPONENT_TOKEN in config:
        found.append(path)

    return sorted(found)


class RealConfigTests(unittest.TestCase):
    """The config as it stands in this repo."""

    def setUp(self):
        self.config = load_config()

    def test_the_root_package_is_configured(self):
        """Guards against the rest of this suite passing on an empty config."""
        self.assertIn(".", self.config.get("packages", {}))

    def test_nothing_declares_a_component(self):
        declared = component_producing_keys(self.config)

        self.assertEqual(
            declared, [],
            "The release branch is `release-please--branches--main`, which "
            "carries no component, so release-please will refuse to tag any "
            f"release while these are set: {declared}.")

    def test_no_pattern_interpolates_a_component(self):
        self.assertEqual(patterns_using_component(self.config), [])

    def test_the_two_halves_agree(self):
        """The invariant. Either both sides have a component, or neither does."""
        declared = component_producing_keys(self.config)
        interpolated = patterns_using_component(self.config)

        self.assertEqual(
            bool(declared), bool(interpolated),
            f"declared {declared} but interpolated {interpolated} — a config "
            "that names a component the patterns never use is the shape that "
            "silently stops every release from being tagged.")


class ComponentDetectionTests(unittest.TestCase):
    """The detectors see the bad shapes, so a clean result means something."""

    def test_a_package_name_is_a_component(self):
        config = {"packages": {".": {"package-name": "multiplying-frogs"}}}
        self.assertEqual(component_producing_keys(config),
                         ["..package-name"])

    def test_an_explicit_component_is_a_component(self):
        config = {"packages": {".": {"component": "frogs"}}}
        self.assertEqual(component_producing_keys(config), ["..component"])

    def test_a_root_level_key_counts_too(self):
        self.assertEqual(component_producing_keys({"package-name": "frogs"}),
                         ["(root).package-name"])

    def test_include_component_in_tag_counts(self):
        self.assertEqual(component_producing_keys(
            {"include-component-in-tag": True}),
            ["(root).include-component-in-tag"])

    def test_an_empty_value_is_not_a_component(self):
        config = {"packages": {".": {"package-name": ""}}}
        self.assertEqual(component_producing_keys(config), [])

    def test_a_clean_package_declares_nothing(self):
        config = {"include-component-in-tag": False,
                  "packages": {".": {"release-type": "simple"}}}
        self.assertEqual(component_producing_keys(config), [])


class PatternDetectionTests(unittest.TestCase):
    def test_a_title_pattern_using_the_component_is_seen(self):
        config = {"pull-request-title-pattern":
                  "chore(${component}): release ${version}"}
        self.assertEqual(patterns_using_component(config),
                         ["(root).pull-request-title-pattern"])

    def test_a_pattern_nested_in_a_package_is_seen(self):
        config = {"packages": {".": {"tag-format": "${component}-v${version}"}}}
        self.assertEqual(patterns_using_component(config),
                         ["(root).packages...tag-format"])

    def test_a_pattern_inside_a_list_is_seen(self):
        config = {"extra-files": [{"path": "${component}/VERSION"}]}
        self.assertEqual(patterns_using_component(config),
                         ["(root).extra-files[0].path"])

    def test_patterns_without_the_token_are_not_seen(self):
        config = {"pull-request-title-pattern":
                  "chore(${branch}): release ${version}"}
        self.assertEqual(patterns_using_component(config), [])


if __name__ == "__main__":
    unittest.main()
