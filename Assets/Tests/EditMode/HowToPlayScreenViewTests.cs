using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Frogs.Core;
using Frogs.Unity.Views;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// SettingsDialogViewTests.cs and ButtonTests.cs work around — so these are
// pulled in by explicit alias, and a bare `Button`, `ButtonKind`,
// `BoardColours` or `FrogColours` in this file always means the shared
// component's.
using BoardColours = Frogs.Unity.UI.BoardColours;
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;
using FrogColours = Frogs.Unity.UI.FrogColours;
using ScreenColours = Frogs.Unity.UI.ScreenColours;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The five pages the settings dialog's `How to play` opens — issue #414,
    /// built against docs/specs/ui/how-to-play.md and its five committed 1:1
    /// mockups.
    ///
    /// Four things these tests hold down matter more than the layout:
    ///
    /// - **It is a screen, not a dialog.** It replaces the settings dialog
    ///   rather than covering it, because shared-components.md#dialog says a
    ///   dialog never opens over another dialog.
    /// - **Both buttons are real on every page.** Neither is ever hidden or
    ///   disabled: on page 1 `Back` leaves, on page 5 `Next` reads `Done` and
    ///   leaves. Leaving by either route is the same one event.
    /// - **Entering always starts on page 1.** Remembering where the last
    ///   reader got to is a mode arriving at the next player.
    /// - **The picture's width and height are what is left over.** They are
    ///   derived from everything anchored around them rather than typed in, so
    ///   moving the words column moves the picture instead of overlapping it.
    /// </summary>
    public sealed class HowToPlayScreenViewTests
    {
        const float CanvasWidth = 1920f;
        const float CanvasHeight = 1200f;

        // The five pages, in order, with the heading how-to-play.md's own
        // per-page table gives each one.
        static readonly string[] Headings =
        {
            "Your lane", "Roll the die", "Work it out", "Your frog hops", "Things people ask"
        };

        [Test]
        public void ThePicturesWidthAndHeight_AreWhatIsLeftOver_RatherThanTypedIn()
        {
            // how-to-play.md's "The two numbers that are derived, and the sums
            // that derive them". Asserted as the sums, not as 1104 and 808:
            // a change to the words column has to move the picture rather than
            // overlap it, and a literal cannot do that.
            Assert.That(
                HowToPlayScreenView.HowToPlayPictureWidth,
                Is.EqualTo(CanvasWidth
                    - (2f * HowToPlayScreenView.SafeMargin)
                    - HowToPlayScreenView.HowToPlayColumnGap
                    - HowToPlayScreenView.HowToPlayWordsWidth).Within(0.001f));

            Assert.That(
                HowToPlayScreenView.HowToPlayPictureHeight,
                Is.EqualTo(CanvasHeight
                    - HowToPlayScreenView.SafeMargin
                    - HowToPlayScreenView.HowToPlayHeadingLineBox
                    - HowToPlayScreenView.HowToPlayHeadingGap
                    - HowToPlayScreenView.HowToPlayControlsGap
                    - Button.ButtonHeight
                    - HowToPlayScreenView.SafeMargin).Within(0.001f));

            // And both sums land exactly on the page's own numbers, which is
            // the check that the layout has no slack hiding in it.
            Assert.That(HowToPlayScreenView.HowToPlayPictureWidth, Is.EqualTo(1104f).Within(0.001f));
            Assert.That(HowToPlayScreenView.HowToPlayPictureHeight, Is.EqualTo(808f).Within(0.001f));
        }

        [Test]
        public void TheLanePositionGap_IsDerivedFromThePicturesWidth_TheWayTheBoardsIsFromTheScreens()
        {
            // how-to-play.md: "(1104 − 2 × 56 − 2 × 100 − 7 × 64) ÷ 8 = 43".
            // Seven pads and eight gaps are Core's own nine-position lane, not
            // a second opinion about how long a lane is.
            Assert.That(
                HowToPlayScreenView.HowToPlayLanePositionGap,
                Is.EqualTo((HowToPlayScreenView.HowToPlayPictureWidth
                    - (2f * HowToPlayScreenView.HowToPlayPictureInset)
                    - (2f * HowToPlayScreenView.HowToPlayLogWidth)
                    - ((Lane.LanePositionCount - 2) * HowToPlayScreenView.HowToPlayPadDiameter))
                    / (Lane.LanePositionCount - 1)).Within(0.001f));

            Assert.That(HowToPlayScreenView.HowToPlayLanePositionGap, Is.EqualTo(43f).Within(0.001f));
        }

        [Test]
        public void ThePictureAndTheWords_AreOneRowBetweenTheHeadingAndTheControls()
        {
            var view = CreateView();

            try
            {
                var picture = view.PictureRect(1);
                var words = view.WordsRect(1);

                // picture — pinned to the left safe margin, its top the
                // heading's line box and gap below the safe area.
                Assert.That(LeftEdge(picture), Is.EqualTo(-(CanvasWidth / 2f) + HowToPlayScreenView.SafeMargin).Within(0.001f));
                Assert.That(
                    TopEdge(picture),
                    Is.EqualTo((CanvasHeight / 2f)
                        - HowToPlayScreenView.SafeMargin
                        - HowToPlayScreenView.HowToPlayHeadingLineBox
                        - HowToPlayScreenView.HowToPlayHeadingGap).Within(0.001f));

                Assert.That(Width(picture), Is.EqualTo(HowToPlayScreenView.HowToPlayPictureWidth).Within(0.001f));
                Assert.That(Height(picture), Is.EqualTo(HowToPlayScreenView.HowToPlayPictureHeight).Within(0.001f));

                // words — HowToPlayColumnGap to the picture's right, running
                // to the right safe margin, top-aligned with the picture.
                Assert.That(
                    LeftEdge(words) - RightEdge(picture),
                    Is.EqualTo(HowToPlayScreenView.HowToPlayColumnGap).Within(0.001f));

                Assert.That(RightEdge(words), Is.EqualTo((CanvasWidth / 2f) - HowToPlayScreenView.SafeMargin).Within(0.001f));
                Assert.That(Width(words), Is.EqualTo(HowToPlayScreenView.HowToPlayWordsWidth).Within(0.001f));
                Assert.That(TopEdge(words), Is.EqualTo(TopEdge(picture)).Within(0.001f), "top-aligned, not centred");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheHeadingAndTheControls_SitAgainstTheSafeArea_WithTheDotsBetweenTheTwoButtons()
        {
            var view = CreateView();

            try
            {
                Assert.That(
                    LeftEdge(view.HeadingText.rectTransform),
                    Is.EqualTo(-(CanvasWidth / 2f) + HowToPlayScreenView.SafeMargin).Within(0.001f));
                Assert.That(
                    TopEdge(view.HeadingText.rectTransform),
                    Is.EqualTo((CanvasHeight / 2f) - HowToPlayScreenView.SafeMargin).Within(0.001f));
                Assert.That(
                    Height(view.HeadingText.rectTransform),
                    Is.EqualTo(HowToPlayScreenView.HowToPlayHeadingLineBox).Within(0.001f));
                Assert.That(view.HeadingText.fontSize, Is.EqualTo((int)HowToPlayScreenView.HowToPlayHeadingSize));

                // `Back` at the left safe margin, `Next` at the right, both at
                // the shared ButtonHeight, their bottoms SafeMargin up.
                foreach (var button in new[] { view.BackButton, view.NextButton })
                {
                    Assert.That(
                        BottomEdge(button.RectTransform),
                        Is.EqualTo(-(CanvasHeight / 2f) + HowToPlayScreenView.SafeMargin).Within(0.001f));
                    Assert.That(Height(button.RectTransform), Is.EqualTo(Button.ButtonHeight).Within(0.001f));
                }

                Assert.That(
                    LeftEdge(view.BackButton.RectTransform),
                    Is.EqualTo(-(CanvasWidth / 2f) + HowToPlayScreenView.SafeMargin).Within(0.001f));
                Assert.That(
                    RightEdge(view.NextButton.RectTransform),
                    Is.EqualTo((CanvasWidth / 2f) - HowToPlayScreenView.SafeMargin).Within(0.001f));

                // The picture's bottom clears the controls row by
                // HowToPlayControlsGap — which is the other half of the
                // height sum.
                Assert.That(
                    BottomEdge(view.PictureRect(1)) - TopEdge(view.BackButton.RectTransform),
                    Is.EqualTo(HowToPlayScreenView.HowToPlayControlsGap).Within(0.001f));

                // progress — HowToPlayPageCount dots, centred across the whole
                // canvas and on the controls row, so they sit between the two
                // buttons rather than under them.
                Assert.That(view.ProgressDots.Count, Is.EqualTo(HowToPlayScreenView.HowToPlayPageCount));

                for (var dot = 0; dot < view.ProgressDots.Count; dot++)
                {
                    var rect = view.ProgressDots[dot].rectTransform;

                    Assert.That(Width(rect), Is.EqualTo(HowToPlayScreenView.HowToPlayDotSize).Within(0.001f));
                    Assert.That(Height(rect), Is.EqualTo(HowToPlayScreenView.HowToPlayDotSize).Within(0.001f));
                    Assert.That(
                        CentreY(rect),
                        Is.EqualTo(CentreY(view.BackButton.RectTransform)).Within(0.001f),
                        "the dots are centred on the controls row");

                    if (dot == 0)
                    {
                        continue;
                    }

                    Assert.That(
                        LeftEdge(rect) - RightEdge(view.ProgressDots[dot - 1].rectTransform),
                        Is.EqualTo(HowToPlayScreenView.HowToPlayDotGap).Within(0.001f));
                }

                var run = RightEdge(view.ProgressDots[HowToPlayScreenView.HowToPlayPageCount - 1].rectTransform)
                    + LeftEdge(view.ProgressDots[0].rectTransform);

                Assert.That(run, Is.EqualTo(0f).Within(0.001f), "the row of dots is centred on the canvas");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EnteringAlwaysStartsOnPageOne_NeverWhereTheLastReaderLeftIt()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.CurrentPage, Is.EqualTo(1), "a freshly built screen is on page 1");

                Tap(view.NextButton);
                Tap(view.NextButton);
                Assert.That(view.CurrentPage, Is.EqualTo(3));

                // Left, and opened again — how-to-play.md#behaviour: "It
                // always opens on page 1. It does not remember where you got
                // to, because remembering is a mode that arrives at the next
                // player in whatever state the last one left it."
                view.Open();

                Assert.That(view.CurrentPage, Is.EqualTo(1));
                Assert.That(view.HeadingText.text, Is.EqualTo(Headings[0]));
                Assert.That(view.NextButton.Label.text, Is.EqualTo("Next"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BackOnPageOne_LeavesTheScreen_RatherThanDoingNothing()
        {
            var view = CreateView();

            try
            {
                var left = 0;
                view.LeaveRequested += () => left++;

                Assert.That(view.CurrentPage, Is.EqualTo(1));

                Tap(view.BackButton);

                Assert.That(left, Is.EqualTo(1), "on the first page `Back` leaves the screen");
                Assert.That(view.CurrentPage, Is.EqualTo(1), "and does not page anywhere on the way out");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void NextOnTheLastPage_ReadsDone_AndLeavesByTheSameRouteBackDoes()
        {
            var view = CreateView();

            try
            {
                var left = 0;
                view.LeaveRequested += () => left++;

                for (var page = 1; page < HowToPlayScreenView.HowToPlayPageCount; page++)
                {
                    Assert.That(view.NextButton.Label.text, Is.EqualTo("Next"), $"page {page}");
                    Tap(view.NextButton);
                }

                Assert.That(view.CurrentPage, Is.EqualTo(HowToPlayScreenView.HowToPlayPageCount));
                Assert.That(view.NextButton.Label.text, Is.EqualTo("Done"));
                Assert.That(left, Is.Zero, "paging is not leaving");

                Tap(view.NextButton);

                Assert.That(left, Is.EqualTo(1), "`Done` leaves");

                // One rule for both buttons: the same event, raised the same
                // number of times, whichever way the reader goes out.
                view.Open();
                Tap(view.BackButton);

                Assert.That(left, Is.EqualTo(2));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BackAndNext_AreThereAndEnabledOnEveryPage_AndNeitherIsEverHidden()
        {
            var view = CreateView();

            try
            {
                for (var page = 1; page <= HowToPlayScreenView.HowToPlayPageCount; page++)
                {
                    Assert.That(view.CurrentPage, Is.EqualTo(page));

                    foreach (var button in new[] { view.BackButton, view.NextButton })
                    {
                        Assert.That(button.IsHidden, Is.False, $"page {page}: never hidden");
                        Assert.That(button.IsDisabled, Is.False, $"page {page}: never disabled");
                        Assert.That(button.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f), $"page {page}");
                    }

                    // "exactly one primary button is visible at a time" —
                    // `Next` is it, on every page, including the one where it
                    // reads `Done`.
                    Assert.That(view.NextButton.Kind, Is.EqualTo(ButtonKind.Primary));
                    Assert.That(view.BackButton.Kind, Is.EqualTo(ButtonKind.Secondary));
                    Assert.That(view.BackButton.Label.text, Is.EqualTo("Back"));

                    if (page < HowToPlayScreenView.HowToPlayPageCount)
                    {
                        Tap(view.NextButton);
                    }
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PagingForwardAndBack_WalksTheFivePages_AndLightsTheDotItIsOn()
        {
            var view = CreateView();

            try
            {
                for (var page = 1; page <= HowToPlayScreenView.HowToPlayPageCount; page++)
                {
                    Assert.That(view.CurrentPage, Is.EqualTo(page));
                    Assert.That(view.HeadingText.text, Is.EqualTo(Headings[page - 1]));

                    for (var dot = 0; dot < view.ProgressDots.Count; dot++)
                    {
                        var isCurrent = dot == page - 1;

                        // The current page's dot is the ink the heading is
                        // written in; the rest are faint.
                        Assert.That(
                            view.ProgressDots[dot].color,
                            isCurrent
                                ? Is.EqualTo(view.HeadingText.color)
                                : Is.Not.EqualTo(view.HeadingText.color),
                            $"page {page}, dot {dot}");
                    }

                    if (page < HowToPlayScreenView.HowToPlayPageCount)
                    {
                        Tap(view.NextButton);
                    }
                }

                for (var page = HowToPlayScreenView.HowToPlayPageCount; page > 1; page--)
                {
                    Assert.That(view.CurrentPage, Is.EqualTo(page));
                    Tap(view.BackButton);
                }

                Assert.That(view.CurrentPage, Is.EqualTo(1));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_DoesWhatBackDoes_AndNeverJumpsOutOfTheMiddle()
        {
            var view = CreateView();

            try
            {
                var left = 0;
                view.LeaveRequested += () => left++;

                Tap(view.NextButton);
                Tap(view.NextButton);
                Assert.That(view.CurrentPage, Is.EqualTo(3));

                view.HandleHardwareBack();

                Assert.That(view.CurrentPage, Is.EqualTo(2), "one page back, not out");
                Assert.That(left, Is.Zero);

                view.HandleHardwareBack();
                Assert.That(view.CurrentPage, Is.EqualTo(1));
                Assert.That(left, Is.Zero);

                view.HandleHardwareBack();
                Assert.That(left, Is.EqualTo(1), "and from page 1 it leaves");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ItIsAScreenNotADialog_SoItPaintsNoScrimAndStacksOverNothing()
        {
            var view = CreateView();

            try
            {
                // shared-components.md#dialog: "Stacked... does not happen. A
                // dialog never opens over another dialog." This is opened from
                // inside the settings dialog, so it cannot be one — and the
                // structural proof is that it holds no DialogPanel at all,
                // which is what would draw the scrim over what is underneath.
                Assert.That(
                    view.GetComponentsInChildren<DialogPanel>(true),
                    Is.Empty,
                    "a screen has no dialog panel and no scrim");

                // Its root fills the whole canvas and paints the app's own
                // background to every edge, rather than letting the board show
                // through around it.
                var rect = view.RectTransform;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));

                Assert.That(view.Background.color, Is.EqualTo(ScreenColours.Background));
                Assert.That(Width(view.Background.rectTransform), Is.EqualTo(CanvasWidth).Within(0.001f));
                Assert.That(Height(view.Background.rectTransform), Is.EqualTo(CanvasHeight).Within(0.001f));

                // And the router's own entry for it is a Screen, not a
                // Dialog — the same statement, where the navigation graph can
                // see it.
                Assert.That(Enum.GetNames(typeof(CoreScreen)), Contains.Item("HowToPlay"));
                Assert.That(Enum.GetNames(typeof(Dialog)), Has.No.Member("HowToPlay"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void NothingOnThisScreenScrolls_AndNothingHereCanReachTheGame()
        {
            var view = CreateView();

            try
            {
                // how-to-play.md#invariants: "nothing on this screen scrolls.
                // It is HowToPlayPageCount pages reached with Back and Next,
                // and a page that does not fit is a page whose copy is too
                // long, not a reason to add a scroll view."
                var scrollers = view
                    .GetComponentsInChildren<Component>(true)
                    .Select(component => component.GetType().Name)
                    .Where(name => name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

                Assert.That(scrollers, Is.Empty, "no scroll view");

                // "this screen changes nothing about the game" — structural,
                // not a promise: no member of this type takes or returns a
                // Game, a Lane or a ScreenRouter, so there is nothing here it
                // could call.
                var reaching = typeof(HowToPlayScreenView)
                    .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OfType<MethodBase>()
                    .SelectMany(method => method.GetParameters())
                    .Select(parameter => parameter.ParameterType)
                    .Where(type => type == typeof(Game) || type == typeof(Lane) || type == typeof(ScreenRouter))
                    .ToArray();

                Assert.That(reaching, Is.Empty, "the screen is never handed the game it is describing");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PagingCrossFadesThePictureAndTheWords_AndLeavesTheFurnitureAlone()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.PageGroup(1).alpha, Is.EqualTo(1f).Within(0.001f));

                Tap(view.NextButton);

                // Both halves of the cross-fade are in flight: the page that
                // is going is not simply switched off.
                Assert.That(view.PageGroup(1).alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(view.PageGroup(2).alpha, Is.EqualTo(0f).Within(0.001f));

                view.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);

                Assert.That(view.PageGroup(1).alpha, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(view.PageGroup(2).alpha, Is.EqualTo(0.5f).Within(0.01f));

                view.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);

                Assert.That(view.PageGroup(1).alpha, Is.EqualTo(0f).Within(0.001f));
                Assert.That(view.PageGroup(2).alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(view.PageRect(1).gameObject.activeSelf, Is.False, "the page that is gone is not left drawing");

                // "heading, progress and controls do not animate at all — they
                // are the furniture that says where you are, and furniture that
                // moves is furniture a child chases."
                var headingBefore = TopEdge(view.HeadingText.rectTransform);
                var backBefore = LeftEdge(view.BackButton.RectTransform);

                Tap(view.NextButton);
                view.AdvanceFade(DialogPanel.DialogFadeDuration / 2f);

                Assert.That(TopEdge(view.HeadingText.rectTransform), Is.EqualTo(headingBefore).Within(0.001f));
                Assert.That(LeftEdge(view.BackButton.RectTransform), Is.EqualTo(backBefore).Within(0.001f));
                Assert.That(view.HeadingText.text, Is.EqualTo(Headings[2]), "the heading changes at once, it does not fade");
            }
            finally
            {
                Destroy(view);
            }
        }

        [TestCase(1, 4)]
        [TestCase(4, 3)]
        [TestCase(5, 4)]
        public void ThePondPages_DrawRealLanes_WithTheirOwnPairOfLogsRatherThanTheBoardsSharedOnes(int page, int laneCount)
        {
            var view = CreateView();

            try
            {
                var picture = view.PictureRect(page);

                Assert.That(view.PictureSurface(page).color, Is.EqualTo(BoardColours.PondWater), "drawn on the pond's own water");

                var lanes = DescendantsNamed(picture, "Lane");
                Assert.That(lanes.Length, Is.EqualTo(laneCount));

                foreach (var lane in lanes)
                {
                    Assert.That(
                        ChildrenNamed(lane, "LilyPad").Length,
                        Is.EqualTo(Lane.LanePositionCount - 2),
                        "seven lily pads, which is Core's own lane less its two logs");

                    // The one place the drawing is deliberately not the board:
                    // "the logs are per-lane here, not shared." A picture of a
                    // single lane needs the log at the end of *that* lane.
                    Assert.That(ChildrenNamed(lane, "Log").Length, Is.EqualTo(2));
                }

                var logs = DescendantsNamed(picture, "Log");
                Assert.That(
                    logs.Length,
                    Is.EqualTo(2 * laneCount),
                    "one pair per lane, not one pair for the whole picture");

                foreach (var log in logs)
                {
                    Assert.That(log.GetComponent<Image>().color, Is.EqualTo(BoardColours.LogBrown));
                    Assert.That(Width(log), Is.EqualTo(HowToPlayScreenView.HowToPlayLogWidth).Within(0.001f));
                    Assert.That(Height(log), Is.EqualTo(HowToPlayScreenView.HowToPlayLogHeight).Within(0.001f));
                }

                // A lane's row is the pond's own arithmetic at the picture's
                // size: two logs, seven pads and eight gaps, inset on both
                // sides of the picture.
                Assert.That(
                    Width(lanes[0]),
                    Is.EqualTo(HowToPlayScreenView.HowToPlayPictureWidth - (2f * HowToPlayScreenView.HowToPlayPictureInset))
                        .Within(0.001f));

                Assert.That(
                    LeftEdge(lanes[0]) - LeftEdge(picture),
                    Is.EqualTo(HowToPlayScreenView.HowToPlayPictureInset).Within(0.001f));

                // The stack sits where what is left of the picture's height
                // puts it, which is where all five mockups draw it — 92 px
                // down for a four-lane page — rather than at a top of its own.
                var stack = lanes[0].parent as RectTransform;

                Assert.That(
                    TopEdge(picture) - TopEdge(stack),
                    Is.EqualTo(HowToPlayScreenView.LaneStackTop(Height(stack))).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PageOne_PutsEveryFrogOnItsStartLog_InItsOwnRealColour()
        {
            var view = CreateView();

            try
            {
                var frogs = DescendantsNamed(view.PictureRect(1), "Frog");

                Assert.That(frogs.Length, Is.EqualTo(4), "a game about to begin, all four frogs on their Start logs");

                var painted = frogs
                    .Select(frog => ChildNamed(frog, "Fill").GetComponent<Image>().color)
                    .ToArray();

                Assert.That(
                    painted,
                    Is.EquivalentTo(new[]
                    {
                        FrogColours.FrogGreen, FrogColours.FrogBlue, FrogColours.FrogOrange, FrogColours.FrogPink
                    }),
                    "the four real frog colours, not four placeholders of this screen's own");

                foreach (var frog in frogs)
                {
                    Assert.That(Width(frog), Is.EqualTo(HowToPlayScreenView.HowToPlayFrogDiameter).Within(0.001f));
                    Assert.That(frog.parent.gameObject.name, Is.EqualTo("Log"), "standing on a log, not on the water");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ThePaperPages_AreDrawnOnPaper_AndPageThreeTeachesNoAlgorithm()
        {
            var view = CreateView();

            try
            {
                foreach (var page in new[] { 2, 3 })
                {
                    Assert.That(view.PictureSurface(page).color, Is.EqualTo(Color.white), $"page {page} is paper, not pond");
                    Assert.That(DescendantsNamed(view.PictureRect(page), "Lane"), Is.Empty);
                }

                // page 3's picture is the grid: four columns, a carry row, two
                // rows to work in and the answer row. "Nothing here teaches an
                // algorithm" — it says where the answer goes, and it marks
                // nothing else, which is ADR-0002's constraint.
                var cells = DescendantsNamed(view.PictureRect(3), "Cell");
                Assert.That(cells.Length, Is.GreaterThan(0));

                var answerCells = DescendantsNamed(view.PictureRect(3), "AnswerCell");
                Assert.That(answerCells.Length, Is.EqualTo(4), "one answer row, four columns wide");

                // page 2's picture is the die, the arrow and the three piles.
                Assert.That(DescendantsNamed(view.PictureRect(2), "Pile").Length, Is.EqualTo(3));
                Assert.That(DescendantsNamed(view.PictureRect(2), "Pip").Length, Is.EqualTo(3), "the die shows 3");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryPagesWords_StackFromTheTopOfTheColumn_AParagraphGapApart()
        {
            var view = CreateView();

            try
            {
                for (var page = 1; page <= HowToPlayScreenView.HowToPlayPageCount; page++)
                {
                    var paragraphs = view.Paragraphs(page);

                    Assert.That(paragraphs.Count, Is.GreaterThan(0), $"page {page} says something");

                    Assert.That(
                        TopEdge(paragraphs[0].rectTransform),
                        Is.EqualTo(TopEdge(view.WordsRect(page))).Within(0.001f),
                        $"page {page} starts at the top of the column");

                    foreach (var paragraph in paragraphs)
                    {
                        Assert.That(paragraph.fontSize, Is.EqualTo((int)HowToPlayScreenView.HowToPlayBodySize), $"page {page}");
                        Assert.That(
                            Width(paragraph.rectTransform),
                            Is.EqualTo(HowToPlayScreenView.HowToPlayWordsWidth).Within(0.001f),
                            $"page {page}");
                    }
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryGeometryValue_IsANamedConstantFromHowToPlaysTables()
        {
            var constants = new Dictionary<string, float>
            {
                // The safe margin every screen keeps, declared here as the
                // board, the title screen and game setup each declare it.
                { "SafeMargin", 48f },

                // how-to-play.md's Named constants table.
                { "HowToPlayHeadingSize", 72f },
                { "HowToPlayHeadingLineBox", 88f },
                { "HowToPlayHeadingGap", 48f },
                { "HowToPlayPictureWidth", 1104f },
                { "HowToPlayPictureHeight", 808f },
                { "HowToPlayPictureInset", 56f },
                { "HowToPlayColumnGap", 64f },
                { "HowToPlayWordsWidth", 656f },
                { "HowToPlayBodySize", 40f },
                { "HowToPlayBodyLineHeight", 1.35f },
                { "HowToPlayParagraphGap", 32f },
                { "HowToPlayControlsGap", 48f },
                { "HowToPlayDotSize", 20f },
                { "HowToPlayDotGap", 24f },
                { "HowToPlayPageCount", 5f },

                // "The pond, drawn smaller" — a second set of numbers for the
                // same shapes, at a size that fits the picture.
                { "HowToPlayPadDiameter", 64f },
                { "HowToPlayLogWidth", 100f },
                { "HowToPlayLogHeight", 120f },
                { "HowToPlayFrogDiameter", 50f },
                { "HowToPlayLanePositionGap", 43f },
                { "HowToPlayLaneGap", 48f },
                { "HowToPlayLogLabelSize", 20f },

                // "What the pictures are drawn from" — the five mockups' own
                // numbers, transcribed onto the page by this issue. No value
                // here is new: every one is a rule in a committed mockup.
                { "HowToPlayLogRadius", 20f },
                { "HowToPlayLogLabelTopPadding", 16f },
                { "HowToPlayLogLabelGap", 12f },
                { "HowToPlayFrogOutline", 3f },
                { "HowToPlayPaperOutline", 3f },
                { "HowToPlayNoteSize", 34f },
                { "HowToPlayNoteLineHeight", 1.4f },
                { "HowToPlayCaptionLineBox", 52f },
                { "HowToPlayExampleGap", 56f },
                { "HowToPlayDieLeft", 96f },
                { "HowToPlayDieTop", 164f },
                { "HowToPlayDieSize", 200f },
                { "HowToPlayDieRadius", 32f },
                { "HowToPlayDieOutline", 4f },
                { "HowToPlayDiePadding", 28f },
                { "HowToPlayDiePipSize", 34f },
                { "HowToPlayArrowLeft", 336f },
                { "HowToPlayArrowSize", 56f },
                { "HowToPlayPileLeft", 452f },
                { "HowToPlayPileWidth", 200f },
                { "HowToPlayPileHeight", 120f },
                { "HowToPlayPileRadius", 16f },
                { "HowToPlayPileOutline", 4f },
                { "HowToPlayPileGap", 24f },
                { "HowToPlayPileLabelSize", 36f },
                { "HowToPlayPileDimOpacity", 0.4f },
                { "HowToPlayRollTableLeft", 96f },
                { "HowToPlayRollTableTop", 520f },
                { "HowToPlayRollTableColumnWidth", 220f },
                { "HowToPlayRollTableColumnGap", 32f },
                { "HowToPlayRollTableHeaderGap", 20f },
                { "HowToPlayGridLeft", 120f },
                { "HowToPlayGridTop", 96f },
                { "HowToPlayCellSize", 88f },
                { "HowToPlayCellGap", 8f },
                { "HowToPlayCellRadius", 8f },
                { "HowToPlayCellOutline", 3f },
                { "HowToPlayCellDigitSize", 48f },
                { "HowToPlayCarryRowHeight", 56f },
                { "HowToPlayCarryBoxWidth", 48f },
                { "HowToPlayCarryBoxHeight", 44f },
                { "HowToPlayCarryBoxRadius", 6f },
                { "HowToPlayAnswerOutline", 4f },
                { "HowToPlayCalloutLeft", 560f },
                { "HowToPlayCalloutTop", 208f },
                { "HowToPlayCalloutWidth", 440f },
                { "HowToPlayCalloutGap", 40f },
                { "HowToPlayCalloutLineHeight", 1.5f }
            };

            var declared = typeof(HowToPlayScreenView)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                declared.Select(field => field.Name).OrderBy(name => name),
                Is.EqualTo(constants.Keys.OrderBy(name => name)),
                "this screen's public constants are exactly how-to-play.md's own, under the identical names");

            foreach (var field in declared)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(constants[field.Name]).Within(0.001f),
                    field.Name);
            }

            // `LanePositionCount` is Core's, reused here under the identical
            // name rather than redeclared — the same line the board draws.
            Assert.That(
                typeof(HowToPlayScreenView).GetField(
                    "LanePositionCount", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                Is.Null,
                "the picture's lanes are Core's nine positions, not a second opinion about how long a lane is");

            // The picture's corner is the shared Dialog's radius, and its
            // buttons are the shared Button — both referenced, not restated.
            Assert.That(
                typeof(HowToPlayScreenView).GetField(
                    "HowToPlayPictureRadius", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                Is.Null,
                "the picture's corner is DialogRadius");
        }

        // --- Helpers -------------------------------------------------------------

        // The screen under a canvas that is exactly the reference size — the
        // shape all five mockups are drawn at, and the shape the target tablet
        // is.
        static HowToPlayScreenView CreateView()
        {
            var canvas = new GameObject(nameof(HowToPlayScreenViewTests), typeof(RectTransform));
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            canvasRect.anchoredPosition = Vector2.zero;

            var view = new GameObject(nameof(HowToPlayScreenView), typeof(RectTransform))
                .AddComponent<HowToPlayScreenView>();

            view.transform.SetParent(canvasRect, worldPositionStays: false);

            // Reading the view's own rect is what makes it build — the same
            // published way AppRoot primes every other screen.
            var built = view.RectTransform;
            Assert.That(built, Is.Not.Null);

            return view;
        }

        static void Destroy(HowToPlayScreenView view)
        {
            if (view == null)
            {
                return;
            }

            var canvas = view.transform.parent;

            UnityEngine.Object.DestroyImmediate(canvas != null ? canvas.gameObject : view.gameObject);
        }

        static void Tap(Button button)
        {
            var corners = new Vector3[4];
            button.RectTransform.GetWorldCorners(corners);

            var eventData = new PointerEventData(null)
            {
                position = (Vector2)(corners[0] + corners[2]) / 2f
            };

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static RectTransform[] ChildrenNamed(RectTransform parent, string name)
        {
            var found = new List<RectTransform>();

            for (var child = 0; child < parent.childCount; child++)
            {
                var rect = parent.GetChild(child) as RectTransform;

                if (rect != null && rect.gameObject.name == name)
                {
                    found.Add(rect);
                }
            }

            return found.ToArray();
        }

        static RectTransform ChildNamed(RectTransform parent, string name)
        {
            var found = ChildrenNamed(parent, name);

            Assert.That(found.Length, Is.EqualTo(1), $"{parent.gameObject.name} holds one {name}");

            return found[0];
        }

        static RectTransform[] DescendantsNamed(RectTransform parent, string name)
        {
            return parent
                .GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.gameObject.name == name)
                .ToArray();
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static float LeftEdge(RectTransform rect) => Corners(rect)[0].x;

        static float RightEdge(RectTransform rect) => Corners(rect)[2].x;

        static float TopEdge(RectTransform rect) => Corners(rect)[1].y;

        static float BottomEdge(RectTransform rect) => Corners(rect)[0].y;

        static float Width(RectTransform rect) => RightEdge(rect) - LeftEdge(rect);

        static float Height(RectTransform rect) => TopEdge(rect) - BottomEdge(rect);

        static float CentreY(RectTransform rect) => (TopEdge(rect) + BottomEdge(rect)) / 2f;
    }
}
