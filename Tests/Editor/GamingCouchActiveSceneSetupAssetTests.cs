using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GamingCouchActiveSceneSetupAssetTests
{
    private const string SuppressAutoOpenKey = "DSB.GC.StartScreen.SuppressAutoOpen";
    private const string TestFolderAssetPathPrefix = "Assets/GamingCouchActiveSceneSetupAssetTests_";
    private const string ExistingSceneBuildPath = "Assets/GamingCouchExistingScene.unity";
    private const string TestSceneBuildPath = "Assets/GamingCouchActiveSceneSetupAssetTestScene.unity";
    private const string OtherSceneBuildPath = "Assets/GamingCouchOtherScene.unity";

    private string previousSuppressAutoOpenConfigValue;
    private EditorBuildSettingsScene[] previousBuildSettingsScenes;
    private string testFolderAssetPath;
    private Scene previousActiveScene;
    private Scene testScene;
    private bool testSceneWasCreatedAdditively;
    private string previousWebGLTemplate;
    private WebGLCompressionFormat previousWebGLCompressionFormat;
    private bool previousWebGLDataCaching;
    private WebGLExceptionSupport previousWebGLExceptionSupport;
    private WebGLDebugSymbolMode previousWebGLDebugSymbolMode;
#if UNITY_2023_1_OR_NEWER
    private bool previousWebGLWasm2023;
#endif
    // Held as object because UnityEditor.WebGL.WasmCodeOptimization ships with the
    // optional WebGL Build Support module; GCWebGLBuildSupport reads/writes it via
    // reflection so this test assembly compiles with or without the module.
    private object previousWebGLCodeOptimization;
    private bool previousDevelopmentBuild;
    private Il2CppCodeGeneration previousIl2CppCodeGeneration;
    private ManagedStrippingLevel previousManagedStrippingLevel;
    private bool previousStripUnusedMeshComponents;
    private bool previousSplashScreenShow;
    private bool previousSplashScreenShowUnityLogo;
    private bool createdExampleProjectFolderForCollisionTest;
    private bool createdExampleFolderForCollisionTest;
    private bool createdExampleGameScriptPathCollisionForTest;
    private bool createdExamplePlayerScriptPathCollisionForTest;
    private bool createdExampleTemplateScriptPathCollisionForTest;

    [SetUp]
    public void SetUp()
    {
        previousSuppressAutoOpenConfigValue = EditorUserSettings.GetConfigValue(SuppressAutoOpenKey);

        // Snapshot build settings WITHOUT any scenes leaked by an interrupted prior run of this
        // test. EditorBuildSettings.scenes persists to disk the moment it is set, so a run killed
        // before TearDown leaves its temp scene paths in ProjectSettings/EditorBuildSettings.asset.
        // Those dangling paths then break Player builds (the scene file is gone) and have been
        // committed to the template before. Sanitizing the snapshot restores clean state in TearDown
        // and, when a leak is already present, heals it here so it cannot reach a built player or a
        // commit. Mirrors GamingCouchStartScreenEditorSmokeTests.
        previousBuildSettingsScenes = WithoutLeakedTestScenes(EditorBuildSettings.scenes);
        if (previousBuildSettingsScenes.Length != EditorBuildSettings.scenes.Length)
        {
            EditorBuildSettings.scenes = previousBuildSettingsScenes;
        }
        testFolderAssetPath = TestFolderAssetPathPrefix + Guid.NewGuid().ToString("N");
        previousActiveScene = SceneManager.GetActiveScene();
        SaveWebGLSettings();

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

    [TearDown]
    public void TearDown()
    {
        var cleanupErrors = new List<Exception>();

        RunCleanup(RestoreActiveSceneAndCloseTestScene, cleanupErrors);
        RunCleanup(DeleteGeneratedScriptPathCollisionTestAssets, cleanupErrors);
        RunCleanup(DeleteTestAssetFolder, cleanupErrors);
        RunCleanup(RestoreBuildSettings, cleanupErrors);
        RunCleanup(RestoreSuppressAutoOpenSetting, cleanupErrors);
        RunCleanup(RestoreWebGLSettings, cleanupErrors);
        if (cleanupErrors.Count > 0)
        {
            throw new AggregateException(cleanupErrors);
        }
    }

    [Test]
    public void SceneWiringPreservesExistingListenerAndPlayerPrefabReferences()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var existingListener = new GameObject("Existing Listener");
        var replacementListener = new GameObject("Replacement Listener");
        var existingPlayerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Existing Player Prefab");
        var replacementPlayerPrefab = GamingCouchEditorTestSupport.CreatePlayerPrefabObject("Replacement Player Prefab");

        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, existingListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, existingPlayerPrefab).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var listenerRerun = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, replacementListener);
        var playerPrefabRerun = GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, replacementPlayerPrefab);

        Assert.That(listenerRerun.status, Is.EqualTo(GamingCouchSceneWiringStatus.Unchanged));
        Assert.That(playerPrefabRerun.status, Is.EqualTo(GamingCouchSceneWiringStatus.Unchanged));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(existingListener));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName), Is.SameAs(existingPlayerPrefab));
    }

    [Test]
    public void SceneWiringReplacesMissingListenerReference()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var deletedListener = GamingCouchEditorTestSupport.CreateCompatibleListener("Deleted Game");
        var replacementListener = GamingCouchEditorTestSupport.CreateCompatibleListener("Replacement Game");
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, deletedListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(deletedListener);

        Assert.That(GamingCouchSceneWiring.HasMissingObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);

        var result = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, replacementListener);

        Assert.That(result.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(result.changed, Is.True);
        Assert.That(result.message, Does.Contain("Replaced the missing"));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(replacementListener));
        Assert.That(GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);
    }

    [Test]
    public void ActiveSceneSetupDefaultSpecUsesGCExampleTemplateAndStockPlayer()
    {
        // The default active-scene setup (incl. "Create example scene") wires the barebones template
        // + the stock GCPlayer, which has no generated player script.
        var missingPiecesSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupAction.ActiveSceneMissingPieces
        );

        Assert.That(missingPiecesSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ExampleFolderAssetPath));
        Assert.That(missingPiecesSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCExampleTemplate.cs"));
        Assert.That(missingPiecesSpec.playerScriptAssetPath, Is.Null);
        Assert.That(missingPiecesSpec.playerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCPlayer.prefab"));
        Assert.That(missingPiecesSpec.gameTypeName, Is.EqualTo("GCExampleTemplate"));
        Assert.That(missingPiecesSpec.playerTypeName, Is.EqualTo("GCPlayer"));
        Assert.That(missingPiecesSpec.listenerObjectName, Is.EqualTo("Game"));
        Assert.That(missingPiecesSpec.requiresGeneratedScriptFolder, Is.True);
    }

    [Test]
    public void WireExampleGameSpecUsesGCExampleGameAndPlayerAssets()
    {
        // The additive "Wire example game" action selects the full game flavor.
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupAction.ActiveSceneWireExampleGame
        );

        Assert.That(gameSpec.scriptFolderAssetPath, Is.EqualTo(GamingCouchActiveSceneSetup.ExampleFolderAssetPath));
        Assert.That(gameSpec.gameScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCExampleGame.cs"));
        Assert.That(gameSpec.playerScriptAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCExamplePlayer.cs"));
        Assert.That(gameSpec.playerPrefabAssetPath, Is.EqualTo("Assets/GamingCouch/GCExample/GCExamplePlayer.prefab"));
        Assert.That(gameSpec.gameTypeName, Is.EqualTo("GCExampleGame"));
        Assert.That(gameSpec.playerTypeName, Is.EqualTo("GCExamplePlayer"));
        Assert.That(gameSpec.listenerObjectName, Is.EqualTo("Game"));
        Assert.That(gameSpec.requiresGeneratedScriptFolder, Is.True);
    }

    [Test]
    public void GeneratedActiveSceneGameSourceEqualsCanonicalGameMasterAfterRename()
    {
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupAction.ActiveSceneWireExampleGame
        );

        var generated = gameSpec.BuildGameScriptSource();

        AssertGeneratedEqualsCanonicalMaster(
            generated,
            GamingCouchActiveSceneSetup.ExampleGameMasterTypeName,
            new Dictionary<string, string>
            {
                { GamingCouchActiveSceneSetup.ExampleGameMasterTypeName, GamingCouchActiveSceneSetup.ExampleGameTypeName },
                { GamingCouchActiveSceneSetup.ExamplePlayerMasterTypeName, GamingCouchActiveSceneSetup.ExamplePlayerTypeName },
            }
        );
        AssertGeneratedSourceShape(generated, "public class GCExampleGame : MonoBehaviour");
    }

    [Test]
    public void GeneratedActiveScenePlayerSourceEqualsCanonicalPlayerMasterAfterRename()
    {
        var gameSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(
            GCActiveSceneSetupAction.ActiveSceneWireExampleGame
        );

        var generated = gameSpec.BuildPlayerScriptSource();

        AssertGeneratedEqualsCanonicalMaster(
            generated,
            GamingCouchActiveSceneSetup.ExamplePlayerMasterTypeName,
            new Dictionary<string, string>
            {
                { GamingCouchActiveSceneSetup.ExamplePlayerMasterTypeName, GamingCouchActiveSceneSetup.ExamplePlayerTypeName },
            }
        );
        AssertGeneratedSourceShape(generated, "public class GCExamplePlayer : GCPlayer");
    }

    [Test]
    public void GeneratedTemplateSourceEqualsCanonicalTemplateMasterAfterRename()
    {
        var masterText = GamingCouchActiveSceneSetup.ReadCanonicalMasterSource(
            GamingCouchActiveSceneSetup.ExampleTemplateMasterTypeName
        );

        var generated = GamingCouchActiveSceneSetup.RewriteCanonicalMasterToGeneratedSource(
            masterText,
            new Dictionary<string, string>
            {
                { GamingCouchActiveSceneSetup.ExampleTemplateMasterTypeName, GamingCouchActiveSceneSetup.ExampleTemplateTypeName },
            },
            true
        );

        AssertGeneratedSourceShape(generated, "public class GCExampleTemplate : MonoBehaviour");
    }

    [Test]
    public void ExamplePlayerPrefabWiresVisiblePlaceholderRendererToPlayerColorField()
    {
        // Generate into the per-test temp folder (cleaned up by DeleteTestAssetFolder) so the test
        // never creates assets in the real Assets/GamingCouch/GCExample and cannot corrupt or reuse a
        // real example prefab.
        EnsureTestAssetFolder();
        var playerPrefabAssetPath = testFolderAssetPath + "/" + nameof(ColorPlaceholderPrefabPlayer) + ".prefab";
        var context = new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            false,
            testFolderAssetPath,
            testFolderAssetPath + "/GameScript.cs",
            testFolderAssetPath + "/PlayerScript.cs",
            playerPrefabAssetPath,
            nameof(CompatibleGameScriptReceiver),
            nameof(ColorPlaceholderPrefabPlayer),
            "Game",
            typeof(CompatibleGameScriptReceiver),
            typeof(ColorPlaceholderPrefabPlayer)
        );

        var result = GamingCouchActiveSceneSetup.EnsureExamplePlayerPrefab(context);

        Assert.That(result.IsBlocked, Is.False, string.Join("\n", result.blockedReasons));
        Assert.That(result.changed, Is.True);
        Assert.That(result.prefab, Is.Not.Null);
        Assert.That(result.prefab.name, Is.EqualTo(nameof(ColorPlaceholderPrefabPlayer)));

        var player = result.prefab.GetComponent<ColorPlaceholderPrefabPlayer>();
        var visual = result.prefab.transform.Find("Visual");
        Assert.That(player, Is.Not.Null);
        Assert.That(visual, Is.Not.Null);

        var renderer = visual.GetComponent<Renderer>();
        Assert.That(renderer, Is.Not.Null);

        var serializedPlayer = new SerializedObject(player);
        var colorRendererProperty = serializedPlayer.FindProperty("colorRenderer");
        Assert.That(colorRendererProperty, Is.Not.Null);
        Assert.That(colorRendererProperty.objectReferenceValue, Is.SameAs(renderer));
    }

    [Test]
    public void ActiveScenePlayerPrefabSetupCreatesPlayerPrefabForCompiledPlayerType()
    {
        // Generate into the per-test temp folder (cleaned up by DeleteTestAssetFolder) so the test
        // never creates assets in the real Assets/GamingCouch/GCExample.
        EnsureTestAssetFolder();
        var playerPrefabAssetPath = testFolderAssetPath + "/" + nameof(GCExamplePlayerFixture) + ".prefab";
        var context = new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            false,
            testFolderAssetPath,
            testFolderAssetPath + "/GameScript.cs",
            testFolderAssetPath + "/PlayerScript.cs",
            playerPrefabAssetPath,
            nameof(GCExampleGameFixture),
            nameof(GCExamplePlayerFixture),
            "Game",
            typeof(GCExampleGameFixture),
            typeof(GCExamplePlayerFixture)
        );

        var result = GamingCouchActiveSceneSetup.EnsureExamplePlayerPrefab(context);

        Assert.That(result.IsBlocked, Is.False, string.Join("\n", result.blockedReasons));
        Assert.That(result.changed, Is.True);
        Assert.That(result.prefab, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(result.prefab), Is.EqualTo(playerPrefabAssetPath));
        Assert.That(result.prefab.GetComponent<GCExamplePlayerFixture>(), Is.Not.Null);
    }

    [Test]
    public void GameListenerSetupCreatesNamedGameObjectAndLeavesPlayerPrefabEmpty()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);
        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject.name, Is.EqualTo("Game"));
        Assert.That(listenerResult.listenerObject.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
        Assert.That(assignResult.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(listenerResult.listenerObject));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName), Is.Null);
    }

    [Test]
    public void GameListenerSetupReplacesMissingListenerReference()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var deletedListener = GamingCouchEditorTestSupport.CreateCompatibleListener("Deleted Game");
        var context = CreateGameListenerTestContext();
        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, deletedListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        UnityEngine.Object.DestroyImmediate(deletedListener);

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);
        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject.name, Is.EqualTo("Game"));
        Assert.That(listenerResult.blockedReasons, Is.Empty);
        Assert.That(assignResult.status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));
        Assert.That(assignResult.message, Does.Contain("Replaced the missing"));
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(listenerResult.listenerObject));
        Assert.That(GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.True);
    }

    [Test]
    public void GameListenerSetupReusesNamedGameObjectBeforeAddingComponent()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var existingGame = new GameObject("Game");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.True);
        Assert.That(listenerResult.listenerObject, Is.SameAs(existingGame));
        Assert.That(existingGame.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
    }

    [Test]
    public void GameListenerSetupBlocksExistingGameComponentOnWrongObjectName()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var existingListener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var context = CreateGameListenerTestContext();

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.True);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(string.Join("\n", listenerResult.blockedReasons), Does.Contain("scene object named Game"));
        Assert.That(existingListener.GetComponent<CompatibleGameScriptReceiver>(), Is.Not.Null);
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void GameListenerSetupPreservesOccupiedListenerField()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var existingListener = GamingCouchEditorTestSupport.CreateCompatibleListener("Existing Listener");
        var context = CreateGameListenerTestContext();

        Assert.That(GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, existingListener).status, Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded));

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.False);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.SameAs(existingListener));
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void GameListenerSetupBlocksIncompatibleGameComponentType()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var context = CreateGameListenerTestContext(typeof(SetupOnlyGameScriptReceiver));

        var listenerResult = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListener(context, gamingCouch);

        Assert.That(listenerResult.IsBlocked, Is.True);
        Assert.That(listenerResult.changed, Is.False);
        Assert.That(listenerResult.listenerObject, Is.Null);
        Assert.That(GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName), Is.Null);
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void CreateAndWireBlocksGCExampleTemplateScriptPathCollisionWithoutOverwrite()
    {
        // The default create-and-wire flow generates the barebones GCExampleTemplate; a folder
        // occupying its generated script path must block generation, never overwrite (ADR 0016).
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchActiveSceneSetup.ExampleTemplateScriptAssetPath);
        EnsureGCExampleFolderForTest(
            ref createdExampleProjectFolderForCollisionTest,
            ref createdExampleFolderForCollisionTest
        );
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ExampleTemplateScriptAssetPath,
            ref createdExampleTemplateScriptPathCollisionForTest
        );
        // Deliberately not imported into the AssetDatabase: a folder whose name ends in ".cs" sends
        // Unity's script importer into a re-import loop. The generator blocks on a raw Directory.Exists
        // check, so importing the collision folder is unnecessary.
        GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var result = GamingCouchActiveSceneSetup.EnsureActiveSceneGameListenerReference();

        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.changed, Is.False);
        Assert.That(result.status, Is.EqualTo(GCActiveSceneSetupStatus.Blocked));
        AssertHasEntryContaining(result.details, "Cannot create the example script Assets/GamingCouch/GCExample/GCExampleTemplate.cs because a folder (not a script file) already exists at that path.");
        Assert.That(testScene.GetRootGameObjects().Any(root => root != null && root.name == "Game"), Is.False);
    }

    [Test]
    public void WireExampleGameGuardRejectsSceneWithoutGamingCouch()
    {
        // The active test scene has no GamingCouch, so the template-first guard rejects it.
        var isTemplateScene = GamingCouchActiveSceneSetup.TryGetTemplateSceneGamingCouch(out var gamingCouch, out var message);

        Assert.That(isTemplateScene, Is.False);
        Assert.That(gamingCouch, Is.Null);
        Assert.That(message, Does.Contain("Create New Example Scene"));
    }

    [Test]
    public void WireExampleGameBlocksWhenSceneIsNotTemplateScene()
    {
        // Drive the real entry point (not just the extracted guard): the active test scene has no
        // GamingCouch, so WireExampleGame must return Blocked with the "create the template scene
        // first" message and generate nothing.
        var result = GamingCouchActiveSceneSetup.WireExampleGame();

        Assert.That(result.IsBlocked, Is.True);
        Assert.That(result.IsWired, Is.False);
        Assert.That(result.changed, Is.False);
        Assert.That(result.message, Does.Contain("Create New Example Scene"));
    }

    [Test]
    public void WireExampleGameScriptsReadySwapReplacesListenerComponentAndPlayerPrefab()
    {
        // The heart of "Wire example game": after the generated GCExampleGame/GCExamplePlayer scripts
        // compile, the scripts-ready continuation swaps the listener's example component to the game
        // type and repoints GamingCouch.playerPrefab at the generated player prefab. Drive that
        // continuation directly with compiled fixture stand-ins — a real domain reload cannot be
        // awaited inside one EditMode test. Template-component removal is keyed to the generated
        // "GCExampleTemplate" type name, which by design has no compiled stand-in (see
        // GCExampleGameFixture's note), so that branch and the post-reload dispatch stay manually
        // verified; the component add + player-prefab repoint are the swap behavior covered here.
        EnsureTestAssetFolder();
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");
        var listener = new GameObject("Game");
        Assert.That(
            GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listener).status,
            Is.EqualTo(GamingCouchSceneWiringStatus.Succeeded)
        );

        var playerPrefabAssetPath = testFolderAssetPath + "/" + nameof(GCExamplePlayerFixture) + ".prefab";
        var context = new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupAction.ActiveSceneWireExampleGame,
            true,
            testFolderAssetPath,
            testFolderAssetPath + "/GameScript.cs",
            testFolderAssetPath + "/PlayerScript.cs",
            playerPrefabAssetPath,
            nameof(GCExampleGameFixture),
            nameof(GCExamplePlayerFixture),
            "Game",
            typeof(GCExampleGameFixture),
            typeof(GCExamplePlayerFixture)
        );

        GamingCouchActiveSceneSetup.SwapListenerToExampleGameOnScriptsReady(context);

        Assert.That(
            listener.GetComponent<GCExampleGameFixture>(),
            Is.Not.Null,
            "swap did not add the example game component to the listener"
        );
        Assert.That(
            GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName),
            Is.SameAs(listener),
            "swap must keep pointing at the same wired listener object"
        );

        var playerPrefab = GamingCouchSceneWiring.ReadObjectReference(
            gamingCouch,
            GamingCouchSceneWiring.PlayerPrefabPropertyName
        ) as GameObject;
        Assert.That(playerPrefab, Is.Not.Null, "swap did not repoint playerPrefab to the example player prefab");
        Assert.That(AssetDatabase.GetAssetPath(playerPrefab), Is.EqualTo(playerPrefabAssetPath));
        Assert.That(playerPrefab.GetComponent<GCExamplePlayerFixture>(), Is.Not.Null);
    }

    [Test]
    public void WireExampleGameGuardRejectsSceneNotWiredToTemplate()
    {
        // A GamingCouch with no listener (or a listener without GCExampleTemplate) is not the
        // template scene "Wire example game" upgrades, so the guard rejects it.
        GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch");

        var isTemplateScene = GamingCouchActiveSceneSetup.TryGetTemplateSceneGamingCouch(out var gamingCouch, out var message);

        Assert.That(isTemplateScene, Is.False);
        Assert.That(gamingCouch, Is.Null);
        Assert.That(message, Does.Contain("Create New Example Scene"));
    }

    [Test]
    public void RemoveBlockingExampleAssetFoldersClearsScriptPathCollisions()
    {
        ReserveGCExampleGameAndPlayerScriptPathsForCollisionTest();
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        CreateScriptPathCollisionDirectory(
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        // Deliberately not imported into the AssetDatabase (a ".cs"-named folder loops Unity's
        // importer); detection and removal both operate on the raw filesystem.

        Assert.That(
            GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(),
            Is.EquivalentTo(new[]
            {
                GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
                GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            })
        );

        var cleanup = GamingCouchActiveSceneSetup.RemoveBlockingExampleAssetFolders();

        Assert.That(cleanup.IsBlocked, Is.False);
        Assert.That(cleanup.changed, Is.True);
        Assert.That(
            cleanup.removedAssetPaths,
            Is.EquivalentTo(new[]
            {
                GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
                GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            })
        );
        Assert.That(GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(), Is.Empty);
        Assert.That(Directory.Exists(AssetPathToFullPath(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath)), Is.False);
        Assert.That(Directory.Exists(AssetPathToFullPath(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath)), Is.False);
    }

    [Test]
    public void BlockingFolderRemovalConfirmationIsSkippedInBatchMode()
    {
        // Wire example game confirms before moving a blocking folder to the Trash, because the folder
        // may hold the user's own work. A batch run has nobody to answer the dialog, so it must
        // proceed unprompted. Only the gate is exercised: raising the real dialog would hang an
        // interactive test run.
        var blockingFolders = new[] { GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath };

        Assert.That(
            GamingCouchActiveSceneSetup.ShouldConfirmBlockingFolderRemoval(blockingFolders, true),
            Is.False,
            "batch mode must never raise the confirmation dialog"
        );
        Assert.That(
            GamingCouchActiveSceneSetup.ShouldConfirmBlockingFolderRemoval(blockingFolders, false),
            Is.True,
            "an interactive run must confirm before trashing a folder"
        );
        Assert.That(
            GamingCouchActiveSceneSetup.ShouldConfirmBlockingFolderRemoval(new string[0], false),
            Is.False,
            "nothing to remove means nothing to confirm"
        );
    }

    [Test]
    public void EnsureProjectFolderRecursiveCreatesEveryMissingLevel()
    {
        var nestedFolderAssetPath = testFolderAssetPath + "/Nested/Deeper";
        var blockedReasons = new List<string>();

        Assert.That(
            GamingCouchActiveSceneSetup.EnsureProjectFolderRecursive(nestedFolderAssetPath, blockedReasons),
            Is.True
        );

        Assert.That(blockedReasons, Is.Empty);
        Assert.That(AssetDatabase.IsValidFolder(testFolderAssetPath), Is.True);
        Assert.That(AssetDatabase.IsValidFolder(testFolderAssetPath + "/Nested"), Is.True);
        Assert.That(AssetDatabase.IsValidFolder(nestedFolderAssetPath), Is.True);
    }

    [Test]
    public void EnsureProjectFolderRecursiveRefusesToBuildAPathThroughAFile()
    {
        // The recursive wrapper delegates each level to the checked single-segment helper, so an
        // existing file where a folder must go is reported instead of silently worked around.
        EnsureTestAssetFolder();
        var fileAssetPath = testFolderAssetPath + "/Nested";
        File.WriteAllText(AssetPathToFullPath(fileAssetPath), "not a folder");
        AssetDatabase.ImportAsset(fileAssetPath, ImportAssetOptions.ForceSynchronousImport);
        var blockedReasons = new List<string>();

        Assert.That(
            GamingCouchActiveSceneSetup.EnsureProjectFolderRecursive(
                fileAssetPath + "/Deeper",
                blockedReasons
            ),
            Is.False
        );

        AssertHasEntryContaining(blockedReasons, "Cannot create folder " + fileAssetPath);
        Assert.That(AssetDatabase.IsValidFolder(fileAssetPath), Is.False);
    }

    [Test]
    public void GeneratedAssetCreationReusesExistingFileWithoutOverwriting()
    {
        EnsureTestAssetFolder();
        var assetPath = testFolderAssetPath + "/ExistingGeneratedAsset.txt";
        var fullPath = AssetPathToFullPath(assetPath);
        const string OriginalContent = "user edits stay";
        var createdAssetPaths = new List<string>();
        var reusedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();

        File.WriteAllText(fullPath, OriginalContent);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        GamingCouchActiveSceneSetup.EnsureGeneratedAssetFileWithoutOverwrite(
            assetPath,
            "generated replacement",
            createdAssetPaths,
            reusedAssetPaths,
            blockedReasons
        );

        Assert.That(blockedReasons, Is.Empty);
        Assert.That(createdAssetPaths, Is.Empty);
        Assert.That(reusedAssetPaths, Is.EquivalentTo(new[] { assetPath }));
        Assert.That(File.ReadAllText(fullPath), Is.EqualTo(OriginalContent));
    }

    [Test]
    public void BuildSettingsReadinessReportsActiveSceneFirstEnabledState()
    {
        var readiness = GamingCouchBuildSettingsReadiness.InspectScenePath(
            true,
            TestSceneBuildPath,
            new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
        );

        Assert.That(readiness.IsReady, Is.True);
        Assert.That(readiness.status, Is.EqualTo(GCActiveSceneBuildSettingsStatus.Ready));
        Assert.That(readiness.firstMatchingIndex, Is.EqualTo(0));
        Assert.That(readiness.matchingEntryCount, Is.EqualTo(1));
    }

    [Test]
    public void BuildSettingsReadinessReportsNonReadyStates()
    {
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(true, null, Array.Empty<EditorBuildSettingsScene>()).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.UnsavedActiveScene)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(ExistingSceneBuildPath, true) }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Missing)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, false) }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Disabled)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[]
                {
                    new EditorBuildSettingsScene(ExistingSceneBuildPath, true),
                    new EditorBuildSettingsScene(TestSceneBuildPath, true),
                }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.NotFirst)
        );
        Assert.That(
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[]
                {
                    new EditorBuildSettingsScene(TestSceneBuildPath, true),
                    new EditorBuildSettingsScene(OtherSceneBuildPath, true),
                    new EditorBuildSettingsScene(TestSceneBuildPath, false),
                }
            ).status,
            Is.EqualTo(GCActiveSceneBuildSettingsStatus.Duplicate)
        );
    }

    [Test]
    public void BuildSettingsSetupMovesActiveSceneFirstEnabledAndPreservesUnrelatedScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ExistingSceneBuildPath, false),
            new EditorBuildSettingsScene(TestSceneBuildPath, false),
            new EditorBuildSettingsScene(OtherSceneBuildPath, true),
            new EditorBuildSettingsScene(TestSceneBuildPath, true),
        };

        var result = GamingCouchBuildSettingsReadiness.SetSceneFirstEnabled(TestSceneBuildPath);
        var scenes = EditorBuildSettings.scenes;

        Assert.That(result.IsBlocked, Is.False);
        Assert.That(result.changed, Is.True);
        Assert.That(scenes.Select(scene => scene.path).ToArray(), Is.EqualTo(new[]
        {
            TestSceneBuildPath,
            ExistingSceneBuildPath,
            OtherSceneBuildPath,
        }));
        Assert.That(scenes.Select(scene => scene.enabled).ToArray(), Is.EqualTo(new[]
        {
            true,
            false,
            true,
        }));
    }

    [Test]
    public void GameViewAspectLogicReportsReadyMismatchAndUnknownStates()
    {
        var entries = new[]
        {
            new GCGameViewSizeEntry(0, "Free Aspect", 0, 0),
            new GCGameViewSizeEntry(1, "16:9 Aspect", 16, 9),
            new GCGameViewSizeEntry(2, "1920x1080", 0, 0),
        };

        var ready = GamingCouchGameViewAspect.InspectSizeEntries(entries, 1, true, null);
        var mismatch = GamingCouchGameViewAspect.InspectSizeEntries(entries, 0, true, null);
        var unknown = GamingCouchGameViewAspect.InspectSizeEntries(entries, -1, true, "Game View is closed.");
        var mismatchWithoutExisting16By9 = GamingCouchGameViewAspect.InspectSizeEntries(
            new[] { new GCGameViewSizeEntry(0, "4:3 Aspect", 4, 3) },
            0,
            true,
            null
        );

        Assert.That(ready.status, Is.EqualTo(GCGameViewAspectStatus.Ready));
        Assert.That(ready.HasSafeSelectionAction, Is.False);
        Assert.That(mismatch.status, Is.EqualTo(GCGameViewAspectStatus.Mismatch));
        Assert.That(mismatch.HasSafeSelectionAction, Is.True);
        Assert.That(mismatch.existing16By9Entry.index, Is.EqualTo(1));
        Assert.That(unknown.status, Is.EqualTo(GCGameViewAspectStatus.Unknown));
        Assert.That(unknown.HasSafeSelectionAction, Is.True);
        Assert.That(mismatchWithoutExisting16By9.status, Is.EqualTo(GCGameViewAspectStatus.Mismatch));
        Assert.That(mismatchWithoutExisting16By9.HasSafeSelectionAction, Is.False);
        Assert.That(GamingCouchGameViewAspect.Is16By9(entries[2]), Is.True);
    }

    [Test]
    public void WebGLTemplateInstallationCreatesMissingProjectTemplateFiles()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("fresh template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            var result = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.changed, Is.True);
            Assert.That(Directory.Exists(destinationDirectory), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(destinationDirectory, "index.html")), Is.EqualTo("fresh template source"));
            Assert.That(result.createdPaths.Any(path => path.Contains("index.html")), Is.True);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLTemplateInstallationRerunReusesExistingFilesWithoutOverwriting()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("generated replacement");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            File.WriteAllText(Path.Combine(destinationDirectory, "index.html"), "user edits stay");

            var result = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.changed, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(destinationDirectory, "index.html")), Is.EqualTo("user edits stay"));
            Assert.That(result.reusedPaths.Any(path => path.Contains("index.html")), Is.True);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLTemplateInstallationBlocksFileAndFolderCollisions()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationAsFile = CreateTemporaryPath("WebGLTemplateDestinationFile");
        var destinationWithFileParent = CreateTemporaryPath("WebGLTemplateParentCollision");
        var destinationWithFileCollision = CreateTemporaryPath("WebGLTemplateFileCollision");

        try
        {
            File.WriteAllText(destinationAsFile, "not a folder");
            File.WriteAllText(destinationWithFileParent, "not a parent folder");
            Directory.CreateDirectory(destinationWithFileCollision);
            Directory.CreateDirectory(Path.Combine(destinationWithFileCollision, "index.html"));

            var fileDestinationResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationAsFile, false);
            var parentCollisionResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(
                sourceDirectory,
                Path.Combine(destinationWithFileParent, "GamingCouch"),
                false
            );
            var fileCollisionResult = GamingCouchWebGLExportSetup.InstallTemplateFiles(sourceDirectory, destinationWithFileCollision, false);

            Assert.That(fileDestinationResult.IsBlocked, Is.True);
            Assert.That(parentCollisionResult.IsBlocked, Is.True);
            Assert.That(fileCollisionResult.IsBlocked, Is.True);
            AssertHasEntryContaining(fileDestinationResult.blockedReasons, "Expected a folder");
            AssertHasEntryContaining(parentCollisionResult.blockedReasons, "Expected a folder");
            AssertHasEntryContaining(fileCollisionResult.blockedReasons, "Expected a file");
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationAsFile);
            DeleteTemporaryPath(destinationWithFileParent);
            DeleteTemporaryPath(destinationWithFileCollision);
        }
    }

    [Test]
    public void WebGLReadinessReportsSelectedTemplateAndWrongTemplateFailure()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();
            PlayerSettings.WebGL.template = "PROJECT:OtherTemplate";

            var wrongTemplateReadiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(wrongTemplateReadiness.IsBlocked, Is.True);
            Assert.That(wrongTemplateReadiness.templateSelected, Is.False);
            AssertHasEntryContaining(wrongTemplateReadiness.details, "PROJECT:OtherTemplate");

            PlayerSettings.WebGL.template = GamingCouchWebGLExportSetup.ProjectTemplateIdentifier;

            var selectedTemplateReadiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(selectedTemplateReadiness.IsBlocked, Is.False);
            Assert.That(selectedTemplateReadiness.templateSelected, Is.True);
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLReadinessReportsReleaseSettingDrift()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;

            var readiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory);

            Assert.That(readiness.IsBlocked, Is.True);
            Assert.That(readiness.releaseSettingsReady, Is.False);
            AssertHasEntryContaining(readiness.details, "WebGL compression: Gzip -> Disabled");
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLBuildProfilesGenerateDiffRowsAndApplyFromSpecs()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = false;

        var plan = GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan();
        var result = GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile();
        var readinessDetails = new List<string>();

        Assert.That(plan.HasChanges, Is.True);
        AssertHasPreviewRow(
            plan.rows,
            GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId,
            "WebGL compression: Gzip -> Disabled",
            true
        );
        Assert.That(result.changed, Is.True);
        AssertHasEntryContaining(result.details, "Applied WebGL compression: Gzip -> Disabled");
        Assert.That(
            GamingCouchWebGLBuildSettingsProfiles.IsReleaseProfileApplied(readinessDetails),
            Is.True
        );
        Assert.That(readinessDetails, Is.Empty);
    }

    [Test]
    public void WebGLBuildProfileSelectedApplySkipsUnselectedSettingForCurrentRun()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = false;

        var result = GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile(new[]
        {
            GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId,
        });
        var readinessDetails = new List<string>();

        Assert.That(result.changed, Is.True);
        Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Disabled));
        Assert.That(PlayerSettings.WebGL.dataCaching, Is.False);
        AssertHasEntryContaining(result.details, "Skipped WebGL data caching: Disabled -> Enabled");
        Assert.That(
            GamingCouchWebGLBuildSettingsProfiles.IsReleaseProfileApplied(readinessDetails),
            Is.False
        );
        AssertHasEntryContaining(readinessDetails.ToArray(), "WebGL data caching: Disabled -> Enabled");
    }

    [Test]
    public void WebGLExportPlanReportsTemplateBlockersBeforeMutating()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationAsFile = CreateTemporaryPath("WebGLTemplateDestinationFile");

        try
        {
            File.WriteAllText(destinationAsFile, "not a folder");

            var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan(
                sourceDirectory,
                destinationAsFile,
                false
            );

            Assert.That(plan.IsBlocked, Is.True);
            AssertHasPreviewRow(
                plan.rows,
                "web-export-template-folder",
                "Project-local web export template folder: File -> Folder",
                false
            );
            Assert.That(File.ReadAllText(destinationAsFile), Is.EqualTo("not a folder"));
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationAsFile);
        }
    }

    [Test]
    public void WebGLExportPlanKeepsTemplateAndTargetRowsRequired()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan(
                sourceDirectory,
                destinationDirectory,
                false
            );

            Assert.That(FindPreviewRow(plan.rows, "web-export-template-folder").isSkippable, Is.False);
            Assert.That(FindPreviewRow(plan.rows, GamingCouchWebGLExportSetup.TemplateSelectionRowId).isSkippable, Is.False);
            Assert.That(FindPreviewRow(plan.rows, GamingCouchWebGLExportSetup.ActiveBuildTargetRowId).isSkippable, Is.False);
            Assert.That(
                FindPreviewRow(
                    plan.rows,
                    GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId
                ).isSkippable,
                Is.True
            );
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLDocsDoNotDuplicateManualBuildSettingLists()
    {
        var packageRoot = GamingCouchEditorTestSupport.FindPackageRootPath();
        var documentPaths = new[]
        {
            "README.md",
            "Documentation~/README.md",
        };
        var forbiddenExactSettingPhrases = new[]
        {
            "development build off",
            "WebGL debug symbols off",
            "high managed stripping",
            "IL2CPP optimize size",
            "WebAssembly 2023 where available",
            "disk-size LTO",
            "data caching on",
            "WebGL compression disabled.",
            "Debug symbols disabled.",
            "Managed stripping level set to high.",
            "Disk-size LTO enabled.",
        };

        foreach (var documentPath in documentPaths)
        {
            Assert.That(documentPath, Is.Not.Null.And.Not.Empty);
            var fullPath = Path.Combine(packageRoot, documentPath);
            var text = File.ReadAllText(fullPath);
            foreach (var phrase in forbiddenExactSettingPhrases)
            {
                Assert.That(text, Does.Not.Contain(phrase), documentPath);
            }
        }
    }

    [Test]
    public void WebGLSetupInspectsAcceptedSplashAndLogoValues()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;

            var result = GamingCouchWebGLExportSetup.EnsureWebGLExportSetup(sourceDirectory, destinationDirectory, false);

            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.readiness.splashSettingsReady, Is.True);
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLApplyReportsWarningWhenOnlyDeselectedRowsAreLeftUnapplied()
    {
        var sourceDirectory = CreateTemporaryWebGLTemplateSource("template source");
        var destinationDirectory = CreateTemporaryPath("WebGLTemplateDestination");

        try
        {
            ApplyReadyWebGLExportSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan(
                sourceDirectory,
                destinationDirectory,
                false
            );

            var result = GamingCouchWebGLExportSetup.ApplyWebGLExportSetupPlan(plan, new string[0]);

            Assert.That(result.HasWarning, Is.True);
            Assert.That(result.IsBlocked, Is.False);
            Assert.That(result.message, Does.Contain("skipped rows were left unapplied"));
            AssertHasEntryContaining(result.details, "Skipped WebGL compression: Gzip -> Disabled.");
            Assert.That(PlayerSettings.WebGL.compressionFormat, Is.EqualTo(WebGLCompressionFormat.Gzip));
            Assert.That(result.readiness.IsBlocked, Is.True);
            Assert.That(result.readiness.releaseSettingsReady, Is.False);
        }
        finally
        {
            DeleteTemporaryPath(sourceDirectory);
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    [Test]
    public void WebGLApplySoftensOnlyDeselectedGapsBehindSatisfiedTemplatePrerequisites()
    {
        ApplyReadyWebGLExportSettings();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        var readiness = CreateUnappliedSettingsWebGLExportReadiness(true);
        var compressionRowIds = new[] { GamingCouchWebGLBuildSettingsProfiles.WebGLCompressionSettingId };

        // Applying honors the selection, so a selected row that is still unapplied cannot be produced
        // through ApplyWebGLExportSetupPlan; the softening predicate answers it directly.
        Assert.That(RemainingGapsAreDeliberateSkips(readiness, new string[0]), Is.True);
        Assert.That(RemainingGapsAreDeliberateSkips(readiness, compressionRowIds), Is.False);
        Assert.That(RemainingGapsAreDeliberateSkips(readiness, null), Is.False);
        Assert.That(
            RemainingGapsAreDeliberateSkips(CreateUnappliedSettingsWebGLExportReadiness(false), new string[0]),
            Is.False
        );
    }

    [Test]
    public void WebGLReadinessWarnsButDoesNotBlockWhenActiveBuildTargetIsNotWebGL()
    {
        var destinationDirectory = CreateTemporaryInstalledWebGLTemplate("installed template");

        try
        {
            ApplyReadyWebGLExportSettings();

            var readiness = GamingCouchWebGLExportSetup.InspectReadiness(destinationDirectory, BuildTarget.NoTarget);

            Assert.That(readiness.status, Is.EqualTo(GCWebGLExportSetupStatus.Warning));
            Assert.That(readiness.IsBlocked, Is.False);
            Assert.That(readiness.activeBuildTargetIsWebGL, Is.False);
            AssertHasEntryContaining(readiness.details, "run Gaming Couch web export settings or switch to WebGL");
            Assert.That(readiness.templateFolderReady, Is.True);
            Assert.That(readiness.templateFilesReady, Is.True);
            Assert.That(readiness.templateSelected, Is.True);
            Assert.That(readiness.releaseSettingsReady, Is.True);
            Assert.That(readiness.splashSettingsReady, Is.True);
        }
        finally
        {
            DeleteTemporaryPath(destinationDirectory);
        }
    }

    private static GameObject CreatePlayerPrefabAsset(string assetPath, Type playerType)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
        try
        {
            root.AddComponent(playerType);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath, out var success);
            Assert.That(success, Is.True);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GCExampleAssetSetupContinuationContext CreateGameListenerTestContext()
    {
        return CreateGameListenerTestContext(typeof(CompatibleGameScriptReceiver));
    }

    private static GCExampleAssetSetupContinuationContext CreateGameListenerTestContext(Type gameType)
    {
        return new GCExampleAssetSetupContinuationContext(
            GCActiveSceneSetupAction.ActiveSceneGameListener,
            false,
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            GamingCouchActiveSceneSetup.ActiveScenePlayerPrefabAssetPath,
            gameType.Name,
            nameof(GCPlayer),
            "Game",
            gameType,
            typeof(GCPlayer)
        );
    }

    private static void AssertCheck(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id,
        GCStartScreenReadinessCheckState state
    )
    {
        Assert.That(readiness.GetCheck(id).state, Is.EqualTo(state));
    }

    private static void AssertHasEntryContaining(IEnumerable<string> entries, string expectedSubstring)
    {
        Assert.That(
            entries != null && entries.Any(entry =>
                entry != null && entry.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0
            ),
            Is.True
        );
    }

    private static void AssertHasPreviewRow(
        GCWebGLPreviewRow[] rows,
        string id,
        string expectedDiffText,
        bool expectedSkippable
    )
    {
        var row = FindPreviewRow(rows, id);
        Assert.That(row.DiffText, Is.EqualTo(expectedDiffText));
        Assert.That(row.isSkippable, Is.EqualTo(expectedSkippable));
    }

    private static GCWebGLPreviewRow FindPreviewRow(GCWebGLPreviewRow[] rows, string id)
    {
        var row = rows != null
            ? rows.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal)
            )
            : null;
        Assert.That(row, Is.Not.Null, "Expected preview row " + id + ".");
        return row;
    }

    // Golden generation check: the file the generator writes must equal the canonical master after
    // the "Source" -> generated name rewrite (normalized for line endings). This replaces the old
    // ~42 hardcoded substring assertions with a single source of truth — the compiled master.
    private static void AssertGeneratedEqualsCanonicalMaster(
        string generated,
        string masterTypeName,
        Dictionary<string, string> typeNameReplacements
    )
    {
        var masterText = GamingCouchActiveSceneSetup.ReadCanonicalMasterSource(masterTypeName);
        var expected = GamingCouchActiveSceneSetup.RewriteCanonicalMasterToGeneratedSource(
            masterText,
            typeNameReplacements,
            true
        );

        Assert.That(
            Normalize(generated),
            Is.EqualTo(Normalize(expected)),
            "Generated source must equal the canonical master " + masterTypeName + " after name rewrite."
        );
    }

    // Structural checks that do not depend on the rewrite implementation, so they catch a broken
    // transform even where the equality check above is internally consistent.
    private static void AssertGeneratedSourceShape(string generated, string expectedClassDeclaration)
    {
        var normalized = Normalize(generated);

        // The move/rename header is present so the user knows they own and can rename the file.
        Assert.That(normalized, Does.StartWith("/*\n * GamingCouch example template file."));
        Assert.That(normalized, Does.Contain("Move this script into your project's own scripts folder"));

        // The generated type keeps the fixed generated name; the canonical "Source" type-name suffix
        // and the DSB.GC.ExampleCanonical namespace are stripped on the way into the user's project.
        Assert.That(normalized, Does.Contain(expectedClassDeclaration));
        Assert.That(
            normalized,
            Does.Not.Contain("Source"),
            "Generated source must not carry the canonical \"Source\" type-name suffix."
        );
        Assert.That(
            normalized,
            Does.Not.Contain("namespace DSB.GC.ExampleCanonical"),
            "Generated source must not carry the canonical namespace."
        );
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
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

    private GCStartScreenReadiness CreateStartScreenReadinessWithWebGL(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCWebGLExportReadiness webGLExport
    )
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            GamingCouchBuildSettingsReadiness.InspectScenePath(
                true,
                TestSceneBuildPath,
                new[] { new EditorBuildSettingsScene(TestSceneBuildPath, true) }
            ),
            GamingCouchGameViewAspect.InspectSizeEntries(
                new[] { new GCGameViewSizeEntry(0, "16:9 Aspect", 16, 9) },
                0,
                true,
                null
            ),
            webGLExport
        ));
    }

    private GCStartScreenReadiness CreateReadyStartScreenReadiness(
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab
    )
    {
        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            testScene,
            new[] { gamingCouch },
            gamingCouch,
            listener,
            playerPrefab,
            CreateValidLocalPlayJsonReadiness(),
            CreateReadyBuildSettingsReadiness(),
            CreateReadyGameViewAspectReadiness(),
            CreateReadyWebGLExportReadiness()
        ));
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

    private static GCWebGLExportReadiness CreateUnappliedSettingsWebGLExportReadiness(bool templateSelected)
    {
        return new GCWebGLExportReadiness(
            GCWebGLExportSetupStatus.Blocked,
            true,
            true,
            templateSelected,
            false,
            true,
            true,
            "Gaming Couch web export settings are incomplete.",
            Array.Empty<string>()
        );
    }

    private static bool RemainingGapsAreDeliberateSkips(
        GCWebGLExportReadiness readiness,
        string[] selectedSkippableRowIds
    )
    {
        var predicate = typeof(GamingCouchWebGLExportSetup).GetMethod(
            "RemainingGapsAreDeliberateSkips",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        Assert.That(predicate, Is.Not.Null);
        return (bool)predicate.Invoke(null, new object[]
        {
            readiness,
            GamingCouchWebGLBuildSettingsProfiles.CreateSelectedIdSet(selectedSkippableRowIds),
        });
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

    private string CreateTemporaryWebGLTemplateSource(string indexContent)
    {
        var sourceDirectory = CreateTemporaryProjectPath("WebGLTemplateSource");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "index.html"), indexContent);
        return sourceDirectory;
    }

    private string CreateTemporaryInstalledWebGLTemplate(string indexContent)
    {
        var destinationDirectory = CreateTemporaryProjectPath("WebGLTemplateDestination");
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(destinationDirectory, "index.html"), indexContent);
        return destinationDirectory;
    }

    private string CreateTemporaryPath(string prefix)
    {
        return CreateTemporaryProjectPath(prefix);
    }

    private string CreateTemporaryProjectPath(string prefix)
    {
        EnsureTestAssetFolder();
        return Path.Combine(
            AssetPathToFullPathUnchecked(testFolderAssetPath),
            prefix + "_" + Guid.NewGuid().ToString("N")
        );
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ApplyReadyWebGLExportSettings()
    {
        PlayerSettings.WebGL.template = GamingCouchWebGLExportSetup.ProjectTemplateIdentifier;
        GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile();
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
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

    private static bool ShouldShowStartScreenActionResult(MessageType messageType)
    {
        var shouldShowActionResult = typeof(GamingCouchStartScreenWindow)
            .GetMethod("ShouldShowActionResult", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(shouldShowActionResult, Is.Not.Null);

        return (bool)shouldShowActionResult.Invoke(null, new object[] { messageType });
    }

    private void ReserveGCExampleGameAndPlayerScriptPathsForCollisionTest()
    {
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath);
        ReserveGeneratedScriptPathForCollisionTest(GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath);
        EnsureGCExampleFolderForTest(
            ref createdExampleProjectFolderForCollisionTest,
            ref createdExampleFolderForCollisionTest
        );
    }

    private static void ReserveGeneratedScriptPathForCollisionTest(string assetPath)
    {
        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            Assert.Ignore("Skipping generated script path collision test because " + assetPath + " already exists on disk.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
        {
            Assert.Ignore("Skipping generated script path collision test because " + assetPath + " already exists.");
        }
    }

    private static void CreateScriptPathCollisionDirectory(string assetPath, ref bool created)
    {
        Directory.CreateDirectory(AssetPathToFullPathUnchecked(assetPath));
        created = true;
    }

    private void EnsureTestAssetFolder()
    {
        if (AssetDatabase.IsValidFolder(testFolderAssetPath))
        {
            return;
        }

        var folderName = testFolderAssetPath.Substring("Assets/".Length);
        var guid = AssetDatabase.CreateFolder("Assets", folderName);
        Assert.That(guid, Is.Not.Empty);
        Assert.That(AssetDatabase.IsValidFolder(testFolderAssetPath), Is.True);
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        Assert.That(assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal), Is.True);
        return AssetPathToFullPathUnchecked(assetPath);
    }

    private static string AssetPathToFullPathUnchecked(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        return Path.Combine(Application.dataPath, assetPath.Substring(assetsPrefix.Length));
    }

    private void RestoreActiveSceneAndCloseTestScene()
    {
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

    private static bool CanReuseActiveSceneAsTestScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.isLoaded &&
               string.IsNullOrEmpty(scene.path);
    }

    private Scene CreateLaunchSceneForActiveSceneSetupTest(out bool launchSceneIsTestScene)
    {
        if (testScene.IsValid() && testScene.isLoaded && string.IsNullOrEmpty(testScene.path))
        {
            launchSceneIsTestScene = true;
            EnsureSceneIsActive(testScene);
            return testScene;
        }

        launchSceneIsTestScene = false;
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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

    private static void EnsureGCExampleFolderForTest(
        ref bool createdProjectFolder,
        ref bool createdExampleFolder
    )
    {
        EnsureAssetFolderForTest(
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
            "Assets",
            "GamingCouch",
            ref createdProjectFolder
        );
        EnsureAssetFolderForTest(
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
            "GCExample",
            ref createdExampleFolder
        );
    }

    private static void EnsureAssetFolderForTest(
        string assetPath,
        string parentAssetPath,
        string folderName,
        ref bool created
    )
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath) ||
            File.Exists(fullPath) ||
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
        {
            Assert.Ignore("Skipping GCExample asset test because " + assetPath + " exists but is not a Unity asset folder.");
        }

        var guid = AssetDatabase.CreateFolder(parentAssetPath, folderName);
        Assert.That(string.IsNullOrEmpty(guid), Is.False);
        Assert.That(AssetDatabase.IsValidFolder(assetPath), Is.True);
        created = true;
    }

    private void DeleteTestAssetFolder()
    {
        if (string.IsNullOrEmpty(testFolderAssetPath) ||
            !testFolderAssetPath.StartsWith(TestFolderAssetPathPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var deletedThroughAssetDatabase = AssetDatabase.IsValidFolder(testFolderAssetPath) &&
                                          AssetDatabase.DeleteAsset(testFolderAssetPath);
        var fullPath = AssetPathToFullPathUnchecked(testFolderAssetPath);
        if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, true);
        }

        var metaPath = fullPath + ".meta";
        if (!deletedThroughAssetDatabase && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private void DeleteGeneratedScriptPathCollisionTestAssets()
    {
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchActiveSceneSetup.ActiveSceneGameScriptAssetPath,
            ref createdExampleGameScriptPathCollisionForTest
        );
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchActiveSceneSetup.ActiveScenePlayerScriptAssetPath,
            ref createdExamplePlayerScriptPathCollisionForTest
        );
        DeleteGeneratedScriptPathCollisionTestAsset(
            GamingCouchActiveSceneSetup.ExampleTemplateScriptAssetPath,
            ref createdExampleTemplateScriptPathCollisionForTest
        );
        DeleteCreatedGCExampleFolders(
            ref createdExampleProjectFolderForCollisionTest,
            ref createdExampleFolderForCollisionTest
        );
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void DeleteCreatedGCExampleFolders(
        ref bool createdProjectFolder,
        ref bool createdExampleFolder
    )
    {
        DeleteEmptyAssetFolderCreatedForTest(
            GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
            ref createdExampleFolder
        );
        DeleteEmptyAssetFolderCreatedForTest(
            GamingCouchActiveSceneSetup.ProjectFolderAssetPath,
            ref createdProjectFolder
        );
    }

    private static void DeleteEmptyAssetFolderCreatedForTest(string assetPath, ref bool created)
    {
        if (!created)
        {
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath))
        {
            var nestedEntries = Directory.GetFileSystemEntries(fullPath);
            if (nestedEntries.Length > 0)
            {
                return;
            }

            var deletedThroughAssetDatabase = AssetDatabase.IsValidFolder(assetPath) &&
                                              AssetDatabase.DeleteAsset(assetPath);
            if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, false);
            }
        }

        var metaPath = fullPath + ".meta";
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath) && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            created = false;
        }
    }

    private static void DeleteGeneratedScriptPathCollisionTestAsset(string assetPath, ref bool created)
    {
        if (!created)
        {
            return;
        }

        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        if (Directory.Exists(fullPath))
        {
            var nestedEntries = Directory.GetFileSystemEntries(fullPath);
            if (nestedEntries.Length > 0)
            {
                throw new InvalidOperationException(
                    "Refusing to recursively delete non-empty collision test folder " + assetPath + "."
                );
            }

            var deletedThroughAssetDatabase = AssetDatabase.DeleteAsset(assetPath);
            if (!deletedThroughAssetDatabase && Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, false);
            }
        }

        var metaPath = fullPath + ".meta";
        if (!Directory.Exists(fullPath) && File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }

        created = false;
    }

    private static bool IsAssetFolderEmpty(string assetPath)
    {
        var fullPath = AssetPathToFullPathUnchecked(assetPath);
        return Directory.Exists(fullPath) &&
               Directory.GetFileSystemEntries(fullPath).Length == 0;
    }

    private void RestoreBuildSettings()
    {
        EditorBuildSettings.scenes = previousBuildSettingsScenes ?? Array.Empty<EditorBuildSettingsScene>();
    }

    // Drop any scene entries this test owns (its temp folder plus the fixed test-only scene
    // paths) so a leak from an interrupted run cannot survive in the project's persisted build
    // settings and break a later Player build. Mirrors GamingCouchStartScreenEditorSmokeTests.
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

        return path.StartsWith(TestFolderAssetPathPrefix, StringComparison.Ordinal) ||
               string.Equals(path, ExistingSceneBuildPath, StringComparison.Ordinal) ||
               string.Equals(path, OtherSceneBuildPath, StringComparison.Ordinal) ||
               string.Equals(path, TestSceneBuildPath, StringComparison.Ordinal);
    }

    private void RestoreSuppressAutoOpenSetting()
    {
        EditorUserSettings.SetConfigValue(SuppressAutoOpenKey, previousSuppressAutoOpenConfigValue);
    }

    private void SaveWebGLSettings()
    {
        var webGLTarget = NamedBuildTarget.WebGL;
        previousWebGLTemplate = PlayerSettings.WebGL.template;
        previousWebGLCompressionFormat = PlayerSettings.WebGL.compressionFormat;
        previousWebGLDataCaching = PlayerSettings.WebGL.dataCaching;
        previousWebGLExceptionSupport = PlayerSettings.WebGL.exceptionSupport;
        previousWebGLDebugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
#if UNITY_2023_1_OR_NEWER
        previousWebGLWasm2023 = PlayerSettings.WebGL.wasm2023;
#endif
        GCWebGLBuildSupport.TryGetCodeOptimization(out previousWebGLCodeOptimization);
        previousDevelopmentBuild = EditorUserBuildSettings.development;
        previousIl2CppCodeGeneration = PlayerSettings.GetIl2CppCodeGeneration(webGLTarget);
        previousManagedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(webGLTarget);
        previousStripUnusedMeshComponents = PlayerSettings.stripUnusedMeshComponents;
        previousSplashScreenShow = PlayerSettings.SplashScreen.show;
        previousSplashScreenShowUnityLogo = PlayerSettings.SplashScreen.showUnityLogo;
    }

    private void RestoreWebGLSettings()
    {
        var webGLTarget = NamedBuildTarget.WebGL;
        PlayerSettings.WebGL.template = previousWebGLTemplate;
        PlayerSettings.WebGL.compressionFormat = previousWebGLCompressionFormat;
        PlayerSettings.WebGL.dataCaching = previousWebGLDataCaching;
        PlayerSettings.WebGL.exceptionSupport = previousWebGLExceptionSupport;
        PlayerSettings.WebGL.debugSymbolMode = previousWebGLDebugSymbolMode;
#if UNITY_2023_1_OR_NEWER
        PlayerSettings.WebGL.wasm2023 = previousWebGLWasm2023;
#endif
        GCWebGLBuildSupport.SetCodeOptimization(previousWebGLCodeOptimization);
        EditorUserBuildSettings.development = previousDevelopmentBuild;
        PlayerSettings.SetIl2CppCodeGeneration(webGLTarget, previousIl2CppCodeGeneration);
        PlayerSettings.SetManagedStrippingLevel(webGLTarget, previousManagedStrippingLevel);
        PlayerSettings.stripUnusedMeshComponents = previousStripUnusedMeshComponents;
        PlayerSettings.SplashScreen.show = previousSplashScreenShow;
        PlayerSettings.SplashScreen.showUnityLogo = previousSplashScreenShowUnityLogo;
    }

    private static void RunCleanup(Action cleanup, List<Exception> cleanupErrors)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            cleanupErrors.Add(exception);
        }
    }
}
