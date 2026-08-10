using System;
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
// WorkingOutGridViewTests.cs and RollAndCardDialogViewTests.cs work around — so
// the shared components are pulled in by explicit alias, and a bare `Button`,
// `FrogColours` or `PlayerChipState` in this file always means the shared
// component's.
using Button = Frogs.Unity.UI.Button;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The answer result dialog and the hop that follows it — issue #224,
    /// built to docs/specs/ui/answer-result.md and its two committed 1:1
    /// mockups. Written before <see cref="AnswerResultDialogView"/> exists,
    /// per docs/engineering/testing.md's sanctioned flow: pushed unexecuted,
    /// with CI turning these red before green — there is no editor here to
    /// watch them fail.
    ///
    /// **Nothing in this file lets the dialog decide anything.** Every fixture
    /// hands it an outcome, a before/after position pair and a correct answer
    /// that came out of Core (<see cref="Lane.Resolve"/>, #210), and the
    /// next frog comes out of <see cref="Game.NextActiveFrog"/> (#208). A test
    /// that had to tell the dialog whether an answer was right would be a test
    /// of the wrong thing.
    /// </summary>
    public sealed class AnswerResultDialogViewTests
    {
        const ulong Seed = 20260810UL;

        // The spec page's worked example: `331 × 41 = 13,571`, a hard-pile
        // shape. Written out here because these tests assert the rendered
        // string, comma and all.
        const int Multiplicand = 331;
        const int Multiplier = 41;
        const int CorrectAnswer = 13571;
        const string Equation = "331 × 41 = 13,571";

        [Test]
        public void RightAnswer_DrawsAFilledMark_TheWholeEquation_AndTheRightSentence()
        {
            var turn = Right(FrogColour.Green, before: 3);
            var view = CreateView(turn);

            try
            {
                // A filled disc: the ring and the inside are the same colour,
                // which is what "filled, no border" is once an outline and its
                // fill are two images.
                Assert.That(view.MarkFill.color, Is.EqualTo(view.MarkRing.color));
                Assert.That(view.MarkGlyph.text, Is.EqualTo("✓"));

                Assert.That(view.VerdictText.text, Is.EqualTo(Equation));
                Assert.That(
                    view.ConsequenceText.text,
                    Is.EqualTo("Right! Green hops forward one lily pad."));

                // Every one of those came off the fixture, not out of this
                // view: it was never told the submitted answer, and never
                // asked for one.
                Assert.That(
                    DeclaredMembers(typeof(AnswerResultDialogView))
                        .Where(name => name.IndexOf("Submit", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Grade", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Correct", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty,
                    "nothing is decided here");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void WrongAboveTheStartLog_DrawsARingedMark_NotThisTime_AndRevealsTheAnswerOnly()
        {
            var turn = WrongAt(FrogColour.Green, before: 3);
            var view = CreateView(turn);

            try
            {
                // A ring, not a disc: the inside is the panel's own colour and
                // the ring is not, and it is ResultMarkRingWidth thick. The
                // shape carries the verdict with the palette removed
                // entirely — the invariant that right and wrong are never
                // signalled by colour alone.
                Assert.That(view.MarkFill.color, Is.Not.EqualTo(view.MarkRing.color));
                Assert.That(view.MarkFill.color, Is.EqualTo(Color.white));
                Assert.That(RingThickness(view), Is.EqualTo(AnswerResultDialogView.ResultMarkRingWidth).Within(0.001f));
                Assert.That(view.MarkGlyph.text, Is.EqualTo("✗"));

                Assert.That(view.VerdictText.text, Is.EqualTo("Not this time"));
                Assert.That(
                    view.ConsequenceText.text,
                    Is.EqualTo("331 × 41 = 13,571. Green hops back one lily pad."));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void WrongOnTheStartLog_SaysTheFrogStays_TheSentenceNoMockupDraws()
        {
            var turn = WrongAt(FrogColour.Green, before: 0);
            var view = CreateView(turn);

            try
            {
                Assert.That(turn.Resolution.Outcome, Is.EqualTo(TurnOutcome.WrongOnStartLog));
                Assert.That(
                    view.ConsequenceText.text,
                    Is.EqualTo("331 × 41 = 13,571. Green stays on the Start log."));

                // Same mark, same verdict as any other wrong answer — the
                // floor is a floor, not a special space.
                Assert.That(view.VerdictText.text, Is.EqualTo("Not this time"));
                Assert.That(view.MarkGlyph.text, Is.EqualTo("✗"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Chip_ShowsTheMoveCoreReported_NotAPadCount_FloorCaseIncluded()
        {
            var moved = CreateView(Right(FrogColour.Green, before: 3));
            var stayed = CreateView(WrongAt(FrogColour.Orange, before: 0));

            try
            {
                Assert.That(moved.Chip.PadCountText.text, Is.EqualTo("pad 3 → 4"));
                Assert.That(moved.Chip.Label.text, Is.EqualTo("Green"));
                Assert.That(moved.Chip.Swatch.color, Is.EqualTo(FrogColours.For(FrogColour.Green)));

                // The floor case: before == after, and the same numeric
                // pattern rather than a sentence of its own.
                Assert.That(stayed.Chip.PadCountText.text, Is.EqualTo("pad 0 → 0"));

                // Not the chip's own `3 of 8` — this screen supplies the
                // secondary line, so nothing here counts pads.
                Assert.That(moved.Chip.PadCountText.text, Does.Not.Contain(" of "));
                Assert.That(moved.Chip.State, Is.Not.EqualTo(PlayerChipState.Home),
                    "the Home chip is the board's next ordinary render, not this dialog's");
            }
            finally
            {
                Destroy(moved);
                Destroy(stayed);
            }
        }

        [Test]
        public void Controls_NameTheNextFrog_FromGamesOwnQuery_WhileTheCurrentPlayerIsStillActive()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);
            var resolution = game.LaneFor(game.ActiveFrog).Resolve(game.DrawnCard.Product, game.DrawnCard);
            var turn = new GameAnswerResultTurn(game, resolution);

            var view = CreateView(turn);

            try
            {
                // Green answered; Blue is next. The label is the *next* frog
                // even though Green is still the active one.
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.ResultShown));
                Assert.That(view.NextTurnButton.Label.text, Is.EqualTo("Blue's turn"));

                // And asking for it advanced nothing.
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.ResultShown));

                // One control, and it is never `OK`.
                Assert.That(view.Dialog.Buttons.Count, Is.EqualTo(1));
                Assert.That(view.NextTurnButton.Label.text, Is.Not.EqualTo("OK"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void NextFrogLabel_SkipsAFrogThatIsHome_BecauseCoresQueryDoes()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);

            // Blue is home, so the frog after Green is Orange — Core's own
            // skip-aware answer, not one this dialog walks turn order for.
            var blue = game.LaneFor(FrogColour.Blue);
            for (var step = 0; step < Lane.LaneWinningPosition; step++)
            {
                blue.MoveForward();
            }

            var resolution = game.LaneFor(game.ActiveFrog).Resolve(game.DrawnCard.Product, game.DrawnCard);
            var view = CreateView(new GameAnswerResultTurn(game, resolution));

            try
            {
                Assert.That(view.NextTurnButton.Label.text, Is.EqualTo("Orange's turn"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePanelAndEveryRegion_AreIdenticalBetweenTheRightAndWrongStates()
        {
            var right = CreateView(Right(FrogColour.Green, before: 3));
            var wrong = CreateView(WrongAt(FrogColour.Green, before: 3));

            try
            {
                AssertSameRect(right.Dialog.PanelRect, wrong.Dialog.PanelRect, "panel");
                AssertSameRect(right.MarkRect, wrong.MarkRect, "mark");
                AssertSameRect(right.VerdictText.rectTransform, wrong.VerdictText.rectTransform, "verdict");
                AssertSameRect(right.ConsequenceText.rectTransform, wrong.ConsequenceText.rectTransform, "consequence");
                AssertSameRect(right.ChipRect, wrong.ChipRect, "chip");
                AssertSameRect(right.NextTurnButton.RectTransform, wrong.NextTurnButton.RectTransform, "controls");

                // The panel is the page's own size, centred — "two dialogs
                // that jump about are two dialogs a child reads as two
                // different things happening."
                Assert.That(right.Dialog.PanelRect.rect.width,
                    Is.EqualTo(AnswerResultDialogView.ResultDialogWidth).Within(0.001f));
                Assert.That(right.Dialog.PanelRect.rect.height,
                    Is.EqualTo(AnswerResultDialogView.ResultDialogHeight).Within(0.001f));
                Assert.That(right.Dialog.PanelRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Destroy(right);
                Destroy(wrong);
            }
        }

        [Test]
        public void HardwareBackAndATapOutside_LeaveTheDialogExactlyAsItWas()
        {
            var router = new ScreenRouter();
            router.OpenDialog(Frogs.Core.Dialog.AnswerResult);

            var turn = Right(FrogColour.Green, before: 3);
            var view = CreateView(turn, router: router);

            try
            {
                router.HandleBack();

                Assert.That(router.CurrentDialog, Is.EqualTo(Frogs.Core.Dialog.AnswerResult), "back does not dismiss");
                Assert.That(view.Dialog.IsOpen, Is.True);
                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Waiting), "and starts nothing");
                Assert.That(turn.Calls, Is.Empty, "back does not hand the turn on either");

                // The shared Dialog routes back to whichever button an
                // instance nominates as least destructive. This dialog
                // nominates none — there is nothing less destructive here than
                // its one button.
                Assert.That(view.Dialog.LeastDestructiveButton, Is.Null);

                // The router owns hardware back (#213); a second handler here
                // could only disagree with it.
                Assert.That(
                    DeclaredMembers(typeof(AnswerResultDialogView))
                        .Where(name => name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty,
                    "hardware back is the router's, not this view's");

                // No tap-outside, no close cross.
                Assert.That(
                    view.Dialog.Scrim.GetComponents<MonoBehaviour>()
                        .OfType<IPointerClickHandler>()
                        .ToArray(),
                    Is.Empty,
                    "no tap-outside-to-dismiss");
                Assert.That(
                    DeclaredMembers(typeof(AnswerResultDialogView))
                        .Where(name => name.IndexOf("Dismiss", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void WhileTheDialogIsOpen_TheFrogIsStillDrawnWhereItWas_NotWhereItIsGoing()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue);
            var turn = ResolvedAt(game, startingOn: 3, correct: true);
            var board = CreateBoard(game);
            var lane = board.LaneFor(FrogColour.Green);

            var view = CreateView(turn, board);

            try
            {
                // Core moved the frog at grading time — its lane already says
                // 4, and the board on its own draws whatever Core reports.
                Assert.That(game.LaneFor(FrogColour.Green).Position, Is.EqualTo(4));

                // "The movement is stated before it happens, in words, and the
                // frog then visibly makes that move on the board once this
                // dialog closes." So while the dialog is up, the frog is still
                // drawn where it was.
                AssertPieceAt(lane, 3);
                Assert.That(view.HopProgress, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Destroy(view);
                Destroy(board);
            }
        }

        [Test]
        public void PressingTheButton_Closes_Holds_Hops_ThenHandsOff_InThatOrderAndNotInOneFrame()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue);
            var turn = ResolvedAt(game, startingOn: 3, correct: true);
            var board = CreateBoard(game);
            var view = CreateView(turn, board);

            var handedOff = 0;
            view.TurnHandedOff += () => handedOff++;

            try
            {
                Press(view.NextTurnButton);

                // 1. The dialog closes — the shared Dialog's own fade, not a
                //    second close animation of this view's.
                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Closing));
                Assert.That(view.Dialog.IsOpen, Is.False);
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.HandOff));
                Assert.That(view.HopProgress, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(handedOff, Is.Zero, "not compressed into the press");
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green), "and the turn has not passed yet");

                view.Advance(DialogPanel.DialogFadeDuration);

                // 2. Then the hold, with the panel already gone and the frog
                //    still where it started.
                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Holding));
                Assert.That(view.Dialog.CanvasGroup.alpha, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(view.HopProgress, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(handedOff, Is.Zero);

                view.Advance(AnswerResultDialogView.ResultHopDelay);

                // 3. Then the hop, over FrogHopDuration — and it is a hop, not
                //    a jump: halfway through, the frog is halfway there.
                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Hopping));
                Assert.That(view.HopProgress, Is.EqualTo(0f).Within(0.0001f));

                view.Advance(GameBoardScreenView.FrogHopDuration / 2f);

                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Hopping));
                Assert.That(view.HopProgress, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(handedOff, Is.Zero, "the turn has not passed while the frog is mid-air");
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Green));

                view.Advance(GameBoardScreenView.FrogHopDuration / 2f);

                // 4. Only then does the next player's turn begin.
                Assert.That(view.Stage, Is.EqualTo(AnswerResultHandOffStage.Complete));
                Assert.That(view.HopProgress, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));
                Assert.That(game.Phase, Is.EqualTo(TurnPhase.WaitingToRoll));
                Assert.That(handedOff, Is.EqualTo(1));

                // And it happens once, however long the clock runs on.
                view.Advance(GameBoardScreenView.FrogHopDuration);
                Assert.That(handedOff, Is.EqualTo(1));
                Assert.That(game.ActiveFrog, Is.EqualTo(FrogColour.Blue));

                // The board is back in play through #220's own seam, not
                // through a second one invented here.
                Assert.That(board.RollButton.IsDisabled, Is.False);
                Assert.That(board.TurnBannerText.text, Is.EqualTo("Blue frog's turn"));
            }
            finally
            {
                Destroy(view);
                Destroy(board);
            }
        }

        [Test]
        public void TheHop_InterpolatesBetweenTheBoardsOwnPlacementForTheTwoPositions()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue);
            var turn = ResolvedAt(game, startingOn: 3, correct: true);
            var board = CreateBoard(game);
            var lane = board.LaneFor(FrogColour.Green);

            var view = CreateView(turn, board);

            try
            {
                var from = lane.PositionRects[3].position;
                var to = lane.PositionRects[4].position;

                Press(view.NextTurnButton);
                view.Advance(DialogPanel.DialogFadeDuration + AnswerResultDialogView.ResultHopDelay);

                Assert.That(lane.PieceRect.position.x, Is.EqualTo(from.x).Within(0.01f));

                view.Advance(GameBoardScreenView.FrogHopDuration / 2f);
                Assert.That(
                    lane.PieceRect.position.x,
                    Is.EqualTo((from.x + to.x) / 2f).Within(0.01f),
                    "halfway between the two placements #220 already computes");

                view.Advance(GameBoardScreenView.FrogHopDuration / 2f);
                Assert.That(lane.PieceRect.position.x, Is.EqualTo(to.x).Within(0.01f));

                // Once it lands, the ordinary at-rest render puts it in
                // exactly the same place — there is no separate "landed"
                // state to reconcile.
                AssertPieceAt(lane, 4);
            }
            finally
            {
                Destroy(view);
                Destroy(board);
            }
        }

        [Test]
        public void AHopOntoTheEndLog_JustFinishes_AndTheBoardsOwnRenderShowsTheHomeChip()
        {
            var game = StartedGame(FrogColour.Green, FrogColour.Blue);
            var turn = ResolvedAt(game, startingOn: Lane.LaneWinningPosition - 1, correct: true);
            var board = CreateBoard(game);
            var lane = board.LaneFor(FrogColour.Green);
            var view = CreateView(turn, board);

            try
            {
                Assert.That(game.LaneFor(FrogColour.Green).IsHome, Is.True, "Core's home flag is already set");

                Press(view.NextTurnButton);
                view.Advance(DialogPanel.DialogFadeDuration
                    + AnswerResultDialogView.ResultHopDelay
                    + GameBoardScreenView.FrogHopDuration);

                // Nothing in this issue special-cases the End log: the hop
                // finishes, and #220's next ordinary render is what draws the
                // Home chip.
                AssertPieceAt(lane, Lane.LaneWinningPosition);
                Assert.That(lane.Chip.State, Is.EqualTo(PlayerChipState.Home));
            }
            finally
            {
                Destroy(view);
                Destroy(board);
            }
        }

        [Test]
        public void EveryGeometryAndTimingValue_IsOneOfTheSpecsNamedConstants()
        {
            // docs/specs/ui/answer-result.md § Named constants, and
            // docs/specs/ui/game-board.md's row for the hop.
            Assert.That(AnswerResultDialogView.ResultDialogWidth, Is.EqualTo(1100f));
            Assert.That(AnswerResultDialogView.ResultDialogHeight, Is.EqualTo(620f));
            Assert.That(AnswerResultDialogView.ResultMarkSize, Is.EqualTo(180f));
            Assert.That(AnswerResultDialogView.ResultMarkRingWidth, Is.EqualTo(8f));
            Assert.That(AnswerResultDialogView.ResultMarkGlyphSize, Is.EqualTo(110f));
            Assert.That(AnswerResultDialogView.ResultVerdictSize, Is.EqualTo(76f));
            Assert.That(AnswerResultDialogView.ResultVerdictTop, Is.EqualTo(70f));
            Assert.That(AnswerResultDialogView.ResultConsequenceSize, Is.EqualTo(48f));
            Assert.That(AnswerResultDialogView.ResultConsequenceTop, Is.EqualTo(180f));
            Assert.That(AnswerResultDialogView.ResultTextWidth, Is.EqualTo(760f));
            Assert.That(AnswerResultDialogView.ResultTextLeft, Is.EqualTo(280f));
            Assert.That(AnswerResultDialogView.ResultChipTop, Is.EqualTo(340f));
            Assert.That(AnswerResultDialogView.ResultHopDelay, Is.EqualTo(0.2f));
            Assert.That(GameBoardScreenView.FrogHopDuration, Is.EqualTo(0.4f));

            var view = CreateView(Right(FrogColour.Green, before: 3));

            try
            {
                // Every drawn region measures to one of them, by name.
                Assert.That(view.MarkRect.rect.width, Is.EqualTo(AnswerResultDialogView.ResultMarkSize).Within(0.001f));
                Assert.That(view.MarkRect.rect.height, Is.EqualTo(AnswerResultDialogView.ResultMarkSize).Within(0.001f));
                Assert.That(view.MarkGlyph.fontSize, Is.EqualTo((int)AnswerResultDialogView.ResultMarkGlyphSize));
                Assert.That(view.VerdictText.fontSize, Is.EqualTo((int)AnswerResultDialogView.ResultVerdictSize));
                Assert.That(view.ConsequenceText.fontSize, Is.EqualTo((int)AnswerResultDialogView.ResultConsequenceSize));
                Assert.That(view.ConsequenceText.rectTransform.rect.width,
                    Is.EqualTo(AnswerResultDialogView.ResultTextWidth).Within(0.001f));

                // `mark`, `chip` and `controls` sit on the shared Dialog's own
                // padding — referenced, not redeclared here.
                Assert.That(view.MarkRect.anchoredPosition,
                    Is.EqualTo(new Vector2(DialogPanel.DialogPadding, -DialogPanel.DialogPadding)));
                Assert.That(view.ChipRect.anchoredPosition,
                    Is.EqualTo(new Vector2(DialogPanel.DialogPadding, -AnswerResultDialogView.ResultChipTop)));
                Assert.That(view.VerdictText.rectTransform.anchoredPosition,
                    Is.EqualTo(new Vector2(AnswerResultDialogView.ResultTextLeft, -AnswerResultDialogView.ResultVerdictTop)));
                Assert.That(view.ConsequenceText.rectTransform.anchoredPosition,
                    Is.EqualTo(new Vector2(AnswerResultDialogView.ResultTextLeft, -AnswerResultDialogView.ResultConsequenceTop)));

                // The button is the shared Button at its own size — nothing
                // here overrides ButtonHeight or ButtonMinWidth.
                Assert.That(view.NextTurnButton.RectTransform.rect.height,
                    Is.EqualTo(Button.ButtonHeight).Within(0.001f));
                Assert.That(view.NextTurnButton.RectTransform.rect.width,
                    Is.EqualTo(Button.ButtonMinWidth).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void NothingHere_AddsAudio_OrAWayToShowTheWorking()
        {
            var view = CreateView(WrongAt(FrogColour.Green, before: 3));

            try
            {
                // The celebration question is left exactly as open as the spec
                // leaves it — not even a silent AudioSource waiting for a clip.
                Assert.That(view.GetComponentsInChildren<AudioSource>(includeInactive: true), Is.Empty);
                Assert.That(
                    DeclaredMembers(typeof(AnswerResultDialogView))
                        .Where(name => name.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Clip", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);

                // ADR-0002: the correct answer is revealed; the working is not.
                Assert.That(
                    DeclaredMembers(typeof(AnswerResultDialogView))
                        .Where(name => name.IndexOf("Working", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("Partial", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty);
                Assert.That(view.Dialog.Buttons.Count, Is.EqualTo(1), "one control, and it hands the turn on");
            }
            finally
            {
                Destroy(view);
            }
        }

        // The ring's thickness, measured rather than trusted: the gap between
        // the mark's outer edge and its inside.
        static float RingThickness(AnswerResultDialogView view)
        {
            return LeftEdge(view.MarkFill.rectTransform) - LeftEdge(view.MarkRing.rectTransform);
        }

        static void AssertPieceAt(GameBoardLaneView lane, int position)
        {
            Assert.That(
                lane.PieceRect.position.x,
                Is.EqualTo(lane.PositionRects[position].position.x).Within(0.01f),
                "the frog is drawn on position " + position);
        }

        static void AssertSameRect(RectTransform a, RectTransform b, string what)
        {
            Assert.That(a.anchoredPosition, Is.EqualTo(b.anchoredPosition), what + " anchor");
            Assert.That(a.sizeDelta, Is.EqualTo(b.sizeDelta), what + " size");
            Assert.That(a.anchorMin, Is.EqualTo(b.anchorMin), what + " anchorMin");
            Assert.That(a.anchorMax, Is.EqualTo(b.anchorMax), what + " anchorMax");
            Assert.That(a.pivot, Is.EqualTo(b.pivot), what + " pivot");
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static float LeftEdge(RectTransform rect) => Corners(rect)[0].x;

        static IEnumerable<string> DeclaredMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name);
        }

        static void Press(Button button)
        {
            var corners = Corners(button.RectTransform);
            var eventData = new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        // A game whose active frog has rolled, drawn a card and had its result
        // graded — the exact moment this dialog opens.
        static Game StartedGame(params FrogColour[] turnOrder)
        {
            var game = new Game(turnOrder, Seed);
            game.RollDie();
            game.BeginAnswering();
            game.ShowResult();
            return game;
        }

        // The real path, for the tests that need the board and Core to agree:
        // the active frog starts on <paramref name="startingOn"/>, answers,
        // and Core grades it. Everything the dialog then shows — including the
        // position it hops from — is that one <see cref="TurnResolution"/>.
        static GameAnswerResultTurn ResolvedAt(Game game, int startingOn, bool correct)
        {
            var lane = game.LaneFor(game.ActiveFrog);

            for (var step = 0; step < startingOn; step++)
            {
                lane.MoveForward();
            }

            var card = game.DrawnCard;
            var answer = correct ? card.Product : card.Product + 1;

            return new GameAnswerResultTurn(game, lane.Resolve(answer, card));
        }

        static FakeTurn Right(FrogColour frog, int before)
        {
            return new FakeTurn(
                frog,
                new TurnResolution(TurnOutcome.Correct, before, before + 1, CorrectAnswer));
        }

        // Wrong, graded by Core rather than by this fixture: which of the two
        // wrong outcomes it is depends on where the frog was, and that is
        // Lane's rule, not a test's.
        static FakeTurn WrongAt(FrogColour frog, int before)
        {
            var outcome = before > 0 ? TurnOutcome.WrongAboveStartLog : TurnOutcome.WrongOnStartLog;
            var after = before > 0 ? before - 1 : before;

            return new FakeTurn(frog, new TurnResolution(outcome, before, after, CorrectAnswer));
        }

        static AnswerResultDialogView CreateView(
            IAnswerResultTurn turn,
            GameBoardScreenView board = null,
            ScreenRouter router = null)
        {
            var host = new GameObject(nameof(AnswerResultDialogViewTests), typeof(RectTransform));
            var view = host.AddComponent<AnswerResultDialogView>();
            view.Initialize(turn, board, router);
            return view;
        }

        static GameBoardScreenView CreateBoard(Game game)
        {
            var host = new GameObject(nameof(GameBoardScreenView), typeof(RectTransform));
            var board = host.AddComponent<GameBoardScreenView>();
            board.Initialize(game);
            return board;
        }

        static void Destroy(Component component)
        {
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }

        /// <summary>
        /// A turn whose facts are all fixed up front — the outcome, the
        /// positions and the correct answer came out of Core before the dialog
        /// opened, and the next frog came out of <see cref="Game"/>'s own
        /// query. It records the two hand-off calls in order, and can decide
        /// nothing.
        /// </summary>
        sealed class FakeTurn : IAnswerResultTurn
        {
            readonly List<string> _calls = new List<string>();

            internal FakeTurn(FrogColour frog, TurnResolution resolution)
            {
                Frog = frog;
                Resolution = resolution;
                NextFrog = FrogColour.Blue;
            }

            public FrogColour Frog { get; }

            public int Multiplicand => AnswerResultDialogViewTests.Multiplicand;

            public int Multiplier => AnswerResultDialogViewTests.Multiplier;

            public TurnResolution Resolution { get; }

            public FrogColour NextFrog { get; }

            internal IReadOnlyList<string> Calls => _calls;

            public void BeginHandOff()
            {
                _calls.Add("BeginHandOff");
            }

            public void CompleteHandOff()
            {
                _calls.Add("CompleteHandOff");
            }
        }
    }
}
