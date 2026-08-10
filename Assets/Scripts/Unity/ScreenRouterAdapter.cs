using System;
using System.Collections.Generic;
using Frogs.Core;
using UnityEngine;
using CoreScreen = Frogs.Core.Screen;

namespace Frogs.Unity
{
    /// <summary>
    /// The thin shell around <see cref="ScreenRouter"/> — issue #213. It owns
    /// the router's engine-facing surface and nothing about navigation logic:
    /// it wires the hardware back key (Android back / <c>Escape</c>) to
    /// <see cref="ScreenRouter.HandleBack"/>, and activates the one empty
    /// root <c>GameObject</c> per <see cref="Frogs.Core.Screen"/> and
    /// <see cref="Dialog"/> that matches the router's current state.
    ///
    /// It draws nothing — no marker, no text, no shapes. What a root looks
    /// like is a later, wireframed issue; this type only decides which root
    /// is active.
    /// </summary>
    public sealed class ScreenRouterAdapter : MonoBehaviour
    {
        readonly ScreenRouter _router = new ScreenRouter();
        readonly Dictionary<CoreScreen, GameObject> _screenRoots = new Dictionary<CoreScreen, GameObject>();
        readonly Dictionary<Dialog, GameObject> _dialogRoots = new Dictionary<Dialog, GameObject>();

        /// <summary>The Core router this adapter wires to the engine.</summary>
        public ScreenRouter Router
        {
            get { return _router; }
        }

        void Awake()
        {
            foreach (CoreScreen screen in Enum.GetValues(typeof(CoreScreen)))
            {
                _screenRoots[screen] = CreateRoot(screen.ToString());
            }

            foreach (Dialog dialog in Enum.GetValues(typeof(Dialog)))
            {
                _dialogRoots[dialog] = CreateRoot(dialog.ToString());
            }

            _router.StateChanged += RefreshActiveRoots;
            RefreshActiveRoots();
        }

        void OnDestroy()
        {
            _router.StateChanged -= RefreshActiveRoots;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }
        }

        /// <summary>
        /// What pressing the hardware back key does: calls into the Core
        /// router's <see cref="ScreenRouter.HandleBack"/>, then quits the app
        /// if that requested an exit. A public method of its own, rather than
        /// reachable only through <see cref="Update"/>, so an EditMode test
        /// can call it directly without simulating a key press.
        /// </summary>
        public void HandleBackButton()
        {
            _router.HandleBack();

            if (_router.AppExitRequested)
            {
                Quit();
            }
        }

        /// <summary>The root <c>GameObject</c> for a screen — active only while it is current.</summary>
        public GameObject RootFor(CoreScreen screen)
        {
            return _screenRoots[screen];
        }

        /// <summary>The root <c>GameObject</c> for a dialog — active only while it is current.</summary>
        public GameObject RootFor(Dialog dialog)
        {
            return _dialogRoots[dialog];
        }

        GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, worldPositionStays: false);
            root.SetActive(false);
            return root;
        }

        // A screen's root is active purely by CurrentScreen — dialogs are
        // drawn over whichever screen is current, not in place of it, per
        // issue #213's navigation graph: "[Bracketed] nodes are dialogs,
        // drawn over whichever full screen is current rather than replacing
        // it."
        void RefreshActiveRoots()
        {
            foreach (var pair in _screenRoots)
            {
                pair.Value.SetActive(pair.Key == _router.CurrentScreen);
            }

            foreach (var pair in _dialogRoots)
            {
                pair.Value.SetActive(_router.CurrentDialog.HasValue && pair.Key == _router.CurrentDialog.Value);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
