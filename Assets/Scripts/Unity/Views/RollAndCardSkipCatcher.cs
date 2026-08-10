using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Frogs.Unity.Views
{
    /// <summary>
    /// A tap, anywhere on the surface it is attached to —
    /// docs/specs/ui/roll-and-card.md: "the whole sequence can be skipped by
    /// tapping anywhere, which jumps straight to the settled state. A
    /// four-player game plays this animation forty times, and the fortieth
    /// time nobody wants to watch it."
    ///
    /// It exists as its own component, attached by
    /// <see cref="RollAndCardDialogView"/> to the shared Dialog's scrim and
    /// panel, rather than as a handler on the shared
    /// <see cref="Frogs.Unity.UI.DialogPanel"/> itself: that type states
    /// plainly that its scrim "carries no click handler of any kind", because
    /// a dialog that decides something has no tap-outside-to-dismiss. This is
    /// not a dismiss — it hurries an animation along and can do nothing else,
    /// which is why it can live over the top of a dialog that cannot be
    /// dismissed at all.
    ///
    /// A click that began on `Solve it` reaches this too, since the shared
    /// Button handles press and release rather than click and so does not
    /// consume one. That is harmless: skipping is idempotent, and skipping an
    /// already-settled dialog does nothing.
    /// </summary>
    public sealed class RollAndCardSkipCatcher : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>A tap landed. The only thing this component can say.</summary>
        public event Action Tapped;

        public void OnPointerClick(PointerEventData eventData)
        {
            var handler = Tapped;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
