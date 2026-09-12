using System;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchStartScreenSetupActionsTests
{
    private const string TestSceneBuildPath = "Assets/GamingCouchStartScreenSetupActionsTestScene.unity";

    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;

    [SetUp]
    public void SetUp()
    {
        // Snapshot build settings WITHOUT any scene this test leaked in a prior interrupted run,
        // and heal a present leak immediately so it cannot reach a Player build or a commit. See
        // WithoutLeakedTestScenes / GamingCouchStartScreenEditorSmokeTests.
        previousBuildSettingsScenes = WithoutLeakedTestScenes(EditorBuildSettings.scenes);
        if (previousBuildSettingsScenes.Length != EditorBuildSettings.scenes.Length)
        {
            EditorBuildSettings.scenes = previousBuildSettingsScenes;
        }
        previousActiveScene = SceneManager.GetActiveScene();

        if (CanReuseActiveSceneAsTestScene(previousActiveScene))
        {
            testScene = previousActiveScene;
            testSceneWasCreatedAdditively = false;
        }
        else
        {
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            testSceneWasCreatedAdditively = true;
        }

        ClearSceneRootObjects(testScene);
        EnsureSceneIsActive(testScene);
    }

    // Drop this test's own scene entry so a leak from an interrupted run cannot survive in the
    // project's persisted build settings and break a later Player build. Mirrors
    // GamingCouchStartScreenEditorSmokeTests.
    private static EditorBuildSettingsScene[] WithoutLeakedTestScenes(EditorBuildSettingsScene[] scenes)
    {
        if (scenes == null)
        {
            return Array.Empty<EditorBuildSettingsScene>();
        }

        return scenes
            .Where(scene => scene != null && !IsTestOwnedScenePath(scene.path))
            .ToArray();
    }

    private static bool IsTestOwnedScenePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return string.Equals(path, TestSceneBuildPath, StringComparison.Ordinal);
    }

    [TearDown]
    public void TearDown()
    {
        CloseWebGLPreviewWindows();
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
        Selection.activeObject = null;

        if (testScene.IsValid() && testScene.isLoaded)
        {
            ClearSceneRootObjects(testScene);
        }

        if (!testSceneWasCreatedAdditively)
        {
            return;
        }

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }
        else if (testScene.IsValid() && testScene.isLoaded)
        {
            SetAnyLoadedSceneActiveExcept(testScene);
        }

        if (testScene.IsValid() && testScene.isLoaded)
        {
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    [Test]
    public void NullChecklistActionReturnsWarningWithoutSceneChanges()
    {
        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Warning));
        Assert.That(result.message, Does.Contain("No checklist item"));
        Assert.That(result.details, Is.Empty);
        Assert.That(result.focusTarget, Is.Null);
        Assert.That(result.shouldRefreshAndRepaint, Is.False);
        Assert.That(FindGamingCouchesInTestScene(), Is.Empty);
    }

    [Test]
    public void CreateGamingCouchActionUsesSceneWiringAndReturnsSelectionTarget()
    {
        var readiness = CreateReadiness(null, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(check);
        var gamingCouches = FindGamingCouchesInTestScene();

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("GamingCouch"));
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
        Assert.That(result.shouldPingFocusTarget, Is.False);
        Assert.That(gamingCouches, Has.Length.EqualTo(1));
        Assert.That(result.focusTarget, Is.SameAs(gamingCouches[0].gameObject));
    }

    [Test]
    public void FocusActionReturnsTargetWithoutRunningSetup()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        var result = GamingCouchStartScreenSetupActions.RunChecklistAction(check);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("Focused Existing Player Prefab"));
        Assert.That(result.focusTarget, Is.SameAs(playerPrefab));
        Assert.That(result.shouldPingFocusTarget, Is.True);
        Assert.That(result.shouldRefreshAndRepaint, Is.False);
        Assert.That(FindGamingCouchesInTestScene(), Has.Length.EqualTo(1));
    }

    [Test]
    public void WindowAppliesFocusActionSelectionFromRunnerResult()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            InvokeWindowChecklistAction(window, check);

            Assert.That(Selection.activeObject, Is.SameAs(playerPrefab));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void WebGLBuildMenuCommandOpensSharedPreviewWithoutApplying()
    {
        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewReleaseBuildSettings();

        Assert.That(FindWebGLPreviewWindows(), Is.Not.Empty);
    }

    [Test]
    public void WebGLProfileMenuCommandsOpenSelectorWithRequestedInitialProfile()
    {
        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewDevBuildSettings();
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Dev)
        );

        CloseWebGLPreviewWindows();

        GamingCouchWebGLBuildMenu.PreviewReleaseBuildSettings();
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Release)
        );
    }

    [Test]
    public void StartScreenBuildSettingsProfileActionOpensSharedPreview()
    {
        CloseWebGLPreviewWindows();

        var result = GamingCouchStartScreenSetupActions.OpenWebGLBuildSettingsProfilePreview(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(result.message, Does.Contain("WebGL build settings preview opened"));
        Assert.That(
            GetSelectedProfile(AssertSingleWebGLPreviewWindow()),
            Is.EqualTo(GCWebGLBuildSettingsProfileId.Dev)
        );
    }

    [Test]
    public void StartScreenWebGLChecklistActionOpensSharedPreview()
    {
        CloseWebGLPreviewWindows();
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(
            gamingCouch,
            listener,
            playerPrefab,
            webGLExport: new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                false,
                false,
                false,
                false,
                false,
                false,
                "Gaming Couch web export settings are incomplete.",
                Array.Empty<string>()
            )
        );
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);
        var window = EditorWindow.CreateInstance<GamingCouchStartScreenWindow>();

        try
        {
            InvokeWindowChecklistAction(window, check);

            Assert.That(FindWebGLPreviewWindows(), Is.Not.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void CreateAndWireGameActionBlocksWithoutGamingCouchAndDoesNotCreateGameObject()
    {
        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateAndWireGameScript
        );

        Assert.That(result.messageType, Is.EqualTo(MessageType.Error));
        Assert.That(result.message, Does.Contain("blocked"));
        AssertHasEntryContaining(result.details, "Create or reuse a GamingCouch object");
        Assert.That(FindRootObject("Game"), Is.Null);
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
    }

    [Test]
    public void ReadySetupActionResultsRemainSilentForWindowDisplay()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateAndWireGameScript
        );

        Assert.That(result.messageType, Is.EqualTo(MessageType.Info));
        Assert.That(GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType), Is.False);
        Assert.That(result.message, Does.Contain("already complete"));
        Assert.That(result.shouldRefreshAndRepaint, Is.True);
    }

    [Test]
    public void PendingCompilationResultReturnsWarningAndPreservesDetails()
    {
        var pendingResult = new GCActiveSceneSetupResult(
            GCActiveSceneSetupStatus.PendingCompilation,
            true,
            "Active Scene Setup created missing example scripts and queued setup continuation after Unity compiles them.",
            new[]
            {
                "Created: Assets/GamingCouch/GCExample/GCExampleGame.cs",
                "Created: Assets/GamingCouch/GCExample/GCExamplePlayer.cs",
            }
        );

        var result = GamingCouchStartScreenSetupActions.FromActiveSceneSetupResult(pendingResult);
        var formatted = GamingCouchStartScreenSetupActions.FormatActionMessage(result.message, result.details);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Warning));
        Assert.That(GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType), Is.True);
        AssertHasEntryContaining(result.details, "Created: Assets/GamingCouch/GCExample/GCExampleGame.cs");
        AssertHasEntryContaining(result.details, "Created: Assets/GamingCouch/GCExample/GCExamplePlayer.cs");
        Assert.That(formatted, Does.Contain("queued setup continuation"));
        Assert.That(formatted, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCExampleGame.cs"));
        Assert.That(formatted, Does.Contain("- Created: Assets/GamingCouch/GCExample/GCExamplePlayer.cs"));
    }

    [Test]
    public void WireExampleGameResultMapsStatusesAndPreservesDetails()
    {
        var blocked = GamingCouchStartScreenSetupActions.FromWireExampleGameResult(
            new GCWireExampleGameResult(
                GCWireExampleGameStatus.Blocked,
                false,
                false,
                "Wire example game is blocked.",
                new[] { "Created: Assets/GamingCouch/GCExample/GCExampleGame.cs" }
            )
        );
        Assert.That(blocked.messageType, Is.EqualTo(MessageType.Error));
        AssertHasEntryContaining(blocked.details, "Created: Assets/GamingCouch/GCExample/GCExampleGame.cs");

        var pending = GamingCouchStartScreenSetupActions.FromWireExampleGameResult(
            new GCWireExampleGameResult(GCWireExampleGameStatus.Wired, true, true, "Generating…", null)
        );
        Assert.That(pending.messageType, Is.EqualTo(MessageType.Warning));

        var wired = GamingCouchStartScreenSetupActions.FromWireExampleGameResult(
            new GCWireExampleGameResult(GCWireExampleGameStatus.Wired, false, true, "Wired the example game.", null)
        );
        Assert.That(wired.messageType, Is.EqualTo(MessageType.Info));

        // Declining the "move blocking folders to the Trash" confirmation is not a failure, so it
        // maps to Warning like a cancelled reset, never to Error.
        var cancelled = GamingCouchStartScreenSetupActions.FromWireExampleGameResult(
            new GCWireExampleGameResult(
                GCWireExampleGameStatus.Cancelled,
                false,
                false,
                "Wire example game was cancelled.",
                null
            )
        );
        Assert.That(cancelled.messageType, Is.EqualTo(MessageType.Warning));

        var nullResult = GamingCouchStartScreenSetupActions.FromWireExampleGameResult(null);
        Assert.That(nullResult.messageType, Is.EqualTo(MessageType.Error));
    }

    [Test]
    public void ActiveSceneSetupNullResultUsesActiveSceneTerminology()
    {
        var result = GamingCouchStartScreenSetupActions.FromActiveSceneSetupResult(null);

        Assert.That(result.messageType, Is.EqualTo(MessageType.Error));
        Assert.That(result.message, Is.EqualTo("Active Scene Setup did not return a result."));
    }

    [Test]
    public void PendingSetupDisplayNameUsesActiveSceneTerminology()
    {
        Assert.That(
            GamingCouchActiveSceneSetup.GetSetupDisplayName(),
            Is.EqualTo("Active Scene Setup")
        );
    }

    [Test]
    public void SetupActionAvailabilityUsesReadinessRules()
    {
        var readinessWithoutActiveScene = CreateReadinessForScene(default(Scene));
        var missingGamingCouchCheck = readinessWithoutActiveScene.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);
        var gameViewReadiness = CreateReadiness(
            GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch"),
            GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game"),
            GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab"),
            CreateReadyBuildSettingsReadiness(),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[]
                {
                    new GCGameViewSizeEntry(0, "Free Aspect", 0, 0),
                    new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9),
                },
                0,
                true,
                null
            ),
            CreateReadyWebGLExportReadiness()
        );
        var gameViewCheck = gameViewReadiness.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9);

        Assert.That(
            GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(
                missingGamingCouchCheck,
                readinessWithoutActiveScene
            ),
            Is.True
        );
        Assert.That(
            GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(gameViewCheck, gameViewReadiness),
            Is.False
        );
    }

    [Test]
    public void WebGLModuleInstallStepsIncludeVersionModuleAndCliCommand()
    {
        var steps = GamingCouchStartScreenSetupActions.BuildWebGLModuleInstallSteps("6000.0.42f1");

        Assert.That(steps, Does.Contain("6000.0.42f1"));
        Assert.That(steps, Does.Contain("Web Build Support"));
        Assert.That(steps, Does.Contain("Add modules"));
        Assert.That(steps, Does.Contain("Reopen this project"));
        Assert.That(steps, Does.Contain("unity install-modules -e 6000.0.42f1 -m webgl"));
    }

    [Test]
    public void WebGLModuleInstallActionIsAlwaysEnabledEvenWithoutActiveScene()
    {
        var readiness = CreateReadinessForScene(default(Scene));
        var moduleCheck = readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLModuleInstalled);

        Assert.That(moduleCheck.HasExternalAction, Is.True);
        Assert.That(
            GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(moduleCheck, readiness),
            Is.False
        );
    }

    private GCStartScreenReadiness CreateReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null
    )
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            testScene,
            gamingCouch != null ? new[] { gamingCouch } : new GamingCouch[0],
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson ?? CreateValidLocalPlayJsonReadiness(),
            buildSettings ?? CreateReadyBuildSettingsReadiness(),
            gameViewAspect ?? CreateReadyGameViewAspectReadiness(),
            webGLExport ?? CreateReadyWebGLExportReadiness()
        ));
    }

    private static GCStartScreenReadiness CreateReadinessForScene(Scene scene)
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(scene));
    }

    private GamingCouch[] FindGamingCouchesInTestScene()
    {
        return testScene.GetRootGameObjects()
            .Select(root => root != null ? root.GetComponent<GamingCouch>() : null)
            .Where(gamingCouch => gamingCouch != null)
            .ToArray();
    }

    private GameObject FindRootObject(string name)
    {
        return testScene.GetRootGameObjects()
            .FirstOrDefault(root => root != null && string.Equals(root.name, name, StringComparison.Ordinal));
    }

    private static GCStartScreenLocalPlayJsonReadiness CreateValidLocalPlayJsonReadiness()
    {
        return new GCStartScreenLocalPlayJsonReadiness(
            true,
            "Library/GamingCouch/gc.dev.json",
            "gc.dev.json is valid for local Play Mode.",
            null
        );
    }

    private static GCActiveSceneBuildSettingsReadiness CreateReadyBuildSettingsReadiness()
    {
        return GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );
    }

    private static GCGameViewAspectReadiness CreateReadyGameViewAspectReadiness()
    {
        return GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9) },
            0,
            true,
            null
        );
    }

    private static GCWebGLExportReadiness CreateReadyWebGLExportReadiness()
    {
        return new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Ready,
            true,
            true,
            true,
            true,
            true,
            true,
            "Gaming Couch web export settings are ready.",
            Array.Empty<string>()
        );
    }

    private static void AssertHasEntryContaining(string[] entries, string expectedSubstring)
    {
        Assert.That(
            entries != null && entries.Any(entry =>
                entry != null && entry.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0
            ),
            Is.True
        );
    }

    private static void InvokeWindowChecklistAction(
        GamingCouchStartScreenWindow window,
        GCStartScreenReadinessCheck check
    )
    {
        var method = typeof(GamingCouchStartScreenWindow)
            .GetMethod("RunChecklistAction", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        method.Invoke(window, new object[] { check });
    }

    private static GamingCouchWebGLBuildSettingsPreviewWindow[] FindWebGLPreviewWindows()
    {
        return Resources.FindObjectsOfTypeAll<GamingCouchWebGLBuildSettingsPreviewWindow>();
    }

    private static GamingCouchWebGLBuildSettingsPreviewWindow AssertSingleWebGLPreviewWindow()
    {
        var windows = FindWebGLPreviewWindows();
        Assert.That(windows, Has.Length.EqualTo(1));
        return windows[0];
    }

    private static GCWebGLBuildSettingsProfileId GetSelectedProfile(
        GamingCouchWebGLBuildSettingsPreviewWindow window
    )
    {
        var field = typeof(GamingCouchWebGLBuildSettingsPreviewWindow)
            .GetField("selectedProfileId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (GCWebGLBuildSettingsProfileId)field.GetValue(window);
    }

    private static void CloseWebGLPreviewWindows()
    {
        var windows = FindWebGLPreviewWindows();
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index] != null)
            {
                windows[index].Close();
            }
        }
    }

    private static bool CanReuseActiveSceneAsTestScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.isLoaded &&
               string.IsNullOrEmpty(scene.path);
    }

    private static void EnsureSceneIsActive(Scene scene)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.handle == scene.handle)
        {
            return;
        }

        Assert.That(SceneManager.SetActiveScene(scene), Is.True);
    }

    private static void ClearSceneRootObjects(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (var index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(roots[index]);
            }
        }
    }

    private static void SetAnyLoadedSceneActiveExcept(Scene excludedScene)
    {
        for (var index = 0; index < SceneManager.sceneCount; index++)
        {
            var scene = SceneManager.GetSceneAt(index);
            if (scene.IsValid() && scene.isLoaded && scene != excludedScene)
            {
                SceneManager.SetActiveScene(scene);
                return;
            }
        }
    }
}
