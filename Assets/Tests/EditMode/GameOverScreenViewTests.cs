using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity.Views;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameSetupScreenViewTests.cs work
// around — so this is pulled in by explicit alias, and a bare `Button` in
// this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The game over screen — issue #225, built directly against
    /// docs/specs/ui/game-over.md's own "Standings row" element and its own
    /// named constants. Every fact this screen shows is handed to it: the
    /// winner, the standings and their order, and the ended game's roster all
    /// come from <c>core-game-end</c> (#211). Nothing here sorts, ranks,
    /// breaks a tie, or decides who won — the tests below are written so that
    /// a screen which did any of those would fail them.
    ///
    /// There is no score on this screen, on purpose —
    /// docs/specs/ui/game-over.md#invariants: "there is no score. The
    /// classroom game has no score — it has an order." No fixture below
    /// carries a number that is not a place, a pad count, or a geometry
    /// constant.
    /// </summary>
    public sealed class GameOverScreenViewTests
    {
        [Test]
        public void Headline_NamesTheColourCoreReportsAsWinner()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                Assert.That(view.HeadlineText.text, Is.EqualTo("Blue frog wins!"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Headline_NamesTheWinner_OnBothRoutesThatProduceOne()
        {
            // docs/specs/ui/game-over.md#behaviour's route table: the game
            // ending itself, and `End the game` confirmed with somebody
            // already home, read the same — because they are the same fact,
            // "did anyone finish, and if so who was first".
            var everybodyHome = new[]
            {
                new StandingsRow(FrogColour.Pink, 1, Lane.LaneWinningPosition, true),
                new StandingsRow(FrogColour.Green, 2, Lane.LaneWinningPosition, true)
            };

            var endedWithOneHome = new[]
            {
                new StandingsRow(FrogColour.Pink, 1, Lane.LaneWinningPosition, true),
                new StandingsRow(FrogColour.Green, 2, 4, false)
            };

            AssertHeadline(FrogColour.Pink, everybodyHome, "Pink frog wins!");
            AssertHeadline(FrogColour.Pink, endedWithOneHome, "Pink frog wins!");
        }

        [Test]
        public void Headline_ReadsGameOver_WhenCoreReportsNoWinner()
        {
            // "announcing a winner who did not win is worse than announcing
            // nobody" — the game was ended before anybody reached home, so
            // the leading row is not a winner and is not named as one.
            var nobodyHome = new[]
            {
                new StandingsRow(FrogColour.Orange, 1, 6, false),
                new StandingsRow(FrogColour.Blue, 2, 2, false)
            };

            AssertHeadline(null, nobodyHome, "Game over");
        }

        [Test]
        public void Standings_RendersOneRowPerFrog_InTheOrderCoreHandsOver()
        {
            var view = CreateView();

            try
            {
                // Deliberately an order no sort would reproduce: not enum
                // order, not alphabetical, and not pad count ascending or
                // descending. A screen that ranked anything itself would
                // rearrange this.
                var standings = new[]
                {
                    new StandingsRow(FrogColour.Orange, 1, Lane.LaneWinningPosition, true),
                    new StandingsRow(FrogColour.Green, 2, 3, false),
                    new StandingsRow(FrogColour.Pink, 3, 5, false),
                    new StandingsRow(FrogColour.Blue, 4, 7, false)
                };

                view.Show(FrogColour.Orange, standings, MockupRoster());

                Assert.That(view.RowCount, Is.EqualTo(standings.Length));
                Assert.That(view.RowColour(0), Is.EqualTo(FrogColour.Orange));
                Assert.That(view.RowColour(1), Is.EqualTo(FrogColour.Green));
                Assert.That(view.RowColour(2), Is.EqualTo(FrogColour.Pink));
                Assert.That(view.RowColour(3), Is.EqualTo(FrogColour.Blue));

                Assert.That(view.RowPlaceText(0).text, Is.EqualTo("1"));
                Assert.That(view.RowPlaceText(1).text, Is.EqualTo("2"));
                Assert.That(view.RowPlaceText(2).text, Is.EqualTo("3"));
                Assert.That(view.RowPlaceText(3).text, Is.EqualTo("4"));

                Assert.That(view.RowNameText(0).text, Is.EqualTo("Orange"));
                Assert.That(view.RowNameText(3).text, Is.EqualTo("Blue"));

                // Every frog that played is listed — nobody drops off the
                // bottom, and no fifth row appears from nowhere.
                for (var index = 0; index < view.RowCount; index++)
                {
                    Assert.That(view.RowRect(index).gameObject.activeSelf, Is.True);
                }

                Assert.That(view.ActiveRowCount, Is.EqualTo(standings.Length));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Standings_LaysRowsOutTopToBottom_AtTheSpecsOwnGeometry()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                Assert.That(
                    view.StandingsRect.anchoredPosition.y,
                    Is.EqualTo(-GameOverScreenView.StandingsColumnTop));

                for (var index = 0; index < view.RowCount; index++)
                {
                    Assert.That(
                        view.RowRect(index).sizeDelta,
                        Is.EqualTo(new Vector2(GameOverScreenView.StandingsRowWidth, GameOverScreenView.StandingsRowHeight)));
                }

                var pitch = GameOverScreenView.StandingsRowHeight + GameOverScreenView.StandingsRowGap;

                for (var index = 1; index < view.RowCount; index++)
                {
                    var above = view.RowRect(index - 1).anchoredPosition.y;
                    var below = view.RowRect(index).anchoredPosition.y;

                    Assert.That(above - below, Is.EqualTo(pitch).Within(Tolerance));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Progress_ReadsHomeForEveryFinisher_AndAPadCountForEverybodyElse()
        {
            var view = CreateView();

            try
            {
                // Two finishers and one frog still swimming. The second
                // finisher is *not* the winner, and its progress readout is
                // keyed off its own home fact, not off where it sits in the
                // list.
                view.Show(FrogColour.Blue, TwoFinishers(), MockupRoster());

                Assert.That(view.RowProgressText(0).text, Is.EqualTo("Home — 8 of 8"));
                Assert.That(view.RowProgressText(1).text, Is.EqualTo("Home — 8 of 8"));
                Assert.That(view.RowProgressText(2).text, Is.EqualTo("4 of 8"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Progress_CountsToLaneWinningPosition_NotLanePositionCount()
        {
            // docs/specs/ui/game-board.md: LaneWinningPosition "is what the
            // `of 8` in every chip's pad count refers to." LanePositionCount
            // (9) counts the Start log too and would print `of 9`, which the
            // mockup never does.
            var row = new StandingsRow(FrogColour.Green, 2, 6, false);

            Assert.That(
                GameOverScreenView.FormatProgress(row),
                Is.EqualTo("6 of " + Lane.LaneWinningPosition));

            Assert.That(GameOverScreenView.FormatProgress(row), Does.Not.Contain("of " + Lane.LanePositionCount));
        }

        [Test]
        public void OnlyTheWinnersRow_IsDrawnHeavier_EvenWhenAnotherFrogAlsoFinished()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, TwoFinishers(), MockupRoster());

                // Row 0 is Blue — the frog Core names as the winner.
                Assert.That(view.IsWinnerRow(0), Is.True);
                Assert.That(view.RowBorderWidth(0), Is.EqualTo(GameOverScreenView.StandingsWinnerBorder));
                Assert.That(view.RowPlaceText(0).fontStyle, Is.EqualTo(FontStyle.Bold));
                Assert.That(view.RowNameText(0).fontStyle, Is.EqualTo(FontStyle.Bold));

                // Row 1 is Pink — home, but not the winner. A finisher is not
                // automatically a winner: it is drawn exactly like row 2,
                // which never left the pond.
                Assert.That(view.IsWinnerRow(1), Is.False);
                Assert.That(view.RowBorderWidth(1), Is.EqualTo(GameOverScreenView.StandingsRowBorder));
                Assert.That(view.RowPlaceText(1).fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(view.RowNameText(1).fontStyle, Is.EqualTo(FontStyle.Normal));

                Assert.That(view.RowBorderWidth(2), Is.EqualTo(GameOverScreenView.StandingsRowBorder));
                Assert.That(view.RowBorderColour(1), Is.EqualTo(view.RowBorderColour(2)));
                Assert.That(view.RowPlaceText(2).fontStyle, Is.EqualTo(FontStyle.Normal));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void NoRow_IsDrawnHeavier_WhenCoreReportsNoWinner()
        {
            var view = CreateView();

            try
            {
                // The same two finishers, but no winner reported. That
                // combination cannot arise from Core; it is the fixture that
                // proves the heavier treatment is keyed off `Winner` alone
                // and never off a row's own home fact or its position in the
                // list.
                view.Show(null, TwoFinishers(), MockupRoster());

                for (var index = 0; index < view.RowCount; index++)
                {
                    Assert.That(view.IsWinnerRow(index), Is.False, $"row {index} must not be a winner row");
                    Assert.That(view.RowBorderWidth(index), Is.EqualTo(GameOverScreenView.StandingsRowBorder));
                    Assert.That(view.RowPlaceText(index).fontStyle, Is.EqualTo(FontStyle.Normal));
                    Assert.That(view.RowNameText(index).fontStyle, Is.EqualTo(FontStyle.Normal));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TiedFrogs_KeepThePlaceNumbersTheyArriveWith()
        {
            var view = CreateView();

            try
            {
                // docs/specs/ui/game-over.md#open-questions is still open:
                // two frogs on the same pad share a place number. This screen
                // prints what it is given and adds no tiebreak of its own.
                var tied = new[]
                {
                    new StandingsRow(FrogColour.Blue, 1, Lane.LaneWinningPosition, true),
                    new StandingsRow(FrogColour.Pink, 2, 5, false),
                    new StandingsRow(FrogColour.Green, 2, 5, false)
                };

                view.Show(FrogColour.Blue, tied, MockupRoster());

                Assert.That(view.RowPlaceText(0).text, Is.EqualTo("1"));
                Assert.That(view.RowPlaceText(1).text, Is.EqualTo("2"));
                Assert.That(view.RowPlaceText(2).text, Is.EqualTo("2"));

                // And the tied pair keeps the order it arrived in.
                Assert.That(view.RowColour(1), Is.EqualTo(FrogColour.Pink));
                Assert.That(view.RowColour(2), Is.EqualTo(FrogColour.Green));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Controls_PutBackToTheTitleLeft_AndPlayAgainRight_AtTheSharedButtonSize()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                Assert.That(view.BackToTheTitleButton.Label.text, Is.EqualTo("Back to the title"));
                Assert.That(view.BackToTheTitleButton.Kind, Is.EqualTo(Frogs.Unity.UI.ButtonKind.Secondary));

                Assert.That(view.PlayAgainButton.Label.text, Is.EqualTo("Play again"));
                Assert.That(view.PlayAgainButton.Kind, Is.EqualTo(Frogs.Unity.UI.ButtonKind.Primary));

                // The shared component at its own unmodified size — this
                // screen overrides neither.
                var sharedSize = new Vector2(Button.ButtonMinWidth, Button.ButtonHeight);
                Assert.That(view.BackToTheTitleButton.RectTransform.sizeDelta, Is.EqualTo(sharedSize));
                Assert.That(view.PlayAgainButton.RectTransform.sizeDelta, Is.EqualTo(sharedSize));
                Assert.That(view.BackToTheTitleButton.Label.fontSize, Is.EqualTo((int)Button.ButtonLabelSize));

                // Bottom safe-area line, one at each edge.
                var back = view.BackToTheTitleButton.RectTransform;
                var playAgain = view.PlayAgainButton.RectTransform;

                Assert.That(back.anchoredPosition, Is.EqualTo(new Vector2(GameOverScreenView.SafeMargin, GameOverScreenView.SafeMargin)));
                Assert.That(playAgain.anchoredPosition, Is.EqualTo(new Vector2(-GameOverScreenView.SafeMargin, GameOverScreenView.SafeMargin)));
                Assert.That(back.anchorMin.x, Is.LessThan(playAgain.anchorMin.x));

                // Neither button is destructive, so the two sit at ordinary
                // spacing or wider — never the ButtonDestructiveGap that
                // guards a confirm-worthy action, of which this screen has
                // none.
                Assert.That(view.ControlsGap, Is.GreaterThanOrEqualTo(Button.ButtonGap));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PlayAgain_ReusesTheEndedGamesTurnOrder_NotTheStandingsFinishingOrder()
        {
            var view = CreateView();

            try
            {
                var router = new ScreenRouter();
                router.NavigateToScreen(CoreScreen.GameOver);

                var screensVisited = new List<CoreScreen>();
                router.StateChanged += () => screensVisited.Add(router.CurrentScreen);

                view.Initialize(router, () => Seed);

                // The roster and the standings deliberately disagree — which
                // is what happens whenever a frog overtakes another. Turn
                // order is the roster's, always.
                var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
                view.Show(FrogColour.Blue, MockupStandings(), roster);

                TapButton(view.PlayAgainButton);

                Assert.That(view.StartedGame, Is.Not.Null);
                Assert.That(view.StartedGame.TurnOrder, Is.EqualTo(roster));
                Assert.That(view.StartedGame.ActiveFrog, Is.EqualTo(FrogColour.Green));

                // Straight to the board. Game setup is not shown at any point
                // on this path — the whole reason `Play again` exists.
                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
                Assert.That(router.CurrentDialog, Is.Null);
                // Has.No.Member, not Does.Not.Contain: the latter binds to
                // NUnit's substring overload and will not take a Screen.
                Assert.That(screensVisited, Has.No.Member(CoreScreen.GameSetup));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PlayAgain_PutsEveryFrogBackOnItsStartLog()
        {
            var view = CreateView();

            try
            {
                var router = new ScreenRouter();
                view.Initialize(router, () => Seed);

                var roster = new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
                view.Show(FrogColour.Blue, MockupStandings(), roster);

                TapButton(view.PlayAgainButton);

                foreach (var colour in roster)
                {
                    var lane = view.StartedGame.LaneFor(colour);

                    Assert.That(lane.Position, Is.Zero, $"{colour} must start the new game on its Start log");
                    Assert.That(lane.IsHome, Is.False);
                }

                Assert.That(view.StartedGame.IsOver, Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BackToTheTitle_GoesStraightToTheTitleScreen_WithNoConfirmFirst()
        {
            var view = CreateView();

            try
            {
                var router = new ScreenRouter();
                router.NavigateToScreen(CoreScreen.GameOver);

                var dialogsOpened = new List<Dialog>();
                router.StateChanged += () =>
                {
                    if (router.CurrentDialog.HasValue)
                    {
                        dialogsOpened.Add(router.CurrentDialog.Value);
                    }
                };

                view.Initialize(router);
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                TapButton(view.BackToTheTitleButton);

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen));
                Assert.That(dialogsOpened, Is.Empty, "nothing on this screen is destructive, so nothing confirms");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BackToTheTitle_IsTheOneActionTheRoutersOwnGameOverBackCaseAlsoLandsOn()
        {
            // The screen exposes one navigation action; the router's
            // `GameOver` → HandleBack() case (#213) already routes hardware
            // back to the same place. This screen adds no second handler of
            // its own — the two paths below are asserted to agree, not to be
            // two separate implementations.
            var view = CreateView();

            try
            {
                var buttonRouter = new ScreenRouter();
                buttonRouter.NavigateToScreen(CoreScreen.GameOver);
                view.Initialize(buttonRouter);
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());
                view.BackToTheTitle();

                var hardwareBackRouter = new ScreenRouter();
                hardwareBackRouter.NavigateToScreen(CoreScreen.GameOver);
                hardwareBackRouter.HandleBack();

                Assert.That(buttonRouter.CurrentScreen, Is.EqualTo(hardwareBackRouter.CurrentScreen));
                Assert.That(buttonRouter.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen));
                Assert.That(buttonRouter.CurrentDialog, Is.EqualTo(hardwareBackRouter.CurrentDialog));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EnteringReveal_BringsRowsInTopToBottom_CompletingWithinStandingsRevealDuration()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                var slot = GameOverScreenView.StandingsRevealDuration / view.RowCount;

                // Nothing is revealed before any time has passed.
                for (var index = 0; index < view.RowCount; index++)
                {
                    Assert.That(view.RowRevealAlpha(index), Is.EqualTo(0f).Within(Tolerance));
                }

                view.AdvanceReveal(slot);
                Assert.That(view.RowRevealAlpha(0), Is.EqualTo(1f).Within(Tolerance));
                Assert.That(view.RowRevealAlpha(1), Is.EqualTo(0f).Within(Tolerance));

                view.AdvanceReveal(slot);
                Assert.That(view.RowRevealAlpha(1), Is.EqualTo(1f).Within(Tolerance));
                Assert.That(view.RowRevealAlpha(2), Is.EqualTo(0f).Within(Tolerance));

                // The whole sequence is complete at StandingsRevealDuration,
                // not some multiple of a per-row duration.
                view.AdvanceReveal(GameOverScreenView.StandingsRevealDuration - (slot * 2f));

                for (var index = 0; index < view.RowCount; index++)
                {
                    Assert.That(view.RowRevealAlpha(index), Is.EqualTo(1f).Within(Tolerance), $"row {index} must be fully revealed");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void RevealStagger_IsDividedOutOfTheTotal_NotAFixedPerRowDuration()
        {
            // Two rows share the same 0.4 s total that four rows share, so
            // each of the two takes twice as long as each of the four. A
            // per-row literal independent of the total would reveal the first
            // row at the same moment in both.
            var quarter = GameOverScreenView.StandingsRevealDuration / 4f;

            var fourRows = CreateView();
            var twoRows = CreateView();

            try
            {
                fourRows.Show(FrogColour.Blue, MockupStandings(), MockupRoster());
                twoRows.Show(
                    FrogColour.Blue,
                    new[]
                    {
                        new StandingsRow(FrogColour.Blue, 1, Lane.LaneWinningPosition, true),
                        new StandingsRow(FrogColour.Pink, 2, 6, false)
                    },
                    new[] { FrogColour.Blue, FrogColour.Pink });

                fourRows.AdvanceReveal(quarter);
                twoRows.AdvanceReveal(quarter);

                Assert.That(fourRows.RowRevealAlpha(0), Is.EqualTo(1f).Within(Tolerance));
                Assert.That(twoRows.RowRevealAlpha(0), Is.EqualTo(0.5f).Within(Tolerance));
            }
            finally
            {
                Destroy(fourRows);
                Destroy(twoRows);
            }
        }

        [Test]
        public void NamedConstants_CarryTheValuesTheSpecTableStates()
        {
            // docs/specs/ui/game-over.md § Named constants — including the
            // seven rows this issue's PR adds to that table, every one of
            // them a value already drawn in the committed mockup or stated in
            // the page's own Behaviour prose.
            Assert.That(GameOverScreenView.GameOverHeadlineSize, Is.EqualTo(88f));
            Assert.That(GameOverScreenView.GameOverHeadlineTop, Is.EqualTo(64f));
            Assert.That(GameOverScreenView.StandingsRowWidth, Is.EqualTo(1200f));
            Assert.That(GameOverScreenView.StandingsColumnTop, Is.EqualTo(250f));
            Assert.That(GameOverScreenView.StandingsRowHeight, Is.EqualTo(140f));
            Assert.That(GameOverScreenView.StandingsRowPadding, Is.EqualTo(40f));
            Assert.That(GameOverScreenView.StandingsRowInnerGap, Is.EqualTo(32f));
            Assert.That(GameOverScreenView.StandingsRowGap, Is.EqualTo(24f));
            Assert.That(GameOverScreenView.StandingsRowRadius, Is.EqualTo(24f));
            Assert.That(GameOverScreenView.StandingsRowBorder, Is.EqualTo(3f));
            Assert.That(GameOverScreenView.StandingsWinnerBorder, Is.EqualTo(6f));
            Assert.That(GameOverScreenView.StandingsPlaceWidth, Is.EqualTo(80f));
            Assert.That(GameOverScreenView.StandingsPlaceSize, Is.EqualTo(56f));
            Assert.That(GameOverScreenView.StandingsSwatchDiameter, Is.EqualTo(88f));
            Assert.That(GameOverScreenView.StandingsNameSize, Is.EqualTo(52f));
            Assert.That(GameOverScreenView.StandingsProgressSize, Is.EqualTo(44f));
            Assert.That(GameOverScreenView.StandingsRevealDuration, Is.EqualTo(0.4f));
        }

        [Test]
        public void SafeMargin_IsTheSameConstantTheOtherScreensAlreadyName()
        {
            // docs/specs/ui/title-screen.md and docs/specs/ui/game-setup.md
            // both name SafeMargin; this screen's controls row sits on the
            // same line and reuses it rather than declaring a second margin.
            Assert.That(GameOverScreenView.SafeMargin, Is.EqualTo(TitleScreenView.SafeMargin));
            Assert.That(GameOverScreenView.SafeMargin, Is.EqualTo(GameSetupScreenView.SafeMargin));
        }

        [Test]
        public void TheSpecsOwnFixture_RendersTheMockupsFourRows()
        {
            // docs/specs/ui/mockups/game-over.html's own data: Blue home,
            // then 6, 4 and 1 of 8 — the picture this screen is built to.
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());

                Assert.That(view.HeadlineText.text, Is.EqualTo("Blue frog wins!"));
                Assert.That(view.HeadlineText.fontSize, Is.EqualTo((int)GameOverScreenView.GameOverHeadlineSize));
                Assert.That(view.HeadlineRect.anchoredPosition.y, Is.EqualTo(-GameOverScreenView.GameOverHeadlineTop));

                Assert.That(view.RowProgressText(0).text, Is.EqualTo("Home — 8 of 8"));
                Assert.That(view.RowProgressText(1).text, Is.EqualTo("6 of 8"));
                Assert.That(view.RowProgressText(2).text, Is.EqualTo("4 of 8"));
                Assert.That(view.RowProgressText(3).text, Is.EqualTo("1 of 8"));

                Assert.That(view.RowSwatch(0).color, Is.EqualTo(Frogs.Unity.UI.FrogColours.FrogBlue));
                Assert.That(view.RowSwatch(1).color, Is.EqualTo(Frogs.Unity.UI.FrogColours.FrogPink));
                Assert.That(
                    view.RowSwatch(0).rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(GameOverScreenView.StandingsSwatchDiameter, GameOverScreenView.StandingsSwatchDiameter)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ShowingASecondResult_ReplacesTheFirst_AndRestartsTheReveal()
        {
            var view = CreateView();

            try
            {
                view.Show(FrogColour.Blue, MockupStandings(), MockupRoster());
                view.AdvanceReveal(GameOverScreenView.StandingsRevealDuration);

                view.Show(null, new[]
                {
                    new StandingsRow(FrogColour.Green, 1, 5, false),
                    new StandingsRow(FrogColour.Orange, 2, 2, false)
                }, new[] { FrogColour.Green, FrogColour.Orange });

                Assert.That(view.RowCount, Is.EqualTo(2));
                Assert.That(view.ActiveRowCount, Is.EqualTo(2));
                Assert.That(view.HeadlineText.text, Is.EqualTo("Game over"));
                Assert.That(view.RowRevealAlpha(0), Is.EqualTo(0f).Within(Tolerance));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ShowReadsTheThreeFactsOffAnEndedGame_WithoutRecomputingAnyOfThem()
        {
            var view = CreateView();

            try
            {
                var roster = new[] { FrogColour.Pink, FrogColour.Green };
                var game = new Frogs.Core.Game(roster, Seed);

                for (var step = 0; step < Lane.LaneWinningPosition; step++)
                {
                    game.LaneFor(FrogColour.Green).MoveForward();
                }

                game.RecordFinish(FrogColour.Green);
                game.EndGame();

                view.Show(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Green frog wins!"));
                Assert.That(view.RowCount, Is.EqualTo(game.Standings.Count));
                Assert.That(view.RowColour(0), Is.EqualTo(FrogColour.Green));
                Assert.That(view.RowProgressText(0).text, Is.EqualTo("Home — 8 of 8"));
                Assert.That(view.RowProgressText(1).text, Is.EqualTo("0 of 8"));

                // Roster order, not the standings' finishing order — Green
                // finished first but Pink still goes first next game.
                view.Initialize(new ScreenRouter(), () => Seed);
                view.PlayAgain();

                Assert.That(view.StartedGame.TurnOrder, Is.EqualTo(roster));
            }
            finally
            {
                Destroy(view);
            }
        }

        const float Tolerance = 0.0001f;

        // Any fixed value will do — this screen never reads a seed, it only
        // hands one to a brand-new Game.
        const ulong Seed = 20260810UL;

        // docs/specs/ui/mockups/game-over.html's four rows.
        static StandingsRow[] MockupStandings()
        {
            return new[]
            {
                new StandingsRow(FrogColour.Blue, 1, Lane.LaneWinningPosition, true),
                new StandingsRow(FrogColour.Pink, 2, 6, false),
                new StandingsRow(FrogColour.Green, 3, 4, false),
                new StandingsRow(FrogColour.Orange, 4, 1, false)
            };
        }

        // Two frogs home, one still swimming — the fixture that tells a
        // finisher apart from a winner.
        static StandingsRow[] TwoFinishers()
        {
            return new[]
            {
                new StandingsRow(FrogColour.Blue, 1, Lane.LaneWinningPosition, true),
                new StandingsRow(FrogColour.Pink, 2, Lane.LaneWinningPosition, true),
                new StandingsRow(FrogColour.Green, 3, 4, false)
            };
        }

        // Turn order, which is the roster's own order and not the standings'.
        static FrogColour[] MockupRoster()
        {
            return new[] { FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink };
        }

        static void AssertHeadline(FrogColour? winner, IReadOnlyList<StandingsRow> standings, string expected)
        {
            var view = CreateView();

            try
            {
                view.Show(winner, standings, MockupRoster());
                Assert.That(view.HeadlineText.text, Is.EqualTo(expected));
            }
            finally
            {
                Destroy(view);
            }
        }

        static GameOverScreenView CreateView()
        {
            var host = new GameObject(nameof(GameOverScreenViewTests), typeof(RectTransform));
            return host.AddComponent<GameOverScreenView>();
        }

        static void Destroy(GameOverScreenView view)
        {
            if (view != null)
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        static void TapButton(Button button)
        {
            var corners = new Vector3[4];
            button.RectTransform.GetWorldCorners(corners);

            var eventData = new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }
    }
}
