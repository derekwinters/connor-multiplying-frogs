using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="TurnPhase"/> is the fixed sequence a turn's screens follow —
    /// docs/specs/ui/game-board.md#behaviour: "`Roll` → roll and card →
    /// working-out grid → answer result → back here with the frog moved and
    /// the turn passed to the next frog in order," read together with
    /// CONTEXT.md's definition of hand-off. The order these members are
    /// declared in is the order <see cref="Game"/> moves through them.
    /// </summary>
    public sealed class TurnPhaseTests
    {
        [Test]
        public void TurnPhase_HasExactlyTheFiveNamedMembersInOrder()
        {
            var members = Enum.GetNames(typeof(TurnPhase));

            Assert.That(members, Is.EqualTo(new[]
            {
                "WaitingToRoll",
                "RolledAndCardDrawn",
                "Answering",
                "ResultShown",
                "HandOff"
            }));
        }
    }
}
