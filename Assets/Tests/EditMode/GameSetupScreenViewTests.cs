using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity.Views;
using CoreScreen = Frogs.Core.Screen;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and TitleScreenViewTests.cs work
// around — so this is pulled in by explicit alias, and a bare `Button` in
// this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The game setup screen — issue #217, built directly against
    /// docs/specs/ui/game-setup.md's own "Frog seat" element and its own
    /// named constants, independent of the shared Player chip (#219, not
    /// built yet) — see this issue's PR for the doc contradiction this
    /// deliberately does not resolve.
    /// </summary>
    public sealed class GameSetupScreenViewTests
    {
        [Test]
        public void FreshView_HasFourEmptySeats_StartDisabled_AndTheDisabledHint()
        {
            var view = CreateView();

            try
            {
                foreach (var colour in AllColours)
                {
                    Assert.That(view.IsSeatChosen(colour), Is.False, $"{colour} must start empty");
                    Assert.That(view.SeatBadgeNumber(colour), Is.Null);
                    Assert.That(view.SeatLabel(colour).text, Is.EqualTo("Tap to play"));
                    Assert.That(view.SeatBadgeRoot(colour).activeSelf, Is.False);
                }

                Assert.That(view.StartButton.IsDisabled, Is.True);
                Assert.That(view.HintText.text, Is.EqualTo("Pick two to four frogs"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void AllFourSeats_AreAlwaysPresent_InFixedGreenBlueOrangePinkOrder()
        {
            var view = CreateView();

            try
            {
                var greenX = view.SeatRect(FrogColour.Green).anchoredPosition.x;
                var blueX = view.SeatRect(FrogColour.Blue).anchoredPosition.x;
                var orangeX = view.SeatRect(FrogColour.Orange).anchoredPosition.x;
                var pinkX = view.SeatRect(FrogColour.Pink).anchoredPosition.x;

                Assert.That(greenX, Is.LessThan(blueX));
                Assert.That(blueX, Is.LessThan(orangeX));
                Assert.That(orangeX, Is.LessThan(pinkX));

                // Choosing three of the four still leaves the fourth laid
                // out — an unchosen frog is an empty seat, never a missing
                // one. docs/specs/ui/game-setup.md's Anchors section.
                Tap(view, FrogColour.Green);
                Tap(view, FrogColour.Blue);
                Tap(view, FrogColour.Orange);

                Assert.That(view.SeatRect(FrogColour.Pink).gameObject.activeInHierarchy, Is.True);
                Assert.That(view.IsSeatChosen(FrogColour.Pink), Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TappingAnEmptySeat_AddsIt_WithTheNextFreeBadge_RegardlessOfSeatPosition()
        {
            var view = CreateView();

            try
            {
                // Tapping the third seat (Orange) first must still yield
                // badge 1, not 3 — turn order is tap order, not seat
                // position.
                Tap(view, FrogColour.Orange);

                Assert.That(view.IsSeatChosen(FrogColour.Orange), Is.True);
                Assert.That(view.SeatBadgeNumber(FrogColour.Orange), Is.EqualTo(1));
                Assert.That(view.SeatBadgeText(FrogColour.Orange).text, Is.EqualTo("1"));
                Assert.That(view.SeatBadgeRoot(FrogColour.Orange).activeSelf, Is.True);
                Assert.That(view.SeatLabel(FrogColour.Orange).text, Is.EqualTo("Orange"));

                Tap(view, FrogColour.Green);
                Assert.That(view.SeatBadgeNumber(FrogColour.Green), Is.EqualTo(2));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TappingAChosenSeat_RemovesIt_AndRenumbersLaterBadgesDownImmediately()
        {
            var view = CreateView();

            try
            {
                Tap(view, FrogColour.Green); // badge 1
                Tap(view, FrogColour.Blue); // badge 2
                Tap(view, FrogColour.Orange); // badge 3

                Tap(view, FrogColour.Blue); // remove — no confirm, just the tap

                Assert.That(view.IsSeatChosen(FrogColour.Blue), Is.False);
                Assert.That(view.SeatBadgeNumber(FrogColour.Blue), Is.Null);

                // Green keeps its badge; Orange's badge shifts down one —
                // renumbering happens on removal, not deferred to Start.
                Assert.That(view.SeatBadgeNumber(FrogColour.Green), Is.EqualTo(1));
                Assert.That(view.SeatBadgeNumber(FrogColour.Orange), Is.EqualTo(2));
                Assert.That(view.SeatBadgeText(FrogColour.Orange).text, Is.EqualTo("2"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        public void Start_IsDisabledBelowGameSetupMinFrogs_AndEnabledUpToGameSetupMaxFrogs(int chosenCount, bool expectedEnabled)
        {
            var view = CreateView();

            try
            {
                for (var index = 0; index < chosenCount; index++)
                {
                    Tap(view, AllColours[index]);
                }

                Assert.That(view.StartButton.IsDisabled, Is.EqualTo(!expectedEnabled));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HintText_NamesWhicheverFrogHoldsBadgeOne_NotAHardcodedColour()
        {
            var view = CreateView();

            try
            {
                Tap(view, FrogColour.Pink);
                Tap(view, FrogColour.Blue);

                Assert.That(view.HintText.text, Is.EqualTo("Pink goes first"));

                Tap(view, FrogColour.Pink); // remove Pink; Blue now holds badge 1
                Assert.That(view.HintText.text, Is.EqualTo("Pick two to four frogs"), "only one frog left — Start is disabled again");

                Tap(view, FrogColour.Orange);
                Assert.That(view.HintText.text, Is.EqualTo("Blue goes first"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ResetToEmptySeats_ReturnsToACleanSlate()
        {
            var view = CreateView();

            try
            {
                Tap(view, FrogColour.Green);
                Tap(view, FrogColour.Blue);

                view.ResetToEmptySeats();

                foreach (var colour in AllColours)
                {
                    Assert.That(view.IsSeatChosen(colour), Is.False);
                }

                Assert.That(view.StartButton.IsDisabled, Is.True);
                Assert.That(view.HintText.text, Is.EqualTo("Pick two to four frogs"));
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
        public void Back_ReturnsToTheTitleScreen()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                router.NavigateToScreen(CoreScreen.GameSetup);
                view.Initialize(router);

                TapButton(view.BackButton);

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.TitleScreen));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void DisabledStart_DoesNothingWhenTapped()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                router.NavigateToScreen(CoreScreen.GameSetup);
                view.Initialize(router);

                Tap(view, FrogColour.Green); // one frog — below GameSetupMinFrogs

                TapButton(view.StartButton);

                Assert.That(view.StartedGame, Is.Null);
                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.GameSetup));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Start_CreatesACoreGame_WithTheChosenFrogsInBadgeOrder_AndRoutesToGameBoard_FrogOneActiveNothingRolled()
        {
            var view = CreateView();
            var router = new ScreenRouter();

            try
            {
                router.NavigateToScreen(CoreScreen.GameSetup);
                view.Initialize(router, () => 424242UL);

                // Tap order deliberately not seat order, to prove badge
                // order — not seat position — is what Core receives.
                Tap(view, FrogColour.Pink);
                Tap(view, FrogColour.Green);

                TapButton(view.StartButton);

                Assert.That(view.StartedGame, Is.Not.Null);
                Assert.That(view.StartedGame.TurnOrder, Is.EqualTo(new[] { FrogColour.Pink, FrogColour.Green }));
                Assert.That(view.StartedGame.ActiveFrog, Is.EqualTo(FrogColour.Pink), "frog 1's turn is active");
                Assert.That(view.StartedGame.Phase, Is.EqualTo(TurnPhase.WaitingToRoll), "nothing rolled");
                Assert.That(view.StartedGame.DrawnRoll, Is.Null);
                Assert.That(view.StartedGame.Seed, Is.EqualTo(424242UL));

                Assert.That(router.CurrentScreen, Is.EqualTo(CoreScreen.GameBoard));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void SeatTapTarget_ActsOnRelease_AndEmitsNothingWhenReleaseLandsOutside()
        {
            var view = CreateView();

            try
            {
                var target = view.SeatTapTargetFor(FrogColour.Green);

                target.OnPointerDown(EventDataAt(target.RectTransform, inside: true));
                target.OnPointerUp(EventDataAt(target.RectTransform, inside: false));

                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.False, "a finger that lands wrong can slide off and cancel");

                target.OnPointerDown(EventDataAt(target.RectTransform, inside: true));
                target.OnPointerUp(EventDataAt(target.RectTransform, inside: true));

                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.True);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EverySeat_IsAtLeastMinTouchTargetInBothDirections()
        {
            Assert.That(GameSetupScreenView.SeatWidth, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
            Assert.That(GameSetupScreenView.SeatHeight, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
        }

        [Test]
        public void Seats_UseTheirNamedConstants_ForSizeGapSwatchAndBadgeGeometry()
        {
            var view = CreateView();

            try
            {
                foreach (var colour in AllColours)
                {
                    Assert.That(view.SeatRect(colour).sizeDelta, Is.EqualTo(new Vector2(GameSetupScreenView.SeatWidth, GameSetupScreenView.SeatHeight)));
                    Assert.That(view.SeatSwatch(colour).rectTransform.sizeDelta, Is.EqualTo(new Vector2(GameSetupScreenView.SeatSwatchDiameter, GameSetupScreenView.SeatSwatchDiameter)));
                    Assert.That(view.SeatBadgeRect(colour).sizeDelta, Is.EqualTo(new Vector2(GameSetupScreenView.SeatOrderBadge, GameSetupScreenView.SeatOrderBadge)));
                    Assert.That(view.SeatBadgeRect(colour).anchoredPosition, Is.EqualTo(new Vector2(GameSetupScreenView.SeatBadgeInset, -GameSetupScreenView.SeatBadgeInset)));
                }

                var gap = view.SeatRect(FrogColour.Blue).anchoredPosition.x - view.SeatRect(FrogColour.Green).anchoredPosition.x;
                Assert.That(gap, Is.EqualTo(GameSetupScreenView.SeatWidth + GameSetupScreenView.SeatGap).Within(0.001f));

                // docs/specs/ui/game-setup.md: "Four seats at 360 px with
                // three 48 px gaps is 1584 px."
                var rowWidth = (4 * GameSetupScreenView.SeatWidth) + (3 * GameSetupScreenView.SeatGap);
                Assert.That(view.SeatsRect.sizeDelta.x, Is.EqualTo(rowWidth));
                Assert.That(view.SeatsRect.anchoredPosition.x, Is.EqualTo(0f), "the row is centred as a whole");

                // The swatch and the label below it are SeatContentGap
                // apart — docs/specs/ui/mockups/game-setup.html.
                var swatchRect = view.SeatSwatch(FrogColour.Green).rectTransform;
                var labelRect = view.SeatLabel(FrogColour.Green).rectTransform;
                var swatchBottom = swatchRect.anchoredPosition.y - (GameSetupScreenView.SeatSwatchDiameter / 2f);
                var labelTop = labelRect.anchoredPosition.y + (GameSetupScreenView.SeatLabelSize / 2f);
                Assert.That(swatchBottom - labelTop, Is.EqualTo(GameSetupScreenView.SeatContentGap).Within(0.001f));

                // hint sits HintGap beneath seats.
                var seatsBottom = view.SeatsRect.anchoredPosition.y - (GameSetupScreenView.SeatHeight / 2f);
                var hintTop = view.HintRect.anchoredPosition.y + (GameSetupScreenView.SetupHintSize / 2f);
                Assert.That(seatsBottom - hintTop, Is.EqualTo(GameSetupScreenView.HintGap).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ChosenSeat_IsFilled_WithTheSeatChosenRing_AroundIt()
        {
            var view = CreateView();

            try
            {
                Tap(view, FrogColour.Green);

                var fillRect = view.SeatFill(FrogColour.Green).rectTransform;
                Assert.That(fillRect.offsetMin, Is.EqualTo(new Vector2(GameSetupScreenView.SeatChosenRing, GameSetupScreenView.SeatChosenRing)));
                Assert.That(fillRect.offsetMax, Is.EqualTo(new Vector2(-GameSetupScreenView.SeatChosenRing, -GameSetupScreenView.SeatChosenRing)));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void GameSetupMinAndMaxFrogs_MatchCoresOwnGameConstants()
        {
            // docs/specs/ui/game-setup.md's Invariants section: "a game
            // cannot start with fewer than two frogs or more than four" —
            // the same rule Frogs.Core.Game's own constructor enforces.
            Assert.That(GameSetupScreenView.GameSetupMinFrogs, Is.EqualTo(Frogs.Core.Game.MinFrogsPerGame));
            Assert.That(GameSetupScreenView.GameSetupMaxFrogs, Is.EqualTo(Frogs.Core.Game.MaxFrogsPerGame));
        }

        static readonly FrogColour[] AllColours =
        {
            FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink
        };

        static GameSetupScreenView CreateView()
        {
            var host = new GameObject(nameof(GameSetupScreenViewTests), typeof(RectTransform));
            return host.AddComponent<GameSetupScreenView>();
        }

        static void Destroy(GameSetupScreenView view)
        {
            if (view != null)
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        static void Tap(GameSetupScreenView view, FrogColour colour)
        {
            var target = view.SeatTapTargetFor(colour);
            var eventData = EventDataAt(target.RectTransform, inside: true);

            target.OnPointerDown(eventData);
            target.OnPointerUp(eventData);
        }

        static void TapButton(Button button)
        {
            var eventData = EventDataAt(button.RectTransform, inside: true);

            button.OnPointerDown(eventData);
            button.OnPointerUp(eventData);
        }

        static PointerEventData EventDataAt(RectTransform rect, bool inside)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var center = (Vector2)(corners[0] + corners[2]) / 2f;
            var width = corners[2].x - corners[0].x;

            var outside = center + new Vector2(Mathf.Abs(width) + (Button.MinTouchTarget * 10f), 0f);

            return new PointerEventData(null)
            {
                position = inside ? center : outside
            };
        }
    }
}
