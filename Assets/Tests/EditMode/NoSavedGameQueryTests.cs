using NUnit.Framework;
using Frogs.Unity;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The only <see cref="ISavedGameQuery"/> in this v0.2 shape-only proof
    /// of concept — see issue #216's PR for why this is a query object
    /// rather than the title screen hardcoding "RESUME never exists".
    /// </summary>
    public sealed class NoSavedGameQueryTests
    {
        [Test]
        public void AlwaysReports_ThatThereIsNoSavedGame()
        {
            Assert.That(new NoSavedGameQuery().HasSavedGame(), Is.False);
        }
    }
}
