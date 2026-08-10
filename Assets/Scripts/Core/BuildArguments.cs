using System;
using System.Collections.Generic;

namespace Frogs.Core
{
    /// <summary>
    /// What CI asked this build to be, read off Unity's command line.
    ///
    /// These values used to travel in environment variables. They never
    /// arrived: `game-ci/unity-builder` runs Unity inside a container and
    /// forwards only its own allow-list of variables into it, so an
    /// `env: FROGS_ANDROID_PROFILE` on the workflow step sat in the runner's
    /// environment where the Unity process never looked. Every reader treated
    /// the absence as "this build did not ask for a profile", and `v0.1.0`
    /// shipped its device and emulator APKs as one byte-identical file
    /// (issue #218).
    ///
    /// Unity's own command line does arrive — the action appends whatever is
    /// in its `customParameters` input to the `unity-editor` invocation
    /// verbatim. So that is the road these values take now.
    ///
    /// Lives in Core so the reading rules are covered by the fast NUnit suite.
    /// The editor shell asks Unity for the command line and hands it here; it
    /// does not pick arguments apart itself.
    ///
    /// See docs/engineering/ci-cd.md.
    /// </summary>
    public readonly struct BuildArguments
    {
        readonly string[] words;

        BuildArguments(string[] words)
        {
            this.words = words;
        }

        /// <summary>
        /// Wraps a command line — typically <c>Environment.GetCommandLineArgs()</c>.
        ///
        /// A null command line is no arguments rather than an error: it is what
        /// a caller with nothing to pass looks like, and there is nothing wrong
        /// with a build that asks for none of this.
        /// </summary>
        public static BuildArguments From(IEnumerable<string> commandLine)
        {
            if (commandLine == null)
            {
                return new BuildArguments(new string[0]);
            }

            var collected = new List<string>();

            foreach (var word in commandLine)
            {
                collected.Add(word ?? string.Empty);
            }

            return new BuildArguments(collected.ToArray());
        }

        /// <summary>
        /// The value passed after <paramref name="flag"/>, or empty if the flag
        /// was not passed at all.
        ///
        /// Empty means "not asked for", which is a real and normal answer — the
        /// editor's own Build button passes none of these. But a flag that *is*
        /// present and has no usable value throws, because that is the exact
        /// shape of the bug this type exists to prevent: something went wrong
        /// upstream, and reading it as "not asked for" hides it until the APK
        /// is on somebody's tablet.
        /// </summary>
        public string Value(string flag)
        {
            RequireFlag(flag);

            var arguments = words ?? new string[0];

            for (var index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], flag, StringComparison.Ordinal))
                {
                    continue;
                }

                var next = index + 1;

                if (next >= arguments.Length || IsFlag(arguments[next]))
                {
                    throw new ArgumentException(
                        $"'{flag}' was passed to the build with no value after it. It "
                        + "carries something the build cannot invent — the profile, the "
                        + "versionCode, the applicationId suffix — so a build that "
                        + "continued would quietly be the wrong build.",
                        nameof(flag));
                }

                return arguments[next];
            }

            return string.Empty;
        }

        static bool IsFlag(string word) => word.Length > 0 && word[0] == '-';

        static void RequireFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag) || !IsFlag(flag))
            {
                throw new ArgumentException(
                    $"'{flag}' is not a command-line flag. Without its leading dash it "
                    + "would never match, and a value that is always absent looks "
                    + "exactly like a value nobody passed.",
                    nameof(flag));
            }
        }
    }
}
