using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Frogs.Core;
using CoreScreen = Frogs.Core.Screen;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The thin adapter's own two jobs — issue #213: pressing the hardware
    /// back key reaches the Core router's <c>HandleBack()</c>, and exactly
    /// one screen root is active at a time. What the router actually does on
    /// back, for every row of the back-button table, is <c>Tests/Core</c>'s
    /// job (<c>ScreenRouterTests</c>) and is not re-asserted here — this
    /// suite only proves the wiring, not the table.
    /// </summary>
    public sealed class ScreenRouterAdapterTests
    {
        [Test]
        public void PressingBack_CallsTheCoreRoutersHandleBack()
        {
            var host = new GameObject(nameof(ScreenRouterAdapterTests));

            try
            {
                var adapter = host.AddComponent<ScreenRouterAdapter>();
                adapter.Router.NavigateToScreen(CoreScreen.GameBoard);

                adapter.HandleBackButton();

                // game-board.md#behaviour: hardware back opens the settings
                // dialog — evidence the key press actually reached
                // ScreenRouter.HandleBack(), not just that nothing threw.
                Assert.That(adapter.Router.CurrentDialog, Is.EqualTo(Dialog.Settings));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnlyTheCurrentScreensRoot_IsActiveAtATime()
        {
            var host = new GameObject(nameof(ScreenRouterAdapterTests));

            try
            {
                var adapter = host.AddComponent<ScreenRouterAdapter>();

                // The router starts on the title screen.
                AssertExactlyOneActiveRoot(adapter, CoreScreen.TitleScreen);

                adapter.Router.NavigateToScreen(CoreScreen.GameBoard);

                AssertExactlyOneActiveRoot(adapter, CoreScreen.GameBoard);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static void AssertExactlyOneActiveRoot(ScreenRouterAdapter adapter, CoreScreen expectedActive)
        {
            var screens = (CoreScreen[])Enum.GetValues(typeof(CoreScreen));
            var active = screens.Where(screen => adapter.RootFor(screen).activeSelf).ToArray();

            Assert.That(active, Is.EqualTo(new[] { expectedActive }));
        }
    }
}
