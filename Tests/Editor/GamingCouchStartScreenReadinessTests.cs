using System;
using System.Linq;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchStartScreenReadinessTests
{
    private const string TestSceneBuildPath = "Assets/GamingCouchStartScreenReadinessTestScene.unity";

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
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();

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
    public void MissingGamingCouchOffersCreateGamingCouchAction()
    {
        var readiness = CreateReadiness(null, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        AssertAction(check, GCStartScreenReadinessActionId.CreateGamingCouch, "Create GamingCouch");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.GamingCouchInstance), Is.True);
    }

    [Test]
    public void ReadyGamingCouchOffersFocusSceneObjectAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var readiness = CreateReadiness(gamingCouch, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);

        AssertAction(check, GCStartScreenReadinessActionId.FocusSceneObject, "Focus Scene Object");
        Assert.That(check.action.target, Is.SameAs(gamingCouch.gameObject));
    }

    [Test]
    public void MissingGameScriptOffersCreateAndWireGameAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var readiness = CreateReadiness(gamingCouch, null, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.CreateAndWireGameScript, "Create & Wire Game");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.True);
    }

    [Test]
    public void CompatibleGameScriptOffersFocusGameScriptAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.FocusGameScript, "Focus Game Script");
        Assert.That(check.action.target, Is.SameAs(listener));
    }

    [Test]
    public void IncompatibleGameScriptOffersNoSetupAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Incomplete Listener");
        listener.AddComponent<SetupOnlyGameScriptReceiver>();
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertNoAction(check);
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void MissingPlayerPrefabOffersWirePlayerPrefabAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var readiness = CreateReadiness(gamingCouch, listener, null);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.WirePlayerPrefab, "Wire Player Prefab");
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.PlayerPrefabAssigned), Is.True);
    }

    [Test]
    public void ReadyPlayerPrefabOffersFocusPrefabAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        var readiness = CreateReadiness(gamingCouch, listener, playerPrefab);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned);

        AssertAction(check, GCStartScreenReadinessActionId.FocusPrefab, "Focus Prefab");
        Assert.That(check.action.target, Is.SameAs(playerPrefab));
    }

    [Test]
    public void BuildSettingsActionRequiresSettableReadiness()
    {
        var readinessWithSetup = CreateReadySceneReadiness(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                Array.Empty<EditorBuildSettingsScene>()
            ),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );
        var readyReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );

        AssertAction(
            readinessWithSetup.GetCheck(GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene),
            GCStartScreenReadinessActionId.SetFirstBuildSettingsScene,
            "Set First Build Scene"
        );
        AssertNoAction(readyReadiness.GetCheck(GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene));
    }

    [Test]
    public void GameViewActionRequiresSafeSelectionAction()
    {
        var mismatchWithSelection = GamingCouchGameViewAspect.InspectSizeEntries(
            new[]
            {
                new GCGameViewSizeEntry(0, "Free Aspect", 0, 0),
                new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9),
            },
            0,
            true,
            null
        );
        var mismatchWithoutSelection = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3) },
            0,
            true,
            null
        );

        var readinessWithSetup = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            mismatchWithSelection,
            CreateReadyWebGLExportReadiness()
        );
        var readinessWithoutSetup = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            mismatchWithoutSelection,
            CreateReadyWebGLExportReadiness()
        );

        AssertAction(
            readinessWithSetup.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9),
            GCStartScreenReadinessActionId.Select16By9GameView,
            "Select 16:9"
        );
        AssertNoAction(readinessWithoutSetup.GetCheck(GCStartScreenReadinessCheckId.GameViewAspect16By9));
    }

    [Test]
    public void WebGLExportActionAppearsForBlockedOrWarningReadiness()
    {
        var blockedReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                false,
                false,
                false,
                false,
                false,
                true,
                "Gaming Couch web export settings are incomplete.",
                Array.Empty<string>()
            )
        );
        var readyReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );
        var warningReadiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Warning,
                true,
                true,
                true,
                true,
                true,
                false,
                "Gaming Couch web export settings are ready, but the active build target is not WebGL.",
                Array.Empty<string>()
            )
        );

        AssertAction(
            blockedReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup),
            GCStartScreenReadinessActionId.SetUpWebGLExport,
            "Set Up Web Export"
        );
        AssertAction(
            warningReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup),
            GCStartScreenReadinessActionId.SetUpWebGLExport,
            "Set Up Web Export"
        );
        AssertNoAction(readyReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup));
    }

    [Test]
    public void LocalPlayJsonRowNeverOffersSetupAction()
    {
        var readiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness(),
            new GCStartScreenLocalPlayJsonReadiness(
                false,
                "Library/GamingCouch/gc.dev.json",
                "gc.dev.json is missing or invalid for local Play Mode.",
                null
            )
        );

        AssertNoAction(readiness.GetCheck(GCStartScreenReadinessCheckId.LocalPlayJsonValid));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.LocalPlayJsonValid), Is.False);
    }

    [Test]
    public void ReadinessReportsMissingGamingCouchInEmptyScene()
    {
        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(0));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).message, Does.Contain("Create a GamingCouch object"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void ReadinessKeepsActiveSceneGuardOutOfDisplayedChecklist()
    {
        var readiness = CreateReadinessForScene(default(Scene));
        GCStartScreenReadinessCheck activeSceneCheck;
        var checklistIds = readiness.checklist.Select(check => check.id).ToArray();
        var checklistLabels = readiness.checklist.Select(check => check.label).ToArray();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.sceneName, Is.EqualTo("Untitled"));
        Assert.That(readiness.scenePath, Is.Null);
        Assert.That(readiness.TryGetCheck(GCStartScreenReadinessCheckId.ActiveScene, out activeSceneCheck), Is.True);
        Assert.That(activeSceneCheck, Is.SameAs(readiness.GetCheck(GCStartScreenReadinessCheckId.ActiveScene)));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.ActiveScene).message, Does.Contain("No loaded active scene"));
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.ActiveScene));
        Assert.That(HasActiveSceneChecklistLabel(checklistLabels), Is.False);
    }

    [Test]
    public void ReadinessProvidesHelpTextForDisplayedChecklistRows()
    {
        var readiness = CreateReadinessForScene(default(Scene));
        var displayableChecks = readiness.checklist.Where(check => check != null).ToArray();

        Assert.That(displayableChecks, Is.Not.Empty);
        Assert.That(displayableChecks.All(check => !string.IsNullOrEmpty(check.helpText)), Is.True);
        Assert.That(displayableChecks.All(check => check.helpText != check.label), Is.True);
        Assert.That(displayableChecks.All(check => check.helpText != check.message), Is.True);
        Assert.That(
            displayableChecks.All(check => check.helpText.EndsWith(".", StringComparison.Ordinal)),
            Is.True
        );
    }

    [Test]
    public void ReadinessUsesGameScriptCopyForListenerChecklistRow()
    {
        var blockedReadiness = CreateReadinessForScene(default(Scene));
        var blockedCheck = blockedReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(blockedCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(blockedCheck.message, Does.Contain("Game script readiness"));
        Assert.That(blockedCheck.helpText, Does.Contain("Game script"));

        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var missingReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var missingCheck = missingReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(missingCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(missingCheck.message, Does.Contain("Game script object"));

        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        var readyReadiness = GCStartScreenReadinessService.InspectActiveScene();
        var readyCheck = readyReadiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(readyCheck.label, Is.EqualTo("Game script is ready"));
        Assert.That(readyCheck.message, Does.Contain("Game script reference"));
        Assert.That(readyCheck.message, Does.Contain("Existing Game"));
    }

    [Test]
    public void ReadinessReportsMissingReferencesOnBareGamingCouch()
    {
        GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(1));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void ReadinessReportsIndividualMissingReferenceStates()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var missingPlayerReadiness = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(missingPlayerReadiness.IsSceneReady, Is.False);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(missingPlayerReadiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Fail);

        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var ready = GCStartScreenReadinessService.InspectActiveScene();
        Assert.That(ready.IsSceneReady, Is.True);
        AssertCheck(ready, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCheck(ready, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Pass);
        AssertCollapsedGamingCouchChecklist(ready);
    }

    [Test]
    public void ReadinessAcceptsCompatibleCustomListenerNotNamedGame()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Round Coordinator");

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
        Assert.That(check.message, Does.Contain("Round Coordinator"));
    }

    [Test]
    public void ReadinessAcceptsPublicInheritedReceiverMethods()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Inherited Listener");
        listener.AddComponent<InheritedCompatibleGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Pass);
    }

    [Test]
    public void ReadinessRejectsAssignedListenerWithoutSetupAndPlayReceivers()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Incomplete Listener");
        listener.AddComponent<SetupOnlyGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("Incomplete Listener"));
        Assert.That(check.message, Does.Contain("GamingCouchSetup(GCSetupOptions)"));
        Assert.That(check.message, Does.Contain("GamingCouchPlay(GCPlayOptions)"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessRejectsReceiverMethodsWithWrongSignatures()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Wrong Signature Listener");
        listener.AddComponent<WrongSignatureGameScriptReceiver>();

        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("Wrong Signature Listener"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessReportsUnresolvedSerializedListenerReferenceGuidance()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var readiness = CreateReadiness(gamingCouch, null, null, hasSerializedListenerReference: true);
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("could not be resolved"));
        Assert.That(check.message, Does.Contain("deleted object"));
        Assert.That(check.message, Does.Contain("unloaded asset"));
        Assert.That(check.message, Does.Contain("script that no longer compiles"));
        Assert.That(check.message, Does.Contain("Clear or replace"));
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.False);
    }

    [Test]
    public void ReadinessReportsMissingSerializedListenerReferenceAndOffersSetupAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Deleted Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(listener);

        var readiness = GCStartScreenReadinessService.InspectActiveScene();
        var check = readiness.GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned);

        Assert.That(readiness.hasMissingSerializedListenerReference, Is.True);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Fail);
        Assert.That(check.message, Does.Contain("missing GameObject"));
        Assert.That(check.message, Does.Contain("Create & Wire Game"));
        Assert.That(check.message.Contains("broken"), Is.False);
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.ListenerAssigned), Is.True);
    }

    [Test]
    public void ReadinessBlocksWhenActiveSceneHasMultipleGamingCouches()
    {
        GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch A");
        GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch B");

        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.IsSceneReady, Is.False);
        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(2));
        Assert.That(readiness.gamingCouch, Is.Null);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, GCStartScreenReadinessCheckState.Fail);
        Assert.That(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).message, Does.Contain("multiple GamingCouch"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCheck(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, GCStartScreenReadinessCheckState.Blocked);
        AssertCollapsedGamingCouchChecklist(readiness);
    }

    [Test]
    public void BlockingChecklistReadinessIgnoresWarningRows()
    {
        var warningOnly = new[]
        {
            new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GameViewAspect16By9,
                "Game View uses 16:9 preview",
                GCStartScreenReadinessCheckState.Warning,
                "Game View could not be inspected."
            ),
        };
        var blocking = new[]
        {
            new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Fail,
                "The active scene is not in Build Settings."
            ),
        };

        Assert.That(GCStartScreenReadiness.HasBlockingChecklistIssues(warningOnly), Is.False);
        Assert.That(GCStartScreenReadiness.HasBlockingChecklistIssues(blocking), Is.True);
    }

    [Test]
    public void StartScreenReadinessSummaryReportsNoPendingItemsWhenReady()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);
        var readiness = CreateReadyStartScreenReadiness(gamingCouch, listener, playerPrefab);

        var summary = GCStartScreenReadinessSummary.Create(readiness, false);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.Ready));
        Assert.That(summary.HasPendingItems, Is.False);
        Assert.That(summary.blockerCount, Is.EqualTo(0));
        Assert.That(summary.warningCount, Is.EqualTo(0));
        Assert.That(summary.actionableSetupCount, Is.EqualTo(0));
        Assert.That(summary.message, Is.EqualTo("Start Screen: no pending setup items."));
    }

    [Test]
    public void StartScreenReadinessSummaryCountsBlockersWarningsAndActions()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var warningReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Warning,
            true,
            true,
            true,
            true,
            true,
            false,
            "Gaming Couch web export settings are ready, but the active build target is not WebGL.",
            Array.Empty<string>()
        );
        var readiness = CreateReadiness(gamingCouch, null, null, webGLExport: warningReadiness);

        var summary = GCStartScreenReadinessSummary.Create(readiness, false);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.Actionable));
        Assert.That(summary.HasPendingItems, Is.True);
        Assert.That(summary.blockerCount, Is.EqualTo(0));
        Assert.That(summary.warningCount, Is.EqualTo(1));
        Assert.That(summary.actionableSetupCount, Is.EqualTo(3));
        Assert.That(summary.message, Is.EqualTo("Start Screen: 3 setup actions, 1 warning."));
    }

    [Test]
    public void StartScreenReadinessSummaryPrioritizesPendingCompilation()
    {
        var summary = GCStartScreenReadinessSummary.Create(null, true);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.PendingCompilation));
        Assert.That(summary.HasPendingItems, Is.True);
        Assert.That(summary.hasPendingCompilation, Is.True);
        Assert.That(summary.message, Does.Contain("waiting for Unity"));
    }

    [Test]
    public void WebGLExportChecklistRowReportsWarningWithTargetSetupAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var warningReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Warning,
            true,
            true,
            true,
            true,
            true,
            false,
            "Gaming Couch web export settings are ready, but the active build target is not WebGL.",
            Array.Empty<string>()
        );

        var readiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            warningReadiness
        );

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Warning);
        AssertAction(
            readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup),
            GCStartScreenReadinessActionId.SetUpWebGLExport,
            "Set Up Web Export"
        );
        Assert.That(readiness.HasBlockingVisibleChecklistIssues, Is.False);
        Assert.That(readiness.HasSafeAutomatableSetupActions, Is.False);
        Assert.That(readiness.AvailableChecklistSetupActionCount, Is.EqualTo(1));
    }

    [Test]
    public void WebGLExportChecklistRowBlockedDoesNotEnterGlobalSceneSetupAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var blockedReadiness = new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Blocked,
            false,
            false,
            false,
            false,
            false,
            true,
            "Gaming Couch web export settings are incomplete.",
            Array.Empty<string>()
        );

        var readiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            blockedReadiness
        );

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Blocked);
        Assert.That(readiness.HasBlockingVisibleChecklistIssues, Is.True);
        Assert.That(readiness.HasSafeAutomatableSetupActions, Is.False);
        Assert.That(readiness.AvailableChecklistSetupActionCount, Is.EqualTo(1));
    }

    [Test]
    public void WebGLExportChecklistRowMapsReadyAndNullReadinessStates()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var readyReadiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            CreateReadyWebGLExportReadiness()
        );
        var uninspectableReadiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            null
        );
        var readyWebGLCheck = readyReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);
        var uninspectableWebGLCheck = uninspectableReadiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);

        AssertCheck(readyReadiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Pass);
        Assert.That(readyWebGLCheck.label, Is.EqualTo("Web export settings configured"));
        Assert.That(readyWebGLCheck.IsSatisfied, Is.True);
        AssertCheck(uninspectableReadiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Fail);
        Assert.That(uninspectableWebGLCheck.label, Is.EqualTo("Web export settings configured"));
        Assert.That(uninspectableWebGLCheck.message, Does.Contain("could not be inspected"));
    }

    [Test]
    public void SetupActionAvailabilityIncludesLaunchReadinessRows()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var buildSettingsMissing = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            Array.Empty<EditorBuildSettingsScene>()
        );
        var gameViewReady = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9) },
            0,
            true,
            null
        );
        var buildSettingsOnlyReadiness = CreateReadiness(
            gamingCouch,
            listener,
            playerPrefab,
            buildSettingsMissing,
            gameViewReady,
            CreateReadyWebGLExportReadiness()
        );

        var buildSettingsReady = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );
        var gameViewMismatch = GamingCouchGameViewAspect.InspectSizeEntries(
            new[]
            {
                new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3),
                new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9),
            },
            0,
            true,
            null
        );
        var gameViewOnlyReadiness = CreateReadiness(
            gamingCouch,
            listener,
            playerPrefab,
            buildSettingsReady,
            gameViewMismatch,
            CreateReadyWebGLExportReadiness()
        );

        Assert.That(buildSettingsOnlyReadiness.HasSafeAutomatableSetupActions, Is.True);
        Assert.That(gameViewOnlyReadiness.HasSafeAutomatableSetupActions, Is.True);
    }


    [Test]
    public void PresentWebGLModuleReportsPassWithoutAction()
    {
        var readiness = CreateReadySceneReadiness(
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        );

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLModuleInstalled, GCStartScreenReadinessCheckState.Pass);
        AssertNoAction(readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLModuleInstalled));
    }

    [Test]
    public void MissingWebGLModuleReportsBlockerWithExternalOpenHubAction()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var readiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            CreateMissingModuleWebGLExportReadiness()
        );
        var moduleCheck = readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLModuleInstalled);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLModuleInstalled, GCStartScreenReadinessCheckState.Fail);
        AssertAction(moduleCheck, GCStartScreenReadinessActionId.OpenWebGLModuleInstallHelp, "Open Unity Hub");
        Assert.That(moduleCheck.HasExternalAction, Is.True);
        Assert.That(moduleCheck.message, Does.Contain("Web Build Support"));
        Assert.That(moduleCheck.message, Does.Contain("is not installed"));
        Assert.That(readiness.HasBlockingVisibleChecklistIssues, Is.True);
        // External actions are not automatable setup, so this row is a real blocker, not a "fix me".
        Assert.That(readiness.HasSafeAutomatableSetupActions, Is.False);
        Assert.That(readiness.IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId.WebGLModuleInstalled), Is.False);
    }

    [Test]
    public void WebGLExportRowDefersToModuleInstallWhenModuleMissing()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var readiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            CreateMissingModuleWebGLExportReadiness()
        );
        var exportCheck = readiness.GetCheck(GCStartScreenReadinessCheckId.WebGLExportSetup);

        AssertCheck(readiness, GCStartScreenReadinessCheckId.WebGLExportSetup, GCStartScreenReadinessCheckState.Blocked);
        AssertNoAction(exportCheck);
        Assert.That(exportCheck.message, Does.Contain("Install Web Build Support"));
    }

    [Test]
    public void MissingWebGLModuleMakesSummaryBlocked()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener);
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);

        var readiness = CreateStartScreenReadinessWithWebGL(
            gamingCouch,
            listener,
            playerPrefab,
            CreateMissingModuleWebGLExportReadiness()
        );

        var summary = GCStartScreenReadinessSummary.Create(readiness, false);

        Assert.That(summary.state, Is.EqualTo(GCStartScreenReadinessSummaryState.Blocked));
        Assert.That(summary.blockerCount, Is.GreaterThanOrEqualTo(1));
    }

    private GCStartScreenReadiness CreateReadySceneReadiness(
        GCActiveSceneBuildSettingsReadiness buildSettings,
        GCGameViewAspectReadiness gameViewAspect,
        GCWebGLExportReadiness webGLExport,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null
    )
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Game");
        var playerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        return CreateReadiness(gamingCouch, listener, playerPrefab, buildSettings, gameViewAspect, webGLExport, localPlayJson);
    }

    private GCStartScreenReadiness CreateReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null,
        bool hasSerializedListenerReference = false,
        bool hasMissingSerializedListenerReference = false,
        bool useDefaultWebGLExportReadiness = true
    )
    {
        var effectiveWebGLExport = useDefaultWebGLExportReadiness
            ? webGLExport ?? CreateReadyWebGLExportReadiness()
            : webGLExport;

        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            testScene,
            gamingCouch != null ? new[] { gamingCouch } : new GamingCouch[0],
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson ?? CreateValidLocalPlayJsonReadiness(),
            buildSettings ?? CreateReadyBuildSettingsReadiness(),
            gameViewAspect ?? CreateReadyGameViewAspectReadiness(),
            effectiveWebGLExport,
            hasSerializedListenerReference,
            hasMissingSerializedListenerReference
        ));
    }

    private static GCStartScreenReadiness CreateReadinessForScene(Scene scene)
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(scene));
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

    private static GCWebGLExportReadiness CreateMissingModuleWebGLExportReadiness()
    {
        return new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Blocked,
            false, // webGLModuleInstalled
            false,
            false,
            false,
            false,
            false,
            false,
            "WebGL Build Support is not installed.",
            Array.Empty<string>()
        );
    }

    private GCStartScreenReadiness CreateStartScreenReadinessWithWebGL(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCWebGLExportReadiness webGLExport
    )
    {
        return CreateReadiness(
            gamingCouch,
            listener,
            playerPrefab,
            webGLExport: webGLExport,
            useDefaultWebGLExportReadiness: false
        );
    }

    private GCStartScreenReadiness CreateReadyStartScreenReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab
    )
    {
        return CreateReadiness(gamingCouch, listener, playerPrefab);
    }

    private static void AssertCheck(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        GCStartScreenReadinessCheckState state
    )
    {
        Assert.That(readiness.GetCheck(id).state, Is.EqualTo(state));
    }

    private static void AssertCollapsedGamingCouchChecklist(GCStartScreenReadiness readiness)
    {
        var checklistIds = readiness.checklist.Select(check => check.id).ToArray();
        var gamingCouchRows = readiness.checklist
            .Where(check => check.id == GCStartScreenReadinessCheckId.GamingCouchInstance)
            .ToArray();
        var checklistLabels = readiness.checklist.Select(check => check.label).ToArray();

        Assert.That(gamingCouchRows, Has.Length.EqualTo(1));
        Assert.That(gamingCouchRows[0].label, Is.EqualTo("GamingCouch game object in scene"));
        AssertCheck(readiness, GCStartScreenReadinessCheckId.ActiveScene, GCStartScreenReadinessCheckState.Pass);
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.ActiveScene));
        Assert.That(checklistIds, Has.Member(GCStartScreenReadinessCheckId.GamingCouchInstance));
        Assert.That(checklistIds, Has.No.Member(GCStartScreenReadinessCheckId.SingleGamingCouchInstance));
        Assert.That(HasActiveSceneChecklistLabel(checklistLabels), Is.False);
        Assert.That(
            readiness.GetCheck(GCStartScreenReadinessCheckId.SingleGamingCouchInstance),
            Is.SameAs(readiness.GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance))
        );
    }

    private static bool HasActiveSceneChecklistLabel(string[] checklistLabels)
    {
        return checklistLabels.Any(label => string.Equals(label, "Active scene is available", StringComparison.Ordinal));
    }

    private static void AssertAction(
        GCStartScreenReadinessCheck check,
        GCStartScreenReadinessActionId expectedId,
        string expectedLabel
    )
    {
        Assert.That(check, Is.Not.Null);
        Assert.That(check.HasAction, Is.True);
        Assert.That(check.action.id, Is.EqualTo(expectedId));
        Assert.That(check.action.label, Is.EqualTo(expectedLabel));
    }

    private static void AssertNoAction(GCStartScreenReadinessCheck check)
    {
        Assert.That(check, Is.Not.Null);
        Assert.That(check.HasAction, Is.False);
        Assert.That(check.action.id, Is.EqualTo(GCStartScreenReadinessActionId.None));
        Assert.That(check.action.label, Is.Null);
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
