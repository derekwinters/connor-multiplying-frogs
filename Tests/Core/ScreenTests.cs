using System;
using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// <see cref="Screen"/> is the fixed set of five full-screen destinations
    /// the router moves between — issue #213's navigation graph: title screen,
    /// game setup, game board, game over, plus docs/specs/ui/how-to-play.md's
    /// own screen (#414). Dialogs are a separate, layered concept — see
    /// <see cref="Dialog"/> — not a member of this type.
    ///
    /// `HowToPlay` is a <see cref="Screen"/> and deliberately not a
    /// <see cref="Dialog"/>. It is opened from inside the settings dialog, and
    /// docs/specs/ui/shared-components.md#dialog says a dialog never opens
    /// over another dialog — so how-to-play.md's own invariant is that it
    /// "replaces what is on screen rather than covering it", which is what a
    /// screen does and what a dialog does not.
    /// </summary>
    public sealed class ScreenTests
    {
        [Test]
        public void Screen_HasExactlyTheFiveNamedMembers()
        {
            var members = Enum.GetNames(typeof(Screen));

            Assert.That(members, Is.EquivalentTo(new[]
            {
                "TitleScreen", "GameSetup", "GameBoard", "GameOver", "HowToPlay"
            }));
        }
    }
}
