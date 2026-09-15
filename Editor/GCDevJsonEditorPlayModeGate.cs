using DSB.GC.Dev;
using System;
using System.Collections.Generic;
using UnityEditor;

[InitializeOnLoad]
internal static class GCDevJsonEditorPlayModeGate
{
    private static readonly List<GCDevJsonInspectorState> states = new List<GCDevJsonInspectorState>();

    static GCDevJsonEditorPlayModeGate()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        GCLocalPlaySession.RegisterProvider(new GCDevJsonLocalPlaySessionProvider());
        GCLocalPlaySession.RegisterPreflightHandler(RunPreflight);
        GCLocalPlaySession.RegisterCaptureSucceededHandler(MarkPlayChangesCaptured);
    }

    internal static void Register(GCDevJsonInspectorState state)
    {
        if (state == null || states.Contains(state))
        {
            return;
        }

        states.Add(state);
    }

    internal static void Unregister(GCDevJsonInspectorState state)
    {
        if (state == null)
        {
            return;
        }

        states.Remove(state);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        var result = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.UnityPlayModeEntry);
        if (result.success)
        {
            return;
        }

        GCLocalPlaySession.LogBlockedBoundary(result);
        EditorApplication.isPlaying = false;
    }

    private static GCLocalPlaySessionPreflightResult RunPreflight(GCLocalPlaySessionBoundary context)
    {
        try
        {
            var snapshot = GetStateSnapshot();
            GCDevJsonInspectorState dirtyState = null;
            for (var index = 0; index < snapshot.Length; index++)
            {
                var state = snapshot[index];
                var stateResult = state.ValidateForPlayBoundary(context);
                if (!stateResult.success)
                {
                    return stateResult;
                }

                if (!state.IsDirty)
                {
                    continue;
                }

                if (dirtyState != null)
                {
                    return FailForMultipleDirtyDrafts(context, dirtyState.DevJsonPath);
                }

                dirtyState = state;
            }

            if (dirtyState != null)
            {
                var applyResult = dirtyState.PrepareForPlayBoundary(context);
                if (!applyResult.success)
                {
                    return applyResult;
                }
            }

            return GCLocalPlaySessionPreflightResult.Succeeded();
        }
        catch (Exception exception)
        {
            var message = GCLocalPlaySession.GetBoundaryDisplayName(context) + " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: GCDevJsonIssueCode.ReadError.ToString()
                ))
            );
        }
    }

    private static void MarkPlayChangesCaptured()
    {
        var snapshot = GetStateSnapshot();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var state = snapshot[index];
            state.MarkPlayChangesCaptured();
        }
    }

    private static GCDevJsonInspectorState[] GetStateSnapshot()
    {
        for (var index = states.Count - 1; index >= 0; index--)
        {
            if (states[index] == null)
            {
                states.RemoveAt(index);
            }
        }

        return states.ToArray();
    }

    private static GCLocalPlaySessionPreflightResult FailForMultipleDirtyDrafts(GCLocalPlaySessionBoundary context, string path)
    {
        var message = GCLocalPlaySession.GetBoundaryDisplayName(context) +
                      " blocked because multiple GamingCouch inspectors have unsaved gc.dev.json drafts. Apply, revert, or close duplicate inspectors before continuing.";
        return GCLocalPlaySessionPreflightResult.Failed(
            message,
            path,
            GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                message,
                path,
                code: GCDevJsonIssueCode.WriteError.ToString()
            ))
        );
    }
}
