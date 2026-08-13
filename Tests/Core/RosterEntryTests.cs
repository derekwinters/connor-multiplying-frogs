using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="RosterEntry"/> is one seat's worth of roster —
    /// docs/specs/ui/game-setup.md#behaviour: "Seating a frog gives it the
    /// bare colour name — `Blue`, not `Blue Frog`."
    /// </summary>
    public sealed class RosterEntryTests
    {
        [Test]
        public void ASeatedFrog_IsNamedAfterItsColour_AndNothingIsAppendedToIt()
        {
            var entry = new RosterEntry(FrogColour.Blue);

            Assert.That(entry.Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(entry.Name, Is.EqualTo("Blue"));
        }

        [Test]
        public void ASeatedFrog_CanBeRenamed_AndKeepsItsColour()
        {
            var renamed = new RosterEntry(FrogColour.Blue).WithName("Connor");

            Assert.That(renamed.Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(renamed.Name, Is.EqualTo("Connor"));
        }

        // docs/specs/ui/game-setup.md#behaviour: "Clearing the name to empty
        // and pressing `Done` restores the frog's colour name — a nameless
        // frog is not a state this screen can reach."
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        [TestCase(null)]
        public void ANameThatIsBlank_FallsBackToTheColourName_RatherThanLeavingANamelessFrog(string blank)
        {
            var entry = new RosterEntry(FrogColour.Orange).WithName(blank);

            Assert.That(entry.Name, Is.EqualTo("Orange"));
        }
    }
}
