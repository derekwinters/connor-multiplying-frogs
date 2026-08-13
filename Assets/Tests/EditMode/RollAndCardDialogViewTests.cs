using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs and GameBoardScreenViewTests.cs work around — so the shared
// components are pulled in by explicit alias, and a bare `Button` in this file
// always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The roll-and-card dialog — issue #221, built to
    /// docs/specs/ui/roll-and-card.md and its committed 1:1 mockup. Written
    /// before <see cref="RollAndCardDialogView"/> exists, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning these red before green — there is no editor here to watch
    /// them fail.
    ///
    /// **There is no randomness in this file, and none in what it tests.**
    /// The roll and the draw both happened in Core before this dialog opened,
    /// so every test drives the view from a <see cref="StubReadout"/>
    /// reporting a fixed face, a fixed pile and a fixed pair of operands.
    /// No seed, no distribution, nothing flaky to chase.
    /// </summary>
    public sealed class RollAndCardDialogViewTests
    {
        // docs/specs/ui/roll-and-card.md's worked example, and the mockup's:
        // Green, the hard pile, and the widest problem the card ever holds.
        const int WorkedExampleFace = 6;
        const int WorkedExampleMultiplicand = 331;
        const int WorkedExampleMultiplier = 41;

        [Test]
        public void Die_ShowsTheFaceCoreReported_AndPileReadsTheLabelForTheReportedPile()
        {
            // The checklist's first case: a readout reporting a rolled face of
            // 5 and a stub card.
            var view = CreateView(new StubReadout
            {
                Frog = FrogColour.Green,
                Face = 5,
                Pile = Pile.Hard,
                Multiplicand = 331,
                Multiplier = 41
            });

            try
            {
                view.SkipEntry();

                Assert.That(view.VisiblePips.Count, Is.EqualTo(5), "five pips for a five");
                Assert.That(PipPattern(view), Is.EquivalentTo(FivePipLayout), "the five-pip layout");
                Assert.That(view.PileLabel, Is.EqualTo("Hard pile · 5 or 6"));

                // Not a numeral anywhere in the die — a pip is a circle, and
                // the spec's own line is "drawn with pips rather than a
                // numeral".
                Assert.That(
                    view.DieRect.GetComponentsInChildren<Text>(true),
                    Is.Empty,
                    "no numeral anywhere in the die region");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Pile_IsReadFromWhatCoreReported_NotReDerivedFromTheFace()
        {
            // A deliberately inconsistent readout: a face of 5 with the Easy
            // pile. Nothing in the real game can produce this — Core's
            // mapping sends a 5 to the hard pile — and that is exactly why it
            // is the test. A view that re-derives the pile from the face
            // would print "Hard pile · 5 or 6" here; a view that displays
            // what Core reported prints the easy pile's label.
            var view = CreateView(new StubReadout
            {
                Frog = FrogColour.Blue,
                Face = 5,
                Pile = Pile.Easy,
                Multiplicand = 68,
                Multiplier = 5
            });

            try
            {
                view.SkipEntry();

                Assert.That(view.PileLabel, Is.EqualTo("Easy pile · 1 or 2"));
                Assert.That(view.VisiblePips.Count, Is.EqualTo(5), "the die still shows the reported face");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryFace_RendersItsOwnDistinctPipPattern_AndNeverANumeral()
        {
            var patterns = new List<Vector2[]>();

            for (var face = Roll.MinimumFace; face <= Roll.MaximumFace; face++)
            {
                var view = CreateView(WorkedExample(face: face));

                try
                {
                    view.SkipEntry();

                    Assert.That(view.VisiblePips.Count, Is.EqualTo(face), $"a {face} shows {face} pips");
                    Assert.That(
                        view.DieRect.GetComponentsInChildren<Text>(true),
                        Is.Empty,
                        $"no numeral in the die region for a face of {face}");

                    foreach (var pip in view.VisiblePips)
                    {
                        Assert.That(
                            pip.rectTransform.sizeDelta,
                            Is.EqualTo(new Vector2(RollAndCardDialogView.DiePipDiameter, RollAndCardDialogView.DiePipDiameter)),
                            "a pip is a DiePipDiameter circle");
                    }

                    patterns.Add(PipPattern(view));
                }
                finally
                {
                    Destroy(view);
                }
            }

            for (var first = 0; first < patterns.Count; first++)
            {
                for (var second = first + 1; second < patterns.Count; second++)
                {
                    Assert.That(
                        patterns[first].OrderBy(Sortable).SequenceEqual(patterns[second].OrderBy(Sortable)),
                        Is.False,
                        $"a face of {first + 1} and a face of {second + 1} must not draw the same pattern");
                }
            }

            // The mockup's own worked example: the hard pile's 6, drawn as two
            // columns of three.
            Assert.That(patterns[WorkedExampleFace - 1], Is.EquivalentTo(SixPipLayout));
        }

        [Test]
        public void Card_RendersTheWidestProblem_StackedAndRightAlignedWithARuleUnderneath()
        {
            var view = CreateView(WorkedExample());

            try
            {
                view.SkipEntry();

                Assert.That(
                    view.CardRect.rect.size,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.CardWidth, RollAndCardDialogView.CardHeight)));

                Assert.That(view.MultiplicandText.text, Is.EqualTo("331"));
                Assert.That(view.MultiplierText.text, Is.EqualTo("× 41"), "× to the left of the second number");

                Assert.That(view.MultiplicandText.fontSize, Is.EqualTo((int)RollAndCardDialogView.CardProblemSize));
                Assert.That(view.MultiplierText.fontSize, Is.EqualTo((int)RollAndCardDialogView.CardProblemSize));

                foreach (var line in new[] { view.MultiplicandText, view.MultiplierText })
                {
                    Assert.That(
                        line.alignment.ToString(),
                        Does.EndWith("Right"),
                        "the two numbers are right-aligned against each other");
                }

                // Stacked: the multiplicand sits above the multiplier, and the
                // rule sits under both.
                Assert.That(CenterY(view.MultiplicandText.rectTransform), Is.GreaterThan(CenterY(view.MultiplierText.rectTransform)));
                Assert.That(CenterY(view.CardRuleRect), Is.LessThan(CenterY(view.MultiplierText.rectTransform)));

                Assert.That(
                    view.CardRuleRect.rect.size,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.CardRuleLength, RollAndCardDialogView.CardRuleThickness)));

                // Inside the card, with room to spare — CardProblemSize is
                // sized so the widest problem in the game fits.
                foreach (var part in new[] { view.MultiplicandText.rectTransform, view.MultiplierText.rectTransform, view.CardRuleRect })
                {
                    Assert.That(Contains(view.CardRect, part), Is.True, $"{part.name} sits inside the card");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Entering_StartsTheDieRolling_AndSettlesOnlyAfterBothDurationsElapse()
        {
            var view = CreateView(WorkedExample());

            try
            {
                Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Rolling));
                Assert.That(view.DieIsRolling, Is.True);
                Assert.That(view.VisiblePips, Is.Empty, "no face is shown while the die is still rolling");
                Assert.That(view.PileCanvasGroup.alpha, Is.EqualTo(0f), "the pile label has not appeared yet");
                Assert.That(view.CardCanvasGroup.alpha, Is.EqualTo(0f), "the card has not dealt in yet");

                var halfRoll = RollAndCardDialogView.DieRollDuration / 2f;
                view.Advance(halfRoll);

                Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Rolling), "still rolling halfway through");
                Assert.That(view.VisiblePips, Is.Empty);

                view.Advance(halfRoll);

                Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Dealing));
                Assert.That(view.DieIsRolling, Is.False);
                Assert.That(view.VisiblePips.Count, Is.EqualTo(WorkedExampleFace), "the face is shown once the die settles");
                Assert.That(view.PileCanvasGroup.alpha, Is.EqualTo(1f), "then the pile label appears");
                Assert.That(view.CardCanvasGroup.alpha, Is.EqualTo(0f), "then, and only then, the card starts dealing");

                var halfDeal = RollAndCardDialogView.CardDealDuration / 2f;
                view.Advance(halfDeal);

                Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Dealing));
                Assert.That(view.CardCanvasGroup.alpha, Is.EqualTo(0.5f).Within(0.001f));

                view.Advance(halfDeal);

                Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Settled), "settled after DieRollDuration + CardDealDuration");
                Assert.That(view.CardCanvasGroup.alpha, Is.EqualTo(1f));

                // No tap was needed to get here.
                Assert.That(view.VisiblePips.Count, Is.EqualTo(WorkedExampleFace));
                Assert.That(view.PileLabel, Is.EqualTo("Hard pile · 5 or 6"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TapOutsideSolveIt_JumpsStraightToSettled_AndTriggersNoTransition()
        {
            foreach (var tapTheScrim in new[] { true, false })
            {
                var router = new ScreenRouter();
                router.OpenDialog(Frogs.Core.Dialog.RollAndCard);

                var presses = 0;
                var view = CreateView(WorkedExample(), router);
                view.SolveItPressed += () => presses++;

                try
                {
                    Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Rolling), "the fixture, not the assertion");

                    var catcher = tapTheScrim ? view.ScrimSkipCatcher : view.PanelSkipCatcher;
                    catcher.OnPointerClick(new PointerEventData(null));

                    Assert.That(view.Phase, Is.EqualTo(RollAndCardEntryPhase.Settled));
                    Assert.That(view.VisiblePips.Count, Is.EqualTo(WorkedExampleFace));
                    Assert.That(view.PileCanvasGroup.alpha, Is.EqualTo(1f));
                    Assert.That(view.CardCanvasGroup.alpha, Is.EqualTo(1f));

                    // Skipping the animation is not answering the card.
                    Assert.That(presses, Is.EqualTo(0));
                    Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.RollAndCard), "still on this dialog");
                }
                finally
                {
                    Destroy(view);
                }
            }
        }

        [Test]
        public void SolveIt_OpensTheWorkingOutGridExactlyOnce_AndHardwareBackDoesNothingAtAll()
        {
            var router = new ScreenRouter();
            router.OpenDialog(Frogs.Core.Dialog.RollAndCard);

            var presses = 0;
            var view = CreateView(WorkedExample(), router);
            view.SolveItPressed += () => presses++;

            try
            {
                view.SkipEntry();

                // Hardware back first, while the dialog is open: nothing at
                // all happens. This is the one place in the game where back is
                // inert, and it is inert because the alternative is losing a
                // drawn card.
                router.HandleBack();

                Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.RollAndCard), "back does not dismiss");
                Assert.That(presses, Is.EqualTo(0), "back does not press `Solve it` either");
                Assert.That(view.Dialog.IsOpen, Is.True);

                // The shared Dialog routes back to whichever button an
                // instance nominates as least destructive. This dialog
                // nominates none, which is what makes back inert rather than
                // a `Solve it` in disguise.
                Assert.That(view.Dialog.LeastDestructiveButton, Is.Null);

                // The router owns hardware back (#213); this view must not add
                // a second handler that could disagree with it.
                Assert.That(
                    DeclaredMembers(typeof(RollAndCardDialogView))
                        .Where(name => name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty,
                    "hardware back is the router's, not this view's");

                Assert.That(view.SolveItButton.Kind, Is.EqualTo(ButtonKind.Primary));
                Assert.That(view.SolveItButton.Label.text, Is.EqualTo("Solve it"));

                Tap(view.SolveItButton);

                Assert.That(presses, Is.EqualTo(1), "acts on release, exactly once");
                Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.WorkingOutGrid), "the only way out");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Panel_LaysOutWhoseDiePileCardAndControls_AtTheMockupsOwnGeometry()
        {
            var view = CreateView(WorkedExample());

            try
            {
                view.SkipEntry();

                var panel = view.Dialog.PanelRect;

                Assert.That(
                    panel.rect.size,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.RollDialogWidth, RollAndCardDialogView.RollDialogHeight)));
                Assert.That(RollAndCardDialogView.RollDialogWidth, Is.LessThanOrEqualTo(DialogPanel.DialogMaxWidth));
                Assert.That(RollAndCardDialogView.RollDialogHeight, Is.LessThanOrEqualTo(DialogPanel.DialogMaxHeight));

                // whose — the active frog's chip, DialogPadding in from the
                // panel's top-left, with `rolled` RolledLabelGap past it.
                Assert.That(view.WhoseChip.State, Is.EqualTo(PlayerChipState.Active));
                Assert.That(view.WhoseChip.Label.text, Is.EqualTo("Green"));
                Assert.That(view.WhoseChip.Swatch.color, Is.EqualTo(FrogColours.For(FrogColour.Green)));
                Assert.That(view.RolledText.text, Is.EqualTo("rolled"));
                Assert.That(view.RolledText.fontSize, Is.EqualTo((int)RollAndCardDialogView.RolledLabelSize));
                Assert.That(
                    LeftEdge(view.RolledText.rectTransform) - RightEdge(view.WhoseChip.RectTransform),
                    Is.EqualTo(RollAndCardDialogView.RolledLabelGap).Within(0.001f));
                Assert.That(
                    LeftEdge(view.WhoseChip.RectTransform) - LeftEdge(panel),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    TopEdge(panel) - TopEdge(view.WhoseChip.RectTransform),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));

                // die + pile — one group, DieColumnWidth wide, DieGroupTop
                // down from the inside top of the panel, left-aligned with the
                // chip above it.
                Assert.That(view.DieGroupRect.rect.width, Is.EqualTo(RollAndCardDialogView.DieColumnWidth).Within(0.001f));
                Assert.That(
                    LeftEdge(view.DieGroupRect) - LeftEdge(panel),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    TopEdge(panel) - TopEdge(view.DieGroupRect),
                    Is.EqualTo(RollAndCardDialogView.DieGroupTop).Within(0.001f));

                Assert.That(
                    view.DieRect.rect.size,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.DieFaceSize, RollAndCardDialogView.DieFaceSize)));
                Assert.That(
                    view.DieFace.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.DieBorderWidth, RollAndCardDialogView.DieBorderWidth)),
                    "the die is outlined, not flat — DieBorderWidth of it");

                Assert.That(view.PileNameText.fontSize, Is.EqualTo((int)RollAndCardDialogView.PileLabelSize));
                Assert.That(view.PileFacesText.fontSize, Is.EqualTo((int)RollAndCardDialogView.PileLabelSize));
                Assert.That(
                    BottomEdge(view.DieRect) - TopEdge(view.PileNameText.rectTransform),
                    Is.EqualTo(RollAndCardDialogView.DiePileGap).Within(0.001f));

                // card — CardTop down from the inside top of the panel,
                // DialogPadding in from its right edge, outlined the same way
                // the die is.
                Assert.That(
                    RightEdge(panel) - RightEdge(view.CardRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    TopEdge(panel) - TopEdge(view.CardRect),
                    Is.EqualTo(RollAndCardDialogView.CardTop).Within(0.001f));
                Assert.That(
                    view.CardFace.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(RollAndCardDialogView.CardBorderWidth, RollAndCardDialogView.CardBorderWidth)));

                // The gap the mockup actually draws: 208, not the table's
                // stale 96 — corrected in this issue's spec change.
                Assert.That(
                    LeftEdge(view.CardRect) - RightEdge(view.DieGroupRect),
                    Is.EqualTo(RollAndCardDialogView.RollCardGap).Within(0.001f));
                Assert.That(RollAndCardDialogView.RollCardGap, Is.EqualTo(208f));

                // The left group is deliberately the smaller of the two.
                Assert.That(view.DieGroupRect.rect.width, Is.LessThan(view.CardRect.rect.width));

                // controls — `Solve it` in the shared Dialog's own button row,
                // primary-on-the-right, at the shared Button's own size.
                Assert.That(view.SolveItButton.transform.parent, Is.EqualTo(view.Dialog.ButtonRowRect));
                Assert.That(
                    view.SolveItButton.RectTransform.rect.size,
                    Is.EqualTo(new Vector2(Button.ButtonMinWidth, Button.ButtonHeight)),
                    "nothing about `Solve it` is oversized the way `Roll` is");
                Assert.That(CenterY(view.SolveItButton.RectTransform), Is.LessThan(CenterY(view.CardRect)));

                // This dialog has no title — `whose` does that job.
                Assert.That(view.Dialog.TitleText.text, Is.Empty);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryPile_IsWorthTheSameOneLilyPad_AndNothingSaysOtherwise()
        {
            var labels = new Dictionary<Pile, string>
            {
                { Pile.Easy, "Easy pile · 1 or 2" },
                { Pile.Medium, "Medium pile · 3 or 4" },
                { Pile.Hard, "Hard pile · 5 or 6" }
            };

            var sizes = new List<int>();
            var colours = new List<Color>();
            var weights = new List<FontStyle>();

            foreach (var pair in labels)
            {
                var view = CreateView(WorkedExample(pile: pair.Key));

                try
                {
                    view.SkipEntry();

                    Assert.That(view.PileLabel, Is.EqualTo(pair.Value));

                    // The three labels differ only in name and the two faces
                    // each one lists — same size, same colour, same weight.
                    sizes.Add(view.PileNameText.fontSize);
                    colours.Add(view.PileNameText.color);
                    weights.Add(view.PileNameText.fontStyle);

                    var words = view.DieGroupRect.GetComponentsInChildren<Text>(true)
                        .Select(text => text.text)
                        .ToArray();

                    Assert.That(words.Length, Is.EqualTo(2), "a pile name and its two faces, and nothing else");
                }
                finally
                {
                    Destroy(view);
                }
            }

            Assert.That(sizes.Distinct().Count(), Is.EqualTo(1), "no pile is drawn bigger than another");
            Assert.That(colours.Distinct().Count(), Is.EqualTo(1), "no pile is drawn in a louder colour than another");
            Assert.That(weights.Distinct().Count(), Is.EqualTo(1), "no pile is drawn heavier than another");

            // No points, no bonus, no difficulty weighting — the reviewer's
            // grep, as a test.
            var forbidden = new[] { "Point", "Bonus", "Score", "Reward", "Difficulty", "Tier" };

            foreach (var member in DeclaredMembers(typeof(RollAndCardDialogView)))
            {
                foreach (var word in forbidden)
                {
                    Assert.That(
                        member.IndexOf(word, StringComparison.OrdinalIgnoreCase),
                        Is.LessThan(0),
                        $"RollAndCardDialogView.{member} looks like {word} — every pile is worth the same one lily pad");
                }
            }
        }

        [Test]
        public void Dialog_IsAReadout_WithNoRandomnessAndNoWorkingOutGridOfItsOwn()
        {
            // "A reviewer can confirm no Random/RNG type appears anywhere in
            // this issue's code" — and that the grid (#223) and the answer
            // result (#224) are not being built here.
            var forbidden = new[]
            {
                "Random", "Rng", "Seed", "Shuffle",
                "WorkingOut", "Grid", "Keypad", "Carry", "Answer", "Correct", "Verdict"
            };

            foreach (var type in new[] { typeof(RollAndCardDialogView), typeof(GameRollAndCardReadout) })
            {
                foreach (var member in DeclaredMembers(type))
                {
                    foreach (var word in forbidden)
                    {
                        Assert.That(
                            member.IndexOf(word, StringComparison.OrdinalIgnoreCase),
                            Is.LessThan(0),
                            $"{type.Name}.{member} looks like {word} — this dialog is a readout, and the grid is #223's");
                    }
                }

                var coroutines = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
                    .ToArray();

                Assert.That(coroutines, Is.Empty, $"{type.Name} starts no coroutine — the entry sequence is advanced, not scheduled");
            }
        }

        [Test]
        public void EveryGeometryValue_IsANamedConstantFromRollAndCardsTable()
        {
            // docs/specs/ui/roll-and-card.md#named-constants, in full: the
            // fourteen the page always had — with `RollCardGap` corrected from
            // its stale 96 px to the 208 px the committed mockup draws — plus
            // the eleven this issue added to the table for values the mockup
            // draws and the page had never named.
            var expected = new Dictionary<string, float>
            {
                { "RollDialogWidth", 1280f },
                { "RollDialogHeight", 760f },
                { "RolledLabelSize", 40f },
                { "RolledLabelGap", 24f },
                { "DieColumnWidth", 400f },
                { "DieGroupTop", 220f },
                { "DieFaceSize", 240f },
                { "DieCornerRadius", 40f },
                { "DieBorderWidth", 4f },
                { "DiePipInset", 34f },
                { "DiePipDiameter", 40f },
                { "DiePileGap", 32f },
                { "PileLabelSize", 40f },
                { "CardTop", 180f },
                { "CardWidth", 560f },
                { "CardHeight", 420f },
                { "CardRadius", 24f },
                { "CardBorderWidth", 4f },
                { "CardProblemSize", 120f },
                { "CardRuleGap", 8f },
                { "CardRuleThickness", 8f },
                { "CardRuleLength", 360f },
                { "RollCardGap", 208f },
                { "DieRollDuration", 0.8f },
                { "CardDealDuration", 0.3f }
            };

            var constants = typeof(RollAndCardDialogView)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                constants.Select(field => field.Name).OrderBy(name => name),
                Is.EqualTo(expected.Keys.OrderBy(name => name)),
                "RollAndCardDialogView's public constants are exactly roll-and-card.md's own, under the identical names");

            foreach (var field in constants)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(expected[field.Name]).Within(0.001f),
                    $"RollAndCardDialogView.{field.Name}");
            }

            // The row across the panel adds up exactly, which is what fixes
            // RollCardGap at 208: the panel less DialogPadding on each side is
            // the die column, the gap, and the card.
            Assert.That(
                RollAndCardDialogView.DieColumnWidth
                    + RollAndCardDialogView.RollCardGap
                    + RollAndCardDialogView.CardWidth,
                Is.EqualTo(RollAndCardDialogView.RollDialogWidth - (2f * DialogPanel.DialogPadding)).Within(0.001f));

            // The shared Dialog's own constants are referenced, never
            // redeclared here — this dialog inherits its scrim, corners,
            // padding and cross-fade.
            foreach (var name in new[] { "DialogPadding", "DialogRadius", "DialogScrimOpacity", "DialogFadeDuration", "DialogTitleSize", "DialogTitleGap" })
            {
                Assert.That(
                    typeof(RollAndCardDialogView).GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    Is.Null,
                    $"RollAndCardDialogView must reference DialogPanel.{name}, not redeclare it");
            }

            // DieBorderWidth and CardBorderWidth hold the same number as the
            // shared Button's outline today and are deliberately not that
            // constant. Asserted as equal-today so a future divergence is a
            // visible, deliberate edit here rather than a silent drift.
            Assert.That(RollAndCardDialogView.DieBorderWidth, Is.EqualTo(Button.ButtonBorderWidth));
            Assert.That(RollAndCardDialogView.CardBorderWidth, Is.EqualTo(Button.ButtonBorderWidth));
        }

        [Test]
        public void Dialog_WiresItsBuiltInSpritesAndFonts_RatherThanLeavingThemMissing()
        {
            var view = CreateView(WorkedExample());

            try
            {
                Assert.That(view.RolledText.font, Is.Not.Null);
                Assert.That(view.PileNameText.font, Is.Not.Null);
                Assert.That(view.PileFacesText.font, Is.Not.Null);
                Assert.That(view.MultiplicandText.font, Is.Not.Null);
                Assert.That(view.MultiplierText.font, Is.Not.Null);
                Assert.That(view.DieBorder.sprite, Is.Not.Null);
                Assert.That(view.DieFace.sprite, Is.Not.Null);
                Assert.That(view.CardBorder.sprite, Is.Not.Null);
                Assert.That(view.CardFace.sprite, Is.Not.Null);
                Assert.That(view.Pips[0].sprite, Is.Not.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void GameReadout_ReportsWhatCoreDecided_AndComputesNothingItself()
        {
            var game = new Game(new[] { FrogColour.Orange, FrogColour.Pink }, 20260810UL);
            game.RollDie();

            var readout = new GameRollAndCardReadout(game);

            Assert.That(readout.Frog, Is.EqualTo(game.ActiveFrog));
            Assert.That(readout.Face, Is.EqualTo(game.DrawnRoll.Face));
            Assert.That(readout.Pile, Is.EqualTo(game.DrawnRoll.Pile), "the pile Core reported, not one worked out from the face");
            Assert.That(readout.Multiplicand, Is.EqualTo(game.DrawnCard.Multiplicand));
            Assert.That(readout.Multiplier, Is.EqualTo(game.DrawnCard.Multiplier));

            // Whatever face this seed produced, the dialog draws it — every
            // face is a face this view can show.
            Assert.That(readout.Face, Is.InRange(Roll.MinimumFace, Roll.MaximumFace));
        }

        // The pip grid, in cell units: -1, 0, 1 across and down, with y up.
        // These are the ordinary die-face arrangements, and the six-pip one is
        // the two columns of three the mockup draws.
        static readonly Vector2[] FivePipLayout =
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f)
        };

        static readonly Vector2[] SixPipLayout =
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f)
        };

        static Vector2[] PipPattern(RollAndCardDialogView view)
        {
            var cell = (RollAndCardDialogView.DieFaceSize
                - (2f * RollAndCardDialogView.DieBorderWidth)
                - (2f * RollAndCardDialogView.DiePipInset)) / 3f;

            return view.VisiblePips
                .Select(pip => pip.rectTransform.anchoredPosition / cell)
                .Select(offset => new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y)))
                .ToArray();
        }

        static float Sortable(Vector2 offset)
        {
            const float rowStride = 10f;
            return (offset.y * rowStride) + offset.x;
        }

        static StubReadout WorkedExample(int face = WorkedExampleFace, Pile pile = Pile.Hard)
        {
            return new StubReadout
            {
                Frog = FrogColour.Green,
                Face = face,
                Pile = pile,
                Multiplicand = WorkedExampleMultiplicand,
                Multiplier = WorkedExampleMultiplier
            };
        }

        /// <summary>
        /// A fixed roll and a fixed card. There is no die in here and no
        /// generator — the whole point of the readout seam is that this
        /// dialog's tests never touch randomness.
        /// </summary>
        sealed class StubReadout : IRollAndCardReadout
        {
            public FrogColour Frog { get; set; }

            // A fake plays under its frog's default name unless a test sets
            // one — a default name is a real name, not a placeholder.
            string _frogName;

            public string FrogName
            {
                get { return string.IsNullOrEmpty(_frogName) ? PlayerName.DefaultFor(Frog) : _frogName; }
                set { _frogName = value; }
            }
            public int Face { get; set; }
            public Pile Pile { get; set; }
            public int Multiplicand { get; set; }
            public int Multiplier { get; set; }
        }

        static IEnumerable<string> DeclaredMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name);
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static float LeftEdge(RectTransform rect) => Corners(rect)[0].x;

        static float RightEdge(RectTransform rect) => Corners(rect)[2].x;

        static float TopEdge(RectTransform rect) => Corners(rect)[2].y;

        static float BottomEdge(RectTransform rect) => Corners(rect)[0].y;

        static float CenterY(RectTransform rect)
        {
            var corners = Corners(rect);
            return (corners[0].y + corners[2].y) / 2f;
        }

        static bool Contains(RectTransform outer, RectTransform inner)
        {
            const float tolerance = 0.001f;

            return LeftEdge(inner) >= LeftEdge(outer) - tolerance
                && RightEdge(inner) <= RightEdge(outer) + tolerance
                && BottomEdge(inner) >= BottomEdge(outer) - tolerance
                && TopEdge(inner) <= TopEdge(outer) + tolerance;
        }

        static RollAndCardDialogView CreateView(IRollAndCardReadout readout, ScreenRouter router = null)
        {
            var host = new GameObject(nameof(RollAndCardDialogViewTests), typeof(RectTransform));
            var view = host.AddComponent<RollAndCardDialogView>();
            view.Initialize(readout, router);
            return view;
        }

        static void Tap(Button button)
        {
            var corners = Corners(button.RectTransform);
            var eventData = new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static void Destroy(RollAndCardDialogView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
