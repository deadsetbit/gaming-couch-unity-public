using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

internal enum GCGameViewAspectStatus
{
    Ready,
    Mismatch,
    Unknown,
}

internal enum GCGameViewAspectSetupStatus
{
    Ready,
    Blocked,
}

internal sealed class GCGameViewSizeEntry
{
    internal readonly int index;
    internal readonly string displayText;
    internal readonly int width;
    internal readonly int height;

    internal GCGameViewSizeEntry(
        int index,
        string displayText,
        int width,
        int height
    )
    {
        this.index = index;
        this.displayText = displayText;
        this.width = width;
        this.height = height;
    }

    internal string DisplayName
    {
        get { return string.IsNullOrEmpty(displayText) ? "Game View size " + index : displayText; }
    }
}

internal sealed class GCGameViewAspectReadiness
{
    internal readonly GCGameViewAspectStatus status;
    internal readonly GCGameViewSizeEntry selectedEntry;
    internal readonly GCGameViewSizeEntry existing16By9Entry;
    internal readonly bool canSelectExisting16By9;
    internal readonly string message;

    internal GCGameViewAspectReadiness(
        GCGameViewAspectStatus status,
        GCGameViewSizeEntry selectedEntry,
        GCGameViewSizeEntry existing16By9Entry,
        bool canSelectExisting16By9,
        string message
    )
    {
        this.status = status;
        this.selectedEntry = selectedEntry;
        this.existing16By9Entry = existing16By9Entry;
        this.canSelectExisting16By9 = canSelectExisting16By9;
        this.message = message;
    }

    internal bool IsReady
    {
        get { return status == GCGameViewAspectStatus.Ready; }
    }

    internal bool HasSafeSelectionAction
    {
        get { return !IsReady && canSelectExisting16By9 && existing16By9Entry != null; }
    }
}

internal sealed class GCGameViewAspectSetupResult
{
    internal readonly GCGameViewAspectSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;

    internal GCGameViewAspectSetupResult(
        GCGameViewAspectSetupStatus status,
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
        get { return status == GCGameViewAspectSetupStatus.Blocked; }
    }
}

internal static class GamingCouchGameViewAspect
{
    private const int TargetAspectWidth = 16;
    private const int TargetAspectHeight = 9;
    private static readonly Regex RatioTextRegex = new Regex(
        @"(?<!\d)(\d+)\s*[:xX]\s*(\d+)(?!\d)",
        RegexOptions.Compiled
    );

    internal static GCGameViewAspectReadiness Inspect()
    {
        ReflectedGameViewContext context;
        string error;
        if (!TryReadGameViewContext(false, out context, out error))
        {
            return CreateUnknownReadiness(null, false, error);
        }

        return InspectSizeEntries(
            context.entries,
            context.selectedSizeIndex,
            context.canSetSelectedSize,
            context.selectedSizeIndex >= 0
                ? null
                : "Game View is not open, so the selected preview aspect could not be inspected."
        );
    }

    internal static GCGameViewAspectReadiness InspectSizeEntries(
        GCGameViewSizeEntry[] entries,
        int selectedIndex,
        bool canSelectExisting16By9,
        string unknownMessage
    )
    {
        var existing16By9 = FindFirst16By9Entry(entries);
        var selectedEntry = FindEntryByIndex(entries, selectedIndex);
        if (selectedEntry == null)
        {
            return CreateUnknownReadiness(existing16By9, canSelectExisting16By9, unknownMessage);
        }

        if (Is16By9(selectedEntry))
        {
            return new GCGameViewAspectReadiness(
                GCGameViewAspectStatus.Ready,
                selectedEntry,
                existing16By9,
                false,
                "Game View is using a 16:9 preview aspect: " + selectedEntry.DisplayName + "."
            );
        }

        return new GCGameViewAspectReadiness(
            GCGameViewAspectStatus.Mismatch,
            selectedEntry,
            existing16By9,
            canSelectExisting16By9 && existing16By9 != null,
            "Game View is using " + selectedEntry.DisplayName + ". Select an existing 16:9 Game View entry; this screen will not create custom Game View sizes."
        );
    }

    internal static GCGameViewAspectSetupResult SelectExisting16By9Size()
    {
        ReflectedGameViewContext context;
        string error;
        if (!TryReadGameViewContext(false, out context, out error))
        {
            return Blocked("Game View 16:9 setup is blocked.", error);
        }

        var targetEntry = FindFirst16By9Entry(context.entries);
        if (targetEntry == null)
        {
            return Blocked(
                "Game View 16:9 setup is blocked.",
                "No existing 16:9 Game View entry was found. Choose or create one manually in Unity; this screen will not create custom Game View sizes."
            );
        }

        if (!context.canSetSelectedSize)
        {
            return Blocked(
                "Game View 16:9 setup is blocked.",
                "Unity's internal Game View selected-size API is unavailable. Choose a 16:9 Game View entry manually."
            );
        }

        var gameViewWindow = context.gameViewWindow;
        if (gameViewWindow == null)
        {
            gameViewWindow = EditorWindow.GetWindow(context.gameViewType);
        }

        if (gameViewWindow == null)
        {
            return Blocked(
                "Game View 16:9 setup is blocked.",
                "Unity did not provide a Game View window for selecting the existing 16:9 entry."
            );
        }

        // Re-read the selected index from the window we are about to mutate (which may be a
        // freshly opened window that already defaults to 16:9) so `changed` reflects that
        // window's real pre-set state rather than the stale context value read earlier.
        var currentSelectedIndex = ReadSelectedSizeIndex(gameViewWindow, context.selectedSizeIndexProperty);

        try
        {
            context.selectedSizeIndexProperty.SetValue(gameViewWindow, targetEntry.index, null);
            gameViewWindow.Repaint();
        }
        catch (Exception exception)
        {
            return Blocked(
                "Game View 16:9 setup is blocked.",
                "Unity rejected the Game View size selection: " + exception.Message
            );
        }

        return new GCGameViewAspectSetupResult(
            GCGameViewAspectSetupStatus.Ready,
            ComputeChanged(currentSelectedIndex, targetEntry.index),
            "Selected an existing 16:9 Game View entry.",
            new[] { "Selected: " + targetEntry.DisplayName }
        );
    }

    internal static bool ComputeChanged(int currentSelectedIndex, int targetIndex)
    {
        return currentSelectedIndex != targetIndex;
    }

    private static int ReadSelectedSizeIndex(
        EditorWindow gameViewWindow,
        PropertyInfo selectedSizeIndexProperty
    )
    {
        if (gameViewWindow == null || selectedSizeIndexProperty == null)
        {
            return -1;
        }

        try
        {
            return Convert.ToInt32(selectedSizeIndexProperty.GetValue(gameViewWindow, null));
        }
        catch (Exception)
        {
            // Conservative fallback: if the pre-set index cannot be read, treat the selection
            // as a change (target entry indices are always >= 0, so -1 never matches).
            return -1;
        }
    }

    internal static bool Is16By9(GCGameViewSizeEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        var width = entry.width;
        var height = entry.height;
        if ((width <= 0 || height <= 0) &&
            TryParseRatio(entry.displayText, out var parsedWidth, out var parsedHeight))
        {
            width = parsedWidth;
            height = parsedHeight;
        }

        return width > 0 &&
               height > 0 &&
               width * TargetAspectHeight == height * TargetAspectWidth;
    }

    internal static GCGameViewSizeEntry FindFirst16By9Entry(GCGameViewSizeEntry[] entries)
    {
        if (entries == null)
        {
            return null;
        }

        for (var index = 0; index < entries.Length; index++)
        {
            if (Is16By9(entries[index]))
            {
                return entries[index];
            }
        }

        return null;
    }

    private static GCGameViewAspectReadiness CreateUnknownReadiness(
        GCGameViewSizeEntry existing16By9,
        bool canSelectExisting16By9,
        string message
    )
    {
        if (string.IsNullOrEmpty(message))
        {
            message = "Game View aspect could not be inspected. Choose a 16:9 Game View entry manually if the preview does not match the web embed shape.";
        }

        return new GCGameViewAspectReadiness(
            GCGameViewAspectStatus.Unknown,
            null,
            existing16By9,
            canSelectExisting16By9 && existing16By9 != null,
            message + " This screen will not create custom Game View sizes."
        );
    }

    private static GCGameViewSizeEntry FindEntryByIndex(GCGameViewSizeEntry[] entries, int selectedIndex)
    {
        if (entries == null || selectedIndex < 0)
        {
            return null;
        }

        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index] != null && entries[index].index == selectedIndex)
            {
                return entries[index];
            }
        }

        return null;
    }

    private static bool TryReadGameViewContext(
        bool createGameViewWindow,
        out ReflectedGameViewContext context,
        out string error
    )
    {
        context = null;
        error = null;

        var editorAssembly = typeof(EditorWindow).Assembly;
        var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
        if (gameViewType == null)
        {
            error = "Unity's internal Game View type is unavailable.";
            return false;
        }

        var selectedSizeIndexProperty = gameViewType.GetProperty(
            "selectedSizeIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        var canSetSelectedSize = selectedSizeIndexProperty != null && selectedSizeIndexProperty.GetSetMethod(true) != null;

        var gameViewWindow = FindGameViewWindow(gameViewType);
        if (gameViewWindow == null && createGameViewWindow)
        {
            gameViewWindow = EditorWindow.GetWindow(gameViewType);
        }

        var selectedSizeIndex = -1;
        if (gameViewWindow != null && selectedSizeIndexProperty != null)
        {
            try
            {
                selectedSizeIndex = Convert.ToInt32(selectedSizeIndexProperty.GetValue(gameViewWindow, null));
            }
            catch (Exception exception)
            {
                error = "Unity's internal Game View selected-size value could not be read: " + exception.Message;
                return false;
            }
        }

        GCGameViewSizeEntry[] entries;
        if (!TryReadCurrentGroupEntries(editorAssembly, out entries, out error))
        {
            return false;
        }

        context = new ReflectedGameViewContext(
            gameViewType,
            gameViewWindow,
            selectedSizeIndexProperty,
            selectedSizeIndex,
            canSetSelectedSize,
            entries
        );
        return true;
    }

    private static EditorWindow FindGameViewWindow(Type gameViewType)
    {
        var objects = Resources.FindObjectsOfTypeAll(gameViewType);
        for (var index = 0; index < objects.Length; index++)
        {
            var window = objects[index] as EditorWindow;
            if (window != null)
            {
                return window;
            }
        }

        return null;
    }

    private static bool TryReadCurrentGroupEntries(
        Assembly editorAssembly,
        out GCGameViewSizeEntry[] entries,
        out string error
    )
    {
        entries = null;
        error = null;

        var gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
        if (gameViewSizesType == null)
        {
            error = "Unity's internal Game View sizes type is unavailable.";
            return false;
        }

        object gameViewSizes;
        if (!TryGetScriptableSingletonInstance(editorAssembly, gameViewSizesType, out gameViewSizes, out error))
        {
            return false;
        }

        object groupType;
        if (!TryGetCurrentGroupType(editorAssembly, gameViewSizesType, gameViewSizes, out groupType, out error))
        {
            return false;
        }

        var getGroupMethod = gameViewSizesType.GetMethod(
            "GetGroup",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (getGroupMethod == null)
        {
            error = "Unity's internal Game View size group API is unavailable.";
            return false;
        }

        object group;
        try
        {
            group = getGroupMethod.Invoke(gameViewSizes, new[] { groupType });
        }
        catch (Exception exception)
        {
            error = "Unity's internal Game View size group could not be read: " + exception.Message;
            return false;
        }

        if (group == null)
        {
            error = "Unity did not provide a current Game View size group.";
            return false;
        }

        return TryReadEntriesFromGroup(group, out entries, out error);
    }

    private static bool TryGetScriptableSingletonInstance(
        Assembly editorAssembly,
        Type singletonTargetType,
        out object instance,
        out string error
    )
    {
        instance = null;
        error = null;

        var scriptableSingletonTypeDefinition = editorAssembly.GetType("UnityEditor.ScriptableSingleton`1");
        if (scriptableSingletonTypeDefinition == null)
        {
            error = "Unity's internal ScriptableSingleton API is unavailable.";
            return false;
        }

        var singletonType = scriptableSingletonTypeDefinition.MakeGenericType(singletonTargetType);
        var instanceProperty = singletonType.GetProperty(
            "instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (instanceProperty == null)
        {
            error = "Unity's internal Game View sizes singleton is unavailable.";
            return false;
        }

        try
        {
            instance = instanceProperty.GetValue(null, null);
        }
        catch (Exception exception)
        {
            error = "Unity's internal Game View sizes singleton could not be read: " + exception.Message;
            return false;
        }

        if (instance == null)
        {
            error = "Unity did not provide Game View size settings.";
            return false;
        }

        return true;
    }

    private static bool TryGetCurrentGroupType(
        Assembly editorAssembly,
        Type gameViewSizesType,
        object gameViewSizes,
        out object groupType,
        out string error
    )
    {
        groupType = null;
        error = null;

        var currentGroupTypeProperty = gameViewSizesType.GetProperty(
            "currentGroupType",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (currentGroupTypeProperty != null)
        {
            try
            {
                groupType = currentGroupTypeProperty.GetValue(gameViewSizes, null);
            }
            catch (Exception exception)
            {
                error = "Unity's internal Game View size group could not be read: " + exception.Message;
                return false;
            }

            if (groupType != null)
            {
                return true;
            }
        }

        var groupTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
        if (groupTypeEnum != null)
        {
            try
            {
                groupType = Enum.Parse(groupTypeEnum, "Standalone");
                return true;
            }
            catch (Exception)
            {
                error = "Unity's internal Game View size group could not be resolved.";
                return false;
            }
        }

        error = "Unity's internal Game View size group type is unavailable.";
        return false;
    }

    private static bool TryReadEntriesFromGroup(
        object group,
        out GCGameViewSizeEntry[] entries,
        out string error
    )
    {
        entries = null;
        error = null;

        var groupType = group.GetType();
        var getTotalCountMethod = groupType.GetMethod(
            "GetTotalCount",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        var getGameViewSizeMethod = groupType.GetMethod(
            "GetGameViewSize",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (getTotalCountMethod == null || getGameViewSizeMethod == null)
        {
            error = "Unity's internal Game View size entries API is unavailable.";
            return false;
        }

        int totalCount;
        try
        {
            totalCount = Convert.ToInt32(getTotalCountMethod.Invoke(group, null));
        }
        catch (Exception exception)
        {
            error = "Unity's internal Game View size count could not be read: " + exception.Message;
            return false;
        }

        var result = new List<GCGameViewSizeEntry>();
        for (var index = 0; index < totalCount; index++)
        {
            object size;
            try
            {
                size = getGameViewSizeMethod.Invoke(group, new object[] { index });
            }
            catch (Exception exception)
            {
                error = "Unity's internal Game View size entry could not be read: " + exception.Message;
                return false;
            }

            if (size != null)
            {
                try
                {
                    result.Add(CreateEntry(index, size));
                }
                catch (Exception exception)
                {
                    error = "Unity's internal Game View size entry metadata could not be read: " + exception.Message;
                    return false;
                }
            }
        }

        entries = result.ToArray();
        return true;
    }

    private static GCGameViewSizeEntry CreateEntry(int index, object size)
    {
        var displayText = GetStringMember(size, "displayText");
        if (string.IsNullOrEmpty(displayText))
        {
            displayText = GetStringMember(size, "baseText");
        }

        var width = GetIntMember(size, "width");
        var height = GetIntMember(size, "height");
        if ((width <= 0 || height <= 0) && TryParseRatio(displayText, out var parsedWidth, out var parsedHeight))
        {
            width = parsedWidth;
            height = parsedHeight;
        }

        return new GCGameViewSizeEntry(index, displayText, width, height);
    }

    private static string GetStringMember(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        return value != null ? value.ToString() : null;
    }

    private static int GetIntMember(object instance, string memberName)
    {
        var value = GetMemberValue(instance, memberName);
        if (value == null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static object GetMemberValue(object instance, string memberName)
    {
        if (instance == null)
        {
            return null;
        }

        var type = instance.GetType();
        var property = type.GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (property != null)
        {
            return property.GetValue(instance, null);
        }

        var field = type.GetField(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        return field != null ? field.GetValue(instance) : null;
    }

    private static bool TryParseRatio(string text, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var match = RatioTextRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups[1].Value, out width) &&
               int.TryParse(match.Groups[2].Value, out height);
    }

    private static GCGameViewAspectSetupResult Blocked(string message, string detail)
    {
        return new GCGameViewAspectSetupResult(
            GCGameViewAspectSetupStatus.Blocked,
            false,
            message,
            new[] { detail }
        );
    }

    private sealed class ReflectedGameViewContext
    {
        internal readonly Type gameViewType;
        internal readonly EditorWindow gameViewWindow;
        internal readonly PropertyInfo selectedSizeIndexProperty;
        internal readonly int selectedSizeIndex;
        internal readonly bool canSetSelectedSize;
        internal readonly GCGameViewSizeEntry[] entries;

        internal ReflectedGameViewContext(
            Type gameViewType,
            EditorWindow gameViewWindow,
            PropertyInfo selectedSizeIndexProperty,
            int selectedSizeIndex,
            bool canSetSelectedSize,
            GCGameViewSizeEntry[] entries
        )
        {
            this.gameViewType = gameViewType;
            this.gameViewWindow = gameViewWindow;
            this.selectedSizeIndexProperty = selectedSizeIndexProperty;
            this.selectedSizeIndex = selectedSizeIndex;
            this.canSetSelectedSize = canSetSelectedSize;
            this.entries = entries ?? new GCGameViewSizeEntry[0];
        }
    }
}
