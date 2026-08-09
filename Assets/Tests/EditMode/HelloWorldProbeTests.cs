using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// What the running app says about itself.
    ///
    /// The assertion that matters is not the wording: it is that a type in the
    /// Unity shell reached a type in Core, inside a real Unity compilation. The
    /// Core suite cannot show that — it compiles Core on its own — and it is
    /// the thing that breaks when an asmdef loses a reference.
    /// </summary>
    public sealed class HelloWorldProbeTests
    {
        [Test]
        public void TheProbeReportsTheVersionCoreReadsOutOfTheBuildName()
        {
            var expected = AppVersion.Parse("0.2.3").ToString();

            Assert.That(HelloWorldProbe.Describe("0.2.3-abc1234"), Does.Contain(expected));
        }

        [Test]
        public void TheProbeSaysSoRatherThanThrowingWhenTheBuildStampIsUnreadable()
        {
            // A build that cannot read its own version has a broken stamp. An
            // app that dies on launch is a worse way to report that than a log
            // line saying so, so this path is asserted rather than assumed.
            Assert.That(() => HelloWorldProbe.Describe("nightly"), Throws.Nothing);
            Assert.That(HelloWorldProbe.Describe("nightly"), Does.Contain("nightly"));
        }
    }
}
