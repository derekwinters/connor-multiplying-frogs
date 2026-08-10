using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="TurnOutcome"/> is the three, and only three, things that can
    /// happen when an answer is graded — docs/specs/rules.md § Moving: "a
    /// frog moves at most one position per turn... [and] the Start log is a
    /// floor, not a special space." Three outcomes, not two: a wrong answer on
    /// the Start log is its own named outcome rather than a "back" that
    /// happens to do nothing.
    /// </summary>
    public sealed class TurnOutcomeTests
    {
        [Test]
        public void TurnOutcome_HasExactlyTheThreeNamedMembersInOrder()
        {
            var members = Enum.GetNames(typeof(TurnOutcome));

            Assert.That(members, Is.EqualTo(new[]
            {
                "Correct",
                "WrongAboveStartLog",
                "WrongOnStartLog"
            }));
        }
    }
}
