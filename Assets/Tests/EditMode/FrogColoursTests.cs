using NUnit.Framework;
using UnityEngine;
using Frogs.Unity.UI;
using CoreFrogColour = Frogs.Core.FrogColour;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The four frog colours — issues #214 and #301,
    /// docs/specs/ui/shared-components.md#frog-colours. These four values are
    /// the v0.2 deliverable, not a placeholder for a follow-up issue.
    ///
    /// They moved on #301, and the reason is worth carrying here: they were
    /// **derived** against
    /// docs/specs/ui/game-board.md#how-the-ponds-colours-are-constrained
    /// rather than picked and then checked. The band they have to live in
    /// spans 2.22 : 1 in total, so a hand-edit to any one of these four is
    /// almost certainly a colour that no longer clears the pond's
    /// separability bar. GameBoardColoursTests is where that is measured.
    /// </summary>
    public sealed class FrogColoursTests
    {
        [Test]
        public void FourFrogColours_ExistAndMatchTheNamedHexValues()
        {
            Assert.That(FrogColours.FrogGreen, Is.EqualTo((Color)new Color32(0x3E, 0x93, 0x3E, 0xFF)));
            Assert.That(FrogColours.FrogBlue, Is.EqualTo((Color)new Color32(0x37, 0x60, 0x9A, 0xFF)));
            Assert.That(FrogColours.FrogOrange, Is.EqualTo((Color)new Color32(0xD3, 0x82, 0x31, 0xFF)));
            Assert.That(FrogColours.FrogPink, Is.EqualTo((Color)new Color32(0xD4, 0x1C, 0x78, 0xFF)));
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

        [TestCase(CoreFrogColour.Green, 0x3E, 0x93, 0x3E)]
        [TestCase(CoreFrogColour.Blue, 0x37, 0x60, 0x9A)]
        [TestCase(CoreFrogColour.Orange, 0xD3, 0x82, 0x31)]
        [TestCase(CoreFrogColour.Pink, 0xD4, 0x1C, 0x78)]
        public void For_MapsEachCoreIdentity_ToItsOneNamedColour(CoreFrogColour colour, byte r, byte g, byte b)
        {
            Assert.That(FrogColours.For(colour), Is.EqualTo((Color)new Color32(r, g, b, 0xFF)));
        }
    }
}
