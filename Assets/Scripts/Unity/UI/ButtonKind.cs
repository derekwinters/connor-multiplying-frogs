namespace Frogs.Unity.UI
{
    /// <summary>
    /// The three kinds the shared <see cref="Button"/> comes in. Per
    /// docs/specs/ui/shared-components.md#button: "It comes in three kinds,
    /// which differ in colour and weight but never in size or shape." A kind
    /// switch touches only the colours <see cref="Button"/> applies, never its
    /// geometry.
    /// </summary>
    public enum ButtonKind
    {
        /// <summary>Filled accent, light label. At most one visible at a time.</summary>
        Primary,

        /// <summary>Outlined, dark label, no fill.</summary>
        Secondary,

        /// <summary>Outlined in the warning colour, warning-coloured label.</summary>
        Destructive
    }
}
