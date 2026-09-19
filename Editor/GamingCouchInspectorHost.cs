using System;
using System.Collections.Generic;
using DSB.GC;
using UnityEditor;

internal static class GamingCouchInspectorHost
{
    private static readonly HashSet<string> ExcludedPropertyPaths = new HashSet<string>
    {
        "gameModeId",
        "playerData",
        "numberOfPlayers",
        "randomizePlayerIds",
        "onlineMultiplayerSupport",
    };

    internal static void DrawSerializedFields(SerializedObject serializedObject)
    {
        if (serializedObject == null)
        {
            throw new ArgumentNullException(nameof(serializedObject));
        }

        if (serializedObject.targetObject != null && !(serializedObject.targetObject is GamingCouch))
        {
            throw new ArgumentException("SerializedObject must target a GamingCouch component.", nameof(serializedObject));
        }

        var property = serializedObject.GetIterator();
        var enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!ShouldDrawProperty(property))
            {
                continue;
            }

            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    private static bool ShouldDrawProperty(SerializedProperty property)
    {
        return property != null && ShouldDrawPropertyPath(property.propertyPath);
    }

    internal static bool ShouldDrawPropertyPath(string propertyPath)
    {
        return !string.IsNullOrEmpty(propertyPath) && !ExcludedPropertyPaths.Contains(propertyPath);
    }
}
