using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// The rule that broke the v0.2.0 release: Unity has no Mono for 64-bit
    /// Android, and asking for it anyway produces no error where the asking
    /// happens — just a build that dies later with "Target architecture not
    /// specified" (issue #282).
    ///
    /// These run without an editor, which is the point: the pairing is checked
    /// where a two-second test can check it, rather than sixteen minutes into a
    /// release build.
    /// </summary>
    public sealed class AndroidBuildSupportTests
    {
        [Test]
        public void SixtyFourBitArchitecturesCannotBeBuiltWithMono()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("X86_64", "Mono2x"),
                Does.Contain("IL2CPP"));
        }

        [Test]
        public void TheTabletsArchitectureCannotBeBuiltWithMonoEither()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("ARM64", "Mono2x"),
                Does.Contain("IL2CPP"));
        }

        [Test]
        public void TheEmulatorPairingTheProjectNowUsesIsFine()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("X86_64", "IL2CPP"),
                Is.Null);
        }

        [Test]
        public void TheDevicePairingIsFine()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("ARM64", "IL2CPP"),
                Is.Null);
        }

        [Test]
        public void ThirtyTwoBitAndroidStillHasAMonoBackend()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("ARMv7", "Mono2x"),
                Is.Null);
        }

        [Test]
        public void AFlagsEnumPrintsSeveralArchitecturesAtOnceAndEachIsChecked()
        {
            var problem = AndroidBuildSupport.PairingProblem("ARMv7, X86_64", "Mono2x");

            Assert.That(problem, Does.Contain("X86_64"));
            Assert.That(problem, Does.Not.Contain("ARMv7"));
        }

        [Test]
        public void TheProblemNamesTheBackendThatCannotBuildIt()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem("X86_64", "Mono2x"),
                Does.Contain("Mono2x"));
        }

        [Test]
        public void OnlyTheAheadOfTimeBackendCanBuildSixtyFourBit()
        {
            Assert.That(AndroidBuildSupport.RequiresIl2Cpp("ARM64"), Is.True);
            Assert.That(AndroidBuildSupport.RequiresIl2Cpp("X86_64"), Is.True);
            Assert.That(AndroidBuildSupport.RequiresIl2Cpp("ARMv7"), Is.False);
            Assert.That(AndroidBuildSupport.RequiresIl2Cpp("X86"), Is.False);
        }

        [Test]
        public void AnEmptyArchitectureSetIsTheFailureItself()
        {
            // What the editor reports back when it has dropped everything the
            // profile asked for. Unity's own message at this point names
            // neither the architecture nor the backend.
            var problem = AndroidBuildSupport.AppliedProblem("emulator", "", "Mono2x");

            Assert.That(problem, Is.Not.Null);
            Assert.That(problem, Does.Contain("emulator"));
            Assert.That(problem, Does.Contain("Mono2x"));
        }

        [Test]
        public void AnAppliedProfileThatKeptItsArchitectureIsStillPairChecked()
        {
            Assert.That(
                AndroidBuildSupport.AppliedProblem("emulator", "X86_64", "Mono2x"),
                Does.Contain("IL2CPP"));
        }

        [Test]
        public void AnAppliedProfileThatIsWhatItAskedForHasNoProblem()
        {
            Assert.That(
                AndroidBuildSupport.AppliedProblem("emulator", "X86_64", "IL2CPP"),
                Is.Null);
        }

        [Test]
        public void SurroundingWhitespaceDoesNotHideAnUnsupportedPairing()
        {
            Assert.That(
                AndroidBuildSupport.PairingProblem(" X86_64 ", " Mono2x "),
                Does.Contain("IL2CPP"));
        }

        [Test]
        public void NothingAppliedAtAllReadsAsNothingApplied()
        {
            Assert.That(
                AndroidBuildSupport.AppliedProblem("emulator", null, "IL2CPP"),
                Is.Not.Null);
        }
    }
}
