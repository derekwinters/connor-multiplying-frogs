using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity;
using Frogs.Unity.Views;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs and TitleScreenView.cs work around — so these two are
// pulled in by explicit alias rather than a wildcard `using Frogs.Unity.UI;`.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using LetteringEffect = Frogs.Unity.UI.LetteringEffect;
using UnityImage = UnityEngine.UI.Image;
using UnityText = UnityEngine.UI.Text;
using UnityGraphic = UnityEngine.UI.Graphic;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The title screen — docs/specs/ui/title-screen.md, as #484 settled it
    /// and #520 builds it: Connor's splash picture full-bleed, the drawn
    /// title as part of it, and a wooden sign reading `START` in the grass
    /// bottom-right. `RESUME` is still a real button behind the saved-game
    /// query, hidden whenever there is no save, which is always today.
    ///
    /// The older tests here asserted the text title, the `RESUME`/`NEW` row
    /// and the eight constants #484 removed. They were rewritten to the new
    /// spec rather than deleted — see #520's PR for which and why.
    /// </summary>
    public sealed class TitleScreenViewTests
    {
        // The one canvas every screen is measured in —
        // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1200f;

        const float Tolerance = 0.01f;

        // One 8-bit step either way: a colour sampled from a texture is read
        // back as bytes, and a lerp rounds.
        const float ColourTolerance = 1.5f / 255f;

        [Test]
        public void ResumeAndStart_AreTheOnlyInteractiveElementsAnywhereOnTheScreen()
        {
            var view = CreateView();

            try
            {
                // Touch the properties first: GetComponentsInChildren is a raw
                // Unity API call that does not funnel through
                // EnsureInitialized(), and Awake() is not guaranteed to have
                // run yet right after AddComponent.
                var resume = view.ResumeButton;
                var start = view.StartButton;

                var handlers = view.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                    .Where(c => c is IEventSystemHandler)
                    .ToArray();

                Assert.That(handlers, Is.EquivalentTo(new MonoBehaviour[] { resume, start }),
                    "docs/specs/ui/title-screen.md's first invariant: RESUME and START, never a third");

                // The art, the post, the label and the version text carry no
                // button of any kind.
                foreach (var picture in new Component[] { view.ArtImage, view.PostRect, start.Label, view.VersionText })
                {
                    Assert.That(picture.GetComponent<Button>(), Is.Null, picture.name);
                    Assert.That(picture.GetComponent<TitleSignButton>(), Is.Null, picture.name);
                    Assert.That(picture.GetComponent<UnityEngine.UI.Selectable>(), Is.Null, picture.name);
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePost_IsDrawingOnly_NothingInItIsARaycastTarget()
        {
            var view = CreateView();

            try
            {
                var graphics = view.PostRect.GetComponentsInChildren<UnityGraphic>(includeInactive: true);

                Assert.That(graphics, Is.Not.Empty, "the post draws something");
                foreach (var graphic in graphics)
                {
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void OnlyThePlankTakesTheTap_ItsLabelArtAndVersionAreNotRaycastTargets()
        {
            var view = CreateView();

            try
            {
                var start = view.StartButton;

                Assert.That(view.ArtImage.raycastTarget, Is.False);
                Assert.That(view.VersionText.raycastTarget, Is.False);
                Assert.That(start.Label.raycastTarget, Is.False);
                Assert.That(start.WoodImage.raycastTarget, Is.False);
                Assert.That(start.OutlineImage.raycastTarget, Is.False);

                // The one graphic that catches the tap is on the plank's own,
                // never-scaled rectangle — so the target is the plank, 320 x 128,
                // tilted with it, and does not shrink while it is held.
                Assert.That(start.TapTarget.raycastTarget, Is.True);
                Assert.That(start.TapTarget.rectTransform, Is.SameAs(start.RectTransform));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheSignsLabel_ReadsStart_InFredokaBold()
        {
            var view = CreateView();

            try
            {
                var label = view.StartButton.Label;

                Assert.That(label.text, Is.EqualTo("START"));
                Assert.That(label.font, Is.Not.Null, "Fredoka did not load — check " + TitleScreenView.SignFontResource);
                Assert.That(label.font, Is.SameAs(Resources.Load<Font>(TitleScreenView.SignFontResource)));
                Assert.That(AssetDatabase.GetAssetPath(label.font), Does.EndWith("/Fredoka-Bold.ttf"),
                    "the static weight-700 instance, not the variable font (which Unity would draw at its default, Light)");

                // Weight 700 is the font file's own; Unity's synthetic bold
                // would thicken it again.
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(label.fontSize, Is.EqualTo((int)TitleScreenView.TitleSignLabelSize));
                Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(label.color, Is.EqualTo(TitleScreenView.TitleSignLabelFill));

                var lettering = label.GetComponent<LetteringEffect>();
                Assert.That(lettering, Is.Not.Null, "the label's tracking and outline");
                Assert.That(lettering.Tracking, Is.EqualTo(TitleScreenView.TitleSignLabelTracking));
                Assert.That(lettering.OutlineWidth, Is.EqualTo(TitleScreenView.TitleSignLabelOutlineWidth));
                Assert.That(lettering.OutlineColor, Is.EqualTo(TitleScreenView.TitleSignOutline));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePlank_IsTitleSignWidthByHeight_TiltedAboutItsCentre_OnTheBottomRightSafeArea()
        {
            var view = CreateView();

            try
            {
                var plank = view.StartButton.RectTransform;

                Assert.That(plank.rect.size, Is.EqualTo(new Vector2(TitleScreenView.TitleSignWidth, TitleScreenView.TitleSignHeight)));
                Assert.That(plank.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), "tilted about its own centre");
                Assert.That(plank.localEulerAngles.z, Is.EqualTo(TitleScreenView.TitleSignTilt).Within(Tolerance),
                    "anticlockwise: Unity's positive z");

                // Measured in the reference canvas, whose origin is its centre.
                var centre = (Vector2)view.ContentRect.InverseTransformPoint(plank.position);

                var rightEdge = centre.x + (TitleScreenView.TitleSignWidth / 2f);
                var bottomEdge = centre.y - (TitleScreenView.TitleSignHeight / 2f);

                Assert.That(rightEdge, Is.EqualTo((ReferenceWidth / 2f) - TitleScreenView.SafeMargin).Within(Tolerance));
                Assert.That(
                    bottomEdge,
                    Is.EqualTo(-(ReferenceHeight / 2f) + TitleScreenView.SafeMargin + TitleScreenView.TitleSignBottomOffset).Within(Tolerance));

                // The spec page's worked numbers: 1552–1872 across, 930–1058 down.
                Assert.That(rightEdge + (ReferenceWidth / 2f), Is.EqualTo(1872f).Within(Tolerance));
                Assert.That((ReferenceHeight / 2f) - bottomEdge, Is.EqualTo(1058f).Within(Tolerance));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePlank_HasItsCornerRadiusAndOutline()
        {
            var view = CreateView();

            try
            {
                var start = view.StartButton;

                Assert.That(start.OutlineImage.color, Is.EqualTo(TitleScreenView.TitleSignOutline));
                Assert.That(start.OutlineImage.sprite, Is.Not.Null);
                Assert.That(start.OutlineImage.sprite.border.x, Is.EqualTo(TitleScreenView.TitleSignCornerRadius), "corner radius");
                Assert.That(start.OutlineImage.rectTransform.rect.size,
                    Is.EqualTo(new Vector2(TitleScreenView.TitleSignWidth, TitleScreenView.TitleSignHeight)));

                // The wood sits inside the outline.
                var inset = 2f * TitleScreenView.TitleSignOutlineWidth;
                Assert.That(start.WoodImage.rectTransform.rect.size,
                    Is.EqualTo(new Vector2(TitleScreenView.TitleSignWidth - inset, TitleScreenView.TitleSignHeight - inset)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheWood_RunsFromBarkToBarkShade_WithGrainLinesEveryGrainSpacing()
        {
            var view = CreateView();

            try
            {
                var texture = view.StartButton.WoodImage.sprite.texture;
                var width = texture.width;
                var height = texture.height;

                var inset = 2f * TitleScreenView.TitleSignOutlineWidth;
                Assert.That(width, Is.EqualTo((int)(TitleScreenView.TitleSignWidth - inset)));
                Assert.That(height, Is.EqualTo((int)(TitleScreenView.TitleSignHeight - inset)));

                // Rows counted from the top, as the mockup draws them; a
                // texture's row 0 is its bottom.
                Color RowFromTop(int row) => texture.GetPixel(width / 2, height - 1 - row);

                var spacing = (int)TitleScreenView.TitleSignGrainSpacing;
                var grain = (int)TitleScreenView.TitleSignGrainWidth;

                // Wood, not grain, at the very top and bottom.
                AssertColour(RowFromTop(0), Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, 0.5f / height));
                AssertColour(RowFromTop(height - 1), Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, (height - 0.5f) / height));

                // The first grain line: the last TitleSignGrainWidth rows of the
                // first TitleSignGrainSpacing, TitleSignGrain at 55% over the wood.
                var line = spacing - grain;
                var wood = Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, (line + 0.5f) / height);
                var grainColour = TitleScreenView.TitleSignGrain;
                var expected = Color.Lerp(wood, new Color(grainColour.r, grainColour.g, grainColour.b, 1f), grainColour.a);
                AssertColour(RowFromTop(line), expected);
                AssertColour(RowFromTop(spacing - 1), Color.Lerp(wood, new Color(grainColour.r, grainColour.g, grainColour.b, 1f), grainColour.a), 3f / 255f);

                // ... and plain wood either side of it.
                AssertColour(RowFromTop(line - 1), Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, (line - 0.5f) / height));
                AssertColour(RowFromTop(spacing), Color.Lerp(TitleScreenView.TitleSignBark, TitleScreenView.TitleSignBarkShade, (spacing + 0.5f) / height));

                // Its corners are rounded: the outline shows through.
                Assert.That(texture.GetPixel(0, 0).a, Is.LessThan(0.5f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePost_IsCentredUnderThePlank_AndEndsTitleSignPostDropBelowIt()
        {
            var view = CreateView();

            try
            {
                var plank = view.StartButton.RectTransform;
                var post = view.PostRect;

                var plankCentre = (Vector2)view.ContentRect.InverseTransformPoint(plank.position);
                var plankBottom = plankCentre.y - (TitleScreenView.TitleSignHeight / 2f);

                var corners = new Vector3[4];
                post.GetWorldCorners(corners);
                var bottomLeft = (Vector2)view.ContentRect.InverseTransformPoint(corners[0]);
                var topRight = (Vector2)view.ContentRect.InverseTransformPoint(corners[2]);

                Assert.That(topRight.x - bottomLeft.x, Is.EqualTo(TitleScreenView.TitleSignPostWidth).Within(Tolerance));
                Assert.That((bottomLeft.x + topRight.x) / 2f, Is.EqualTo(plankCentre.x).Within(Tolerance), "centred under the plank");
                Assert.That(bottomLeft.y, Is.EqualTo(plankBottom - TitleScreenView.TitleSignPostDrop).Within(Tolerance));

                // From behind the plank: its top is hidden under it, and it is
                // drawn first.
                Assert.That(topRight.y, Is.GreaterThan(plankBottom));
                Assert.That(post.GetSiblingIndex(), Is.LessThan(plank.GetSiblingIndex()));
                Assert.That(post.parent, Is.SameAs(plank.parent));

                var outline = post.GetComponent<UnityImage>();
                Assert.That(outline.color, Is.EqualTo(TitleScreenView.TitleSignOutline));
                Assert.That(outline.sprite.border.x, Is.EqualTo(TitleScreenView.TitleSignPostCornerRadius));

                var fill = view.PostFillImage;
                Assert.That(fill.color, Is.EqualTo(TitleScreenView.TitleSignBarkShade));
                Assert.That(fill.rectTransform.rect.width,
                    Is.EqualTo(TitleScreenView.TitleSignPostWidth - (2f * TitleScreenView.TitleSignPostOutlineWidth)).Within(Tolerance));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PressingThePlank_GoesToGameSetup_AndDoesNothingElse()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                view.Initialize(router);

                view.StartButton.OnPointerDown(EventDataAt(view.StartButton.RectTransform, inside: true));
                view.StartButton.OnPointerUp(EventDataAt(view.StartButton.RectTransform, inside: true));

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.GameSetup));
                Assert.That(router.CurrentDialog, Is.Null, "START carries no dialog, no roster, no game-start side effect");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ASlideOffThePlank_CancelsThePress()
        {
            // The shared Button's contract, which START keeps: it acts on
            // release, and only when the release lands on it.
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                view.Initialize(router);

                view.StartButton.OnPointerDown(EventDataAt(view.StartButton.RectTransform, inside: true));
                view.StartButton.OnPointerUp(EventDataAt(view.StartButton.RectTransform, inside: false));

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen));

                view.StartButton.OnPointerDown(EventDataAt(view.StartButton.RectTransform, inside: true));
                view.StartButton.OnPointerExit(EventDataAt(view.StartButton.RectTransform, inside: false));
                view.StartButton.OnPointerUp(EventDataAt(view.StartButton.RectTransform, inside: true));

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void WhileHeld_ThePlankShrinksToTitleSignPressScale_AndItsTapTargetDoesNot()
        {
            // docs/specs/ui/title-screen.md, open question 9, settled
            // 2026-11-26: "Press down" — the plank shrinks slightly while held.
            var view = CreateView();

            try
            {
                var start = view.StartButton;
                var plank = start.RectTransform;

                Assert.That(start.VisualRoot.localScale, Is.EqualTo(Vector3.one));

                start.OnPointerDown(EventDataAt(plank, inside: true));

                Assert.That(start.IsPressed, Is.True);
                Assert.That(start.VisualRoot.localScale,
                    Is.EqualTo(new Vector3(TitleScreenView.TitleSignPressScale, TitleScreenView.TitleSignPressScale, 1f)));
                Assert.That(start.VisualRoot.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), "about its own centre");
                Assert.That(plank.localScale, Is.EqualTo(Vector3.one), "the tap target keeps its size");
                Assert.That(view.PostRect.localScale, Is.EqualTo(Vector3.one), "the post does not move");

                // Only the size changes: no colour, no travel.
                Assert.That(start.OutlineImage.color, Is.EqualTo(TitleScreenView.TitleSignOutline));
                Assert.That(start.VisualRoot.anchoredPosition, Is.EqualTo(Vector2.zero));

                start.OnPointerUp(EventDataAt(plank, inside: true));

                Assert.That(start.IsPressed, Is.False);
                Assert.That(start.VisualRoot.localScale, Is.EqualTo(Vector3.one));

                start.OnPointerDown(EventDataAt(plank, inside: true));
                start.OnPointerExit(EventDataAt(plank, inside: false));

                Assert.That(start.VisualRoot.localScale, Is.EqualTo(Vector3.one), "sliding off lets it go");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Resume_IsHiddenWhenThereIsNoSavedGame()
        {
            // No save system exists anywhere yet, so the default
            // NoSavedGameQuery always answers no, and RESUME is not laid out.
            var view = CreateView();

            try
            {
                Assert.That(view.ResumeButton.IsHidden, Is.True);
                Assert.That(view.ResumeButton.Kind, Is.EqualTo(ButtonKind.Secondary));
                Assert.That(view.ResumeButton.Label.text, Is.EqualTo("RESUME"));
                Assert.That(view.StartButton.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Resume_IsShown_WhenTheQuerySaysThereIsASavedGame()
        {
            // Only that it is shown. Where it goes and how it looks then is
            // open question 8, for the save-system issue — this asserts no
            // position, because none is agreed.
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                view.Initialize(router, new AlwaysHasSavedGame());

                Assert.That(view.ResumeButton.IsHidden, Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheArt_IsTheCommittedSplashSprite_SizedToTheFullReferenceCanvas()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.ArtImage.sprite, Is.Not.Null, "the art did not load — check " + TitleScreenView.ArtResource);
                Assert.That(view.ArtImage.sprite, Is.SameAs(Resources.Load<Sprite>(TitleScreenView.ArtResource)));
                Assert.That(AssetDatabase.GetAssetPath(view.ArtImage.sprite),
                    Is.EqualTo("Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png"));
                Assert.That(view.ArtImage.color, Is.EqualTo(Color.white), "drawn as painted, not tinted");

                Assert.That(view.ArtRect.rect.size, Is.EqualTo(new Vector2(ReferenceWidth, ReferenceHeight)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheArt_ImportsAsAFullSizeSpriteWithNoMipmaps()
        {
            var path = "Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(2048));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        }

        [Test]
        public void TheFont_ImportsAsADynamicFontWithItsDataIncluded()
        {
            var path = "Assets/Fonts/Fredoka/Resources/Fredoka/Fredoka-Bold.ttf";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.fontTextureCase, Is.EqualTo(FontTextureCase.Dynamic));
            Assert.That(importer.includeFontData, Is.True, "a device has no Fredoka of its own to fall back on");
        }

        [Test]
        public void TheArt_IsDrawnBehindTheSignAndTheFootprint()
        {
            var view = CreateView();

            try
            {
                var backdrop = view.BackdropCanvasGroup.transform;
                var sign = view.StartButton.RectTransform;

                Assert.That(view.ArtRect.IsChildOf(backdrop), Is.True);
                Assert.That(sign.IsChildOf(view.ContentRect), Is.True);
                Assert.That(view.FootprintRect.IsChildOf(view.ContentRect), Is.True);
                Assert.That(backdrop.parent, Is.SameAs(view.ContentRect.parent));
                Assert.That(backdrop.GetSiblingIndex(), Is.LessThan(view.ContentRect.GetSiblingIndex()));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThereIsNoTitleTextElement_TheDrawnTitleInTheArtIsTheTitle()
        {
            var view = CreateView();

            try
            {
                // The START label, RESUME's label, and the version string —
                // read first, because GetComponentsInChildren does not build the
                // hierarchy and the properties do.
                var expected = new[] { view.StartButton.Label, view.ResumeButton.Label, view.VersionText };

                var texts = view.GetComponentsInChildren<UnityText>(includeInactive: true);

                Assert.That(texts, Is.EquivalentTo(expected));
                Assert.That(texts.Select(t => t.text), Has.None.Contains("Frogs"));
                Assert.That(typeof(TitleScreenView).GetProperty("TitleText"), Is.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheEightRemovedConstants_AreGone_AndEveryNewOneIsDeclaredUnderItsSpecName()
        {
            foreach (var removed in new[]
            {
                "TitleBaselineY", "TitleSize", "TitleButtonWidth", "TitleButtonHeight",
                "TitleButtonGap", "TitleButtonBottomOffset", "TitleButtonLabelSize", "TitleScrimOpacity"
            })
            {
                Assert.That(typeof(TitleScreenView).GetField(removed), Is.Null, removed);
            }

            // docs/specs/ui/title-screen.md#named-constants.
            Assert.That(TitleScreenView.SafeMargin, Is.EqualTo(48f));
            Assert.That(TitleScreenView.TitleSignWidth, Is.EqualTo(320f));
            Assert.That(TitleScreenView.TitleSignHeight, Is.EqualTo(128f));
            Assert.That(TitleScreenView.TitleSignBottomOffset, Is.EqualTo(94f));
            Assert.That(TitleScreenView.TitleSignTilt, Is.EqualTo(3f));
            Assert.That(TitleScreenView.TitleSignCornerRadius, Is.EqualTo(18f));
            Assert.That(TitleScreenView.TitleSignOutlineWidth, Is.EqualTo(7f));
            Assert.That(TitleScreenView.TitleSignGrainWidth, Is.EqualTo(4f));
            Assert.That(TitleScreenView.TitleSignGrainSpacing, Is.EqualTo(32f));
            Assert.That(TitleScreenView.TitleSignPostWidth, Is.EqualTo(32f));
            Assert.That(TitleScreenView.TitleSignPostDrop, Is.EqualTo(114f));
            Assert.That(TitleScreenView.TitleSignPostOutlineWidth, Is.EqualTo(6f));
            Assert.That(TitleScreenView.TitleSignPostCornerRadius, Is.EqualTo(8f));
            Assert.That(TitleScreenView.TitleSignLabelSize, Is.EqualTo(72f));
            Assert.That(TitleScreenView.TitleSignLabelOutlineWidth, Is.EqualTo(7f));
            Assert.That(TitleScreenView.TitleSignLabelTracking, Is.EqualTo(2f));
            Assert.That(TitleScreenView.TitleSignPressScale, Is.EqualTo(0.95f));
            Assert.That(TitleScreenView.VersionLabelSize, Is.EqualTo(28f));
            Assert.That(TitleScreenView.TitleFadeDuration, Is.EqualTo(0.3f));

            // The sign colours table.
            Assert.That((Color32)TitleScreenView.TitleSignBark, Is.EqualTo(new Color32(0x9C, 0x54, 0x0C, 0xFF)));
            Assert.That((Color32)TitleScreenView.TitleSignBarkShade, Is.EqualTo(new Color32(0x84, 0x3C, 0x0C, 0xFF)));
            Assert.That((Color32)TitleScreenView.TitleSignGrain, Is.EqualTo(new Color32(0x54, 0x24, 0x0C, 0x8C)), "#54240C at 55%");
            Assert.That((Color32)TitleScreenView.TitleSignOutline, Is.EqualTo(new Color32(0x06, 0x0B, 0x04, 0xFF)));
            Assert.That((Color32)TitleScreenView.TitleSignLabelFill, Is.EqualTo(new Color32(0xF8, 0xFA, 0xF6, 0xFF)));
        }

        [Test]
        public void VersionText_IsPinnedToTheBottomLeftSafeAreaCorner_AtVersionLabelSize()
        {
            var view = CreateView();

            try
            {
                var rect = view.VersionText.rectTransform;

                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.zero));
                Assert.That(
                    rect.anchoredPosition,
                    Is.EqualTo(new Vector2(TitleScreenView.SafeMargin, TitleScreenView.SafeMargin)));
                Assert.That(view.VersionText.fontSize, Is.EqualTo((int)TitleScreenView.VersionLabelSize));
                Assert.That(view.VersionText.text, Does.StartWith("v"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void FormatVersionLabel_ReadsTheVersionOutOfTheBuildName()
        {
            var expected = AppVersion.Parse("0.2.3").ToString();

            Assert.That(TitleScreenView.FormatVersionLabel("0.2.3-abc1234"), Is.EqualTo("v" + expected));
        }

        [Test]
        public void FormatVersionLabel_NeverThrows_OnAnUnreadableBuildStamp()
        {
            Assert.That(() => TitleScreenView.FormatVersionLabel("nightly"), Throws.Nothing);
            Assert.That(TitleScreenView.FormatVersionLabel("nightly"), Does.Contain("nightly"));
        }

        [Test]
        public void EnteringFade_RaisesTheArtFromZeroToOne_OverTitleFadeDuration_AndTheSignDoesNotAnimate()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(0f));

                view.AdvanceFade(TitleScreenView.TitleFadeDuration / 2f);
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(0.5f).Within(0.001f));

                view.AdvanceFade(TitleScreenView.TitleFadeDuration);
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(1f), "clamped, not overshooting past full duration");

                // "The sign does not animate": it is not under the fading group.
                Assert.That(view.StartButton.RectTransform.IsChildOf(view.BackdropCanvasGroup.transform), Is.False);
                Assert.That(view.PostRect.IsChildOf(view.BackdropCanvasGroup.transform), Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Initialize_RequiresARouter()
        {
            var view = CreateView();

            try
            {
                Assert.That(() => view.Initialize(null), Throws.ArgumentNullException);
            }
            finally
            {
                Destroy(view);
            }
        }

        static void AssertColour(Color actual, Color expected, float tolerance = ColourTolerance)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), $"red of {actual} vs {expected}");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), $"green of {actual} vs {expected}");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), $"blue of {actual} vs {expected}");
            Assert.That(actual.a, Is.EqualTo(1f).Within(tolerance), "the wood is opaque");
        }

        static TitleScreenView CreateView()
        {
            var host = new GameObject(nameof(TitleScreenViewTests), typeof(RectTransform));
            return host.AddComponent<TitleScreenView>();
        }

        static void Destroy(TitleScreenView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        static PointerEventData EventDataAt(RectTransform target, bool inside)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            var center = (Vector2)(corners[0] + corners[2]) / 2f;
            var width = corners[2].x - corners[0].x;

            var outside = center + new Vector2(Mathf.Abs(width) + Button.MinTouchTarget * 10f, 0f);

            return new PointerEventData(null)
            {
                position = inside ? center : outside
            };
        }

        sealed class AlwaysHasSavedGame : ISavedGameQuery
        {
            public bool HasSavedGame() => true;
        }
    }
}
