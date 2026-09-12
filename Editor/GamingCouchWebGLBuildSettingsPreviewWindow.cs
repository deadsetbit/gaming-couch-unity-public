using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class GCWebGLPreviewApplyOutcome
{
    internal readonly bool changed;
    internal readonly string message;
    internal readonly MessageType messageType;
    internal readonly string[] details;

    internal GCWebGLPreviewApplyOutcome(
        bool changed,
        string message,
        MessageType messageType,
        string[] details
    )
    {
        this.changed = changed;
        this.message = message;
        this.messageType = messageType;
        this.details = details ?? new string[0];
    }
}

internal sealed class GamingCouchWebGLBuildSettingsPreviewWindow : EditorWindow
{
    private const float WindowWidth = 620f;
    private const float WindowHeight = 480f;
    private static readonly string[] ProfileSelectorLabels =
    {
        "Dev (fast build)",
        "Release (slow build)",
    };
    private static readonly GCWebGLBuildSettingsProfileId[] ProfileSelectorIds =
    {
        GCWebGLBuildSettingsProfileId.Dev,
        GCWebGLBuildSettingsProfileId.Release,
    };

    private string heading;
    private string message;
    private string applyLabel;
    private GCWebGLPreviewRow[] rows = new GCWebGLPreviewRow[0];
    private HashSet<string> selectedSkippableRowIds = new HashSet<string>(StringComparer.Ordinal);
    private Func<string[], GCWebGLPreviewApplyOutcome> applyHandler;
    private Action<GCWebGLPreviewApplyOutcome> onApplied;
    private Vector2 scrollPosition;
    private bool canApply;
    private bool showProfileSelector;
    private GCWebGLBuildSettingsProfileId selectedProfileId;
    private int selectedProfileIndex;

    internal static void OpenReleaseProfile()
    {
        OpenProfileSelector(GCWebGLBuildSettingsProfileId.Release, null);
    }

    internal static void OpenDevProfile()
    {
        OpenProfileSelector(GCWebGLBuildSettingsProfileId.Dev, null);
    }

    internal static void OpenWebGLExportSettings(Action<GCWebGLExportSetupResult> onApplied)
    {
        var plan = GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan();
        OpenWebGLExportSettings(plan, onApplied);
    }

    internal static void OpenWebGLExportSettings(
        GCWebGLExportSetupPlan plan,
        Action<GCWebGLExportSetupResult> onApplied
    )
    {
        var safePlan = plan ?? GamingCouchWebGLExportSetup.CreateWebGLExportSetupPlan();
        OpenWindow(
            "GamingCouch Web Export Settings",
            safePlan.message,
            "Apply Web Export Settings",
            safePlan.rows,
            safePlan.GetDefaultSelectedSkippableRowIds(),
            !safePlan.IsBlocked && safePlan.HasChanges,
            selectedIds =>
            {
                var result = GamingCouchWebGLExportSetup.ApplyWebGLExportSetupPlan(
                    safePlan,
                    selectedIds
                );
                if (onApplied != null)
                {
                    onApplied(result);
                }

                return FromWebGLExportResult(result);
            }
        );
    }

    internal static void OpenProfileSelector(
        GCWebGLBuildSettingsProfileId initialProfileId,
        Action<GCWebGLPreviewApplyOutcome> onApplied
    )
    {
        var window = CreateWindow("GamingCouch WebGL Build Settings");
        window.showProfileSelector = true;
        window.onApplied = onApplied;
        window.SetProfile(initialProfileId);
        window.ShowUtility();
    }

    private static void OpenWindow(
        string windowTitle,
        string message,
        string applyButtonLabel,
        GCWebGLPreviewRow[] rows,
        string[] defaultSelectedSkippableRowIds,
        bool canApply,
        Func<string[], GCWebGLPreviewApplyOutcome> apply
    )
    {
        var window = CreateWindow(windowTitle);
        window.heading = windowTitle;
        window.message = message;
        window.applyLabel = applyButtonLabel;
        window.rows = rows ?? new GCWebGLPreviewRow[0];
        window.selectedSkippableRowIds = new HashSet<string>(
            defaultSelectedSkippableRowIds ?? new string[0],
            StringComparer.Ordinal
        );
        window.canApply = canApply;
        window.applyHandler = apply;
        window.ShowUtility();
    }

    private static GamingCouchWebGLBuildSettingsPreviewWindow CreateWindow(string windowTitle)
    {
        var window = CreateInstance<GamingCouchWebGLBuildSettingsPreviewWindow>();
        window.titleContent = new GUIContent(windowTitle);
        window.minSize = new Vector2(WindowWidth, WindowHeight);
        window.maxSize = new Vector2(WindowWidth, 800f);
        return window;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        DrawProfileSelector();
        EditorGUILayout.HelpBox(message, GetHeaderMessageType());
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawRows();
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        DrawFooterButtons();
    }

    private void DrawProfileSelector()
    {
        if (!showProfileSelector)
        {
            return;
        }

        var nextProfileIndex = EditorGUILayout.Popup(
            "Profile",
            selectedProfileIndex,
            ProfileSelectorLabels
        );
        if (nextProfileIndex != selectedProfileIndex &&
            nextProfileIndex >= 0 &&
            nextProfileIndex < ProfileSelectorIds.Length)
        {
            SetProfile(ProfileSelectorIds[nextProfileIndex]);
        }
    }

    private void SetProfile(GCWebGLBuildSettingsProfileId profileId)
    {
        selectedProfileId = profileId;
        selectedProfileIndex = GetProfileSelectorIndex(profileId);

        var plan = BuildProfilePlan(profileId);
        heading = "GamingCouch WebGL Build Settings";
        message = plan.HasChanges
            ? "Review " + plan.displayName + " WebGL build setting changes before applying them."
            : plan.displayName + " WebGL build settings are already configured.";
        applyLabel = "Apply " + plan.displayName + " Settings";
        rows = plan.rows;
        selectedSkippableRowIds = new HashSet<string>(
            GCWebGLPreviewRowQueries.GetDefaultSelectedSkippableRowIds(plan.rows),
            StringComparer.Ordinal
        );
        canApply = plan.HasChanges;
        applyHandler = selectedIds => ApplyProfile(selectedProfileId, selectedIds);
        scrollPosition = Vector2.zero;
    }

    private static int GetProfileSelectorIndex(GCWebGLBuildSettingsProfileId profileId)
    {
        for (var index = 0; index < ProfileSelectorIds.Length; index++)
        {
            if (ProfileSelectorIds[index] == profileId)
            {
                return index;
            }
        }

        return 0;
    }

    private static GCWebGLBuildSettingsProfilePlan BuildProfilePlan(
        GCWebGLBuildSettingsProfileId profileId
    )
    {
        return profileId == GCWebGLBuildSettingsProfileId.Dev
            ? GamingCouchWebGLBuildSettingsProfiles.BuildDevProfilePlan()
            : GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan();
    }

    private static GCWebGLPreviewApplyOutcome ApplyProfile(
        GCWebGLBuildSettingsProfileId profileId,
        string[] selectedIds
    )
    {
        return profileId == GCWebGLBuildSettingsProfileId.Dev
            ? FromProfileResult(GamingCouchWebGLBuildSettingsProfiles.ApplyDevProfile(selectedIds))
            : FromProfileResult(GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile(selectedIds));
    }

    private void DrawRows()
    {
        var drewRow = false;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row == null || !row.isChanged)
            {
                continue;
            }

            drewRow = true;
            DrawRow(row);
        }

        if (!drewRow)
        {
            EditorGUILayout.HelpBox("No WebGL build setting changes are needed.", MessageType.Info);
        }
    }

    private void DrawRow(GCWebGLPreviewRow row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (row.isSkippable)
            {
                var selected = selectedSkippableRowIds.Contains(row.id);
                var nextSelected = EditorGUILayout.ToggleLeft(row.DiffText, selected);
                if (nextSelected)
                {
                    selectedSkippableRowIds.Add(row.id);
                }
                else
                {
                    selectedSkippableRowIds.Remove(row.id);
                }

                return;
            }

            EditorGUILayout.LabelField(row.DiffText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                row.isBlocked ? "Blocked" : "Required",
                row.isBlocked ? EditorStyles.boldLabel : EditorStyles.miniLabel
            );
        }
    }

    private void DrawFooterButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(!canApply || applyHandler == null))
            {
                if (GUILayout.Button(applyLabel, GUILayout.Width(180f)))
                {
                    Apply();
                }
            }
        }
    }

    private void Apply()
    {
        var outcome = applyHandler(GetSelectedSkippableRowIds());
        LogOutcome(outcome);
        if (onApplied != null)
        {
            onApplied(outcome);
        }

        Close();
    }

    private string[] GetSelectedSkippableRowIds()
    {
        var selectedIds = new List<string>();
        foreach (var id in selectedSkippableRowIds)
        {
            selectedIds.Add(id);
        }

        return selectedIds.ToArray();
    }

    private MessageType GetHeaderMessageType()
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index] != null && rows[index].isBlocked)
            {
                return MessageType.Error;
            }
        }

        return canApply ? MessageType.Info : MessageType.None;
    }

    private static GCWebGLPreviewApplyOutcome FromProfileResult(GCWebGLBuildSettingsProfileResult result)
    {
        if (result == null)
        {
            return new GCWebGLPreviewApplyOutcome(
                false,
                "WebGL build settings did not return a result.",
                MessageType.Error,
                null
            );
        }

        return new GCWebGLPreviewApplyOutcome(
            result.changed,
            result.message,
            MessageType.Info,
            result.details
        );
    }

    private static GCWebGLPreviewApplyOutcome FromWebGLExportResult(GCWebGLExportSetupResult result)
    {
        if (result == null)
        {
            return new GCWebGLPreviewApplyOutcome(
                false,
                "Web export settings did not return a result.",
                MessageType.Error,
                null
            );
        }

        return new GCWebGLPreviewApplyOutcome(
            result.changed,
            result.message,
            GetWebGLExportMessageType(result),
            result.details
        );
    }

    private static MessageType GetWebGLExportMessageType(GCWebGLExportSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.HasWarning ? MessageType.Warning : MessageType.Info;
    }

    private static void LogOutcome(GCWebGLPreviewApplyOutcome outcome)
    {
        if (outcome == null)
        {
            return;
        }

        var feedback = FormatFeedback(outcome.message, outcome.details);
        if (outcome.messageType == MessageType.Error)
        {
            Debug.LogError(feedback);
            EditorUtility.DisplayDialog("GamingCouch WebGL Build Settings", feedback, "OK");
            return;
        }

        if (outcome.messageType == MessageType.Warning)
        {
            Debug.LogWarning(feedback);
            EditorUtility.DisplayDialog("GamingCouch WebGL Build Settings", feedback, "OK");
            return;
        }

        if (outcome.changed || outcome.details.Length > 0)
        {
            Debug.Log(feedback);
        }
    }

    private static string FormatFeedback(string message, string[] details)
    {
        if (details == null || details.Length == 0)
        {
            return message;
        }

        return message + "\n\n" + string.Join("\n", details);
    }
}
