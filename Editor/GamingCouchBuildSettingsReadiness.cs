using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

internal enum GCActiveSceneBuildSettingsStatus
{
    Ready,
    NoActiveScene,
    UnsavedActiveScene,
    Missing,
    Disabled,
    NotFirst,
    Duplicate,
}

internal enum GCActiveSceneBuildSettingsSetupStatus
{
    Ready,
    Blocked,
}

internal sealed class GCActiveSceneBuildSettingsReadiness
{
    internal readonly GCActiveSceneBuildSettingsStatus status;
    internal readonly string scenePath;
    internal readonly int firstMatchingIndex;
    internal readonly int matchingEntryCount;
    internal readonly string message;

    internal GCActiveSceneBuildSettingsReadiness(
        GCActiveSceneBuildSettingsStatus status,
        string scenePath,
        int firstMatchingIndex,
        int matchingEntryCount,
        string message
    )
    {
        this.status = status;
        this.scenePath = scenePath;
        this.firstMatchingIndex = firstMatchingIndex;
        this.matchingEntryCount = matchingEntryCount;
        this.message = message;
    }

    internal bool IsReady
    {
        get { return status == GCActiveSceneBuildSettingsStatus.Ready; }
    }

    internal bool CanSetFirst
    {
        get
        {
            return status != GCActiveSceneBuildSettingsStatus.Ready &&
                   status != GCActiveSceneBuildSettingsStatus.NoActiveScene;
        }
    }
}

internal sealed class GCActiveSceneBuildSettingsSetupResult
{
    internal readonly GCActiveSceneBuildSettingsSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCActiveSceneBuildSettingsSetupResult(
        GCActiveSceneBuildSettingsSetupStatus status,
        bool changed,
        string message,
        string[] details
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCActiveSceneBuildSettingsSetupStatus.Blocked; }
    }
}

internal static class GamingCouchBuildSettingsReadiness
{
    internal static GCActiveSceneBuildSettingsReadiness InspectActiveScene()
    {
        return Inspect(SceneManager.GetActiveScene(), EditorBuildSettings.scenes);
    }

    internal static GCActiveSceneBuildSettingsReadiness Inspect(
        Scene scene,
        EditorBuildSettingsScene[] scenes
    )
    {
        return InspectScenePath(
            scene.IsValid() && scene.isLoaded,
            scene.IsValid() && scene.isLoaded ? scene.path : null,
            scenes
        );
    }

    internal static GCActiveSceneBuildSettingsReadiness InspectScenePath(
        bool hasLoadedScene,
        string scenePath,
        EditorBuildSettingsScene[] scenes
    )
    {
        if (!hasLoadedScene)
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.NoActiveScene,
                null,
                -1,
                0,
                "Build Settings can be checked after a loaded active scene is available."
            );
        }

        if (string.IsNullOrEmpty(scenePath))
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.UnsavedActiveScene,
                null,
                -1,
                0,
                "Save the active scene before it can be made the first Build Settings scene."
            );
        }

        scenes = scenes ?? new EditorBuildSettingsScene[0];
        var firstMatchingIndex = -1;
        var matchingEntryCount = 0;
        for (var index = 0; index < scenes.Length; index++)
        {
            if (scenes[index] == null ||
                !string.Equals(scenes[index].path, scenePath, StringComparison.Ordinal))
            {
                continue;
            }

            if (firstMatchingIndex < 0)
            {
                firstMatchingIndex = index;
            }

            matchingEntryCount++;
        }

        if (matchingEntryCount == 0)
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.Missing,
                scenePath,
                -1,
                0,
                "The active scene is not in Build Settings."
            );
        }

        if (matchingEntryCount > 1)
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.Duplicate,
                scenePath,
                firstMatchingIndex,
                matchingEntryCount,
                "The active scene appears more than once in Build Settings."
            );
        }

        if (firstMatchingIndex != 0)
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.NotFirst,
                scenePath,
                firstMatchingIndex,
                matchingEntryCount,
                "The active scene is in Build Settings, but it is not the first entry."
            );
        }

        if (scenes.Length == 0 || scenes[0] == null || !scenes[0].enabled)
        {
            return new GCActiveSceneBuildSettingsReadiness(
                GCActiveSceneBuildSettingsStatus.Disabled,
                scenePath,
                0,
                matchingEntryCount,
                "The active scene is first in Build Settings, but it is disabled."
            );
        }

        return new GCActiveSceneBuildSettingsReadiness(
            GCActiveSceneBuildSettingsStatus.Ready,
            scenePath,
            0,
            matchingEntryCount,
            "The active scene is the first enabled Build Settings scene."
        );
    }

    internal static GCActiveSceneBuildSettingsSetupResult EnsureActiveSceneFirstEnabled()
    {
        return EnsureActiveSceneFirstEnabled(SaveActiveSceneWithPrompt);
    }

    internal static GCActiveSceneBuildSettingsSetupResult EnsureActiveSceneFirstEnabled(
        Func<Scene, bool> saveScene
    )
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return Blocked(false, "Build Settings setup is blocked.", "No loaded active scene is available.");
        }

        var scenePath = scene.path;
        var changed = false;
        var details = new List<string>();
        if (string.IsNullOrEmpty(scenePath))
        {
            var save = saveScene ?? SaveActiveSceneWithPrompt;
            if (!save(scene))
            {
                return Blocked(
                    false,
                    "Build Settings setup is blocked.",
                    "The active scene must be saved before it can be added to Build Settings."
                );
            }

            changed = true;
            scenePath = scene.path;
            if (string.IsNullOrEmpty(scenePath))
            {
                return Blocked(
                    true,
                    "Build Settings setup is blocked.",
                    "Unity reported that the active scene was saved, but it still has no scene asset path."
                );
            }

            details.Add("Saved the active scene.");
        }

        var result = SetSceneFirstEnabled(scenePath);
        details.AddRange(result.details);
        return new GCActiveSceneBuildSettingsSetupResult(
            result.status,
            changed || result.changed,
            result.message,
            details.ToArray()
        );
    }

    internal static GCActiveSceneBuildSettingsSetupResult SetSceneFirstEnabled(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
        {
            return Blocked(false, "Build Settings setup is blocked.", "A saved scene asset path is required.");
        }

        var currentScenes = EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0];
        var nextScenes = BuildSceneFirstEnabledSettings(currentScenes, sceneAssetPath);
        if (AreEquivalent(currentScenes, nextScenes))
        {
            return Ready(
                false,
                "The active scene is already the first enabled Build Settings scene.",
                "No Build Settings changes were needed."
            );
        }

        EditorBuildSettings.scenes = nextScenes;
        return Ready(
            true,
            "Set the active scene as the first enabled Build Settings scene.",
            "Preserved unrelated Build Settings entries after the active scene."
        );
    }

    internal static EditorBuildSettingsScene[] BuildSceneFirstEnabledSettings(
        EditorBuildSettingsScene[] scenes,
        string sceneAssetPath
    )
    {
        var nextScenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(sceneAssetPath, true)
        };

        scenes = scenes ?? new EditorBuildSettingsScene[0];
        for (var index = 0; index < scenes.Length; index++)
        {
            var scene = scenes[index];
            if (scene != null && string.Equals(scene.path, sceneAssetPath, StringComparison.Ordinal))
            {
                continue;
            }

            nextScenes.Add(scene);
        }

        return nextScenes.ToArray();
    }

    private static bool SaveActiveSceneWithPrompt(Scene scene)
    {
        return EditorSceneManager.SaveScene(scene);
    }

    private static GCActiveSceneBuildSettingsSetupResult Ready(
        bool changed,
        string message,
        string detail
    )
    {
        return new GCActiveSceneBuildSettingsSetupResult(
            GCActiveSceneBuildSettingsSetupStatus.Ready,
            changed,
            message,
            new[] { detail }
        );
    }

    private static GCActiveSceneBuildSettingsSetupResult Blocked(
        bool changed,
        string message,
        string detail
    )
    {
        return new GCActiveSceneBuildSettingsSetupResult(
            GCActiveSceneBuildSettingsSetupStatus.Blocked,
            changed,
            message,
            new[] { detail }
        );
    }

    private static bool AreEquivalent(EditorBuildSettingsScene[] left, EditorBuildSettingsScene[] right)
    {
        left = left ?? new EditorBuildSettingsScene[0];
        right = right ?? new EditorBuildSettingsScene[0];
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            var leftScene = left[index];
            var rightScene = right[index];
            if (leftScene == null || rightScene == null)
            {
                if (leftScene != rightScene)
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(leftScene.path, rightScene.path, StringComparison.Ordinal) ||
                leftScene.enabled != rightScene.enabled)
            {
                return false;
            }
        }

        return true;
    }
}
