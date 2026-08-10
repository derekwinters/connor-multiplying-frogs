using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Screen"/> is the fixed set of four full-screen destinations
    /// the router moves between — issue #213's navigation graph: title screen,
    /// game setup, game board, game over. Dialogs are a separate, layered
    /// concept — see <see cref="Dialog"/> — not a member of this type.
    /// </summary>
    public sealed class ScreenTests
    {
        [Test]
        public void Screen_HasExactlyTheFourNamedMembers()
        {
            var members = Enum.GetNames(typeof(Screen));

            Assert.That(members, Is.EquivalentTo(new[]
            {
                "TitleScreen", "GameSetup", "GameBoard", "GameOver"
            }));
        }
    }
}
