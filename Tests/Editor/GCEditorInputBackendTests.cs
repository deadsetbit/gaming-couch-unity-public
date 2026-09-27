using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class GCEditorInputBackendTests
{
    private const string KeyboardReaderFileName = "GCEditorKeyboard.cs";

    // "Input." not preceded by an identifier character, so GCControllerInputs and
    // UnityEngine.InputSystem do not match but UnityEngine.Input.GetAxis does.
    private static readonly Regex LegacyInputCall = new Regex(@"(?<![A-Za-z0-9_])Input\s*\.", RegexOptions.Compiled);

    [Test]
    public void LegacyInputCallsLiveOnlyInTheEditorKeyboardReader()
    {
        var packageRootPath = GamingCouchEditorTestSupport.FindPackageRootPath();

        var offenders = ShippedSourceFiles(packageRootPath)
            .Where(path => Path.GetFileName(path) != KeyboardReaderFileName)
            .Where(path => LegacyInputCall.IsMatch(File.ReadAllText(path)))
            .Select(path => path.Substring(packageRootPath.Length).TrimStart(Path.DirectorySeparatorChar))
            .ToArray();

        Assert.That(
            offenders,
            Is.Empty,
            "Legacy UnityEngine.Input is unavailable when Active Input Handling is set to Input System " +
            "Package: a player build fails to compile against it and the editor throws on every read. " +
            "Reads belong in " + KeyboardReaderFileName + ", which branches on the backend."
        );
    }

    [Test]
    public void EditorKeyboardReaderIsEditorOnlyAndFallsBackToLegacyInput()
    {
        var keyboardReaderPath = ShippedSourceFiles(GamingCouchEditorTestSupport.FindPackageRootPath())
            .Single(path => Path.GetFileName(path) == KeyboardReaderFileName);
        var source = File.ReadAllText(keyboardReaderPath);

        Assert.That(source.TrimStart(), Does.StartWith("#if UNITY_EDITOR"));
        Assert.That(source, Does.Contain("#if ENABLE_INPUT_SYSTEM && GC_INPUT_SYSTEM"));

        // An undefined symbol evaluates to false rather than erroring, so a Unity that predates
        // Active Input Handling still lands on the legacy branch.
        Assert.That(source, Does.Contain("#elif ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM"));
    }

    [Test]
    public void RuntimeAssemblyDefinesGcInputSystemFromThePackageBeingInstalled()
    {
        var assemblyDefinitionPath = Path.Combine(
            GamingCouchEditorTestSupport.FindPackageRootPath(),
            "Runtime",
            "dsb.gamingcouch.runtime.asmdef"
        );
        var assemblyDefinition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(assemblyDefinitionPath));

        Assert.That(assemblyDefinition.references, Does.Contain("Unity.InputSystem"));

        // ENABLE_INPUT_SYSTEM follows the project setting, not whether the package is installed,
        // so the keyboard reader needs a symbol that tracks the package itself.
        var versionDefine = assemblyDefinition.versionDefines
            .SingleOrDefault(define => define.name == "com.unity.inputsystem");

        Assert.That(versionDefine, Is.Not.Null);
        Assert.That(versionDefine.define, Is.EqualTo("GC_INPUT_SYSTEM"));
    }

    private static string[] ShippedSourceFiles(string packageRootPath)
    {
        return new[] { "Runtime", "Editor" }
            .Select(folder => Path.Combine(packageRootPath, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            .ToArray();
    }

    [Serializable]
    private sealed class AssemblyDefinition
    {
        public string[] references;
        public VersionDefine[] versionDefines;
    }

    [Serializable]
    private sealed class VersionDefine
    {
        public string name;
        public string expression;
        public string define;
    }
}
