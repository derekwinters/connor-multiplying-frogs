using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// The rules a player's name obeys, which are Core's and not the text
    /// field's — docs/specs/ui/game-setup.md.
    /// </summary>
    public sealed class PlayerNameTests
    {
        // docs/specs/ui/game-setup.md#named-constants — PlayerNameMaxLength
        // 10, read off the setup seat's name row.
        [Test]
        public void TheCap_IsTenCharacters()
        {
            Assert.That(PlayerName.PlayerNameMaxLength, Is.EqualTo(10));
        }

        // At the boundary exactly, not near it: ten characters is a name the
        // seat can draw and survives untouched; eleven is one it cannot.
        [Test]
        public void AtTheCapExactly_TheNameSurvivesWhole()
        {
            var tenCharacters = "Alexandra";

            Assert.That(tenCharacters.Length, Is.EqualTo(PlayerName.PlayerNameMaxLength - 1));
            Assert.That(PlayerName.Resolve("Alexandras", FrogColour.Green), Is.EqualTo("Alexandras"));
            Assert.That("Alexandras".Length, Is.EqualTo(PlayerName.PlayerNameMaxLength));
        }

        [Test]
        public void OneCharacterPastTheCap_IsCutBackToIt_ByCoreAndNotByTheTextField()
        {
            var elevenCharacters = "Alexandras!";
            Assert.That(elevenCharacters.Length, Is.EqualTo(PlayerName.PlayerNameMaxLength + 1));

            var resolved = PlayerName.Resolve(elevenCharacters, FrogColour.Green);

            Assert.That(resolved, Is.EqualTo("Alexandras"));
            Assert.That(resolved.Length, Is.EqualTo(PlayerName.PlayerNameMaxLength));
        }
    }
}
