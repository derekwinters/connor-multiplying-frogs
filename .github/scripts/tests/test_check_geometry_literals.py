"""Unit tests for the geometry/tuning literal check.

The check is deliberately conservative: it exists to stop the named-values rule
decaying, not to catch every violation. These tests pin both what it flags and
what it deliberately does not, so a later "improvement" has to face the cases
that were left out on purpose.
"""

import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import check_geometry_literals as check  # noqa: E402


class FlaggedTests(unittest.TestCase):
    def findings(self, source):
        return check.find_literals(source, "Thing.cs")

    def test_a_bare_literal_in_a_method_body_is_flagged(self):
        found = self.findings("void Move() { transform.position = new Vector3(12f, 0f, 0f); }")

        self.assertEqual(1, len(found))
        self.assertIn("12f", found[0].literal)

    def test_several_on_one_line_are_all_flagged(self):
        found = self.findings("void Move() { Place(12f, 40f); }")

        self.assertEqual(2, len(found))

    def test_a_literal_in_a_comparison_is_flagged(self):
        found = self.findings("void Tick() { if (elapsed > 4f) Split(); }")

        self.assertEqual(1, len(found))

    def test_the_line_number_is_reported(self):
        found = check.find_literals("void A()\n{\n    Place(12f);\n}\n", "Thing.cs")

        self.assertEqual(3, found[0].line)


class NotFlaggedTests(unittest.TestCase):
    def findings(self, source):
        return check.find_literals(source, "Thing.cs")

    def test_a_named_constant_is_not_flagged(self):
        self.assertEqual([], self.findings("const float PanelWidth = 280f;"))

    def test_a_serialized_field_is_not_flagged(self):
        self.assertEqual(
            [], self.findings("[SerializeField] float _splitSeconds = 4f;"))

    def test_a_local_declaration_is_not_flagged(self):
        self.assertEqual([], self.findings("void A() { var gap = 12f; }"))

    def test_a_typed_local_declaration_is_not_flagged(self):
        self.assertEqual([], self.findings("void A() { float gap = 12f; }"))

    def test_small_magnitudes_are_not_flagged(self):
        # 0, 1, and 2 are overwhelmingly arithmetic and halving rather than
        # measurements; flagging them would bury the real findings.
        self.assertEqual([], self.findings("void A() { Place(0f, 1f, 2f, 2.5f); }"))

    def test_the_magnitude_boundary_is_inclusive(self):
        self.assertEqual(1, len(self.findings("void A() { Place(3f); }")))

    def test_a_negative_literal_is_judged_on_magnitude(self):
        self.assertEqual(1, len(self.findings("void A() { Place(-12f); }")))

    def test_an_int_literal_is_not_flagged(self):
        # Only f-suffixed floats. Ints are loop bounds and counts far more often
        # than they are geometry, and the false-positive rate is what decides
        # whether a check survives.
        self.assertEqual([], self.findings("void A() { for (var i = 0; i < 32; i++) { } }"))

    def test_a_literal_in_a_line_comment_is_not_flagged(self):
        self.assertEqual([], self.findings("void A() { } // was 12f before"))

    def test_a_literal_in_a_string_is_not_flagged(self):
        self.assertEqual([], self.findings('void A() { Log("moved 12f"); }'))

    def test_a_literal_in_a_doc_comment_is_not_flagged(self):
        self.assertEqual([], self.findings("/// Defaults to 12f.\nvoid A() { }"))


class BaselineTests(unittest.TestCase):
    def test_a_file_within_its_baseline_passes(self):
        verdict = check.compare({"A.cs": 3}, {"A.cs": 3})

        self.assertTrue(verdict.ok, verdict.reason)

    def test_a_file_over_its_baseline_fails(self):
        verdict = check.compare({"A.cs": 4}, {"A.cs": 3})

        self.assertFalse(verdict.ok)
        self.assertIn("A.cs", verdict.reason)
        self.assertIn("4", verdict.reason)

    def test_a_new_file_with_any_literal_fails(self):
        verdict = check.compare({"B.cs": 1}, {"A.cs": 3})

        self.assertFalse(verdict.ok)
        self.assertIn("B.cs", verdict.reason)

    def test_a_file_under_its_baseline_passes_and_says_so(self):
        # A decrease must not fail: the ratchet's job is "not worse", and
        # failing on improvement is how a check gets turned off.
        verdict = check.compare({"A.cs": 1}, {"A.cs": 3})

        self.assertTrue(verdict.ok, verdict.reason)
        self.assertIn("--update-baseline", verdict.reason)

    def test_a_removed_file_passes(self):
        verdict = check.compare({}, {"A.cs": 3})

        self.assertTrue(verdict.ok, verdict.reason)

    def test_a_clean_tree_against_an_empty_baseline_passes(self):
        self.assertTrue(check.compare({}, {}).ok)


class BaselineFileTests(unittest.TestCase):
    def test_a_baseline_round_trips(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "baseline.txt"
            check.write_baseline(path, {"b.cs": 2, "a.cs": 1})

            self.assertEqual({"a.cs": 1, "b.cs": 2}, check.read_baseline(path))

    def test_a_missing_baseline_reads_as_empty(self):
        with TemporaryDirectory() as directory:
            self.assertEqual({}, check.read_baseline(Path(directory) / "nope.txt"))

    def test_comments_and_blank_lines_are_ignored(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "baseline.txt"
            path.write_text("# a comment\n\na.cs\t1\n", encoding="utf-8")

            self.assertEqual({"a.cs": 1}, check.read_baseline(path))

    def test_the_written_baseline_is_sorted_for_reviewable_diffs(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "baseline.txt"
            check.write_baseline(path, {"z.cs": 1, "a.cs": 1})

            body = [line for line in path.read_text().splitlines() if not line.startswith("#")]
            self.assertEqual(["a.cs\t1", "z.cs\t1"], [line for line in body if line])


if __name__ == "__main__":
    unittest.main()
