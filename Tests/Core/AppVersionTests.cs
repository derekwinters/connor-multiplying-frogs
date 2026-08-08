using System;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    public sealed class AppVersionTests
    {
        [Test]
        public void Parse_ReadsMajorMinorAndPatch()
        {
            var version = AppVersion.Parse("0.2.3");

            Assert.That(version.Major, Is.EqualTo(0));
            Assert.That(version.Minor, Is.EqualTo(2));
            Assert.That(version.Patch, Is.EqualTo(3));
        }

        // /VERSION is read by the build. Malformed input has to fail loudly at
        // the point of reading, not silently become 0.0.0 in a shipped APK.
        [TestCase("")]
        [TestCase("0.2")]
        [TestCase("0.2.3.4")]
        [TestCase("v0.2.3")]
        [TestCase("0.two.3")]
        [TestCase("0.-2.3")]
        public void Parse_RejectsAnythingThatIsNotThreeNumbers(string text)
        {
            Assert.That(() => AppVersion.Parse(text), Throws.TypeOf<FormatException>());
        }

        // /VERSION carries the release-please marker on the same line as the
        // version, so every consumer has to strip from '#' onward. Doing that
        // in one place beats each caller reinventing it slightly differently.
        [TestCase("0.0.1 # x-release-please-version", 0, 0, 1)]
        [TestCase("1.2.3 # x-release-please-version", 1, 2, 3)]
        [TestCase("0.4.0#x-release-please-version", 0, 4, 0)]
        [TestCase("  0.5.6   # trailing whitespace and a comment  ", 0, 5, 6)]
        [TestCase("0.7.8\n", 0, 7, 8)]
        public void ReadFrom_StripsTheMarkerComment(string contents, int major, int minor, int patch)
        {
            var version = AppVersion.ReadFrom(contents);

            Assert.That(version.Major, Is.EqualTo(major));
            Assert.That(version.Minor, Is.EqualTo(minor));
            Assert.That(version.Patch, Is.EqualTo(patch));
        }

        [TestCase("# x-release-please-version")]
        [TestCase("")]
        [TestCase("   ")]
        public void ReadFrom_RejectsAFileWithNoVersionOnIt(string contents)
        {
            Assert.That(() => AppVersion.ReadFrom(contents), Throws.TypeOf<FormatException>());
        }
    }
}
