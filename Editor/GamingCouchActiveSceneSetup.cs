using DSB.GC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GCActiveSceneSetupAction
{
    ActiveSceneMissingPieces,
    ActiveScenePlayerPrefab,
    ActiveSceneGameListener,
    // Additive upgrade: swaps the wired template for the full example game (GCExampleGame +
    // GCExamplePlayer). Selects the game-flavor spec in GetScriptSetupSpec.
    ActiveSceneWireExampleGame,
}

internal enum GCExampleScriptSetupStatus
{
    Ready,
    PendingCompilation,
    Blocked,
}

internal enum GCExamplePlayerPrefabSetupStatus
{
    Ready,
    Blocked,
}

internal enum GCActiveSceneGameListenerSetupStatus
{
    Ready,
    Blocked,
}

internal enum GCActiveSceneSetupStatus
{
    Ready,
    PendingCompilation,
    Blocked,
}

internal sealed class GCExampleScriptSetupResult
{
    internal readonly GCExampleScriptSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] createdAssetPaths;
    internal readonly string[] reusedAssetPaths;
    internal readonly string[] blockedReasons;

    internal GCExampleScriptSetupResult(
        GCExampleScriptSetupStatus status,
        bool changed,
        string message,
        string[] createdAssetPaths,
        string[] reusedAssetPaths,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.createdAssetPaths = createdAssetPaths ?? new string[0];
        this.reusedAssetPaths = reusedAssetPaths ?? new string[0];
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCExampleScriptSetupStatus.Blocked; }
    }

    internal bool IsPendingCompilation
    {
        get { return status == GCExampleScriptSetupStatus.PendingCompilation; }
    }
}

internal sealed class GCExamplePlayerPrefabSetupResult
{
    internal readonly GCExamplePlayerPrefabSetupStatus status;
    internal readonly bool changed;
    internal readonly GameObject prefab;
    internal readonly string message;
    internal readonly string[] blockedReasons;

    internal GCExamplePlayerPrefabSetupResult(
        GCExamplePlayerPrefabSetupStatus status,
        bool changed,
        GameObject prefab,
        string message,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.prefab = prefab;
        this.message = message;
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCExamplePlayerPrefabSetupStatus.Blocked; }
    }
}

internal sealed class GCActiveSceneGameListenerSetupResult
{
    internal readonly GCActiveSceneGameListenerSetupStatus status;
    internal readonly bool changed;
    internal readonly GameObject listenerObject;
    internal readonly string message;
    internal readonly string[] blockedReasons;

    internal GCActiveSceneGameListenerSetupResult(
        GCActiveSceneGameListenerSetupStatus status,
        bool changed,
        GameObject listenerObject,
        string message,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.listenerObject = listenerObject;
        this.message = message;
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCActiveSceneGameListenerSetupStatus.Blocked; }
    }
}

internal sealed class GCExampleAssetFolderCleanupResult
{
    internal readonly bool changed;
    internal readonly string[] removedAssetPaths;
    internal readonly string[] blockedReasons;

    internal GCExampleAssetFolderCleanupResult(
        bool changed,
        string[] removedAssetPaths,
        string[] blockedReasons
    )
    {
        this.changed = changed;
        this.removedAssetPaths = removedAssetPaths ?? new string[0];
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return blockedReasons.Length > 0; }
    }
}

internal sealed class GCActiveSceneSetupResult
{
    internal readonly GCActiveSceneSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCActiveSceneSetupResult(
        GCActiveSceneSetupStatus status,
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
        get { return status == GCActiveSceneSetupStatus.Blocked; }
    }

    internal bool IsPendingCompilation
    {
        get { return status == GCActiveSceneSetupStatus.PendingCompilation; }
    }
}

internal sealed class GCExampleScriptSetupSpec
{
    internal readonly string scriptFolderAssetPath;
    internal readonly string gameScriptAssetPath;
    internal readonly string playerScriptAssetPath;
    internal readonly string playerPrefabAssetPath;
    internal readonly string gameTypeName;
    internal readonly string playerTypeName;
    internal readonly string listenerObjectName;
    internal readonly bool requiresGeneratedScriptFolder;
    private readonly Func<string> gameScriptSourceFactory;
    private readonly Func<string> playerScriptSourceFactory;

    internal GCExampleScriptSetupSpec(
        string scriptFolderAssetPath,
        string gameScriptAssetPath,
        string playerScriptAssetPath,
        string playerPrefabAssetPath,
        string gameTypeName,
        string playerTypeName,
        string listenerObjectName,
        bool requiresGeneratedScriptFolder,
        Func<string> gameScriptSourceFactory,
        Func<string> playerScriptSourceFactory
    )
    {
        this.scriptFolderAssetPath = scriptFolderAssetPath;
        this.gameScriptAssetPath = gameScriptAssetPath;
        this.playerScriptAssetPath = playerScriptAssetPath;
        this.playerPrefabAssetPath = playerPrefabAssetPath;
        this.gameTypeName = gameTypeName;
        this.playerTypeName = playerTypeName;
        this.listenerObjectName = listenerObjectName;
        this.requiresGeneratedScriptFolder = requiresGeneratedScriptFolder;
        this.gameScriptSourceFactory = gameScriptSourceFactory;
        this.playerScriptSourceFactory = playerScriptSourceFactory;
    }

    internal string BuildGameScriptSource()
    {
        return gameScriptSourceFactory();
    }

    internal string BuildPlayerScriptSource()
    {
        return playerScriptSourceFactory();
    }
}

internal sealed class GCExampleAssetSetupContinuationContext
{
    internal readonly GCActiveSceneSetupAction action;
    internal readonly bool resumedAfterCompilation;
    internal readonly string exampleFolderAssetPath;
    internal readonly string gameScriptAssetPath;
    internal readonly string playerScriptAssetPath;
    internal readonly string playerPrefabAssetPath;
    internal readonly string gameTypeName;
    internal readonly string playerTypeName;
    internal readonly string listenerObjectName;
    internal readonly Type gameType;
    internal readonly Type playerType;

    internal GCExampleAssetSetupContinuationContext(
        GCActiveSceneSetupAction action,
        bool resumedAfterCompilation,
        string exampleFolderAssetPath,
        string gameScriptAssetPath,
        string playerScriptAssetPath,
        string playerPrefabAssetPath,
        string gameTypeName,
        string playerTypeName,
        string listenerObjectName,
        Type gameType,
        Type playerType
    )
    {
        this.action = action;
        this.resumedAfterCompilation = resumedAfterCompilation;
        this.exampleFolderAssetPath = exampleFolderAssetPath;
        this.gameScriptAssetPath = gameScriptAssetPath;
        this.playerScriptAssetPath = playerScriptAssetPath;
        this.playerPrefabAssetPath = playerPrefabAssetPath;
        this.gameTypeName = gameTypeName;
        this.playerTypeName = playerTypeName;
        this.listenerObjectName = listenerObjectName;
        this.gameType = gameType;
        this.playerType = playerType;
    }

    internal bool HasRequiredTypes
    {
        get { return gameType != null && playerType != null; }
    }
}

internal enum GCWireExampleGameStatus
{
    Wired,
    Cancelled,
    Blocked,
}

// Result of the additive "Wire example game" action, which upgrades a template scene in place to
// the full example game. Mirrors GCExampleSceneCreationResult so the Start Screen message mapping
// can treat both scene actions the same way.
internal sealed class GCWireExampleGameResult
{
    internal readonly GCWireExampleGameStatus status;
    internal readonly bool isPendingCompilation;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCWireExampleGameResult(
        GCWireExampleGameStatus status,
        bool isPendingCompilation,
        bool changed,
        string message,
        string[] details
    )
    {
        this.status = status;
        this.isPendingCompilation = isPendingCompilation;
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
    }

    internal bool IsWired { get { return status == GCWireExampleGameStatus.Wired; } }
    internal bool IsCancelled { get { return status == GCWireExampleGameStatus.Cancelled; } }
    internal bool IsBlocked { get { return status == GCWireExampleGameStatus.Blocked; } }
    internal bool IsPendingCompilation { get { return isPendingCompilation; } }
}

[InitializeOnLoad]
internal static class GamingCouchActiveSceneSetup
{
    internal const string ProjectFolderAssetPath = "Assets/GamingCouch";
    internal const string ExampleFolderAssetPath = ProjectFolderAssetPath + "/GCExample";
    internal const string ExampleGameTypeName = "GCExampleGame";
    internal const string ExamplePlayerTypeName = "GCExamplePlayer";
    internal const string ExampleGameScriptAssetPath = ExampleFolderAssetPath + "/" + ExampleGameTypeName + ".cs";
    internal const string ExamplePlayerScriptAssetPath = ExampleFolderAssetPath + "/" + ExamplePlayerTypeName + ".cs";
    internal const string ExamplePlayerPrefabAssetPath = ExampleFolderAssetPath + "/" + ExamplePlayerTypeName + ".prefab";
    internal const string ActiveSceneGameTypeName = ExampleGameTypeName;
    internal const string ActiveScenePlayerTypeName = ExamplePlayerTypeName;
    internal const string ActiveSceneGameScriptAssetPath = ExampleGameScriptAssetPath;
    internal const string ActiveScenePlayerScriptAssetPath = ExamplePlayerScriptAssetPath;
    internal const string ActiveScenePlayerPrefabAssetPath = ExamplePlayerPrefabAssetPath;

    // The generated example is copied from canonical master source that compiles against the real
    // runtime in the UNITY_INCLUDE_TESTS-gated GamingCouch.Tests.ExampleCanonical assembly (ADR
    // 0017). The generator copies each master, strips the "Source" type-name suffix and the
    // DSB.GC.ExampleCanonical namespace, and injects ExampleTemplateHeader. The barebones template
    // (GCExampleTemplate, stock GCPlayer) is wired by "Create example scene"; the full game
    // (GCExampleGame + GCExamplePlayer) is wired by "Wire example game".
    internal const string ExampleCanonicalSourceRelativeDir = "Tests/ExampleCanonical";
    internal const string ExampleTemplateTypeName = "GCExampleTemplate";
    internal const string ExampleTemplateMasterTypeName = "GCExampleTemplateSource";
    internal const string ExampleGameMasterTypeName = "GCExampleGameSource";
    internal const string ExamplePlayerMasterTypeName = "GCExamplePlayerSource";
    internal const string ExampleTemplateScriptAssetPath = ExampleFolderAssetPath + "/" + ExampleTemplateTypeName + ".cs";
    // The template flavor spawns the stock GCPlayer (no generated player script) from a generated
    // capsule prefab. "Create example scene" wires this; "Wire example game" swaps it for
    // GCExamplePlayer.prefab.
    internal const string StockPlayerTypeName = "GCPlayer";
    internal const string StockPlayerPrefabAssetPath = ExampleFolderAssetPath + "/" + StockPlayerTypeName + ".prefab";

    private const string PendingSetupSessionKey = "DSB.GC.ActiveSceneSetup.PendingSetup.v1";
    private const string PendingActionSessionKey = "DSB.GC.ActiveSceneSetup.PendingAction.v1";
    private const string PendingWarningLoggedSessionKey = "DSB.GC.ActiveSceneSetup.PendingWarningLogged.v1";
    private const string DefaultActionValue = "ActiveSceneMissingPieces";
    private const string ListenerObjectName = "Game";
    private const string ActiveSceneGameListenerObjectName = ListenerObjectName;
    private const string CreateGameListenerUndoName = "Create Example Game Listener";
    private const string AddGameListenerComponentUndoName = "Add Example Game Listener";
    private const string WireExampleGameUndoName = "Wire Example Game";
    private const string PlayerVisualName = "Visual";
    private const string ExampleTemplateHeader =
        "/*\n" +
        " * GamingCouch example template file.\n" +
        " *\n" +
        " * Move this script into your project's own scripts folder, then rename the file and class to fit your project.\n" +
        " * For example: Game.cs/Game for your game script and Player.cs/Player for your player script.\n" +
        " */\n\n";
    private const string BlockingFolderRemediationHint =
        " Use Create New Example Scene to move blocking folders to the Trash, or remove the folder manually, then run setup again.";

    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

    // Template flavor (the "Create example scene" / active-scene default): generates only the
    // barebones GCExampleTemplate listener and wires the stock GCPlayer spawned from a generated
    // capsule prefab. It has no generated player script (playerScriptAssetPath == null); the player
    // type is the compiled stock GCPlayer, resolved directly in CreateContinuationContext.
    private static readonly GCExampleScriptSetupSpec TemplateScriptSetupSpec =
        new GCExampleScriptSetupSpec(
            ExampleFolderAssetPath,
            ExampleTemplateScriptAssetPath,
            null,
            StockPlayerPrefabAssetPath,
            ExampleTemplateTypeName,
            StockPlayerTypeName,
            ListenerObjectName,
            true,
            GenerateExampleTemplateSource,
            null
        );

    // Game flavor (the additive "Wire example game" action): the full playable example, generating
    // GCExampleGame + the custom GCExamplePlayer and its prefab.
    private static readonly GCExampleScriptSetupSpec GameScriptSetupSpec =
        new GCExampleScriptSetupSpec(
            ExampleFolderAssetPath,
            ExampleGameScriptAssetPath,
            ExamplePlayerScriptAssetPath,
            ExamplePlayerPrefabAssetPath,
            ExampleGameTypeName,
            ExamplePlayerTypeName,
            ListenerObjectName,
            true,
            GenerateExampleGameSource,
            GenerateExamplePlayerSource
        );
    private static Action<GCExampleAssetSetupContinuationContext> scriptsReadyHandlers;

    static GamingCouchActiveSceneSetup()
    {
        RegisterScriptsReadyHandler(EnsureActiveSceneGameListenerOnScriptsReady);
        RegisterScriptsReadyHandler(EnsureExamplePlayerPrefabOnScriptsReady);
        RegisterScriptsReadyHandler(SwapListenerToExampleGameOnScriptsReady);

        if (HasPendingSetup())
        {
            StartPendingSetupPolling();
        }
    }

    internal static void RegisterScriptsReadyHandler(Action<GCExampleAssetSetupContinuationContext> handler)
    {
        if (handler == null)
        {
            return;
        }

        scriptsReadyHandlers -= handler;
        scriptsReadyHandlers += handler;

        if (HasPendingSetup())
        {
            StartPendingSetupPolling();
        }
    }

    internal static void UnregisterScriptsReadyHandler(Action<GCExampleAssetSetupContinuationContext> handler)
    {
        if (handler == null)
        {
            return;
        }

        scriptsReadyHandlers -= handler;
    }

    internal static GCExampleScriptSetupResult EnsureExampleScripts()
    {
        return EnsureExampleScripts(GetDefaultAction(), true);
    }

    private static GCExampleScriptSetupResult EnsureExampleScripts(
        GCActiveSceneSetupAction action,
        bool dispatchWhenReady
    )
    {
        var spec = GetScriptSetupSpec(action);
        var createdAssetPaths = new List<string>();
        var reusedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();

        EnsureScriptFolders(spec, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreateResult(
                GCExampleScriptSetupStatus.Blocked,
                false,
                GetSetupDisplayName() + " script generation is blocked.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        EnsureScriptAsset(spec.gameScriptAssetPath, spec.gameTypeName, spec.BuildGameScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
        // The template flavor has no generated player script (playerScriptAssetPath == null); it
        // spawns the stock GCPlayer, so only the listener script is generated here.
        if (spec.playerScriptAssetPath != null)
        {
            EnsureScriptAsset(spec.playerScriptAssetPath, spec.playerTypeName, spec.BuildPlayerScriptSource(), createdAssetPaths, reusedAssetPaths, blockedReasons);
        }
        if (blockedReasons.Count > 0)
        {
            if (createdAssetPaths.Count > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            return CreateResult(
                GCExampleScriptSetupStatus.Blocked,
                createdAssetPaths.Count > 0,
                GetSetupDisplayName() + " script generation is blocked.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        if (createdAssetPaths.Count > 0)
        {
            PersistPendingSetup(action);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            StartPendingSetupPolling();

            return CreateResult(
                GCExampleScriptSetupStatus.PendingCompilation,
                true,
                GetSetupDisplayName() + " created missing example scripts and queued setup continuation after Unity compiles them.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        var context = CreateContinuationContext(action, false);
        if (!context.HasRequiredTypes)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                PersistPendingSetup(action);
                StartPendingSetupPolling();

                return CreateResult(
                    GCExampleScriptSetupStatus.PendingCompilation,
                    false,
                    GetSetupDisplayName() + " is waiting for Unity to compile existing example scripts.",
                    createdAssetPaths,
                    reusedAssetPaths,
                    blockedReasons
                );
            }

            blockedReasons.Add("Example script assets already exist, but compiled types " + spec.gameTypeName + " and " + spec.playerTypeName + " are not both available. Existing scripts were left untouched.");
            return CreateResult(
                GCExampleScriptSetupStatus.Blocked,
                false,
                GetSetupDisplayName() + " cannot continue until the existing script assets compile with the expected type names.",
                createdAssetPaths,
                reusedAssetPaths,
                blockedReasons
            );
        }

        if (dispatchWhenReady)
        {
            DispatchScriptsReady(context);
        }

        return CreateResult(
            GCExampleScriptSetupStatus.Ready,
            false,
            "Example scripts already exist and compiled types are available.",
            createdAssetPaths,
            reusedAssetPaths,
            blockedReasons
        );
    }

    internal static GCExampleScriptSetupSpec GetScriptSetupSpec(GCActiveSceneSetupAction action)
    {
        // The default active-scene setup (including "Create example scene") wires the barebones
        // template; only the additive "Wire example game" action selects the full game flavor.
        return action == GCActiveSceneSetupAction.ActiveSceneWireExampleGame
            ? GameScriptSetupSpec
            : TemplateScriptSetupSpec;
    }

    // A directory sitting where an example script or the player prefab file is expected (for example
    // a folder literally named "GCExampleGame.cs") blocks generation, because the SDK never
    // overwrites anything at those paths. These helpers let the Create New Example Scene flow detect
    // and clear such folders up front, turning a cryptic mid-setup block into an explicit,
    // recoverable cleanup step.
    internal static string[] FindBlockingExampleAssetFolders()
    {
        // Cover both flavors' generated paths so a reset clears a folder blocking either the template
        // (GCExampleTemplate.cs + GCPlayer.prefab) or the full game (GCExampleGame/GCExamplePlayer).
        var candidatePaths = new[]
        {
            ExampleTemplateScriptAssetPath,
            StockPlayerPrefabAssetPath,
            ExampleGameScriptAssetPath,
            ExamplePlayerScriptAssetPath,
            ExamplePlayerPrefabAssetPath,
        };

        var blockingFolders = new List<string>();
        for (var index = 0; index < candidatePaths.Length; index++)
        {
            if (Directory.Exists(AssetPathToFullPath(candidatePaths[index])))
            {
                blockingFolders.Add(candidatePaths[index]);
            }
        }

        return blockingFolders.ToArray();
    }

    internal static GCExampleAssetFolderCleanupResult RemoveBlockingExampleAssetFolders()
    {
        var removedAssetPaths = new List<string>();
        var blockedReasons = new List<string>();
        var blockingFolders = FindBlockingExampleAssetFolders();

        for (var index = 0; index < blockingFolders.Length; index++)
        {
            if (TryRemoveBlockingAssetFolder(blockingFolders[index], blockedReasons))
            {
                removedAssetPaths.Add(blockingFolders[index]);
            }
        }

        if (removedAssetPaths.Count > 0)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        return new GCExampleAssetFolderCleanupResult(
            removedAssetPaths.Count > 0,
            removedAssetPaths.ToArray(),
            blockedReasons.ToArray()
        );
    }

    private static bool TryRemoveBlockingAssetFolder(string assetPath, List<string> blockedReasons)
    {
        var fullPath = AssetPathToFullPath(assetPath);
        if (!Directory.Exists(fullPath))
        {
            // Only ever remove a directory here; a file at the path is left untouched so user
            // content is never deleted by the cleanup.
            return false;
        }

        // Prefer MoveAssetToTrash so the removal is recoverable from the OS trash.
        if (AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.MoveAssetToTrash(assetPath))
        {
            return true;
        }

        // Fallback for a raw directory Unity never imported as an asset folder (MoveAssetToTrash can't
        // recover it). Only remove it when empty — the stray ".cs"-named folder case. Refusing a
        // non-empty folder keeps the "moved to the Trash" promise honest and never destroys content.
        if (Directory.GetFileSystemEntries(fullPath).Length > 0)
        {
            blockedReasons.Add("Cannot safely remove " + assetPath + " because it is a non-empty folder Unity did not import as an asset. Remove it manually.");
            return false;
        }

        try
        {
            Directory.Delete(fullPath, false);
            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            return true;
        }
        catch (Exception exception)
        {
            blockedReasons.Add("Could not remove the folder " + assetPath + ": " + exception.Message);
            return false;
        }
    }

    internal static bool IsActiveSceneGeneratedPlayerPrefabCompatible(
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        out string message
    )
    {
        message = null;
        if (!IsActiveSceneGeneratedGameListener(listener))
        {
            return true;
        }

        var prefabObject = playerPrefab as GameObject;
        if (prefabObject == null)
        {
            message = "The active-scene Game script uses " + ActiveScenePlayerTypeName + ", but the assigned player prefab could not be inspected.";
            return false;
        }

        if (FindComponentByTypeName(prefabObject, ActiveScenePlayerTypeName, typeof(GCPlayer)) != null)
        {
            return true;
        }

        message = "The active-scene Game script uses " + ActiveScenePlayerTypeName + ", but the assigned player prefab root does not have " + ActiveScenePlayerTypeName + ". Assign a compatible player prefab manually.";
        return false;
    }

    private static bool IsActiveSceneGeneratedGameListener(UnityEngine.Object listener)
    {
        var listenerObject = listener as GameObject;
        return listenerObject != null &&
               FindComponentByTypeName(listenerObject, ActiveSceneGameTypeName, typeof(MonoBehaviour)) != null;
    }

    private static Component FindComponentByTypeName(
        GameObject gameObject,
        string typeName,
        Type requiredBaseType
    )
    {
        if (gameObject == null)
        {
            return null;
        }

        var components = gameObject.GetComponents<Component>();
        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            if (component == null)
            {
                continue;
            }

            var componentType = component.GetType();
            if (componentType.Name == typeName &&
                requiredBaseType.IsAssignableFrom(componentType))
            {
                return component;
            }
        }

        return null;
    }

    private static void EnsureScriptFolders(
        GCExampleScriptSetupSpec spec,
        List<string> blockedReasons
    )
    {
        if (spec == null || !spec.requiresGeneratedScriptFolder)
        {
            return;
        }

        EnsureProjectFolder(ProjectFolderAssetPath, "Assets", "GamingCouch", blockedReasons);
        EnsureProjectFolder(
            spec.scriptFolderAssetPath,
            ProjectFolderAssetPath,
            GetAssetPathName(spec.scriptFolderAssetPath),
            blockedReasons
        );
    }

    internal static bool HasPendingSetup()
    {
        return SessionState.GetBool(PendingSetupSessionKey, false);
    }

    internal static string GetPendingSetupDisplayName()
    {
        return GetSetupDisplayName();
    }

    internal static string GetSetupDisplayName()
    {
        return "Active Scene Setup";
    }

    internal static GCActiveSceneSetupResult EnsureActiveSceneSetup()
    {
        return EnsureActiveSceneSetup(true);
    }

    // setFirstBuildSettingsScene lets the create-new-example-scene flow skip promoting the new
    // scene to the first Build Settings scene, so creating an example never silently changes which
    // scene a build boots into. The user can still opt in later via "Set up missing pieces".
    internal static GCActiveSceneSetupResult EnsureActiveSceneSetup(bool setFirstBuildSettingsScene)
    {
        var details = new List<string>();
        var gamingCouchResult = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
        details.Add(gamingCouchResult.message);

        var changed = gamingCouchResult.changed;
        var buildSettingsResult = setFirstBuildSettingsScene
            ? EnsureActiveSceneFirstBuildSettingsScene(details)
            : null;
        changed |= buildSettingsResult != null && buildSettingsResult.changed;
        var gameViewResult = EnsureGameView16By9IfSafe(details);
        changed |= gameViewResult.changed;

        if (gamingCouchResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed,
                "Active Scene Setup is blocked.",
                details
            );
        }

        if (buildSettingsResult != null && buildSettingsResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed,
                buildSettingsResult.message,
                details
            );
        }

        if (gameViewResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed,
                gameViewResult.message,
                details
            );
        }

        if (IsActiveSceneSetupWiringReady(gamingCouchResult.gamingCouch))
        {
            details.Add("The GamingCouch Game script reference already contains a serialized reference.");
            details.Add("The GamingCouch player prefab reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Ready,
                changed,
                changed
                    ? "Active Scene Setup completed."
                    : "Active Scene Setup was already complete; existing scene references were reused.",
                details
            );
        }

        var scriptResult = EnsureExampleScripts(
            GCActiveSceneSetupAction.ActiveSceneMissingPieces,
            false
        );

        AddScriptResultDetails(scriptResult, details);
        if (scriptResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed || scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        if (scriptResult.IsPendingCompilation)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.PendingCompilation,
                changed || scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        var context = CreateContinuationContext(
            GCActiveSceneSetupAction.ActiveSceneMissingPieces,
            false
        );

        changed |= scriptResult.changed;
        var listenerResult = EnsureActiveSceneGameListenerReference(context, gamingCouchResult.gamingCouch, details);
        changed |= listenerResult.changed;
        if (listenerResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed,
                listenerResult.message,
                details
            );
        }

        var prefabResult = EnsureExamplePlayerPrefabReference(context, gamingCouchResult.gamingCouch, details);
        changed |= prefabResult.changed;
        if (prefabResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                changed,
                prefabResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCActiveSceneSetupStatus.Ready,
            changed,
            changed
                ? "Active Scene Setup completed."
                : "Active Scene Setup was already complete; existing assets and references were reused.",
            details
        );
    }

    internal static GCActiveSceneSetupResult EnsureActiveScenePlayerPrefabReference()
    {
        var details = new List<string>();
        var gamingCouch = GetSingleActiveSceneGamingCouch(details);
        if (gamingCouch == null)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                false,
                "Player prefab setup is blocked.",
                details
            );
        }

        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var playerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(listener, playerPrefab, out playerPrefabCompatibilityMessage))
        {
            details.Add(playerPrefabCompatibilityMessage);
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                false,
                "Player prefab setup is blocked.",
                details
            );
        }

        if (GamingCouchSceneWiring.HasObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName))
        {
            details.Add("The GamingCouch player prefab reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Ready,
                false,
                "Player prefab setup was already complete; existing reference was reused.",
                details
            );
        }

        var scriptResult = EnsureExampleScripts(
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            false
        );

        return ContinueActiveSceneObjectReferenceSetup(
            scriptResult,
            GCActiveSceneSetupAction.ActiveScenePlayerPrefab,
            gamingCouch,
            details
        );
    }

    internal static GCActiveSceneSetupResult EnsureActiveSceneGameListenerReference()
    {
        var details = new List<string>();
        var gamingCouch = GetSingleActiveSceneGamingCouch(details);
        if (gamingCouch == null)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                false,
                "Game script setup is blocked.",
                details
            );
        }

        var listenerReferenceState = GamingCouchSceneWiring.GetObjectReferenceState(
            gamingCouch,
            GamingCouchSceneWiring.ListenerPropertyName
        );
        if (listenerReferenceState == GamingCouchObjectReferenceState.Assigned)
        {
            details.Add("The GamingCouch Game script reference already contains a serialized reference.");
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Ready,
                false,
                "Game script setup was already complete; existing reference was reused.",
                details
            );
        }

        if (listenerReferenceState == GamingCouchObjectReferenceState.Missing)
        {
            details.Add("The GamingCouch listener field points to a missing GameObject and will be replaced.");
        }

        var scriptResult = EnsureExampleScripts(
            GCActiveSceneSetupAction.ActiveSceneGameListener,
            false
        );

        return ContinueActiveSceneObjectReferenceSetup(
            scriptResult,
            GCActiveSceneSetupAction.ActiveSceneGameListener,
            gamingCouch,
            details
        );
    }

    private static GCExampleScriptSetupResult CreateResult(
        GCExampleScriptSetupStatus status,
        bool changed,
        string message,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        return new GCExampleScriptSetupResult(
            status,
            changed,
            message,
            createdAssetPaths.ToArray(),
            reusedAssetPaths.ToArray(),
            blockedReasons.ToArray()
        );
    }

    private static GCActiveSceneSetupResult CreateActiveSceneResult(
        GCActiveSceneSetupStatus status,
        bool changed,
        string message,
        List<string> details
    )
    {
        return new GCActiveSceneSetupResult(status, changed, message, details.ToArray());
    }

    private static GCActiveSceneSetupResult ContinueActiveSceneObjectReferenceSetup(
        GCExampleScriptSetupResult scriptResult,
        GCActiveSceneSetupAction action,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        AddScriptResultDetails(scriptResult, details);
        if (scriptResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        if (scriptResult.IsPendingCompilation)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.PendingCompilation,
                scriptResult.changed,
                scriptResult.message,
                details
            );
        }

        var context = CreateContinuationContext(action, false);
        if (action == GCActiveSceneSetupAction.ActiveScenePlayerPrefab)
        {
            return EnsureExamplePlayerPrefabReference(context, gamingCouch, details);
        }

        if (action == GCActiveSceneSetupAction.ActiveSceneGameListener)
        {
            return EnsureActiveSceneGameListenerReference(context, gamingCouch, details);
        }

        details.Add("Unknown Active Scene Setup action: " + action + ".");
        return CreateActiveSceneResult(
            GCActiveSceneSetupStatus.Blocked,
            scriptResult.changed,
            "Active Scene Setup is blocked.",
            details
        );
    }

    private static GCActiveSceneSetupResult EnsureExamplePlayerPrefabReference(
        GCExampleAssetSetupContinuationContext context,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        var existingListener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var existingPlayerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(existingListener, existingPlayerPrefab, out playerPrefabCompatibilityMessage))
        {
            details.Add(playerPrefabCompatibilityMessage);
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                false,
                "Player prefab setup is blocked.",
                details
            );
        }

        var prefabResult = EnsureExamplePlayerPrefab(context);
        details.Add(prefabResult.message);
        AddDetails(prefabResult.blockedReasons, details);
        if (prefabResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                prefabResult.changed,
                prefabResult.message,
                details
            );
        }

        var assignResult = AssignOrReplaceActiveScenePlayerPrefab(gamingCouch, prefabResult.prefab);
        details.Add(assignResult.message);
        if (assignResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                prefabResult.changed || assignResult.changed,
                assignResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCActiveSceneSetupStatus.Ready,
            prefabResult.changed || assignResult.changed,
            prefabResult.changed || assignResult.changed
                ? "Example player prefab reference is ready."
                : "Example player prefab reference was already ready; existing assets and references were reused.",
            details
        );
    }

    private static GCActiveSceneSetupResult EnsureActiveSceneGameListenerReference(
        GCExampleAssetSetupContinuationContext context,
        GamingCouch gamingCouch,
        List<string> details
    )
    {
        var listenerResult = EnsureActiveSceneGameListener(context, gamingCouch);
        details.Add(listenerResult.message);
        AddDetails(listenerResult.blockedReasons, details);
        if (listenerResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                listenerResult.changed,
                listenerResult.message,
                details
            );
        }

        if (listenerResult.listenerObject == null)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Ready,
                listenerResult.changed,
                listenerResult.message,
                details
            );
        }

        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(gamingCouch, listenerResult.listenerObject);
        details.Add(assignResult.message);
        if (assignResult.IsBlocked)
        {
            return CreateActiveSceneResult(
                GCActiveSceneSetupStatus.Blocked,
                listenerResult.changed || assignResult.changed,
                assignResult.message,
                details
            );
        }

        return CreateActiveSceneResult(
            GCActiveSceneSetupStatus.Ready,
            listenerResult.changed || assignResult.changed,
            listenerResult.changed || assignResult.changed
                ? "Game script is ready."
                : "Game script was already ready; existing assets and references were reused.",
            details
        );
    }

    private static GamingCouch GetSingleActiveSceneGamingCouch(List<string> details)
    {
        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        if (gamingCouches.Length == 0)
        {
            details.Add("Create or reuse a GamingCouch object before wiring this reference.");
            return null;
        }

        if (gamingCouches.Length > 1)
        {
            details.Add("The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup.");
            return null;
        }

        details.Add("Reused the active scene GamingCouch object.");
        return gamingCouches[0];
    }

    private static bool IsActiveSceneSetupWiringReady(GamingCouch gamingCouch)
    {
        if (!GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName) ||
            !GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName))
        {
            return false;
        }

        string playerPrefabCompatibilityMessage;
        return IsActiveSceneGeneratedPlayerPrefabCompatible(
            GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName),
            GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName),
            out playerPrefabCompatibilityMessage
        );
    }

    private static GCActiveSceneBuildSettingsSetupResult EnsureActiveSceneFirstBuildSettingsScene(
        List<string> details
    )
    {
        var readiness = GamingCouchBuildSettingsReadiness.InspectActiveScene();
        if (readiness == null || readiness.IsReady)
        {
            return new GCActiveSceneBuildSettingsSetupResult(
                GCActiveSceneBuildSettingsSetupStatus.Ready,
                false,
                "The active scene is already the first enabled Build Settings scene.",
                new string[0]
            );
        }

        if (!readiness.CanSetFirst)
        {
            details.Add(readiness.message);
            return new GCActiveSceneBuildSettingsSetupResult(
                GCActiveSceneBuildSettingsSetupStatus.Blocked,
                false,
                "Build Settings setup is blocked.",
                new[] { readiness.message }
            );
        }

        var result = GamingCouchBuildSettingsReadiness.EnsureActiveSceneFirstEnabled();
        details.Add(result.message);
        AddDetails(result.details, details);
        return result;
    }

    private static GCGameViewAspectSetupResult EnsureGameView16By9IfSafe(List<string> details)
    {
        var readiness = GamingCouchGameViewAspect.Inspect();
        if (readiness == null || readiness.IsReady || !readiness.HasSafeSelectionAction)
        {
            return new GCGameViewAspectSetupResult(
                GCGameViewAspectSetupStatus.Ready,
                false,
                "No safe Game View 16:9 setup action is available.",
                new string[0]
            );
        }

        var result = GamingCouchGameViewAspect.SelectExisting16By9Size();
        details.Add(result.message);
        AddDetails(result.details, details);
        return result;
    }

    private static void AddScriptResultDetails(GCExampleScriptSetupResult result, List<string> details)
    {
        if (result == null)
        {
            return;
        }

        details.Add(result.message);
        AddLabeledDetails("Created", result.createdAssetPaths, details);
        AddLabeledDetails("Reused", result.reusedAssetPaths, details);
        AddDetails(result.blockedReasons, details);
    }

    private static void AddLabeledDetails(string label, string[] values, List<string> details)
    {
        if (values == null)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrEmpty(values[index]))
            {
                details.Add(label + ": " + values[index]);
            }
        }
    }

    private static void AddDetails(string[] values, List<string> details)
    {
        if (values == null)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrEmpty(values[index]))
            {
                details.Add(values[index]);
            }
        }
    }

    internal static GCExamplePlayerPrefabSetupResult EnsureExamplePlayerPrefab(
        GCExampleAssetSetupContinuationContext context
    )
    {
        var blockedReasons = new List<string>();

        if (context == null)
        {
            blockedReasons.Add("Example player prefab generation requires a scripts-ready continuation context.");
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (context.playerType == null)
        {
            blockedReasons.Add("Example player prefab generation requires the compiled " + context.playerTypeName + " type.");
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (context.playerType.Name != context.playerTypeName)
        {
            blockedReasons.Add("Example player prefab generation requires " + context.playerTypeName + ", but the continuation context provided " + context.playerType.FullName + ".");
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (!typeof(GCPlayer).IsAssignableFrom(context.playerType))
        {
            blockedReasons.Add("Compiled type " + context.playerType.FullName + " does not inherit from GCPlayer.");
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (!AssetDatabase.IsValidFolder(context.exampleFolderAssetPath))
        {
            blockedReasons.Add("Example asset folder " + context.exampleFolderAssetPath + " is missing. Run example script setup first.");
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        var existingPrefab = LoadExistingPlayerPrefab(context.playerPrefabAssetPath, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        if (existingPrefab != null)
        {
            if (existingPrefab.GetComponent(context.playerType) == null)
            {
                blockedReasons.Add("Existing prefab " + context.playerPrefabAssetPath + " does not have " + context.playerTypeName + " on its root. Existing prefab assets are never overwritten.");
                return CreatePlayerPrefabResult(
                    GCExamplePlayerPrefabSetupStatus.Blocked,
                    false,
                    existingPrefab,
                    "Example player prefab generation is blocked.",
                    blockedReasons
                );
            }

            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Ready,
                false,
                existingPrefab,
                "Reused existing example player prefab.",
                blockedReasons
            );
        }

        var createdPrefab = CreatePlayerPrefab(context, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreatePlayerPrefabResult(
                GCExamplePlayerPrefabSetupStatus.Blocked,
                false,
                null,
                "Example player prefab generation is blocked.",
                blockedReasons
            );
        }

        return CreatePlayerPrefabResult(
            GCExamplePlayerPrefabSetupStatus.Ready,
            true,
            createdPrefab,
            "Created example player prefab.",
            blockedReasons
        );
    }

    internal static GCActiveSceneGameListenerSetupResult EnsureActiveSceneGameListener(
        GCExampleAssetSetupContinuationContext context,
        GamingCouch gamingCouch
    )
    {
        var blockedReasons = new List<string>();

        if (context == null)
        {
            blockedReasons.Add("Game script setup requires a scripts-ready continuation context.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (context.gameType == null)
        {
            blockedReasons.Add("Game script setup requires the compiled " + context.gameTypeName + " type.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (context.gameType.Name != context.gameTypeName)
        {
            blockedReasons.Add("Game script setup requires " + context.gameTypeName + ", but the continuation context provided " + context.gameType.FullName + ".");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(context.gameType))
        {
            blockedReasons.Add("Compiled type " + context.gameType.FullName + " does not inherit from MonoBehaviour.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (!GCStartScreenGameScriptReceiverCompatibility.CanReceiveSetupAndPlay(context.gameType))
        {
            blockedReasons.Add("Compiled type " + context.gameType.FullName + " cannot receive both GamingCouchSetup(GCSetupOptions) and GamingCouchPlay(GCPlayOptions). Existing scripts were left untouched.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        if (gamingCouch == null)
        {
            blockedReasons.Add("A GamingCouch object is required before creating the example Game script object.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var listenerReferenceState = GamingCouchSceneWiring.GetObjectReferenceState(
            gamingCouch,
            GamingCouchSceneWiring.ListenerPropertyName
        );
        if (listenerReferenceState == GamingCouchObjectReferenceState.Assigned)
        {
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Ready,
                false,
                null,
                "The GamingCouch Game script reference already contains a serialized reference.",
                blockedReasons
            );
        }

        if (!GamingCouchSceneWiring.HasObjectReferenceSlot(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName))
        {
            blockedReasons.Add("The GamingCouch Game script serialized field could not be found.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var scene = gamingCouch.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            blockedReasons.Add("The GamingCouch object is not in a loaded scene.");
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                false,
                null,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        var listenerObject = EnsureActiveSceneGameListenerObject(context, scene, blockedReasons, out var changed);
        if (blockedReasons.Count > 0)
        {
            return CreateGameListenerResult(
                GCActiveSceneGameListenerSetupStatus.Blocked,
                changed,
                listenerObject,
                "Game script setup is blocked.",
                blockedReasons
            );
        }

        return CreateGameListenerResult(
            GCActiveSceneGameListenerSetupStatus.Ready,
            changed,
            listenerObject,
            changed ? "Created example Game script object." : "Reused existing example Game script object.",
            blockedReasons
        );
    }

    // Recursive form of EnsureProjectFolder for callers that have only a path: walks the segments
    // top-down so every missing level is created by the single-segment helper below, which is the one
    // that rejects a non-folder asset, a file, or a uniquified sibling Unity created instead.
    internal static bool EnsureProjectFolderRecursive(string assetPath, List<string> blockedReasons)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            blockedReasons.Add("Cannot create a folder without an asset path.");
            return false;
        }

        var segments = assetPath.Split('/');
        var currentAssetPath = segments[0];
        for (var index = 1; index < segments.Length; index++)
        {
            var parentAssetPath = currentAssetPath;
            currentAssetPath = parentAssetPath + "/" + segments[index];
            EnsureProjectFolder(currentAssetPath, parentAssetPath, segments[index], blockedReasons);

            // Stop at the first level that could not be created; carrying on would only report every
            // deeper level as "parent folder is missing".
            if (!AssetDatabase.IsValidFolder(currentAssetPath))
            {
                return false;
            }
        }

        return AssetDatabase.IsValidFolder(currentAssetPath);
    }

    private static void EnsureProjectFolder(
        string assetPath,
        string parentFolderAssetPath,
        string folderName,
        List<string> blockedReasons
    )
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (existingAsset != null)
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because a non-folder asset already exists at that path.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(parentFolderAssetPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because parent folder " + parentFolderAssetPath + " is missing.");
            return;
        }

        var fullPath = AssetPathToFullPath(assetPath);
        if (File.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because a file exists at that path.");
            return;
        }

        if (Directory.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            blockedReasons.Add("Cannot create folder " + assetPath + " because a directory exists at that path but Unity did not import it as a valid asset folder.");
            return;
        }

        var createdFolderGuid = AssetDatabase.CreateFolder(parentFolderAssetPath, folderName);
        if (string.IsNullOrEmpty(createdFolderGuid))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity did not create the folder.");
            return;
        }

        var createdFolderAssetPath = AssetDatabase.GUIDToAssetPath(createdFolderGuid);
        if (!string.Equals(createdFolderAssetPath, assetPath, StringComparison.Ordinal))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity created " + createdFolderAssetPath + " instead.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            blockedReasons.Add("Cannot create folder " + assetPath + " because Unity did not import it as a valid asset folder.");
        }
    }

    private static string GetAssetPathName(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        var separatorIndex = assetPath.LastIndexOf('/');
        return separatorIndex >= 0 && separatorIndex + 1 < assetPath.Length
            ? assetPath.Substring(separatorIndex + 1)
            : assetPath;
    }

    private static void EnsureScriptAsset(
        string assetPath,
        string typeName,
        string source,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        var fullPath = AssetPathToFullPath(assetPath);
        if (Directory.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create the example script " + assetPath + " because a folder (not a script file) already exists at that path." + BlockingFolderRemediationHint);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath) != null)
        {
            reusedAssetPaths.Add(assetPath);
            return;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (existingAsset != null)
        {
            blockedReasons.Add("Cannot create script " + assetPath + " because a non-script asset already exists at that path.");
            return;
        }

        if (File.Exists(fullPath))
        {
            EnsureGeneratedAssetFileWithoutOverwrite(assetPath, source, createdAssetPaths, reusedAssetPaths, blockedReasons);
            return;
        }

        if (FindTypeByName(typeName) != null)
        {
            blockedReasons.Add("Cannot create " + assetPath + " because a compiled type named " + typeName + " already exists outside the generated script path.");
            return;
        }

        EnsureGeneratedAssetFileWithoutOverwrite(assetPath, source, createdAssetPaths, reusedAssetPaths, blockedReasons);
    }

    internal static void EnsureGeneratedAssetFileWithoutOverwrite(
        string assetPath,
        string source,
        List<string> createdAssetPaths,
        List<string> reusedAssetPaths,
        List<string> blockedReasons
    )
    {
        var fullPath = AssetPathToFullPath(assetPath);
        if (File.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            reusedAssetPaths.Add(assetPath);
            return;
        }

        try
        {
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(source);
            }

            createdAssetPaths.Add(assetPath);
        }
        catch (IOException exception)
        {
            blockedReasons.Add("Could not create " + assetPath + " without overwriting existing content: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            blockedReasons.Add("Could not create " + assetPath + ": " + exception.Message);
        }
    }

    private static GCExamplePlayerPrefabSetupResult CreatePlayerPrefabResult(
        GCExamplePlayerPrefabSetupStatus status,
        bool changed,
        GameObject prefab,
        string message,
        List<string> blockedReasons
    )
    {
        return new GCExamplePlayerPrefabSetupResult(
            status,
            changed,
            prefab,
            message,
            blockedReasons.ToArray()
        );
    }

    private static GCActiveSceneGameListenerSetupResult CreateGameListenerResult(
        GCActiveSceneGameListenerSetupStatus status,
        bool changed,
        GameObject listenerObject,
        string message,
        List<string> blockedReasons
    )
    {
        return new GCActiveSceneGameListenerSetupResult(
            status,
            changed,
            listenerObject,
            message,
            blockedReasons.ToArray()
        );
    }

    private static GameObject EnsureActiveSceneGameListenerObject(
        GCExampleAssetSetupContinuationContext context,
        Scene scene,
        List<string> blockedReasons,
        out bool changed
    )
    {
        changed = false;

        var existingComponents = FindComponentsInScene(scene, context.gameType);
        if (existingComponents.Length > 1)
        {
            blockedReasons.Add("The scene contains multiple " + context.gameTypeName + " components. Assign the GamingCouch listener manually or remove duplicates before rerunning " + GetSetupDisplayName() + ".");
            return null;
        }

        if (existingComponents.Length == 1)
        {
            var componentObject = existingComponents[0].gameObject;
            if (string.Equals(componentObject.name, context.listenerObjectName, StringComparison.Ordinal))
            {
                return componentObject;
            }

            blockedReasons.Add("The scene already contains a " + context.gameTypeName + " component on " + componentObject.name + ". Assign it manually or move it to a scene object named " + context.listenerObjectName + " before rerunning setup.");
            return null;
        }

        var existingObject = FindRootGameObjectInScene(scene, context.listenerObjectName);
        if (existingObject != null)
        {
            try
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(AddGameListenerComponentUndoName);
                var listenerComponent = Undo.AddComponent(existingObject, context.gameType);
                if (listenerComponent == null)
                {
                    blockedReasons.Add("Unity did not add " + context.gameTypeName + " to existing scene object " + context.listenerObjectName + ".");
                    return existingObject;
                }

                EditorUtility.SetDirty(listenerComponent);
                EditorUtility.SetDirty(existingObject);
                GamingCouchSceneWiring.MarkSceneDirty(existingObject);
                changed = true;
                return existingObject;
            }
            catch (Exception exception)
            {
                blockedReasons.Add("Could not add " + context.gameTypeName + " to existing scene object " + context.listenerObjectName + ": " + exception.Message);
                return existingObject;
            }
        }

        GameObject listenerObject = null;
        try
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(CreateGameListenerUndoName);
            listenerObject = new GameObject(context.listenerObjectName);
            if (listenerObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(listenerObject, scene);
            }

            var listenerComponent = listenerObject.AddComponent(context.gameType);
            if (listenerComponent == null)
            {
                blockedReasons.Add("Unity did not add " + context.gameTypeName + " to the new example Game script object.");
                UnityEngine.Object.DestroyImmediate(listenerObject);
                return null;
            }

            Undo.RegisterCreatedObjectUndo(listenerObject, CreateGameListenerUndoName);
            EditorUtility.SetDirty(listenerComponent);
            EditorUtility.SetDirty(listenerObject);
            GamingCouchSceneWiring.MarkSceneDirty(listenerObject);
            changed = true;
            return listenerObject;
        }
        catch (Exception exception)
        {
            blockedReasons.Add("Could not create example Game script object " + context.listenerObjectName + ": " + exception.Message);
            if (listenerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(listenerObject);
            }

            return null;
        }
    }

    private static Component[] FindComponentsInScene(Scene scene, Type componentType)
    {
        var foundComponents = new List<Component>();
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root == null)
            {
                continue;
            }

            var components = root.GetComponentsInChildren(componentType, true);
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                if (components[componentIndex] != null)
                {
                    foundComponents.Add(components[componentIndex]);
                }
            }
        }

        return foundComponents.ToArray();
    }

    private static GameObject FindRootGameObjectInScene(Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            if (root != null && root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static GameObject LoadExistingPlayerPrefab(string playerPrefabAssetPath, List<string> blockedReasons)
    {
        var fullPath = AssetPathToFullPath(playerPrefabAssetPath);
        if (Directory.Exists(fullPath))
        {
            blockedReasons.Add("Cannot create the example player prefab " + playerPrefabAssetPath + " because a folder (not a prefab file) already exists at that path." + BlockingFolderRemediationHint);
            return null;
        }

        var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(playerPrefabAssetPath);
        if (existingAsset == null && File.Exists(fullPath))
        {
            AssetDatabase.ImportAsset(playerPrefabAssetPath, ImportAssetOptions.ForceSynchronousImport);
            existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(playerPrefabAssetPath);
            if (existingAsset == null)
            {
                blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a file exists at that path but Unity did not import it as a prefab asset.");
                return null;
            }
        }

        if (existingAsset == null)
        {
            return null;
        }

        var prefab = existingAsset as GameObject;
        if (prefab == null)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a non-prefab asset already exists at that path.");
            return null;
        }

        var prefabAssetType = PrefabUtility.GetPrefabAssetType(prefab);
        if (prefabAssetType == PrefabAssetType.NotAPrefab)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because a non-prefab asset already exists at that path.");
            return null;
        }

        if (prefabAssetType != PrefabAssetType.Regular && prefabAssetType != PrefabAssetType.Variant)
        {
            blockedReasons.Add("Cannot create player prefab " + playerPrefabAssetPath + " because the existing asset imports as a " + prefabAssetType + " prefab asset. Existing prefab assets are never overwritten.");
            return null;
        }

        return prefab;
    }

    private static GameObject CreatePlayerPrefab(
        GCExampleAssetSetupContinuationContext context,
        List<string> blockedReasons
    )
    {
        var root = new GameObject(context.playerTypeName);
        GameObject visual = null;
        try
        {
            var playerComponent = root.AddComponent(context.playerType);
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = PlayerVisualName;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0.0f, 0.75f, 0.0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var serializedPlayer = new SerializedObject(playerComponent);
                serializedPlayer.Update();
                var colorRendererProperty = serializedPlayer.FindProperty("colorRenderer");
                if (colorRendererProperty != null &&
                    colorRendererProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    colorRendererProperty.objectReferenceValue = renderer;
                    serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, context.playerPrefabAssetPath, out var success);
            if (!success || prefab == null)
            {
                blockedReasons.Add("Unity did not save the example player prefab at " + context.playerPrefabAssetPath + ".");
                return null;
            }

            return prefab;
        }
        catch (Exception exception)
        {
            blockedReasons.Add("Could not create example player prefab " + context.playerPrefabAssetPath + ": " + exception.Message);
            return null;
        }
        finally
        {
            if (visual != null && visual.transform.parent != root.transform)
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureExamplePlayerPrefabOnScriptsReady(
        GCExampleAssetSetupContinuationContext context
    )
    {
        if (context.action != GCActiveSceneSetupAction.ActiveSceneMissingPieces &&
            context.action != GCActiveSceneSetupAction.ActiveScenePlayerPrefab)
        {
            return;
        }

        var prefabResult = EnsureExamplePlayerPrefab(context);
        if (prefabResult.IsBlocked)
        {
            Debug.LogWarning(prefabResult.message + " " + string.Join(" ", prefabResult.blockedReasons));
            return;
        }

        var gamingCouch = GetGamingCouchForScriptsReadyAction(context.action);
        if (gamingCouch == null)
        {
            return;
        }

        var assignResult = AssignOrReplaceActiveScenePlayerPrefab(gamingCouch, prefabResult.prefab);
        if (assignResult.IsBlocked)
        {
            Debug.LogWarning(assignResult.message);
            return;
        }

        Debug.Log(prefabResult.message + " " + assignResult.message);
    }

    private static GamingCouchSceneWiringResult AssignOrReplaceActiveScenePlayerPrefab(
        GamingCouch gamingCouch,
        GameObject playerPrefab
    )
    {
        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var existingPlayerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        string playerPrefabCompatibilityMessage;
        if (GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName) &&
            !IsActiveSceneGeneratedPlayerPrefabCompatible(listener, existingPlayerPrefab, out playerPrefabCompatibilityMessage))
        {
            return GamingCouchSceneWiringResult.BlockedResult(
                gamingCouch,
                playerPrefabCompatibilityMessage
            );
        }

        return GamingCouchSceneWiring.AssignPlayerPrefabIfMissing(gamingCouch, playerPrefab);
    }

    private static void EnsureActiveSceneGameListenerOnScriptsReady(
        GCExampleAssetSetupContinuationContext context
    )
    {
        if (context.action != GCActiveSceneSetupAction.ActiveSceneMissingPieces &&
            context.action != GCActiveSceneSetupAction.ActiveSceneGameListener)
        {
            return;
        }

        var gamingCouch = GetGamingCouchForScriptsReadyAction(context.action);
        if (gamingCouch == null)
        {
            return;
        }

        var listenerResult = EnsureActiveSceneGameListener(context, gamingCouch);
        if (listenerResult.IsBlocked)
        {
            Debug.LogWarning(listenerResult.message + " " + string.Join(" ", listenerResult.blockedReasons));
            return;
        }

        if (listenerResult.listenerObject == null)
        {
            Debug.Log(listenerResult.message);
            return;
        }

        var assignResult = GamingCouchSceneWiring.AssignListenerIfMissing(
            gamingCouch,
            listenerResult.listenerObject
        );
        if (assignResult.IsBlocked)
        {
            Debug.LogWarning(assignResult.message);
            return;
        }

        Debug.Log(listenerResult.message + " " + assignResult.message);
    }

    // ── "Wire example game": additive upgrade of a template scene to the full example game ──────

    // Upgrades the open template scene in place: generates GCExampleGame + GCExamplePlayer, swaps the
    // "Game" listener component from GCExampleTemplate to GCExampleGame, and swaps the wired player
    // prefab from the stock GCPlayer to GCExamplePlayer. Never overwrites existing scripts or the
    // player prefab (ADR 0016). Two-phase like Active Scene Setup: the swap completes in the
    // scripts-ready continuation after Unity compiles the generated scripts.
    internal static GCWireExampleGameResult WireExampleGame()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return WireExampleGameBlocked("Exit Play Mode before wiring the example game.", null);
        }

        if (HasPendingSetup())
        {
            return WireExampleGameBlocked(
                "Setup is still finishing after generating example scripts. Wait for it to complete, then wire the example game.",
                null
            );
        }

        if (!TryGetTemplateSceneGamingCouch(out _, out var guardMessage))
        {
            return WireExampleGameBlocked(guardMessage, null);
        }

        var details = new List<string>();

        // Clear any folder sitting where a generated game/player asset must go (moved to Trash,
        // recoverable), mirroring the reset action; existing script/prefab files are still reused,
        // never overwritten. Such a folder can be full of the user's own work, so it is confirmed
        // first, exactly as the reset flow confirms it.
        var blockingFolders = FindBlockingExampleAssetFolders();
        if (ShouldConfirmBlockingFolderRemoval(blockingFolders, Application.isBatchMode) &&
            !ConfirmBlockingFolderRemoval(blockingFolders))
        {
            return WireExampleGameCancelled();
        }

        var cleanup = RemoveBlockingExampleAssetFolders();
        if (cleanup.removedAssetPaths.Length > 0)
        {
            details.Add("Moved " + cleanup.removedAssetPaths.Length + " leftover blocking folder(s) to the Trash.");
        }

        var scriptsResult = EnsureExampleScripts(
            GCActiveSceneSetupAction.ActiveSceneWireExampleGame,
            true
        );
        for (var index = 0; index < scriptsResult.createdAssetPaths.Length; index++)
        {
            details.Add("Created: " + scriptsResult.createdAssetPaths[index]);
        }
        for (var index = 0; index < scriptsResult.blockedReasons.Length; index++)
        {
            details.Add(scriptsResult.blockedReasons[index]);
        }

        if (scriptsResult.IsBlocked)
        {
            return new GCWireExampleGameResult(
                GCWireExampleGameStatus.Blocked,
                false,
                scriptsResult.changed,
                "Wire example game is blocked.",
                details.ToArray()
            );
        }

        if (scriptsResult.IsPendingCompilation)
        {
            return new GCWireExampleGameResult(
                GCWireExampleGameStatus.Wired,
                true,
                scriptsResult.changed,
                "Generating the example game scripts. The template is swapped for the full game after Unity compiles them.",
                details.ToArray()
            );
        }

        // Scripts already existed and compiled: the scripts-ready dispatch above performed the swap
        // synchronously.
        return new GCWireExampleGameResult(
            GCWireExampleGameStatus.Wired,
            false,
            scriptsResult.changed,
            "Wired the example game (GCExampleGame + GCExamplePlayer) into the scene.",
            details.ToArray()
        );
    }

    private static GCWireExampleGameResult WireExampleGameBlocked(string message, string[] details)
    {
        return new GCWireExampleGameResult(GCWireExampleGameStatus.Blocked, false, false, message, details);
    }

    private static GCWireExampleGameResult WireExampleGameCancelled()
    {
        return new GCWireExampleGameResult(
            GCWireExampleGameStatus.Cancelled,
            false,
            false,
            "Wire example game was cancelled.",
            null
        );
    }

    // Batch mode has no user to ask, so automation proceeds unprompted — the same bypass the reset
    // flow uses. internal so the EditMode suite can pin that bypass without raising a dialog.
    internal static bool ShouldConfirmBlockingFolderRemoval(string[] blockingFolders, bool isBatchMode)
    {
        return !isBatchMode && blockingFolders != null && blockingFolders.Length > 0;
    }

    private static bool ConfirmBlockingFolderRemoval(string[] blockingFolders)
    {
        return EditorUtility.DisplayDialog(
            "Wire Example Game",
            "Wiring the example game needs the generated example asset paths under " +
                ExampleFolderAssetPath + " free.\n\n" +
                GamingCouchExampleSceneCreation.DescribeResetActions(new string[0], blockingFolders) +
                "\n\nRemoved items are moved to the Trash (recoverable). Existing example scripts " +
                "and the player prefab are reused, never overwritten.",
            "Move to Trash and Wire",
            "Cancel"
        );
    }

    // Template-first guard: the open scene must have exactly one GamingCouch whose listener is a
    // "Game" object carrying a GCExampleTemplate component.
    internal static bool TryGetTemplateSceneGamingCouch(out GamingCouch gamingCouch, out string message)
    {
        gamingCouch = null;
        message = null;

        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        if (gamingCouches.Length == 0)
        {
            message = "Wire example game needs the example template scene. Run \"Create New Example Scene\" first.";
            return false;
        }

        if (gamingCouches.Length > 1)
        {
            message = "The active scene contains multiple GamingCouch components. Remove duplicates before wiring the example game.";
            return false;
        }

        var candidate = gamingCouches[0];
        var listener = GamingCouchSceneWiring.ReadObjectReference(candidate, GamingCouchSceneWiring.ListenerPropertyName) as GameObject;
        if (listener == null ||
            FindComponentByTypeName(listener, ExampleTemplateTypeName, typeof(MonoBehaviour)) == null)
        {
            message = "Wire example game needs the example template scene (a \"" + ListenerObjectName + "\" object with " + ExampleTemplateTypeName + "). Run \"Create New Example Scene\" first.";
            return false;
        }

        gamingCouch = candidate;
        return true;
    }

    // internal (not private) so the EditMode suite can drive the post-compile swap directly with
    // compiled fixture stand-ins; a real domain-reload dispatch cannot be awaited inside one test.
    internal static void SwapListenerToExampleGameOnScriptsReady(
        GCExampleAssetSetupContinuationContext context
    )
    {
        if (context.action != GCActiveSceneSetupAction.ActiveSceneWireExampleGame)
        {
            return;
        }

        // The swap registers several undo entries (template-component destroy, game-component add,
        // the player-prefab record), so collapse them into one group: a single Ctrl+Z must revert the
        // whole upgrade, including the early-return branches. The group has to open here rather than
        // in WireExampleGame because this handler runs from a post-domain-reload dispatch, by which
        // time any group the entry point opened is gone.
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(WireExampleGameUndoName);
        var undoGroup = Undo.GetCurrentGroup();
        try
        {
            var gamingCouch = GetGamingCouchForScriptsReadyAction(context.action);
            if (gamingCouch == null)
            {
                return;
            }

            if (!SwapActiveSceneListenerComponentToExampleGame(gamingCouch, context, out var swapMessage))
            {
                Debug.LogWarning(swapMessage);
                return;
            }

            var prefabResult = EnsureExamplePlayerPrefab(context);
            if (prefabResult.IsBlocked)
            {
                Debug.LogWarning(prefabResult.message + " " + string.Join(" ", prefabResult.blockedReasons));
                return;
            }

            var replaceResult = GamingCouchSceneWiring.ReplacePlayerPrefab(gamingCouch, prefabResult.prefab);
            if (replaceResult.IsBlocked)
            {
                Debug.LogWarning(replaceResult.message);
                return;
            }

            Debug.Log(swapMessage + " " + prefabResult.message + " " + replaceResult.message);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    // Removes the GCExampleTemplate component from the wired "Game" object and adds GCExampleGame in
    // its place. The GamingCouch.listener reference keeps pointing at the same "Game" object.
    private static bool SwapActiveSceneListenerComponentToExampleGame(
        GamingCouch gamingCouch,
        GCExampleAssetSetupContinuationContext context,
        out string message
    )
    {
        message = null;

        if (context.gameType == null || context.gameType.Name != context.gameTypeName)
        {
            message = "Wire example game requires the compiled " + context.gameTypeName + " type.";
            return false;
        }

        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName) as GameObject;
        if (listener == null)
        {
            message = "Wire example game could not find the wired \"" + ListenerObjectName + "\" object to upgrade.";
            return false;
        }

        var templateComponent = FindComponentByTypeName(listener, ExampleTemplateTypeName, typeof(MonoBehaviour));
        if (templateComponent != null)
        {
            Undo.DestroyObjectImmediate(templateComponent);
        }

        if (FindComponentByTypeName(listener, context.gameTypeName, typeof(MonoBehaviour)) == null)
        {
            var gameComponent = Undo.AddComponent(listener, context.gameType);
            if (gameComponent == null)
            {
                message = "Unity did not add " + context.gameTypeName + " to the \"" + ListenerObjectName + "\" object.";
                return false;
            }

            EditorUtility.SetDirty(gameComponent);
        }

        EditorUtility.SetDirty(listener);
        GamingCouchSceneWiring.MarkSceneDirty(listener);
        message = "Swapped the " + ListenerObjectName + " listener from " + ExampleTemplateTypeName + " to " + context.gameTypeName + ".";
        return true;
    }

    private static GamingCouch GetGamingCouchForScriptsReadyAction(GCActiveSceneSetupAction action)
    {
        if (action == GCActiveSceneSetupAction.ActiveSceneMissingPieces)
        {
            var gamingCouchResult = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
            if (gamingCouchResult.IsBlocked)
            {
                Debug.LogWarning(gamingCouchResult.message);
                return null;
            }

            return gamingCouchResult.gamingCouch;
        }

        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        if (gamingCouches.Length == 0)
        {
            Debug.LogWarning("Create or reuse a GamingCouch object before wiring Active Scene Setup references.");
            return null;
        }

        if (gamingCouches.Length > 1)
        {
            Debug.LogWarning("The active scene contains multiple GamingCouch components. Remove duplicates manually before running setup.");
            return null;
        }

        return gamingCouches[0];
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        if (!assetPath.StartsWith(assetsPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected a project Assets path: " + assetPath, nameof(assetPath));
        }

        return Path.Combine(Application.dataPath, assetPath.Substring(assetsPrefix.Length));
    }

    private static void PersistPendingSetup(GCActiveSceneSetupAction action)
    {
        SessionState.SetBool(PendingSetupSessionKey, true);
        SessionState.SetString(PendingActionSessionKey, action.ToString());
        SessionState.SetBool(PendingWarningLoggedSessionKey, false);
    }

    private static void ClearPendingSetup()
    {
        SessionState.SetBool(PendingSetupSessionKey, false);
        SessionState.SetString(PendingActionSessionKey, string.Empty);
        SessionState.SetBool(PendingWarningLoggedSessionKey, false);
    }

    private static void StartPendingSetupPolling()
    {
        EditorApplication.update -= ResumePendingSetupWhenReady;
        EditorApplication.update += ResumePendingSetupWhenReady;
    }

    private static void StopPendingSetupPolling()
    {
        EditorApplication.update -= ResumePendingSetupWhenReady;
    }

    private static void ResumePendingSetupWhenReady()
    {
        if (!HasPendingSetup())
        {
            StopPendingSetupPolling();
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        var context = CreateContinuationContext(GetPendingAction(), true);
        if (!context.HasRequiredTypes)
        {
            LogPendingTypeWarningOnce(context);
            return;
        }

        ClearPendingSetup();
        StopPendingSetupPolling();
        DispatchScriptsReady(context);
    }

    private static GCActiveSceneSetupAction GetPendingAction()
    {
        var value = SessionState.GetString(PendingActionSessionKey, DefaultActionValue);
        if (Enum.TryParse(value, out GCActiveSceneSetupAction action) &&
            IsKnownAction(action))
        {
            return action;
        }

        return GetDefaultAction();
    }

    private static GCActiveSceneSetupAction GetDefaultAction()
    {
        return GCActiveSceneSetupAction.ActiveSceneMissingPieces;
    }

    private static bool IsKnownAction(GCActiveSceneSetupAction action)
    {
        return action == GCActiveSceneSetupAction.ActiveSceneMissingPieces ||
               action == GCActiveSceneSetupAction.ActiveScenePlayerPrefab ||
               action == GCActiveSceneSetupAction.ActiveSceneGameListener ||
               action == GCActiveSceneSetupAction.ActiveSceneWireExampleGame;
    }

    private static GCExampleAssetSetupContinuationContext CreateContinuationContext(
        GCActiveSceneSetupAction action,
        bool resumedAfterCompilation
    )
    {
        var spec = GetScriptSetupSpec(action);
        return new GCExampleAssetSetupContinuationContext(
            action,
            resumedAfterCompilation,
            spec.scriptFolderAssetPath,
            spec.gameScriptAssetPath,
            spec.playerScriptAssetPath,
            spec.playerPrefabAssetPath,
            spec.gameTypeName,
            spec.playerTypeName,
            spec.listenerObjectName,
            FindScriptType(spec.gameScriptAssetPath, spec.gameTypeName, typeof(MonoBehaviour)),
            ResolvePlayerType(spec)
        );
    }

    // The game flavor's player type comes from its generated script; the template flavor has no
    // generated player script and spawns the stock GCPlayer, so resolve it from the loaded runtime
    // assembly instead.
    private static Type ResolvePlayerType(GCExampleScriptSetupSpec spec)
    {
        if (spec.playerScriptAssetPath == null)
        {
            return ResolveStockPlayerType(spec.playerTypeName);
        }

        return FindScriptType(spec.playerScriptAssetPath, spec.playerTypeName, typeof(GCPlayer));
    }

    // The pending-setup poller resolves this every editor tick, and FindTypeByName sweeps GetTypes()
    // over every loaded assembly, so a hit is cached. Only a hit: a tick during compilation
    // legitimately sees no type and must stay free to find one later. A hit cannot go stale — the
    // stock player lives in the package's own runtime assembly, loaded before any editor code runs
    // and unchangeable within a domain, and this static dies with the domain anyway.
    private static Type cachedStockPlayerType;

    private static Type ResolveStockPlayerType(string typeName)
    {
        if (cachedStockPlayerType != null && cachedStockPlayerType.Name == typeName)
        {
            return cachedStockPlayerType;
        }

        var stockPlayerType = FindTypeByName(typeName, typeof(GCPlayer));
        if (stockPlayerType != null)
        {
            cachedStockPlayerType = stockPlayerType;
        }

        return stockPlayerType;
    }

    private static void DispatchScriptsReady(GCExampleAssetSetupContinuationContext context)
    {
        if (scriptsReadyHandlers == null)
        {
            Debug.Log("GamingCouch example scripts are ready. Later setup tasks can continue from the scripts-ready hook.");
            return;
        }

        var handlers = scriptsReadyHandlers.GetInvocationList();
        for (var index = 0; index < handlers.Length; index++)
        {
            try
            {
                ((Action<GCExampleAssetSetupContinuationContext>)handlers[index]).Invoke(context);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void LogPendingTypeWarningOnce(GCExampleAssetSetupContinuationContext context)
    {
        if (SessionState.GetBool(PendingWarningLoggedSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(PendingWarningLoggedSessionKey, true);
        Debug.LogWarning("GamingCouch " + GetSetupDisplayName() + " is waiting for compiled types " + context.gameTypeName + " and " + context.playerTypeName + ". Existing scripts will not be overwritten.");
    }

    private static Type FindScriptType(string scriptAssetPath, string typeName, Type requiredBaseType)
    {
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptAssetPath);
        if (script == null)
        {
            return null;
        }

        var type = script.GetClass();
        if (type == null ||
            type.Name != typeName ||
            type.IsAbstract ||
            type.ContainsGenericParameters ||
            !requiredBaseType.IsAssignableFrom(type))
        {
            return null;
        }

        return type;
    }

    private static Type FindTypeByName(string typeName)
    {
        return FindTypeByName(typeName, null);
    }

    // requiredBaseType makes the search skip an unrelated type that happens to share the simple name,
    // so a namesake in another assembly cannot shadow the real one. The generator's "a compiled type
    // with this name already exists" guard deliberately passes null: any namesake blocks it (ADR 0017).
    private static Type FindTypeByName(string typeName, Type requiredBaseType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] types;
            try
            {
                types = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
            {
                continue;
            }

            for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                var type = types[typeIndex];
                if (type != null &&
                    type.Name == typeName &&
                    (requiredBaseType == null || requiredBaseType.IsAssignableFrom(type)))
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static string GenerateExampleTemplateSource()
    {
        return ReadAndRewriteMasterSource(
            ExampleTemplateMasterTypeName,
            new Dictionary<string, string>
            {
                { ExampleTemplateMasterTypeName, ExampleTemplateTypeName },
            });
    }

    private static string GenerateExampleGameSource()
    {
        return ReadAndRewriteMasterSource(
            ExampleGameMasterTypeName,
            new Dictionary<string, string>
            {
                { ExampleGameMasterTypeName, ExampleGameTypeName },
                { ExamplePlayerMasterTypeName, ExamplePlayerTypeName },
            });
    }

    private static string GenerateExamplePlayerSource()
    {
        return ReadAndRewriteMasterSource(
            ExamplePlayerMasterTypeName,
            new Dictionary<string, string>
            {
                { ExamplePlayerMasterTypeName, ExamplePlayerTypeName },
            });
    }

    private static string ReadAndRewriteMasterSource(
        string masterTypeName,
        IReadOnlyDictionary<string, string> typeNameReplacements
    )
    {
        var masterText = ReadCanonicalMasterSource(masterTypeName);
        return RewriteCanonicalMasterToGeneratedSource(masterText, typeNameReplacements, true);
    }

    // Reads a canonical example master (Tests/ExampleCanonical/<masterTypeName>.cs). Resolves the
    // file whether the SDK is an embedded/registry package or the in-repo working copy, mirroring
    // the package-vs-in-repo resolution the contract-fixture tests use.
    internal static string ReadCanonicalMasterSource(string masterTypeName)
    {
        var fullPath = ResolveCanonicalMasterFullPath(masterTypeName + ".cs");
        if (fullPath == null)
        {
            throw new FileNotFoundException(
                "Could not locate canonical example master " + masterTypeName + ".cs under " + ExampleCanonicalSourceRelativeDir + "."
            );
        }

        return File.ReadAllText(fullPath);
    }

    private static string ResolveCanonicalMasterFullPath(string fileName)
    {
        var relativePath = ExampleCanonicalSourceRelativeDir + "/" + fileName;

        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
            typeof(GamingCouchActiveSceneSetup).Assembly
        );
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            var packageCandidate = Path.Combine(packageInfo.resolvedPath, relativePath);
            if (File.Exists(packageCandidate))
            {
                return packageCandidate;
            }
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    // Turns a canonical master's text into the source generated into the user's project: drops the
    // leading maintainer comment (everything before the first using directive), strips the
    // DSB.GC.ExampleCanonical namespace wrapper and de-indents its body one level, applies the
    // "Source" -> generated type-name replacements, and (optionally) prepends ExampleTemplateHeader.
    // The generator and the golden test share this single method so generation cannot drift from the
    // compiled master.
    internal static string RewriteCanonicalMasterToGeneratedSource(
        string masterText,
        IReadOnlyDictionary<string, string> typeNameReplacements,
        bool includeExampleTemplateHeader
    )
    {
        if (masterText == null)
        {
            throw new ArgumentNullException(nameof(masterText));
        }

        var normalized = masterText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = new List<string>(normalized.Split('\n'));

        var firstUsingIndex = lines.FindIndex(
            line => line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
        );
        if (firstUsingIndex < 0)
        {
            throw new InvalidOperationException("Canonical master source has no using directive.");
        }
        lines.RemoveRange(0, firstUsingIndex);

        var namespaceIndex = lines.FindIndex(
            line => line.TrimStart().StartsWith("namespace ", StringComparison.Ordinal)
        );
        if (namespaceIndex >= 0)
        {
            var openBraceIndex = namespaceIndex + 1;
            if (openBraceIndex >= lines.Count || lines[openBraceIndex].Trim() != "{")
            {
                throw new InvalidOperationException(
                    "Canonical master namespace must be followed by an opening brace on its own line."
                );
            }

            var closeBraceIndex = lines.FindLastIndex(line => line.Trim() == "}");
            if (closeBraceIndex <= openBraceIndex)
            {
                throw new InvalidOperationException("Canonical master namespace has no closing brace.");
            }

            var rebuilt = new List<string>();
            rebuilt.AddRange(lines.GetRange(0, namespaceIndex));
            for (var i = openBraceIndex + 1; i < closeBraceIndex; i++)
            {
                rebuilt.Add(RemoveOneIndentLevel(lines[i]));
            }

            lines = rebuilt;
        }

        var generated = string.Join("\n", lines).TrimEnd('\n') + "\n";

        foreach (var replacement in typeNameReplacements)
        {
            generated = generated.Replace(replacement.Key, replacement.Value);
        }

        if (includeExampleTemplateHeader)
        {
            generated = ExampleTemplateHeader + generated;
        }

        return generated;
    }

    private static string RemoveOneIndentLevel(string line)
    {
        if (line.StartsWith("    ", StringComparison.Ordinal))
        {
            return line.Substring(4);
        }

        if (line.StartsWith("\t", StringComparison.Ordinal))
        {
            return line.Substring(1);
        }

        return line;
    }
}
