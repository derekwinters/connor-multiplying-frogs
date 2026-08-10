using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Unity.UI;
using UnityImage = UnityEngine.UI.Image;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The shared Dialog — issue #219. Built through the typed Unity API
    /// with no committed prefab, the same decision #214 made for
    /// <see cref="Button"/>. Written before <c>DialogPanel</c> exists, per
    /// docs/engineering/testing.md's sanctioned flow: there is no editor
    /// here to see these fail, so they were written first and pushed
    /// unexecuted, and CI is what turns them red before it turns them green.
    /// </summary>
    public sealed class DialogPanelTests
    {
        [Test]
        public void DialogMaxWidthAndHeight_AreTheCanvasInsetByA48PxMarginOnEverySide()
        {
            // docs/specs/ui/shared-components.md#dialog: "DialogMaxWidth and
            // DialogMaxHeight are the canvas inset by 48 px on every side."
            // The 1920 x 1200 canvas and the 48 px margin are independently
            // known here (docs/engineering/tech-stack.md's reference
            // resolution, and shared-components.md's own inset), so this
            // proves the relationship rather than just the two totals.
            const float canvasWidth = 1920f;
            const float canvasHeight = 1200f;
            const float margin = 48f;

            Assert.That(DialogPanel.DialogMaxWidth, Is.EqualTo(canvasWidth - (margin * 2f)));
            Assert.That(DialogPanel.DialogMaxWidth, Is.EqualTo(1824f));
            Assert.That(DialogPanel.DialogMaxHeight, Is.EqualTo(canvasHeight - (margin * 2f)));
            Assert.That(DialogPanel.DialogMaxHeight, Is.EqualTo(1104f));
        }

        [Test]
        public void EveryDialogConstant_ExistsAsANamedValue()
        {
            // The nine named constants from shared-components.md#dialog's
            // table, nothing free to invent.
            Assert.That(DialogPanel.DialogScrimOpacity, Is.EqualTo(0.66f));
            Assert.That(DialogPanel.DialogRadius, Is.EqualTo(32f));
            Assert.That(DialogPanel.DialogPadding, Is.EqualTo(56f));
            Assert.That(DialogPanel.DialogTitleSize, Is.EqualTo(56f));
            Assert.That(DialogPanel.DialogTitleGap, Is.EqualTo(40f));
            Assert.That(DialogPanel.DialogButtonRowGap, Is.EqualTo(48f));
            Assert.That(DialogPanel.DialogMaxWidth, Is.EqualTo(1824f));
            Assert.That(DialogPanel.DialogMaxHeight, Is.EqualTo(1104f));
            Assert.That(DialogPanel.DialogFadeDuration, Is.EqualTo(0.15f));
        }

        [Test]
        public void PanelSize_AtOrBelowTheCap_RendersUnchanged()
        {
            // end-game-confirm.md's committed 1160 x 540 — below the cap,
            // honoured unchanged, not clamped to it.
            var dialog = CreateDialog();

            try
            {
                dialog.SetSize(1160f, 540f);

                Assert.That(dialog.PanelRect.sizeDelta, Is.EqualTo(new Vector2(1160f, 540f)));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void PanelSize_AboveTheCapOnEitherAxis_IsClampedDownToTheCap()
        {
            var dialog = CreateDialog();

            try
            {
                dialog.SetSize(2200f, 1500f);

                Assert.That(
                    dialog.PanelRect.sizeDelta,
                    Is.EqualTo(new Vector2(DialogPanel.DialogMaxWidth, DialogPanel.DialogMaxHeight)),
                    "the cap is a ceiling, not the panel's actual size — a caller-supplied size at or below it must not be touched");
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Dialog_ComposesAScrimTitleBodyAndButtonRow_FromTheSharedButton()
        {
            var dialog = CreateDialog();

            try
            {
                dialog.SetTitle("End the game for everyone?");
                var button = dialog.AddButton(ButtonKind.Primary, "Keep playing", onClick: null);

                Assert.That(dialog.Scrim, Is.Not.Null, "a Dialog must always compose a scrim");
                Assert.That(dialog.TitleText, Is.Not.Null);
                Assert.That(dialog.TitleText.text, Is.EqualTo("End the game for everyone?"));
                Assert.That(dialog.BodyRect, Is.Not.Null, "a Dialog must always compose a body area");
                Assert.That(dialog.ButtonRowRect, Is.Not.Null);
                Assert.That(button, Is.InstanceOf<Button>(), "the button row is built from the shared Button (#214)");
                Assert.That(dialog.Buttons, Does.Contain(button));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Dialog_WiresItsBuiltInSpriteAndFont_RatherThanLeavingThemMissing()
        {
            var dialog = CreateDialog();

            try
            {
                var panelImage = dialog.PanelRect.GetComponent<UnityImage>();

                Assert.That(panelImage.sprite, Is.Not.Null, "built-in panel sprite missing — check the resource name");
                Assert.That(dialog.TitleText.font, Is.Not.Null, "built-in title font missing — check the resource name");
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Scrim_RendersAtDialogScrimOpacity_WheneverTheDialogIsOpen_AndIsNeverAbsent()
        {
            var dialog = CreateDialog();

            try
            {
                // Present as soon as the component exists — a Dialog never
                // renders without a scrim, whether open or not.
                Assert.That(dialog.Scrim, Is.Not.Null);

                dialog.Open();

                Assert.That(dialog.Scrim.color.a, Is.EqualTo(DialogPanel.DialogScrimOpacity).Within(0.0001f));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void FadeDriver_IsSeededFromDialogFadeDuration()
        {
            var dialog = CreateDialog();

            try
            {
                dialog.Open();
                dialog.AdvanceFade(0f);
                Assert.That(dialog.CanvasGroup.alpha, Is.EqualTo(0f).Within(0.0001f));

                dialog.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);
                Assert.That(dialog.CanvasGroup.alpha, Is.EqualTo(0.5f).Within(0.0001f));

                dialog.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);
                Assert.That(dialog.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void OpenAndClose_NeverMoveThePanel_NoSlideNoBounce()
        {
            var dialog = CreateDialog();

            try
            {
                var startPosition = dialog.PanelRect.anchoredPosition;

                dialog.Open();
                dialog.AdvanceFade(DialogPanel.DialogFadeDuration / 3f);
                Assert.That(dialog.PanelRect.anchoredPosition, Is.EqualTo(startPosition));

                dialog.AdvanceFade(DialogPanel.DialogFadeDuration);
                Assert.That(dialog.PanelRect.anchoredPosition, Is.EqualTo(startPosition));

                dialog.Close();
                dialog.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);
                Assert.That(dialog.PanelRect.anchoredPosition, Is.EqualTo(startPosition));

                dialog.AdvanceFade(DialogPanel.DialogFadeDuration);
                Assert.That(dialog.PanelRect.anchoredPosition, Is.EqualTo(startPosition));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Dialog_HasNoTapOutsideToDismiss_AndNoCloseCross()
        {
            var dialog = CreateDialog();

            try
            {
                Assert.That(
                    dialog.Scrim.GetComponent<IPointerClickHandler>(),
                    Is.Null,
                    "the scrim must not be a dismiss handler — a dialog that decides something is left only by its own buttons");

                var childNames = dialog.RectTransform
                    .GetComponentsInChildren<Transform>(includeInactive: true)
                    .Select(t => t.name.ToLowerInvariant());

                Assert.That(childNames, Has.None.Contains("close"), "no close-cross element anywhere in the hierarchy");
                Assert.That(childNames, Has.None.Contains("dismiss"));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Dialog_ExposesWhichButtonIsLeastDestructive()
        {
            var dialog = CreateDialog();

            try
            {
                var destructive = dialog.AddButton(ButtonKind.Destructive, "End the game", onClick: null);
                var keepPlaying = dialog.AddButton(ButtonKind.Primary, "Keep playing", onClick: null, isLeastDestructive: true);

                Assert.That(dialog.LeastDestructiveButton, Is.SameAs(keepPlaying));
                Assert.That(dialog.LeastDestructiveButton, Is.Not.SameAs(destructive));
            }
            finally
            {
                Destroy(dialog);
            }
        }

        [Test]
        public void Open_TakesNoOrderingOrParentDialogArgument_AndExposesNoZOrderOrTopmostConcept()
        {
            // Proves this component cannot itself produce the "Stacked"
            // state or a second live instance — the router's dialog layer
            // (#213) is the sole place at-most-one-dialog is enforced, and
            // this Dialog carries no field, method, or argument that would
            // let it, or a caller, compete with that guarantee.
            var openMethod = typeof(DialogPanel).GetMethod("Open");
            Assert.That(openMethod, Is.Not.Null);
            Assert.That(openMethod.GetParameters(), Is.Empty, "Open() takes no ordering or parent-dialog argument");

            var forbidden = new[] { "zorder", "z-order", "topmost", "toplevel", "parentdialog", "sortorder", "stackindex", "priority" };
            var memberNames = typeof(DialogPanel)
                .GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
                .Select(m => m.Name.ToLowerInvariant());

            foreach (var forbiddenTerm in forbidden)
            {
                Assert.That(memberNames, Has.None.Contains(forbiddenTerm), $"DialogPanel must expose no '{forbiddenTerm}' concept");
            }
        }

        static DialogPanel CreateDialog()
        {
            var host = new GameObject(nameof(DialogPanelTests), typeof(RectTransform));
            return host.AddComponent<DialogPanel>();
        }

        static void Destroy(DialogPanel dialog)
        {
            if (dialog != null)
            {
                Object.DestroyImmediate(dialog.gameObject);
            }
        }
    }
}
