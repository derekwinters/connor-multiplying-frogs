using System.Linq;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// A game's roster carries names, not only colours — so that no screen
    /// has to look a name up anywhere else. docs/specs/ui/game-setup.md#behaviour:
    /// "`Start` begins the game with the chosen frogs in badge order ... Their
    /// names go with them, and are what every later screen shows."
    /// </summary>
    public sealed class GameRosterTests
    {
        const ulong AnySeed = 311UL;

        [Test]
        public void AGameBuiltFromColoursAlone_NamesEveryFrogAfterItsColour()
        {
            var game = new Game(new[] { FrogColour.Blue, FrogColour.Green }, AnySeed);

            Assert.That(game.NameFor(FrogColour.Blue), Is.EqualTo("Blue"));
            Assert.That(game.NameFor(FrogColour.Green), Is.EqualTo("Green"));
        }

        [Test]
        public void AGameBuiltFromARoster_CarriesTheTypedNames_InTheSameTurnOrder()
        {
            var roster = new[]
            {
                new RosterEntry(FrogColour.Green).WithName("Connor"),
                new RosterEntry(FrogColour.Blue)
            };

            var game = new Game(roster, AnySeed);

            Assert.That(game.TurnOrder, Is.EqualTo(new[] { FrogColour.Green, FrogColour.Blue }));
            Assert.That(game.NameFor(FrogColour.Green), Is.EqualTo("Connor"));
            Assert.That(game.NameFor(FrogColour.Blue), Is.EqualTo("Blue"));
            Assert.That(game.Roster.Select(entry => entry.Name), Is.EqualTo(new[] { "Connor", "Blue" }));
        }

        // docs/specs/ui/game-setup.md#behaviour: "Two seats may hold the same
        // name. Nothing prevents it, nothing numbers them, nothing warns."
        // Pinned by a test either way, per this issue's checklist — and the
        // way it is settled is: allowed.
        [Test]
        public void TwoFrogsMayHoldTheSameName_NothingPreventsNumbersOrWarns()
        {
            var roster = new[]
            {
                new RosterEntry(FrogColour.Green).WithName("Sam"),
                new RosterEntry(FrogColour.Pink).WithName("Sam")
            };

            var game = new Game(roster, AnySeed);

            Assert.That(game.NameFor(FrogColour.Green), Is.EqualTo("Sam"));
            Assert.That(game.NameFor(FrogColour.Pink), Is.EqualTo("Sam"));
        }

        [Test]
        public void TheStandingsCarryEachFrogsName_SoGameOverNeedsNoSecondLookup()
        {
            var roster = new[]
            {
                new RosterEntry(FrogColour.Green).WithName("Connor"),
                new RosterEntry(FrogColour.Blue)
            };

            var game = new Game(roster, AnySeed);

            var names = game.Standings.ToDictionary(row => row.Colour, row => row.Name);

            Assert.That(names[FrogColour.Green], Is.EqualTo("Connor"));
            Assert.That(names[FrogColour.Blue], Is.EqualTo("Blue"));
        }

        [Test]
        public void AskingForTheNameOfAFrogThatIsNotPlaying_IsRefused()
        {
            var game = new Game(new[] { FrogColour.Blue, FrogColour.Green }, AnySeed);

            Assert.That(() => game.NameFor(FrogColour.Pink), Throws.ArgumentException);
        }
    }
}
