using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;

internal enum GCWebGLPreviewRowKind
{
    Setting,
    Template,
    BuildTarget,
    Splash,
}

internal sealed class GCWebGLPreviewRow
{
    internal readonly string id;
    internal readonly GCWebGLPreviewRowKind kind;
    internal readonly string label;
    internal readonly string currentValue;
    internal readonly string targetValue;
    internal readonly bool isChanged;
    internal readonly bool isSkippable;
    internal readonly bool isBlocked;

    internal GCWebGLPreviewRow(
        string id,
        GCWebGLPreviewRowKind kind,
        string label,
        string currentValue,
        string targetValue,
        bool isChanged,
        bool isSkippable,
        bool isBlocked = false
    )
    {
        this.id = id;
        this.kind = kind;
        this.label = label;
        this.currentValue = string.IsNullOrEmpty(currentValue) ? "(none)" : currentValue;
        this.targetValue = string.IsNullOrEmpty(targetValue) ? "(none)" : targetValue;
        this.isChanged = isChanged || isBlocked;
        this.isSkippable = isSkippable && !isBlocked;
        this.isBlocked = isBlocked;
    }

    internal string DiffText
    {
        get { return label + ": " + currentValue + " -> " + targetValue; }
    }
}

internal static class GCWebGLPreviewRowQueries
{
    internal static bool HasChangedRows(IReadOnlyList<GCWebGLPreviewRow> rows)
    {
        for (var index = 0; rows != null && index < rows.Count; index++)
        {
            if (rows[index] != null && rows[index].isChanged)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasBlockedRows(IReadOnlyList<GCWebGLPreviewRow> rows)
    {
        for (var index = 0; rows != null && index < rows.Count; index++)
        {
            if (rows[index] != null && rows[index].isBlocked)
            {
                return true;
            }
        }

        return false;
    }

    internal static string[] GetDefaultSelectedSkippableRowIds(IReadOnlyList<GCWebGLPreviewRow> rows)
    {
        var selectedIds = new List<string>();
        for (var index = 0; rows != null && index < rows.Count; index++)
        {
            if (rows[index] != null &&
                rows[index].isChanged &&
                rows[index].isSkippable)
            {
                selectedIds.Add(rows[index].id);
            }
        }

        return selectedIds.ToArray();
    }
}

internal enum GCWebGLBuildSettingsProfileId
{
    Dev,
    Release,
}

internal sealed class GCWebGLBuildSettingsProfilePlan
{
    internal readonly GCWebGLBuildSettingsProfileId profileId;
    internal readonly string displayName;
    internal readonly GCWebGLPreviewRow[] rows;

    internal GCWebGLBuildSettingsProfilePlan(
        GCWebGLBuildSettingsProfileId profileId,
        string displayName,
        GCWebGLPreviewRow[] rows
    )
    {
        this.profileId = profileId;
        this.displayName = displayName;
        this.rows = rows ?? new GCWebGLPreviewRow[0];
    }

    internal bool HasChanges
    {
        get { return GCWebGLPreviewRowQueries.HasChangedRows(rows); }
    }
}

internal sealed class GCWebGLBuildSettingsProfileResult
{
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCWebGLBuildSettingsProfileResult(bool changed, string message, string[] details)
    {
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
    }
}

internal static class GamingCouchWebGLBuildSettingsProfiles
{
    internal const string Il2CppCodeGenerationSettingId = "il2cpp-code-generation";
    internal const string ManagedStrippingLevelSettingId = "managed-stripping-level";
    internal const string StripUnusedMeshComponentsSettingId = "strip-unused-mesh-components";
    internal const string WebGLDataCachingSettingId = "webgl-data-caching";
    internal const string WebGLCompressionSettingId = "webgl-compression";
    internal const string WebGLExceptionSupportSettingId = "webgl-exception-support";
    internal const string WebGLDebugSymbolsSettingId = "webgl-debug-symbols";
    internal const string WebAssembly2023SettingId = "webassembly-2023";
    internal const string DevelopmentBuildSettingId = "development-build";
    internal const string WebGLCodeOptimizationSettingId = "webgl-code-optimization";

    private static readonly GCWebGLBuildSettingSpec[] ReleaseProfileSpecs = CreateReleaseProfileSpecs();
    private static readonly GCWebGLBuildSettingSpec[] DevProfileSpecs = CreateDevProfileSpecs();

    internal static GCWebGLBuildSettingsProfilePlan BuildReleaseProfilePlan()
    {
        return BuildProfilePlan(GCWebGLBuildSettingsProfileId.Release);
    }

    internal static GCWebGLBuildSettingsProfilePlan BuildDevProfilePlan()
    {
        return BuildProfilePlan(GCWebGLBuildSettingsProfileId.Dev);
    }

    internal static GCWebGLBuildSettingsProfileResult ApplyReleaseProfile()
    {
        return ApplyProfile(GCWebGLBuildSettingsProfileId.Release, null);
    }

    internal static GCWebGLBuildSettingsProfileResult ApplyReleaseProfile(IEnumerable<string> selectedSettingIds)
    {
        return ApplyProfile(GCWebGLBuildSettingsProfileId.Release, selectedSettingIds);
    }

    internal static bool ApplyReleaseProfile(List<string> details, IEnumerable<string> selectedSettingIds)
    {
        return ApplyProfile(GCWebGLBuildSettingsProfileId.Release, details, selectedSettingIds);
    }

    internal static GCWebGLBuildSettingsProfileResult ApplyDevProfile(IEnumerable<string> selectedSettingIds)
    {
        return ApplyProfile(GCWebGLBuildSettingsProfileId.Dev, selectedSettingIds);
    }

    internal static bool IsReleaseProfileApplied(List<string> details)
    {
        return IsProfileApplied(GCWebGLBuildSettingsProfileId.Release, details);
    }

    private static GCWebGLBuildSettingsProfilePlan BuildProfilePlan(GCWebGLBuildSettingsProfileId profileId)
    {
        var specs = GetProfileSpecs(profileId);
        var rows = new GCWebGLPreviewRow[specs.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            rows[index] = specs[index].BuildPreviewRow();
        }

        return new GCWebGLBuildSettingsProfilePlan(
            profileId,
            GetProfileDisplayName(profileId),
            rows
        );
    }

    private static GCWebGLBuildSettingsProfileResult ApplyProfile(
        GCWebGLBuildSettingsProfileId profileId,
        IEnumerable<string> selectedSettingIds
    )
    {
        var details = new List<string>();
        var changed = ApplyProfile(profileId, details, selectedSettingIds);
        var displayName = GetProfileDisplayName(profileId);
        var message = changed
            ? displayName + " WebGL build settings were applied."
            : displayName + " WebGL build settings were already configured or skipped.";

        return new GCWebGLBuildSettingsProfileResult(changed, message, details.ToArray());
    }

    private static bool ApplyProfile(
        GCWebGLBuildSettingsProfileId profileId,
        List<string> details,
        IEnumerable<string> selectedSettingIds
    )
    {
        var changed = false;
        var selectedIds = CreateSelectedIdSet(selectedSettingIds);
        var specs = GetProfileSpecs(profileId);

        for (var index = 0; index < specs.Length; index++)
        {
            var row = specs[index].BuildPreviewRow();
            if (!row.isChanged)
            {
                continue;
            }

            if (selectedIds != null && !selectedIds.Contains(row.id))
            {
                AddDetail(details, "Skipped " + row.DiffText + ".");
                continue;
            }

            changed |= specs[index].Apply(details);
        }

        return changed;
    }

    private static bool IsProfileApplied(GCWebGLBuildSettingsProfileId profileId, List<string> details)
    {
        var ready = true;
        var specs = GetProfileSpecs(profileId);
        for (var index = 0; index < specs.Length; index++)
        {
            if (specs[index].IsApplied())
            {
                continue;
            }

            ready = false;
            AddDetail(details, specs[index].BuildPreviewRow().DiffText);
        }

        return ready;
    }

    private static GCWebGLBuildSettingSpec[] GetProfileSpecs(GCWebGLBuildSettingsProfileId profileId)
    {
        return profileId == GCWebGLBuildSettingsProfileId.Dev
            ? DevProfileSpecs
            : ReleaseProfileSpecs;
    }

    private static string GetProfileDisplayName(GCWebGLBuildSettingsProfileId profileId)
    {
        return profileId == GCWebGLBuildSettingsProfileId.Dev ? "Dev" : "Release";
    }

    internal static HashSet<string> CreateSelectedIdSet(IEnumerable<string> selectedSettingIds)
    {
        if (selectedSettingIds == null)
        {
            return null;
        }

        return new HashSet<string>(selectedSettingIds, StringComparer.Ordinal);
    }

    private static GCWebGLBuildSettingSpec[] CreateReleaseProfileSpecs()
    {
        var specs = new List<GCWebGLBuildSettingSpec>
        {
            CreateIl2CppCodeGenerationSpec(Il2CppCodeGeneration.OptimizeSize),
            CreateManagedStrippingLevelSpec(ManagedStrippingLevel.High),
            CreateStripUnusedMeshComponentsSpec(true),
            CreateWebGLDataCachingSpec(true),
            // Disabled by design: compressing served build files is the hosting platform's
            // serving-layer concern, not the build's. Do not "fix" this to Brotli/Gzip.
            CreateWebGLCompressionSpec(WebGLCompressionFormat.Disabled),
            CreateWebGLExceptionSupportSpec(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly),
            CreateWebGLDebugSymbolsSpec(WebGLDebugSymbolMode.Off),
        };
#if UNITY_2023_1_OR_NEWER
        specs.Add(CreateWebAssembly2023Spec(true));
#endif
        specs.Add(CreateDevelopmentBuildSpec(false));
        AddSpecIfPresent(specs, CreateWebGLCodeOptimizationSpec("DiskSizeLTO"));
        return specs.ToArray();
    }

    private static GCWebGLBuildSettingSpec[] CreateDevProfileSpecs()
    {
        var specs = new List<GCWebGLBuildSettingSpec>
        {
            CreateIl2CppCodeGenerationSpec(Il2CppCodeGeneration.OptimizeSpeed),
            CreateManagedStrippingLevelSpec(ManagedStrippingLevel.Disabled),
            CreateStripUnusedMeshComponentsSpec(false),
            CreateWebGLDataCachingSpec(false),
            CreateWebGLCompressionSpec(WebGLCompressionFormat.Disabled),
            CreateWebGLExceptionSupportSpec(WebGLExceptionSupport.FullWithStacktrace),
            CreateWebGLDebugSymbolsSpec(WebGLDebugSymbolMode.Embedded),
        };
#if UNITY_2023_1_OR_NEWER
        specs.Add(CreateWebAssembly2023Spec(true));
#endif
        specs.Add(CreateDevelopmentBuildSpec(true));
        AddSpecIfPresent(specs, CreateWebGLCodeOptimizationSpec("BuildTimes"));
        return specs.ToArray();
    }

    private static GCWebGLBuildSettingSpec CreateIl2CppCodeGenerationSpec(Il2CppCodeGeneration expected)
    {
        return new GCWebGLBuildSettingSpec(
            Il2CppCodeGenerationSettingId,
            "IL2CPP code generation",
            () => FormatEnum(PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL)),
            FormatEnum(expected),
            () => PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL) == expected,
            () => PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, expected)
        );
    }

    private static GCWebGLBuildSettingSpec CreateManagedStrippingLevelSpec(ManagedStrippingLevel expected)
    {
        return new GCWebGLBuildSettingSpec(
            ManagedStrippingLevelSettingId,
            "Managed stripping level",
            () => FormatEnum(PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)),
            FormatEnum(expected),
            () => PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL) == expected,
            () => PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, expected)
        );
    }

    private static GCWebGLBuildSettingSpec CreateStripUnusedMeshComponentsSpec(bool expected)
    {
        return new GCWebGLBuildSettingSpec(
            StripUnusedMeshComponentsSettingId,
            "Unused mesh component stripping",
            () => FormatEnabled(PlayerSettings.stripUnusedMeshComponents),
            FormatEnabled(expected),
            () => PlayerSettings.stripUnusedMeshComponents == expected,
            () => PlayerSettings.stripUnusedMeshComponents = expected
        );
    }

    private static GCWebGLBuildSettingSpec CreateWebGLDataCachingSpec(bool expected)
    {
        return new GCWebGLBuildSettingSpec(
            WebGLDataCachingSettingId,
            "WebGL data caching",
            () => FormatEnabled(PlayerSettings.WebGL.dataCaching),
            FormatEnabled(expected),
            () => PlayerSettings.WebGL.dataCaching == expected,
            () => PlayerSettings.WebGL.dataCaching = expected
        );
    }

    private static GCWebGLBuildSettingSpec CreateWebGLCompressionSpec(WebGLCompressionFormat expected)
    {
        return new GCWebGLBuildSettingSpec(
            WebGLCompressionSettingId,
            "WebGL compression",
            () => FormatEnum(PlayerSettings.WebGL.compressionFormat),
            FormatEnum(expected),
            () => PlayerSettings.WebGL.compressionFormat == expected,
            () => PlayerSettings.WebGL.compressionFormat = expected
        );
    }

    private static GCWebGLBuildSettingSpec CreateWebGLExceptionSupportSpec(WebGLExceptionSupport expected)
    {
        return new GCWebGLBuildSettingSpec(
            WebGLExceptionSupportSettingId,
            "WebGL exception support",
            () => FormatEnum(PlayerSettings.WebGL.exceptionSupport),
            FormatEnum(expected),
            () => PlayerSettings.WebGL.exceptionSupport == expected,
            () => PlayerSettings.WebGL.exceptionSupport = expected
        );
    }

    private static GCWebGLBuildSettingSpec CreateWebGLDebugSymbolsSpec(WebGLDebugSymbolMode expected)
    {
        return new GCWebGLBuildSettingSpec(
            WebGLDebugSymbolsSettingId,
            "WebGL debug symbols",
            () => FormatEnum(PlayerSettings.WebGL.debugSymbolMode),
            FormatEnum(expected),
            () => PlayerSettings.WebGL.debugSymbolMode == expected,
            () => PlayerSettings.WebGL.debugSymbolMode = expected
        );
    }

#if UNITY_2023_1_OR_NEWER
    private static GCWebGLBuildSettingSpec CreateWebAssembly2023Spec(bool expected)
    {
        return new GCWebGLBuildSettingSpec(
            WebAssembly2023SettingId,
            "WebAssembly 2023",
            () => FormatEnabled(PlayerSettings.WebGL.wasm2023),
            FormatEnabled(expected),
            () => PlayerSettings.WebGL.wasm2023 == expected,
            () => PlayerSettings.WebGL.wasm2023 = expected
        );
    }
#endif

    private static GCWebGLBuildSettingSpec CreateDevelopmentBuildSpec(bool expected)
    {
        return new GCWebGLBuildSettingSpec(
            DevelopmentBuildSettingId,
            "Development build",
            () => FormatEnabled(EditorUserBuildSettings.development),
            FormatEnabled(expected),
            () => EditorUserBuildSettings.development == expected,
            () => EditorUserBuildSettings.development = expected
        );
    }

    private static void AddSpecIfPresent(
        List<GCWebGLBuildSettingSpec> specs,
        GCWebGLBuildSettingSpec spec
    )
    {
        if (spec != null)
        {
            specs.Add(spec);
        }
    }

    // WebGL code optimization lives in the optional WebGL Build Support module, so it is
    // read/written through GCWebGLBuildSupport (reflection). When the module is not
    // installed this spec is skipped entirely; the dedicated "Web Build Support
    // installed" blocker is the single source of truth for that situation.
    private static GCWebGLBuildSettingSpec CreateWebGLCodeOptimizationSpec(string expectedValueName)
    {
        if (!GCWebGLBuildSupport.IsModuleInstalled)
        {
            return null;
        }

        var expected = (Enum)GCWebGLBuildSupport.ParseCodeOptimization(expectedValueName);
        return new GCWebGLBuildSettingSpec(
            WebGLCodeOptimizationSettingId,
            "WebGL code optimization",
            () =>
            {
                GCWebGLBuildSupport.TryGetCodeOptimization(out var current);
                return FormatEnum((Enum)current);
            },
            FormatEnum(expected),
            () =>
            {
                GCWebGLBuildSupport.TryGetCodeOptimization(out var current);
                return Equals(current, expected);
            },
            () => GCWebGLBuildSupport.SetCodeOptimization(expected)
        );
    }

    internal static string FormatEnabled(bool value)
    {
        return value ? "Enabled" : "Disabled";
    }

    private static string FormatEnum(Enum value)
    {
        if (value == null)
        {
            return "(unknown)";
        }

        return SplitPascalCase(value.ToString());
    }

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 &&
                char.IsUpper(character) &&
                ShouldInsertSpaceBeforeUppercase(value, index))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool ShouldInsertSpaceBeforeUppercase(string value, int index)
    {
        var previous = value[index - 1];
        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        return index + 1 < value.Length && char.IsLower(value[index + 1]);
    }

    internal static void AddDetail(List<string> details, string detail)
    {
        if (details != null && !string.IsNullOrEmpty(detail))
        {
            details.Add(detail);
        }
    }

    private sealed class GCWebGLBuildSettingSpec
    {
        internal readonly string id;
        private readonly string label;
        private readonly Func<string> getCurrentValue;
        private readonly string targetValue;
        private readonly Func<bool> isApplied;
        private readonly Action applyTarget;

        internal GCWebGLBuildSettingSpec(
            string id,
            string label,
            Func<string> getCurrentValue,
            string targetValue,
            Func<bool> isApplied,
            Action applyTarget
        )
        {
            this.id = id;
            this.label = label;
            this.getCurrentValue = getCurrentValue;
            this.targetValue = targetValue;
            this.isApplied = isApplied;
            this.applyTarget = applyTarget;
        }

        internal bool IsApplied()
        {
            return isApplied();
        }

        internal GCWebGLPreviewRow BuildPreviewRow()
        {
            return new GCWebGLPreviewRow(
                id,
                GCWebGLPreviewRowKind.Setting,
                label,
                getCurrentValue(),
                targetValue,
                !IsApplied(),
                true
            );
        }

        internal bool Apply(List<string> details)
        {
            var row = BuildPreviewRow();
            if (!row.isChanged)
            {
                return false;
            }

            applyTarget();
            AddDetail(details, "Applied " + row.DiffText + ".");
            return true;
        }
    }
}
