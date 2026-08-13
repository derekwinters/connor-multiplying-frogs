using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// One key of game setup's name keyboard — docs/specs/ui/game-setup.md#the-keyboard.
    ///
    /// Deliberately not the shared <see cref="Frogs.Unity.UI.Button"/>, for
    /// the same reason <see cref="WorkingOutKeypadKey"/> is not: that
    /// component is 112 px tall with a 320 px minimum width, and this page
    /// gives its keys their own named size. The one shared Button behaviour
    /// this borrows is the disabled one — a key that is refused "does nothing,
    /// and nothing explains itself".
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class NameKeyboardKey : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>This key was tapped. Never raised while <see cref="IsDisabled"/>.</summary>
        public event Action<NameKeyboardKey> Tapped;

        /// <summary>What the key does.</summary>
        public NameKeyKind Kind { get; private set; }

        /// <summary>The character a <see cref="NameKeyKind.Letter"/> or <see cref="NameKeyKind.Space"/> key types.</summary>
        public char Character { get; private set; }

        /// <summary>
        /// The key's outline — and its hit area. #288's lesson: without a
        /// raycast target of its own there is nothing raycastable under the
        /// key, the <c>GraphicRaycaster</c> never finds it, and the keyboard
        /// types nothing.
        /// </summary>
        public Image Border { get; private set; }

        /// <summary>The key's fill, inside its outline.</summary>
        public Image Fill { get; private set; }

        /// <summary>The key's label.</summary>
        public Text Label { get; private set; }

        /// <summary>Whether this key is refused. A disabled key emits nothing.</summary>
        public bool IsDisabled { get; private set; }

        /// <summary>The key's own <see cref="RectTransform"/>.</summary>
        public RectTransform RectTransform
        {
            get { return (RectTransform)transform; }
        }

        /// <summary>Records what this key is, as the view builds it.</summary>
        public void Describe(NameKeyKind kind, char character, Image border, Image fill, Text label)
        {
            Kind = kind;
            Character = character;
            Border = border;
            Fill = fill;
            Label = label;
        }

        /// <summary>Turns the key off, or back on.</summary>
        public void SetDisabled(bool disabled)
        {
            IsDisabled = disabled;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsDisabled)
            {
                return;
            }

            var handler = Tapped;
            if (handler != null)
            {
                handler(this);
            }
        }
    }
}
