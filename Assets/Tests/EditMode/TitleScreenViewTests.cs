using System;
using NUnit.Framework;
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

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The title screen — issue #216, built against the CURRENT
    /// docs/specs/ui/title-screen.md (RESUME/NEW), not the stale
    /// single-Play wireframe #216's own issue body was originally written
    /// against. See this issue's PR for why.
    /// </summary>
    public sealed class TitleScreenViewTests
    {
        [Test]
        public void ResumeAndNew_AreTheOnlyInteractiveElementsAnywhereOnTheScreen()
        {
            var view = CreateView();

            try
            {
                // Touch the properties first: unlike every property on
                // TitleScreenView, GetComponentsInChildren is a raw Unity API
                // call that does not funnel through EnsureInitialized(), and
                // Awake() is not guaranteed to have run yet right after
                // AddComponent — the same reasoning TitleScreenView's own
                // EnsureInitialized comment gives. Without this, the search
                // below can run against an empty hierarchy.
                var resumeButton = view.ResumeButton;
                var newButton = view.NewButton;

                var buttons = view.GetComponentsInChildren<Button>(includeInactive: true);

                Assert.That(buttons, Is.EquivalentTo(new[] { resumeButton, newButton }));

                // art, title and footprint carry no Button component at all —
                // docs/specs/ui/title-screen.md's first invariant: "the only
                // interactive elements on this screen are RESUME and NEW."
                Assert.That(view.ArtRect.GetComponent<Button>(), Is.Null);
                Assert.That(view.TitleRect.GetComponent<Button>(), Is.Null);
                Assert.That(view.VersionText.GetComponent<Button>(), Is.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Art_IsAFlatColouredRectangle_SizedToTheFullReferenceCanvas()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.ArtImage, Is.Not.Null);

                // The 1920 x 1200 reference canvas —
                // docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in.
                Assert.That(view.ArtRect.rect.size, Is.EqualTo(new Vector2(1920f, 1200f)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Art_IsLaidOutBehindTitleActionAndFootprint_InDrawOrder()
        {
            var view = CreateView();

            try
            {
                var backdrop = view.ArtRect.parent;

                Assert.That(backdrop.GetSiblingIndex(), Is.LessThan(view.ActionRect.GetSiblingIndex()));
                Assert.That(view.ActionRect.GetSiblingIndex(), Is.LessThan(view.FootprintRect.GetSiblingIndex()));
                Assert.That(view.ArtRect.GetSiblingIndex(), Is.LessThan(view.TitleRect.GetSiblingIndex()));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Title_IsTextNotALogoImage_CentredHorizontally_WithItsBaselineAndSize()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.TitleText.text, Is.EqualTo("Multiplying Frogs"));
                Assert.That(
                    view.TitleText.GetComponent<UnityEngine.UI.Image>(),
                    Is.Null,
                    "title is a text element, not an Image/sprite component");

                Assert.That(view.TitleText.fontSize, Is.EqualTo((int)TitleScreenView.TitleSize));
                Assert.That(view.TitleRect.anchorMin.x, Is.EqualTo(0.5f));
                Assert.That(view.TitleRect.anchorMax.x, Is.EqualTo(0.5f));
                Assert.That(view.TitleRect.anchoredPosition.y, Is.EqualTo(-TitleScreenView.TitleBaselineY));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ResumeAndNew_HaveTheirSettledKindsAndLabels()
        {
            // Derek's call on issue #216: NEW is primary, RESUME is
            // secondary.
            var view = CreateView();

            try
            {
                Assert.That(view.NewButton.Kind, Is.EqualTo(ButtonKind.Primary));
                Assert.That(view.NewButton.Label.text, Is.EqualTo("NEW"));

                Assert.That(view.ResumeButton.Kind, Is.EqualTo(ButtonKind.Secondary));
                Assert.That(view.ResumeButton.Label.text, Is.EqualTo("RESUME"));

                foreach (var button in new[] { view.ResumeButton, view.NewButton })
                {
                    Assert.That(
                        button.RectTransform.sizeDelta,
                        Is.EqualTo(new Vector2(TitleScreenView.TitleButtonWidth, TitleScreenView.TitleButtonHeight)));
                    Assert.That(button.Label.fontSize, Is.EqualTo((int)TitleScreenView.TitleButtonLabelSize));
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Resume_IsHiddenWhenThereIsNoSavedGame_AndNewAloneIsCentred()
        {
            // No save/resume system exists anywhere in this v0.2 POC (epic
            // #198), so the default NoSavedGameQuery always answers no, and
            // RESUME reports itself hidden without anybody having to ask.
            var view = CreateView();

            try
            {
                Assert.That(view.ResumeButton.IsHidden, Is.True);
                Assert.That(view.NewButton.IsHidden, Is.False);

                // docs/specs/ui/title-screen.md's Anchors section: "the row
                // contains one button and centres it, which is exactly
                // where Play used to sit."
                Assert.That(view.NewButton.RectTransform.anchoredPosition.x, Is.EqualTo(0f));

                var expectedY = TitleScreenView.SafeMargin + TitleScreenView.TitleButtonBottomOffset;
                Assert.That(view.NewButton.RectTransform.anchoredPosition.y, Is.EqualTo(expectedY));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Resume_IsShownAndPositionedLeftOfNew_WhenASavedGameExists()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                view.Initialize(router, new AlwaysHasSavedGame());

                Assert.That(view.ResumeButton.IsHidden, Is.False);

                var resumeX = view.ResumeButton.RectTransform.anchoredPosition.x;
                var newX = view.NewButton.RectTransform.anchoredPosition.x;

                Assert.That(resumeX, Is.LessThan(newX), "RESUME sits on the left, NEW on the right");

                // The row is centred as a whole: the two button centres
                // straddle the canvas centre symmetrically.
                Assert.That(resumeX, Is.EqualTo(-newX).Within(0.001f));

                var gap = newX - resumeX - TitleScreenView.TitleButtonWidth;
                Assert.That(gap, Is.EqualTo(TitleScreenView.TitleButtonGap).Within(0.001f));

                var expectedY = TitleScreenView.SafeMargin + TitleScreenView.TitleButtonBottomOffset;
                Assert.That(view.ResumeButton.RectTransform.anchoredPosition.y, Is.EqualTo(expectedY));
                Assert.That(view.NewButton.RectTransform.anchoredPosition.y, Is.EqualTo(expectedY));
            }
            finally
            {
                Destroy(view);
            }
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

                // The content itself is FormatVersionLabel's job, tested
                // below against a build-name parameter rather than a live
                // Application.version — this only proves the label was
                // actually formatted, not left as placeholder text.
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
            // Mirrors HelloWorldProbeTests.TheProbeSaysSoRatherThanThrowingWhenTheBuildStampIsUnreadable:
            // a build (or a mid-test editor session, where PlayerSettings.bundleVersion
            // is only ever stamped at build time) that cannot read its own
            // version has a broken stamp, not a crash.
            Assert.That(() => TitleScreenView.FormatVersionLabel("nightly"), Throws.Nothing);
            Assert.That(TitleScreenView.FormatVersionLabel("nightly"), Does.Contain("nightly"));
        }

        [Test]
        public void EnteringFade_RaisesTheBackdropAlphaFromZeroToOne_OverTitleFadeDuration()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(0f));

                view.AdvanceFade(TitleScreenView.TitleFadeDuration / 2f);
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(0.5f).Within(0.001f));

                view.AdvanceFade(TitleScreenView.TitleFadeDuration);
                Assert.That(view.BackdropCanvasGroup.alpha, Is.EqualTo(1f), "clamped, not overshooting past full duration");

                // docs/specs/ui/title-screen.md#behaviour: "Neither button
                // animates." The shared Button's own CanvasGroup (which
                // carries ButtonDisabledOpacity) is untouched by the
                // screen's entering fade.
                Assert.That(view.NewButton.CanvasGroup.alpha, Is.EqualTo(1f));
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

        [Test]
        public void TappingNew_AsksTheRouterToNavigateToGameSetup_AndDoesNothingElse()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                view.Initialize(router);

                view.NewButton.OnPointerDown(EventDataAt(view.NewButton, inside: true));
                view.NewButton.OnPointerUp(EventDataAt(view.NewButton, inside: true));

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.GameSetup));
                Assert.That(router.CurrentDialog, Is.Null, "NEW carries no dialog, no roster, no game-start side effect");
            }
            finally
            {
                Destroy(view);
            }
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

        static PointerEventData EventDataAt(Button button, bool inside)
        {
            var corners = new Vector3[4];
            button.RectTransform.GetWorldCorners(corners);

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
