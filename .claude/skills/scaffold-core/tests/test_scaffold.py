"""Unit tests for the Core scaffolder."""

import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import scaffold  # noqa: E402


class NameTests(unittest.TestCase):
    def test_a_plain_name_is_accepted(self):
        self.assertEqual("Pond", scaffold.validate_type_name("Pond"))

    def test_whitespace_is_trimmed(self):
        self.assertEqual("Pond", scaffold.validate_type_name("  Pond  "))

    def test_a_lowercase_name_is_rejected(self):
        # C# types are PascalCase, and a scaffolder that silently "fixes" the
        # name produces a file whose name does not match what was asked for.
        with self.assertRaises(ValueError):
            scaffold.validate_type_name("pond")

    def test_a_name_with_a_space_is_rejected(self):
        with self.assertRaises(ValueError):
            scaffold.validate_type_name("Frog Colony")

    def test_a_name_with_a_dot_is_rejected(self):
        with self.assertRaises(ValueError):
            scaffold.validate_type_name("Frogs.Pond")

    def test_an_empty_name_is_rejected(self):
        with self.assertRaises(ValueError):
            scaffold.validate_type_name("")


class NamespaceTests(unittest.TestCase):
    def test_the_root_namespace_for_no_subfolder(self):
        self.assertEqual("Frogs.Core", scaffold.namespace_for(""))

    def test_a_subfolder_becomes_a_namespace_segment(self):
        self.assertEqual("Frogs.Core.Rules", scaffold.namespace_for("Rules"))

    def test_nested_subfolders_nest_the_namespace(self):
        self.assertEqual("Frogs.Core.Rules.Splitting", scaffold.namespace_for("Rules/Splitting"))

    def test_the_test_namespace_mirrors_it(self):
        self.assertEqual("Frogs.Core.Tests.Rules", scaffold.test_namespace_for("Rules"))


class GuidTests(unittest.TestCase):
    def test_a_guid_is_thirty_two_hex_characters(self):
        guid = scaffold.new_guid()

        self.assertEqual(32, len(guid))
        self.assertTrue(all(character in "0123456789abcdef" for character in guid))

    def test_guids_do_not_repeat(self):
        # Two assets sharing a GUID is the one thing a .meta file must never do.
        self.assertNotEqual(scaffold.new_guid(), scaffold.new_guid())

    def test_a_meta_file_carries_the_guid(self):
        meta = scaffold.meta_file("abc123")

        self.assertIn("guid: abc123", meta)
        self.assertIn("MonoImporter", meta)


class GeneratedSourceTests(unittest.TestCase):
    def test_the_class_is_in_the_right_namespace(self):
        source = scaffold.class_source("Pond", "Frogs.Core.Rules")

        self.assertIn("namespace Frogs.Core.Rules", source)
        self.assertIn("public sealed class Pond", source)

    def test_the_class_does_not_reference_unity(self):
        source = scaffold.class_source("Pond", "Frogs.Core")

        self.assertNotIn("UnityEngine", source)
        self.assertNotIn("MonoBehaviour", source)

    def test_the_class_body_is_empty_so_the_test_fails(self):
        # The scaffolder leaves you at the START of the red phase. A class with
        # a plausible implementation already in it is an invitation to skip
        # writing the test that should have driven it.
        source = scaffold.class_source("Pond", "Frogs.Core")

        self.assertNotIn("return", source)


class GeneratedTestTests(unittest.TestCase):
    def source(self):
        return scaffold.test_source("Pond", "Frogs.Core", "Frogs.Core.Tests")

    def test_it_is_a_failing_test_not_an_empty_stub(self):
        # The whole point: running the suite immediately after scaffolding must
        # go red, so the red-green loop starts where it should.
        source = self.source()

        self.assertIn("Assert.Fail", source)

    def test_it_names_the_type_under_test(self):
        self.assertIn("PondTests", self.source())

    def test_it_uses_the_test_namespace_and_imports_the_type(self):
        source = self.source()

        self.assertIn("namespace Frogs.Core.Tests", source)
        self.assertIn("using Frogs.Core;", source)

    def test_it_uses_nunit(self):
        self.assertIn("using NUnit.Framework;", self.source())


class WritingTests(unittest.TestCase):
    def test_it_writes_four_files_in_the_right_places(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            written = scaffold.scaffold(root, "Pond", subfolder="Rules")

            self.assertTrue((root / "Assets/Scripts/Core/Rules/Pond.cs").is_file())
            self.assertTrue((root / "Assets/Scripts/Core/Rules/Pond.cs.meta").is_file())
            self.assertTrue((root / "Tests/Core/Rules/PondTests.cs").is_file())
            self.assertEqual(3, len(written))

    def test_the_test_file_gets_no_meta(self):
        # Tests/Core is outside Assets/, so Unity never sees it and a .meta
        # there would be a file nothing reads.
        with TemporaryDirectory() as directory:
            root = Path(directory)
            scaffold.scaffold(root, "Pond")

            self.assertFalse((root / "Tests/Core/PondTests.cs.meta").exists())

    def test_it_refuses_to_overwrite(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            scaffold.scaffold(root, "Pond")

            with self.assertRaises(FileExistsError):
                scaffold.scaffold(root, "Pond")


if __name__ == "__main__":
    unittest.main()
