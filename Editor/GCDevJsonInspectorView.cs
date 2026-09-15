using System;
using System.Collections.Generic;
using DSB.GC;
using DSB.GC.Dev;
using UnityEditor;
using UnityEngine;

internal sealed class GCDevJsonInspectorView
{
    private static readonly string[] SeedModeLabels = { "Random", "Fixed" };
    private static readonly string[] SeatColorKeys =
    {
        GCPlayerColor.blue.ToString(),
        GCPlayerColor.red.ToString(),
        GCPlayerColor.green.ToString(),
        GCPlayerColor.yellow.ToString(),
        GCPlayerColor.purple.ToString(),
        GCPlayerColor.pink.ToString(),
        GCPlayerColor.cyan.ToString(),
        GCPlayerColor.brown.ToString(),
    };

    internal void Draw(GCDevJsonInspectorState state)
    {
        if (state == null)
        {
            EditorGUILayout.HelpBox("Local play settings state is unavailable.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Local Play Settings", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("File", state.DevJsonPath);
        DrawPlatformDataSummary(state);
        DrawStateMessages(state);
        DrawIssues(state.GetDisplayIssues());

        if (!state.HasDraft)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Entry Key", string.Empty);
            }

            DrawActions(state);
            return;
        }

        EditorGUI.BeginChangeCheck();
        DrawEntry(state);
        DrawSeed(state.Draft);
        DrawSeats(state);
        if (EditorGUI.EndChangeCheck())
        {
            state.NotifyDraftChanged();
        }

        DrawActions(state);
    }

    private static void DrawPlatformDataSummary(GCDevJsonInspectorState state)
    {
        var platformData = state.PlatformDataReadResult != null ? state.PlatformDataReadResult.data : null;
        if (!state.HasValidPlatformData || platformData == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Game", platformData.gameName + " (" + platformData.gameKey + ")");
        EditorGUILayout.LabelField("Platform", platformData.platformId);
    }

    private static void DrawStateMessages(GCDevJsonInspectorState state)
    {
        if (state.HasConflict)
        {
            EditorGUILayout.HelpBox(
                "gc.dev.json changed on disk while this draft has unsaved edits. Reload from disk or write the draft to resolve the conflict.",
                MessageType.Warning
            );
        }

        if (state.HasPendingPlayChange)
        {
            EditorGUILayout.HelpBox(
                "JSON changed during active Play Mode. These changes apply after a Gaming Couch restart or the next Play Mode entry.",
                MessageType.Info
            );
        }
    }

    private static void DrawEntry(GCDevJsonInspectorState state)
    {
        if (!state.HasValidPlatformData)
        {
            state.Draft.entryKey = EditorGUILayout.TextField("Entry Key", state.Draft.entryKey ?? string.Empty);
            return;
        }

        var options = BuildEntryOptions(state.PlatformDataReadResult.data, state.Draft.entryKey);
        var labels = new string[options.Count];
        var selectedIndex = 0;
        for (var index = 0; index < options.Count; index++)
        {
            labels[index] = options[index].label;
            if (options[index].isSelected)
            {
                selectedIndex = index;
            }
        }

        var nextIndex = EditorGUILayout.Popup("Entry", selectedIndex, labels);
        if (nextIndex >= 0 && nextIndex < options.Count)
        {
            state.Draft.entryKey = options[nextIndex].entryKey;
        }
    }

    private static List<EntryOption> BuildEntryOptions(GCPlatformDataFile platformData, string currentEntryKey)
    {
        var options = new List<EntryOption>();
        var keys = new List<string>();
        foreach (var pair in platformData.entries)
        {
            keys.Add(pair.Key);
        }

        keys.Sort(StringComparer.Ordinal);

        var currentFound = false;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            var entry = platformData.entries[key];
            var isSelected = string.Equals(key, currentEntryKey, StringComparison.Ordinal);
            currentFound = currentFound || isSelected;
            options.Add(new EntryOption(
                key,
                entry.name + " (" + key + ", " + entry.minPlayers + "-" + entry.maxPlayers + ")",
                isSelected
            ));
        }

        if (!currentFound)
        {
            var label = string.IsNullOrEmpty(currentEntryKey) ? "Missing entry: <empty>" : "Missing entry: " + currentEntryKey;
            options.Insert(0, new EntryOption(currentEntryKey ?? string.Empty, label, true));
        }

        return options;
    }

    private static void DrawSeed(GCDevJsonDraft draft)
    {
        var selectedSeedMode = draft.usesRandomSeed ? 0 : 1;
        var nextSeedMode = EditorGUILayout.Popup("Seed", selectedSeedMode, SeedModeLabels);
        draft.usesRandomSeed = nextSeedMode == 0;
        if (!draft.usesRandomSeed)
        {
            draft.fixedSeed = EditorGUILayout.IntField("Fixed Seed", draft.fixedSeed);
        }
    }

    private static void DrawSeats(GCDevJsonInspectorState state)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Seats", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("#", EditorStyles.miniLabel, GUILayout.Width(24));
            GUILayout.Label(string.Empty, GUILayout.Width(18));
            GUILayout.Label("Name", EditorStyles.miniLabel);
            GUILayout.Label("Enabled", EditorStyles.miniLabel, GUILayout.Width(56));
            GUILayout.Label("Bot", EditorStyles.miniLabel, GUILayout.Width(32));
        }

        for (var index = 0; index < state.Draft.seats.Length; index++)
        {
            DrawSeatRow(state, index);
        }
    }

    private static void DrawSeatRow(GCDevJsonInspectorState state, int seatIndex)
    {
        var seat = state.Draft.seats[seatIndex];
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label((seatIndex + 1).ToString(), GUILayout.Width(24));
            DrawSeatColor(state, seatIndex);
            seat.name = EditorGUILayout.TextField(seat.name ?? string.Empty);
            seat.enabled = EditorGUILayout.Toggle(seat.enabled, GUILayout.Width(56));
            seat.isBot = EditorGUILayout.Toggle(seat.isBot, GUILayout.Width(32));
        }
    }

    private static void DrawSeatColor(GCDevJsonInspectorState state, int seatIndex)
    {
        var rect = EditorGUILayout.GetControlRect(false, 14, GUILayout.Width(18));
        Color color;
        if (!TryGetSeatColor(state, seatIndex, out color))
        {
            return;
        }

        var border = new Rect(rect.x, rect.y + 1, 14, 12);
        var inner = new Rect(border.x + 1, border.y + 1, border.width - 2, border.height - 2);
        EditorGUI.DrawRect(border, new Color(0.16f, 0.16f, 0.16f));
        EditorGUI.DrawRect(inner, color);
    }

    private static bool TryGetSeatColor(GCDevJsonInspectorState state, int seatIndex, out Color color)
    {
        color = default(Color);
        var platformData = state.PlatformDataReadResult != null ? state.PlatformDataReadResult.data : null;
        if (!state.HasValidPlatformData || platformData == null || seatIndex < 0 || seatIndex >= SeatColorKeys.Length)
        {
            return false;
        }

        GCPlatformDataColorVariants variants;
        if (!platformData.playerColors.TryGetValue(SeatColorKeys[seatIndex], out variants) || variants == null)
        {
            return false;
        }

        var baseColor = variants.baseColor;
        color = new Color(baseColor.r / 255f, baseColor.g / 255f, baseColor.b / 255f, 1f);
        return true;
    }

    private static void DrawActions(GCDevJsonInspectorState state)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(GetStateLabel(state), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (state.HasConflict)
            {
                if (GUILayout.Button("Reload from disk", GUILayout.Width(116)))
                {
                    state.Reload();
                }

                using (new EditorGUI.DisabledScope(!state.CanWriteDraft))
                {
                    if (GUILayout.Button("Write draft", GUILayout.Width(88)))
                    {
                        state.WriteDraft();
                    }
                }

                return;
            }

            using (new EditorGUI.DisabledScope(!state.CanApply))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                {
                    state.Apply();
                }
            }

            if (GUILayout.Button("Revert", GUILayout.Width(80)))
            {
                state.Reload();
            }
        }
    }

    private static string GetStateLabel(GCDevJsonInspectorState state)
    {
        if (state.HasConflict)
        {
            return "Conflict";
        }

        if (state.IsDirty)
        {
            return state.HasPendingPlayChange ? "Dirty, pending play" : "Dirty";
        }

        return state.HasPendingPlayChange ? "Pending play" : "Clean";
    }

    private static void DrawIssues(GCDevJsonIssue[] issues)
    {
        if (issues == null)
        {
            return;
        }

        for (var index = 0; index < issues.Length; index++)
        {
            var issue = issues[index];
            if (issue == null)
            {
                continue;
            }

            var messageType = issue.severity == GCDevJsonIssueSeverity.Error
                ? MessageType.Error
                : MessageType.Warning;
            EditorGUILayout.HelpBox(GCDevJsonIssueFormatter.Format(issue), messageType);
        }
    }

    private struct EntryOption
    {
        internal readonly string entryKey;
        internal readonly string label;
        internal readonly bool isSelected;

        internal EntryOption(string entryKey, string label, bool isSelected)
        {
            this.entryKey = entryKey;
            this.label = label;
            this.isSelected = isSelected;
        }
    }
}
