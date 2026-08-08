using System;
using System.IO;
using System.Text.Json;
using Frogs.Core;
using NUnit.Framework;

namespace Frogs.Core.Tests
{
    /// <summary>
    /// /VERSION and release-please's manifest are two files holding the same
    /// number, and nothing in release-please notices when they stop agreeing:
    /// a missing or damaged x-release-please-version marker leaves /VERSION
    /// behind while the manifest advances, releases keep shipping, and the
    /// first symptom is a build reporting a version from three releases ago.
    ///
    /// This runs in the ordinary Core suite, in milliseconds, on every push.
    /// </summary>
    public sealed class VersionDriftTests
    {
        const string VersionFileName = "VERSION";
        const string ManifestPath = ".github/release-please/manifest.json";
        const string ManifestKey = ".";

        static string RepoRoot => FindRepoRoot();

        [Test]
        public void VersionFile_IsBareSemverOnceTheMarkerIsStripped()
        {
            var contents = File.ReadAllText(Path.Combine(RepoRoot, VersionFileName));

            Assert.That(
                () => AppVersion.ReadFrom(contents),
                Throws.Nothing,
                $"/VERSION should be `0.0.1 # x-release-please-version`, but is: {contents.Trim()}");
        }

        [Test]
        public void VersionFile_StillCarriesTheReleasePleaseMarker()
        {
            var contents = File.ReadAllText(Path.Combine(RepoRoot, VersionFileName));

            // Without the marker release-please stops rewriting the file, and
            // it stops silently — this is the drift's usual cause, so it is
            // worth failing on directly rather than only on the symptom.
            Assert.That(
                contents,
                Does.Contain("x-release-please-version"),
                "/VERSION has lost its marker, so release-please will no longer update it.");
        }

        [Test]
        public void VersionFile_AgreesWithTheReleasePleaseManifest()
        {
            var versionFile = AppVersion.ReadFrom(
                File.ReadAllText(Path.Combine(RepoRoot, VersionFileName)));
            var manifest = ReadManifestVersion();

            Assert.That(
                versionFile,
                Is.EqualTo(manifest),
                $"/VERSION says {versionFile} and {ManifestPath} says {manifest}. "
                + "One of them was hand-edited, or the marker in /VERSION is broken.");
        }

        static AppVersion ReadManifestVersion()
        {
            var path = Path.Combine(RepoRoot, ManifestPath);
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (!document.RootElement.TryGetProperty(ManifestKey, out var element))
            {
                Assert.Fail($"{ManifestPath} has no \"{ManifestKey}\" entry.");
            }

            return AppVersion.Parse(element.GetString());
        }

        /// <summary>
        /// Walks up from the test binary. The suite runs from
        /// Tests/Core/bin/&lt;config&gt;/&lt;tfm&gt;/, and hard-coding that many
        /// "../" is a test that breaks when someone changes the target
        /// framework.
        /// </summary>
        static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var looksLikeRoot =
                    File.Exists(Path.Combine(directory.FullName, VersionFileName))
                    && Directory.Exists(Path.Combine(directory.FullName, ".github"));

                if (looksLikeRoot)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                $"No repository root above {AppContext.BaseDirectory} — "
                + $"looked for a directory containing both {VersionFileName} and .github/.");
        }
    }
}
