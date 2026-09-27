using System;
using System.Reflection;

// UnityEditor.WebGL.* lives in the WebGL Build Support platform module, an optional
// Unity Hub install. Referencing those types at compile time makes this editor
// assembly fail to build when the module is absent - which would also take down the
// Start Screen readiness check whose whole job is to detect and report that very
// module being missing. This helper accesses the one member we depend on
// (UnityEditor.WebGL.UserBuildSettings.codeOptimization) via reflection so every call
// site keeps compiling with or without the module, degrading gracefully when it is
// not installed.
internal static class GCWebGLBuildSupport
{
    private static Type userBuildSettingsType;
    private static bool userBuildSettingsTypeResolved;

    private static Type UserBuildSettingsType
    {
        get
        {
            if (!userBuildSettingsTypeResolved)
            {
                userBuildSettingsTypeResolved = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("UnityEditor.WebGL.UserBuildSettings", false);
                    if (type != null)
                    {
                        userBuildSettingsType = type;
                        break;
                    }
                }
            }

            return userBuildSettingsType;
        }
    }

    private static PropertyInfo CodeOptimizationProperty =>
        UserBuildSettingsType?.GetProperty(
            "codeOptimization",
            BindingFlags.Public | BindingFlags.Static
        );

    /// <summary>True when the WebGL Build Support module is installed.</summary>
    internal static bool IsModuleInstalled => CodeOptimizationProperty != null;

    /// <summary>
    /// Reads UnityEditor.WebGL.UserBuildSettings.codeOptimization. Returns false (and a
    /// null value) when the WebGL Build Support module is not installed.
    /// </summary>
    internal static bool TryGetCodeOptimization(out object value)
    {
        var property = CodeOptimizationProperty;
        if (property == null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(null);
        return true;
    }

    /// <summary>
    /// Writes UnityEditor.WebGL.UserBuildSettings.codeOptimization. No-op when the module
    /// is not installed (or when value is null).
    /// </summary>
    internal static void SetCodeOptimization(object value)
    {
        if (value != null)
        {
            CodeOptimizationProperty?.SetValue(null, value);
        }
    }

    /// <summary>
    /// Parses a WasmCodeOptimization value by name (e.g. "DiskSizeLTO"), or returns null
    /// when the module is not installed.
    /// </summary>
    internal static object ParseCodeOptimization(string valueName)
    {
        // Parse against the enum type of the codeOptimization property
        // (UnityEditor.WebGL.WasmCodeOptimization), NOT the UserBuildSettings class.
        var property = CodeOptimizationProperty;
        return property == null ? null : Enum.Parse(property.PropertyType, valueName);
    }

    /// <summary>
    /// Current code optimization as a string, or null when the module is not installed.
    /// </summary>
    internal static string GetCodeOptimizationString()
    {
        return TryGetCodeOptimization(out var value) ? value.ToString() : null;
    }
}
