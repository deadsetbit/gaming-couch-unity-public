using System.IO;
using NUnit.Framework;

public sealed class GCUnityBuildInfoPathNormalizerRelativePathTests
{
    // Roots that no real working directory can live under, so a CWD-anchored resolution is
    // guaranteed to land outside every root regardless of where the tests run. Anchored on the
    // current directory's own root so the paths stay absolute on every platform.
    private static readonly string UnreachableRoot =
        Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), "gc-nonexistent");
    private static readonly string UnreachableProjectRoot = Path.Combine(UnreachableRoot, "project");
    private static readonly string UnreachableBuildOutputRoot = Path.Combine(UnreachableProjectRoot, "BuildOutput");
    private static readonly string UnreachableUserHome = Path.Combine(UnreachableRoot, "home");

    [Test]
    public void RelativePathUnderBuildOutputRootIsClassifiedBuildOutputRelative()
    {
        // Path.GetFullPath anchors relative inputs to the current working directory, so we
        // derive the build output root from the actual CWD to keep this test CWD-independent:
        // the canonicalized path is guaranteed to sit under the root wherever it runs.
        var currentDirectory = Directory.GetCurrentDirectory();
        var context = new GCUnityBuildInfoPathNormalizationContext(
            UnreachableProjectRoot,
            currentDirectory,
            UnreachableUserHome
        );

        var normalized = GCUnityBuildInfoPathNormalizer.Normalize("Build/game.wasm", context);

        // Pre-fix: relative input fails IsAbsolutePath, matches no root, and returns
        // Relative + raw "Build/game.wasm". Post-fix: canonicalizes to <cwd>/Build/game.wasm,
        // resolves under the build output root, and returns the sub-path.
        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative));
        Assert.That(normalized.value, Is.EqualTo("Build/game.wasm"));
        Assert.That(normalized.WasRedacted, Is.False);
    }

    [Test]
    public void RelativePathEqualToBuildOutputRootCollapsesToDot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var context = new GCUnityBuildInfoPathNormalizationContext(
            UnreachableProjectRoot,
            currentDirectory,
            UnreachableUserHome
        );

        var normalized = GCUnityBuildInfoPathNormalizer.Normalize(".", context);

        // Pre-fix: "." is Relative + raw ".". Post-fix: "." canonicalizes to the CWD which
        // equals the build output root, collapsing to ".".
        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative));
        Assert.That(normalized.value, Is.EqualTo("."));
        Assert.That(normalized.WasRedacted, Is.False);
    }

    [Test]
    public void RelativePathOutsideAllRootsIsRedactedNotEmittedRaw()
    {
        var context = new GCUnityBuildInfoPathNormalizationContext(
            UnreachableProjectRoot,
            UnreachableBuildOutputRoot,
            UnreachableUserHome
        );

        var normalized = GCUnityBuildInfoPathNormalizer.Normalize("some/deep/output/index.html", context);

        // Pre-fix: Relative + raw "some/deep/output/index.html" with outputPathRedacted=false,
        // which would leak the raw value. Post-fix: canonicalizes to an absolute CWD-anchored
        // path that matches no root, so it is redacted (no absolute segment leaks).
        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute));
        Assert.That(normalized.WasRedacted, Is.True);
        Assert.That(normalized.value, Is.Null);
        Assert.That(
            normalized.redactionReason,
            Is.EqualTo(GCUnityBuildInfoPathNormalizer.UnknownAbsolutePathRedactionReason)
        );
    }

    [Test]
    public void MalformedRelativePathDoesNotThrowAndFallsBackToRelative()
    {
        var context = new GCUnityBuildInfoPathNormalizationContext(
            UnreachableProjectRoot,
            UnreachableBuildOutputRoot,
            UnreachableUserHome
        );

        // An embedded null character makes Path.GetFullPath throw; the try/catch must swallow it
        // and fall back to the existing Relative handling instead of propagating out of Normalize.
        var malformedPath = "bad\0path";

        GCUnityBuildInfoNormalizedPath normalized = null;
        Assert.DoesNotThrow(
            () => normalized = GCUnityBuildInfoPathNormalizer.Normalize(malformedPath, context)
        );
        Assert.That(normalized.kind, Is.EqualTo(GCUnityBuildInfoNormalizedPathKind.Relative));
    }
}
