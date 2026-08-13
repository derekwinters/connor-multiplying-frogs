using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using Frogs.Unity.UI;
using Frogs.Unity.Views;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// A typed name is what every screen after game setup shows — issue #311.
    ///
    /// Two format strings used to staple a word onto a colour, and the
    /// wireframe (#310) settled that **nothing appends anything to a name**:
    /// the turn banner reads `Blue's turn` and the game over headline
    /// `Blue wins!`, with the word `frog` in neither.
    /// </summary>
    public sealed class PlayerNamesAcrossScreensTests
    {
        const ulong AnySeed = 311UL;

        static Game GameWith(params RosterEntry[] roster)
        {
            return new Game(roster, AnySeed);
        }

        // ---- The turn banner ----------------------------------------------

        [Test]
        public void TheTurnBanner_ReadsTheNameAndNothingElse_ForADefaultFrogAndARenamedOne()
        {
            var board = CreateBoard();

            try
            {
                board.Initialize(GameWith(
                    new RosterEntry(FrogColour.Blue),
                    new RosterEntry(FrogColour.Green).WithName("Connor")));

                Assert.That(board.TurnBannerText.text, Is.EqualTo("Blue's turn"));
                Assert.That(board.TurnBannerText.text, Does.Not.Contain("frog"));

                board.Initialize(GameWith(
                    new RosterEntry(FrogColour.Green).WithName("Connor"),
                    new RosterEntry(FrogColour.Blue)));

                Assert.That(board.TurnBannerText.text, Is.EqualTo("Connor's turn"));
                Assert.That(board.TurnBannerText.text, Does.Not.Contain("frog"));
            }
            finally
            {
                Object.DestroyImmediate(board.gameObject);
            }
        }

        [Test]
        public void TheTurnBannersChip_CarriesTheTypedName()
        {
            var board = CreateBoard();

            try
            {
                board.Initialize(GameWith(
                    new RosterEntry(FrogColour.Green).WithName("Connor"),
                    new RosterEntry(FrogColour.Blue)));

                Assert.That(board.TurnBannerChip.Label.text, Is.EqualTo("Connor"));
            }
            finally
            {
                Object.DestroyImmediate(board.gameObject);
            }
        }

        [Test]
        public void EveryLanesChip_CarriesItsFrogsName_AndFallsBackToTheColourName()
        {
            var board = CreateBoard();

            try
            {
                board.Initialize(GameWith(
                    new RosterEntry(FrogColour.Green).WithName("Connor"),
                    new RosterEntry(FrogColour.Blue)));

                Assert.That(board.LaneFor(FrogColour.Green).Chip.Label.text, Is.EqualTo("Connor"));
                Assert.That(board.LaneFor(FrogColour.Blue).Chip.Label.text, Is.EqualTo("Blue"));
            }
            finally
            {
                Object.DestroyImmediate(board.gameObject);
            }
        }

        // ---- Game over ------------------------------------------------------

        [Test]
        public void TheGameOverHeadline_ReadsTheNameAndNothingElse()
        {
            Assert.That(GameOverScreenView.FormatHeadline("Blue"), Is.EqualTo("Blue wins!"));
            Assert.That(GameOverScreenView.FormatHeadline("Connor"), Is.EqualTo("Connor wins!"));
            Assert.That(GameOverScreenView.FormatHeadline("Connor"), Does.Not.Contain("frog"));
            Assert.That(GameOverScreenView.FormatHeadline((string)null), Is.EqualTo("Game over"));
        }

        [Test]
        public void TheGameOverScreen_HeadlinesTheWinnersTypedName_AndNamesEveryStandingsRow()
        {
            var view = CreateGameOver();

            try
            {
                var game = GameWith(
                    new RosterEntry(FrogColour.Green).WithName("Connor"),
                    new RosterEntry(FrogColour.Blue));

                var standings = new[]
                {
                    new StandingsRow(FrogColour.Green, "Connor", 1, Lane.LaneWinningPosition, true),
                    new StandingsRow(FrogColour.Blue, "Blue", 2, 4, false)
                };

                view.Show(FrogColour.Green, standings, game.Roster);

                Assert.That(view.HeadlineText.text, Is.EqualTo("Connor wins!"));
                Assert.That(view.RowNameText(0).text, Is.EqualTo("Connor"));
                Assert.That(view.RowNameText(1).text, Is.EqualTo("Blue"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        // docs/specs/ui/game-setup.md#behaviour: "Names last as long as the
        // game does, which includes `Play again` — that button starts a new
        // game with the same frogs in the same turn order without passing
        // through this screen, so it keeps their names too."
        [Test]
        public void PlayAgain_KeepsEveryTypedName()
        {
            var view = CreateGameOver();

            try
            {
                view.Initialize(new ScreenRouter(), () => AnySeed);

                var ended = GameWith(
                    new RosterEntry(FrogColour.Green).WithName("Connor"),
                    new RosterEntry(FrogColour.Blue).WithName("Dad"));

                view.Show(ended);
                view.PlayAgain();

                var next = view.StartedGame;

                Assert.That(next, Is.Not.Null);
                Assert.That(next.TurnOrder, Is.EqualTo(new[] { FrogColour.Green, FrogColour.Blue }));
                Assert.That(next.NameFor(FrogColour.Green), Is.EqualTo("Connor"));
                Assert.That(next.NameFor(FrogColour.Blue), Is.EqualTo("Dad"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        // ---- The chip's own truncation ---------------------------------------

        // docs/specs/ui/shared-components.md#player-chip: "the chip never
        // refuses or alters a name it is given; if a name does not fit, the
        // chip truncates it with an ellipsis." `Orange` is the game's own
        // longest default name and it already overflows the 128 px label
        // column at 132 px, before anybody has typed anything.
        [Test]
        public void TheChip_TruncatesANameItCannotFit_RatherThanRefusingIt()
        {
            var chip = CreatePlayerChip();

            try
            {
                chip.SetFrog(Color.blue, "Alexandras");

                Assert.That(chip.Name, Is.EqualTo("Alexandras"), "the chip never alters the name it was given");
                Assert.That(chip.Label.text, Does.EndWith(DisplayText.Ellipsis));
                Assert.That(chip.Label.text.Length, Is.LessThan("Alexandras".Length));
            }
            finally
            {
                Object.DestroyImmediate(chip.gameObject);
            }
        }

        [Test]
        public void TheChip_LeavesAShortNameAlone()
        {
            var chip = CreatePlayerChip();

            try
            {
                chip.SetFrog(Color.blue, "Sam");

                Assert.That(chip.Label.text, Is.EqualTo("Sam"));
                Assert.That(chip.Label.text, Does.Not.Contain(DisplayText.Ellipsis));
            }
            finally
            {
                Object.DestroyImmediate(chip.gameObject);
            }
        }

        [Test]
        public void TheChipsLabelColumn_IsTheOneTheSpecDerives()
        {
            var expected = PlayerChip.PlayerChipWidth
                - (PlayerChip.PlayerChipLabelPaddingX * 2f)
                - PlayerChip.PlayerSwatchDiameter
                - PlayerChip.PlayerChipSwatchGap;

            Assert.That(PlayerChip.PlayerChipLabelColumn, Is.EqualTo(expected));
            Assert.That(PlayerChip.PlayerChipLabelColumn, Is.EqualTo(128f));
        }

        // ---- Harness -----------------------------------------------------

        static GameBoardScreenView CreateBoard()
        {
            var go = new GameObject("GameBoardScreenView", typeof(RectTransform));
            return go.AddComponent<GameBoardScreenView>();
        }

        static GameOverScreenView CreateGameOver()
        {
            var go = new GameObject("GameOverScreenView", typeof(RectTransform));
            return go.AddComponent<GameOverScreenView>();
        }

        static PlayerChip CreatePlayerChip()
        {
            var go = new GameObject("PlayerChip", typeof(RectTransform));
            return go.AddComponent<PlayerChip>();
        }
    }
}
