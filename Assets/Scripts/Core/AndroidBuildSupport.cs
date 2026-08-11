using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>
    /// Which Android architecture and scripting-backend pairings Unity can
    /// actually build, and whether the settings a build asked for took.
    ///
    /// **Unity has no Mono for 64-bit Android.** The Mono backend compiles at
    /// runtime, with a JIT, and that JIT is not supported on 64-bit Android —
    /// so `ARM64` and `x86_64` are IL2CPP-only (Unity Manual, "Mono scripting
    /// backend" and "IL2CPP Overview").
    ///
    /// Asking for the unsupported pairing anyway does not produce an error at
    /// the point of asking. Unity drops the architecture it cannot build,
    /// `PlayerSettings.Android.targetArchitectures` is left holding nothing,
    /// and the build dies much later, at the prerequisites check, saying
    /// *"Target architecture not specified"* — which names neither the
    /// architecture nor the backend that caused it. That is how the emulator
    /// profile (x86_64 + Mono) got as far as a release before anyone saw it:
    /// issue #282, release v0.2.0.
    ///
    /// So the pairing is checked here, in plain C# with no editor in sight, and
    /// <see cref="AppliedProblem"/> re-checks what the editor really ended up
    /// with. Strings rather than Unity's enums, because this assembly does not
    /// reference UnityEngine — the caller in `Assets/Editor` converts.
    ///
    /// See docs/engineering/tech-stack.md#two-build-profiles.
    /// </summary>
    public static class AndroidBuildSupport
    {
        /// <summary>Unity's name for the ahead-of-time backend.</summary>
        public const string Il2Cpp = "IL2CPP";

        /// <summary>
        /// The 64-bit Android architectures, spelled as Unity's
        /// `AndroidArchitecture` members are.
        /// </summary>
        static readonly string[] SixtyFourBit = { "ARM64", "X86_64" };

        /// <summary>
        /// Whether this architecture can only be built with IL2CPP.
        /// </summary>
        public static bool RequiresIl2Cpp(string architecture) =>
            IndexOf(SixtyFourBit, Clean(architecture)) >= 0;

        /// <summary>
        /// What is wrong with building <paramref name="architectures"/> with
        /// <paramref name="scriptingBackend"/>, or null if nothing is.
        ///
        /// <paramref name="architectures"/> may name several, comma-separated,
        /// the way a flags enum prints itself: "ARM64, X86_64".
        /// </summary>
        public static string PairingProblem(string architectures, string scriptingBackend)
        {
            if (IsIl2Cpp(scriptingBackend))
            {
                return null;
            }

            var unsupported = new List<string>();

            foreach (var architecture in Split(architectures))
            {
                if (RequiresIl2Cpp(architecture))
                {
                    unsupported.Add(architecture);
                }
            }

            if (unsupported.Count == 0)
            {
                return null;
            }

            return $"{string.Join(", ", unsupported.ToArray())} is 64-bit Android, "
                + $"which Unity builds with {Il2Cpp} only — the "
                + $"{Clean(scriptingBackend)} backend has no 64-bit JIT. Unity "
                + "does not refuse this pairing; it drops the architecture and "
                + "then fails the build with \"Target architecture not "
                + "specified\". See issue #282.";
        }

        /// <summary>
        /// What is wrong with the settings a build ended up with, or null.
        ///
        /// Called with what the editor reports *after* the profile was applied,
        /// not with what was asked for. An empty
        /// <paramref name="architectures"/> means Unity kept none of them,
        /// which is the failure this whole type exists to name.
        /// </summary>
        public static string AppliedProblem(
            string profile, string architectures, string scriptingBackend)
        {
            if (!string.IsNullOrWhiteSpace(architectures))
            {
                return PairingProblem(architectures, scriptingBackend);
            }

            return $"The '{Clean(profile)}' Android build profile left no target "
                + $"architecture set at all, with the {Clean(scriptingBackend)} "
                + "scripting backend. Unity silently drops an architecture that "
                + "backend cannot build, so an empty set here means the profile "
                + "asked for something impossible. See issue #282.";
        }

        static bool IsIl2Cpp(string scriptingBackend) =>
            string.Equals(Clean(scriptingBackend), Il2Cpp, StringComparison.OrdinalIgnoreCase);

        static IEnumerable<string> Split(string architectures)
        {
            foreach (var part in (architectures ?? string.Empty).Split(','))
            {
                var cleaned = Clean(part);

                if (cleaned.Length > 0)
                {
                    yield return cleaned;
                }
            }
        }

        static int IndexOf(string[] names, string value)
        {
            for (var index = 0; index < names.Length; index++)
            {
                if (string.Equals(names[index], value, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        static string Clean(string value) => (value ?? string.Empty).Trim();
    }
}
