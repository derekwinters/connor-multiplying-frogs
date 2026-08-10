using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Dialog"/> is the fixed set of five panels the router can
    /// hold at most one of at a time, over whichever <see cref="Screen"/> is
    /// current — issue #213's navigation graph.
    /// </summary>
    public sealed class DialogTests
    {
        [Test]
        public void Dialog_HasExactlyTheFiveNamedMembers()
        {
            var members = Enum.GetNames(typeof(Dialog));

            Assert.That(members, Is.EquivalentTo(new[]
            {
                "RollAndCard", "WorkingOutGrid", "AnswerResult", "Settings", "EndGameConfirm"
            }));
        }
    }
}
