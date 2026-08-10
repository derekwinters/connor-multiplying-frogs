using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Core;
using Frogs.Unity.Views;
// UnityEngine.UI also declares a Button type — the same collision
// ButtonTests.cs, TitleScreenView.cs and GameBoardScreenViewTests.cs work
// around — so these are pulled in by explicit alias, and a bare `Button`,
// `ButtonKind` or `DialogPanel` in this file always means the shared
// component's.
using Button = Frogs.Unity.UI.Button;
using ButtonKind = Frogs.Unity.UI.ButtonKind;
using DialogPanel = Frogs.Unity.UI.DialogPanel;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The settings dialog — issue #222, built against
    /// docs/specs/ui/settings-dialog.md and its committed 1:1 mockup.
    ///
    /// The three things these tests are really here to hold down:
    ///
    /// - **`End the game` never ends anything.** It opens
    ///   docs/specs/ui/end-game-confirm.md (#226) and nothing else. The test
    ///   below proves it structurally — the view is never handed a reference
    ///   capable of ending a game, so there is no path by which it could.
    /// - **`Back to the game` and hardware back are one action.** Not two
    ///   handlers that agree today.
    /// - **`ButtonDestructiveGap` does not shrink.** The gap under the
    ///   destructive button is the layout, not decoration, so it is asserted
    ///   against the named constant rather than against a pixel position.
    /// </summary>
    public sealed class SettingsDialogViewTests
    {
        const string ReadableBuildName = "0.2.3-abc1234";
        const string UnreadableBuildName = "nightly";

        [Test]
        public void Layout_IsTheSharedDialogAtTheSpecsOwnSize_WithAllFourRegions()
        {
            var view = CreateView();

            try
            {
                // A centred shared Dialog sized SettingsDialogWidth x
                // SettingsDialogHeight — not DialogMaxWidth/DialogMaxHeight.
                Assert.That(
                    view.Dialog.PanelRect.sizeDelta,
                    Is.EqualTo(new Vector2(
                        SettingsDialogView.SettingsDialogWidth,
                        SettingsDialogView.SettingsDialogHeight)));

                // title
                Assert.That(view.Dialog.TitleText.text, Is.EqualTo("Settings"));

                // actions — `How to play`, then `End the game`, one
                // left-aligned column.
                Assert.That(view.HowToPlayButton, Is.Not.Null);
                Assert.That(view.EndGameButton, Is.Not.Null);
                Assert.That(LeftEdge(view.HowToPlayButton.RectTransform),
                    Is.EqualTo(LeftEdge(view.EndGameButton.RectTransform)).Within(0.001f),
                    "one left-aligned column");
                Assert.That(
                    LeftEdge(view.HowToPlayButton.RectTransform) - LeftEdge(view.Dialog.PanelRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    BottomEdge(view.HowToPlayButton.RectTransform),
                    Is.GreaterThan(TopEdge(view.EndGameButton.RectTransform)),
                    "`How to play`, then `End the game`");

                // footprint — bottom-left.
                Assert.That(view.VersionText, Is.Not.Null);
                Assert.That(view.VersionText.alignment, Is.EqualTo(TextAnchor.LowerLeft));
                Assert.That(
                    LeftEdge(view.VersionText.rectTransform) - LeftEdge(view.Dialog.PanelRect),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
                Assert.That(
                    BottomEdge(view.VersionText.rectTransform) - BottomEdge(view.Dialog.PanelRect),
                    Is.EqualTo(SettingsDialogView.SettingsVersionBottomOffset).Within(0.001f));

                // controls — the primary button, bottom-right, per the shared
                // Dialog's primary-on-the-right rule.
                Assert.That(view.BackToTheGameButton, Is.Not.Null);
                Assert.That(view.Dialog.Buttons, Has.Member(view.BackToTheGameButton));
                Assert.That(
                    RightEdge(view.Dialog.PanelRect) - RightEdge(view.BackToTheGameButton.RectTransform),
                    Is.EqualTo(DialogPanel.DialogPadding).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PublicConstants_AreExactlySettingsDialogsOwn_UnderTheIdenticalNames()
        {
            // Everything else this screen measures itself with — the shared
            // Dialog's padding and title metrics, the shared Button's height,
            // label size and ButtonDestructiveGap, title-screen.md's
            // VersionLabelSize — is referenced from where it already lives
            // rather than redeclared here. This test is what proves that: a
            // second copy of any of them would show up as an extra name.
            AssertPublicConstantsAreExactly(typeof(SettingsDialogView), new Dictionary<string, float>
            {
                { nameof(SettingsDialogView.SettingsDialogWidth), 900f },
                { nameof(SettingsDialogView.SettingsDialogHeight), 760f },
                { nameof(SettingsDialogView.SettingsActionWidth), 788f },
                { nameof(SettingsDialogView.SettingsActionGap), 96f },
                { nameof(SettingsDialogView.SettingsVersionBottomOffset), 60f }
            });
        }

        [Test]
        public void BackToTheGame_IsPrimaryAtTheSharedFootprint_AndClosesExactlyOnce()
        {
            var view = CreateView();

            try
            {
                var closes = 0;
                var confirms = 0;
                view.CloseRequested += () => closes++;
                view.EndGameConfirmRequested += () => confirms++;

                Assert.That(view.BackToTheGameButton.Kind, Is.EqualTo(ButtonKind.Primary));

                // "nothing about it is widened the way the two action buttons
                // are" — the shared component's own default footprint.
                Assert.That(
                    view.BackToTheGameButton.RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(Button.ButtonMinWidth, Button.ButtonHeight)));

                TapButton(view.BackToTheGameButton);

                Assert.That(closes, Is.EqualTo(1));
                Assert.That(confirms, Is.Zero, "the least destructive button never opens the confirm");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_FiresTheIdenticalCloseCallbackBackToTheGameFires()
        {
            var view = CreateView();

            try
            {
                var closes = 0;
                var confirms = 0;
                view.CloseRequested += () => closes++;
                view.EndGameConfirmRequested += () => confirms++;

                // The router (#213) already routes hardware back on
                // Dialog.Settings to "what `Back to the game` does". This view
                // exposes that one action rather than listening for the key
                // itself — so there is exactly one close path, not two that
                // happen to agree.
                view.RequestClose();

                Assert.That(closes, Is.EqualTo(1));
                Assert.That(confirms, Is.Zero, "never `End the game`'s");

                TapButton(view.BackToTheGameButton);

                Assert.That(closes, Is.EqualTo(2), "the same callback, not a second distinct one");
                Assert.That(confirms, Is.Zero);
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HardwareBack_IsNotListenedForTwice()
        {
            // The board (#220) owns the Escape key on the screen underneath,
            // and the router owns what back means with a dialog open. A second
            // Update()/Input handler here would be a third opinion.
            var declared = DeclaredMemberNames(typeof(SettingsDialogView));

            Assert.That(declared, Does.Not.Contain("Update"));
        }

        [Test]
        public void BackToTheGame_IsTheDialogsLeastDestructiveButton()
        {
            var view = CreateView();

            try
            {
                // The value the router reads for hardware back —
                // shared-components.md#dialog: "the hardware back button does
                // what the dialog's least destructive button does, and never
                // what its most destructive one does."
                Assert.That(view.Dialog.LeastDestructiveButton, Is.SameAs(view.BackToTheGameButton));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void HowToPlay_IsPresentAndDisabled_AndATapFiresNothing()
        {
            var view = CreateView();

            try
            {
                var fired = 0;
                view.CloseRequested += () => fired++;
                view.EndGameConfirmRequested += () => fired++;

                // settings-dialog.md's open question proposes "present. A
                // disabled button that appears later is less confusing than a
                // button that appears from nowhere." The screen it would open
                // has no wireframe, so under rule 8 it cannot be built and
                // there is nothing to route to.
                Assert.That(view.HowToPlayButton.IsHidden, Is.False, "present");
                Assert.That(view.HowToPlayButton.IsDisabled, Is.True, "and disabled");
                Assert.That(view.HowToPlayButton.Kind, Is.EqualTo(ButtonKind.Secondary));
                Assert.That(
                    view.HowToPlayButton.CanvasGroup.alpha,
                    Is.EqualTo(Button.ButtonDisabledOpacity).Within(0.001f));
                Assert.That(
                    view.HowToPlayButton.RectTransform.sizeDelta.x,
                    Is.EqualTo(SettingsDialogView.SettingsActionWidth).Within(0.001f));

                TapButton(view.HowToPlayButton);

                Assert.That(view.HowToPlayButton.IsPressed, Is.False, "no press response");
                Assert.That(fired, Is.Zero, "a disabled button does nothing at all");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EndTheGame_OpensTheConfirmExactlyOnce_AndNeverEndsAnything()
        {
            var view = CreateView();

            try
            {
                var confirms = 0;
                var closes = 0;
                view.EndGameConfirmRequested += () => confirms++;
                view.CloseRequested += () => closes++;

                Assert.That(view.EndGameButton.Kind, Is.EqualTo(ButtonKind.Destructive));
                Assert.That(
                    view.EndGameButton.RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(SettingsDialogView.SettingsActionWidth, Button.ButtonHeight)));

                TapButton(view.EndGameButton);

                Assert.That(confirms, Is.EqualTo(1), "it opens the confirm");
                Assert.That(closes, Is.Zero, "and does not close the settings dialog itself");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void EndingTheGame_IsNotACapabilityThisDialogHas()
        {
            // The other half of "it never ends the game itself", and the
            // half a callback count cannot show: the view is never given a
            // reference capable of ending one. No Game, no Lane, no standings
            // — public or private — so there is no method it could call.
            const BindingFlags everything = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var forbidden = new[] { typeof(Game), typeof(Lane), typeof(StandingsRow) };

            foreach (var field in typeof(SettingsDialogView).GetFields(everything))
            {
                Assert.That(
                    forbidden.Any(type => type.IsAssignableFrom(field.FieldType)),
                    Is.False,
                    $"SettingsDialogView holds {field.FieldType.Name} in {field.Name}");
            }

            foreach (var method in typeof(SettingsDialogView).GetMethods(everything))
            {
                foreach (var parameter in method.GetParameters())
                {
                    Assert.That(
                        forbidden.Any(type => type.IsAssignableFrom(parameter.ParameterType)),
                        Is.False,
                        $"SettingsDialogView.{method.Name} is handed a {parameter.ParameterType.Name}");
                }
            }
        }

        [Test]
        public void Gaps_AreTwoDistinctNamedConstants_NotOneSharedNumber()
        {
            var view = CreateView();

            try
            {
                var actionGap = BottomEdge(view.HowToPlayButton.RectTransform)
                    - TopEdge(view.EndGameButton.RectTransform);

                var belowDestructive = BottomEdge(view.EndGameButton.RectTransform)
                    - TopEdge(view.BackToTheGameButton.RectTransform);

                Assert.That(actionGap, Is.EqualTo(SettingsDialogView.SettingsActionGap).Within(0.001f));
                Assert.That(belowDestructive, Is.EqualTo(Button.ButtonDestructiveGap).Within(0.001f));

                // The two happen to be 96 px today. They are two rows on two
                // different tables, and the code keeps them distinct so either
                // can move without dragging the other — this is the assertion
                // that fails if they are ever collapsed into one field.
                Assert.That(
                    typeof(SettingsDialogView).GetField(
                        nameof(SettingsDialogView.SettingsActionGap),
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    Is.Not.Null,
                    "SettingsActionGap is settings-dialog.md's own");

                Assert.That(
                    typeof(Button).GetField(
                        nameof(Button.ButtonDestructiveGap),
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    Is.Not.Null,
                    "ButtonDestructiveGap is the shared Button's, referenced not redeclared");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void TheGapUnderTheDestructiveButton_SurvivesALongerLabel()
        {
            // "it must not drift closer to the destructive one above it, in
            // either direction, for any reason (a longer label, a smaller
            // dialog height, anything)."
            var view = CreateView();

            try
            {
                view.EndGameButton.SetLabelText("End the game for absolutely everybody right now");
                view.BackToTheGameButton.SetLabelText("Back to the game, please");

                var belowDestructive = BottomEdge(view.EndGameButton.RectTransform)
                    - TopEdge(view.BackToTheGameButton.RectTransform);

                Assert.That(belowDestructive, Is.EqualTo(Button.ButtonDestructiveGap).Within(0.001f));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Version_IsReadThroughAppVersion_NeverTyped()
        {
            // Asserted the same way HelloWorldProbeTests asserts
            // HelloWorldProbe.Describe: against a static formatting method fed
            // a fixed build-name string, not against a live
            // Application.version at test time.
            var expected = AppVersion.ReadFromBuildName(ReadableBuildName);

            Assert.That(
                SettingsDialogView.FormatVersionLabel(ReadableBuildName),
                Is.EqualTo("v" + expected));
        }

        [Test]
        public void Version_IsDrawnAtVersionLabelSize()
        {
            var view = CreateView();

            try
            {
                // title-screen.md's own constant, the same value doing the
                // same job — referenced, not redeclared.
                Assert.That(view.VersionText.fontSize, Is.EqualTo((int)TitleScreenView.VersionLabelSize));
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void Version_DoesNotThrowOnAnUnreadableBuildStamp_AndTheDialogStaysUsable()
        {
            // Mirrors HelloWorldProbeTests'
            // TheProbeSaysSoRatherThanThrowingWhenTheBuildStampIsUnreadable.
            // No specific fallback wording is asserted — settings-dialog.md
            // does not define one, so this issue does not invent one.
            Assert.That(() => SettingsDialogView.FormatVersionLabel(UnreadableBuildName), Throws.Nothing);
            Assert.That(() => SettingsDialogView.FormatVersionLabel(null), Throws.Nothing);

            var view = CreateView();

            try
            {
                var closes = 0;
                view.CloseRequested += () => closes++;

                Assert.That(view.VersionText.text, Is.Not.Null.And.Not.Empty);

                TapButton(view.BackToTheGameButton);

                Assert.That(closes, Is.EqualTo(1), "still usable");
            }
            finally
            {
                Destroy(view);
            }
        }

        [Test]
        public void PublicSurface_IsOpeningClosingAndTheVersionRead_AndNothingElse()
        {
            // settings-dialog.md's first invariant: "opening this changes
            // nothing about the game. It is a menu, not a turn." And: "nothing
            // here can change a rule of the game mid-play. There is no
            // difficulty setting, no player count, no undo." This is the test
            // that carries both — as a claim about the type's surface, not a
            // round-trip against a live game.
            var expected = new[]
            {
                nameof(SettingsDialogView.SettingsDialogWidth),
                nameof(SettingsDialogView.SettingsDialogHeight),
                nameof(SettingsDialogView.SettingsActionWidth),
                nameof(SettingsDialogView.SettingsActionGap),
                nameof(SettingsDialogView.SettingsVersionBottomOffset),
                // The two events, spelled out rather than via nameof — an
                // event cannot be named from outside its declaring type in
                // every context, and these two are the whole of what this
                // dialog can ask anybody to do.
                "CloseRequested",
                "EndGameConfirmRequested",
                nameof(SettingsDialogView.RectTransform),
                nameof(SettingsDialogView.Dialog),
                nameof(SettingsDialogView.ActionsRect),
                nameof(SettingsDialogView.FootprintRect),
                nameof(SettingsDialogView.HowToPlayButton),
                nameof(SettingsDialogView.EndGameButton),
                nameof(SettingsDialogView.BackToTheGameButton),
                nameof(SettingsDialogView.VersionText),
                nameof(SettingsDialogView.Open),
                nameof(SettingsDialogView.Close),
                nameof(SettingsDialogView.RequestClose),
                nameof(SettingsDialogView.FormatVersionLabel)
            };

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var declared = typeof(SettingsDialogView)
                .GetMembers(flags)
                .Select(member => member.Name)
                // Property getters and event add/remove accessors come back as
                // methods of their own; the member they belong to is already
                // in the list.
                .Where(name => !name.StartsWith("get_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("set_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("add_", StringComparison.Ordinal))
                .Where(name => !name.StartsWith("remove_", StringComparison.Ordinal))
                // MonoBehaviour subclasses get an implicit public constructor.
                .Where(name => !name.StartsWith(".", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal);

            Assert.That(declared, Is.EqualTo(expected.OrderBy(name => name, StringComparer.Ordinal)));

            foreach (var forbidden in new[] { "Difficulty", "PlayerCount", "Roster", "Undo", "Quit", "Finish" })
            {
                var word = forbidden;

                Assert.That(
                    DeclaredMemberNames(typeof(SettingsDialogView))
                        .Where(name => name.IndexOf(word, StringComparison.Ordinal) >= 0),
                    Is.Empty,
                    $"nothing on this dialog reaches {word}");
            }
        }

        static SettingsDialogView CreateView()
        {
            var host = new GameObject(nameof(SettingsDialogViewTests), typeof(RectTransform));
            return host.AddComponent<SettingsDialogView>();
        }

        static void Destroy(SettingsDialogView view)
        {
            if (view != null)
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        static void TapButton(Button button)
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

        static float LeftEdge(RectTransform rect)
        {
            return Corners(rect)[0].x;
        }

        static float RightEdge(RectTransform rect)
        {
            return Corners(rect)[2].x;
        }

        static float BottomEdge(RectTransform rect)
        {
            return Corners(rect)[0].y;
        }

        static float TopEdge(RectTransform rect)
        {
            return Corners(rect)[1].y;
        }

        static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        static IEnumerable<string> DeclaredMemberNames(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetMembers(flags).Select(member => member.Name).ToArray();
        }

        static void AssertPublicConstantsAreExactly(Type type, IDictionary<string, float> expected)
        {
            var constants = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsLiteral && !field.IsInitOnly)
                .ToArray();

            Assert.That(
                constants.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(expected.Keys.OrderBy(name => name, StringComparer.Ordinal)),
                $"{type.Name}'s public constants are exactly settings-dialog.md's own, under the identical names");

            foreach (var field in constants)
            {
                Assert.That(
                    Convert.ToSingle(field.GetValue(null)),
                    Is.EqualTo(expected[field.Name]).Within(0.001f),
                    $"{type.Name}.{field.Name}");
            }
        }
    }
}
