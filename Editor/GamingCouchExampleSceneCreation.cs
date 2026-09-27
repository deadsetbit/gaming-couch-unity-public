using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if GC_HAS_UGUI
using UnityEngine.UI;
#endif

internal enum GCExampleSceneCreationStatus
{
    Created,
    Cancelled,
    Blocked,
}

internal sealed class GCExampleSceneCreationResult
{
    internal readonly GCExampleSceneCreationStatus status;
    internal readonly bool isPendingCompilation;
    internal readonly string scenePath;
    internal readonly string message;
    internal readonly string[] details;
    internal readonly string[] existingAssetPaths;

    internal GCExampleSceneCreationResult(
        GCExampleSceneCreationStatus status,
        bool isPendingCompilation,
        string scenePath,
        string message,
        string[] details,
        string[] existingAssetPaths
    )
    {
        this.status = status;
        this.isPendingCompilation = isPendingCompilation;
        this.scenePath = scenePath;
        this.message = message;
        this.details = details ?? new string[0];
        this.existingAssetPaths = existingAssetPaths ?? new string[0];
    }

    internal bool IsCreated { get { return status == GCExampleSceneCreationStatus.Created; } }
    internal bool IsCancelled { get { return status == GCExampleSceneCreationStatus.Cancelled; } }
    internal bool IsBlocked { get { return status == GCExampleSceneCreationStatus.Blocked; } }
    internal bool IsPendingCompilation { get { return isPendingCompilation; } }
}

internal enum GCExampleSceneKind
{
    Template,
    ExampleGame,
}

internal sealed class GCExampleSceneSpec
{
    internal readonly string optionName;
    internal readonly string caption;
    internal readonly string sceneAssetPath;
    internal readonly GCActiveSceneSetupAction setupAction;
    internal readonly string overlayTitle;
    internal readonly string overlayHint;

    internal GCExampleSceneSpec(
        string optionName,
        string caption,
        string sceneAssetPath,
        GCActiveSceneSetupAction setupAction,
        string overlayTitle,
        string overlayHint
    )
    {
        this.optionName = optionName;
        this.caption = caption;
        this.sceneAssetPath = sceneAssetPath;
        this.setupAction = setupAction;
        this.overlayTitle = overlayTitle;
        this.overlayHint = overlayHint;
    }
}

// Creates one of the two starting scenes: the template (GCExampleTemplate + stock GCPlayer) or the
// example game (GCExampleGame + GCExamplePlayer). Each kind owns its own scene file, so creating
// one never touches the other. Unlike Active Scene Setup, which wires the scene the user already
// has open, this saves modified scenes, opens a fresh scene with the default camera + light, moves
// that kind's previous scene, scripts and player prefab plus any leftover blocking folders to the
// Trash, then reuses Active Scene Setup to generate fresh copies and wire the GamingCouch object,
// listener and player prefab. A developer creating a scene again is usually starting over, so
// every press gets the original example rather than whatever state the files were left in; the
// Trash keeps the old copies recoverable. Making the new scene the first
// Build Settings scene is intentionally left to the existing "Set up missing pieces" action so
// creating a scene never silently changes which scene a build boots into.
[InitializeOnLoad]
internal static class GamingCouchExampleSceneCreation
{
    internal const string ActionName = "Create Example Scene";

    private static readonly GCExampleSceneSpec TemplateSceneSpec = new GCExampleSceneSpec(
        "Template",
        "An empty game that logs each lifecycle step. Start your own game here.",
        GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "/GCTemplateScene.unity",
        GCActiveSceneSetupAction.ActiveSceneMissingPieces,
        "GamingCouch template scene",
        "Copy GCExampleTemplate.cs to start your own game."
    );

    private static readonly GCExampleSceneSpec ExampleGameSceneSpec = new GCExampleSceneSpec(
        "Example Game",
        "A playable game with scoring, timed rounds and elimination. Read it to learn the SDK.",
        GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "/GCExampleGameScene.unity",
        GCActiveSceneSetupAction.ActiveSceneExampleGame,
        "GamingCouch example game scene",
        "Read GCExampleGame.cs and GCExamplePlayer.cs to learn the SDK."
    );

    private const string PendingScenePathSessionKey = "DSB.GC.ExampleSceneCreation.PendingScenePath.v1";
    private const string PendingSceneKindSessionKey = "DSB.GC.ExampleSceneCreation.PendingSceneKind.v1";

    static GamingCouchExampleSceneCreation()
    {
        // Calling into GamingCouchActiveSceneSetup runs its static constructor first, which
        // registers the listener and player-prefab handlers. This one is therefore dispatched after
        // them and sees the finished wiring.
        GamingCouchActiveSceneSetup.RegisterScriptsReadyHandler(FinishPendingSceneOnScriptsReady);
    }

    internal static GCExampleSceneSpec GetSpec(GCExampleSceneKind kind)
    {
        return kind == GCExampleSceneKind.ExampleGame ? ExampleGameSceneSpec : TemplateSceneSpec;
    }

    internal static GCExampleSceneCreationResult CreateScene(GCExampleSceneKind kind)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return Blocked(null, "Exit Play Mode before creating a new scene.", null, null);
        }

        if (GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return Blocked(
                null,
                "Setup is still finishing after generating example scripts. Wait for it to complete, then create the scene.",
                null,
                null
            );
        }

        var spec = GetSpec(kind);
        var sceneAssetPath = spec.sceneAssetPath;
        var scriptSpec = GamingCouchActiveSceneSetup.GetScriptSetupSpec(spec.setupAction);
        var ownedAssetPaths = GetOwnedAssetPaths(scriptSpec);
        var blockingFolders = GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(ownedAssetPaths);
        var existingAssetPaths = FindExistingAssets(sceneAssetPath, ownedAssetPaths, blockingFolders);
        var hasSomethingToReplace = existingAssetPaths.Length > 0 || blockingFolders.Length > 0;
        if (hasSomethingToReplace && !Application.isBatchMode)
        {
            var confirmed = EditorUtility.DisplayDialog(
                ActionName,
                DescribeResetActions(existingAssetPaths, blockingFolders) +
                    "\n\nYou can restore anything moved to the Trash.",
                "Create Clean Scene",
                "Cancel"
            );
            if (!confirmed)
            {
                return Cancelled(existingAssetPaths);
            }
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return Cancelled(existingAssetPaths);
        }

        // Switch to a fresh scene BEFORE removing the previous scene, so we never delete the scene
        // that is currently open. In Single mode this closes it if it is open.
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove this kind's previous files and any leftover blocking folders (e.g. a directory
        // literally named "GCExampleGame.cs"). A trashed script's class stays loaded until Unity
        // recompiles, so the generator is told those names are free to write again.
        var removalReasons = new List<string>();
        var removedAssets = RemoveAssets(existingAssetPaths, removalReasons);
        MarkTrashedScriptTypes(scriptSpec, removedAssets);
        var cleanup = GamingCouchActiveSceneSetup.RemoveBlockingExampleAssetFolders(ownedAssetPaths);
        AppendRange(removalReasons, cleanup.blockedReasons);
        if (removalReasons.Count > 0 ||
            GamingCouchActiveSceneSetup.FindBlockingExampleAssetFolders(ownedAssetPaths).Length > 0)
        {
            return Blocked(
                null,
                "Could not remove the previous files before creating a clean scene. Remove the listed items manually, then try again.",
                removalReasons.ToArray(),
                existingAssetPaths
            );
        }

        var blockedReasons = new List<string>();
        if (!GamingCouchActiveSceneSetup.EnsureProjectFolderRecursive(
                GamingCouchActiveSceneSetup.ExampleFolderAssetPath,
                blockedReasons
            ))
        {
            return Blocked(
                null,
                "Could not create the example folder for the new scene.",
                blockedReasons.ToArray(),
                existingAssetPaths
            );
        }

        // The scene path is normally free now that the previous scene is trashed; fall back to a
        // unique name only if something still occupies it, rather than overwriting.
        var scenePath = AssetExistsAtPath(sceneAssetPath)
            ? AssetDatabase.GenerateUniqueAssetPath(sceneAssetPath)
            : sceneAssetPath;
        if (!EditorSceneManager.SaveScene(newScene, scenePath))
        {
            return Blocked(scenePath, "Could not save the new scene to " + scenePath + ".", null, existingAssetPaths);
        }

        var setupResult = GamingCouchActiveSceneSetup.EnsureActiveSceneSetup(false, spec.setupAction);

        // Add an on-screen label so the otherwise-empty Game View (players only spawn at runtime)
        // points the user at the generated example scripts.
        CreateSceneInfoOverlay(newScene, spec);

        // Persist the GamingCouch object (and any synchronously wired references) so they survive
        // the domain reload that first-run example-script generation triggers. When wiring has to
        // wait for that compile, FinishPendingSceneOnScriptsReady saves the scene again afterwards.
        SaveWithoutPendingUndo(newScene);

        var details = new List<string>();
        AddRemovedExampleDetail(details, removedAssets, cleanup.removedAssetPaths);
        AppendRange(details, setupResult != null ? setupResult.details : null);

        var sceneName = Path.GetFileNameWithoutExtension(scenePath);
        if (setupResult != null && setupResult.IsBlocked)
        {
            return new GCExampleSceneCreationResult(
                GCExampleSceneCreationStatus.Blocked,
                false,
                scenePath,
                "Created " + sceneName + ", but setting it up is blocked.",
                details.ToArray(),
                existingAssetPaths
            );
        }

        var pending = setupResult != null && setupResult.IsPendingCompilation;
        if (pending)
        {
            SessionState.SetString(PendingScenePathSessionKey, scenePath);
            SessionState.SetInt(PendingSceneKindSessionKey, (int)kind);
        }

        var message = pending
            ? "Created " + sceneName + ". It finishes setting up after Unity compiles the generated scripts."
            : BuildReadyMessage(sceneName);

        Debug.Log(
            BuildGeneratedFilesGuidance(message, spec),
            AssetDatabase.LoadMainAssetAtPath(scriptSpec.gameScriptAssetPath)
        );

        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            pending,
            scenePath,
            message,
            details.ToArray(),
            existingAssetPaths
        );
    }

    internal static string BuildReadyMessage(string sceneName)
    {
        return "Created " + sceneName + ". Press Play to run it.";
    }

    // Runs after the listener and player-prefab handlers once Unity has compiled the generated
    // scripts. The scene was created by the press that queued this, so it holds nothing the
    // developer wrote and is saved without asking.
    private static void FinishPendingSceneOnScriptsReady(GCExampleAssetSetupContinuationContext context)
    {
        var scenePath = SessionState.GetString(PendingScenePathSessionKey, string.Empty);
        var spec = GetSpec((GCExampleSceneKind)SessionState.GetInt(PendingSceneKindSessionKey, (int)GCExampleSceneKind.Template));
        if (string.IsNullOrEmpty(scenePath) || context.action != spec.setupAction)
        {
            return;
        }

        SessionState.EraseString(PendingScenePathSessionKey);
        SessionState.EraseInt(PendingSceneKindSessionKey);

        var result = FinishPendingScene(scenePath);
        if (result.IsCreated)
        {
            Debug.Log("GamingCouch: " + result.message);
        }
        else
        {
            Debug.LogWarning("GamingCouch: " + result.message);
        }

        GamingCouchStartScreenWindow.ShowResultIfOpen(
            GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(result)
        );
    }

    private static GCExampleSceneCreationResult FinishPendingScene(string scenePath)
    {
        var sceneName = Path.GetFileNameWithoutExtension(scenePath);
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, scenePath, StringComparison.Ordinal))
        {
            return Blocked(
                scenePath,
                sceneName + " was no longer the open scene when Unity finished compiling, so it was not wired. Run " + ActionName + " again.",
                null,
                null
            );
        }

        if (!IsSceneWired())
        {
            return Blocked(scenePath, "Created " + sceneName + ", but could not finish wiring it. The Console has the reason.", null, null);
        }

        if (!SaveWithoutPendingUndo(scene))
        {
            return Blocked(scenePath, "Wired " + sceneName + ", but Unity did not save it. Save the scene before closing it.", null, null);
        }

        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Created,
            false,
            scenePath,
            BuildReadyMessage(sceneName),
            null,
            null
        );
    }

    // The wiring records Undo steps that Unity only processes at the end of the editor tick, and
    // processing them marks the scene dirty again. Processing them before the save leaves the
    // scene clean.
    private static bool SaveWithoutPendingUndo(Scene scene)
    {
        Undo.FlushUndoRecordObjects();
        return EditorSceneManager.SaveScene(scene);
    }

    private static bool IsSceneWired()
    {
        var gamingCouches = GamingCouchSceneWiring.FindActiveSceneGamingCouches();
        return gamingCouches.Length == 1 &&
               GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouches[0], GamingCouchSceneWiring.ListenerPropertyName) &&
               GamingCouchSceneWiring.HasAssignedObjectReference(gamingCouches[0], GamingCouchSceneWiring.PlayerPrefabPropertyName);
    }

    // The generated files a scene kind owns: its listener script, its player script (the template
    // has none and spawns the stock GCPlayer) and its player prefab.
    internal static string[] GetOwnedAssetPaths(GCExampleScriptSetupSpec scriptSpec)
    {
        var paths = new List<string> { scriptSpec.gameScriptAssetPath };
        if (scriptSpec.playerScriptAssetPath != null)
        {
            paths.Add(scriptSpec.playerScriptAssetPath);
        }

        paths.Add(scriptSpec.playerPrefabAssetPath);
        return paths.ToArray();
    }

    // Folders at the owned paths are left to the blocking-folder cleanup, so they are not listed
    // twice in the confirmation.
    private static string[] FindExistingAssets(string sceneAssetPath, string[] ownedAssetPaths, string[] blockingFolders)
    {
        var skipped = new HashSet<string>(blockingFolders);
        var existing = new List<string>();
        if (AssetExistsAtPath(sceneAssetPath))
        {
            existing.Add(sceneAssetPath);
        }

        for (var i = 0; i < ownedAssetPaths.Length; i++)
        {
            if (!skipped.Contains(ownedAssetPaths[i]) && AssetExistsAtPath(ownedAssetPaths[i]))
            {
                existing.Add(ownedAssetPaths[i]);
            }
        }

        return existing.ToArray();
    }

    private static void MarkTrashedScriptTypes(GCExampleScriptSetupSpec scriptSpec, string[] removedAssets)
    {
        var removed = new HashSet<string>(removedAssets);
        if (removed.Contains(scriptSpec.gameScriptAssetPath))
        {
            GamingCouchActiveSceneSetup.MarkExampleTypeTrashed(scriptSpec.gameTypeName);
        }

        if (scriptSpec.playerScriptAssetPath != null && removed.Contains(scriptSpec.playerScriptAssetPath))
        {
            GamingCouchActiveSceneSetup.MarkExampleTypeTrashed(scriptSpec.playerTypeName);
        }
    }

    // Moves assets to the Trash (recoverable). Returns the ones actually removed; any failure is
    // recorded in blockedReasons so the caller can surface it.
    private static string[] RemoveAssets(string[] assetPaths, List<string> blockedReasons)
    {
        var removed = new List<string>();
        for (var i = 0; i < assetPaths.Length; i++)
        {
            var path = assetPaths[i];
            if (!AssetExistsAtPath(path))
            {
                continue;
            }

            if (AssetDatabase.MoveAssetToTrash(path))
            {
                removed.Add(path);
            }
            else
            {
                blockedReasons.Add("Could not move " + path + " to the Trash.");
            }
        }

        return removed.ToArray();
    }

    private static bool AssetExistsAtPath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
    }

    private static string DescribeResetActions(string[] existingAssetPaths, string[] blockingFolders)
    {
        var lines = new List<string>();
        AppendDescribedPaths(lines, "Move to the Trash and create fresh copies:", existingAssetPaths);
        AppendDescribedPaths(lines, "Remove " + blockingFolders.Length + " leftover folder(s) blocking the example scripts:", blockingFolders);
        return string.Join("\n", lines);
    }

    private static void AppendDescribedPaths(List<string> lines, string heading, string[] paths)
    {
        if (paths == null || paths.Length == 0)
        {
            return;
        }

        lines.Add(heading);
        for (var i = 0; i < paths.Length; i++)
        {
            lines.Add("  - " + paths[i]);
        }
    }

    private static void AddRemovedExampleDetail(List<string> details, string[] removedAssets, string[] removedFolders)
    {
        if (removedAssets != null && removedAssets.Length > 0)
        {
            details.Add("Moved the previous " + string.Join(", ", removedAssets) + " to the Trash.");
        }

        if (removedFolders != null && removedFolders.Length > 0)
        {
            details.Add("Moved " + removedFolders.Length + " leftover blocking folder(s) to the Trash.");
        }
    }

    private static void AppendRange(List<string> target, string[] source)
    {
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Length; i++)
        {
            target.Add(source[i]);
        }
    }

    private static void CreateSceneInfoOverlay(Scene scene, GCExampleSceneSpec spec)
    {
#if GC_HAS_UGUI
        var root = new GameObject("GamingCouch Example Info", typeof(Canvas), typeof(CanvasScaler));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        panelImage.raycastTarget = false;
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -24f);
        panelRect.sizeDelta = new Vector2(1040f, 210f);

        var textObject = new GameObject("Text", typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        var text = textObject.GetComponent<Text>();
        text.font = GetBuiltinFont();
        text.text = BuildSceneInfoText(spec);
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 28;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 16f);
        textRect.offsetMax = new Vector2(-24f, -16f);

        if (root.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(root, scene);
        }
#endif
    }

#if GC_HAS_UGUI
    private static Font GetBuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
#endif

    internal static string BuildSceneInfoText(GCExampleSceneSpec spec)
    {
        return
            spec.overlayTitle + "\n" +
            spec.caption + "\n" +
            spec.overlayHint + "\n" +
            "Scripts: " + GamingCouchActiveSceneSetup.ExampleFolderAssetPath + "\n" +
            "Players spawn at runtime on the Gaming Couch platform.\n" +
            "(You can delete this label.)";
    }

    internal static string BuildGeneratedFilesGuidance(string headline, GCExampleSceneSpec spec)
    {
        return
            "GamingCouch: " + headline + "\n" +
            "Browse the generated example files and grow them into your game:\n" +
            "  - " + string.Join("\n  - ", GetOwnedAssetPaths(GamingCouchActiveSceneSetup.GetScriptSetupSpec(spec.setupAction)));
    }

    private static GCExampleSceneCreationResult Cancelled(string[] existingAssetPaths)
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Cancelled,
            false,
            null,
            ActionName + " was cancelled.",
            null,
            existingAssetPaths
        );
    }

    private static GCExampleSceneCreationResult Blocked(
        string scenePath,
        string message,
        string[] details,
        string[] existingAssetPaths
    )
    {
        return new GCExampleSceneCreationResult(
            GCExampleSceneCreationStatus.Blocked,
            false,
            scenePath,
            message,
            details,
            existingAssetPaths
        );
    }
}
