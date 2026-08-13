using NUnit.Framework;
using Frogs.Core;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// One naming session — what the setup screen's keyboard is a set of
    /// buttons on top of. docs/specs/ui/game-setup.md#behaviour is the whole
    /// specification: typing appends, the cap refuses, backspace deletes, and
    /// `Done` puts a blank name back to the colour name.
    ///
    /// It is Core's rather than the view's so that "the eleventh keystroke
    /// does nothing" is a rule with a two-second test around it instead of a
    /// rule the keyboard happens to follow.
    /// </summary>
    public sealed class PlayerNameEditorTests
    {
        [Test]
        public void OpeningAnEditor_StartsFromTheNameTheSeatAlreadyCarries()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Green));

            Assert.That(editor.Text, Is.EqualTo("Green"));
            Assert.That(editor.Colour, Is.EqualTo(FrogColour.Green));
        }

        [Test]
        public void Typing_AppendsTheCharacter()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Green));
            editor.Clear();

            Assert.That(editor.Append('D'), Is.True);
            Assert.That(editor.Append('a'), Is.True);
            Assert.That(editor.Append('d'), Is.True);

            Assert.That(editor.Text, Is.EqualTo("Dad"));
        }

        // docs/specs/ui/game-setup.md#behaviour: "At `PlayerNameMaxLength` the
        // next keystroke is refused — the key does nothing, the name is
        // unchanged, and nothing explains itself." The boundary exactly: the
        // tenth character lands, the eleventh does not.
        [Test]
        public void AtTheCap_TheNextKeystrokeIsRefusedSilently_AndTheNameIsUnchanged()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Green));
            editor.Clear();

            for (var i = 0; i < PlayerName.PlayerNameMaxLength; i++)
            {
                Assert.That(editor.Append('a'), Is.True, $"keystroke {i + 1} of the cap should land");
            }

            Assert.That(editor.Text.Length, Is.EqualTo(PlayerName.PlayerNameMaxLength));

            var atTheCap = editor.Text;
            Assert.That(editor.Append('b'), Is.False);
            Assert.That(editor.Text, Is.EqualTo(atTheCap));
        }

        [Test]
        public void Backspace_DeletesTheLastCharacter_AndDoesNothingOnAnEmptyName()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Pink));

            editor.Backspace();
            Assert.That(editor.Text, Is.EqualTo("Pin"));

            editor.Clear();
            Assert.That(editor.Text, Is.Empty);

            editor.Backspace();
            Assert.That(editor.Text, Is.Empty);
        }

        [Test]
        public void Done_OnATypedName_KeepsIt()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Blue));
            editor.Clear();
            foreach (var character in "Connor")
            {
                editor.Append(character);
            }

            var committed = editor.Commit();

            Assert.That(committed.Colour, Is.EqualTo(FrogColour.Blue));
            Assert.That(committed.Name, Is.EqualTo("Connor"));
        }

        // docs/specs/ui/game-setup.md#behaviour: "Clearing the name to empty
        // and pressing `Done` restores the frog's colour name."
        [Test]
        public void Done_OnAClearedName_RestoresTheColourName()
        {
            var editor = new PlayerNameEditor(new RosterEntry(FrogColour.Blue).WithName("Connor"));
            editor.Clear();

            Assert.That(editor.Commit().Name, Is.EqualTo("Blue"));
        }
    }
}
