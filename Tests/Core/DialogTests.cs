using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Dialog"/> is the fixed set of six panels the router can
    /// hold at most one of at a time, over whichever <see cref="Screen"/> is
    /// current — issue #213's navigation graph, plus
    /// docs/specs/ui/player-won.md's own dialog (#329).
    /// </summary>
    public sealed class DialogTests
    {
        [Test]
        public void Dialog_HasExactlyTheSixNamedMembers()
        {
            var members = Enum.GetNames(typeof(Dialog));

            Assert.That(members, Is.EquivalentTo(new[]
            {
                "RollAndCard", "WorkingOutGrid", "AnswerResult", "PlayerWon", "Settings", "EndGameConfirm"
            }));
        }
    }
}
