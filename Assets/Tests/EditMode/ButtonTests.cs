using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Unity.UI;
// UnityEngine.UI also declares a Button type, so it is deliberately not
// imported wholesale here — only Image is needed, qualified below, so a bare
// `Button` in this file always means Frogs.Unity.UI.Button.
using UnityImage = UnityEngine.UI.Image;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The shared Button — issue #214. Built through the typed Unity API with
    /// no committed prefab (docs/engineering/unity-serialization.md's
    /// <c>FrogPrefab_HasItsSpriteAndScriptWiredUp</c> pattern, applied to a
    /// component that builds itself instead of one loaded from an asset), so
    /// these tests prove the pieces the constructor wires together rather than
    /// asserting on a `.prefab` file.
    /// </summary>
    public sealed class ButtonTests
    {
        [Test]
        public void Button_WiresItsBuiltInSpriteAndFont_RatherThanLeavingThemMissing()
        {
            // docs/engineering/unity-serialization.md's
            // FrogPrefab_HasItsSpriteAndScriptWiredUp pattern, applied to a
            // component built at runtime instead of loaded from an asset:
            // Resources.GetBuiltinResource fails silently (returns null, not
            // an exception) if the built-in resource name is wrong, so assert
            // the wiring rather than trusting the lookup succeeded.
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                var border = button.RectTransform.Find("Visual/Border").GetComponent<UnityImage>();
                var fill = button.RectTransform.Find("Visual/Fill").GetComponent<UnityImage>();

                Assert.That(border.sprite, Is.Not.Null, "built-in button sprite missing — check the resource name");
                Assert.That(fill.sprite, Is.Not.Null, "built-in button sprite missing — check the resource name");
                Assert.That(button.Label.font, Is.Not.Null, "built-in label font missing — check the resource name");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void ThreeKinds_ShareIdenticalGeometry_AndDifferOnlyInColour()
        {
            var primary = CreateButton(ButtonKind.Primary);
            var secondary = CreateButton(ButtonKind.Secondary);
            var destructive = CreateButton(ButtonKind.Destructive);

            try
            {
                foreach (var button in new[] { primary, secondary, destructive })
                {
                    // docs/specs/ui/shared-components.md#button: "All three
                    // share ButtonHeight, ButtonMinWidth, ButtonPaddingX,
                    // ButtonRadius and ButtonLabelSize exactly."
                    Assert.That(
                        button.RectTransform.sizeDelta,
                        Is.EqualTo(new Vector2(Button.ButtonMinWidth, Button.ButtonHeight)),
                        $"{button.Kind} must default to the shared ButtonMinWidth x ButtonHeight");
                    Assert.That(button.Label.fontSize, Is.EqualTo((int)Button.ButtonLabelSize));

                    var labelRect = button.Label.rectTransform;
                    Assert.That(labelRect.offsetMin.x, Is.EqualTo(Button.ButtonPaddingX));
                    Assert.That(labelRect.offsetMax.x, Is.EqualTo(-Button.ButtonPaddingX));
                }

                var signatures = new[] { primary, secondary, destructive }
                    .Select(button => (button.FillColor, button.BorderColor, button.LabelColor))
                    .Distinct()
                    .Count();

                Assert.That(signatures, Is.EqualTo(3), "each of the three kinds must have its own colour signature");
            }
            finally
            {
                Destroy(primary);
                Destroy(secondary);
                Destroy(destructive);
            }
        }

        [Test]
        public void Button_IsNeverSmallerThanMinTouchTarget_AtAnyKindOrCallerSuppliedSize()
        {
            foreach (var kind in new[] { ButtonKind.Primary, ButtonKind.Secondary, ButtonKind.Destructive })
            {
                var button = CreateButton(kind);

                try
                {
                    button.SetSize(10f, 10f);

                    Assert.That(button.RectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
                    Assert.That(button.RectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
                }
                finally
                {
                    Destroy(button);
                }
            }
        }

        [Test]
        public void CallerSuppliedSize_LargerThanMinTouchTarget_IsRespected()
        {
            // title-screen.md gives its buttons their own 560 x 160 —
            // "never differ in size" is a rule about the three kinds, not a
            // ban on a screen sizing its own instance.
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                button.SetSize(560f, 160f);

                Assert.That(button.RectTransform.sizeDelta, Is.EqualTo(new Vector2(560f, 160f)));
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void SetLabelSize_OverridesTheDefaultButtonLabelSize()
        {
            // title-screen.md's TitleButtonLabelSize (64 px) is not the
            // shared ButtonLabelSize (44 px) — the same "a screen may size
            // its own instance" carve-out CallerSuppliedSize_… asserts for
            // width/height, extended to the label.
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                button.SetLabelSize(64f);

                Assert.That(button.Label.fontSize, Is.EqualTo(64));
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void OutlineKinds_LeaveTheirCentreUnpainted_RatherThanDrawingASolidBlock()
        {
            // #323. Secondary and Destructive both set a fully transparent
            // fill, so whatever is painted in the middle of the button is
            // painted by the *border* image. While that border was a filled
            // rounded rect, the middle was the border colour: the button read
            // as a solid block, and Destructive's warning-red label sat
            // invisibly on a warning-red block.
            //
            // docs/specs/ui/shared-components.md#button's States table says
            // "no fill", and every mockup draws these two as `border:4px solid`
            // with nothing behind it.
            foreach (var kind in new[] { ButtonKind.Secondary, ButtonKind.Destructive })
            {
                var button = CreateButton(kind);

                try
                {
                    Assert.That(
                        CentrePaintedAlpha(button),
                        Is.EqualTo(0f).Within(0.001f),
                        $"{kind} is an outline kind — whatever is behind the button must show through its middle");
                }
                finally
                {
                    Destroy(button);
                }
            }

            // The other half, so the rule cannot be satisfied by painting
            // nothing anywhere: a filled kind still fills.
            var primary = CreateButton(ButtonKind.Primary);

            try
            {
                Assert.That(
                    CentrePaintedAlpha(primary),
                    Is.EqualTo(1f).Within(0.001f),
                    "Primary is 'filled accent' — its middle is painted");
            }
            finally
            {
                Destroy(primary);
            }
        }

        [Test]
        public void AButtonWidensToHoldItsLabel_RatherThanLettingItOverflow()
        {
            // #323. `Back to the game` is sixteen characters at
            // ButtonLabelSize, and ButtonMinWidth leaves only 224 px between
            // the two ButtonPaddingX. The label carries
            // HorizontalWrapMode.Overflow, so it did not wrap and did not
            // shrink — it hung out past the button's right-hand edge, which is
            // what the screenshot on #323 shows.
            //
            // Every committed mockup draws a button the way CSS does —
            // `min-width:320px` with `padding:0 48px` on a shrink-to-fit box —
            // so ButtonMinWidth is a floor and the label is what sets the
            // width when it needs more. settings-dialog.html's own comment
            // measures `Back to the game` at 531 px.
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                button.SetLabelText("Back to the game");

                var inner = button.RectTransform.sizeDelta.x - (Button.ButtonPaddingX * 2f);

                Assert.That(
                    button.Label.preferredWidth,
                    Is.LessThanOrEqualTo(inner),
                    "the label must fit between the two ButtonPaddingX rather than hang over the edge");
                Assert.That(
                    button.RectTransform.sizeDelta.x,
                    Is.GreaterThan(Button.ButtonMinWidth),
                    "and this is a label that does not fit at ButtonMinWidth");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void AShortLabel_LeavesTheButtonAtButtonMinWidth_BecauseItIsAFloorNotAFit()
        {
            // The other side of the widening rule: a button does not
            // shrink-wrap its words. `Roll` gets the same footprint every
            // other button has.
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                button.SetLabelText("Roll");

                Assert.That(
                    button.RectTransform.sizeDelta.x,
                    Is.EqualTo(Button.ButtonMinWidth).Within(0.001f));
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void Pressed_MovesTheVisualDownByButtonPressOffset_AndDarkensTheFill()
        {
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                var defaultFill = button.FillColor;
                var defaultBorder = button.BorderColor;

                button.OnPointerDown(EventDataAt(button, inside: true));

                Assert.That(button.IsPressed, Is.True);
                Assert.That(button.VisualRoot.anchoredPosition, Is.EqualTo(new Vector2(0f, -Button.ButtonPressOffset)));
                Assert.That(button.FillColor, Is.Not.EqualTo(defaultFill), "fill must darken while pressed");
                Assert.That(button.BorderColor, Is.Not.EqualTo(defaultBorder), "border must darken while pressed");

                button.OnPointerUp(EventDataAt(button, inside: true));

                Assert.That(button.IsPressed, Is.False);
                Assert.That(button.VisualRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(button.FillColor, Is.EqualTo(defaultFill), "fill must return to its default once released");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void Disabled_RendersAtButtonDisabledOpacity_AndSwallowsTheTapWithoutEmittingAnything()
        {
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                var clicks = 0;
                button.Clicked += () => clicks++;

                button.SetDisabled(true);

                Assert.That(button.CanvasGroup.alpha, Is.EqualTo(Button.ButtonDisabledOpacity).Within(0.0001f));

                button.OnPointerDown(EventDataAt(button, inside: true));
                button.OnPointerUp(EventDataAt(button, inside: true));

                Assert.That(button.IsPressed, Is.False, "a disabled button has no press response");
                Assert.That(clicks, Is.EqualTo(0), "a disabled button does nothing at all");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void Hidden_IsRemovedFromLayoutEntirely_NotMadeTransparent()
        {
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                Assert.That(button.IsHidden, Is.False);

                button.SetHidden(true);

                Assert.That(button.IsHidden, Is.True);
                Assert.That(button.gameObject.activeSelf, Is.False, "Hidden buttons do not leave gaps behind — they are removed from layout, not faded out");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void ActsOnRelease_NotOnPress()
        {
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                var clicks = 0;
                button.Clicked += () => clicks++;

                button.OnPointerDown(EventDataAt(button, inside: true));
                Assert.That(clicks, Is.EqualTo(0), "must not fire on press");

                button.OnPointerUp(EventDataAt(button, inside: true));
                Assert.That(clicks, Is.EqualTo(1), "must fire on release, over the button");
            }
            finally
            {
                Destroy(button);
            }
        }

        [Test]
        public void EmitsNothing_WhenReleaseLandsOutsideTheButton()
        {
            var button = CreateButton(ButtonKind.Primary);

            try
            {
                var clicks = 0;
                button.Clicked += () => clicks++;

                button.OnPointerDown(EventDataAt(button, inside: true));
                button.OnPointerUp(EventDataAt(button, inside: false));

                Assert.That(clicks, Is.EqualTo(0), "a finger that lands wrong can slide off and cancel");
            }
            finally
            {
                Destroy(button);
            }
        }

        // What the button paints at its own centre, as an alpha: the more
        // opaque of the two stacked images that meet there. This reads the
        // sprite's own texture rather than a rendered frame, because an
        // EditMode test has no frame to sample — the sprites are generated
        // readable, so the pixel that lands in the middle of the button can
        // simply be asked for.
        static float CentrePaintedAlpha(Button button)
        {
            var border = button.RectTransform.Find("Visual/Border").GetComponent<UnityImage>();
            var fill = button.RectTransform.Find("Visual/Fill").GetComponent<UnityImage>();

            return Mathf.Max(CentrePaintedAlpha(border), CentrePaintedAlpha(fill));
        }

        static float CentrePaintedAlpha(UnityImage image)
        {
            var texture = image.sprite.texture;

            // A sliced sprite stretches only its middle band, so the texture's
            // own centre pixel is the pixel that lands in the middle of the
            // button at whatever size it is drawn.
            return texture.GetPixel(texture.width / 2, texture.height / 2).a * image.color.a;
        }

        static Button CreateButton(ButtonKind kind)
        {
            var host = new GameObject(nameof(ButtonTests), typeof(RectTransform));
            var button = host.AddComponent<Button>();
            button.SetKind(kind);
            return button;
        }

        static void Destroy(Button button)
        {
            if (button != null)
            {
                Object.DestroyImmediate(button.gameObject);
            }
        }

        static PointerEventData EventDataAt(Button button, bool inside)
        {
            var corners = new Vector3[4];
            button.RectTransform.GetWorldCorners(corners);

            var center = (Vector2)(corners[0] + corners[2]) / 2f;
            var width = corners[2].x - corners[0].x;

            // Comfortably outside the rect in every case, regardless of the
            // caller-supplied size the test is exercising.
            var outside = center + new Vector2(Mathf.Abs(width) + Button.MinTouchTarget * 10f, 0f);

            return new PointerEventData(null)
            {
                position = inside ? center : outside
            };
        }
    }
}
