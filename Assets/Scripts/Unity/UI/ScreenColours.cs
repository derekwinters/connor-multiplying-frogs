using UnityEngine;

namespace Frogs.Unity.UI
{
    /// <summary>
    /// The colours a whole screen is painted in, as opposed to the colours a
    /// single component is painted in.
    ///
    /// There is one of them today: the background every screen sits on. It is
    /// declared here rather than on each screen because two different things
    /// have to agree on it — every view's own background, and the scene
    /// camera's clear colour — and a hex value copied into seven places is a
    /// hex value that will be corrected in six.
    ///
    /// docs/specs/ui/shared-components.md#the-canvas-every-component-is-measured-in
    /// is where the rule lives; this is where the value does.
    /// </summary>
    public static class ScreenColours
    {
        /// <summary>
        /// The background every screen is painted on — the mockups' `--bg`,
        /// which every committed mockup sets on its 1920 x 1200 `.frame`.
        ///
        /// It reaches the edge of the screen on any aspect ratio, and the
        /// scene camera clears to the same value, so a frame drawn before any
        /// view has painted is this colour rather than whatever the engine
        /// would otherwise show.
        /// </summary>
        public static readonly Color Background = new Color32(0xED, 0xF1, 0xEF, 0xFF);
    }
}
