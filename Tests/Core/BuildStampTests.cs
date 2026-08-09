using System;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    public sealed class BuildStampTests
    {
        static readonly AppVersion Version = AppVersion.Parse("0.2.3");

        [Test]
        public void Release_UsesTheBareVersionAsItsName()
        {
            var stamp = BuildStamp.Release(Version, commitCount: 147);

            Assert.That(stamp.VersionName, Is.EqualTo("0.2.3"));
            Assert.That(stamp.VersionCode, Is.EqualTo(147));
        }

        [Test]
        public void Debug_AppendsTheShortShaToTheName()
        {
            var stamp = BuildStamp.Debug(Version, commitCount: 147, shortSha: "abc1234");

            Assert.That(stamp.VersionName, Is.EqualTo("0.2.3-abc1234"));
            Assert.That(stamp.VersionCode, Is.EqualTo(147));
        }

        // Android refuses to install an APK whose versionCode is not greater
        // than the installed one, so a zero or negative code is a build that
        // cannot be installed over anything — fail while we still know why.
        [TestCase(0)]
        [TestCase(-1)]
        public void Release_RejectsACommitCountThatIsNotPositive(int commitCount)
        {
            Assert.That(
                () => BuildStamp.Release(Version, commitCount),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Debug_RejectsAMissingSha(string shortSha)
        {
            Assert.That(
                () => BuildStamp.Debug(Version, 147, shortSha),
                Throws.InstanceOf<ArgumentException>());
        }

        // "the one from Tuesday" is not an answer when there are four builds on
        // the phone, so a debug stamp without a usable sha is worse than a
        // failed build.
        [TestCase("nothex!")]
        [TestCase("abc")]
        public void Debug_RejectsAShaThatIsNotUsable(string shortSha)
        {
            Assert.That(
                () => BuildStamp.Debug(Version, 147, shortSha),
                Throws.InstanceOf<ArgumentException>());
        }

        // A debug build has to install alongside the release build, so Connor
        // keeps the game he plays and the build being tested at the same time.
        [Test]
        public void ApplicationIdWithSuffix_AppendsTheSuffix()
        {
            Assert.That(
                BuildStamp.ApplicationIdWithSuffix("com.derekwinters.multiplyingfrogs", ".debug"),
                Is.EqualTo("com.derekwinters.multiplyingfrogs.debug"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ApplicationIdWithSuffix_LeavesTheIdAloneWhenThereIsNoSuffix(string suffix)
        {
            Assert.That(
                BuildStamp.ApplicationIdWithSuffix("com.derekwinters.multiplyingfrogs", suffix),
                Is.EqualTo("com.derekwinters.multiplyingfrogs"));
        }

        [Test]
        public void ApplicationIdWithSuffix_IsNotAppliedTwice()
        {
            var once = BuildStamp.ApplicationIdWithSuffix("com.frogs", ".debug");

            Assert.That(BuildStamp.ApplicationIdWithSuffix(once, ".debug"), Is.EqualTo(once));
        }

        [TestCase("debug")]
        [TestCase(".Debug")]
        [TestCase(".deb ug")]
        [TestCase(".")]
        public void ApplicationIdWithSuffix_RejectsASuffixAndroidWouldNotAccept(string suffix)
        {
            Assert.That(
                () => BuildStamp.ApplicationIdWithSuffix("com.frogs", suffix),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase("")]
        public void ApplicationIdWithSuffix_RejectsAMissingApplicationId(string applicationId)
        {
            Assert.That(
                () => BuildStamp.ApplicationIdWithSuffix(applicationId, ".debug"),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ReleaseCandidate_NamesItselfRcN()
        {
            var stamp = BuildStamp.ReleaseCandidate(Version, commitCount: 147, rcNumber: 2);

            Assert.That(stamp.VersionName, Is.EqualTo("0.2.3-rc2"));
            Assert.That(stamp.VersionCode, Is.EqualTo(147));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ReleaseCandidate_RejectsAnRcNumberBelowOne(int rcNumber)
        {
            // rc0 would mean the counting went wrong, and an RC nobody can
            // order against its siblings is worse than a failed build.
            Assert.That(
                () => BuildStamp.ReleaseCandidate(Version, 147, rcNumber),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Debug_AcceptsAFullLengthShaAndShortensIt()
        {
            var stamp = BuildStamp.Debug(Version, 147, "abc1234def5678901234567890abcdef12345678");

            Assert.That(stamp.VersionName, Is.EqualTo("0.2.3-abc1234"));
        }

        // The running app reads its own version name back to say which build it
        // is. That only works while composing and reading stay inverses, so the
        // round trip is asserted rather than assumed.
        [Test]
        public void EveryKindOfStamp_HasAVersionNameThatReadsBackToItsVersion()
        {
            var stamps = new[]
            {
                BuildStamp.Release(Version, 147),
                BuildStamp.Debug(Version, 147, "abc1234"),
                BuildStamp.ReleaseCandidate(Version, 147, 2),
            };

            foreach (var stamp in stamps)
            {
                Assert.That(
                    AppVersion.ReadFromBuildName(stamp.VersionName),
                    Is.EqualTo(stamp.Version),
                    $"'{stamp.VersionName}' did not read back to {stamp.Version}");
            }
        }
    }
}
