using DSB.GC;
using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GamingCouch))]
internal sealed class GamingCouchEditor : Editor
{
    // The active-scene readiness scan is expensive (disk reads/parses, a full scene
    // GetComponentsInChildren walk, GameView reflection, WebGL template stats). Cache
    // it on the instance and refresh at most this often instead of on every repaint.
    private const double StartScreenSummaryRefreshIntervalSeconds = 0.5;

    private GCDevJsonInspectorState devJsonState;
    private GCDevJsonInspectorView devJsonView;
    private GCStartScreenReadinessSummary startScreenSummary;
    private bool hasStartScreenSummary;
    private bool startScreenSummaryScanErrorLogged;
    private double nextStartScreenSummaryRefreshTime;

    private void OnEnable()
    {
        devJsonState = new GCDevJsonInspectorState();
        devJsonView = new GCDevJsonInspectorView();
        GCDevJsonEditorPlayModeGate.Register(devJsonState);
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.update += OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        // Populate the cache eagerly so the first paint after selection is not blank.
        RefreshStartScreenSummary();
    }

    private void OnDisable()
    {
        DisposeDevJsonState();
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        DrawStartScreenControls();

        EditorGUILayout.Space();
        serializedObject.Update();
        GamingCouchInspectorHost.DrawSerializedFields(serializedObject);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (devJsonView != null)
        {
            devJsonView.Draw(devJsonState);
        }
    }

    private void DrawStartScreenControls()
    {
        if (GUILayout.Button("Open Start Screen"))
        {
            GamingCouchStartScreenWindow.Open();
        }

        // Draw the cached summary only -- the scan itself is throttled in
        // OnEditorApplicationUpdate (and refreshed on external gc.dev/platform json
        // changes). Compute lazily but STILL throttled on the off chance the cache was never
        // populated (e.g. the OnEnable scan threw): a persistently failing scan must not re-run
        // the expensive inspection on every repaint.
        if (!hasStartScreenSummary)
        {
            MaybeRefreshStartScreenSummary();
        }

        var summary = startScreenSummary;
        if (summary == null || string.IsNullOrEmpty(summary.message))
        {
            return;
        }

        EditorGUILayout.HelpBox(summary.message, GetStartScreenSummaryMessageType(summary.state));
    }

    // Recompute the readiness summary and repaint if the visible output changed. Also
    // resets the throttle gate, so callers may invoke this to force a refresh.
    private void RefreshStartScreenSummary()
    {
        nextStartScreenSummaryRefreshTime =
            EditorApplication.timeSinceStartup + StartScreenSummaryRefreshIntervalSeconds;

        GCStartScreenReadinessSummary next;
        try
        {
            next = GCStartScreenReadinessService.InspectActiveSceneSummary();
            startScreenSummaryScanErrorLogged = false;
        }
        catch (Exception exception)
        {
            // The readiness scan does disk I/O + reflection and can throw transiently. This runs from
            // EditorApplication.update at ~2 Hz even while the inspector is hidden, so log at most
            // once per failure streak instead of spamming, keep the last good summary, and let a
            // later tick recover.
            if (!startScreenSummaryScanErrorLogged)
            {
                Debug.LogWarning("Gaming Couch: start screen readiness inspection failed; keeping last known state. " + exception);
                startScreenSummaryScanErrorLogged = true;
            }

            return;
        }

        if (hasStartScreenSummary && StartScreenSummariesEqual(startScreenSummary, next))
        {
            return;
        }

        startScreenSummary = next;
        hasStartScreenSummary = true;
        Repaint();
    }

    private void MaybeRefreshStartScreenSummary()
    {
        if (EditorApplication.timeSinceStartup < nextStartScreenSummaryRefreshTime)
        {
            return;
        }

        RefreshStartScreenSummary();
    }

    // The HelpBox shows summary.message with a MessageType derived from summary.state,
    // so those two fields fully determine the visible output.
    private static bool StartScreenSummariesEqual(
        GCStartScreenReadinessSummary a,
        GCStartScreenReadinessSummary b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.state == b.state && string.Equals(a.message, b.message, StringComparison.Ordinal);
    }

    private static MessageType GetStartScreenSummaryMessageType(GCStartScreenReadinessSummaryState state)
    {
        switch (state)
        {
            case GCStartScreenReadinessSummaryState.Ready:
                return MessageType.Info;
            case GCStartScreenReadinessSummaryState.Warning:
            case GCStartScreenReadinessSummaryState.Actionable:
            case GCStartScreenReadinessSummaryState.PendingCompilation:
                return MessageType.Warning;
            default:
                return MessageType.Error;
        }
    }

    private void OnEditorApplicationUpdate()
    {
        if (target == null)
        {
            DisposeDevJsonState();
            return;
        }

        if (devJsonState != null && devJsonState.PollForExternalChanges())
        {
            // A gc.dev/platform json change can alter readiness -- recompute eagerly on
            // the same path that already repaints for the dev-json view.
            RefreshStartScreenSummary();
            Repaint();
        }

        MaybeRefreshStartScreenSummary();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (devJsonState != null && devJsonState.HandlePlayModeStateChanged(change))
        {
            Repaint();
        }
    }

    private void DisposeDevJsonState()
    {
        EditorApplication.update -= OnEditorApplicationUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        GCDevJsonEditorPlayModeGate.Unregister(devJsonState);
        devJsonState = null;
        devJsonView = null;
        startScreenSummary = null;
        hasStartScreenSummary = false;
        nextStartScreenSummaryRefreshTime = 0;
    }
}
