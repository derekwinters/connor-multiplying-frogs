using NUnit.Framework;
using UnityEngine;
using Frogs.Unity.UI;
using CoreFrogColour = Frogs.Core.FrogColour;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The four frog colours — issue #214,
    /// docs/specs/ui/shared-components.md#frog-colours. These four values are
    /// the v0.2 deliverable, not a placeholder for a follow-up issue.
    /// </summary>
    public sealed class FrogColoursTests
    {
        [Test]
        public void FourFrogColours_ExistAndMatchTheNamedHexValues()
        {
            Assert.That(FrogColours.FrogGreen, Is.EqualTo((Color)new Color32(0x3F, 0x8E, 0x4F, 0xFF)));
            Assert.That(FrogColours.FrogBlue, Is.EqualTo((Color)new Color32(0x2C, 0x6D, 0xAF, 0xFF)));
            Assert.That(FrogColours.FrogOrange, Is.EqualTo((Color)new Color32(0xD2, 0x76, 0x2B, 0xFF)));
            Assert.That(FrogColours.FrogPink, Is.EqualTo((Color)new Color32(0xC2, 0x4C, 0x86, 0xFF)));
        }

        [Test]
        public void ExactlyFourColours_AreDistinguishableFromEachOther()
        {
            var colours = new[] { FrogColours.FrogGreen, FrogColours.FrogBlue, FrogColours.FrogOrange, FrogColours.FrogPink };

            for (var i = 0; i < colours.Length; i++)
            {
                for (var j = i + 1; j < colours.Length; j++)
                {
                    Assert.That(colours[i], Is.Not.EqualTo(colours[j]), "two frogs in a game are never the same colour");
                }
            }
        }

        [TestCase(CoreFrogColour.Green, 0x3F, 0x8E, 0x4F)]
        [TestCase(CoreFrogColour.Blue, 0x2C, 0x6D, 0xAF)]
        [TestCase(CoreFrogColour.Orange, 0xD2, 0x76, 0x2B)]
        [TestCase(CoreFrogColour.Pink, 0xC2, 0x4C, 0x86)]
        public void For_MapsEachCoreIdentity_ToItsOneNamedColour(CoreFrogColour colour, byte r, byte g, byte b)
        {
            Assert.That(FrogColours.For(colour), Is.EqualTo((Color)new Color32(r, g, b, 0xFF)));
        }
    }
}
