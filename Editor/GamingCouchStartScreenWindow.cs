using System;
using System.Collections.Generic;
using System.Reflection;
using DSB.GC.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal sealed class GamingCouchStartScreenWindow : EditorWindow
{
    internal const string WindowTitle = "GamingCouch Start Screen";
    private const float MinWindowWidth = 420f;
    private const float MinWindowHeight = 360f;
    private const float PopupWindowWidth = 460f;
    private const float PopupWindowHeight = 560f;
    private const float ChecklistRowHeight = 28f;
    private const float ChecklistRowPaddingX = 8f;
    private const float ChecklistStatusWidth = 28f;
    private const float ChecklistMinimumButtonWidth = 96f;
    private const float ChecklistButtonWidth = 148f;
    private const float ChecklistHelpButtonWidth = 22f;
    private const float ChecklistColumnSpacing = 6f;
    // Indent messages to the label's left edge: past the row padding, the status-indicator
    // column, and the spacing after it. This aligns the message with the item text above it.
    private const float ChecklistMessageIndent = ChecklistRowPaddingX + ChecklistStatusWidth + ChecklistColumnSpacing;
    private const float ChecklistStatusIndicatorSize = 10f;
    private const float SceneRowHeight = 26f;
    private const float SceneRowButtonWidth = 74f;
    private const float SceneRowActiveTagWidth = 64f;
    private const float SceneRowAccentWidth = 2f;

    private GCStartScreenReadiness readiness;
    private GCGamingCouchSceneEntry[] gamingCouchScenes = new GCGamingCouchSceneEntry[0];
    // Built from gamingCouchScenes + readiness and read more than once per OnGUI; both refreshes
    // clear it.
    private List<GCGamingCouchSceneEntry> displayedScenes;
    private Vector2 scrollPosition;
    private string actionMessage;
    private string[] actionDetails = new string[0];
    private MessageType actionMessageType = MessageType.Info;
    private bool hasSelectedChecklistHelp;
    private GCStartScreenReadinessCheckId selectedChecklistHelpId;

    // Row styles are rebuilt on first draw after every domain reload, because Unity clears static
    // GUIStyle fields there and EditorStyles is only readable from inside OnGUI.
    private static GUIStyle sceneNameStyle;
    private static GUIStyle activeSceneNameStyle;
    private static GUIStyle scenePathStyle;
    private static GUIStyle sceneActiveTagStyle;
    private static GUIStyle checklistLabelStyle;

    // EditorWindow.docked is internal to UnityEditor; cache the reflection lookup since
    // IsDocked() runs every OnGUI frame.
    private static readonly PropertyInfo EditorWindowDockedProperty = typeof(EditorWindow).GetProperty(
        "docked",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    internal static GamingCouchStartScreenWindow Open()
    {
        // Detect a fresh open before GetWindow (which returns an existing instance
        // when one is already present) so we only reposition newly created windows.
        var isNewWindow = !HasOpenWindow();

        var window = GetWindow<GamingCouchStartScreenWindow>(false, WindowTitle);
        window.Refresh();
        window.Show();

        if (isNewWindow)
        {
            window.CenterOnMainWindowIfFloating();
        }

        return window;
    }

    private static bool HasOpenWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<GamingCouchStartScreenWindow>();
        return windows != null && windows.Length > 0;
    }

    // Presents the window like a popup on first open: centered over the Unity main
    // window at a comfortable size. Skipped when docked so we never yank a window the
    // user has intentionally placed in their layout.
    private void CenterOnMainWindowIfFloating()
    {
        if (IsDocked())
        {
            return;
        }

        var mainWindow = EditorGUIUtility.GetMainWindowPosition();
        if (mainWindow.width <= 0f || mainWindow.height <= 0f)
        {
            return;
        }

        // Never let the popup exceed the editor window; a larger rect would spill off-screen.
        var size = new Vector2(
            Mathf.Min(Mathf.Max(PopupWindowWidth, minSize.x), mainWindow.width),
            Mathf.Min(Mathf.Max(PopupWindowHeight, minSize.y), mainWindow.height)
        );
        var centered = new Rect(Vector2.zero, size)
        {
            center = mainWindow.center
        };

        // Clamp fully inside the editor window so the title bar always stays reachable,
        // even if GetMainWindowPosition reports an unexpected rect during early startup.
        centered.x = Mathf.Clamp(centered.x, mainWindow.xMin, mainWindow.xMax - centered.width);
        centered.y = Mathf.Clamp(centered.y, mainWindow.yMin, mainWindow.yMax - centered.height);
        position = centered;
    }

    private bool IsDocked()
    {
        // Treat an unreadable value as "floating" so a fresh window still centers.
        return EditorWindowDockedProperty != null
            && EditorWindowDockedProperty.GetValue(this, null) is bool docked
            && docked;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        minSize = new Vector2(MinWindowWidth, MinWindowHeight);
        Refresh();
        RefreshSceneCatalog();
    }

    private void OnFocus()
    {
        RefreshSceneCatalog();
        Refresh();
    }

    private void OnHierarchyChange()
    {
        Refresh();
        Repaint();
    }

    private void OnProjectChange()
    {
        // Saving, adding, or deleting a scene reimports assets and lands here; rescan so the
        // Gaming Couch scenes list reflects which scene files now hold a GamingCouch component.
        RefreshSceneCatalog();
        Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        if (readiness == null)
        {
            Refresh();
        }

        using (new EditorGUILayout.VerticalScope())
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            DrawGamingCouchScenesSection();
            DrawActiveSceneIssue();
            DrawSceneSummary();
            DrawChecklist();
            DrawLocalPlayJsonDetails();
            DrawPendingSetupStatus();
            DrawActions();
            DrawActionResult();
            EditorGUILayout.EndScrollView();

            // The auto-open toggle only governs the floating startup popup. A docked window
            // is restored by Unity's layout regardless, so the toggle is irrelevant there.
            if (!IsDocked())
            {
                DrawAutoOpenSettings();
            }
        }
    }

    private void Refresh()
    {
        readiness = GCStartScreenReadinessService.InspectActiveScene();
        displayedScenes = null;
    }

    private void RefreshSceneCatalog()
    {
        gamingCouchScenes = GamingCouchSceneCatalog.FindGamingCouchScenes();
        displayedScenes = null;
    }

    private void DrawGamingCouchScenesSection()
    {
        EditorGUILayout.LabelField("Gaming Couch scenes", EditorStyles.boldLabel);

        var scenes = GetDisplayedScenes();
        if (scenes.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No scenes with a GamingCouch component were found yet. Create a new example scene to get started.",
                MessageType.Info
            );
        }
        else
        {
            var activeScenePath = GetActiveScenePath();
            for (var index = 0; index < scenes.Count; index++)
            {
                DrawGamingCouchSceneRow(scenes[index], index, activeScenePath);
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(GamingCouchActiveSceneSetup.HasPendingSetup()))
        {
            if (GUILayout.Button("Create New Example Scene"))
            {
                RunDeferred(RunCreateNewExampleScene);
            }

            if (GUILayout.Button("Wire Example Game"))
            {
                RunDeferred(RunWireExampleGame);
            }
        }

        EditorGUILayout.Space();
    }

    // The catalog is scanned from saved scene files. Overlay the active scene from the live
    // readiness state so a GamingCouch just added to (or a scene just opened in) the editor shows
    // up immediately, even before it is saved.
    private List<GCGamingCouchSceneEntry> GetDisplayedScenes()
    {
        if (displayedScenes != null)
        {
            return displayedScenes;
        }

        var scenes = new List<GCGamingCouchSceneEntry>();
        if (gamingCouchScenes != null)
        {
            scenes.AddRange(gamingCouchScenes);
        }

        var activeScenePath = GetActiveScenePath();
        if (!string.IsNullOrEmpty(activeScenePath) &&
            ActiveSceneHasGamingCouch() &&
            !ContainsScenePath(scenes, activeScenePath))
        {
            scenes.Add(new GCGamingCouchSceneEntry(readiness.sceneName, activeScenePath));
            scenes.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
        }

        displayedScenes = scenes;
        return scenes;
    }

    private static bool ContainsScenePath(List<GCGamingCouchSceneEntry> scenes, string path)
    {
        for (var index = 0; index < scenes.Count; index++)
        {
            var entry = scenes[index];
            if (entry != null && string.Equals(entry.path, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private string GetActiveScenePath()
    {
        return readiness != null ? readiness.scenePath : null;
    }

    private bool IsActiveSceneInScenesList()
    {
        var activeScenePath = GetActiveScenePath();
        return !string.IsNullOrEmpty(activeScenePath) &&
            ContainsScenePath(GetDisplayedScenes(), activeScenePath);
    }

    private bool ActiveSceneHasGamingCouch()
    {
        return readiness != null && readiness.gamingCouches != null && readiness.gamingCouches.Length > 0;
    }

    private void DrawGamingCouchSceneRow(GCGamingCouchSceneEntry entry, int index, string activeScenePath)
    {
        if (entry == null)
        {
            return;
        }

        var isActive = !string.IsNullOrEmpty(entry.path) &&
            !string.IsNullOrEmpty(activeScenePath) &&
            string.Equals(entry.path, activeScenePath, StringComparison.Ordinal);

        var rowRect = EditorGUILayout.GetControlRect(false, SceneRowHeight);
        DrawSceneRowBackground(rowRect, index, isActive);

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var lineY = rowRect.y + (rowRect.height - lineHeight) * 0.5f;
        var contentLeft = rowRect.x + ChecklistRowPaddingX + SceneRowAccentWidth;
        var contentRight = rowRect.xMax - ChecklistRowPaddingX;

        // Right-hand control: an "Active" tag on the current scene, an "Open" button on the rest.
        if (isActive)
        {
            var tagRect = new Rect(contentRight - SceneRowActiveTagWidth, lineY, SceneRowActiveTagWidth, lineHeight);
            GUI.Label(tagRect, "Active", GetSceneActiveTagStyle());
            contentRight = tagRect.x - ChecklistColumnSpacing;
        }
        else
        {
            var buttonRect = new Rect(contentRight - SceneRowButtonWidth, lineY, SceneRowButtonWidth, lineHeight);
            using (new EditorGUI.DisabledScope(
                GamingCouchActiveSceneSetup.HasPendingSetup() || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUI.Button(buttonRect, "Open"))
                {
                    RunDeferred(() => OpenGamingCouchScene(entry));
                }
            }

            contentRight = buttonRect.x - ChecklistColumnSpacing;
        }

        // Scene name, then a dimmed project path filling any remaining space.
        var nameStyle = GetSceneNameStyle(isActive);
        var nameContent = new GUIContent(entry.name, entry.path);
        var nameWidth = Mathf.Min(nameStyle.CalcSize(nameContent).x, Mathf.Max(0f, contentRight - contentLeft));
        var nameRect = new Rect(contentLeft, lineY, nameWidth, lineHeight);
        if (HasVisibleRect(nameRect))
        {
            GUI.Label(nameRect, nameContent, nameStyle);
        }

        var pathLeft = nameRect.xMax + ChecklistColumnSpacing;
        if (pathLeft < contentRight)
        {
            var pathRect = new Rect(pathLeft, lineY, contentRight - pathLeft, lineHeight);
            GUI.Label(pathRect, new GUIContent(entry.path, entry.path), GetScenePathStyle());
        }
    }

    private void OpenGamingCouchScene(GCGamingCouchSceneEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.path))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SetActionResult("Exit Play Mode before switching scenes.", MessageType.Warning, null);
            Repaint();
            return;
        }

        // Already open: nothing to load, just make sure the checks reflect the current state.
        if (string.Equals(entry.path, GetActiveScenePath(), StringComparison.Ordinal))
        {
            Refresh();
            Repaint();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        try
        {
            EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
        }
        catch (Exception exception)
        {
            SetActionResult("Could not open " + entry.path + ".", MessageType.Error, new[] { exception.Message });
            Repaint();
            return;
        }

        // The active scene changed: rescan the catalog and re-run the readiness checks so the
        // checklist immediately reflects the newly opened scene.
        RefreshSceneCatalog();
        Refresh();
        Repaint();
    }

    private static void DrawSceneRowBackground(Rect rowRect, int index, bool isActive)
    {
        var rowColor = isActive
            ? (EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.55f, 0.32f, 0.22f)
                : new Color(0.30f, 0.66f, 0.38f, 0.20f))
            : GetChecklistRowColor(index);
        EditorGUI.DrawRect(rowRect, rowColor);

        var dividerColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.08f);
        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), dividerColor);

        if (isActive)
        {
            EditorGUI.DrawRect(
                new Rect(rowRect.x, rowRect.y, SceneRowAccentWidth, rowRect.height),
                new Color(0.22f, 0.72f, 0.34f, 1f)
            );
        }
    }

    private static GUIStyle GetSceneNameStyle(bool isActive)
    {
        if (isActive)
        {
            if (activeSceneNameStyle == null)
            {
                activeSceneNameStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };
            }

            return activeSceneNameStyle;
        }

        if (sceneNameStyle == null)
        {
            sceneNameStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Normal
            };
        }

        return sceneNameStyle;
    }

    private static GUIStyle GetScenePathStyle()
    {
        if (scenePathStyle == null)
        {
            scenePathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            var color = scenePathStyle.normal.textColor;
            color.a = 0.6f;
            scenePathStyle.normal.textColor = color;
        }

        return scenePathStyle;
    }

    private static GUIStyle GetSceneActiveTagStyle()
    {
        if (sceneActiveTagStyle == null)
        {
            sceneActiveTagStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.35f, 0.8f, 0.45f, 1f) }
            };
        }

        return sceneActiveTagStyle;
    }

    private void DrawActiveSceneIssue()
    {
        if (readiness == null)
        {
            return;
        }

        var check = FindReadinessCheck(GCStartScreenReadinessCheckId.ActiveScene);
        if (check == null || check.IsSatisfied)
        {
            return;
        }

        var message = string.IsNullOrEmpty(check.message)
            ? "No loaded active scene is available for GamingCouch setup inspection."
            : check.message;
        EditorGUILayout.HelpBox(message, GetMessageType(check.state));
        EditorGUILayout.Space();
    }

    private static void DrawAutoOpenSettings()
    {
        EditorGUILayout.Space();
        var suppressed = GCStartScreenSettings.SuppressAutoOpen;
        var nextSuppressed = EditorGUILayout.ToggleLeft("Never open this again on startup", suppressed);
        if (nextSuppressed != suppressed)
        {
            GCStartScreenSettings.SuppressAutoOpen = nextSuppressed;
        }

        EditorGUILayout.Space();
    }

    private void DrawSceneSummary()
    {
        if (readiness == null)
        {
            EditorGUILayout.HelpBox("Readiness state is unavailable.", MessageType.Error);
            return;
        }

        // The Gaming Couch scenes list already shows and highlights the active scene when it holds a
        // GamingCouch component; only fall back to an explicit summary when it is not in that list.
        if (IsActiveSceneInScenesList())
        {
            return;
        }

        EditorGUILayout.LabelField("Active Scene", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Name", readiness.sceneName);
        if (!string.IsNullOrEmpty(readiness.scenePath))
        {
            EditorGUILayout.LabelField("Path", readiness.scenePath);
        }

        EditorGUILayout.Space();
    }

    private void DrawChecklist()
    {
        if (readiness == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);
        for (var index = 0; index < readiness.checklist.Length; index++)
        {
            DrawChecklistItem(readiness.checklist[index], index);
        }

        EditorGUILayout.Space();
    }

    private void DrawChecklistItem(GCStartScreenReadinessCheck check, int index)
    {
        if (check == null)
        {
            return;
        }

        var rowRect = EditorGUILayout.GetControlRect(false, ChecklistRowHeight);
        DrawChecklistRowBackground(rowRect, index);

        var contentRect = new Rect(
            rowRect.x + ChecklistRowPaddingX,
            rowRect.y + 4f,
            Mathf.Max(0f, rowRect.width - ChecklistRowPaddingX * 2f),
            EditorGUIUtility.singleLineHeight
        );

        var buttonLabel = check.HasAction ? check.action.label : null;
        var helpContent = GetChecklistHelpContent(check);
        var statusRect = new Rect(
            contentRect.x,
            contentRect.y,
            Mathf.Min(ChecklistStatusWidth, contentRect.width),
            contentRect.height
        );

        var contentLeft = statusRect.width > 0f
            ? statusRect.xMax + ChecklistColumnSpacing
            : contentRect.x;
        var contentRight = contentRect.xMax;
        var helpRect = Rect.zero;
        if (helpContent != null)
        {
            var availableHelpWidth = contentRight - contentLeft;
            if (availableHelpWidth >= ChecklistHelpButtonWidth)
            {
                helpRect = new Rect(
                    contentRight - ChecklistHelpButtonWidth,
                    contentRect.y,
                    ChecklistHelpButtonWidth,
                    contentRect.height
                );
                contentRight = helpRect.x - ChecklistColumnSpacing;
            }
        }

        var buttonRect = Rect.zero;
        if (!string.IsNullOrEmpty(buttonLabel))
        {
            var availableButtonWidth = contentRight - contentLeft;
            if (availableButtonWidth >= ChecklistMinimumButtonWidth)
            {
                var buttonWidth = Mathf.Min(ChecklistButtonWidth, availableButtonWidth);
                buttonRect = new Rect(
                    contentRight - buttonWidth,
                    contentRect.y,
                    buttonWidth,
                    contentRect.height
                );
                contentRight = buttonRect.x - ChecklistColumnSpacing;
            }
        }

        DrawChecklistStatusIndicator(statusRect, check.state);

        var labelRect = new Rect(
            contentLeft,
            contentRect.y,
            Mathf.Max(0f, contentRight - contentLeft),
            contentRect.height
        );
        if (HasVisibleRect(labelRect))
        {
            GUI.Label(labelRect, check.label, GetChecklistLabelStyle());
        }

        if (HasVisibleRect(buttonRect))
        {
            using (new EditorGUI.DisabledScope(IsChecklistActionDisabled(check)))
            {
                if (GUI.Button(buttonRect, buttonLabel))
                {
                    RunDeferred(() => RunChecklistAction(check));
                }
            }
        }

        if (HasVisibleRect(helpRect))
        {
            if (GUI.Button(helpRect, helpContent, EditorStyles.iconButton))
            {
                ToggleChecklistHelp(check);
            }
        }

        if (check.state != GCStartScreenReadinessCheckState.Pass && !string.IsNullOrEmpty(check.message))
        {
            DrawChecklistMessage(check.message, GetMessageType(check.state));
        }

        if (IsChecklistHelpSelected(check))
        {
            DrawChecklistMessage(check.helpText, MessageType.Info);
        }
    }

    private static void DrawChecklistMessage(string message, MessageType messageType)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(ChecklistMessageIndent);
            EditorGUILayout.HelpBox(message, messageType);
        }
    }

    private void DrawLocalPlayJsonDetails()
    {
        if (readiness == null)
        {
            return;
        }

        var localPlayJson = readiness.localPlayJson;
        if (!ShouldShowLocalPlayJsonDetails(localPlayJson))
        {
            return;
        }

        EditorGUILayout.LabelField("Play Mode Readiness", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(localPlayJson.path))
        {
            EditorGUILayout.LabelField("File", localPlayJson.path);
        }

        var issues = localPlayJson.Issues;
        if (!HasDisplayableIssues(issues))
        {
            if (localPlayJson.isValid)
            {
                var warningCount = GetWarningCount(localPlayJson);
                var warningMessage = "gc.dev.json is valid with " + warningCount + " warning" + (warningCount == 1 ? string.Empty : "s") + ".";
                EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                EditorGUILayout.Space();
                return;
            }

            EditorGUILayout.HelpBox(GetLocalPlayJsonBlockedMessage(localPlayJson), MessageType.Error);
            EditorGUILayout.Space();
            return;
        }

        var displayedError = false;
        for (var index = 0; index < issues.Length; index++)
        {
            var issue = issues[index];
            if (issue == null)
            {
                continue;
            }

            var messageType = issue.severity == GCDevJsonIssueSeverity.Error ? MessageType.Error : MessageType.Warning;
            var message = GCDevJsonIssueFormatter.Format(issue);
            if (string.IsNullOrEmpty(message))
            {
                message = issue.severity == GCDevJsonIssueSeverity.Error
                    ? "gc.dev.json has a validation error."
                    : "gc.dev.json has a validation warning.";
            }

            if (issue.severity == GCDevJsonIssueSeverity.Error)
            {
                displayedError = true;
                message += " Local Play Mode remains blocked until DevApp provides valid local play JSON; this screen will not create or repair it.";
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        if (!localPlayJson.isValid && !displayedError)
        {
            EditorGUILayout.HelpBox(GetLocalPlayJsonBlockedMessage(localPlayJson), MessageType.Error);
        }

        EditorGUILayout.Space();
    }

    private static bool ShouldShowLocalPlayJsonDetails(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        if (localPlayJson == null)
        {
            return false;
        }

        if (!localPlayJson.isValid)
        {
            return true;
        }

        return HasDisplayableIssues(localPlayJson.Issues) || GetWarningCount(localPlayJson) > 0;
    }

    private static bool HasDisplayableIssues(GCDevJsonIssue[] issues)
    {
        if (issues == null)
        {
            return false;
        }

        for (var index = 0; index < issues.Length; index++)
        {
            if (issues[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetWarningCount(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        return localPlayJson != null && localPlayJson.validation != null ? localPlayJson.validation.WarningCount : 0;
    }

    private static string GetLocalPlayJsonBlockedMessage(GCStartScreenLocalPlayJsonReadiness localPlayJson)
    {
        return localPlayJson == null || string.IsNullOrEmpty(localPlayJson.message)
            ? "Local Play Mode is blocked because gc.dev.json is missing or invalid. The Unity package will not create or repair this file."
            : localPlayJson.message + " The Unity package will not create or repair this file.";
    }

    private static void DrawPendingSetupStatus()
    {
        if (!GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return;
        }

        var setupDisplayName = GamingCouchActiveSceneSetup.GetPendingSetupDisplayName();
        EditorGUILayout.HelpBox(
            setupDisplayName + " is waiting for Unity to compile generated scripts. Setup will continue automatically after compilation finishes.",
            MessageType.Warning
        );
        EditorGUILayout.Space();
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        if (GamingCouchStartScreenSetupActions.ShouldShowActiveSceneSetupAction(readiness))
        {
            using (new EditorGUI.DisabledScope(GamingCouchStartScreenSetupActions.IsActiveSceneSetupActionBlocked(readiness)))
            {
                if (GUILayout.Button("Set up missing pieces"))
                {
                    RunDeferred(RunActiveSceneSetup);
                }
            }
        }

        if (GUILayout.Button("Configure WebGL Build Settings"))
        {
            RunDeferred(RunWebGLBuildSettingsProfilePreview);
        }

        EditorGUILayout.Space();
    }

    private GCStartScreenReadinessCheck FindReadinessCheck(GCStartScreenReadinessCheckId id)
    {
        if (readiness == null)
        {
            return null;
        }

        GCStartScreenReadinessCheck check;
        return readiness.TryGetCheck(id, out check) ? check : null;
    }

    private void DrawActionResult()
    {
        if (!ShouldShowActionResult(actionMessageType))
        {
            ClearActionResult();
            return;
        }

        if (string.IsNullOrEmpty(actionMessage))
        {
            return;
        }

        EditorGUILayout.HelpBox(FormatActionMessage(actionMessage, actionDetails), actionMessageType);
        EditorGUILayout.Space();
    }

    private void RunChecklistAction(GCStartScreenReadinessCheck check)
    {
        if (check != null &&
            check.action != null &&
            check.action.id == GCStartScreenReadinessActionId.SetUpWebGLExport)
        {
            ApplySetupActionResult(
                GamingCouchStartScreenSetupActions.OpenWebGLExportSetupPreview(ApplySetupActionResult)
            );
            return;
        }

        ApplySetupActionResult(GamingCouchStartScreenSetupActions.RunChecklistAction(check));
    }

    private bool IsChecklistActionDisabled(GCStartScreenReadinessCheck check)
    {
        return GamingCouchStartScreenSetupActions.IsChecklistActionDisabled(check, readiness);
    }

    private void RunActiveSceneSetup()
    {
        ApplySetupActionResult(GamingCouchStartScreenSetupActions.RunActiveSceneSetup());
    }

    // Button handlers that create/open scenes or run setup mutate the scene and asset database.
    // Running that work straight from the click executes it mid-OnGUI, which corrupts the IMGUI
    // layout stack ("EndLayoutGroup: BeginLayoutGroup must be called first" / unbalanced GUIClips).
    // Defer it to the next editor tick so the current OnGUI pass finishes cleanly first.
    private void RunDeferred(Action action)
    {
        EditorApplication.delayCall += () =>
        {
            action();
            Repaint();
        };
    }

    private void RunCreateNewExampleScene()
    {
        var creation = GamingCouchExampleSceneCreation.CreateExampleScene();
        if (creation.IsCancelled)
        {
            return;
        }

        ApplySetupActionResult(GamingCouchStartScreenSetupActions.FromExampleSceneCreationResult(creation));
    }

    private void RunWireExampleGame()
    {
        var result = GamingCouchActiveSceneSetup.WireExampleGame();
        if (result.IsCancelled)
        {
            return;
        }

        ApplySetupActionResult(GamingCouchStartScreenSetupActions.FromWireExampleGameResult(result));
    }

    internal void ApplyExternalSetupActionResult(GCStartScreenSetupActionResult result)
    {
        ApplySetupActionResult(result);
    }

    private void RunWebGLBuildSettingsProfilePreview()
    {
        ApplySetupActionResult(
            GamingCouchStartScreenSetupActions.OpenWebGLBuildSettingsProfilePreview(ApplySetupActionResult)
        );
    }

    private void ApplySetupActionResult(GCStartScreenSetupActionResult result)
    {
        if (result == null)
        {
            return;
        }

        if (result.focusTarget != null)
        {
            Selection.activeObject = result.focusTarget;
            if (result.shouldPingFocusTarget)
            {
                EditorGUIUtility.PingObject(result.focusTarget);
            }
        }

        SetActionResult(result.message, result.messageType, result.details);
        if (result.shouldRefreshAndRepaint)
        {
            // Creating an example scene adds a new GamingCouch scene, so keep the list in sync.
            RefreshSceneCatalog();
            Refresh();
            Repaint();
        }
    }

    private void SetActionResult(string message, MessageType messageType, string[] details)
    {
        if (!ShouldShowActionResult(messageType))
        {
            ClearActionResult();
            return;
        }

        actionMessage = message;
        actionMessageType = messageType;
        actionDetails = details ?? new string[0];
    }

    private void ClearActionResult()
    {
        actionMessage = null;
        actionMessageType = MessageType.Info;
        actionDetails = new string[0];
    }

    private static bool ShouldShowActionResult(MessageType messageType)
    {
        return GamingCouchStartScreenSetupActions.ShouldDisplayActionResult(messageType);
    }

    private static string FormatActionMessage(string message, string[] details)
    {
        return GamingCouchStartScreenSetupActions.FormatActionMessage(message, details);
    }

    private static GUIContent GetChecklistHelpContent(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText))
        {
            return null;
        }

        var content = EditorGUIUtility.IconContent("_Help");
        return new GUIContent(content.image, check.helpText);
    }

    private void ToggleChecklistHelp(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText))
        {
            hasSelectedChecklistHelp = false;
            return;
        }

        var checkId = GCStartScreenReadiness.NormalizeCheckId(check.id);
        if (hasSelectedChecklistHelp && selectedChecklistHelpId == checkId)
        {
            hasSelectedChecklistHelp = false;
            return;
        }

        selectedChecklistHelpId = checkId;
        hasSelectedChecklistHelp = true;
    }

    private bool IsChecklistHelpSelected(GCStartScreenReadinessCheck check)
    {
        if (check == null || string.IsNullOrEmpty(check.helpText) || !hasSelectedChecklistHelp)
        {
            return false;
        }

        return selectedChecklistHelpId == GCStartScreenReadiness.NormalizeCheckId(check.id);
    }

    private static void DrawChecklistRowBackground(Rect rowRect, int index)
    {
        EditorGUI.DrawRect(rowRect, GetChecklistRowColor(index));

        var dividerColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.08f);
        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), dividerColor);
    }

    private static Color GetChecklistRowColor(int index)
    {
        if (EditorGUIUtility.isProSkin)
        {
            return index % 2 == 0
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(1f, 1f, 1f, 0.025f);
        }

        return index % 2 == 0
            ? new Color(0f, 0f, 0f, 0.045f)
            : new Color(0f, 0f, 0f, 0.02f);
    }

    private static void DrawChecklistStatusIndicator(Rect statusRect, GCStartScreenReadinessCheckState state)
    {
        if (!HasVisibleRect(statusRect))
        {
            return;
        }

        var indicatorSize = Mathf.Min(ChecklistStatusIndicatorSize, Mathf.Min(statusRect.width, statusRect.height));
        if (indicatorSize <= 0f)
        {
            return;
        }

        var indicatorRect = new Rect(
            statusRect.x + (statusRect.width - indicatorSize) * 0.5f,
            statusRect.y + (statusRect.height - indicatorSize) * 0.5f,
            indicatorSize,
            indicatorSize
        );
        var outlineColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.22f)
            : new Color(0f, 0f, 0f, 0.18f);

        EditorGUI.DrawRect(
            new Rect(indicatorRect.x - 1f, indicatorRect.y - 1f, indicatorRect.width + 2f, indicatorRect.height + 2f),
            outlineColor
        );
        EditorGUI.DrawRect(indicatorRect, GetChecklistStateColor(state));
        GUI.Label(statusRect, new GUIContent(string.Empty, GetStateTooltip(state)), GUIStyle.none);
    }

    private static bool HasVisibleRect(Rect rect)
    {
        return rect.width > 0f && rect.height > 0f;
    }

    private static Color GetChecklistStateColor(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return new Color(0.22f, 0.72f, 0.34f, 1f);
            case GCStartScreenReadinessCheckState.Warning:
            case GCStartScreenReadinessCheckState.Blocked:
                return new Color(0.95f, 0.62f, 0.18f, 1f);
            case GCStartScreenReadinessCheckState.Fail:
                return new Color(0.86f, 0.26f, 0.24f, 1f);
            default:
                return EditorGUIUtility.isProSkin
                    ? new Color(0.72f, 0.72f, 0.72f, 1f)
                    : new Color(0.38f, 0.38f, 0.38f, 1f);
        }
    }

    private static string GetStateTooltip(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Pass:
                return "Ready";
            case GCStartScreenReadinessCheckState.Warning:
                return "Warning";
            case GCStartScreenReadinessCheckState.Blocked:
                return "Blocked";
            case GCStartScreenReadinessCheckState.Fail:
                return "Error";
            default:
                return "Unknown";
        }
    }

    private static GUIStyle GetChecklistLabelStyle()
    {
        if (checklistLabelStyle == null)
        {
            checklistLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }

        return checklistLabelStyle;
    }

    private static MessageType GetMessageType(GCStartScreenReadinessCheckState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessCheckState.Warning:
                return MessageType.Warning;
            case GCStartScreenReadinessCheckState.Blocked:
                return MessageType.Warning;
            case GCStartScreenReadinessCheckState.Fail:
                return MessageType.Error;
            default:
                return MessageType.Info;
        }
    }
}
