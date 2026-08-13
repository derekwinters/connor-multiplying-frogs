using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity.Views;
// UnityEngine.UI also declares a Button type — the same collision every other
// view test works around — so this is pulled in by explicit alias, and a bare
// `Button` in this file always means the shared component's.
using Button = Frogs.Unity.UI.Button;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// Naming a player on game setup — issue #311, built against
    /// docs/specs/ui/game-setup.md and the two authoritative mockups
    /// `game-setup-names-set.html` (at rest) and
    /// `game-setup-name-edit-inline.html` (typing).
    ///
    /// The wireframe (#310) settled the two things these tests are mostly
    /// about: the name row is the edit target and the corner badge is the
    /// remove target, so **a chosen seat's body does nothing at all**; and the
    /// keyboard is one this game draws rather than Android's.
    /// </summary>
    public sealed class GameSetupNamingTests
    {
        static readonly FrogColour[] AllColours =
        {
            FrogColour.Green, FrogColour.Blue, FrogColour.Orange, FrogColour.Pink
        };

        // ---- The seat at rest -------------------------------------------

        [Test]
        public void ASeatedFrog_ShowsItsBareColourName_WithNothingAppendedToIt()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Blue);

                Assert.That(view.NameFor(FrogColour.Blue), Is.EqualTo("Blue"));
                Assert.That(view.SeatLabel(FrogColour.Blue).text, Is.EqualTo("Blue"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void AnEmptySeat_HasNoNameRowAndNoRemoveTarget_ButItsBodyStillSeatsTheFrog()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.SeatNameRowRoot(FrogColour.Pink).activeSelf, Is.False);
                Assert.That(view.SeatRemoveRoot(FrogColour.Pink).activeSelf, Is.False);

                TapSeatBody(view, FrogColour.Pink);

                Assert.That(view.IsSeatChosen(FrogColour.Pink), Is.True);
                Assert.That(view.SeatNameRowRoot(FrogColour.Pink).activeSelf, Is.True);
                Assert.That(view.SeatRemoveRoot(FrogColour.Pink).activeSelf, Is.True);
            }
            finally
            {
                Destroy(view);
            }
        }

        // The change the wireframe exists for. This is #310's question 2, and
        // the test is the thing that stops the fix drifting back into one
        // target.
        [Test]
        public void TheEditTargetAndTheRemoveTargetAreSeparate_AndAChosenSeatsBodyIsInert()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.True);

                // Tapping the body of a chosen seat does nothing at all — it
                // does not remove the frog, and it does not open the editor.
                TapSeatBody(view, FrogColour.Green);
                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.True, "a chosen seat's body must not remove the frog");
                Assert.That(view.EditingSeat, Is.Null, "a chosen seat's body must not open the editor");

                // The name row edits, and does not remove.
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                Assert.That(view.EditingSeat, Is.EqualTo(FrogColour.Green));
                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.True);

                view.DoneNaming();

                // The remove target removes, and does not edit.
                TapTarget(view.SeatRemoveTapTarget(FrogColour.Green));
                Assert.That(view.IsSeatChosen(FrogColour.Green), Is.False);
                Assert.That(view.EditingSeat, Is.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void BothTargets_AreMinTouchTargetSafe_ByGeometryRatherThanByInspection()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);

                var nameRow = view.SeatNameRowRect(FrogColour.Green).sizeDelta;
                var remove = view.SeatRemoveRect(FrogColour.Green).sizeDelta;

                Assert.That(nameRow.x, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
                Assert.That(nameRow.y, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
                Assert.That(remove.x, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
                Assert.That(remove.y, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));

                // The remove target is MinTouchTarget exactly, per the spec's
                // Elements section, and larger than the readout opposite it.
                Assert.That(remove.x, Is.EqualTo(GameSetupScreenView.SeatCornerTarget));
                Assert.That(GameSetupScreenView.SeatCornerTarget, Is.GreaterThan(GameSetupScreenView.SeatOrderBadge));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void RemovingAFrog_RenumbersTheBadgesAfterItImmediately()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapSeatBody(view, FrogColour.Blue);
                TapSeatBody(view, FrogColour.Orange);

                TapTarget(view.SeatRemoveTapTarget(FrogColour.Blue));

                Assert.That(view.SeatBadgeNumber(FrogColour.Green), Is.EqualTo(1));
                Assert.That(view.SeatBadgeNumber(FrogColour.Blue), Is.Null);
                Assert.That(view.SeatBadgeNumber(FrogColour.Orange), Is.EqualTo(2));
            }
            finally
            {
                Destroy(view);
            }
        }

        // ---- The keyboard -----------------------------------------------

        [Test]
        public void TheKeyboard_IsOneThisGameDraws_AndIsUpOnlyWhileANameIsBeingTyped()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.KeyboardRoot.activeSelf, Is.False);

                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                Assert.That(view.KeyboardRoot.activeSelf, Is.True);

                // hint and the controls are hidden while it is up — the
                // keyboard is laid out over them, not beside them.
                Assert.That(view.HintRect.gameObject.activeSelf, Is.False);
                Assert.That(view.ControlsRect.gameObject.activeSelf, Is.False);

                view.DoneNaming();

                Assert.That(view.KeyboardRoot.activeSelf, Is.False);
                Assert.That(view.HintRect.gameObject.activeSelf, Is.True);
                Assert.That(view.ControlsRect.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheKeyboard_LaysOutItsFourQwertyRows_AtItsNamedConstants()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                Assert.That(view.KeyboardRect.sizeDelta, Is.EqualTo(
                    new Vector2(GameSetupScreenView.NameKeyboardWidth, GameSetupScreenView.NameKeyboardHeight)));

                var letters = string.Concat(view.NameKeys
                    .Where(key => key.Kind == NameKeyKind.Letter)
                    .Select(key => key.Character));

                Assert.That(letters, Is.EqualTo("QWERTYUIOPASDFGHJKLZXCVBNM"), "the mockups draw QWERTY");

                foreach (var key in view.NameKeys.Where(key => key.Kind == NameKeyKind.Letter))
                {
                    Assert.That(key.RectTransform.sizeDelta, Is.EqualTo(
                        new Vector2(GameSetupScreenView.NameKeyWidth, GameSetupScreenView.NameKeyHeight)));
                }

                Assert.That(view.SpaceKey.RectTransform.sizeDelta.x, Is.EqualTo(GameSetupScreenView.NameSpaceKeyWidth));
                Assert.That(view.DoneKey.RectTransform.sizeDelta.x, Is.EqualTo(GameSetupScreenView.NameDoneKeyWidth));
            }
            finally
            {
                Destroy(view);
            }
        }

        // #288's lesson: without a raycast target of its own there is nothing
        // raycastable under the key, the GraphicRaycaster never finds it, and
        // the keyboard types nothing.
        [Test]
        public void EveryKey_HasARaycastTargetOfItsOwn()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                foreach (var key in view.NameKeys)
                {
                    Assert.That(key.Border.raycastTarget, Is.True, $"{key.name} needs its own raycast target");
                    Assert.That(key.Label.raycastTarget, Is.False, $"{key.name}'s label must not steal the tap");
                }
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EveryKey_IsMinTouchTargetSafeInHeight()
        {
            Assert.That(GameSetupScreenView.NameKeyHeight, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
            Assert.That(GameSetupScreenView.NameKeyWidth, Is.GreaterThanOrEqualTo(Button.MinTouchTarget));
        }

        [Test]
        public void TypingAName_ReplacesTheSeatsNameOnDone()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                view.ClearName();
                TapKeys(view, "DAD");
                view.DoneNaming();

                Assert.That(view.NameFor(FrogColour.Green), Is.EqualTo("DAD"));
                Assert.That(view.SeatLabel(FrogColour.Green).text, Is.EqualTo("DAD"));
                Assert.That(view.EditingSeat, Is.Null);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Backspace_DeletesTheLastCharacter()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                view.ClearName();
                TapKeys(view, "DAD");
                TapKey(view.BackspaceKey);
                view.DoneNaming();

                Assert.That(view.NameFor(FrogColour.Green), Is.EqualTo("DA"));
            }
            finally
            {
                Destroy(view);
            }
        }

        // docs/specs/ui/game-setup.md#behaviour: "At `PlayerNameMaxLength` the
        // next keystroke is refused — the key does nothing, the name is
        // unchanged, and nothing explains itself." At the boundary exactly.
        [Test]
        public void AtThePlayerNameMaxLength_TheNextKeystrokeIsRefusedSilently()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                view.ClearName();

                for (var i = 0; i < GameSetupScreenView.PlayerNameMaxLength; i++)
                {
                    TapKey(view.NameKey('A'));
                }

                var atTheCap = view.TypedName;
                Assert.That(atTheCap.Length, Is.EqualTo(GameSetupScreenView.PlayerNameMaxLength));

                TapKey(view.NameKey('B'));

                Assert.That(view.TypedName, Is.EqualTo(atTheCap), "the eleventh keystroke does nothing");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ClearingTheNameAndPressingDone_RestoresTheColourName()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Orange);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Orange));

                view.ClearName();
                TapKeys(view, "ZZ");
                view.DoneNaming();
                Assert.That(view.NameFor(FrogColour.Orange), Is.EqualTo("ZZ"));

                TapTarget(view.SeatNameRowTapTarget(FrogColour.Orange));
                view.ClearName();
                view.DoneNaming();

                Assert.That(view.NameFor(FrogColour.Orange), Is.EqualTo("Orange"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void AnEmptySeat_CannotBeNamed()
        {
            var view = CreateView();

            try
            {
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Pink));

                Assert.That(view.EditingSeat, Is.Null, "naming a frog that is not in the game is a state with no meaning");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void OnlyOneSeatIsEditedAtATime()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapSeatBody(view, FrogColour.Blue);

                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                Assert.That(view.EditingSeat, Is.EqualTo(FrogColour.Green));

                TapTarget(view.SeatNameRowTapTarget(FrogColour.Blue));
                Assert.That(view.EditingSeat, Is.EqualTo(FrogColour.Blue));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheSeatBeingEdited_HidesItsRemoveTarget()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                Assert.That(view.SeatRemoveRoot(FrogColour.Green).activeSelf, Is.False,
                    "Done first, then remove if that is what you meant");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheSeatRow_MovesUpToClearTheKeyboard_AndBackDownOnDone()
        {
            var view = CreateView();

            try
            {
                var atRest = view.SeatsRect.anchoredPosition.y;

                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                var editing = view.SeatsRect.anchoredPosition.y;

                Assert.That(editing - atRest, Is.EqualTo(
                    GameSetupScreenView.SeatRowTop - GameSetupScreenView.SeatRowEditingTop).Within(0.001f));

                // Nothing else moves and nothing resizes.
                Assert.That(view.SeatRect(FrogColour.Green).sizeDelta, Is.EqualTo(
                    new Vector2(GameSetupScreenView.SeatWidth, GameSetupScreenView.SeatHeight)));

                view.DoneNaming();

                Assert.That(view.SeatsRect.anchoredPosition.y, Is.EqualTo(atRest).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void WhileTyping_TheHeaderSaysWhichFrogIsBeingNamed()
        {
            var view = CreateView();

            try
            {
                Assert.That(view.HeaderText.text, Is.EqualTo("Who is playing?"));

                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));

                Assert.That(view.HeaderText.text, Is.EqualTo("Name the green frog"));

                view.DoneNaming();

                Assert.That(view.HeaderText.text, Is.EqualTo("Who is playing?"));
            }
            finally
            {
                Destroy(view);
            }
        }

        // ---- The hint and Start ------------------------------------------

        [Test]
        public void TheHint_NamesWhoeverGoesFirst_ByTheirTypedName()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapSeatBody(view, FrogColour.Blue);

                Assert.That(view.HintText.text, Is.EqualTo("Green goes first"));

                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                view.ClearName();
                TapKeys(view, "CONNOR");
                view.DoneNaming();

                Assert.That(view.HintText.text, Is.EqualTo("CONNOR goes first"),
                    "not `Green goes first` once Green has been renamed");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Start_HandsTheTypedNamesToCore_InBadgeOrder()
        {
            var view = CreateView();

            try
            {
                view.Initialize(new ScreenRouter(), () => 311UL);

                TapSeatBody(view, FrogColour.Orange);
                TapSeatBody(view, FrogColour.Green);

                TapTarget(view.SeatNameRowTapTarget(FrogColour.Orange));
                view.ClearName();
                TapKeys(view, "CONNOR");
                view.DoneNaming();

                TapButton(view.StartButton);

                var game = view.StartedGame;

                Assert.That(game, Is.Not.Null);
                Assert.That(game.TurnOrder, Is.EqualTo(new[] { FrogColour.Orange, FrogColour.Green }));
                Assert.That(game.NameFor(FrogColour.Orange), Is.EqualTo("CONNOR"));
                Assert.That(game.NameFor(FrogColour.Green), Is.EqualTo("Green"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ResetToEmptySeats_ForgetsEveryTypedName()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                view.ClearName();
                TapKeys(view, "CONNOR");
                view.DoneNaming();

                view.ResetToEmptySeats();
                TapSeatBody(view, FrogColour.Green);

                Assert.That(view.NameFor(FrogColour.Green), Is.EqualTo("Green"),
                    "no name survives re-entering setup");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void ReSeatingAFrogAfterRemovingIt_ForgetsItsTypedName()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapTarget(view.SeatNameRowTapTarget(FrogColour.Green));
                view.ClearName();
                TapKeys(view, "CONNOR");
                view.DoneNaming();

                TapTarget(view.SeatRemoveTapTarget(FrogColour.Green));
                TapSeatBody(view, FrogColour.Green);

                Assert.That(view.NameFor(FrogColour.Green), Is.EqualTo("Green"));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TwoSeatsMayHoldTheSameName_NothingPreventsNumbersOrWarns()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);
                TapSeatBody(view, FrogColour.Blue);

                foreach (var colour in new[] { FrogColour.Green, FrogColour.Blue })
                {
                    TapTarget(view.SeatNameRowTapTarget(colour));
                    view.ClearName();
                    TapKeys(view, "SAM");
                    view.DoneNaming();
                }

                Assert.That(view.NameFor(FrogColour.Green), Is.EqualTo("SAM"));
                Assert.That(view.NameFor(FrogColour.Blue), Is.EqualTo("SAM"));
                Assert.That(view.StartButton.IsDisabled, Is.False);
            }
            finally
            {
                Destroy(view);
            }
        }

        // ---- Seat geometry, against the spec's table ---------------------

        [Test]
        public void TheSeat_IsFourEightyTall_WithItsContentGapAtSixteen()
        {
            Assert.That(GameSetupScreenView.SeatHeight, Is.EqualTo(480f));
            Assert.That(GameSetupScreenView.SeatContentGap, Is.EqualTo(16f));
            Assert.That(GameSetupScreenView.SeatTopBand, Is.EqualTo(136f));
        }

        [Test]
        public void TheNameRow_SitsSeatContentGapBelowTheSwatch_AtItsNamedSize()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);

                var swatch = view.SeatSwatch(FrogColour.Green).rectTransform;
                var nameRow = view.SeatNameRowRect(FrogColour.Green);

                Assert.That(nameRow.sizeDelta, Is.EqualTo(
                    new Vector2(GameSetupScreenView.SeatNameRowWidth, GameSetupScreenView.SeatNameRowHeight)));

                var swatchBottom = swatch.anchoredPosition.y - (GameSetupScreenView.SeatSwatchDiameter / 2f);
                var nameRowTop = nameRow.anchoredPosition.y + (GameSetupScreenView.SeatNameRowHeight / 2f);

                Assert.That(swatchBottom - nameRowTop, Is.EqualTo(GameSetupScreenView.SeatContentGap).Within(0.001f));

                // SeatTopBand is the space above the swatch that the two
                // corner targets live in.
                var seatTop = GameSetupScreenView.SeatHeight / 2f;
                var swatchTop = swatch.anchoredPosition.y + (GameSetupScreenView.SeatSwatchDiameter / 2f);

                Assert.That(seatTop - swatchTop, Is.EqualTo(GameSetupScreenView.SeatTopBand).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheRemoveTarget_SitsInTheTopRightCorner_AtItsNamedInset()
        {
            var view = CreateView();

            try
            {
                TapSeatBody(view, FrogColour.Green);

                var remove = view.SeatRemoveRect(FrogColour.Green);

                // Anchored to the seat's top-right, inset by SeatCornerInset.
                Assert.That(remove.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(remove.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(remove.anchoredPosition.x, Is.EqualTo(-GameSetupScreenView.SeatCornerInset).Within(0.001f));
                Assert.That(remove.anchoredPosition.y, Is.EqualTo(-GameSetupScreenView.SeatCornerInset).Within(0.001f));

                // It does not overlap the swatch — the whole reason the seat
                // grew from 440 to 480.
                var removeBottom = (GameSetupScreenView.SeatHeight / 2f)
                    - GameSetupScreenView.SeatCornerInset
                    - GameSetupScreenView.SeatCornerTarget;
                var swatchTop = view.SeatSwatch(FrogColour.Green).rectTransform.anchoredPosition.y
                    + (GameSetupScreenView.SeatSwatchDiameter / 2f);

                Assert.That(removeBottom, Is.GreaterThanOrEqualTo(swatchTop));
            }
            finally
            {
                Destroy(view);
            }
        }

        // ---- Harness ------------------------------------------------------

        static GameSetupScreenView CreateView()
        {
            var go = new GameObject("GameSetupScreenView", typeof(RectTransform));
            return go.AddComponent<GameSetupScreenView>();
        }

        static void Destroy(GameSetupScreenView view)
        {
            Object.DestroyImmediate(view.gameObject);
        }

        static void TapSeatBody(GameSetupScreenView view, FrogColour colour)
        {
            TapTarget(view.SeatTapTargetFor(colour));
        }

        static void TapTarget(GameSetupScreenView.SeatTapTarget target)
        {
            var eventData = EventDataAt(target.RectTransform, inside: true);

            target.OnPointerDown(eventData);
            target.OnPointerUp(eventData);
        }

        static void TapKeys(GameSetupScreenView view, string letters)
        {
            foreach (var letter in letters)
            {
                TapKey(view.NameKey(letter));
            }
        }

        static void TapKey(NameKeyboardKey key)
        {
            key.OnPointerClick(new PointerEventData(null));
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
