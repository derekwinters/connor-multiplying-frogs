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

        // The formula is specified in docs/engineering/versioning.md. Android
        // needs a monotonically increasing integer, and deriving it means a
        // rebuild of a tag produces the same artifact rather than a new number.
        [TestCase("0.0.1", 1)]
        [TestCase("0.1.0", 100)]
        [TestCase("0.2.3", 203)]
        [TestCase("1.0.0", 10000)]
        public void AndroidVersionCode_IsDerivedFromTheVersion(string text, int expected)
        {
            Assert.That(AppVersion.Parse(text).AndroidVersionCode, Is.EqualTo(expected));
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
    }
}
