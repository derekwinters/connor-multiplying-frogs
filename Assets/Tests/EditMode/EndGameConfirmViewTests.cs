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
// SettingsDialogViewTests.cs and ButtonTests.cs work around — so these are
// pulled in by explicit alias, and a bare `Button`, `ButtonKind` or
// `DialogPanel` in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The end-game confirm — issue #226, built against
    /// docs/specs/ui/end-game-confirm.md and its committed 1:1 mockup.
    ///
    /// This is the one dialog in the game that can take something away from
    /// somebody who did nothing wrong, so four things these tests hold down
    /// matter more than the layout:
    ///
    /// - **The cost sentence is true, every time.** Both of its numbers come
    ///   from the live game — how many frogs are still swimming, and how many
    ///   are playing — and every reachable pair of them is asserted, not just
    ///   the two the spec table prints.
    /// - **Ending is not losing.** Every frog keeps its lane position across
    ///   the tap.
    /// - **`Keep playing` and hardware back are one action**, and neither can
    ///   reach Core's end-the-game call.
    /// - **The destructive button is nowhere near the thumb**, by more than
    ///   `ButtonDestructiveGap`.
    /// </summary>
    public sealed class EndGameConfirmViewTests
    {
        // Every reachable (frogs still swimming, roster size) pair, and the
        // sentence end-game-confirm.md's cost table says each one renders.
        // Written out rather than generated, so this table is an independent
        // statement of the wording and not a second copy of the helper.
        //
        // Roster runs Game.MinFrogsPerGame..Game.MaxFrogsPerGame; still
        // swimming runs 1..roster, because the game ends itself the moment the
        // last frog is home, so this dialog can never open on zero.
        static readonly object[] CostSentenceCases =
        {
            new object[] { 1, 2, "One frog is still swimming. Ending it now stops the game for all two players and shows the results." },
            new object[] { 2, 2, "Two frogs are still swimming. Ending it now stops the game for all two players and shows the results." },
            new object[] { 1, 3, "One frog is still swimming. Ending it now stops the game for all three players and shows the results." },
            new object[] { 2, 3, "Two frogs are still swimming. Ending it now stops the game for all three players and shows the results." },
            new object[] { 3, 3, "Three frogs are still swimming. Ending it now stops the game for all three players and shows the results." },
            new object[] { 1, 4, "One frog is still swimming. Ending it now stops the game for all four players and shows the results." },
            new object[] { 2, 4, "Two frogs are still swimming. Ending it now stops the game for all four players and shows the results." },
            new object[] { 3, 4, "Three frogs are still swimming. Ending it now stops the game for all four players and shows the results." },
            new object[] { 4, 4, "Four frogs are still swimming. Ending it now stops the game for all four players and shows the results." }
        };

        [TestCaseSource(nameof(CostSentenceCases))]
        public void CostSentence_RendersBothLiveNumbersAsWords_ForEveryReachablePair(
            int frogsStillSwimming,
            int rosterSize,
            string expected)
        {
            Assert.That(
                EndGameConfirmView.FormatCostSentence(frogsStillSwimming, rosterSize),
                Is.EqualTo(expected));
        }

        [Test]
        public void CostSentence_RendersTheSpecsTwoPrintedRowsVerbatim()
        {
            // The two rows end-game-confirm.md's cost table prints, in the
            // four-player game the mockup is drawn in. Kept as their own test
            // so a change to the wording has to walk past the spec's own
            // sentences, not only a parameterised table.
            Assert.That(
                EndGameConfirmView.FormatCostSentence(3, 4),
                Is.EqualTo("Three frogs are still swimming. Ending it now stops the game for all four players and shows the results."));

            Assert.That(
                EndGameConfirmView.FormatCostSentence(1, 4),
                Is.EqualTo("One frog is still swimming. Ending it now stops the game for all four players and shows the results."));
        }

        [Test]
        public void CostSentence_HasNoEverybodyIsHomeWording()
        {
            // "There is no everybody-is-home wording, and there does not need
            // to be" — the game ends itself the moment the last frog lands, so
            // this dialog can never open on zero. Structural rather than
            // hoped-for: there is no string for it to return.
            Assert.That(
                () => EndGameConfirmView.FormatCostSentence(0, 4),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CostSentence_RefusesARosterNoGameCanHave()
        {
            // game-setup.md#invariants: "a game cannot start with fewer than
            // two frogs, or more than four." Nothing here invents wording for
            // a roster Core would not have built.
            Assert.That(
                () => EndGameConfirmView.FormatCostSentence(1, Game.MinFrogsPerGame - 1),
                Throws.InstanceOf<ArgumentOutOfRangeException>());

            Assert.That(
                () => EndGameConfirmView.FormatCostSentence(1, Game.MaxFrogsPerGame + 1),
                Throws.InstanceOf<ArgumentOutOfRangeException>());

            // More frogs swimming than are in the game is not a state Core can
            // produce either.
            Assert.That(
                () => EndGameConfirmView.FormatCostSentence(3, 2),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Cost_IsReadFromTheLiveGame_NotComposedFromAFixedString()
        {
            // A three-frog game with one frog already home: the sentence has
            // to say two still swimming and three players, which is a pair no
            // fixed string in the spec table prints.
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);
            SendHome(game, FrogColour.Orange);

            var view = CreateView();

            try
            {
                view.Initialize(game);
                view.Open();

                Assert.That(
                    view.CostText.text,
                    Is.EqualTo("Two frogs are still swimming. Ending it now stops the game for all three players and shows the results."));

                // And it is asked again on every open, never remembered.
                SendHome(game, FrogColour.Blue);
                view.Close();
                view.Open();

                Assert.That(
                    view.CostText.text,
                    Is.EqualTo("One frog is still swimming. Ending it now stops the game for all three players and shows the results."));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Layout_IsTheSharedDialogAtTheSpecsOwnSize_WithAllThreeRegions()
        {
            var view = CreateView();

            try
            {
                view.Initialize(CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink));

                // A centred shared Dialog at ConfirmDialogWidth x
                // ConfirmDialogHeight — not DialogMaxWidth/DialogMaxHeight.
                Assert.That(
                    view.Dialog.PanelRect.sizeDelta,
                    Is.EqualTo(new Vector2(
                        EndGameConfirmView.ConfirmDialogWidth,
                        EndGameConfirmView.ConfirmDialogHeight)));

                // question — the shared Dialog's own title slot, at the top of
                // the padding box, at the spec's ConfirmQuestionSize.
                Assert.That(view.QuestionText.text, Is.EqualTo("End the game for everyone?"));
                Assert.That(view.QuestionText.fontSize, Is.EqualTo((int)EndGameConfirmView.ConfirmQuestionSize));
                Assert.That(
                    LeftEdge(view.QuestionText.rectTransform) - LeftEdge(view.Dialog.PanelRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));

                // cost — ConfirmBodyWidth wide, at ConfirmBodySize, wrapping,
                // one DialogTitleGap below the question.
                Assert.That(view.CostText.fontSize, Is.EqualTo((int)EndGameConfirmView.ConfirmBodySize));
                Assert.That(
                    view.CostText.rectTransform.rect.width,
                    Is.EqualTo(EndGameConfirmView.ConfirmBodyWidth).Within(0.001f));
                Assert.That(view.CostText.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(
                    view.CostText.lineSpacing,
                    Is.EqualTo(EndGameConfirmView.ConfirmBodyLineHeight).Within(0.001f));
                Assert.That(
                    TopEdge(view.CostText.rectTransform),
                    Is.EqualTo(TopEdge(view.Dialog.BodyRect)).Within(0.001f),
                    "the cost sits at the top of the shared Dialog's body, one DialogTitleGap under the question");

                // controls — the shared Dialog's own button row.
                Assert.That(view.ControlsRect, Is.SameAs(view.Dialog.ButtonRowRect));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void CostColumn_IsThePaddingBoxWidth_NotASecondIndependentNumber()
        {
            // ConfirmBodyWidth (1048) is ConfirmDialogWidth (1160) inset by
            // DialogPadding (56) on both sides. The spec table gives it its own
            // row, so the code declares it under that name — this is what
            // catches the day the two stop agreeing.
            Assert.That(
                EndGameConfirmView.ConfirmBodyWidth,
                Is.EqualTo(EndGameConfirmView.ConfirmDialogWidth - (DialogPanel.DialogPadding * 2f)).Within(0.001f));
        }

        [Test]
        public void PublicConstants_AreExactlyEndGameConfirmsOwn_UnderTheIdenticalNames()
        {
            // Everything else this dialog measures itself with — the shared
            // Dialog's padding, title gap and button-row gap, the shared
            // Button's height and ButtonDestructiveGap — is referenced from
            // where it already lives rather than redeclared here. This test is
            // what proves that: a second copy of any of them would show up as
            // an extra name.
            AssertPublicConstantsAreExactly(typeof(EndGameConfirmView), new Dictionary<string, float>
            {
                { nameof(EndGameConfirmView.ConfirmDialogWidth), 1160f },
                { nameof(EndGameConfirmView.ConfirmDialogHeight), 540f },
                { nameof(EndGameConfirmView.ConfirmQuestionSize), 56f },
                { nameof(EndGameConfirmView.ConfirmBodySize), 40f },
                { nameof(EndGameConfirmView.ConfirmBodyWidth), 1048f },
                { nameof(EndGameConfirmView.ConfirmBodyLineHeight), 1.4f }
            });
        }

        [Test]
        public void Controls_AreDestructiveLeftAndPrimaryRight_OnTheDialogPaddingEdges()
        {
            var view = CreateView();

            try
            {
                view.Initialize(CreateGame(FrogColour.Green, FrogColour.Blue));

                Assert.That(view.EndTheGameButton.Kind, Is.EqualTo(ButtonKind.Destructive));
                Assert.That(view.KeepPlayingButton.Kind, Is.EqualTo(ButtonKind.Primary));

                // "sitting on the left and right edges of the DialogPadding
                // box exactly as the mockup draws them".
                Assert.That(
                    LeftEdge(view.EndTheGameButton.RectTransform) - LeftEdge(view.Dialog.PanelRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    RightEdge(view.Dialog.PanelRect) - RightEdge(view.KeepPlayingButton.RectTransform),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));

                // Both at the shared Button's own default footprint — nothing
                // about this dialog widens either one.
                Assert.That(
                    view.EndTheGameButton.RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(Button.ButtonMinWidth, Button.ButtonHeight)));
                Assert.That(
                    view.KeepPlayingButton.RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(Button.ButtonMinWidth, Button.ButtonHeight)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheDestructiveButton_ClearsButtonDestructiveGap_ByAWideMargin()
        {
            var view = CreateView();

            try
            {
                view.Initialize(CreateGame(FrogColour.Green, FrogColour.Blue));

                var gap = LeftEdge(view.KeepPlayingButton.RectTransform)
                    - RightEdge(view.EndTheGameButton.RectTransform);

                // ButtonDestructiveGap is the shared invariant's stated
                // *minimum* separation, not the placement measurement —
                // "the safe option is... a full ButtonDestructiveGap away".
                Assert.That(
                    gap,
                    Is.GreaterThanOrEqualTo(Button.ButtonDestructiveGap),
                    "the destructive button is never within ButtonGap of the thumb");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EndTheGame_EndsTheGameInCore_AndAsksForGameOver_Once()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);
            var view = CreateView();

            try
            {
                var ended = 0;
                var keptPlaying = 0;
                view.GameEnded += () => ended++;
                view.KeepPlayingRequested += () => keptPlaying++;

                view.Initialize(game);
                view.Open();

                Assert.That(game.IsOver, Is.False);

                TapButton(view.EndTheGameButton);

                // Two separate things: Core's own end-the-game action...
                Assert.That(game.IsOver, Is.True, "Core's end-the-game action ran");
                // ...and the request to move the screen to game over.
                Assert.That(ended, Is.EqualTo(1));
                Assert.That(keptPlaying, Is.Zero, "and never the keep-playing transition");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EndingTheGame_IsNotLosingIt_EveryFrogKeepsItsPosition()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue, FrogColour.Orange);
            game.LaneFor(FrogColour.Green).MoveForward();
            game.LaneFor(FrogColour.Green).MoveForward();
            game.LaneFor(FrogColour.Orange).MoveForward();

            var before = game.TurnOrder.ToDictionary(colour => colour, colour => game.LaneFor(colour).Position);

            var view = CreateView();

            try
            {
                view.Initialize(game);
                view.Open();

                TapButton(view.EndTheGameButton);

                foreach (var colour in game.TurnOrder)
                {
                    Assert.That(
                        game.LaneFor(colour).Position,
                        Is.EqualTo(before[colour]),
                        $"{colour} kept the pads it had");
                }

                // And the standings it leaves behind are the ones that were
                // on the board — nothing was reset on the way out.
                Assert.That(
                    game.Standings.Single(row => row.Colour == FrogColour.Green).Position,
                    Is.EqualTo(before[FrogColour.Green]));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void KeepPlaying_AsksOnlyForTheBoard_AndEndsNothing()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                var ended = 0;
                var keptPlaying = 0;
                view.GameEnded += () => ended++;
                view.KeepPlayingRequested += () => keptPlaying++;

                view.Initialize(game);
                view.Open();

                var activeBefore = game.ActiveFrog;
                var phaseBefore = game.Phase;

                TapButton(view.KeepPlayingButton);

                Assert.That(keptPlaying, Is.EqualTo(1));
                Assert.That(ended, Is.Zero, "the safe button never ends a game");
                Assert.That(game.IsOver, Is.False, "and never calls Core's end-the-game action");

                // "cancelling returns to the exact board state — same turn,
                // same positions."
                Assert.That(game.ActiveFrog, Is.EqualTo(activeBefore), "no turn advanced");
                Assert.That(game.Phase, Is.EqualTo(phaseBefore));
                foreach (var colour in game.TurnOrder)
                {
                    Assert.That(game.LaneFor(colour).Position, Is.Zero, "no frog moved");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_DrivesTheIdenticalActionKeepPlayingDrives()
        {
            var game = CreateGame(FrogColour.Green, FrogColour.Blue);
            var view = CreateView();

            try
            {
                var ended = 0;
                var keptPlaying = 0;
                view.GameEnded += () => ended++;
                view.KeepPlayingRequested += () => keptPlaying++;

                view.Initialize(game);
                view.Open();

                // The router (#213) already routes hardware back on
                // Dialog.EndGameConfirm to "what `Keep playing` does". This
                // view exposes that one action rather than listening for the
                // key itself — so there is exactly one keep-playing path, not
                // two that happen to agree today.
                view.RequestKeepPlaying();

                Assert.That(keptPlaying, Is.EqualTo(1));
                Assert.That(ended, Is.Zero, "back never ends a game");
                Assert.That(game.IsOver, Is.False);

                TapButton(view.KeepPlayingButton);

                Assert.That(keptPlaying, Is.EqualTo(2), "the same callback, not a second distinct one");
                Assert.That(ended, Is.Zero);
                Assert.That(game.IsOver, Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_IsNotListenedForTwice()
        {
            // The board (#220) owns the Escape key on the screen underneath,
            // and the router owns what back means with a dialog open. A second
            // Update()/Input handler here would be a third opinion — and on
            // this dialog, the one that could end a game by accident.
            var declared = DeclaredMemberNames(typeof(EndGameConfirmView));

            Assert.That(declared, Does.Not.Contain("Update"));
        }

        [Test]
        public void KeepPlaying_IsTheDialogsLeastDestructiveButton()
        {
            var view = CreateView();

            try
            {
                view.Initialize(CreateGame(FrogColour.Green, FrogColour.Blue));

                // The value the router reads for hardware back —
                // shared-components.md#dialog: "the hardware back button does
                // what the dialog's least destructive button does, and never
                // what its most destructive one does."
                Assert.That(view.Dialog.LeastDestructiveButton, Is.SameAs(view.KeepPlayingButton));
                Assert.That(view.Dialog.LeastDestructiveButton, Is.Not.SameAs(view.EndTheGameButton));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PublicSurface_IsTheTwoAnswersAndTheCostSentence_AndNothingElse()
        {
            // In particular: nothing that resets, restarts, quits or scores.
            // Ending a game stops it and shows the results; it does not undo
            // it.
            var expected = new[]
            {
                nameof(EndGameConfirmView.ConfirmDialogWidth),
                nameof(EndGameConfirmView.ConfirmDialogHeight),
                nameof(EndGameConfirmView.ConfirmQuestionSize),
                nameof(EndGameConfirmView.ConfirmBodySize),
                nameof(EndGameConfirmView.ConfirmBodyWidth),
                nameof(EndGameConfirmView.ConfirmBodyLineHeight),
                // The two events, spelled out rather than via nameof — an
                // event cannot be named from outside its declaring type in
                // every context, and these two are the whole of what this
                // dialog can ask anybody to do.
                "GameEnded",
                "KeepPlayingRequested",
                nameof(EndGameConfirmView.RectTransform),
                nameof(EndGameConfirmView.Dialog),
                nameof(EndGameConfirmView.QuestionText),
                nameof(EndGameConfirmView.CostText),
                nameof(EndGameConfirmView.ControlsRect),
                nameof(EndGameConfirmView.EndTheGameButton),
                nameof(EndGameConfirmView.KeepPlayingButton),
                nameof(EndGameConfirmView.Initialize),
                nameof(EndGameConfirmView.Open),
                nameof(EndGameConfirmView.Close),
                nameof(EndGameConfirmView.Refresh),
                nameof(EndGameConfirmView.RequestKeepPlaying),
                nameof(EndGameConfirmView.FormatCostSentence)
            };

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var declared = typeof(EndGameConfirmView)
                .GetMembers(flags)
                .Select(member => member.Name)
                // Property getters and event add/remove accessors come back as
                // methods of their own; the member they belong to is already
                // in the list.
                .Where(name => !name.StartsWith("get_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("set_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("add_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("remove_", StringComparison.Ordinal))
                // MonoBehaviour subclasses get an implicit public constructor.
                .Where(name => !name.StartsWith(".", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal);

            Assert.That(declared, Is.EqualTo(expected.OrderBy(name => name, StringComparer.Ordinal)));

            foreach (var forbidden in new[] { "Reset", "Restart", "Quit", "Undo", "Score", "Winner" })
            {
                var word = forbidden;

                Assert.That(
                    DeclaredMemberNames(typeof(EndGameConfirmView))
                        .Where(name => name.IndexOf(word, StringComparison.Ordinal) >= 0),
                    Is.Empty,
                    $"nothing on this dialog reaches {word}");
            }
        }

        static Game CreateGame(params FrogColour[] turnOrder)
        {
            return new Game(turnOrder, seed: 226UL);
        }

        static void SendHome(Game game, FrogColour colour)
        {
            var lane = game.LaneFor(colour);

            while (!lane.IsHome)
            {
                lane.MoveForward();
            }
        }

        static EndGameConfirmView CreateView()
        {
            var host = new GameObject(nameof(EndGameConfirmViewTests), typeof(RectTransform));
            return host.AddComponent<EndGameConfirmView>();
        }

        static void Destroy(EndGameConfirmView view)
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

        static float LeftEdge(RectTransform rect)
        {
            return Corners(rect)[0].x;
        }

        static float RightEdge(RectTransform rect)
        {
            return Corners(rect)[2].x;
        }

        static float TopEdge(RectTransform rect)
        {
            return Corners(rect)[1].y;
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
                $"{type.Name}'s public constants are exactly end-game-confirm.md's own, under the identical names");

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
