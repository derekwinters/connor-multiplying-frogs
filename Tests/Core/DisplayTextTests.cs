using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// Fitting a name into a column that may be too narrow for it —
    /// docs/specs/ui/shared-components.md#player-chip: "the chip never
    /// refuses or alters a name it is given; if a name does not fit, the chip
    /// truncates it with an ellipsis."
    ///
    /// The rule is Core's and the measuring is the renderer's, because a
    /// character count cannot promise a width: `Mohammed` is eight characters
    /// and wider than `Alexander` at nine. So the caller passes in whatever
    /// can measure its own font, and Core owns where the cut goes.
    /// </summary>
    public sealed class DisplayTextTests
    {
        // A stand-in for a real font: every character is ten pixels wide, and
        // so is the ellipsis. Keeps these tests about where the cut lands
        // rather than about font metrics.
        static readonly Func<string, float> TenPixelsPerCharacter =
            text => text == null ? 0f : text.Length * 10f;

        [Test]
        public void ANameThatFits_IsLeftExactlyAsItIs()
        {
            Assert.That(DisplayText.TruncateToWidth("Blue", 128f, TenPixelsPerCharacter), Is.EqualTo("Blue"));
        }

        [Test]
        public void ANameThatFitsExactly_IsNotTruncated()
        {
            // "Isabella" is eight characters — 80 px — in exactly 80 px.
            Assert.That(DisplayText.TruncateToWidth("Isabella", 80f, TenPixelsPerCharacter), Is.EqualTo("Isabella"));
        }

        [Test]
        public void ANameOnePixelTooWide_IsCutBackAndGivenAnEllipsis()
        {
            // 80 px of name in 79 px: the longest prefix that leaves room for
            // the ellipsis is seven characters, less the ellipsis's own ten,
            // so six characters and the ellipsis — 70 px.
            var fitted = DisplayText.TruncateToWidth("Isabella", 79f, TenPixelsPerCharacter);

            Assert.That(fitted, Is.EqualTo("Isabel" + DisplayText.Ellipsis));
            Assert.That(TenPixelsPerCharacter(fitted), Is.LessThanOrEqualTo(79f));
        }

        // The case the spec calls out by name: `Orange` is the game's own
        // longest default name and it overflows the chip's 128 px label
        // column before anybody has typed anything.
        [Test]
        public void TheChipsOwnWorstDefaultName_Truncates()
        {
            Func<string, float> asOnTheChip = text => text == null ? 0f : text.Length * 22f;

            var fitted = DisplayText.TruncateToWidth("Orange", 128f, asOnTheChip);

            Assert.That(fitted, Does.EndWith(DisplayText.Ellipsis));
            Assert.That(asOnTheChip(fitted), Is.LessThanOrEqualTo(128f));
        }

        [Test]
        public void AColumnTooNarrowForEvenOneCharacter_IsJustTheEllipsis()
        {
            Assert.That(DisplayText.TruncateToWidth("Isabella", 5f, TenPixelsPerCharacter), Is.EqualTo(DisplayText.Ellipsis));
        }

        [Test]
        public void AnEmptyOrMissingName_IsHandedBackUnchanged()
        {
            Assert.That(DisplayText.TruncateToWidth("", 128f, TenPixelsPerCharacter), Is.Empty);
            Assert.That(DisplayText.TruncateToWidth(null, 128f, TenPixelsPerCharacter), Is.Null);
        }

        [Test]
        public void WithNoWayToMeasure_TheNameIsLeftAlone_RatherThanGuessed()
        {
            Assert.That(DisplayText.TruncateToWidth("Isabella", 5f, null), Is.EqualTo("Isabella"));
        }
    }
}
