using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// One key of the working-out grid's keypad — a
    /// <c>KeypadKeySize</c> square with a border and a label, and a tap.
    ///
    /// It is deliberately not the shared <see cref="Frogs.Unity.UI.Button"/>:
    /// that component is 112 px tall with a 320 px minimum width and a press
    /// offset, and docs/specs/ui/working-out-grid.md gives the keypad its own
    /// square key with its own named size. The one shared Button on this
    /// screen is `Check it`.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkingOutKeypadKey : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>This key was tapped.</summary>
        public event Action<WorkingOutKeypadKey> Tapped;

        /// <summary>What the key does.</summary>
        public KeypadKeyKind Kind { get; private set; }

        /// <summary>The digit this key types, or <c>-1</c> when it is not a digit key.</summary>
        public int Digit { get; private set; }

        /// <summary>The key's outline.</summary>
        public Image Border { get; private set; }

        /// <summary>The key's fill, inside its outline.</summary>
        public Image Fill { get; private set; }

        /// <summary>The key's label — a numeral, `⌫`, or `clear`.</summary>
        public Text Label { get; private set; }

        /// <summary>The key's own <see cref="RectTransform"/>.</summary>
        public RectTransform RectTransform
        {
            get { return (RectTransform)transform; }
        }

        /// <summary>Records what this key is, as the view builds it.</summary>
        public void Describe(KeypadKeyKind kind, int digit, Image border, Image fill, Text label)
        {
            Kind = kind;
            Digit = digit;
            Border = border;
            Fill = fill;
            Label = label;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var handler = Tapped;
            if (handler != null)
            {
                handler(this);
            }
        }
    }
}
