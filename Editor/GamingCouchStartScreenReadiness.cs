using System;
using System.Reflection;
using DSB.GC;
using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum GCStartScreenReadinessCheckState
{
    Pass,
    Warning,
    Fail,
    Blocked,
}

internal enum GCStartScreenReadinessSummaryState
{
    Ready,
    Warning,
    Actionable,
    Blocked,
    PendingCompilation,
}

internal enum GCStartScreenReadinessCheckId
{
    ActiveScene,
    GamingCouchInstance,
    SingleGamingCouchInstance,
    ListenerAssigned,
    PlayerPrefabAssigned,
    ActiveSceneFirstBuildSettingsScene,
    GameViewAspect16By9,
    WebGLModuleInstalled,
    WebGLExportSetup,
    LocalPlayJsonValid,
}

internal enum GCStartScreenReadinessActionId
{
    None,
    FocusSceneObject,
    FocusGameScript,
    FocusPrefab,
    CreateGamingCouch,
    CreateAndWireGameScript,
    WirePlayerPrefab,
    WireExampleGame,
    SetFirstBuildSettingsScene,
    Select16By9GameView,
    SetUpWebGLExport,
    OpenWebGLModuleInstallHelp,
}

internal sealed class GCStartScreenReadinessAction
{
    internal static readonly GCStartScreenReadinessAction NoAction = new GCStartScreenReadinessAction(
        GCStartScreenReadinessActionId.None,
        null,
        false,
        false,
        false,
        false,
        null
    );

    internal readonly GCStartScreenReadinessActionId id;
    internal readonly string label;
    internal readonly bool isSetupAction;
    internal readonly bool isFocusAction;
    // External actions hand off to something outside the editor (e.g. opening Unity Hub) and are
    // never automatable fixes: they are excluded from setup-action counts so an unmet check they
    // belong to still registers as a blocker rather than an "actionable setup" item.
    internal readonly bool isExternalAction;
    internal readonly bool requiresLoadedActiveScene;
    internal readonly UnityEngine.Object target;

    private GCStartScreenReadinessAction(
        GCStartScreenReadinessActionId id,
        string label,
        bool isSetupAction,
        bool isFocusAction,
        bool isExternalAction,
        bool requiresLoadedActiveScene,
        UnityEngine.Object target
    )
    {
        this.id = id;
        this.label = label;
        this.isSetupAction = isSetupAction;
        this.isFocusAction = isFocusAction;
        this.isExternalAction = isExternalAction;
        this.requiresLoadedActiveScene = requiresLoadedActiveScene;
        this.target = target;
    }

    internal bool IsAvailable
    {
        get { return id != GCStartScreenReadinessActionId.None; }
    }

    internal static GCStartScreenReadinessAction CreateSetupAction(
        GCStartScreenReadinessActionId id,
        string label,
        bool requiresLoadedActiveScene = true
    )
    {
        return new GCStartScreenReadinessAction(id, label, true, false, false, requiresLoadedActiveScene, null);
    }

    internal static GCStartScreenReadinessAction CreateFocusAction(
        GCStartScreenReadinessActionId id,
        string label,
        UnityEngine.Object target
    )
    {
        return target == null
            ? NoAction
            : new GCStartScreenReadinessAction(id, label, false, true, false, false, target);
    }

    internal static GCStartScreenReadinessAction CreateExternalAction(
        GCStartScreenReadinessActionId id,
        string label
    )
    {
        return new GCStartScreenReadinessAction(id, label, false, false, true, false, null);
    }
}

internal sealed class GCStartScreenReadinessCheck
{
    internal readonly GCStartScreenReadinessCheckId id;
    internal readonly string label;
    internal readonly GCStartScreenReadinessCheckState state;
    internal readonly string message;
    internal readonly string helpText;
    internal readonly GCStartScreenReadinessAction action;

    internal GCStartScreenReadinessCheck(
        GCStartScreenReadinessCheckId id,
        string label,
        GCStartScreenReadinessCheckState state,
        string message,
        string helpText = null,
        GCStartScreenReadinessAction action = null
    )
    {
        this.id = id;
        this.label = label;
        this.state = state;
        this.message = message;
        this.helpText = string.IsNullOrEmpty(helpText) ? message : helpText;
        this.action = action ?? GCStartScreenReadinessAction.NoAction;
    }

    internal bool IsSatisfied
    {
        get { return state == GCStartScreenReadinessCheckState.Pass || state == GCStartScreenReadinessCheckState.Warning; }
    }

    internal bool HasAction
    {
        get { return action != null && action.IsAvailable; }
    }

    internal bool HasSetupAction
    {
        get { return action != null && action.isSetupAction; }
    }

    internal bool HasFocusAction
    {
        get { return action != null && action.isFocusAction; }
    }

    internal bool HasExternalAction
    {
        get { return action != null && action.isExternalAction; }
    }
}

internal sealed class GCStartScreenLocalPlayJsonReadiness
{
    internal readonly bool isValid;
    internal readonly string path;
    internal readonly string message;
    internal readonly GCDevJsonValidationResult validation;

    internal GCStartScreenLocalPlayJsonReadiness(
        bool isValid,
        string path,
        string message,
        GCDevJsonValidationResult validation
    )
    {
        this.isValid = isValid;
        this.path = path;
        this.message = message;
        this.validation = validation;
    }

    internal GCDevJsonIssue[] Issues
    {
        get { return validation != null && validation.issues != null ? validation.issues : new GCDevJsonIssue[0]; }
    }
}

internal sealed class GCStartScreenReadinessSummary
{
    internal readonly GCStartScreenReadinessSummaryState state;
    internal readonly bool hasPendingCompilation;
    internal readonly int blockerCount;
    internal readonly int warningCount;
    internal readonly int actionableSetupCount;
    internal readonly string message;

    private GCStartScreenReadinessSummary(
        GCStartScreenReadinessSummaryState state,
        bool hasPendingCompilation,
        int blockerCount,
        int warningCount,
        int actionableSetupCount,
        string message
    )
    {
        this.state = state;
        this.hasPendingCompilation = hasPendingCompilation;
        this.blockerCount = blockerCount;
        this.warningCount = warningCount;
        this.actionableSetupCount = actionableSetupCount;
        this.message = message;
    }

    internal bool HasPendingItems
    {
        get { return hasPendingCompilation || blockerCount > 0 || warningCount > 0 || actionableSetupCount > 0; }
    }

    internal static GCStartScreenReadinessSummary Create(GCStartScreenReadiness readiness, bool hasPendingCompilation)
    {
        if (hasPendingCompilation)
        {
            return new GCStartScreenReadinessSummary(
                GCStartScreenReadinessSummaryState.PendingCompilation,
                true,
                0,
                0,
                0,
                "Start Screen: setup is waiting for Unity to finish compiling generated scripts."
            );
        }

        if (readiness == null)
        {
            return new GCStartScreenReadinessSummary(
                GCStartScreenReadinessSummaryState.Blocked,
                false,
                1,
                0,
                0,
                "Start Screen: readiness state is unavailable."
            );
        }

        var blockerCount = CountBlockedChecksWithoutSetupAction(readiness);
        var warningCount = CountChecks(readiness, GCStartScreenReadinessCheckState.Warning);
        var actionableSetupCount = readiness.AvailableChecklistSetupActionCount;
        var state = GetState(blockerCount, warningCount, actionableSetupCount);
        var message = FormatMessage(blockerCount, warningCount, actionableSetupCount);

        return new GCStartScreenReadinessSummary(
            state,
            false,
            blockerCount,
            warningCount,
            actionableSetupCount,
            message
        );
    }

    private static GCStartScreenReadinessSummaryState GetState(
        int blockerCount,
        int warningCount,
        int actionableSetupCount
    )
    {
        if (blockerCount > 0)
        {
            return GCStartScreenReadinessSummaryState.Blocked;
        }

        if (actionableSetupCount > 0)
        {
            return GCStartScreenReadinessSummaryState.Actionable;
        }

        return warningCount > 0
            ? GCStartScreenReadinessSummaryState.Warning
            : GCStartScreenReadinessSummaryState.Ready;
    }

    private static int CountChecks(GCStartScreenReadiness readiness, GCStartScreenReadinessCheckState state)
    {
        var count = 0;
        if (readiness.activeSceneCheck != null && readiness.activeSceneCheck.state == state)
        {
            count++;
        }

        for (var index = 0; readiness.checklist != null && index < readiness.checklist.Length; index++)
        {
            if (readiness.checklist[index] != null && readiness.checklist[index].state == state)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountBlockedChecksWithoutSetupAction(GCStartScreenReadiness readiness)
    {
        var count = 0;
        if (IsBlockedWithoutSetupAction(readiness.activeSceneCheck))
        {
            count++;
        }

        for (var index = 0; readiness.checklist != null && index < readiness.checklist.Length; index++)
        {
            if (IsBlockedWithoutSetupAction(readiness.checklist[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsBlockedWithoutSetupAction(GCStartScreenReadinessCheck check)
    {
        if (check == null ||
            (check.state != GCStartScreenReadinessCheckState.Fail &&
             check.state != GCStartScreenReadinessCheckState.Blocked))
        {
            return false;
        }

        return !check.HasSetupAction;
    }

    private static string FormatMessage(int blockerCount, int warningCount, int actionableSetupCount)
    {
        if (blockerCount == 0 && warningCount == 0 && actionableSetupCount == 0)
        {
            return "Start Screen: no pending setup items.";
        }

        var parts = new[]
        {
            FormatCount(blockerCount, "blocker"),
            FormatCount(actionableSetupCount, "setup action"),
            FormatCount(warningCount, "warning"),
        };
        var message = "Start Screen:";
        var addedPart = false;
        for (var index = 0; index < parts.Length; index++)
        {
            if (string.IsNullOrEmpty(parts[index]))
            {
                continue;
            }

            message += addedPart ? ", " : " ";
            message += parts[index];
            addedPart = true;
        }

        return message + ".";
    }

    private static string FormatCount(int count, string label)
    {
        if (count <= 0)
        {
            return null;
        }

        return count + " " + label + (count == 1 ? string.Empty : "s");
    }
}

internal sealed class GCStartScreenReadinessFacts
{
    internal readonly Scene scene;
    internal readonly GamingCouch[] gamingCouches;
    internal readonly GamingCouch gamingCouch;
    internal readonly UnityEngine.Object listener;
    internal readonly bool hasSerializedListenerReference;
    internal readonly bool hasMissingSerializedListenerReference;
    internal readonly UnityEngine.Object playerPrefab;
    internal readonly GCStartScreenLocalPlayJsonReadiness localPlayJson;
    internal readonly GCActiveSceneBuildSettingsReadiness buildSettings;
    internal readonly GCGameViewAspectReadiness gameViewAspect;
    internal readonly GCWebGLExportReadiness webGLExport;

    internal GCStartScreenReadinessFacts(
        Scene scene,
        GamingCouch[] gamingCouches = null,
        GamingCouch gamingCouch = null,
        UnityEngine.Object listener = null,
        UnityEngine.Object playerPrefab = null,
        GCStartScreenLocalPlayJsonReadiness localPlayJson = null,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null,
        bool hasSerializedListenerReference = false,
        bool hasMissingSerializedListenerReference = false
    )
    {
        this.scene = scene;
        this.gamingCouches = gamingCouches ?? new GamingCouch[0];
        this.gamingCouch = gamingCouch;
        this.listener = listener;
        this.hasSerializedListenerReference = hasSerializedListenerReference || listener != null;
        this.hasMissingSerializedListenerReference = hasMissingSerializedListenerReference && listener == null;
        this.playerPrefab = playerPrefab;
        this.localPlayJson = localPlayJson;
        this.buildSettings = buildSettings;
        this.gameViewAspect = gameViewAspect;
        this.webGLExport = webGLExport;
    }

    internal bool HasLoadedScene
    {
        get { return scene.IsValid() && scene.isLoaded; }
    }

    internal string SceneName
    {
        get { return HasLoadedScene && !string.IsNullOrEmpty(scene.name) ? scene.name : "Untitled"; }
    }

    internal string ScenePath
    {
        get { return HasLoadedScene ? scene.path : null; }
    }

    internal GCActiveSceneBuildSettingsReadiness BuildSettings
    {
        get { return buildSettings ?? GamingCouchBuildSettingsReadiness.Inspect(scene, EditorBuildSettings.scenes); }
    }

    internal GCGameViewAspectReadiness GameViewAspect
    {
        get { return gameViewAspect ?? GamingCouchGameViewAspect.InspectSizeEntries(null, -1, false, null); }
    }
}

internal sealed class GCStartScreenReadiness
{
    internal const string GamingCouchInstanceCheckLabel = "GamingCouch game object in scene";
    private const string ActiveSceneHelpText = "Requires a loaded active scene before setup inspection or changes.";
    private const string GamingCouchInstanceHelpText = "Requires one GamingCouch component in the active scene for local play wiring.";
    private const string GameScriptReadyCheckLabel = "Game script is ready";
    private const string GameScriptReadyHelpText = "Tracks the GamingCouch listener field used by your Game script for setup and play.";
    private const string PlayerPrefabAssignedHelpText = "Provides the player prefab GamingCouch spawns for connected players.";
    private const string LocalPlayJsonValidHelpText = "Validates the DevApp-generated gc.dev.json used to start local Play Mode.";
    private const string BuildSettingsHelpText = "Keeps the active scene first among enabled scenes loaded by WebGL builds.";
    private const string GameViewAspectHelpText = "Keeps the Unity Game View preview on a 16:9 aspect ratio.";
    private const string WebGLExportSetupHelpText = "Checks the WebGL target, template, and release settings for web export readiness.";
    private const string WebGLModuleInstalledCheckLabel = "Web Build Support installed";
    private const string WebGLModuleInstalledHelpText = "Web Build Support (WebGL) must be installed for this Unity Editor to build for the web.";
    private const string FocusSceneObjectActionLabel = "Focus Scene Object";
    private const string FocusGameScriptActionLabel = "Focus Game Script";
    private const string FocusPrefabActionLabel = "Focus Prefab";
    private const string CreateGamingCouchActionLabel = "Create GamingCouch";
    private const string CreateAndWireGameScriptActionLabel = "Create & Wire Game";
    private const string WirePlayerPrefabActionLabel = "Wire Player Prefab";
    private const string SetFirstBuildSettingsSceneActionLabel = "Set First Build Scene";
    private const string Select16By9GameViewActionLabel = "Select 16:9";
    private const string SetUpWebGLExportActionLabel = "Set Up Web Export";
    private const string OpenWebGLModuleInstallHelpActionLabel = "Open Unity Hub";

    internal readonly Scene scene;
    internal readonly string sceneName;
    internal readonly string scenePath;
    internal readonly GamingCouch[] gamingCouches;
    internal readonly GamingCouch gamingCouch;
    internal readonly UnityEngine.Object listener;
    internal readonly bool hasSerializedListenerReference;
    internal readonly bool hasMissingSerializedListenerReference;
    internal readonly UnityEngine.Object playerPrefab;
    internal readonly GCStartScreenLocalPlayJsonReadiness localPlayJson;
    internal readonly GCActiveSceneBuildSettingsReadiness buildSettings;
    internal readonly GCGameViewAspectReadiness gameViewAspect;
    internal readonly GCWebGLExportReadiness webGLExport;
    internal readonly GCStartScreenReadinessCheck activeSceneCheck;
    internal readonly GCStartScreenReadinessCheck[] checklist;

    internal GCStartScreenReadiness(
        Scene scene,
        GamingCouch[] gamingCouches,
        GamingCouch gamingCouch,
        UnityEngine.Object listener,
        UnityEngine.Object playerPrefab,
        GCStartScreenLocalPlayJsonReadiness localPlayJson,
        GCActiveSceneBuildSettingsReadiness buildSettings = null,
        GCGameViewAspectReadiness gameViewAspect = null,
        GCWebGLExportReadiness webGLExport = null,
        bool hasSerializedListenerReference = false,
        bool hasMissingSerializedListenerReference = false
    )
        : this(new GCStartScreenReadinessFacts(
            scene,
            gamingCouches,
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson,
            buildSettings,
            gameViewAspect,
            webGLExport,
            hasSerializedListenerReference,
            hasMissingSerializedListenerReference
        ))
    {
    }

    internal static GCStartScreenReadiness FromFacts(GCStartScreenReadinessFacts facts)
    {
        return new GCStartScreenReadiness(facts);
    }

    private GCStartScreenReadiness(GCStartScreenReadinessFacts facts)
    {
        if (facts == null)
        {
            throw new ArgumentNullException(nameof(facts));
        }

        scene = facts.scene;
        sceneName = facts.SceneName;
        scenePath = facts.ScenePath;
        gamingCouches = facts.gamingCouches;
        gamingCouch = facts.gamingCouch;
        listener = facts.listener;
        hasSerializedListenerReference = facts.hasSerializedListenerReference;
        hasMissingSerializedListenerReference = facts.hasMissingSerializedListenerReference;
        playerPrefab = facts.playerPrefab;
        localPlayJson = facts.localPlayJson;
        buildSettings = facts.BuildSettings;
        gameViewAspect = facts.GameViewAspect;
        webGLExport = facts.webGLExport;
        activeSceneCheck = BuildActiveSceneCheck();
        checklist = BuildChecklist();
    }

    internal bool IsSceneReady
    {
        get
        {
            return GetCheck(GCStartScreenReadinessCheckId.ActiveScene).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.GamingCouchInstance).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.ListenerAssigned).IsSatisfied &&
                   GetCheck(GCStartScreenReadinessCheckId.PlayerPrefabAssigned).IsSatisfied;
        }
    }

    internal bool IsLocalPlayReady
    {
        get { return IsSceneReady && GetCheck(GCStartScreenReadinessCheckId.LocalPlayJsonValid).IsSatisfied; }
    }

    internal bool HasBlockingVisibleChecklistIssues
    {
        get { return HasBlockingChecklistIssues(checklist); }
    }

    internal bool HasSafeAutomatableSetupActions
    {
        get { return SafeAutomatableSetupActionCount > 0; }
    }

    internal int SafeAutomatableSetupActionCount
    {
        get { return CountChecklistSetupActions(IsSafeAutomatableSetupAction); }
    }

    internal int AvailableChecklistSetupActionCount
    {
        get { return CountChecklistSetupActions(null); }
    }

    internal bool IsChecklistSetupActionAvailable(GCStartScreenReadinessCheckId id)
    {
        GCStartScreenReadinessCheck check;
        return TryGetCheck(id, out check) && check.HasSetupAction;
    }

    internal GCStartScreenReadinessCheck GetCheck(GCStartScreenReadinessCheckId id)
    {
        GCStartScreenReadinessCheck check;
        if (TryGetCheck(id, out check))
        {
            return check;
        }

        throw new ArgumentException("Unknown readiness check: " + id, nameof(id));
    }

    internal bool TryGetCheck(GCStartScreenReadinessCheckId id, out GCStartScreenReadinessCheck check)
    {
        id = NormalizeCheckId(id);
        if (id == GCStartScreenReadinessCheckId.ActiveScene)
        {
            check = activeSceneCheck;
            return check != null;
        }

        for (var index = 0; checklist != null && index < checklist.Length; index++)
        {
            if (checklist[index] != null && checklist[index].id == id)
            {
                check = checklist[index];
                return true;
            }
        }

        check = null;
        return false;
    }

    internal static GCStartScreenReadinessCheckId NormalizeCheckId(GCStartScreenReadinessCheckId id)
    {
        return id == GCStartScreenReadinessCheckId.SingleGamingCouchInstance
            ? GCStartScreenReadinessCheckId.GamingCouchInstance
            : id;
    }

    internal static bool HasBlockingChecklistIssues(GCStartScreenReadinessCheck[] checks)
    {
        for (var index = 0; checks != null && index < checks.Length; index++)
        {
            if (checks[index] == null)
            {
                continue;
            }

            if (checks[index].state == GCStartScreenReadinessCheckState.Fail ||
                checks[index].state == GCStartScreenReadinessCheckState.Blocked)
            {
                return true;
            }
        }

        return false;
    }

    private GCStartScreenReadinessCheck[] BuildChecklist()
    {
        return new[]
        {
            BuildGamingCouchInstanceCheck(),
            BuildListenerAssignedCheck(),
            BuildPlayerPrefabAssignedCheck(),
            BuildActiveSceneFirstBuildSettingsSceneCheck(),
            BuildGameViewAspect16By9Check(),
            BuildWebGLModuleInstalledCheck(),
            BuildWebGLExportSetupCheck(),
            BuildLocalPlayJsonValidCheck(),
        };
    }

    private GCStartScreenReadinessCheck BuildActiveSceneCheck()
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveScene,
                "Active scene is available",
                GCStartScreenReadinessCheckState.Fail,
                "No loaded active scene is available for GamingCouch setup inspection.",
                ActiveSceneHelpText
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ActiveScene,
            "Active scene is available",
            GCStartScreenReadinessCheckState.Pass,
            "Inspecting active scene: " + sceneName + ".",
            ActiveSceneHelpText
        );
    }

    private GCStartScreenReadinessCheck BuildGamingCouchInstanceCheck()
    {
        if (gamingCouches.Length == 0)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GamingCouchInstance,
                GamingCouchInstanceCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                "Create a GamingCouch object to continue active-scene setup.",
                GamingCouchInstanceHelpText,
                GCStartScreenReadinessAction.CreateSetupAction(
                    GCStartScreenReadinessActionId.CreateGamingCouch,
                    CreateGamingCouchActionLabel
                )
            );
        }

        if (gamingCouches.Length > 1)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GamingCouchInstance,
                GamingCouchInstanceCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                "The active scene contains multiple GamingCouch components. Remove duplicates manually before running Active Scene Setup.",
                GamingCouchInstanceHelpText
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.GamingCouchInstance,
            GamingCouchInstanceCheckLabel,
            GCStartScreenReadinessCheckState.Pass,
            "The active scene has one GamingCouch component.",
            GamingCouchInstanceHelpText,
            CreateFocusSceneObjectAction(gamingCouch)
        );
    }

    private GCStartScreenReadinessCheck BuildListenerAssignedCheck()
    {
        if (gamingCouch == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ListenerAssigned,
                GameScriptReadyCheckLabel,
                GCStartScreenReadinessCheckState.Blocked,
                "Game script readiness can be checked after the active scene has exactly one GamingCouch component.",
                GameScriptReadyHelpText
            );
        }

        if (listener == null)
        {
            if (hasMissingSerializedListenerReference)
            {
                return new GCStartScreenReadinessCheck(
                    GCStartScreenReadinessCheckId.ListenerAssigned,
                    GameScriptReadyCheckLabel,
                    GCStartScreenReadinessCheckState.Fail,
                    "The GamingCouch listener field points to a missing GameObject. Use Create & Wire Game to replace it, or clear the missing listener reference manually.",
                    GameScriptReadyHelpText,
                    GamingCouchSceneWiring.HasObjectReferenceSlot(
                        gamingCouch,
                        GamingCouchSceneWiring.ListenerPropertyName
                    )
                        ? CreateAndWireGameScriptAction()
                        : GCStartScreenReadinessAction.NoAction
                );
            }

            if (hasSerializedListenerReference)
            {
                return new GCStartScreenReadinessCheck(
                    GCStartScreenReadinessCheckId.ListenerAssigned,
                    GameScriptReadyCheckLabel,
                    GCStartScreenReadinessCheckState.Fail,
                    "The GamingCouch listener reference could not be resolved. It may point to a deleted object, an unloaded asset, or a script that no longer compiles. Clear or replace the listener reference manually.",
                    GameScriptReadyHelpText
                );
            }

            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ListenerAssigned,
                GameScriptReadyCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                "Assign a Game script object to the GamingCouch listener field.",
                GameScriptReadyHelpText,
                CanAssignMissingActiveSceneReference(GamingCouchSceneWiring.ListenerPropertyName)
                    ? CreateAndWireGameScriptAction()
                    : GCStartScreenReadinessAction.NoAction
            );
        }

        var compatibility = GCStartScreenGameScriptReceiverCompatibility.Inspect(listener);
        if (!compatibility.isCompatible)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ListenerAssigned,
                GameScriptReadyCheckLabel,
                GCStartScreenReadinessCheckState.Fail,
                compatibility.message,
                GameScriptReadyHelpText
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ListenerAssigned,
            GameScriptReadyCheckLabel,
            GCStartScreenReadinessCheckState.Pass,
            "The GamingCouch Game script reference points to " + listener.name + ".",
            GameScriptReadyHelpText,
            CreateFocusGameScriptAction(listener)
        );
    }

    private GCStartScreenReadinessCheck BuildPlayerPrefabAssignedCheck()
    {
        if (gamingCouch == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
                "Player prefab is assigned",
                GCStartScreenReadinessCheckState.Blocked,
                "Player prefab assignment can be checked after the active scene has exactly one GamingCouch component.",
                PlayerPrefabAssignedHelpText
            );
        }

        if (playerPrefab == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
                "Player prefab is assigned",
                GCStartScreenReadinessCheckState.Fail,
                "The GamingCouch player prefab reference is missing.",
                PlayerPrefabAssignedHelpText,
                CanAssignMissingActiveSceneReference(GamingCouchSceneWiring.PlayerPrefabPropertyName)
                    ? WirePlayerPrefabAction()
                    : GCStartScreenReadinessAction.NoAction
            );
        }

        string playerPrefabCompatibilityMessage;
        if (!GamingCouchActiveSceneSetup.IsActiveSceneGeneratedPlayerPrefabCompatible(
            listener,
            playerPrefab,
            out playerPrefabCompatibilityMessage
        ))
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
                "Player prefab is assigned",
                GCStartScreenReadinessCheckState.Fail,
                playerPrefabCompatibilityMessage,
                PlayerPrefabAssignedHelpText,
                GCStartScreenReadinessAction.NoAction
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.PlayerPrefabAssigned,
            "Player prefab is assigned",
            GCStartScreenReadinessCheckState.Pass,
            "The GamingCouch player prefab reference points to " + playerPrefab.name + ".",
            PlayerPrefabAssignedHelpText,
            CreateFocusPrefabAction(playerPrefab)
        );
    }

    private GCStartScreenReadinessCheck BuildLocalPlayJsonValidCheck()
    {
        if (localPlayJson == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                "Local play JSON is valid",
                GCStartScreenReadinessCheckState.Fail,
                "gc.dev.json validation did not produce a result.",
                LocalPlayJsonValidHelpText
            );
        }

        if (localPlayJson.isValid)
        {
            var warningCount = localPlayJson.validation != null ? localPlayJson.validation.WarningCount : 0;
            if (warningCount > 0)
            {
                return new GCStartScreenReadinessCheck(
                    GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                    "Local play JSON is valid",
                    GCStartScreenReadinessCheckState.Warning,
                    "gc.dev.json is valid with " + warningCount + " warning" + (warningCount == 1 ? string.Empty : "s") + ".",
                    LocalPlayJsonValidHelpText
                );
            }

            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.LocalPlayJsonValid,
                "Local play JSON is valid",
                GCStartScreenReadinessCheckState.Pass,
                "gc.dev.json is valid for local Play Mode.",
                LocalPlayJsonValidHelpText
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.LocalPlayJsonValid,
            "Local play JSON is valid",
            GCStartScreenReadinessCheckState.Fail,
            string.IsNullOrEmpty(localPlayJson.message)
                ? "gc.dev.json is missing or invalid for local Play Mode."
                : localPlayJson.message,
            LocalPlayJsonValidHelpText
        );
    }

    private GCStartScreenReadinessCheck BuildActiveSceneFirstBuildSettingsSceneCheck()
    {
        if (buildSettings == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Fail,
                "Build Settings readiness could not be inspected.",
                BuildSettingsHelpText
            );
        }

        if (buildSettings.IsReady)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
                "Active scene is first Build Settings scene",
                GCStartScreenReadinessCheckState.Pass,
                buildSettings.message,
                BuildSettingsHelpText
            );
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.ActiveSceneFirstBuildSettingsScene,
            "Active scene is first Build Settings scene",
            buildSettings.status == GCActiveSceneBuildSettingsStatus.NoActiveScene
                ? GCStartScreenReadinessCheckState.Blocked
                : GCStartScreenReadinessCheckState.Fail,
            buildSettings.message,
            BuildSettingsHelpText,
            buildSettings.CanSetFirst
                ? GCStartScreenReadinessAction.CreateSetupAction(
                    GCStartScreenReadinessActionId.SetFirstBuildSettingsScene,
                    SetFirstBuildSettingsSceneActionLabel
                )
                : GCStartScreenReadinessAction.NoAction
        );
    }

    private GCStartScreenReadinessCheck BuildGameViewAspect16By9Check()
    {
        if (gameViewAspect == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.GameViewAspect16By9,
                "Game View uses 16:9 preview",
                GCStartScreenReadinessCheckState.Warning,
                "Game View aspect could not be inspected. Choose a 16:9 Game View entry manually if needed.",
                GameViewAspectHelpText
            );
        }

        var state = GCStartScreenReadinessCheckState.Fail;
        if (gameViewAspect.status == GCGameViewAspectStatus.Ready)
        {
            state = GCStartScreenReadinessCheckState.Pass;
        }
        else if (gameViewAspect.status == GCGameViewAspectStatus.Unknown)
        {
            state = GCStartScreenReadinessCheckState.Warning;
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.GameViewAspect16By9,
            "Game View uses 16:9 preview",
            state,
            gameViewAspect.message,
            GameViewAspectHelpText,
            state != GCStartScreenReadinessCheckState.Pass && gameViewAspect.HasSafeSelectionAction
                ? GCStartScreenReadinessAction.CreateSetupAction(
                    GCStartScreenReadinessActionId.Select16By9GameView,
                    Select16By9GameViewActionLabel,
                    false
                )
                : GCStartScreenReadinessAction.NoAction
        );
    }

    private GCStartScreenReadinessCheck BuildWebGLModuleInstalledCheck()
    {
        if (webGLExport != null && webGLExport.webGLModuleInstalled)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.WebGLModuleInstalled,
                WebGLModuleInstalledCheckLabel,
                GCStartScreenReadinessCheckState.Pass,
                "Web Build Support is installed for this Unity Editor.",
                WebGLModuleInstalledHelpText
            );
        }

        // No editor API can install a Hub module, so this is a manual, non-automatable fix: the
        // action opens Unity Hub and copies the exact steps, but the check stays a hard blocker.
        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.WebGLModuleInstalled,
            WebGLModuleInstalledCheckLabel,
            GCStartScreenReadinessCheckState.Fail,
            "Web Build Support (the WebGL platform) is not installed for this Unity Editor. GamingCouch games run on the web, so it is required. Install it from Unity Hub (Add modules), then reopen this project.",
            WebGLModuleInstalledHelpText,
            GCStartScreenReadinessAction.CreateExternalAction(
                GCStartScreenReadinessActionId.OpenWebGLModuleInstallHelp,
                OpenWebGLModuleInstallHelpActionLabel
            )
        );
    }

    private GCStartScreenReadinessCheck BuildWebGLExportSetupCheck()
    {
        if (webGLExport == null)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.WebGLExportSetup,
                "Web export settings configured",
                GCStartScreenReadinessCheckState.Fail,
                "Web export settings readiness could not be inspected.",
                WebGLExportSetupHelpText
            );
        }

        // The WebGL module check above owns this problem; defer here (no misleading "switch to
        // WebGL" fix) so the developer acts on the real blocker first.
        if (!webGLExport.webGLModuleInstalled)
        {
            return new GCStartScreenReadinessCheck(
                GCStartScreenReadinessCheckId.WebGLExportSetup,
                "Web export settings configured",
                GCStartScreenReadinessCheckState.Blocked,
                "Install Web Build Support before configuring web export settings.",
                WebGLExportSetupHelpText
            );
        }

        var state = GCStartScreenReadinessCheckState.Fail;
        switch (webGLExport.status)
        {
            case GCWebGLExportSetupStatus.Ready:
                state = GCStartScreenReadinessCheckState.Pass;
                break;
            case GCWebGLExportSetupStatus.Warning:
                state = GCStartScreenReadinessCheckState.Warning;
                break;
            case GCWebGLExportSetupStatus.Blocked:
                state = GCStartScreenReadinessCheckState.Blocked;
                break;
        }

        return new GCStartScreenReadinessCheck(
            GCStartScreenReadinessCheckId.WebGLExportSetup,
            "Web export settings configured",
            state,
            webGLExport.message,
            WebGLExportSetupHelpText,
            state != GCStartScreenReadinessCheckState.Pass && (webGLExport.IsBlocked || webGLExport.HasWarning)
                ? GCStartScreenReadinessAction.CreateSetupAction(
                    GCStartScreenReadinessActionId.SetUpWebGLExport,
                    SetUpWebGLExportActionLabel,
                    false
                )
                : GCStartScreenReadinessAction.NoAction
        );
    }

    private int CountChecklistSetupActions(Func<GCStartScreenReadinessAction, bool> predicate)
    {
        var count = 0;
        for (var index = 0; checklist != null && index < checklist.Length; index++)
        {
            var action = checklist[index] != null ? checklist[index].action : null;
            if (action != null &&
                action.isSetupAction &&
                (predicate == null || predicate(action)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSafeAutomatableSetupAction(GCStartScreenReadinessAction action)
    {
        if (action == null)
        {
            return false;
        }

        switch (action.id)
        {
            case GCStartScreenReadinessActionId.CreateGamingCouch:
            case GCStartScreenReadinessActionId.CreateAndWireGameScript:
            case GCStartScreenReadinessActionId.WirePlayerPrefab:
            case GCStartScreenReadinessActionId.SetFirstBuildSettingsScene:
            case GCStartScreenReadinessActionId.Select16By9GameView:
                return true;
            default:
                return false;
        }
    }

    private bool CanAssignMissingActiveSceneReference(string propertyName)
    {
        return gamingCouch != null &&
               GamingCouchSceneWiring.HasObjectReferenceSlot(gamingCouch, propertyName) &&
               !GamingCouchSceneWiring.HasObjectReference(gamingCouch, propertyName);
    }

    private static GCStartScreenReadinessAction CreateAndWireGameScriptAction()
    {
        return GCStartScreenReadinessAction.CreateSetupAction(
            GCStartScreenReadinessActionId.CreateAndWireGameScript,
            CreateAndWireGameScriptActionLabel
        );
    }

    private static GCStartScreenReadinessAction WirePlayerPrefabAction()
    {
        return GCStartScreenReadinessAction.CreateSetupAction(
            GCStartScreenReadinessActionId.WirePlayerPrefab,
            WirePlayerPrefabActionLabel
        );
    }

    private static GCStartScreenReadinessAction CreateFocusSceneObjectAction(UnityEngine.Object target)
    {
        return GCStartScreenReadinessAction.CreateFocusAction(
            GCStartScreenReadinessActionId.FocusSceneObject,
            FocusSceneObjectActionLabel,
            GetSelectionTarget(target)
        );
    }

    private static GCStartScreenReadinessAction CreateFocusGameScriptAction(UnityEngine.Object target)
    {
        return GCStartScreenReadinessAction.CreateFocusAction(
            GCStartScreenReadinessActionId.FocusGameScript,
            FocusGameScriptActionLabel,
            GetSelectionTarget(target)
        );
    }

    private static GCStartScreenReadinessAction CreateFocusPrefabAction(UnityEngine.Object target)
    {
        return GCStartScreenReadinessAction.CreateFocusAction(
            GCStartScreenReadinessActionId.FocusPrefab,
            FocusPrefabActionLabel,
            GetPrefabSelectionTarget(target)
        );
    }

    private static UnityEngine.Object GetPrefabSelectionTarget(UnityEngine.Object target)
    {
        var selectionTarget = GetSelectionTarget(target);
        if (selectionTarget == null)
        {
            return null;
        }

        if (AssetDatabase.Contains(selectionTarget))
        {
            return selectionTarget;
        }

        var gameObject = selectionTarget as GameObject;
        if (gameObject == null)
        {
            return selectionTarget;
        }

        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        return prefabAsset != null ? prefabAsset : selectionTarget;
    }

    private static UnityEngine.Object GetSelectionTarget(UnityEngine.Object target)
    {
        if (target == null)
        {
            return null;
        }

        var component = target as Component;
        if (component != null && component.gameObject != null)
        {
            return component.gameObject;
        }

        return target;
    }
}

internal sealed class GCStartScreenGameScriptReceiverCompatibility
{
    private const string SetupMethodName = "GamingCouchSetup";
    private const string PlayMethodName = "GamingCouchPlay";

    internal readonly bool isCompatible;
    internal readonly string message;

    private GCStartScreenGameScriptReceiverCompatibility(bool isCompatible, string message)
    {
        this.isCompatible = isCompatible;
        this.message = message;
    }

    internal static GCStartScreenGameScriptReceiverCompatibility Inspect(UnityEngine.Object listener)
    {
        var gameObject = ResolveGameObject(listener);
        if (gameObject == null)
        {
            return new GCStartScreenGameScriptReceiverCompatibility(
                false,
                "The GamingCouch Game script reference must point to a GameObject with a compatible listener component."
            );
        }

        var components = gameObject.GetComponents<Component>();
        for (var index = 0; components != null && index < components.Length; index++)
        {
            if (CanReceiveSetupAndPlay(components[index]))
            {
                return new GCStartScreenGameScriptReceiverCompatibility(
                    true,
                    "The GamingCouch Game script reference points to " + gameObject.name + "."
                );
            }
        }

        return new GCStartScreenGameScriptReceiverCompatibility(
            false,
            "The GamingCouch Game script reference points to " + gameObject.name + ", but no component on that object can receive both GamingCouchSetup(GCSetupOptions) and GamingCouchPlay(GCPlayOptions). Add a compatible Game script component or replace the listener reference."
        );
    }

    private static GameObject ResolveGameObject(UnityEngine.Object listener)
    {
        var gameObject = listener as GameObject;
        if (gameObject != null)
        {
            return gameObject;
        }

        var component = listener as Component;
        return component != null ? component.gameObject : null;
    }

    internal static bool CanReceiveSetupAndPlay(Type type)
    {
        return type != null &&
               typeof(Component).IsAssignableFrom(type) &&
               HasReceiverMethod(type, SetupMethodName, typeof(GCSetupOptions)) &&
               HasReceiverMethod(type, PlayMethodName, typeof(GCPlayOptions));
    }

    private static bool CanReceiveSetupAndPlay(Component component)
    {
        if (component == null)
        {
            return false;
        }

        return CanReceiveSetupAndPlay(component.GetType());
    }

    private static bool HasReceiverMethod(Type type, string methodName, Type parameterType)
    {
        while (type != null && type != typeof(MonoBehaviour))
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                new[] { parameterType },
                null
            );

            if (method != null)
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }
}

internal static class GCStartScreenReadinessService
{
    internal static GCStartScreenReadinessSummary InspectActiveSceneSummary()
    {
        return GCStartScreenReadinessSummary.Create(
            InspectActiveScene(),
            GamingCouchActiveSceneSetup.HasPendingSetup()
        );
    }

    internal static GCStartScreenReadiness InspectActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var gamingCouches = GamingCouchSceneWiring.FindGamingCouchesInScene(scene);
        var gamingCouch = gamingCouches.Length == 1 ? gamingCouches[0] : null;
        var listener = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var hasSerializedListenerReference = GamingCouchSceneWiring.HasObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var hasMissingSerializedListenerReference = GamingCouchSceneWiring.HasMissingObjectReference(gamingCouch, GamingCouchSceneWiring.ListenerPropertyName);
        var playerPrefab = GamingCouchSceneWiring.ReadObjectReference(gamingCouch, GamingCouchSceneWiring.PlayerPrefabPropertyName);
        var localPlayJson = InspectLocalPlayJson();
        var buildSettings = GamingCouchBuildSettingsReadiness.Inspect(scene, EditorBuildSettings.scenes);
        var gameViewAspect = GamingCouchGameViewAspect.Inspect();
        var webGLExport = GamingCouchWebGLExportSetup.InspectReadiness();

        return GCStartScreenReadiness.FromFacts(new GCStartScreenReadinessFacts(
            scene,
            gamingCouches,
            gamingCouch,
            listener,
            playerPrefab,
            localPlayJson,
            buildSettings,
            gameViewAspect,
            webGLExport,
            hasSerializedListenerReference,
            hasMissingSerializedListenerReference
        ));
    }

    private static GCStartScreenLocalPlayJsonReadiness InspectLocalPlayJson()
    {
        try
        {
            var readResult = new GCDevJsonStore().Read();
            if (readResult == null)
            {
                return new GCStartScreenLocalPlayJsonReadiness(
                    false,
                    null,
                    "gc.dev.json could not be read because the read result was missing.",
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.ReadError,
                        "gc.dev.json could not be read because the read result was missing.",
                        null
                    ))
                );
            }

            var validation = readResult.validation ?? GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                GCDevJsonIssueCode.ReadError,
                "gc.dev.json validation did not produce a result.",
                readResult.parsedFile != null ? readResult.parsedFile.path : null
            ));

            return new GCStartScreenLocalPlayJsonReadiness(
                readResult.IsValid,
                readResult.parsedFile != null ? readResult.parsedFile.path : null,
                readResult.IsValid ? null : "gc.dev.json is missing, invalid, or rejected by valid platform data gates.",
                validation
            );
        }
        catch (Exception exception)
        {
            var message = "gc.dev.json could not be validated: " + exception.Message;
            return new GCStartScreenLocalPlayJsonReadiness(
                false,
                null,
                message,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }
    }
}
