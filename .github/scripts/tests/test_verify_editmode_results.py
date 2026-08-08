"""Unit tests for the EditMode results gate.

The gate decides whether a Unity test run passed. Getting it wrong in the
lenient direction means shipping on a red suite, so these tests lean on the
failure cases.
"""

import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import verify_editmode_results as gate  # noqa: E402


def results_xml(total=3, passed=3, failed=0, inconclusive=0, skipped=0, result="Passed"):
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<test-run id="2" result="{result}" total="{total}" passed="{passed}" '
        f'failed="{failed}" inconclusive="{inconclusive}" skipped="{skipped}" '
        'asserts="3" duration="1.234">\n'
        '  <test-suite type="Assembly" name="Frogs.Unity.EditModeTests" />\n'
        "</test-run>\n"
    )


class GreenRunTests(unittest.TestCase):
    def test_a_clean_run_passes(self):
        verdict = gate.verify(results_xml(), exit_code=0)

        self.assertTrue(verdict.ok, verdict.reason)
        self.assertIn("3", verdict.reason)

    def test_skipped_tests_do_not_fail_the_run(self):
        verdict = gate.verify(results_xml(total=3, passed=2, skipped=1), exit_code=0)

        self.assertTrue(verdict.ok, verdict.reason)


class ZeroTestTests(unittest.TestCase):
    def test_zero_tests_is_a_failure_however_green_it_looks(self):
        # The most dangerous green there is: a licence problem, a missing
        # assembly, or a filter that matched nothing all look like success.
        verdict = gate.verify(results_xml(total=0, passed=0), exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("0 tests", verdict.reason)


class RedRunTests(unittest.TestCase):
    def test_a_failing_test_fails_the_gate(self):
        verdict = gate.verify(results_xml(total=3, passed=2, failed=1, result="Failed"), exit_code=1)

        self.assertFalse(verdict.ok)
        self.assertIn("1 failed", verdict.reason)

    def test_a_failing_test_fails_even_when_unity_exited_zero(self):
        verdict = gate.verify(results_xml(total=3, passed=2, failed=1), exit_code=0)

        self.assertFalse(verdict.ok)

    def test_a_failing_test_fails_even_on_the_forgiven_exit_code(self):
        # Forgiveness is about teardown, never about results.
        verdict = gate.verify(
            results_xml(total=3, passed=2, failed=1), exit_code=gate.DEFAULT_FORGIVEN_EXIT_CODE
        )

        self.assertFalse(verdict.ok)

    def test_an_inconclusive_test_fails(self):
        verdict = gate.verify(results_xml(total=3, passed=2, inconclusive=1), exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("inconclusive", verdict.reason)


class TeardownForgivenessTests(unittest.TestCase):
    def test_the_known_teardown_exit_code_is_forgiven_on_a_green_run(self):
        verdict = gate.verify(results_xml(), exit_code=gate.DEFAULT_FORGIVEN_EXIT_CODE)

        self.assertTrue(verdict.ok, verdict.reason)
        self.assertIn("teardown", verdict.reason)

    def test_any_other_nonzero_exit_code_still_fails(self):
        verdict = gate.verify(results_xml(), exit_code=2)

        self.assertFalse(verdict.ok)
        self.assertIn("exit code 2", verdict.reason)

    def test_an_extra_forgiven_code_can_be_supplied(self):
        verdict = gate.verify(results_xml(), exit_code=134, forgiven=(134,))

        self.assertTrue(verdict.ok, verdict.reason)


class MissingOrBrokenResultsTests(unittest.TestCase):
    def test_a_run_that_died_before_writing_results_fails(self):
        verdict = gate.verify(None, exit_code=139)

        self.assertFalse(verdict.ok)
        self.assertIn("no results", verdict.reason.lower())

    def test_a_missing_file_fails_rather_than_being_skipped(self):
        with TemporaryDirectory() as directory:
            verdict = gate.verify_file(Path(directory) / "nope.xml", exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("nope.xml", verdict.reason)

    def test_unparseable_xml_fails(self):
        verdict = gate.verify("<test-run total='3'", exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("could not be parsed", verdict.reason)

    def test_the_wrong_root_element_fails(self):
        verdict = gate.verify("<results total='3' passed='3' failed='0' />", exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("test-run", verdict.reason)

    def test_missing_counts_fail_rather_than_defaulting_to_zero(self):
        # A total that defaults to 0 would fail anyway, but a *failed* count
        # defaulting to 0 would turn an unreadable file into a pass.
        verdict = gate.verify('<test-run total="3" passed="3" />', exit_code=0)

        self.assertFalse(verdict.ok)
        self.assertIn("failed", verdict.reason)


class FileAndCommandLineTests(unittest.TestCase):
    def write(self, directory, contents):
        path = Path(directory) / "results.xml"
        path.write_text(contents, encoding="utf-8")
        return path

    def test_verify_file_reads_and_passes(self):
        with TemporaryDirectory() as directory:
            path = self.write(directory, results_xml())

            self.assertTrue(gate.verify_file(path, exit_code=0).ok)

    def test_main_exits_zero_on_a_green_run(self):
        with TemporaryDirectory() as directory:
            path = self.write(directory, results_xml())

            self.assertEqual(0, gate.main(["--results", str(path), "--exit-code", "0"]))

    def test_main_exits_one_on_a_red_run(self):
        with TemporaryDirectory() as directory:
            path = self.write(directory, results_xml(total=3, passed=2, failed=1))

            self.assertEqual(1, gate.main(["--results", str(path), "--exit-code", "0"]))


if __name__ == "__main__":
    unittest.main()
