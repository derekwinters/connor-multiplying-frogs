using System;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// Which of the generated icon files fills which of Unity's Android icon
    /// slots (#459).
    ///
    /// Unity lists the slots and says how big each one is; the generator wrote
    /// one file per density. This is the mapping between the two, checked here
    /// without an editor so a wrong pick fails in two seconds rather than on a
    /// tablet's home screen.
    /// </summary>
    public sealed class AppIconSetTests
    {
        [TestCase(108, "mdpi")]
        [TestCase(162, "hdpi")]
        [TestCase(216, "xhdpi")]
        [TestCase(324, "xxhdpi")]
        [TestCase(432, "xxxhdpi")]
        public void AnAdaptiveSlotTakesBothLayersAtItsOwnDensity(int pixels, string density)
        {
            // Background first, then foreground: the order the layers sit in
            // an adaptive slot.
            Assert.That(
                AppIconSet.FilesFor(AppIconKind.Adaptive, pixels),
                Is.EqualTo(new[]
                {
                    $"adaptive-background-{density}.png",
                    $"adaptive-foreground-{density}.png",
                }));
        }

        [TestCase(48, "mdpi")]
        [TestCase(72, "hdpi")]
        [TestCase(96, "xhdpi")]
        [TestCase(144, "xxhdpi")]
        [TestCase(192, "xxxhdpi")]
        public void ARoundSlotTakesTheRoundIconAtItsOwnDensity(int pixels, string density)
        {
            Assert.That(
                AppIconSet.FilesFor(AppIconKind.Round, pixels),
                Is.EqualTo(new[] { $"round-{density}.png" }));
        }

        [TestCase(48, "mdpi")]
        [TestCase(72, "hdpi")]
        [TestCase(96, "xhdpi")]
        [TestCase(144, "xxhdpi")]
        [TestCase(192, "xxxhdpi")]
        public void ALegacySlotTakesTheLegacyIconAtItsOwnDensity(int pixels, string density)
        {
            Assert.That(
                AppIconSet.FilesFor(AppIconKind.Legacy, pixels),
                Is.EqualTo(new[] { $"legacy-{density}.png" }));
        }

        [TestCase(AppIconKind.Adaptive, 81, "adaptive-background-mdpi.png")]
        [TestCase(AppIconKind.Round, 36, "round-mdpi.png")]
        [TestCase(AppIconKind.Legacy, 36, "legacy-mdpi.png")]
        public void AnLdpiSlotIsFilledFromMdpiRatherThanLeftEmpty(
            AppIconKind kind, int pixels, string expectedFirst)
        {
            // ldpi is 0.75x. The generator draws no ldpi file, and an empty
            // slot is how a default icon gets in, so the next size up is
            // scaled down into it — never a smaller one scaled up.
            Assert.That(AppIconSet.FilesFor(kind, pixels)[0], Is.EqualTo(expectedFirst));
        }

        [TestCase(AppIconKind.Adaptive, 500)]
        [TestCase(AppIconKind.Legacy, 100)]
        [TestCase(AppIconKind.Round, 0)]
        public void ASlotOfAnUnexpectedSizeIsRefusedRatherThanGuessed(AppIconKind kind, int pixels)
        {
            // A slot size that is no Android density means Unity's slot list
            // changed under us. Picking the nearest file would build fine and
            // ship a blurred or cropped icon nobody looked at.
            var thrown = Assert.Throws<ArgumentException>(() => AppIconSet.FilesFor(kind, pixels));

            Assert.That(thrown.Message, Does.Contain(pixels.ToString()));
        }

        [Test]
        public void TheDefaultIconIsTheLargestLegacyIcon()
        {
            Assert.That(AppIconSet.DefaultIconFile, Is.EqualTo("legacy-xxxhdpi.png"));
        }

        [Test]
        public void TheFilesAreReadFromTheGeneratedFolder()
        {
            Assert.That(AppIconSet.Folder, Is.EqualTo("Assets/Art/AppIcon"));
        }
    }
}
