using System;
using Frogs.Core;
using UnityEngine;

namespace Frogs.Unity
{
    /// <summary>
    /// The one component in the Hello World scene: it says which build is
    /// running, having asked Core to read the version.
    ///
    /// It exists to prove the chain — Unity project, Core assembly, CI, APK —
    /// end to end. The evidence it produces is a line in the Android log, which
    /// is the only thing that can distinguish "the APK installed and ran" from
    /// "the APK installed".
    ///
    /// This is the thin shell the split describes: the engine hands it a string,
    /// Core decides what that string means, and nothing here knows what a
    /// version is. See docs/engineering/tech-stack.md.
    /// </summary>
    public sealed class HelloWorldProbe : MonoBehaviour
    {
        void Awake()
        {
            Debug.Log(Describe(Application.version));
        }

        /// <summary>
        /// The line this build writes to the log, from the version name the
        /// engine reports — "0.2.3-abc1234" for a debug build, "0.2.3" for a
        /// release.
        ///
        /// Static and total so an EditMode test can ask what a given build would
        /// say without a running player. It never throws: a build that cannot
        /// read its own version has a broken stamp, and an app that dies on
        /// launch is a worse way to report that than a log line saying so.
        /// </summary>
        public static string Describe(string applicationVersion)
        {
            try
            {
                var version = AppVersion.ReadFromBuildName(applicationVersion);

                return $"Multiplying Frogs {version} is running (build '{applicationVersion}').";
            }
            catch (Exception error) when (error is FormatException || error is ArgumentNullException)
            {
                return $"Multiplying Frogs is running, but the build stamp "
                    + $"'{applicationVersion}' is not a version Core can read: {error.Message}";
            }
        }
    }
}
