using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Frogs.Unity.UI;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The shared Player chip — issue #219. Built through the typed Unity
    /// API with no committed prefab, the same decision #214 made for
    /// <see cref="Button"/>. Written before <c>PlayerChip</c> exists, per
    /// docs/engineering/testing.md's sanctioned flow: pushed unexecuted,
    /// with CI turning these red before green — there is no editor here to
    /// watch them fail.
    /// </summary>
    public sealed class PlayerChipTests
    {
        [Test]
        public void Default_ShowsSwatchColourNameAndPadCount_SizedToTheNamedConstants()
        {
            var chip = CreateChip();

            try
            {
                chip.SetFrog(new Color32(0x3E, 0x93, 0x3E, 0xFF), "Green");
                chip.SetPadCount("3 of 8");

                Assert.That(chip.RectTransform.sizeDelta, Is.EqualTo(new Vector2(PlayerChip.PlayerChipWidth, PlayerChip.PlayerChipHeight)));
                Assert.That(chip.Swatch.rectTransform.sizeDelta, Is.EqualTo(new Vector2(PlayerChip.PlayerSwatchDiameter, PlayerChip.PlayerSwatchDiameter)));
                Assert.That(chip.Label.fontSize, Is.EqualTo((int)PlayerChip.PlayerChipLabelSize));
                Assert.That(chip.PadCountText.fontSize, Is.EqualTo((int)PlayerChip.PlayerChipPadCountSize));
                Assert.That(chip.Label.text, Is.EqualTo("Green"));
                Assert.That(chip.PadCountText.text, Is.EqualTo("3 of 8"));
                Assert.That(chip.State, Is.EqualTo(PlayerChipState.Default));
            }
            finally
            {
                Destroy(chip);
            }
        }

        [Test]
        public void Chip_WiresItsBuiltInSpriteAndFont_RatherThanLeavingThemMissing()
        {
            var chip = CreateChip();

            try
            {
                Assert.That(chip.Swatch.sprite, Is.Not.Null, "built-in swatch sprite missing — check the resource name");
                Assert.That(chip.Label.font, Is.Not.Null, "built-in label font missing — check the resource name");
                Assert.That(chip.PadCountText.font, Is.Not.Null, "built-in pad-count font missing — check the resource name");
            }
            finally
            {
                Destroy(chip);
            }
        }

        [Test]
        public void Active_RendersThePlayerChipActiveRing_AndIsVisiblyDifferentByMoreThanColour()
        {
            var chip = CreateChip();

            try
            {
                chip.SetFrog(Color.green, "Green");
                var defaultBorderAlpha = chip.BorderColor.a;
                var defaultFontStyle = chip.Label.fontStyle;

                chip.SetState(PlayerChipState.Active);

                Assert.That(chip.State, Is.EqualTo(PlayerChipState.Active));
                Assert.That(chip.BorderColor.a, Is.GreaterThan(0f), "the ring must actually render, not just be present at zero alpha");
                Assert.That(chip.BorderColor.a, Is.Not.EqualTo(defaultBorderAlpha));
                Assert.That(chip.Label.fontStyle, Is.EqualTo(FontStyle.Bold), "label at full weight");
                Assert.That(chip.Label.fontStyle, Is.Not.EqualTo(defaultFontStyle));

                // Visibly different by something other than colour: the
                // swatch and label colour are untouched by the state change.
                Assert.That(chip.Swatch.color, Is.EqualTo(Color.green));
            }
            finally
            {
                Destroy(chip);
            }
        }

        [Test]
        public void Home_ReplacesThePadCountWithHomeExclamationPoint()
        {
            var chip = CreateChip();

            try
            {
                chip.SetFrog(Color.blue, "Blue");
                chip.SetPadCount("6 of 8");

                chip.SetState(PlayerChipState.Home);

                Assert.That(chip.PadCountText.text, Is.EqualTo("Home!"));
            }
            finally
            {
                Destroy(chip);
            }
        }

        [Test]
        public void Chip_ExposesNoTapHandler_AndEmitsNothingOnTap_InAnyState()
        {
            foreach (var state in new[] { PlayerChipState.Default, PlayerChipState.Active, PlayerChipState.Home })
            {
                var chip = CreateChip();

                try
                {
                    chip.SetState(state);

                    Assert.That(chip, Is.Not.InstanceOf<IPointerClickHandler>());
                    Assert.That(chip, Is.Not.InstanceOf<IPointerDownHandler>());
                    Assert.That(chip, Is.Not.InstanceOf<IPointerUpHandler>());

                    var events = typeof(PlayerChip).GetEvents();
                    Assert.That(events, Is.Empty, "the chip is a readout in v0.2 — it emits nothing, in any state");
                }
                finally
                {
                    Destroy(chip);
                }
            }
        }

        static PlayerChip CreateChip()
        {
            var host = new GameObject(nameof(PlayerChipTests), typeof(RectTransform));
            return host.AddComponent<PlayerChip>();
        }

        static void Destroy(PlayerChip chip)
        {
            if (chip != null)
            {
                Object.DestroyImmediate(chip.gameObject);
            }
        }
    }
}
