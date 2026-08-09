#!/usr/bin/env python3
"""Fail if a script imports a project namespace its assembly cannot see.

Unity decides which assembly a `.cs` file compiles into by where it sits: the
nearest `.asmdef` above it, or — if there is none — one of the *predefined*
assemblies, `Assembly-CSharp` and `Assembly-CSharp-Editor`. What a file may
`using` follows from that, and the rules are not visible in the C#:

  * An assembly definition sees only what its `references` list names.
  * A predefined assembly has no references list to edit. It automatically sees
    every assembly definition marked `autoReferenced: true`, and *cannot* be
    made to see one marked false.

So `Frogs.Core.asmdef` setting `autoReferenced: false` — which it does, on
purpose, so that nothing drifts into depending on Core by accident — makes
`using Frogs.Core;` a compile error in any file that is not inside an assembly
definition that names it. That error surfaces only in an editor, minutes into a
build, behind a licence. This check surfaces it in milliseconds, anywhere.

Usage:
    python .github/scripts/check_assembly_references.py [--verbose]

Exits 0 when every import is reachable, 1 with one line per violation.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
ASSETS_ROOT = REPO_ROOT / "Assets"

# `using Frogs.Core;`, `using static Frogs.Core.AppVersion;`,
# `global using Frogs.Core;`, and the aliased `using Stamp = Frogs.Core.BuildStamp;`.
USING_PATTERN = re.compile(
    r"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:[A-Za-z_]\w*\s*=\s*)?([\w.]+)\s*;",
    re.MULTILINE,
)

# guid: 0e6403fcc68e43bab1fee384ab964ce0 — in an .asmdef.meta.
GUID_PATTERN = re.compile(r"^guid:\s*([0-9a-fA-F]+)\s*$", re.MULTILINE)

# The name Unity gives the assembly a file lands in when no .asmdef covers it.
# Both predefined assemblies behave identically for our purposes.
PREDEFINED = "Assembly-CSharp"


class Assembly:
    def __init__(self, path: Path, data: dict, guid: str | None):
        self.path = path
        self.directory = path.parent
        self.name = data.get("name") or path.stem
        # An empty rootNamespace means Unity uses the assembly name, which is
        # also the convention this project follows.
        self.root_namespace = data.get("rootNamespace") or self.name
        self.references = list(data.get("references") or [])
        # Unity's default when the key is absent is true.
        self.auto_referenced = data.get("autoReferenced", True) is not False
        self.guid = guid

    def owns(self, namespace: str) -> bool:
        return namespace == self.root_namespace or namespace.startswith(
            self.root_namespace + "."
        )

    def can_see(self, other: "Assembly") -> bool:
        for reference in self.references:
            if reference == other.name:
                return True
            if reference.startswith("GUID:") and other.guid:
                if reference[len("GUID:") :].strip().lower() == other.guid.lower():
                    return True
        return False


def load_assemblies(assets_root: Path) -> list[Assembly]:
    assemblies = []
    for path in sorted(assets_root.rglob("*.asmdef")):
        try:
            data = json.loads(path.read_text())
        except json.JSONDecodeError as error:
            raise SystemExit(f"{path}: invalid JSON — {error}")

        meta = path.with_suffix(".asmdef.meta")
        guid = None
        if meta.exists():
            found = GUID_PATTERN.search(meta.read_text())
            guid = found.group(1) if found else None

        assemblies.append(Assembly(path, data, guid))
    return assemblies


def owning_assembly(source: Path, assemblies: list[Assembly]) -> Assembly | None:
    """The nearest .asmdef at or above `source`, or None for a predefined one."""
    candidates = [a for a in assemblies if a.directory in source.parents]
    if not candidates:
        return None
    # Nearest wins: the deepest directory of those that contain the file.
    return max(candidates, key=lambda a: len(a.directory.parts))


def scan(assets_root: Path) -> list[str]:
    """One human-readable line per import that will not compile."""
    assets_root = Path(assets_root)
    if not assets_root.is_dir():
        return []

    assemblies = load_assemblies(assets_root)
    problems: list[str] = []

    for source in sorted(assets_root.rglob("*.cs")):
        owner = owning_assembly(source, assemblies)
        where = source.relative_to(assets_root)

        for namespace in USING_PATTERN.findall(source.read_text()):
            target = next((a for a in assemblies if a.owns(namespace)), None)

            # Not one of ours — System, UnityEngine, a package. Not our business.
            if target is None or target is owner:
                continue

            if owner is None:
                if not target.auto_referenced:
                    problems.append(
                        f"Assets/{where}: `using {namespace};` — this file is in no "
                        f"assembly definition, so it compiles into {PREDEFINED}, "
                        f"which cannot reference {target.name} because that assembly "
                        f"sets autoReferenced: false. Give the folder its own "
                        f".asmdef referencing {target.name}."
                    )
                continue

            if not owner.can_see(target):
                problems.append(
                    f"Assets/{where}: `using {namespace};` — {owner.name} does not "
                    f"reference {target.name}. Add it to the references list in "
                    f"{owner.path.name}."
                )

    return problems


def main(argv: list[str]) -> int:
    verbose = "--verbose" in argv
    problems = scan(ASSETS_ROOT)

    if problems:
        print("Imports that will not compile:\n", file=sys.stderr)
        for problem in problems:
            print(f"  {problem}", file=sys.stderr)
        print(
            "\nSee docs/engineering/tech-stack.md for how the assemblies fit together.",
            file=sys.stderr,
        )
        return 1

    if verbose:
        print("Every project import is reachable from the assembly it is used in.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
