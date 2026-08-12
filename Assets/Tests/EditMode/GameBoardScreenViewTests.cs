using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity.Views;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameSetupScreenViewTests.cs work
// around — so the shared components are pulled in by explicit alias, and a
// bare `Button` in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using FrogColours = Frogs.Unity.UI.FrogColours;
using Image = UnityEngine.UI.Image;
using PlayerChip = Frogs.Unity.UI.PlayerChip;
using PlayerChipState = Frogs.Unity.UI.PlayerChipState;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The game board — issue #220, built to docs/specs/ui/game-board.md and
    /// its committed 1:1 mockup. Written before
    /// <see cref="GameBoardScreenView"/> exists, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted, with
    /// CI turning these red before green — there is no editor here to watch
    /// them fail.
    ///
    /// Every fact about the game — whose turn it is, where each frog sits,
    /// whether a frog is home — is read from a real <see cref="Game"/> driven
    /// into the state under test through its own public API. Nothing here
    /// hands the board a number it could have computed for itself.
    /// </summary>
    public sealed class GameBoardScreenViewTests
    {
        const ulong AnySeed = 20260810UL;

        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        [Test]
        public void TurnBanner_NamesWhicheverFrogCoreReportsActive_AndShowsItsChipActive()
        {
            var game = new Game(new[] { FrogColour.Blue, FrogColour.Green }, AnySeed);
            var view = CreateView(game);

            try
            {
                // Blue is first in turn order, so Core reports Blue active.
                Assert.That(view.TurnBannerText.text, Is.EqualTo("Blue frog's turn"));
                Assert.That(view.TurnBannerChip.Label.text, Is.EqualTo("Blue"));
                Assert.That(view.TurnBannerChip.State, Is.EqualTo(PlayerChipState.Active));
                Assert.That(view.TurnBannerChip.Swatch.color, Is.EqualTo(FrogColours.For(FrogColour.Blue)));

                // Hand the turn on in Core and ask the board again: the
                // banner has to follow Core, not a value baked in at build.
                PassTheTurn(game);
                view.Refresh();

                Assert.That(view.TurnBannerText.text, Is.EqualTo("Green frog's turn"));
                Assert.That(view.TurnBannerChip.Label.text, Is.EqualTo("Green"));
                Assert.That(view.TurnBannerChip.Swatch.color, Is.EqualTo(FrogColours.For(FrogColour.Green)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Header_IsBoardHeaderHeightTall_FullWidth_WithTheBannerLeftAndSettingsRight()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var header = view.HeaderRect;

                Assert.That(header.rect.height, Is.EqualTo(GameBoardScreenView.BoardHeaderHeight).Within(0.001f));
                Assert.That(header.rect.width, Is.EqualTo(CanvasWidth).Within(0.001f), "full width");
                Assert.That(header.anchorMin.y, Is.EqualTo(1f), "pinned to the top");
                Assert.That(header.anchorMax.y, Is.EqualTo(1f));

                Assert.That(view.TurnBannerText.fontSize, Is.EqualTo((int)GameBoardScreenView.TurnBannerSize));

                // The chip at the safe margin, and the words TurnBannerGap
                // past it — the mockup's own 48/256/24, not an approximation
                // composed out of the lane's gutter constants.
                Assert.That(
                    view.TurnBannerChip.RectTransform.anchoredPosition.x,
                    Is.EqualTo(GameBoardScreenView.SafeMargin).Within(0.001f));
                Assert.That(
                    view.TurnBannerText.rectTransform.anchoredPosition.x,
                    Is.EqualTo(GameBoardScreenView.SafeMargin + PlayerChip.PlayerChipWidth + GameBoardScreenView.TurnBannerGap).Within(0.001f));

                Assert.That(
                    view.HeaderHairline.rectTransform.rect.height,
                    Is.EqualTo(GameBoardScreenView.BoardBandOutline).Within(0.001f));

                var settings = view.SettingsButton.RectTransform;
                Assert.That(
                    settings.sizeDelta,
                    Is.EqualTo(new Vector2(GameBoardScreenView.SettingsButtonSize, GameBoardScreenView.SettingsButtonSize)),
                    "a SettingsButtonSize square, not the shared Button's pill");

                // Left of the header, right of the header — measured in world
                // space so anchoring choices cannot fake it.
                Assert.That(CenterX(view.TurnBannerChip.RectTransform), Is.LessThan(CenterX(settings)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Settings_IsAtLeastMinTouchTarget_ByConstruction()
        {
            // SettingsButtonSize (96) already equals MinTouchTarget, so the
            // shared touch-target invariant is met with no extra number.
            Assert.That(GameBoardScreenView.SettingsButtonSize, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(Lane.LaneWinningPosition)]
        public void FrogPiece_SitsOnTheTrackElementCoreReports_NotOnARecomputedOffset(int position)
        {
            var game = TwoFrogGame();
            MoveTo(game, FrogColour.Green, position);

            var view = CreateView(game);

            try
            {
                var lane = view.LaneFor(FrogColour.Green);

                Assert.That(lane.RenderedPosition, Is.EqualTo(position));
                Assert.That(
                    lane.PieceRect.parent,
                    Is.SameAs(lane.PositionRects[position].transform),
                    "the piece is placed onto the track element already drawn for that position");
                Assert.That(lane.PieceRect.anchoredPosition, Is.EqualTo(Vector2.zero), "centred on it");
                Assert.That(
                    lane.PieceRect.sizeDelta,
                    Is.EqualTo(new Vector2(GameBoardLaneView.FrogPieceDiameter, GameBoardLaneView.FrogPieceDiameter)));
                Assert.That(lane.Piece.color, Is.EqualTo(FrogColours.For(FrogColour.Green)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Track_IsSevenLilyPadsBetweenTheTwoSharedLogColumns_AndTheLaneArithmeticLandsOn1920()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var lane = view.LaneFor(FrogColour.Green);
                var positions = lane.PositionRects;

                Assert.That(positions.Count, Is.EqualTo(Lane.LanePositionCount));

                // A lane is still nine positions. Seven of them are its own
                // lily pads; the two on the ends are where this lane crosses a
                // log the whole pond shares, so the lane holds a rect there
                // for its piece to sit on and draws no log of its own.
                var logColumn = new Vector2(GameBoardScreenView.LogWidth, GameBoardLaneView.LaneHeight);
                var padSize = new Vector2(GameBoardLaneView.LilyPadDiameter, GameBoardLaneView.LilyPadDiameter);

                Assert.That(positions[0].sizeDelta, Is.EqualTo(logColumn), "the Start log's column");
                Assert.That(positions[Lane.LaneWinningPosition].sizeDelta, Is.EqualTo(logColumn), "the End log's column");

                Assert.That(
                    positions[0].GetComponent<Image>(),
                    Is.Null,
                    "position 0 draws nothing: the Start log under it belongs to the pond");
                Assert.That(
                    positions[Lane.LaneWinningPosition].GetComponent<Image>(),
                    Is.Null,
                    "position 8 draws nothing: the End log under it belongs to the pond");

                for (var index = 1; index < Lane.LaneWinningPosition; index++)
                {
                    Assert.That(positions[index].sizeDelta, Is.EqualTo(padSize), $"lily pad {index}");
                }

                // Every neighbouring pair is exactly LanePositionGap apart.
                for (var index = 1; index < positions.Count; index++)
                {
                    var previousRightEdge = positions[index - 1].anchoredPosition.x + (positions[index - 1].sizeDelta.x / 2f);
                    var leftEdge = positions[index].anchoredPosition.x - (positions[index].sizeDelta.x / 2f);

                    Assert.That(
                        leftEdge - previousRightEdge,
                        Is.EqualTo(GameBoardLaneView.LanePositionGap).Within(0.001f),
                        $"gap before position {index}");
                }

                // The spec's own arithmetic, carried into the code as a check
                // rather than trusted: two logs, seven pads and eight gaps is
                // 1520 px of track; plus the chip gutter, one gutter gap and
                // two safe margins, 1920 px on the nose.
                var expectedTrackWidth = (2f * GameBoardScreenView.LogWidth)
                    + ((Lane.LanePositionCount - 2) * GameBoardLaneView.LilyPadDiameter)
                    + ((Lane.LanePositionCount - 1) * GameBoardLaneView.LanePositionGap);

                Assert.That(GameBoardLaneView.TrackWidth, Is.EqualTo(expectedTrackWidth).Within(0.001f));
                Assert.That(lane.TrackRect.sizeDelta.x, Is.EqualTo(expectedTrackWidth).Within(0.001f));

                var laneWidth = GameBoardLaneView.LaneGutterWidth
                    + GameBoardLaneView.LaneGutterGap
                    + GameBoardLaneView.TrackWidth
                    + (2f * GameBoardScreenView.SafeMargin);

                Assert.That(laneWidth, Is.EqualTo(CanvasWidth).Within(0.001f), "the spec's 1920 px on the nose");
                Assert.That(lane.RectTransform.rect.height, Is.EqualTo(GameBoardLaneView.LaneHeight).Within(0.001f));

                // chip pinned left of the safe area, track pinned right of it.
                Assert.That(lane.TrackRect.anchorMin.x, Is.EqualTo(1f).Within(0.001f), "track pinned right");
                Assert.That(lane.TrackRect.pivot.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(lane.Chip.RectTransform.anchorMin.x, Is.EqualTo(0f).Within(0.001f), "chip pinned left");
                Assert.That(lane.Chip.RectTransform.pivot.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(lane.Chip.RectTransform.rect.width, Is.EqualTo(GameBoardLaneView.LaneGutterWidth).Within(0.001f));

                // The lane draws seven things and no more — the logs are the
                // pond's now, so there is no eighth or ninth element here to
                // walk.
                Assert.That(
                    lane.LilyPadFills.Count,
                    Is.EqualTo(Lane.LanePositionCount - 2),
                    "seven lily pads — everything else on this lane's track is somebody else's drawing");
                Assert.That(lane.LilyPadOutlines.Count, Is.EqualTo(Lane.LanePositionCount - 2));

                // Outlines are drawn inside each element's own bounds, so
                // they cost the 1520 px track nothing.
                for (var index = 0; index < lane.LilyPadFills.Count; index++)
                {
                    var position = index + 1;
                    var fill = lane.LilyPadFills[index].rectTransform;

                    Assert.That(lane.LilyPadOutlines[index].rectTransform, Is.SameAs(positions[position]));
                    Assert.That(fill.parent, Is.SameAs(positions[position].transform));
                    Assert.That(
                        fill.offsetMin,
                        Is.EqualTo(new Vector2(GameBoardLaneView.TrackOutline, GameBoardLaneView.TrackOutline)),
                        $"position {position}'s fill is inset by TrackOutline");
                    Assert.That(
                        fill.offsetMax,
                        Is.EqualTo(new Vector2(-GameBoardLaneView.TrackOutline, -GameBoardLaneView.TrackOutline)));
                }

                Assert.That(
                    lane.Piece.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(GameBoardLaneView.FrogPieceOutline, GameBoardLaneView.FrogPieceOutline)),
                    "the piece's fill is inset by FrogPieceOutline, so the piece is still FrogPieceDiameter across");
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// The whole of #296, in one count: the board draws **two** logs,
        /// however many frogs are playing. It used to draw a pair inside every
        /// lane, so a four-frog game drew eight.
        /// </summary>
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Pond_DrawsOneStartLogAndOneEndLogForTheWholeBoard_NotOnePairPerLane(int frogCount)
        {
            var view = CreateView(new Game(AllColours.Take(frogCount).ToArray(), AnySeed));

            try
            {
                var logs = view
                    .GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.name)
                    .Where(name => name == "StartLog" || name == "EndLog")
                    .ToArray();

                Assert.That(
                    logs.Length,
                    Is.EqualTo(2),
                    $"a {frogCount}-frog game draws two logs, not {2 * frogCount}");
                Assert.That(logs, Is.Unique, "one Start log and one End log, not two of either");

                // They belong to the pond, not to a lane — which is the reason
                // there are two of them rather than two per frog.
                Assert.That(view.StartLogOutline.transform.parent, Is.SameAs(view.PondRect.transform));
                Assert.That(view.EndLogOutline.transform.parent, Is.SameAs(view.PondRect.transform));

                // And they are drawn before the lanes are, so every frog sits
                // on top of the log rather than under it.
                foreach (var lane in view.Lanes)
                {
                    Assert.That(
                        lane.RectTransform.GetSiblingIndex(),
                        Is.GreaterThan(view.StartLogOutline.rectTransform.GetSiblingIndex()),
                        "the lanes, and so the frogs, are drawn over the logs");
                    Assert.That(
                        lane.RectTransform.GetSiblingIndex(),
                        Is.GreaterThan(view.EndLogOutline.rectTransform.GetSiblingIndex()));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// Derek's answer to game-board.md's first open question, on #296: the
        /// log spans the **full pond**, not the lanes in play. So its height is
        /// one number rather than three, and it is the same at two frogs as at
        /// four — which is the visible difference from the mockup's proposal.
        /// </summary>
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SharedLogs_FillThePondBand_AndAreTheSameHeightAtEveryFrogCount(int frogCount)
        {
            var view = CreateView(new Game(AllColours.Take(frogCount).ToArray(), AnySeed));

            try
            {
                var expected = new Vector2(GameBoardScreenView.LogWidth, GameBoardScreenView.SharedLogHeight);

                Assert.That(view.StartLogOutline.rectTransform.sizeDelta, Is.EqualTo(expected));
                Assert.That(view.EndLogOutline.rectTransform.sizeDelta, Is.EqualTo(expected));

                Assert.That(
                    GameBoardScreenView.SharedLogHeight,
                    Is.EqualTo(view.PondRect.rect.height).Within(0.001f),
                    "the log fills the pond band, edge to edge with the two hairlines");
                Assert.That(
                    GameBoardScreenView.SharedLogHeight,
                    Is.Not.EqualTo(frogCount * GameBoardLaneView.LaneHeight).Within(0.001f),
                    "full pond, not LaneCount x LaneHeight — the option Derek chose over the mockup's");

                // Vertically centred on the pond, so it is centred on the lane
                // stack the pond centres too.
                Assert.That(view.StartLogOutline.rectTransform.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(view.EndLogOutline.rectTransform.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));

                // The rim that separates a log from the water it floats on is
                // drawn inside the log's own bounds, as every outline on this
                // screen is.
                foreach (var fill in new[] { view.StartLogFill, view.EndLogFill })
                {
                    Assert.That(fill.rectTransform.parent.name, Is.AnyOf("StartLog", "EndLog"));
                    Assert.That(
                        fill.rectTransform.offsetMin,
                        Is.EqualTo(new Vector2(GameBoardLaneView.TrackOutline, GameBoardLaneView.TrackOutline)));
                    Assert.That(
                        fill.rectTransform.offsetMax,
                        Is.EqualTo(new Vector2(-GameBoardLaneView.TrackOutline, -GameBoardLaneView.TrackOutline)));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// The invariant the sharing could have broken, drawn rather than
        /// asserted in prose: two frogs on the Start log are not sharing a
        /// space. Each is on position 0 *of its own lane*, on its own lane's
        /// centre line — and that line crosses the shared log, which is what
        /// makes it a log they are both standing on.
        /// </summary>
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void FrogOnASharedLog_SitsOnItsOwnLanesCentreLine_OverTheLogThePondShares(int frogCount)
        {
            var roster = AllColours.Take(frogCount).ToArray();
            var game = new Game(roster, AnySeed);

            // Every frog starts on the Start log; sending the last one home
            // puts a frog on each of the two shared logs at once.
            var homeFrog = roster[roster.Length - 1];
            MoveTo(game, homeFrog, Lane.LaneWinningPosition);

            var view = CreateView(game);

            try
            {
                foreach (var colour in roster)
                {
                    var lane = view.LaneFor(colour);
                    var log = colour == homeFrog ? view.EndLogOutline : view.StartLogOutline;

                    Assert.That(
                        CenterX(lane.PieceRect),
                        Is.EqualTo(CenterX(log.rectTransform)).Within(0.001f),
                        $"{colour} is in the shared log's column");
                    Assert.That(
                        CenterY(lane.PieceRect),
                        Is.EqualTo(CenterY(lane.RectTransform)).Within(0.001f),
                        $"{colour} sits on its own lane's centre line, not clustered with the others");

                    var corners = new Vector3[4];
                    log.rectTransform.GetWorldCorners(corners);

                    Assert.That(
                        CenterY(lane.PieceRect),
                        Is.InRange(corners[0].y, corners[1].y),
                        $"{colour}'s lane centre line crosses the shared log");
                }

                // Distinct lines, not one: the frogs are on the same drawing
                // and visibly not in the same place.
                var lines = view.Lanes.Select(lane => CenterY(lane.PieceRect)).ToArray();
                Assert.That(lines, Is.Unique, "one line per lane");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ChipPadCount_ReadsPositionFromCore_AndTheEightFromLaneWinningPosition()
        {
            var game = TwoFrogGame();
            MoveTo(game, FrogColour.Green, 3);

            var view = CreateView(game);

            try
            {
                Assert.That(view.LaneFor(FrogColour.Green).Chip.PadCountText.text, Is.EqualTo("3 of 8"));
                Assert.That(view.LaneFor(FrogColour.Blue).Chip.PadCountText.text, Is.EqualTo("0 of 8"));

                // The denominator is Lane's own constant, never a literal the
                // board keeps a second copy of.
                Assert.That(Lane.LaneWinningPosition, Is.EqualTo(8));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void FrogCoreReportsHome_RestsOnTheEndLog_WithItsChipInTheHomeState()
        {
            var game = TwoFrogGame();
            MoveTo(game, FrogColour.Blue, Lane.LaneWinningPosition);

            var view = CreateView(game);

            try
            {
                var lane = view.LaneFor(FrogColour.Blue);

                Assert.That(game.LaneFor(FrogColour.Blue).IsHome, Is.True, "the fixture, not the assertion");
                Assert.That(
                    lane.PieceRect.parent,
                    Is.SameAs(lane.PositionRects[Lane.LaneWinningPosition].transform),
                    "resting on the End log");
                Assert.That(lane.Chip.State, Is.EqualTo(PlayerChipState.Home));
                Assert.That(lane.Chip.PadCountText.text, Is.EqualTo("Home!"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Pond_LaysOutOneLanePerFrog_WithTheGroupVerticallyCentred(int frogCount)
        {
            var roster = AllColours.Take(frogCount).ToArray();
            var view = CreateView(new Game(roster, AnySeed));

            try
            {
                Assert.That(view.Lanes.Count, Is.EqualTo(frogCount), "one lane per frog in the game — no placeholders");
                Assert.That(view.Lanes.Select(lane => lane.Colour), Is.EqualTo(roster), "in turn order");

                var pond = view.PondRect;
                Assert.That(
                    pond.rect.height,
                    Is.EqualTo(CanvasHeight - GameBoardScreenView.BoardHeaderHeight - GameBoardScreenView.BoardControlsHeight).Within(0.001f),
                    "pond is everything between the two pinned bands, with no gaps");

                var ys = view.Lanes.Select(lane => lane.RectTransform.anchoredPosition.y).ToArray();

                for (var index = 1; index < ys.Length; index++)
                {
                    Assert.That(ys[index], Is.LessThan(ys[index - 1]), "lanes stack downward in turn order");
                    Assert.That(
                        ys[index - 1] - ys[index],
                        Is.EqualTo(GameBoardLaneView.LaneHeight).Within(0.001f),
                        "LaneHeight per lane, stacked with no gaps");
                }

                // Centred as a group within the pond band, not top-pinned:
                // the first lane's top edge and the last lane's bottom edge
                // are the same distance from the pond's centre.
                var topEdge = ys[0] + (GameBoardLaneView.LaneHeight / 2f);
                var bottomEdge = ys[ys.Length - 1] - (GameBoardLaneView.LaneHeight / 2f);

                Assert.That(topEdge + bottomEdge, Is.EqualTo(0f).Within(0.001f), "vertically centred in the pond");

                if (frogCount < 4)
                {
                    var topPinnedY = (pond.rect.height / 2f) - (GameBoardLaneView.LaneHeight / 2f);
                    Assert.That(ys[0], Is.Not.EqualTo(topPinnedY).Within(0.001f), "not clinging to the top");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Roll_StartsEnabledOnAFreshBoard_AndStaysEnabledUntilFirstPressed()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                Assert.That(view.RollButton.IsDisabled, Is.False, "entering from game setup, Roll is enabled");

                view.Refresh();
                Assert.That(view.RollButton.IsDisabled, Is.False, "redrawing the board does not disable it");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Roll_FiresExactlyOnceAndDisablesImmediately_SoADoubleTapCannotRollTwice()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var rolls = 0;
                view.RollPressed += () => rolls++;

                TapButton(view.RollButton);

                Assert.That(rolls, Is.EqualTo(1));
                Assert.That(view.RollButton.IsDisabled, Is.True, "disabled the instant the press resolves");

                TapButton(view.RollButton);

                Assert.That(rolls, Is.EqualTo(1), "a second press before the turn resolves does nothing");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Roll_BecomesInteractableAgain_OnlyOnTheTurnResolvedSignal()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var rolls = 0;
                view.RollPressed += () => rolls++;

                TapButton(view.RollButton);
                Assert.That(view.RollButton.IsDisabled, Is.True);

                view.NotifyTurnResolved();

                Assert.That(view.RollButton.IsDisabled, Is.False, "re-enabled by the signal, not by a timer");

                TapButton(view.RollButton);
                Assert.That(rolls, Is.EqualTo(2));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Controls_IsBoardControlsHeightTall_WithRollOversizedAndCentred()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var controls = view.ControlsRect;

                Assert.That(controls.rect.height, Is.EqualTo(GameBoardScreenView.BoardControlsHeight).Within(0.001f));
                Assert.That(controls.rect.width, Is.EqualTo(CanvasWidth).Within(0.001f), "full width");
                Assert.That(controls.anchorMin.y, Is.EqualTo(0f), "pinned to the bottom");
                Assert.That(controls.anchorMax.y, Is.EqualTo(0f));

                var roll = view.RollButton;

                Assert.That(
                    roll.RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(GameBoardScreenView.RollButtonWidth, GameBoardScreenView.RollButtonHeight)),
                    "primary, oversized — game-board.md's own named override of the shared footprint");
                Assert.That(roll.RectTransform.sizeDelta.x, Is.Not.EqualTo(Button.ButtonMinWidth));
                Assert.That(roll.RectTransform.sizeDelta.y, Is.Not.EqualTo(Button.ButtonHeight));
                Assert.That(roll.Label.fontSize, Is.EqualTo((int)GameBoardScreenView.RollButtonLabelSize));
                Assert.That(roll.Label.text, Is.EqualTo("Roll"));
                Assert.That(roll.Kind, Is.EqualTo(ButtonKind.Primary));
                Assert.That(roll.RectTransform.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f), "centred");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Settings_FiresOnEveryTurnPhase_AndIsNeverDisabledByTurnState()
        {
            var game = TwoFrogGame();
            var view = CreateView(game);

            try
            {
                var opened = 0;
                view.SettingsRequested += () => opened++;

                // Every phase of a turn, including the ones representing "not
                // this player's local action" — hiding the way out of a game
                // "would be worse".
                var phases = new List<TurnPhase>();

                phases.Add(game.Phase);
                TapSettings(view);

                game.RollDie();
                view.Refresh();
                phases.Add(game.Phase);
                TapSettings(view);

                game.BeginAnswering();
                view.Refresh();
                phases.Add(game.Phase);
                TapSettings(view);

                game.ShowResult();
                view.Refresh();
                phases.Add(game.Phase);
                TapSettings(view);

                game.BeginHandOff();
                view.Refresh();
                phases.Add(game.Phase);
                TapSettings(view);

                Assert.That(phases, Is.Unique, "the five phases really were distinct");
                Assert.That(opened, Is.EqualTo(phases.Count), "available on any turn, at any time");

                // And still available while Roll itself is disabled mid-turn.
                TapButton(view.RollButton);
                Assert.That(view.RollButton.IsDisabled, Is.True);

                TapSettings(view);
                Assert.That(opened, Is.EqualTo(phases.Count + 1));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_InvokesTheSameOpenSettingsCallbackAsTheGear_AndNeverQuits()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                var opened = 0;
                view.SettingsRequested += () => opened++;

                TapSettings(view);
                Assert.That(opened, Is.EqualTo(1));

                view.HandleHardwareBack();

                Assert.That(opened, Is.EqualTo(2), "back goes through the same open-settings callback the gear uses");
                Assert.That(view.gameObject.activeInHierarchy, Is.True, "the board is still showing");

                // "It does not quit, and it never quits without the confirm."
                // Asserted structurally as well as behaviourally: the board
                // owns no exit path at all for a future edit to reach for.
                var quitLike = DeclaredMembers(typeof(GameBoardScreenView))
                    .Concat(DeclaredMembers(typeof(GameBoardLaneView)))
                    .Where(name => name.IndexOf("Quit", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

                Assert.That(quitLike, Is.Empty, "the board has no quit or exit path of its own");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryGeometryValue_IsANamedConstantFromGameBoardsThreeTables()
        {
            var boardConstants = new Dictionary<string, float>
            {
                { "SafeMargin", 48f },
                { "BoardHeaderHeight", 128f },
                { "BoardControlsHeight", 176f },
                { "BoardBandOutline", 3f },
                { "TurnBannerSize", 52f },
                { "TurnBannerGap", 24f },
                { "SettingsButtonSize", 96f },
                { "SettingsGlyphSize", 44f },
                { "SettingsButtonOutline", 4f },
                { "RollButtonWidth", 480f },
                { "RollButtonHeight", 144f },
                { "RollButtonLabelSize", 56f },

                // Added to game-board.md's table by #224, which is the first
                // issue that turns it into code. It was already named in that
                // page's own Behaviour prose — "the move is animated on this
                // screen, after the result dialog closes, over
                // FrogHopDuration (0.4 s)" — so this is the table catching up
                // with the page, not a new number. The value is the board's
                // because the hop happens here; running it is #224's, and
                // Board_HasNoHopAnimation_AndNoEndOfGameDetection still holds
                // this screen to playing none of it itself.
                { "FrogHopDuration", 0.4f },

                // game-board.md's third table — the two shared logs. They
                // belong to the pond rather than to a lane (#296), so their
                // constants live with the pond's, and `LogHeight` (120 px) is
                // gone rather than renamed: it was the height of a log sized
                // to sit inside one 184 px lane, and there is no such thing on
                // this board any more. `SharedLogHeight` is a flat 896 px, the
                // pond band's own height — Derek's answer to that page's first
                // open question, on #296 — not `LaneCount x LaneHeight`.
                { "LogWidth", 176f },
                { "SharedLogHeight", 896f },
                { "LogRadius", 24f }
            };

            var laneConstants = new Dictionary<string, float>
            {
                { "LaneHeight", 184f },
                { "LilyPadDiameter", 112f },
                { "FrogPieceDiameter", 88f },
                { "FrogPieceOutline", 4f },
                { "TrackOutline", 3f },
                { "LanePositionGap", 48f },
                { "LaneGutterWidth", 256f },
                { "LaneGutterGap", 48f }
            };

            AssertPublicConstantsAreExactly(typeof(GameBoardScreenView), boardConstants);
            AssertPublicConstantsAreExactly(typeof(GameBoardLaneView), laneConstants);

            // LogRadius and SettingsGlyphSize hold the same numbers as
            // shared-components.md's ButtonRadius and ButtonLabelSize today,
            // and are deliberately not those constants: the Button is free to
            // restyle its corner and its label without moving the pond's logs
            // or its gear. Asserted as equal-today so a future divergence is
            // a visible, deliberate edit here rather than a silent drift.
            Assert.That(GameBoardScreenView.LogRadius, Is.EqualTo(Button.ButtonRadius));
            Assert.That(GameBoardScreenView.SettingsGlyphSize, Is.EqualTo(Button.ButtonLabelSize));

            // SharedLogHeight is the pond band's own height, not a number
            // typed in beside it — the log fills the band, so if the header or
            // the controls band ever changes, the log follows without anybody
            // remembering to.
            Assert.That(
                GameBoardScreenView.SharedLogHeight,
                Is.EqualTo(CanvasHeight
                    - GameBoardScreenView.BoardHeaderHeight
                    - GameBoardScreenView.BoardControlsHeight).Within(0.001f));

            // The remaining two of game-board.md's constants are Lane's own,
            // reused under the same name rather than redeclared here.
            Assert.That(Lane.LanePositionCount, Is.EqualTo(9));
            Assert.That(Lane.LaneWinningPosition, Is.EqualTo(8));

            foreach (var type in new[] { typeof(GameBoardScreenView), typeof(GameBoardLaneView) })
            {
                foreach (var name in new[] { "LanePositionCount", "LaneWinningPosition" })
                {
                    Assert.That(
                        type.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                        Is.Null,
                        $"{type.Name} must reference Lane.{name}, not redeclare it");
                }
            }
        }

        [Test]
        public void Board_HasNoHopAnimation_AndNoEndOfGameDetection()
        {
            // Running the hop is #224's and the game-over transition is
            // #225's. This board draws every frog at rest, at whatever
            // position Core reports, and keeps rendering whatever Core reports
            // for as long as it is shown. This is the reviewer's grep, as a
            // test.
            var forbidden = new[]
            {
                "ResultHopDelay", "Hop", "Tween", "Coroutine",
                "Animate", "Animation", "Duration", "Delay",
                "Elapsed", "Progress", "Advance",
                "GameOver", "IsOver", "Winner", "Standings", "FinishingOrder"
            };

            // One exemption, and only this exact name. `FrogHopDuration` is a
            // row on game-board.md's own constants table — the hop happens on
            // this screen, so the value is this page's — and #224 references
            // it rather than declaring a second copy of another page's number.
            // It is a bare `const float` that nothing here reads. What this
            // test is really about is that no *clock* lives on the board, and
            // the words above plus the coroutine check below still forbid one.
            var exempt = new[] { "FrogHopDuration" };

            foreach (var type in new[] { typeof(GameBoardScreenView), typeof(GameBoardLaneView) })
            {
                foreach (var member in DeclaredMembers(type))
                {
                    if (exempt.Contains(member))
                    {
                        continue;
                    }

                    foreach (var word in forbidden)
                    {
                        Assert.That(
                            member.IndexOf(word, StringComparison.OrdinalIgnoreCase),
                            Is.LessThan(0),
                            $"{type.Name}.{member} looks like {word} — that belongs to a later issue");
                    }
                }

                var coroutines = type
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
                    .Select(method => method.Name)
                    .ToArray();

                Assert.That(coroutines, Is.Empty, $"{type.Name} starts no coroutine — nothing here moves on its own");
            }
        }

        [Test]
        public void Board_AddsNoPlaceholderLanes_AndNoLastRollReadout()
        {
            // Both of game-board.md's open questions are left exactly as open
            // as it leaves them.
            var view = CreateView(new Game(new[] { FrogColour.Green, FrogColour.Pink }, AnySeed));

            try
            {
                Assert.That(view.Lanes.Count, Is.EqualTo(2), "only the lanes in play are drawn");

                var lastRollLike = DeclaredMembers(typeof(GameBoardScreenView))
                    .Where(name => name.IndexOf("LastRoll", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

                Assert.That(lastRollLike, Is.Empty, "no last-roll readout, no placeholder lane");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Board_WiresItsBuiltInSpritesAndFonts_RatherThanLeavingThemMissing()
        {
            var view = CreateView(TwoFrogGame());

            try
            {
                Assert.That(view.TurnBannerText.font, Is.Not.Null);
                Assert.That(view.SettingsButton.Glyph.font, Is.Not.Null);
                Assert.That(view.SettingsButton.Background.sprite, Is.Not.Null);
                Assert.That(view.SettingsButton.Outline.sprite, Is.Not.Null);
                Assert.That(view.SettingsButton.Glyph.fontSize, Is.EqualTo((int)GameBoardScreenView.SettingsGlyphSize));

                var lane = view.LaneFor(FrogColour.Green);
                Assert.That(lane.Piece.sprite, Is.Not.Null);
                Assert.That(lane.PieceOutline.sprite, Is.Not.Null);
                Assert.That(lane.LilyPadFills[0].sprite, Is.Not.Null);
                Assert.That(lane.LilyPadOutlines[0].sprite, Is.Not.Null);

                Assert.That(view.StartLogFill.sprite, Is.Not.Null);
                Assert.That(view.StartLogOutline.sprite, Is.Not.Null);
                Assert.That(view.EndLogFill.sprite, Is.Not.Null);
                Assert.That(view.EndLogOutline.sprite, Is.Not.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        static readonly FrogColour[] AllColours =
        {
            FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink
        };

        static Game TwoFrogGame()
        {
            return new Game(new[] { FrogColour.Green, FrogColour.Blue }, AnySeed);
        }

        // Drives a real Lane to `position` through its own public API — the
        // board is never handed a position it could not have read off Core.
        static void MoveTo(Game game, FrogColour colour, int position)
        {
            var lane = game.LaneFor(colour);

            for (var step = 0; step < position; step++)
            {
                lane.MoveForward();
            }

            Assert.That(lane.Position, Is.EqualTo(position), "the fixture, not the assertion");
        }

        static void PassTheTurn(Game game)
        {
            game.RollDie();
            game.BeginAnswering();
            game.ShowResult();
            game.BeginHandOff();
            game.CompleteHandOff();
        }

        static IEnumerable<string> DeclaredMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name);
        }

        static void AssertPublicConstantsAreExactly(Type type, IDictionary<string, float> expected)
        {
            var constants = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                constants.Select(field => field.Name).OrderBy(name => name),
                Is.EqualTo(expected.Keys.OrderBy(name => name)),
                $"{type.Name}'s public constants are exactly game-board.md's own, under the identical names");

            foreach (var field in constants)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(expected[field.Name]).Within(0.001f),
                    $"{type.Name}.{field.Name}");
            }
        }

        static float CenterX(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0].x + corners[2].x) / 2f;
        }

        static float CenterY(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0].y + corners[2].y) / 2f;
        }

        static GameBoardScreenView CreateView(Game game)
        {
            var host = new GameObject(nameof(GameBoardScreenViewTests), typeof(RectTransform));
            var view = host.AddComponent<GameBoardScreenView>();
            view.Initialize(game);
            return view;
        }

        static void TapButton(Button button)
        {
            var eventData = EventDataAt(button.RectTransform);

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static void TapSettings(GameBoardScreenView view)
        {
            var target = view.SettingsButton;
            var eventData = EventDataAt(target.RectTransform);

            target.OnPointerDown(eventData);
            target.OnPointerUp(eventData);
        }

        static PointerEventData EventDataAt(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };
        }

        static void Destroy(GameBoardScreenView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
