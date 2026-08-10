using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="FrogColour"/> is the fixed set of four colours a player can
    /// be — docs/specs/ui/shared-components.md#frog-colours: "exactly four
    /// are offered". Nothing else about a frog (turn order, its lane) is this
    /// type's concern.
    /// </summary>
    public sealed class FrogColourTests
    {
        [Test]
        public void FrogColour_HasExactlyTheFourNamedMembers()
        {
            var members = Enum.GetNames(typeof(FrogColour));

            Assert.That(members, Is.EquivalentTo(new[] { "Green", "Blue", "Orange", "Pink" }));
        }
    }
}
