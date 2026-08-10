using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="StandingsRow"/> is the fact <see cref="Game.Standings"/>
    /// hands back for one frog — docs/specs/ui/game-over.md's standings row
    /// reads directly off it: place number, frog colour, and how far it got
    /// (`Position` out of <see cref="Lane.LaneWinningPosition"/>, and whether
    /// it is home). Nothing else — no formatted "6 of 8" string, no colour
    /// swatch; those are the screen's job to render.
    /// </summary>
    public sealed class StandingsRowTests
    {
        [Test]
        public void ANewStandingsRow_CarriesExactlyTheColourPlacePositionAndIsHome()
        {
            var row = new StandingsRow(FrogColour.Blue, 1, 8, true);

            Assert.That(row.Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(row.Place, Is.EqualTo(1));
            Assert.That(row.Position, Is.EqualTo(8));
            Assert.That(row.IsHome, Is.True);
        }

        // Structural, not behavioural: game-over.md's standings row needs
        // exactly these four facts and nothing more.
        [Test]
        public void StandingsRow_ExposesNoPublicMembersBeyondTheFourFacts()
        {
            var propertyNames = typeof(StandingsRow)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[]
            {
                "Colour",
                "IsHome",
                "Place",
                "Position"
            }));
        }
    }
}
