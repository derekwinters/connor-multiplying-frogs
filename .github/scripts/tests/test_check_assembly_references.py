"""Unit tests for the assembly-reference check.

The rule being pinned is the one that cost a whole CI cycle to find: a `using`
of a project namespace only compiles if the assembly the file lands in can
actually see the assembly that namespace lives in. Unity answers that question
minutes into a build, in an editor, and only after a licence has been sorted
out. These tests answer it in milliseconds.

The cases below are the ones that differ from intuition — the predefined
assembly that cannot be given a reference, and the `autoReferenced: false` flag
that decides whether it gets one anyway.
"""

import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import check_assembly_references as check  # noqa: E402

ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


class Tree:
    """A throwaway Assets/ tree, built a file at a time."""

    def __init__(self, root):
        self.root = Path(root)

    def asmdef(self, directory, name, *, references=(), auto_referenced=True, guid=None):
        import json

        folder = self.root / directory
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / f"{name}.asmdef"
        path.write_text(
            json.dumps(
                {
                    "name": name,
                    "rootNamespace": name,
                    "references": list(references),
                    "autoReferenced": auto_referenced,
                }
            )
        )
        if guid:
            path.with_suffix(".asmdef.meta").write_text(ASMDEF_META.format(guid=guid))
        return path

    def source(self, path, body):
        full = self.root / path
        full.parent.mkdir(parents=True, exist_ok=True)
        full.write_text(body)
        return full


class PredefinedAssemblyTests(unittest.TestCase):
    """Scripts in a folder with no .asmdef land in Assembly-CSharp*."""

    def test_it_cannot_see_an_asmdef_that_is_not_auto_referenced(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.source("Editor/Applier.cs", "using Frogs.Core;\n")

            problems = check.scan(tree.root)

            self.assertEqual(1, len(problems))
            self.assertIn("Editor/Applier.cs", problems[0])
            self.assertIn("Frogs.Core", problems[0])

    def test_it_can_see_an_asmdef_that_is_auto_referenced(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=True)
            tree.source("Editor/Applier.cs", "using Frogs.Core;\n")

            self.assertEqual([], check.scan(tree.root))


class AssemblyDefinitionTests(unittest.TestCase):
    def test_a_listed_reference_is_fine(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.asmdef("Editor", "Frogs.EditorTools", references=["Frogs.Core"])
            tree.source("Editor/Applier.cs", "using Frogs.Core;\n")

            self.assertEqual([], check.scan(tree.root))

    def test_a_missing_reference_is_flagged(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.asmdef("Editor", "Frogs.EditorTools", references=[])
            tree.source("Editor/Applier.cs", "using Frogs.Core;\n")

            problems = check.scan(tree.root)

            self.assertEqual(1, len(problems))
            self.assertIn("Frogs.EditorTools", problems[0])

    def test_a_guid_reference_resolves_through_the_meta_file(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            guid = "0e6403fcc68e43bab1fee384ab964ce0"
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False, guid=guid)
            tree.asmdef("Editor", "Frogs.EditorTools", references=[f"GUID:{guid}"])
            tree.source("Editor/Applier.cs", "using Frogs.Core;\n")

            self.assertEqual([], check.scan(tree.root))

    def test_a_file_using_its_own_assembly_needs_no_reference(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.source("Scripts/Core/Version.cs", "using Frogs.Core;\n")

            self.assertEqual([], check.scan(tree.root))

    def test_a_nested_namespace_counts_as_the_same_assembly(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.asmdef("Editor", "Frogs.EditorTools", references=["Frogs.Core"])
            tree.source("Editor/Applier.cs", "using Frogs.Core.Frogs;\n")

            self.assertEqual([], check.scan(tree.root))

    def test_the_nearest_asmdef_upwards_owns_the_file(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.asmdef("Editor", "Frogs.EditorTools", references=["Frogs.Core"])
            tree.source("Editor/Deep/Nested/Applier.cs", "using Frogs.Core;\n")

            self.assertEqual([], check.scan(tree.root))


class IgnoredTests(unittest.TestCase):
    """Only project assemblies are checked — engine and BCL namespaces are not."""

    def test_engine_and_framework_usings_are_ignored(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.source(
                "Editor/Applier.cs",
                "using System;\nusing UnityEditor;\nusing UnityEngine;\n",
            )

            self.assertEqual([], check.scan(tree.root))

    def test_static_and_global_usings_are_read(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.source("Editor/Applier.cs", "global using static Frogs.Core.AppVersion;\n")

            self.assertEqual(1, len(check.scan(tree.root)))

    def test_an_aliased_using_is_read(self):
        with TemporaryDirectory() as tmp:
            tree = Tree(tmp)
            tree.asmdef("Scripts/Core", "Frogs.Core", auto_referenced=False)
            tree.source("Editor/Applier.cs", "using Stamp = Frogs.Core.BuildStamp;\n")

            self.assertEqual(1, len(check.scan(tree.root)))


if __name__ == "__main__":
    unittest.main()
