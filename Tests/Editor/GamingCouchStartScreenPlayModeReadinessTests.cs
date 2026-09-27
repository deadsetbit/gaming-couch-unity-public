using System;
using System.Collections;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Play Mode is the one state the Start Screen cannot reach from the active scene's roots:
// GamingCouch.Awake marks the object DontDestroyOnLoad, which moves it into a scene SceneManager
// does not enumerate. These tests drive the real transition rather than simulating it, because the
// move is Unity's, not the package's.
//
// Domain reload is disabled for the duration so the running test survives entering Play Mode. Scene
// reload stays on, so the transition behaves as it does for a developer pressing Play.
public sealed class GamingCouchStartScreenPlayModeReadinessTests
{
    private bool previousEnterPlayModeOptionsEnabled;
    private EnterPlayModeOptions previousEnterPlayModeOptions;
    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;

    [SetUp]
    public void SetUp()
    {
        previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
        previousBuildSettingsScenes = EditorBuildSettings.scenes;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

        // Never discard a saved scene the developer has open: reuse an untitled active scene, and
        // otherwise add one alongside theirs. Mirrors GamingCouchStartScreenReadinessTests.
        previousActiveScene = SceneManager.GetActiveScene();
        if (previousActiveScene.IsValid() &&
            previousActiveScene.isLoaded &&
            string.IsNullOrEmpty(previousActiveScene.path))
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

    // SetActiveScene returns false for the scene that is already active, so ask first.
    private static void EnsureSceneIsActive(Scene scene)
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.handle == scene.handle)
        {
            return;
        }

        Assert.That(SceneManager.SetActiveScene(scene), Is.True);
    }

    [UnityTearDown]
    public IEnumerator UnityTearDown()
    {
        if (EditorApplication.isPlaying)
        {
            yield return ExitPlayMode();
        }

        LogAssert.ignoreFailingMessages = false;
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
        EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
        EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;

        if (testScene.IsValid() && testScene.isLoaded)
        {
            ClearSceneRootObjects(testScene);
        }

        if (!testSceneWasCreatedAdditively)
        {
            yield break;
        }

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousActiveScene);
        }

        if (testScene.IsValid() && testScene.isLoaded)
        {
            EditorSceneManager.CloseScene(testScene, true);
        }
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

    [UnityTest]
    public IEnumerator WiredSceneStaysReadyWhilePlaying()
    {
        CreateWiredActiveScene();
        AssertGamingCouchChecksPass("before entering Play Mode");
        var setupActionsBeforePlay = GCStartScreenReadinessService
            .InspectActiveScene()
            .SafeAutomatableSetupActionCount;

        yield return EnterPlayMode();

        // The bug only exists because Play Mode empties the active scene's roots of the object. If
        // Unity ever stopped moving it, the rest of this test would pass while covering nothing.
        Assert.That(
            GamingCouchSceneWiring.FindActiveSceneGamingCouches(),
            Is.Empty,
            "Play Mode did not move the GamingCouch out of the active scene, so this test proves nothing"
        );

        AssertGamingCouchChecksPass("during Play Mode");
        var instanceCheck = InspectGamingCouchInstanceCheck();
        Assert.That(
            instanceCheck.action.id,
            Is.Not.EqualTo(GCStartScreenReadinessActionId.CreateGamingCouch),
            "the Start Screen offered to create a GamingCouch that the playing scene already has"
        );
        Assert.That(
            GCStartScreenReadinessService.InspectActiveScene().SafeAutomatableSetupActionCount,
            Is.EqualTo(setupActionsBeforePlay),
            "entering Play Mode added work to \"Set up missing pieces\""
        );

        yield return ExitPlayMode();

        AssertGamingCouchChecksPass("after leaving Play Mode");
    }

    [UnityTest]
    public IEnumerator SceneSetupActionsDuringPlayModeRefuseInTheWindow()
    {
        CreateWiredActiveScene();

        yield return EnterPlayMode();

        var gamingCouchesBefore = CountLoadedGamingCouches();
        var result = GamingCouchStartScreenSetupActions.RunSetupAction(
            GCStartScreenReadinessActionId.CreateGamingCouch
        );

        Assert.That(
            GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType),
            Is.True,
            "the refusal never reached the window: " + result.message
        );
        Assert.That(result.message, Does.Contain("Play Mode"));

        // Destroy() defers to the end of the frame, so a duplicate would still be counted now.
        yield return null;
        yield return null;
        Assert.That(
            CountLoadedGamingCouches(),
            Is.EqualTo(gamingCouchesBefore),
            "a second GamingCouch was created during Play Mode"
        );
    }

    [UnityTest]
    public IEnumerator SetUpMissingPiecesDuringPlayModeChangesNoBuildSettings()
    {
        CreateWiredActiveScene();
        EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();

        yield return EnterPlayMode();

        var result = GamingCouchStartScreenSetupActions.RunActiveSceneSetup();

        Assert.That(
            GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(result.messageType),
            Is.True,
            "the refusal never reached the window: " + result.message
        );
        Assert.That(result.message, Does.Contain("Play Mode"));
        Assert.That(
            EditorBuildSettings.scenes,
            Is.Empty,
            "Build Settings were rewritten during Play Mode"
        );
    }

    private static int CountLoadedGamingCouches()
    {
        return UnityEngine.Object
            .FindObjectsByType<GamingCouch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Length;
    }

    // Builds into the active scene, which SetUp has already made the fixture's own.
    private static void CreateWiredActiveScene()
    {
        var gamingCouchObject = new GameObject("GamingCouch");
        gamingCouchObject.SetActive(false);
        var gamingCouch = gamingCouchObject.AddComponent<GamingCouch>();
        GamingCouchSceneWiring.AssignListenerIfMissing(
            gamingCouch,
            GamingCouchEditorTestSupport.CreateCompatibleListener("Game")
        );
        GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(
            gamingCouch,
            GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Player")
        );
        gamingCouchObject.SetActive(true);
    }

    private static void AssertGamingCouchChecksPass(string phase)
    {
        var readiness = GCStartScreenReadinessService.InspectActiveScene();

        Assert.That(readiness.gamingCouches, Has.Length.EqualTo(1), "GamingCouch objects found " + phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.GamingCouchInstance, phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.ListenerAssigned, phase);
        AssertCheckPasses(readiness, GCStartScreenReadinessCheckId.PlayerPrefabAssigned, phase);
    }

    private static void AssertCheckPasses(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        string phase
    )
    {
        var check = readiness.GetCheck(id);
        Assert.That(
            check.state,
            Is.EqualTo(GCStartScreenReadinessCheckState.Pass),
            id + " " + phase + ": " + check.message
        );
    }

    private static GCStartScreenReadinessCheck InspectGamingCouchInstanceCheck()
    {
        return GCStartScreenReadinessService
            .InspectActiveScene()
            .GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance);
    }

    // Entering Play Mode runs the whole GamingCouch runtime, whose logging depends on the host
    // project's local play configuration rather than on anything under test here.
    private static IEnumerator EnterPlayMode()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorApplication.EnterPlaymode();
        yield return WaitForPlayMode(true);
        LogAssert.ignoreFailingMessages = true;
        yield return null;
    }

    private static IEnumerator ExitPlayMode()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorApplication.ExitPlaymode();
        yield return WaitForPlayMode(false);
        LogAssert.ignoreFailingMessages = true;
        yield return null;
    }

    private static IEnumerator WaitForPlayMode(bool playing)
    {
        var deadlineSeconds = Time.realtimeSinceStartup + 30f;
        while (EditorApplication.isPlaying != playing && Time.realtimeSinceStartup < deadlineSeconds)
        {
            yield return null;
        }

        Assert.That(
            EditorApplication.isPlaying,
            Is.EqualTo(playing),
            playing ? "the editor never entered Play Mode" : "the editor never left Play Mode"
        );
    }
}
