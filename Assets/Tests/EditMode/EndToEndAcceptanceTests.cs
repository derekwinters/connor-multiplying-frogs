using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.Views;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The shell half of issue #230's acceptance pass. The Core half —
    /// <c>Tests/Core/ScriptedGameTests.cs</c> — plays whole games from a fixed
    /// seed and proves every joint of the turn loop fits together. It cannot
    /// say anything about what reaches the screen, and this is the part that
    /// can.
    ///
    /// **It is deliberately not a walkthrough.** EditMode is an edit-mode
    /// editor session: no <c>Start</c>, no coroutines, no frames, and
    /// <c>Time.deltaTime</c> never advances, so a tapped-through sequence of
    /// screens cannot be observed here and no harness in this repo builds one
    /// — see docs/engineering/testing.md#the-end-to-end-acceptance-pass. What
    /// EditMode is actually for is the adapter-shaped checking below: that a
    /// view builds what Core reported, that a screen renders what Core
    /// decided, and that the assets the flow depends on are still wired after
    /// a round trip through Unity's serializer.
    ///
    /// The two game-over fixtures are **games that were played**, not
    /// standings typed out by hand: the same scripted plays the Core tier
    /// runs, replayed here so the screen is shown a result a real game
    /// produced.
    /// </summary>
    public sealed class EndToEndAcceptanceTests
    {
        /// <summary>
        /// The same named seed the Core-tier scripted games use, so the cards
        /// dealt here are the cards dealt there.
        /// </summary>
        const ulong ScriptedGameSeed = 20260810UL;

        /// <summary>
        /// A stop on the turn loop, so a play that stops making progress
        /// fails as a test rather than hanging the editor.
        /// </summary>
        const int MaxScriptedTurns = 64;

        /// <summary>Turns played before the deliberate-ending fixture ends its game.</summary>
        const int TurnsBeforeTheDeliberateEnding = 4;

        // --- The working-out grid, from Core's own grid model ---------------

        /// <summary>
        /// The one seam Core alone structurally cannot reach: whether the view
        /// draws the grid Core reported. Asserted against the model rather
        /// than against expected numbers, for one card of each of the three
        /// problem shapes ADR-0002 pins — so a view that drew a plausible grid
        /// of its own devising fails even if the numbers happen to look right.
        /// </summary>
        [Test]
        public void TheWorkingOutGridView_BuildsExactlyTheRowsColumnsAndCells_CoreReportsForEachProblemShape()
        {
            foreach (var pile in new[] { Pile.Easy, Pile.Medium, Pile.Hard })
            {
                var card = Card.Draw(pile, Rng.FromSeed(ScriptedGameSeed));
                var model = WorkingOutGrid.For(card, WorkingOutGrid.GridAdditionRowsAtStart);
                var view = CreateGridView(card, pile);

                try
                {
                    Assert.That(
                        view.RowKinds, Is.EqualTo(model.Rows.Select(row => row.Kind).ToArray()),
                        $"{pile}: the view's rows are not Core's rows");

                    Assert.That(view.Cells.Count, Is.EqualTo(model.Rows.Count), $"{pile}: row count");

                    for (var row = 0; row < model.Rows.Count; row++)
                    {
                        Assert.That(
                            view.Cells[row].Count, Is.EqualTo(model.ColumnCount),
                            $"{pile}: row {row} is not {model.ColumnCount} columns wide");

                        Assert.That(
                            view.Cells[row].Select(cell => cell.Kind).ToArray(),
                            Is.EqualTo(model.Rows[row].Cells.Select(cell => cell.Kind).ToArray()),
                            $"{pile}: row {row}'s cell kinds are not the ones Core reported");
                    }

                    Assert.That(
                        view.Cells.Sum(row => row.Count),
                        Is.EqualTo(model.Rows.Count * model.ColumnCount),
                        $"{pile}: total cell count");

                    // The card's own digits, read back off the cells the view
                    // drew — the round trip from a Core card to what a child
                    // actually sees printed.
                    AssertPrintedRowReads(view, GridRowKind.Multiplicand, card.Multiplicand);
                    AssertPrintedRowReads(view, GridRowKind.Multiplier, card.Multiplier);
                }
                finally
                {
                    Destroy(view);
                }
            }
        }

        // --- The game-over screen, from games that were actually played -----

        /// <summary>
        /// docs/specs/ui/game-over.md#behaviour's first route: the last frog
        /// reaches its End log and the game ends itself. The headline names
        /// the frog that got home *first*, and its row leads the standings and
        /// is the only one drawn heavier.
        /// </summary>
        [Test]
        public void TheGameOverScreen_NamesTheWinner_FromAGamePlayedAllTheWayToItsOwnEnding()
        {
            var game = PlayUntilEveryFrogIsHome();
            var view = CreateGameOverView();

            try
            {
                // Both frogs are home, so a screen that guessed "the winner is
                // whoever is home" would have two answers and no way to pick.
                Assert.That(game.IsOver, Is.True);
                Assert.That(game.TurnOrder.All(colour => game.LaneFor(colour).IsHome), Is.True);

                view.Show(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Green wins!"));
                Assert.That(view.RowCount, Is.EqualTo(game.Standings.Count));
                Assert.That(view.RowColour(0), Is.EqualTo(FrogColour.Green), "the first finisher leads the standings");
                Assert.That(view.RowColour(1), Is.EqualTo(FrogColour.Blue));
                Assert.That(view.RowProgressText(0).text, Is.EqualTo("Home — 8 of 8"));
                Assert.That(view.RowProgressText(1).text, Is.EqualTo("Home — 8 of 8"));

                Assert.That(
                    view.RowBorderWidth(0), Is.EqualTo(GameOverScreenView.StandingsWinnerBorder),
                    "the winner's row is drawn heavier");
                Assert.That(
                    view.RowBorderWidth(1), Is.EqualTo(GameOverScreenView.StandingsRowBorder),
                    "a finisher that did not win is an ordinary row");
            }
            finally
            {
                Destroy(view);
            }
        }

        /// <summary>
        /// The other headline: the game was ended before anybody got home, so
        /// there is nobody to name. docs/specs/ui/game-over.md — "announcing a
        /// winner who did not win is worse than announcing nobody."
        /// </summary>
        [Test]
        public void TheGameOverScreen_ReadsGameOver_FromAGamePlayedAndThenEndedBeforeAnybodyGotHome()
        {
            var game = PlayThenEndDeliberatelyWithNobodyHome();
            var view = CreateGameOverView();

            try
            {
                Assert.That(game.IsOver, Is.True);
                Assert.That(game.Winner, Is.Null);

                view.Show(game);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Game over"));
                Assert.That(view.RowCount, Is.EqualTo(game.Standings.Count));

                for (var row = 0; row < view.RowCount; row++)
                {
                    Assert.That(
                        view.IsWinnerRow(row), Is.False,
                        $"row {row} is highlighted as a winner in a game nobody won");
                    Assert.That(
                        view.RowBorderWidth(row), Is.EqualTo(GameOverScreenView.StandingsRowBorder),
                        $"row {row} is drawn as a winner's row in a game nobody won");
                    Assert.That(view.RowProgressText(row).text, Does.Not.Contain("Home"));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        // --- The assets the flow depends on ---------------------------------

        /// <summary>
        /// Every scene the build ships is present where the build looks for
        /// it, and every object in it comes back off disk with all of its
        /// components still attached.
        ///
        /// A component whose script reference did not survive the round trip
        /// does not throw and does not log: it comes back as a **null entry**
        /// in the object's component list, and the scene looks fine until the
        /// thing it was driving quietly does nothing. That silent failure is
        /// what docs/engineering/unity-serialization.md asks to be guarded,
        /// and it is what this looks for. <c>GameSceneTests</c> and
        /// <c>HelloWorldSceneTests</c> pin their own scene's path, ordering
        /// and camera; this asks a different question, of every registered
        /// scene at once, so a scene added later is covered without anybody
        /// remembering to come back here.
        /// </summary>
        [Test]
        public void EverySceneTheBuildShips_IsPresent_AndKeepsEveryComponentThroughARoundTripThroughTheSerializer()
        {
            var registered = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();

            Assert.That(
                registered, Is.Not.Empty,
                "No scene is registered in build settings at all, so the app has nothing to boot into.");

            foreach (var entry in registered)
            {
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path), Is.Not.Null,
                    $"{entry.path} is registered in build settings but there is no scene asset there.");

                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);

                try
                {
                    Assert.That(
                        scene.GetRootGameObjects(), Is.Not.Empty,
                        $"{entry.path} opened with nothing in it at all.");

                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var component in root.GetComponentsInChildren<Component>(includeInactive: true))
                        {
                            Assert.That(
                                component, Is.Not.Null,
                                $"An object under {root.name} in {entry.path} has a component that did not "
                                + "come back from the serializer. A detached script reads as a missing "
                                + "script in the inspector and as nothing at all at runtime.");
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        /// <summary>
        /// **The flow needs no prefab, and this is what says so out loud.**
        /// Every screen builds its whole hierarchy through the typed Unity API
        /// the first time it is asked for anything — no committed prefab, no
        /// imported font, no imported sprite
        /// (docs/specs/ui/shared-components.md: "no external assets"). A
        /// screen that quietly started depending on an asset that is not
        /// committed would come up empty here, in milliseconds, instead of on
        /// the tablet.
        /// </summary>
        [Test]
        public void EveryScreenInTheFlow_BuildsItsWholeHierarchyItself_SoTheFlowDependsOnNoPrefabAndNoImportedAsset()
        {
            AssertScreenBuildsItself<TitleScreenView>(view => view.RectTransform);
            AssertScreenBuildsItself<GameSetupScreenView>(view => view.RectTransform);
            AssertScreenBuildsItself<GameBoardScreenView>(view => view.RectTransform);
            AssertScreenBuildsItself<RollAndCardDialogView>(view => view.RectTransform);
            AssertScreenBuildsItself<WorkingOutGridView>(view => view.RectTransform);
            AssertScreenBuildsItself<AnswerResultDialogView>(view => view.RectTransform);
            AssertScreenBuildsItself<PlayerWonDialogView>(view => view.RectTransform);
            AssertScreenBuildsItself<SettingsDialogView>(view => view.RectTransform);
            AssertScreenBuildsItself<EndGameConfirmView>(view => view.RectTransform);
            AssertScreenBuildsItself<GameOverScreenView>(view => view.RectTransform);
        }

        // --- The scripted plays ---------------------------------------------

        // The same two-frog roster and the same seed the Core tier plays, so
        // the results shown to the screen here are the results asserted there.
        static FrogColour[] ScriptedRoster()
        {
            return new[] { FrogColour.Green, FrogColour.Blue };
        }

        // Both frogs answer everything right; Green goes first, so Green gets
        // home first and Blue's finish is what ends the game.
        static Game PlayUntilEveryFrogIsHome()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);

            for (var turn = 0; turn < MaxScriptedTurns && !game.IsOver; turn++)
            {
                PlayOneTurn(game, answerCorrectly: true);
            }

            Assert.That(game.IsOver, Is.True, "the scripted game never ended");
            return game;
        }

        // Four real turns, both frogs answering right, then the game is ended
        // from the end-game confirm's route with everybody still swimming.
        static Game PlayThenEndDeliberatelyWithNobodyHome()
        {
            var game = new Game(ScriptedRoster(), ScriptedGameSeed);

            for (var turn = 0; turn < TurnsBeforeTheDeliberateEnding; turn++)
            {
                PlayOneTurn(game, answerCorrectly: true);
            }

            game.EndGame();
            return game;
        }

        // One turn, through the phases docs/specs/ui/game-board.md lists, with
        // the frog moved by Core's own grading and nothing else. The Core tier
        // is where each of these joints is asserted; here the play is only a
        // way of producing a genuine result for the screen to render.
        static void PlayOneTurn(Game game, bool answerCorrectly)
        {
            var frog = game.ActiveFrog;

            game.RollDie();
            var card = game.DrawnCard;
            game.BeginAnswering();

            game.LaneFor(frog).Resolve(answerCorrectly ? card.Product : card.Product + 1, card);

            game.ShowResult();
            game.BeginHandOff();

            if (!game.IsOver)
            {
                game.CompleteHandOff();
            }
        }

        // --- Helpers ----------------------------------------------------------

        static void AssertPrintedRowReads(WorkingOutGridView view, GridRowKind kind, int operand)
        {
            var rowIndex = view.RowKinds.ToList().IndexOf(kind);

            Assert.That(rowIndex, Is.GreaterThanOrEqualTo(0), $"the view drew no {kind} row");

            var printed = view.Cells[rowIndex]
                .Where(cell => cell.Kind == GridCellKind.Printed)
                .Select(cell => cell.Content);

            Assert.That(
                string.Concat(printed), Is.EqualTo(operand.ToString()),
                $"the {kind} row does not read {operand}");
        }

        static void AssertScreenBuildsItself<TView>(Func<TView, RectTransform> ownRect)
            where TView : MonoBehaviour
        {
            var host = new GameObject(typeof(TView).Name, typeof(RectTransform));

            try
            {
                var view = host.AddComponent<TView>();

                // Asking for the screen's own rect is what forces it to build
                // itself, on every one of these views. Nothing here depends on
                // Awake having run, because in an edit-mode session it may not
                // have.
                Assert.That(
                    ownRect(view), Is.Not.Null,
                    $"{typeof(TView).Name} has no RectTransform of its own after being added.");

                Assert.That(
                    host.transform.childCount, Is.GreaterThan(0),
                    $"{typeof(TView).Name} built nothing, so the screen it draws comes from an "
                    + "asset that is not committed rather than from its own code.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static WorkingOutGridView CreateGridView(Card card, Pile pile)
        {
            var host = new GameObject(nameof(EndToEndAcceptanceTests), typeof(RectTransform));
            var view = host.AddComponent<WorkingOutGridView>();
            view.Initialize(new AcceptanceTurn { Frog = FrogColour.Green, Pile = pile, Card = card });
            return view;
        }

        static GameOverScreenView CreateGameOverView()
        {
            var host = new GameObject(nameof(EndToEndAcceptanceTests), typeof(RectTransform));
            return host.AddComponent<GameOverScreenView>();
        }

        static void Destroy(MonoBehaviour view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        // The card the grid is opened on, and somewhere for the answer to go.
        // Nothing here grades anything — the grid never learns whether what it
        // handed over was right (ADR-0002).
        sealed class AcceptanceTurn : IWorkingOutTurn
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

            public Pile Pile { get; set; }

            public Card Card { get; set; }

            public List<int> Submitted { get; } = new List<int>();

            public void SubmitAnswer(int answer)
            {
                Submitted.Add(answer);
            }
        }
    }
}
