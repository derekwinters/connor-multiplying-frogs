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
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// EndGameConfirmViewTests.cs and ButtonTests.cs work around — so these are
// pulled in by explicit alias, and a bare `Button`, `ButtonKind`,
// `DialogPanel`, `BoardColours` or `FrogColours` in this file always means the
// shared component's.
using BoardColours = Frogs.Unity.UI.BoardColours;
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The dialog that says a player has won — issue #329, built against
    /// docs/specs/ui/player-won.md and its two committed 1:1 mockups.
    ///
    /// Four things these tests hold down matter more than the layout:
    ///
    /// - **Only the first frog home wins.** The headline's two wordings are
    ///   chosen by Core's own <c>Game.Winner</c>, and the later wording is
    ///   asserted for a frog that arrived second, not merely for a frog that
    ///   is not the winner by construction.
    /// - **The dialog changes nothing.** No frog moves, no turn advances, no
    ///   game ends across an opening and a press.
    /// - **The last frog home still reaches the standings.** The button hands
    ///   to game over on the one turn where there is no next player.
    /// - **Every layout number is player-won.md's own, under its own name.**
    ///   A number the spec does not have is a layout that was not agreed.
    /// </summary>
    public sealed class PlayerWonDialogViewTests
    {
        const ulong Seed = 329UL;

        [Test]
        public void TheFirstFrogHome_Wins_AndTheButtonNamesWhoeverIsNext()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                view.Initialize(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Green wins!"));
                Assert.That(view.HandOnButton.Label.text, Is.EqualTo("Blue's turn"));
                Assert.That(view.HandOnButton.Label.text, Is.Not.EqualTo("OK"));
                Assert.That(view.Dialog.IsOpen, Is.True);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ALaterFrogHome_IsHome_RatherThanWins()
        {
            // "Saying 'wins' four times in one game would be four lies after
            // the first." Green arrives first, Pink second — so Pink reads the
            // other wording while Green's own dialog read the first.
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Pink);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                view.Initialize(game);
                Assert.That(view.HeadlineText.text, Is.EqualTo("Green wins!"));

                PassTurnsUntil(game, FrogColour.Pink);
                LandHome(game, FrogColour.Pink);
                view.Initialize(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Pink is home!"));
                Assert.That(game.Winner, Is.EqualTo(FrogColour.Green), "the first arrival still owns the win");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheHeadlineUsesTheNameTyped_NotAlwaysTheColour()
        {
            // player-won.md § Elements: "The frog's name is whatever it is
            // called — its colour by default, or the name typed on game setup,
            // so `Connor wins!` for a renamed frog."
            var game = new Game(
                new[]
                {
                    new RosterEntry(FrogColour.Blue, "Connor"),
                    new RosterEntry(FrogColour.Pink)
                },
                Seed);

            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Blue);
                view.Initialize(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Connor wins!"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheLastFrogHome_HandsToTheResults_BecauseThereIsNoNextPlayer()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                LandHome(game, FrogColour.Blue);

                Assert.That(game.IsOver, Is.True, "every frog is home");

                view.Initialize(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Blue is home!"));
                Assert.That(view.HandOnButton.Label.text, Is.EqualTo("See the results"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheButtonHandsOn_Once_HoweverManyTimesItIsPressed()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                var handedOn = 0;
                view.HandedOn += () => handedOn++;

                LandHome(game, FrogColour.Green);
                view.Initialize(game);

                TapButton(view.HandOnButton);
                TapButton(view.HandOnButton);

                Assert.That(handedOn, Is.EqualTo(1), "one arrival is handed on once");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void OpeningAndPressing_ChangesNothingAboutTheGame()
        {
            // player-won.md's third invariant: "this dialog changes nothing
            // about the game. It does not end it, does not skip anyone, does
            // not reorder anything."
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);

                var positionsBefore = game.TurnOrder.ToDictionary(
                    colour => colour,
                    colour => game.LaneFor(colour).Position);
                var activeBefore = game.ActiveFrog;
                var phaseBefore = game.Phase;
                var orderBefore = game.FinishingOrder.ToArray();

                view.Initialize(game);
                TapButton(view.HandOnButton);

                Assert.That(game.IsOver, Is.False, "an arrival is not an ending");
                Assert.That(game.ActiveFrog, Is.EqualTo(activeBefore), "no turn advanced");
                Assert.That(game.Phase, Is.EqualTo(phaseBefore));
                Assert.That(game.FinishingOrder, Is.EqualTo(orderBefore), "nothing was reordered");

                foreach (var colour in game.TurnOrder)
                {
                    Assert.That(
                        game.LaneFor(colour).Position,
                        Is.EqualTo(positionsBefore[colour]),
                        $"{colour} kept the pads it had");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ATurnThatLandedNobodyHome_HasNoDialogToOpen()
        {
            // Structural rather than a comment: there is no wording for a turn
            // that got nowhere, and player-won.md's entry condition is exactly
            // Game.FrogJustHome having a value.
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                Assert.That(game.FrogJustHome, Is.Null);
                Assert.That(() => view.Initialize(game), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => view.Initialize(null), Throws.InstanceOf<ArgumentNullException>());
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Layout_IsTheSharedDialogAtTheSpecsOwnSize_WithAllThreeRegions()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                view.Initialize(game);

                // A centred shared Dialog at WonDialogWidth x WonDialogHeight
                // — not DialogMaxWidth/DialogMaxHeight.
                Assert.That(
                    view.Dialog.PanelRect.sizeDelta,
                    Is.EqualTo(new Vector2(
                        PlayerWonDialogView.WonDialogWidth,
                        PlayerWonDialogView.WonDialogHeight)));

                // frog — WonFrogDiameter across, horizontally centred, one
                // DialogPadding from the panel's top edge.
                Assert.That(
                    view.FrogRect.rect.size,
                    Is.EqualTo(new Vector2(
                        PlayerWonDialogView.WonFrogDiameter,
                        PlayerWonDialogView.WonFrogDiameter)));
                Assert.That(
                    TopEdge(view.Dialog.PanelRect) - TopEdge(view.FrogRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    CentreX(view.FrogRect),
                    Is.EqualTo(CentreX(view.Dialog.PanelRect)).Within(0.001f));

                // headline — WonHeadlineGap below the frog, in a
                // WonHeadlineLineBox line box, centred, at WonHeadlineSize.
                Assert.That(
                    TopEdge(view.FrogRect) - TopEdge(view.HeadlineText.rectTransform)
                        - PlayerWonDialogView.WonFrogDiameter,
                    Is.EqualTo(PlayerWonDialogView.WonHeadlineGap).Within(0.001f));
                Assert.That(
                    view.HeadlineText.rectTransform.rect.height,
                    Is.EqualTo(PlayerWonDialogView.WonHeadlineLineBox).Within(0.001f));
                Assert.That(
                    view.HeadlineText.fontSize,
                    Is.EqualTo((int)PlayerWonDialogView.WonHeadlineSize));
                Assert.That(view.HeadlineText.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(
                    view.HeadlineText.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Overflow),
                    "the headline is one line, so a long name never moves the button");
                Assert.That(
                    CentreX(view.HeadlineText.rectTransform),
                    Is.EqualTo(CentreX(view.Dialog.PanelRect)).Within(0.001f));

                // controls — the shared Dialog's own button row, bottom-right
                // at DialogPadding.
                Assert.That(view.ControlsRect, Is.SameAs(view.Dialog.ButtonRowRect));
                Assert.That(
                    RightEdge(view.Dialog.PanelRect) - RightEdge(view.HandOnButton.RectTransform),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    BottomEdge(view.HandOnButton.RectTransform) - BottomEdge(view.Dialog.PanelRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheFrogIsDrawnInItsOwnColour_InsideThePieceEdgeOutline()
        {
            var game = CreateGame(FrogColour.Pink, FrogColour.Green);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Pink);
                view.Initialize(game);

                Assert.That(view.FrogFill.color, Is.EqualTo(FrogColours.For(FrogColour.Pink)));
                Assert.That(view.FrogOutline.color, Is.EqualTo(BoardColours.PieceEdge));

                // WonFrogOutline thick on every side — the ring is the board's
                // PieceEdge at this page's own width, not the board's.
                Assert.That(
                    view.FrogFill.rectTransform.offsetMin,
                    Is.EqualTo(new Vector2(
                        PlayerWonDialogView.WonFrogOutline,
                        PlayerWonDialogView.WonFrogOutline)));
                Assert.That(
                    view.FrogFill.rectTransform.offsetMax,
                    Is.EqualTo(new Vector2(
                        -PlayerWonDialogView.WonFrogOutline,
                        -PlayerWonDialogView.WonFrogOutline)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheFrogIsDrawnBig_AndNotAtTheBoardsOwnPieceSize()
        {
            // player-won.md: "`WonFrogDiameter` is deliberately not the
            // board's `FrogPieceDiameter` (88 px)... A constant that happened
            // to equal the board's would be re-used by someone later and drag
            // the two into step."
            Assert.That(
                PlayerWonDialogView.WonFrogDiameter,
                Is.Not.EqualTo(GameBoardLaneView.FrogPieceDiameter));
            Assert.That(
                PlayerWonDialogView.WonFrogDiameter,
                Is.GreaterThan(GameBoardLaneView.FrogPieceDiameter));
        }

        [Test]
        public void TheDialogCarriesNoStandings()
        {
            // player-won.md's fourth invariant: "it carries no standings.
            // Game over lists every frog in finishing order; a second table in
            // different words on the screen before it would be the same
            // information twice." There is one Text on this panel, and it is
            // the headline.
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                view.Initialize(game);

                var texts = view.Dialog.PanelRect
                    .GetComponentsInChildren<Text>(includeInactive: true)
                    .Where(text => !string.IsNullOrEmpty(text.text))
                    .ToArray();

                Assert.That(
                    texts.Select(text => text.text),
                    Is.EquivalentTo(new[] { "Green wins!", "Blue's turn" }),
                    "the headline and the button, and nothing that lists anybody else");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_IsInert_AndNoButtonIsNominatedForIt()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                view.Initialize(game);

                // shared-components.md#dialog's amended invariant counts four
                // inert-back dialogs, and this is the fourth. The router
                // already knows; this view nominates nothing and listens for
                // no key, so there is no second opinion to disagree with it.
                Assert.That(view.Dialog.LeastDestructiveButton, Is.Null);
                Assert.That(DeclaredMemberNames(typeof(PlayerWonDialogView)), Does.Not.Contain("Update"));

                var router = new ScreenRouter();
                router.NavigateToScreen(CoreScreen.GameBoard);
                router.OpenDialog(Dialog.PlayerWon);
                router.HandleBack();

                Assert.That(router.CurrentDialog, Is.EqualTo(Dialog.PlayerWon), "back did nothing");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PublicConstants_AreExactlyPlayerWonsOwn_UnderTheIdenticalNames()
        {
            // Everything else this dialog measures itself with — the shared
            // Dialog's padding, the shared Button's height and minimum width,
            // the board's PieceEdge — is referenced from where it already
            // lives rather than redeclared here. This test is what proves
            // that: a second copy of any of them would show up as an extra
            // name.
            AssertPublicConstantsAreExactly(typeof(PlayerWonDialogView), new Dictionary<string, float>
            {
                { nameof(PlayerWonDialogView.WonDialogWidth), 900f },
                { nameof(PlayerWonDialogView.WonDialogHeight), 640f },
                { nameof(PlayerWonDialogView.WonFrogDiameter), 220f },
                { nameof(PlayerWonDialogView.WonFrogOutline), 8f },
                { nameof(PlayerWonDialogView.WonHeadlineGap), 40f },
                { nameof(PlayerWonDialogView.WonHeadlineSize), 76f },
                { nameof(PlayerWonDialogView.WonHeadlineLineBox), 92f }
            });
        }

        [Test]
        public void TheButtonHoldsItsOwnWords_AtTheSharedButtonsFootprint()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                LandHome(game, FrogColour.Green);
                LandHome(game, FrogColour.Blue);
                view.Initialize(game);

                Assert.That(view.HandOnButton.Kind, Is.EqualTo(ButtonKind.Primary));
                Assert.That(
                    view.HandOnButton.RectTransform.sizeDelta.y,
                    Is.EqualTo(Button.ButtonHeight).Within(0.001f));
                Assert.That(
                    view.HandOnButton.RectTransform.sizeDelta.x,
                    Is.GreaterThanOrEqualTo(Button.ButtonMinWidth));

                // Since #323 the shared Button widens to hold its label rather
                // than letting it hang over the edge — `See the results` is
                // the longest thing this button ever says.
                Assert.That(
                    view.HandOnButton.Label.preferredWidth,
                    Is.LessThanOrEqualTo(
                        view.HandOnButton.RectTransform.sizeDelta.x - (Button.ButtonPaddingX * 2f)),
                    $"`{view.HandOnButton.Label.text}` does not fit inside its own button");
            }
            finally
            {
                Destroy(view);
            }
        }

        // --- Helpers ------------------------------------------------------------

        static Game CreateGame(params FrogColour[] turnOrder)
        {
            return new Game(turnOrder, Seed);
        }

        // Plays the active frog's turn from one pad short of the End log,
        // answering correctly, and then runs the hand-off the answer result
        // dialog runs — so the game is left in exactly the state
        // AppRoot.HandOffFinished hands this dialog: the frog is home, the
        // arrival is Core's FrogJustHome, and the next player's turn has
        // already begun. The hand-off is skipped when that was the last frog
        // home, which is what GameAnswerResultTurn.CompleteHandOff does and
        // why: there is nobody to pass the device to.
        static void LandHome(Game game, FrogColour colour)
        {
            Assert.That(game.ActiveFrog, Is.EqualTo(colour), "it is not this frog's turn");

            var lane = game.LaneFor(colour);
            while (lane.Position < Lane.LaneWinningPosition - 1)
            {
                lane.MoveForward();
            }

            game.RollDie();
            game.BeginAnswering();
            lane.Resolve(game.DrawnCard.Product, game.DrawnCard);
            game.ShowResult();
            game.BeginHandOff();

            if (!game.IsOver)
            {
                game.CompleteHandOff();
            }

            Assert.That(lane.IsHome, Is.True, "the turn did not land the frog home");
            Assert.That(game.FrogJustHome, Is.EqualTo(colour), "Core did not record the arrival");
        }

        // Takes as many further turns as it needs to bring the device round to
        // `next` — each answered wrongly from the Start log, which moves
        // nobody and lands nobody home.
        static void PassTurnsUntil(Game game, FrogColour next)
        {
            for (var guard = 0; game.ActiveFrog != next && guard < game.TurnOrder.Count; guard++)
            {
                game.RollDie();
                game.BeginAnswering();
                game.LaneFor(game.ActiveFrog).Resolve(game.DrawnCard.Product + 1, game.DrawnCard);
                game.ShowResult();
                game.BeginHandOff();
                game.CompleteHandOff();
            }

            Assert.That(game.ActiveFrog, Is.EqualTo(next), "the device never came round to this frog");
        }

        static PlayerWonDialogView CreateView()
        {
            var host = new GameObject(nameof(PlayerWonDialogViewTests), typeof(RectTransform));
            return host.AddComponent<PlayerWonDialogView>();
        }

        static void Destroy(PlayerWonDialogView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
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

        static float RightEdge(RectTransform rect)
        {
            return Corners(rect)[2].x;
        }

        static float TopEdge(RectTransform rect)
        {
            return Corners(rect)[1].y;
        }

        static float BottomEdge(RectTransform rect)
        {
            return Corners(rect)[0].y;
        }

        static float CentreX(RectTransform rect)
        {
            var corners = Corners(rect);
            return (corners[0].x + corners[2].x) / 2f;
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static IEnumerable<string> DeclaredMemberNames(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name).ToArray();
        }

        static void AssertPublicConstantsAreExactly(Type type, IDictionary<string, float> expected)
        {
            var constants = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                constants.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(expected.Keys.OrderBy(name => name, StringComparer.Ordinal)),
                $"{type.Name}'s public constants are exactly player-won.md's own, under the identical names");

            foreach (var field in constants)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(expected[field.Name]).Within(0.001f),
                    $"{type.Name}.{field.Name}");
            }
        }
    }
}
