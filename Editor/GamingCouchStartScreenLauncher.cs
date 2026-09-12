using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class GCStartScreenSettings
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";

    internal static bool SuppressAutoOpen
    {
        get { return EditorUserSettings.GetConfigValue(SuppressAutoOpenKey) == "true"; }
        set { EditorUserSettings.SetConfigValue(SuppressAutoOpenKey, value ? "true" : "false"); }
    }
}

[InitializeOnLoad]
internal static class GCStartScreenStartupLauncher
{
    private const string StartupCheckCompletedSessionKey = "DSB.GC.StartScreen.StartupCheckCompleted.v1";

    static GCStartScreenStartupLauncher()
    {
        if (SessionState.GetBool(StartupCheckCompletedSessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall -= BeginStartupCheck;
        EditorApplication.delayCall += BeginStartupCheck;
    }

    private static void BeginStartupCheck()
    {
        EditorApplication.delayCall -= BeginStartupCheck;

        if (SessionState.GetBool(StartupCheckCompletedSessionKey, false))
        {
            return;
        }

        EditorApplication.update -= RunStartupCheckWhenSceneIsAvailable;
        EditorApplication.update += RunStartupCheckWhenSceneIsAvailable;
    }

    private static void RunStartupCheckWhenSceneIsAvailable()
    {
        if (SessionState.GetBool(StartupCheckCompletedSessionKey, false))
        {
            StopStartupCheck();
            return;
        }

        if (Application.isBatchMode)
        {
            CompleteStartupCheck();
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            CompleteStartupCheck();
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        CompleteStartupCheck();

        if (GCStartScreenSettings.SuppressAutoOpen)
        {
            return;
        }

        if (!IsNormalSceneContext(activeScene))
        {
            return;
        }

        // Greet on every launch until the user opts out via
        // "Never open this again on startup" (GCStartScreenSettings.SuppressAutoOpen).
        // Readiness is no longer gated here; the window inspects and reports it on open.
        GamingCouchStartScreenWindow.Open();
    }

    private static void CompleteStartupCheck()
    {
        SessionState.SetBool(StartupCheckCompletedSessionKey, true);
        StopStartupCheck();
    }

    private static void StopStartupCheck()
    {
        EditorApplication.update -= RunStartupCheckWhenSceneIsAvailable;
    }

    private static bool IsNormalSceneContext(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            return false;
        }

        return !EditorSceneManager.IsPreviewScene(scene);
    }
}
