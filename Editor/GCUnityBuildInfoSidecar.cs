using System;
using System.Globalization;
using System.IO;
using DSB.GC;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal enum GCUnityBuildInfoSidecarWriteStatus
{
    Written,
    SkippedNonWebGLBuild,
}

internal sealed class GCUnityBuildInfoSidecarWriteResult
{
    internal readonly GCUnityBuildInfoSidecarWriteStatus status;
    internal readonly string sidecarPath;

    internal GCUnityBuildInfoSidecarWriteResult(
        GCUnityBuildInfoSidecarWriteStatus status,
        string sidecarPath
    )
    {
        this.status = status;
        this.sidecarPath = sidecarPath;
    }

    internal bool WasWritten
    {
        get { return status == GCUnityBuildInfoSidecarWriteStatus.Written; }
    }
}

internal static class GCUnityBuildInfoSidecarWriter
{
    internal const string SidecarFileName = "gc.unity-build-info.json";
    private const string OutputPathDescription = "Unity build info sidecar";

    internal static GCUnityBuildInfoSidecarWriteResult WriteForBuild(
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
        if (buildTarget != BuildTarget.WebGL)
        {
            return new GCUnityBuildInfoSidecarWriteResult(
                GCUnityBuildInfoSidecarWriteStatus.SkippedNonWebGLBuild,
                TryResolveSidecarPath(buildOutputPath)
            );
        }

        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        if (buildSummary == null)
        {
            throw new ArgumentNullException(nameof(buildSummary));
        }

        if (webGLSettings == null)
        {
            throw new ArgumentNullException(nameof(webGLSettings));
        }

        var outputRootPath = ResolveOutputRootPath(buildOutputPath);
        var sidecarPath = GCWebGLBuildSidecarOutputPaths.ResolveSidecarPath(outputRootPath, SidecarFileName);

        return WriteSidecarUnchecked(
            buildTarget,
            outputRootPath,
            sidecarPath,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );
    }

    internal static GCUnityBuildInfoSidecarWriteResult WriteSidecar(
        BuildTarget buildTarget,
        string outputRootPath,
        string sidecarPath,
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
            return new GCUnityBuildInfoSidecarWriteResult(
                GCUnityBuildInfoSidecarWriteStatus.SkippedNonWebGLBuild,
                sidecarPath
            );
        }

        if (packageIdentity == null)
        {
            throw new ArgumentNullException(nameof(packageIdentity));
        }

        if (buildSummary == null)
        {
            throw new ArgumentNullException(nameof(buildSummary));
        }

        if (webGLSettings == null)
        {
            throw new ArgumentNullException(nameof(webGLSettings));
        }

        return WriteSidecarUnchecked(
            buildTarget,
            outputRootPath,
            sidecarPath,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );
    }

    private static GCUnityBuildInfoSidecarWriteResult WriteSidecarUnchecked(
        BuildTarget buildTarget,
        string outputRootPath,
        string sidecarPath,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        var sidecar = GCUnityBuildInfoSidecarFactory.Create(
            buildTarget,
            webGLTemplate,
            packageIdentity,
            buildSummary,
            webGLSettings,
            unityVersion,
            capturedAtUtc
        );

        Directory.CreateDirectory(outputRootPath);
        GCEditorAtomicFileWriter.WriteAllText(sidecarPath, JsonUtility.ToJson(sidecar, true));

        return new GCUnityBuildInfoSidecarWriteResult(
            GCUnityBuildInfoSidecarWriteStatus.Written,
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

    internal static string ResolveOutputRootPath(string buildOutputPath)
    {
        return GCWebGLBuildSidecarOutputPaths.ResolveOutputRootPath(
            buildOutputPath,
            OutputPathDescription
        );
    }
}

internal static class GCUnityBuildInfoSidecarFactory
{
    internal const int SchemaVersion = 1;
    private const string Generator = "dsb.gamingcouch.unity";

    internal static GCUnityBuildInfoSidecar Create(
        BuildTarget buildTarget,
        string webGLTemplate,
        GCPackageIdentity packageIdentity,
        GCUnityBuildInfoBuildSummary buildSummary,
        GCUnityBuildInfoWebGLSettings webGLSettings,
        string unityVersion,
        string capturedAtUtc
    )
    {
        return new GCUnityBuildInfoSidecar
        {
            schemaVersion = SchemaVersion,
            generator = Generator,
            identity = new GCUnityBuildInfoIdentityMetadata
            {
                platform = packageIdentity.platform,
                packageName = packageIdentity.packageName,
                packageVersion = packageIdentity.packageVersion,
                gameProtocolVersion = packageIdentity.gameProtocolVersion,
            },
            buildEnvironment = new GCUnityBuildInfoBuildEnvironment
            {
                unityEditorVersion = string.IsNullOrWhiteSpace(unityVersion) ? Application.unityVersion : unityVersion,
                target = buildTarget.ToString(),
                targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget).ToString(),
                webGL = new GCUnityBuildInfoWebGLEnvironment
                {
                    template = webGLTemplate,
                    settings = webGLSettings,
                },
                host = GCUnityBuildInfoHostDiagnosticsCapture.Capture(),
            },
            buildResult = CreateBuildResult(buildSummary, capturedAtUtc),
        };
    }

    private static GCUnityBuildInfoBuildSummary CreateBuildResult(
        GCUnityBuildInfoBuildSummary buildSummary,
        string capturedAtUtc
    )
    {
        return new GCUnityBuildInfoBuildSummary
        {
            capturedAtUtc = string.IsNullOrWhiteSpace(capturedAtUtc)
                ? GCUnityBuildInfoCaptureClock.CaptureUtcNow()
                : capturedAtUtc,
            result = buildSummary.result,
            totalSizeBytes = buildSummary.totalSizeBytes,
            totalTimeSeconds = buildSummary.totalTimeSeconds,
            totalWarnings = buildSummary.totalWarnings,
            totalErrors = buildSummary.totalErrors,
            guid = buildSummary.guid,
            outputPath = buildSummary.outputPath,
            outputPathKind = buildSummary.outputPathKind,
            outputPathRedacted = buildSummary.outputPathRedacted,
            outputPathRedactionReason = buildSummary.outputPathRedactionReason,
        };
    }
}

internal static class GCUnityBuildInfoCaptureClock
{
    internal static string CaptureUtcNow()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}

internal static class GCUnityBuildInfoBuildSummaryCapture
{
    internal static GCUnityBuildInfoBuildSummary Capture(BuildReport report, string buildOutputRootPath)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var summary = report.summary;
        return Create(
            summary.result.ToString(),
            summary.totalSize,
            summary.totalTime,
            summary.totalWarnings,
            summary.totalErrors,
            summary.guid.ToString(),
            summary.outputPath,
            new GCUnityBuildInfoPathNormalizationContext(
                ResolveProjectRootPath(),
                buildOutputRootPath,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            )
        );
    }

    internal static GCUnityBuildInfoBuildSummary Create(
        string result,
        ulong totalSizeBytes,
        TimeSpan totalTime,
        int totalWarnings,
        int totalErrors,
        string guid,
        string outputPath,
        GCUnityBuildInfoPathNormalizationContext pathContext
    )
    {
        var normalizedOutputPath = GCUnityBuildInfoPathNormalizer.Normalize(outputPath, pathContext);

        return new GCUnityBuildInfoBuildSummary
        {
            result = result,
            totalSizeBytes = ClampToInt64(totalSizeBytes),
            totalTimeSeconds = Math.Round(totalTime.TotalSeconds, 3),
            totalWarnings = totalWarnings,
            totalErrors = totalErrors,
            guid = guid,
            outputPath = normalizedOutputPath.ShouldEmitValue ? normalizedOutputPath.value : null,
            outputPathKind = FormatPathKind(normalizedOutputPath.kind),
            outputPathRedacted = normalizedOutputPath.WasRedacted,
            outputPathRedactionReason = normalizedOutputPath.redactionReason,
        };
    }

    private static long ClampToInt64(ulong value)
    {
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }

    private static string ResolveProjectRootPath()
    {
        if (string.IsNullOrWhiteSpace(Application.dataPath))
        {
            return null;
        }

        var parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : null;
    }

    private static string FormatPathKind(GCUnityBuildInfoNormalizedPathKind kind)
    {
        switch (kind)
        {
            case GCUnityBuildInfoNormalizedPathKind.Empty:
                return "empty";
            case GCUnityBuildInfoNormalizedPathKind.Relative:
                return "relative";
            case GCUnityBuildInfoNormalizedPathKind.ProjectRelative:
                return "projectRelative";
            case GCUnityBuildInfoNormalizedPathKind.BuildOutputRelative:
                return "buildOutputRelative";
            case GCUnityBuildInfoNormalizedPathKind.UserHomeRelative:
                return "userHomeRelative";
            case GCUnityBuildInfoNormalizedPathKind.UnknownAbsolute:
                return "unknownAbsolute";
            default:
                return "unknown";
        }
    }
}

internal static class GCUnityBuildInfoWebGLSettingsCapture
{
    internal static GCUnityBuildInfoWebGLSettings Capture()
    {
        return new GCUnityBuildInfoWebGLSettings
        {
            il2CppCodeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL).ToString(),
            managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL).ToString(),
            stripUnusedMeshComponents = PlayerSettings.stripUnusedMeshComponents,
            dataCaching = PlayerSettings.WebGL.dataCaching,
            compressionFormat = PlayerSettings.WebGL.compressionFormat.ToString(),
            exceptionSupport = PlayerSettings.WebGL.exceptionSupport.ToString(),
            debugSymbolMode = PlayerSettings.WebGL.debugSymbolMode.ToString(),
#if UNITY_2023_1_OR_NEWER
            webAssembly2023 = PlayerSettings.WebGL.wasm2023,
#endif
            developmentBuild = EditorUserBuildSettings.development,
            codeOptimization = GCWebGLBuildSupport.GetCodeOptimizationString() ?? "(WebGL module not installed)",
        };
    }
}

internal static class GCUnityBuildInfoHostDiagnosticsCapture
{
    internal static GCUnityBuildInfoHostDiagnostics Capture()
    {
        return new GCUnityBuildInfoHostDiagnostics
        {
            editorPlatform = Application.platform.ToString(),
            osFamily = SystemInfo.operatingSystemFamily.ToString(),
            operatingSystem = ResolveOperatingSystem(),
        };
    }

    private static string ResolveOperatingSystem()
    {
        if (!string.IsNullOrWhiteSpace(SystemInfo.operatingSystem))
        {
            return SystemInfo.operatingSystem;
        }

        return Environment.OSVersion.ToString();
    }
}

[Serializable]
internal sealed class GCUnityBuildInfoSidecar
{
    public int schemaVersion;
    public string generator;
    public GCUnityBuildInfoIdentityMetadata identity;
    public GCUnityBuildInfoBuildEnvironment buildEnvironment;
    public GCUnityBuildInfoBuildSummary buildResult;
}

[Serializable]
internal sealed class GCUnityBuildInfoIdentityMetadata
{
    public string platform;
    public string packageName;
    public string packageVersion;
    public int gameProtocolVersion;
}

[Serializable]
internal sealed class GCUnityBuildInfoBuildEnvironment
{
    public string unityEditorVersion;
    public string target;
    public string targetGroup;
    public GCUnityBuildInfoWebGLEnvironment webGL;
    public GCUnityBuildInfoHostDiagnostics host;
}

[Serializable]
internal sealed class GCUnityBuildInfoWebGLEnvironment
{
    public string template;
    public GCUnityBuildInfoWebGLSettings settings;
}

[Serializable]
internal sealed class GCUnityBuildInfoHostDiagnostics
{
    public string editorPlatform;
    public string osFamily;
    public string operatingSystem;
}

[Serializable]
internal sealed class GCUnityBuildInfoBuildSummary
{
    public string capturedAtUtc;
    public string result;
    public long totalSizeBytes;
    public double totalTimeSeconds;
    public int totalWarnings;
    public int totalErrors;
    public string guid;
    public string outputPath;
    public string outputPathKind;
    public bool outputPathRedacted;
    public string outputPathRedactionReason;
}

[Serializable]
internal sealed class GCUnityBuildInfoWebGLSettings
{
    public string il2CppCodeGeneration;
    public string managedStrippingLevel;
    public bool stripUnusedMeshComponents;
    public bool dataCaching;
    public string compressionFormat;
    public string exceptionSupport;
    public string debugSymbolMode;
#if UNITY_2023_1_OR_NEWER
    public bool webAssembly2023;
#endif
    public bool developmentBuild;
    public string codeOptimization;
}
