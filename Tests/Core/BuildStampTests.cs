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

        [Test]
        public void Debug_AcceptsAFullLengthShaAndShortensIt()
        {
            var stamp = BuildStamp.Debug(Version, 147, "abc1234def5678901234567890abcdef12345678");

            Assert.That(stamp.VersionName, Is.EqualTo("0.2.3-abc1234"));
        }
    }
}
