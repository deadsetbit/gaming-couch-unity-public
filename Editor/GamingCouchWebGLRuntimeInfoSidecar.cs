using System;
using System.IO;
using DSB.GC;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal enum GCWebGLRuntimeInfoSidecarWriteStatus
{
    Written,
    SkippedNonWebGLBuild,
    SkippedTemplate,
}

internal sealed class GCWebGLRuntimeInfoSidecarWriteResult
{
    internal readonly GCWebGLRuntimeInfoSidecarWriteStatus status;
    internal readonly string sidecarPath;

    internal GCWebGLRuntimeInfoSidecarWriteResult(
        GCWebGLRuntimeInfoSidecarWriteStatus status,
        string sidecarPath
    )
    {
        this.status = status;
        this.sidecarPath = sidecarPath;
    }

    internal bool WasWritten
    {
        get { return status == GCWebGLRuntimeInfoSidecarWriteStatus.Written; }
    }
}

internal static class GCWebGLBuildSidecarOutputPaths
{
    internal static string ResolveOutputRootPath(string buildOutputPath, string sidecarDescription)
    {
        if (string.IsNullOrWhiteSpace(buildOutputPath))
        {
            throw new InvalidOperationException(sidecarDescription + " output path is empty.");
        }

        string fullOutputPath;
        try
        {
            fullOutputPath = Path.GetFullPath(buildOutputPath);
        }
        catch (ArgumentException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, sidecarDescription, exception);
        }
        catch (NotSupportedException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, sidecarDescription, exception);
        }
        catch (PathTooLongException exception)
        {
            throw CreateInvalidOutputPathException(buildOutputPath, sidecarDescription, exception);
        }

        if (File.Exists(fullOutputPath))
        {
            var directoryName = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrEmpty(directoryName))
            {
                throw new InvalidOperationException(
                    sidecarDescription + " output path has no containing directory: " + buildOutputPath
                );
            }

            return directoryName;
        }

        return fullOutputPath;
    }

    internal static string ResolveSidecarPath(string outputRootPath, string sidecarFileName)
    {
        return Path.Combine(outputRootPath, sidecarFileName);
    }

    internal static string TryResolveSidecarPath(
        string buildOutputPath,
        string sidecarFileName,
        string sidecarDescription
    )
    {
        if (string.IsNullOrWhiteSpace(buildOutputPath))
        {
            return null;
        }

        try
        {
            return ResolveSidecarPath(
                ResolveOutputRootPath(buildOutputPath, sidecarDescription),
                sidecarFileName
            );
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static InvalidOperationException CreateInvalidOutputPathException(
        string buildOutputPath,
        string sidecarDescription,
        Exception exception
    )
    {
        return new InvalidOperationException(
            sidecarDescription + " output path is invalid: " + buildOutputPath,
            exception
        );
    }
}

internal static class GCWebGLBuildSidecarTemplatePolicy
{
    internal static bool ShouldWriteRuntimeInfo(string webGLTemplate)
    {
        return string.Equals(
            webGLTemplate,
            GamingCouchWebGLExportSetup.ProjectTemplateIdentifier,
            StringComparison.Ordinal
        );
    }

    internal static bool ShouldRemoveStaleRuntimeInfo(BuildTarget buildTarget, string webGLTemplate)
    {
        return buildTarget == BuildTarget.WebGL && !ShouldWriteRuntimeInfo(webGLTemplate);
    }

    internal static bool ShouldWriteUnityBuildInfo(BuildTarget buildTarget)
    {
        return buildTarget == BuildTarget.WebGL;
    }
}

internal static class GCWebGLRuntimeInfoSidecarWriter
{
    internal const string SidecarFileName = "gc.runtime-info.json";
    private const string OutputPathDescription = "WebGL runtime info sidecar";

    internal static GCWebGLRuntimeInfoSidecarWriteResult WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity
    )
    {
        if (buildTarget != BuildTarget.WebGL)
        {
            return new GCWebGLRuntimeInfoSidecarWriteResult(
                GCWebGLRuntimeInfoSidecarWriteStatus.SkippedNonWebGLBuild,
                TryResolveSidecarPath(buildOutputPath)
            );
        }

        if (GCWebGLBuildSidecarTemplatePolicy.ShouldRemoveStaleRuntimeInfo(buildTarget, webGLTemplate))
        {
            return SkipTemplateBuild(buildOutputPath);
        }

        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        var outputRootPath = GCWebGLBuildSidecarOutputPaths.ResolveOutputRootPath(
            buildOutputPath,
            OutputPathDescription
        );
        var sidecarPath = GCWebGLBuildSidecarOutputPaths.ResolveSidecarPath(
            outputRootPath,
            SidecarFileName
        );

        return WriteSidecarUnchecked(outputRootPath, sidecarPath, packageIdentity);
    }

    private static GCWebGLRuntimeInfoSidecarWriteResult SkipTemplateBuild(string buildOutputPath)
    {
        return SkipTemplateSidecar(TryResolveSidecarPath(buildOutputPath));
    }

    internal static GCWebGLRuntimeInfoSidecarWriteResult SkipTemplateSidecar(string sidecarPath)
    {
        if (!string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath))
        {
            File.Delete(sidecarPath);
        }

        return new GCWebGLRuntimeInfoSidecarWriteResult(
            GCWebGLRuntimeInfoSidecarWriteStatus.SkippedTemplate,
            sidecarPath
        );
    }

    internal static GCWebGLRuntimeInfoSidecarWriteResult WriteSidecar(
        string outputRootPath,
        string sidecarPath,
        GCPackageIdentity packageIdentity
    )
    {
        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        return WriteSidecarUnchecked(outputRootPath, sidecarPath, packageIdentity);
    }

    private static GCWebGLRuntimeInfoSidecarWriteResult WriteSidecarUnchecked(
        string outputRootPath,
        string sidecarPath,
        GCPackageIdentity packageIdentity
    )
    {
        Directory.CreateDirectory(outputRootPath);
        GCEditorAtomicFileWriter.WriteAllText(sidecarPath, GCRuntimeInfoJson.Serialize(packageIdentity.ToRuntimeInfo()));

        return new GCWebGLRuntimeInfoSidecarWriteResult(
            GCWebGLRuntimeInfoSidecarWriteStatus.Written,
            sidecarPath
        );
    }

    private static string TryResolveSidecarPath(string buildOutputPath)
    {
        return GCWebGLBuildSidecarOutputPaths.TryResolveSidecarPath(
            buildOutputPath,
            SidecarFileName,
            OutputPathDescription
        );
    }
}

public sealed class GamingCouchWebGLRuntimeInfoSidecarPostprocess : IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 0; }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        GCWebGLBuildSidecarPostprocessWriter.WriteForBuild(report, PlayerSettings.WebGL.template);
    }
}

internal static class GCWebGLBuildSidecarPostprocessWriter
{
    internal static void WriteForBuild(BuildReport report, string webGLTemplate)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var summary = report.summary;
        if (!GCWebGLBuildSidecarTemplatePolicy.ShouldWriteUnityBuildInfo(summary.platform))
        {
            return;
        }

        // The build has already completed, so no sidecar work may throw out of this postprocess
        // callback and fail an otherwise-successful build. The inner resolve-then-write seam guards
        // identity resolution and the sidecar write; this outer guard additionally covers the
        // output-path resolution and build-info capture that run before the seam is reached.
        try
        {
            var outputRootPath = GCWebGLBuildSidecarOutputPaths.ResolveOutputRootPath(
                summary.outputPath,
                "WebGL sidecar"
            );

            WriteForBuild(
                summary.platform,
                summary.outputPath,
                webGLTemplate,
                GCEditorPackageIdentity.Resolve,
                GCUnityBuildInfoBuildSummaryCapture.Capture(report, outputRootPath),
                GCUnityBuildInfoWebGLSettingsCapture.Capture(),
                Application.unityVersion,
                GCUnityBuildInfoCaptureClock.CaptureUtcNow()
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Gaming Couch WebGL build sidecar emission failed after the build completed; "
                    + "the build output is unaffected. "
                    + exception
            );
        }
    }

    // Sidecar emission runs after the build has already completed. Identity resolution or the
    // sidecar write throwing here must never turn an otherwise-successful build into a failed one,
    // so this resolve-then-write entry point swallows (and logs) any failure without rethrowing.
    // Identity resolution is injectable so the swallow can be exercised in isolation.
    internal static void WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate,
        Func<GCPackageIdentity> resolvePackageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        if (resolvePackageIdentity == null)
        {
            throw new ArgumentNullException(nameof(resolvePackageIdentity));
        }

        try
        {
            WriteForBuild(
                buildTarget,
                buildOutputPath,
                webGLTemplate,
                resolvePackageIdentity(),
                buildSummary,
                webGLSettings,
                unityVersion,
                capturedAtUtc
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Gaming Couch WebGL build sidecar emission failed after the build completed; "
                    + "the build output is unaffected. "
                    + exception
            );
        }
    }

    internal static void WriteForBuild(
        BuildTarget buildTarget,
        string buildOutputPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        if (!GCWebGLBuildSidecarTemplatePolicy.ShouldWriteUnityBuildInfo(buildTarget))
        {
            return;
        }

        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        var outputRootPath = GCWebGLBuildSidecarOutputPaths.ResolveOutputRootPath(
            buildOutputPath,
            "WebGL sidecar"
        );

        WriteResolvedWebGLSidecars(
            buildTarget,
            outputRootPath,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );
    }

    private static void WriteResolvedWebGLSidecars(
        BuildTarget buildTarget,
        string outputRootPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        var runtimeInfoSidecarPath = GCWebGLBuildSidecarOutputPaths.ResolveSidecarPath(
            outputRootPath,
            GCWebGLRuntimeInfoSidecarWriter.SidecarFileName
        );

        if (GCWebGLBuildSidecarTemplatePolicy.ShouldWriteRuntimeInfo(webGLTemplate))
        {
            GCWebGLRuntimeInfoSidecarWriter.WriteSidecar(
                outputRootPath,
                runtimeInfoSidecarPath,
                packageIdentity
            );
        }
        else if (GCWebGLBuildSidecarTemplatePolicy.ShouldRemoveStaleRuntimeInfo(buildTarget, webGLTemplate))
        {
            GCWebGLRuntimeInfoSidecarWriter.SkipTemplateSidecar(runtimeInfoSidecarPath);
        }

        GCUnityBuildInfoSidecarWriter.WriteSidecar(
            buildTarget,
            outputRootPath,
            GCWebGLBuildSidecarOutputPaths.ResolveSidecarPath(
                outputRootPath,
                GCUnityBuildInfoSidecarWriter.SidecarFileName
            ),
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );
    }
}
