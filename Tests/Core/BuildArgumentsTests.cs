using System;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// Reading what CI asked this build to be, off Unity's command line.
    ///
    /// This used to be read from environment variables, and CI used to set
    /// them — on the runner. `game-ci/unity-builder` runs Unity in a container
    /// and forwards only its own allow-list of variables into it, so none of
    /// them ever arrived, and every reader treated "absent" as "not asked for".
    /// Two release profiles silently produced one APK (issue #218).
    ///
    /// The values now travel on Unity's own command line, so these are the
    /// rules for reading them — in Core, where the fast suite covers them.
    /// </summary>
    public sealed class BuildArgumentsTests
    {
        static BuildArguments Of(params string[] commandLine) =>
            BuildArguments.From(commandLine);

        [Test]
        public void Value_ReadsTheWordAfterTheFlag()
        {
            var arguments = Of("-frogsAndroidProfile", "emulator");

            Assert.That(arguments.Value("-frogsAndroidProfile"), Is.EqualTo("emulator"));
        }

        [Test]
        public void Value_FindsAFlagAmongUnityAndTheBuildersOwnArguments()
        {
            // What the command line actually looks like: unity-builder puts a
            // dozen of its own arguments in front of ours.
            var arguments = Of(
                "unity-editor", "-batchmode", "-quit",
                "-projectPath", "/github/workspace",
                "-executeMethod", "UnityBuilderAction.Builder.BuildProject",
                "-frogsVersionCode", "412",
                "-frogsAndroidProfile", "device");

            Assert.That(arguments.Value("-frogsVersionCode"), Is.EqualTo("412"));
            Assert.That(arguments.Value("-frogsAndroidProfile"), Is.EqualTo("device"));
        }

        [Test]
        public void Value_IsEmptyWhenTheFlagWasNotPassed()
        {
            // Not an error: a build started from the editor's own Build button
            // passes none of these, and must keep working.
            Assert.That(Of("-batchmode").Value("-frogsAndroidProfile"), Is.Empty);
        }

        [Test]
        public void Value_IsEmptyForAnEmptyCommandLine()
        {
            Assert.That(Of().Value("-frogsAndroidProfile"), Is.Empty);
        }

        [Test]
        public void Value_MatchesTheWholeFlagRatherThanAPrefix()
        {
            // `-frogsVersionCode` must not answer for `-frogsVersion`, or a
            // renamed flag reads the wrong value instead of reading none.
            var arguments = Of("-frogsVersionCodeExtra", "nonsense");

            Assert.That(arguments.Value("-frogsVersionCode"), Is.Empty);
        }

        [Test]
        public void Value_TakesTheFirstOfARepeatedFlag()
        {
            var arguments = Of("-frogsAndroidProfile", "device",
                               "-frogsAndroidProfile", "emulator");

            Assert.That(arguments.Value("-frogsAndroidProfile"), Is.EqualTo("device"));
        }

        // A flag with nothing usable after it is the failure this whole change
        // is about: silently reading it as "absent" is what shipped one APK
        // twice. Fail loudly instead, while the build log is still open.
        [Test]
        public void Value_RejectsAFlagAtTheEndOfTheCommandLine()
        {
            Assert.That(
                () => Of("-batchmode", "-frogsAndroidProfile").Value("-frogsAndroidProfile"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Value_RejectsAFlagWhoseValueIsAnotherFlag()
        {
            Assert.That(
                () => Of("-frogsAndroidProfile", "-frogsVersionCode", "412")
                    .Value("-frogsAndroidProfile"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void From_TreatsANullCommandLineAsNoArguments()
        {
            Assert.That(BuildArguments.From(null).Value("-frogsAndroidProfile"), Is.Empty);
        }

        [Test]
        public void Value_RejectsAFlagThatIsNotAFlag()
        {
            // Asking for "frogsAndroidProfile" without the dash would never
            // match, and would look like the value simply was not passed.
            Assert.That(
                () => Of("-frogsAndroidProfile", "device").Value("frogsAndroidProfile"),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
