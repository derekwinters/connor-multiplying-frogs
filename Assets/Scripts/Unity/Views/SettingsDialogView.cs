using System;
using UnityEngine;
using UnityEngine.UI;
// UnityEngine.UI also declares a Button type — the same collision
// TitleScreenView.cs and GameBoardScreenView.cs work around — so these are
// pulled in by explicit alias rather than a wildcard `using Frogs.Unity.UI;`,
// and a bare `Button`, `ButtonKind` or `DialogPanel` in this file always means
// the shared component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
// Only one Core type is reachable from this dialog at all, and it only parses
// a string — pulled in by alias so that stays visible at a glance.
using AppVersion = Frogs.Core.AppVersion;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// The settings dialog — docs/specs/ui/settings-dialog.md, built to that
    /// page and its committed 1:1 mockup. The one screen in the POC that
    /// exists purely so a player can get *out* — of a confusing rule, or of
    /// the game itself — without anything on the board underneath being
    /// disturbed by the looking.
    ///
    /// It is a composition of the shared <see cref="DialogPanel"/> (#219) and
    /// the shared <see cref="Button"/> (#214) and nothing else: no new visual
    /// component, no sprite, no bitmap.
    ///
    /// Three things it deliberately does not do:
    ///
    /// - **It cannot change the game.** settings-dialog.md's own first
    ///   invariant — "opening this changes nothing about the game. It is a
    ///   menu, not a turn" — is structural here, not a promise: this type is
    ///   never handed a <c>Game</c> or a <c>Lane</c>, so there is no method it
    ///   could call. There is no difficulty setting, no player count, no undo,
    ///   because there is nothing here that could reach one.
    /// - **`End the game` never ends the game.** It raises
    ///   <see cref="EndGameConfirmRequested"/> and stops. Ending the game —
    ///   standings, leaving the board — belongs entirely to
    ///   docs/specs/ui/end-game-confirm.md (#226), which is not built yet;
    ///   this view wires the transition, not the target.
    /// - **It does not listen for hardware back.** The router (#213) already
    ///   routes back on <c>Dialog.Settings</c> to what `Back to the game`
    ///   does; this view exposes that one action as
    ///   <see cref="RequestClose"/> rather than adding a second opinion about
    ///   the key.
    ///
    /// `How to play` ships present and disabled. That is the answer
    /// settings-dialog.md's own open question already proposes — "present. A
    /// disabled button that appears later is less confusing than a button that
    /// appears from nowhere" — not a decision made here: the screen it would
    /// open has no wireframe, so under rule 8 it cannot be built, and it is
    /// wired to nothing at all.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SettingsDialogView : MonoBehaviour
    {
        // docs/specs/ui/settings-dialog.md#named-constants.
        public const float SettingsDialogWidth = 900f;
        public const float SettingsDialogHeight = 760f;
        public const float SettingsActionWidth = 788f;
        public const float SettingsActionGap = 96f;
        public const float SettingsVersionBottomOffset = 60f;

        // The rest of that table is other pages' rows, referenced under the
        // identical name rather than redeclared here — the same line #220 drew
        // for `LanePositionCount`:
        //
        // - `ButtonDestructiveGap` is the shared Button's own constant
        //   (shared-components.md#button); settings-dialog.md restates it
        //   rather than inventing a second one.
        // - the panel's padding, corner radius, title metrics, scrim and fade
        //   are the shared Dialog's (shared-components.md#dialog).
        // - `VersionLabelSize` is title-screen.md's — the same value doing the
        //   same job, a quiet version readout bottom-left.

        const string TitleLabel = "Settings";
        const string HowToPlayLabel = "How to play";
        const string EndGameLabel = "End the game";
        const string BackToTheGameLabel = "Back to the game";

        const string VersionPrefix = "v";

        // No imported font — matches Button.cs's and TitleScreenView.cs's own
        // choice, for the same reason (no external assets).
        const string BuiltinLabelFontName = "LegacyRuntime.ttf";

        // Chrome colour copied verbatim from the committed mockup's CSS custom
        // properties, the same line Button.cs and TitleScreenView.cs both draw:
        // not a geometry constant on any spec page's table, so not declared as
        // a named spec constant.
        static readonly Color VersionColor = new Color32(0x6C, 0x78, 0x73, 0xFF); // mockup's --line

        RectTransform _rect;
        DialogPanel _dialog;

        RectTransform _actionsRect;
        Button _howToPlayButton;
        Button _endGameButton;

        RectTransform _footprintRect;
        Text _versionText;

        Button _backToTheGameButton;

        bool _initialized;

        /// <summary>
        /// `Back to the game` was pressed, or hardware back arrived through
        /// <see cref="RequestClose"/>. Closing returns to exactly the board
        /// state that was there — same turn, same positions, same enabled
        /// `Roll` — which is free, because this view never touched it.
        /// </summary>
        public event Action CloseRequested;

        /// <summary>
        /// `End the game` was pressed: open
        /// docs/specs/ui/end-game-confirm.md (#226). This is the whole of what
        /// that button does. Nothing here ends a game.
        /// </summary>
        public event Action EndGameConfirmRequested;

        /// <summary>The view's own <see cref="RectTransform"/>, filling the whole canvas — which is the reference canvas or larger.</summary>
        public RectTransform RectTransform
        {
            get
            {
                EnsureInitialized();
                return _rect;
            }
        }

        /// <summary>The shared Dialog this screen is laid out on, at <see cref="SettingsDialogWidth"/> x <see cref="SettingsDialogHeight"/>.</summary>
        public DialogPanel Dialog
        {
            get
            {
                EnsureInitialized();
                return _dialog;
            }
        }

        /// <summary>`actions` — the single left-aligned column `How to play` and `End the game` sit in.</summary>
        public RectTransform ActionsRect
        {
            get
            {
                EnsureInitialized();
                return _actionsRect;
            }
        }

        /// <summary>`footprint` — the region the version readout sits in, bottom-left.</summary>
        public RectTransform FootprintRect
        {
            get
            {
                EnsureInitialized();
                return _footprintRect;
            }
        }

        /// <summary>`How to play` — secondary, <see cref="SettingsActionWidth"/> wide, present and disabled.</summary>
        public Button HowToPlayButton
        {
            get
            {
                EnsureInitialized();
                return _howToPlayButton;
            }
        }

        /// <summary>`End the game` — destructive, <see cref="SettingsActionWidth"/> wide. Opens the confirm; ends nothing.</summary>
        public Button EndGameButton
        {
            get
            {
                EnsureInitialized();
                return _endGameButton;
            }
        }

        /// <summary>`Back to the game` — primary, at the shared Button's own default footprint, bottom-right.</summary>
        public Button BackToTheGameButton
        {
            get
            {
                EnsureInitialized();
                return _backToTheGameButton;
            }
        }

        /// <summary>The version readout — read from <see cref="AppVersion"/>, never typed.</summary>
        public Text VersionText
        {
            get
            {
                EnsureInitialized();
                return _versionText;
            }
        }

        void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Opens the dialog — the shared Dialog's own cross-fade in.</summary>
        public void Open()
        {
            EnsureInitialized();
            _dialog.Open();
        }

        /// <summary>Closes the dialog — the shared Dialog's own cross-fade out.</summary>
        public void Close()
        {
            EnsureInitialized();
            _dialog.Close();
        }

        /// <summary>
        /// The one action `Back to the game` and hardware back both invoke —
        /// shared-components.md#dialog: "the hardware back button does what
        /// the dialog's least destructive button does, and never what its most
        /// destructive one does." Public so the router's back handling (#213)
        /// can call it directly, which is also how an EditMode test simulates
        /// hardware back. There is deliberately no second key handler here.
        /// </summary>
        public void RequestClose()
        {
            EnsureInitialized();

            var handler = CloseRequested;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>
        /// The line `footprint` shows — asserted the same way
        /// <c>HelloWorldProbeTests</c> asserts <c>HelloWorldProbe.Describe</c>:
        /// against this static, total formatting method taking the build-name
        /// string as a parameter, not against a live <c>Application.version</c>
        /// at test time.
        ///
        /// Total in the same sense <c>Describe</c> is. <c>ReadFromBuildName</c>
        /// throws on a stamp it cannot parse, by design, so a bad value fails
        /// loudly at the point of reading — but a broken build stamp must not
        /// stop the settings dialog opening, because the settings dialog is
        /// the way out of a game. settings-dialog.md does not say what an
        /// unreadable version should display, so nothing new is invented here:
        /// it shows what the build stamped, and does not throw.
        ///
        /// This mirrors <see cref="TitleScreenView.FormatVersionLabel"/> rather
        /// than calling it: that one is #216's, already built and tested, and
        /// folding both onto a shared helper is a refactor of a merged file
        /// with no editor here to re-verify it — the same call #219 made about
        /// <c>Button</c>'s inline rounded-rect generator. See this issue's PR.
        /// </summary>
        public static string FormatVersionLabel(string applicationVersion)
        {
            try
            {
                return VersionPrefix + AppVersion.ReadFromBuildName(applicationVersion);
            }
            catch (Exception error) when (error is FormatException || error is ArgumentNullException)
            {
                return VersionPrefix + applicationVersion;
            }
        }

        // Unity does not guarantee Awake() has run before another component's
        // Awake() or a test reaches this one right after AddComponent — the
        // same reasoning as Button, DialogPanel and GameBoardScreenView's own
        // EnsureInitialized. Every public entry point funnels through this
        // idempotent guard instead of trusting Awake's timing.
        void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            BuildHierarchy();
        }

        void BuildHierarchy()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect == null)
            {
                _rect = gameObject.AddComponent<RectTransform>();
            }

            // The whole canvas, not the 1920 x 1200 reference rectangle. This
            // dialog paints no background of its own — what it lays over the
            // screen is the shared Dialog's scrim, and that has to reach the
            // edges on a device that is not 16:10. The panel underneath is
            // centre-anchored, so it does not move.
            StretchToFill(_rect);

            BuildDialog();
            BuildActions();
            BuildFootprint();
            BuildControls();
        }

        void BuildDialog()
        {
            var dialogGO = new GameObject("SettingsDialog", typeof(RectTransform));
            dialogGO.transform.SetParent(_rect, worldPositionStays: false);

            _dialog = dialogGO.AddComponent<DialogPanel>();
            _dialog.SetTitle(TitleLabel);

            // The shared Dialog at this screen's own size, in place of
            // DialogMaxWidth/DialogMaxHeight. Everything else about it — the
            // scrim, the corners, the padding, the title metrics, the
            // cross-fade — is inherited unchanged.
            _dialog.SetSize(SettingsDialogWidth, SettingsDialogHeight);
        }

        void BuildActions()
        {
            // The actions column is placed from the *bottom* of the panel up,
            // not from the top down. That is what makes settings-dialog.md's
            // Anchors section ("`End the game` is separated from everything
            // below it by `ButtonDestructiveGap` — that gap is the layout, not
            // decoration") a fact of the layout rather than a consequence of
            // arithmetic: a longer label, a different title, or a shorter
            // panel cannot squeeze it.
            var columnBottom = DialogPanel.DialogPadding + Button.ButtonHeight + Button.ButtonDestructiveGap;
            var columnHeight = (Button.ButtonHeight * 2f) + SettingsActionGap;

            var actionsGO = new GameObject("Actions", typeof(RectTransform));
            _actionsRect = (RectTransform)actionsGO.transform;
            _actionsRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            _actionsRect.anchorMin = Vector2.zero;
            _actionsRect.anchorMax = Vector2.zero;
            _actionsRect.pivot = Vector2.zero;
            _actionsRect.sizeDelta = new Vector2(SettingsActionWidth, columnHeight);
            _actionsRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, columnBottom);

            // `How to play`, then `End the game` — the column's top and
            // bottom, SettingsActionGap apart because that is what is left
            // between them.
            _howToPlayButton = CreateActionButton("HowToPlay", ButtonKind.Secondary, HowToPlayLabel, atTop: true);

            // Present, and disabled — settings-dialog.md's own proposed answer
            // to its open question. Deliberately wired to nothing: the screen
            // it would open has no wireframe, so there is no destination to
            // route to and none is invented here. A tap does nothing at all,
            // silently, per the shared Button's own behaviour.
            _howToPlayButton.SetDisabled(true);

            _endGameButton = CreateActionButton("EndTheGame", ButtonKind.Destructive, EndGameLabel, atTop: false);
            _endGameButton.Clicked += HandleEndGameClicked;
        }

        Button CreateActionButton(string name, ButtonKind kind, string label, bool atTop)
        {
            var buttonGO = new GameObject(name, typeof(RectTransform));
            buttonGO.transform.SetParent(_actionsRect, worldPositionStays: false);

            var button = buttonGO.AddComponent<Button>();
            button.SetKind(kind);
            button.SetLabelText(label);

            // Only the width is overridden — the shared ButtonHeight and
            // ButtonLabelSize both stand, per the mockup.
            button.SetSize(SettingsActionWidth, Button.ButtonHeight);

            var rect = button.RectTransform;
            rect.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            rect.anchorMax = new Vector2(0f, atTop ? 1f : 0f);
            rect.pivot = new Vector2(0f, atTop ? 1f : 0f);
            rect.anchoredPosition = Vector2.zero;

            return button;
        }

        void BuildFootprint()
        {
            var footprintGO = new GameObject("Footprint", typeof(RectTransform));
            _footprintRect = (RectTransform)footprintGO.transform;
            _footprintRect.SetParent(_dialog.PanelRect, worldPositionStays: false);
            StretchToFill(_footprintRect);

            var versionGO = new GameObject("Version", typeof(RectTransform), typeof(Text));
            _versionText = versionGO.GetComponent<Text>();
            _versionText.font = Resources.GetBuiltinResource<Font>(BuiltinLabelFontName);
            _versionText.fontSize = (int)TitleScreenView.VersionLabelSize;
            _versionText.alignment = TextAnchor.LowerLeft;
            _versionText.color = VersionColor;
            _versionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _versionText.verticalOverflow = VerticalWrapMode.Overflow;
            _versionText.raycastTarget = false;

            // What the build stamped, parsed by Core. Nothing here composes,
            // stores, or hard-codes a version literal.
            _versionText.text = FormatVersionLabel(Application.version);

            var versionRect = _versionText.rectTransform;
            versionRect.SetParent(_footprintRect, worldPositionStays: false);
            versionRect.anchorMin = Vector2.zero;
            versionRect.anchorMax = Vector2.zero;
            versionRect.pivot = Vector2.zero;
            versionRect.sizeDelta = Vector2.zero;
            versionRect.anchoredPosition = new Vector2(DialogPanel.DialogPadding, SettingsVersionBottomOffset);
        }

        void BuildControls()
        {
            // The primary button, bottom-right, at the shared Dialog's own
            // button row — and this dialog's least destructive button, which
            // is the value the router reads for hardware back.
            _backToTheGameButton = _dialog.AddButton(
                ButtonKind.Primary,
                BackToTheGameLabel,
                RequestClose,
                isLeastDestructive: true);
        }

        void HandleEndGameClicked()
        {
            // Opens the confirm. That is all — and all it *can* do.
            var handler = EndGameConfirmRequested;
            if (handler != null)
            {
                handler();
            }
        }

        static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
