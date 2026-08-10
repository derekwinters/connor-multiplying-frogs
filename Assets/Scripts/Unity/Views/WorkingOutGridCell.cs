using System;
using Frogs.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// One drawn cell of the working-out grid — the rectangle, the digit in
    /// it, and a tap.
    ///
    /// It knows which <see cref="GridRowKind"/> it was drawn for and which
    /// <see cref="GridCellKind"/> Core reported for it, and it decides
    /// nothing from either: <see cref="WorkingOutGridView"/> derives the whole
    /// grid's shape from Core's model and stamps the answers onto each cell as
    /// it builds them. The cell carries them so a tap can be answered — "tapping
    /// any cell moves the caret there" applies to editable cells, and a printed
    /// digit or an empty operator column is not one.
    ///
    /// Nothing here is ever marked right or wrong
    /// (docs/adr/0002-structured-working-out-grid.md): this type has no notion
    /// of a correct value, no second colour for one, and no member that names
    /// one.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkingOutGridCell : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>This cell was tapped. Whether that moves the caret is the view's call, not this cell's.</summary>
        public event Action<WorkingOutGridCell> Tapped;

        /// <summary>The kind of row this cell was drawn in, as Core reported it.</summary>
        public GridRowKind RowKind { get; private set; }

        /// <summary>The kind of cell Core reported at this position.</summary>
        public GridCellKind Kind { get; private set; }

        /// <summary>Which column this is, column zero being the operator column.</summary>
        public int Column { get; private set; }

        /// <summary>
        /// Which row of its own kind this is — the second carry strip is
        /// <c>1</c>, the fourth addition row <c>3</c>. Stable while the
        /// addition section grows and shrinks underneath it, which the display
        /// row index is not.
        /// </summary>
        public int RowOrdinal { get; private set; }

        /// <summary>The cell's outline, or null where the drawing has none — a printed digit and the operator column are both unboxed.</summary>
        public Image Border { get; private set; }

        /// <summary>The cell's fill, inside its outline, or null where there is no box.</summary>
        public Image Fill { get; private set; }

        /// <summary>The text drawn in the cell — a printed digit, an operator glyph, a typed digit, or empty.</summary>
        public Text Label { get; private set; }

        /// <summary>The cell's own <see cref="RectTransform"/>.</summary>
        public RectTransform RectTransform
        {
            get { return (RectTransform)transform; }
        }

        /// <summary>
        /// Whether the player may put a digit here. True of the addition
        /// rows, the answer row and the carry boxes; false of the card's own
        /// printed digits, the blank leading columns and the operator column.
        /// </summary>
        public bool IsEditable
        {
            get { return Kind == GridCellKind.Editable || Kind == GridCellKind.CarryBox; }
        }

        /// <summary>What is currently drawn in the cell, or the empty string.</summary>
        public string Content
        {
            get { return Label == null ? string.Empty : Label.text; }
        }

        /// <summary>
        /// Records what this cell is, as the view builds it. Called once, by
        /// the view, immediately after the cell's objects exist.
        /// </summary>
        public void Describe(
            GridRowKind rowKind,
            int rowOrdinal,
            GridCellKind kind,
            int column,
            Image border,
            Image fill,
            Text label)
        {
            RowKind = rowKind;
            RowOrdinal = rowOrdinal;
            Kind = kind;
            Column = column;
            Border = border;
            Fill = fill;
            Label = label;
        }

        /// <summary>Sets the text drawn in the cell — a digit, a glyph, or nothing.</summary>
        public void SetText(string text)
        {
            if (Label != null)
            {
                Label.text = text;
            }
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
