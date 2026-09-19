using System;
using UnityEditor;
using UnityEngine;

internal sealed class GCStartScreenSetupActionResult
{
    internal readonly string message;
    internal readonly MessageType messageType;
    internal readonly string[] details;
    internal readonly UnityEngine.Object focusTarget;
    internal readonly bool shouldPingFocusTarget;
    internal readonly bool shouldRefreshAndRepaint;

    internal GCStartScreenSetupActionResult(
        string message,
        MessageType messageType,
        string[] details,
        UnityEngine.Object focusTarget,
        bool shouldPingFocusTarget,
        bool shouldRefreshAndRepaint
    )
    {
        this.message = message;
        this.messageType = messageType;
        this.details = details ?? new string[0];
        this.focusTarget = focusTarget;
        this.shouldPingFocusTarget = shouldPingFocusTarget;
        this.shouldRefreshAndRepaint = shouldRefreshAndRepaint;
    }
}

internal static class GamingCouchStartScreenSetupActions
{
    internal static bool ShouldShowActiveSceneSetupAction(GCStartScreenReadiness readiness)
    {
        if (readiness == null)
        {
            return false;
        }

        if (!IsReadinessCheckSatisfied(readiness, GCStartScreenReadinessCheckId.ActiveScene))
        {
            return false;
        }

        return readiness.HasSafeAutomatableSetupActions;
    }

    internal static bool IsActiveSceneSetupActionBlocked(GCStartScreenReadiness readiness)
    {
        if (GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return true;
        }

        if (readiness == null)
        {
            return true;
        }

        return !IsReadinessCheckSatisfied(readiness, GCStartScreenReadinessCheckId.ActiveScene) ||
               !readiness.HasSafeAutomatableSetupActions;
    }

    internal static bool IsChecklistActionDisabled(
        GCStartScreenReadinessCheck check,
        GCStartScreenReadiness readiness
    )
    {
        if (check == null || !check.HasAction)
        {
            return true;
        }

        if (check.HasFocusAction)
        {
            return check.action.target == null;
        }

        // External actions (e.g. opening Unity Hub) are always available; they never depend on
        // scene state or a pending setup, so they must not be gated like setup actions.
        if (check.HasExternalAction)
        {
            return false;
        }

        return IsChecklistSetupActionBlocked(check, readiness);
    }

    internal static GCStartScreenSetupActionResult RunChecklistAction(GCStartScreenReadinessCheck check)
    {
        if (check == null || !check.HasAction)
        {
            return CreateResult(
                "No checklist item is available for this action.",
                MessageType.Warning,
                null,
                null,
                false,
                false
            );
        }

        return RunAction(check.action);
    }

    internal static GCStartScreenSetupActionResult RunAction(GCStartScreenReadinessAction action)
    {
        if (action == null || !action.IsAvailable)
        {
            return CreateResult(
                "No checklist item is available for this action.",
                MessageType.Warning,
                null,
                null,
                false,
                false
            );
        }

        if (action.isFocusAction)
        {
            return RunFocusAction(action);
        }

        if (action.isExternalAction)
        {
            return RunExternalAction(action.id);
        }

        return RunSetupAction(action.id);
    }

    internal static GCStartScreenSetupActionResult RunSetupAction(GCStartScreenReadinessActionId actionId)
    {
        switch (actionId)
        {
            case GCStartScreenReadinessActionId.CreateGamingCouch:
                return RunEnsureGamingCouch();
            case GCStartScreenReadinessActionId.CreateAndWireGameScript:
                return FromActiveSceneSetupResult(
                    GamingCouchActiveSceneSetup.EnsureActiveSceneGameListenerReference()
                );
            case GCStartScreenReadinessActionId.WirePlayerPrefab:
                return FromActiveSceneSetupResult(
                    GamingCouchActiveSceneSetup.EnsureActiveScenePlayerPrefabReference()
                );
            case GCStartScreenReadinessActionId.WireExampleGame:
                return FromWireExampleGameResult(GamingCouchActiveSceneSetup.WireExampleGame());
            case GCStartScreenReadinessActionId.SetFirstBuildSettingsScene:
                return FromBuildSettingsSetupResult(
                    GamingCouchBuildSettingsReadiness.EnsureActiveSceneFirstEnabled()
                );
            case GCStartScreenReadinessActionId.Select16By9GameView:
                return FromGameViewAspectSetupResult(
                    GamingCouchGameViewAspect.SelectExisting16By9Size()
                );
            case GCStartScreenReadinessActionId.SetUpWebGLExport:
                return OpenWebGLExportSetupPreview(null);
            default:
                return CreateResult(
                    "No setup action is available for this checklist item.",
                    MessageType.Info,
                    null,
                    null,
                    false,
                    false
                );
        }
    }

    internal static GCStartScreenSetupActionResult RunExternalAction(GCStartScreenReadinessActionId actionId)
    {
        switch (actionId)
        {
            case GCStartScreenReadinessActionId.OpenWebGLModuleInstallHelp:
                return OpenWebGLModuleInstallHelp();
            default:
                return CreateResult(
                    "No external action is available for this checklist item.",
                    MessageType.Info,
                    null,
                    null,
                    false,
                    false
                );
        }
    }

    // We cannot install a Hub module from the editor (no API) or deep-link to the module screen
    // (unityhub:// only supports editor-version installs and opening projects). Best we can do:
    // surface the Hub and hand the developer the exact, version-filled steps to paste/follow.
    internal static GCStartScreenSetupActionResult OpenWebGLModuleInstallHelp()
    {
        var steps = BuildWebGLModuleInstallSteps(Application.unityVersion);
        EditorGUIUtility.systemCopyBuffer = steps;
        Application.OpenURL("unityhub://");

        return CreateResult(
            "Opened Unity Hub and copied WebGL Build Support install steps to the clipboard.",
            MessageType.Info,
            new[] { steps },
            null,
            false,
            false
        );
    }

    internal static string BuildWebGLModuleInstallSteps(string unityVersion)
    {
        return
            "Install Web Build Support for Unity " + unityVersion + ":\n" +
            "1. In Unity Hub, open the Installs tab.\n" +
            "2. Click the gear icon on Unity " + unityVersion + " and choose \"Add modules\".\n" +
            "3. Enable \"Web Build Support\" (formerly \"WebGL Build Support\") and select Install.\n" +
            "4. Reopen this project after the install completes.\n" +
            "CLI alternative: unity install-modules -e " + unityVersion + " -m webgl";
    }

    internal static GCStartScreenSetupActionResult RunActiveSceneSetup()
    {
        return FromActiveSceneSetupResult(GamingCouchActiveSceneSetup.EnsureActiveSceneSetup());
    }

    internal static GCStartScreenSetupActionResult OpenWebGLExportSetupPreview(
        Action<GCStartScreenSetupActionResult> onApplied
    )
    {
        GamingCouchWebGLBuildSettingsPreviewWindow.OpenWebGLExportSettings(result =>
        {
            if (onApplied != null)
            {
                onApplied(FromWebGLExportSetupResult(result));
            }
        });

        return CreateResult(
            "Web export settings preview opened.",
            MessageType.Info,
            null,
            null,
            false,
            false
        );
    }

    internal static GCStartScreenSetupActionResult OpenWebGLBuildSettingsProfilePreview(
        Action<GCStartScreenSetupActionResult> onApplied
    )
    {
        GamingCouchWebGLBuildSettingsPreviewWindow.OpenProfileSelector(
            GCWebGLBuildSettingsProfileId.Dev,
            outcome =>
            {
                if (onApplied != null)
                {
                    onApplied(FromWebGLBuildSettingsProfileOutcome(outcome));
                }
            }
        );

        return CreateResult(
            "WebGL build settings preview opened.",
            MessageType.Info,
            null,
            null,
            false,
            false
        );
    }

    internal static GCStartScreenSetupActionResult FromActiveSceneSetupResult(
        GCActiveSceneSetupResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Active Scene Setup did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            GetActiveSceneResultMessageType(result),
            result.details,
            null,
            false,
            true
        );
    }

    internal static GCStartScreenSetupActionResult FromExampleSceneCreationResult(
        GCExampleSceneCreationResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Create New Example Scene did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            GetExampleSceneCreationMessageType(result),
            result.details,
            null,
            false,
            true
        );
    }

    internal static GCStartScreenSetupActionResult FromWireExampleGameResult(
        GCWireExampleGameResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Wire example game did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            GetWireExampleGameMessageType(result),
            result.details,
            null,
            false,
            true
        );
    }

    internal static bool ShouldDisplayActionResult(MessageType messageType)
    {
        return messageType == MessageType.Warning || messageType == MessageType.Error;
    }

    internal static string FormatActionMessage(string message, string[] details)
    {
        if (details == null || details.Length == 0)
        {
            return message;
        }

        var formatted = message;
        for (var index = 0; index < details.Length; index++)
        {
            if (!string.IsNullOrEmpty(details[index]))
            {
                formatted += "\n- " + details[index];
            }
        }

        return formatted;
    }

    private static bool IsChecklistSetupActionBlocked(
        GCStartScreenReadinessCheck check,
        GCStartScreenReadiness readiness
    )
    {
        if (check == null || !check.HasSetupAction)
        {
            return true;
        }

        if (GamingCouchActiveSceneSetup.HasPendingSetup())
        {
            return true;
        }

        if (readiness == null)
        {
            return true;
        }

        return check.action.requiresLoadedActiveScene &&
               !IsReadinessCheckSatisfied(readiness, GCStartScreenReadinessCheckId.ActiveScene);
    }

    private static bool IsReadinessCheckSatisfied(
        GCStartScreenReadiness readiness,
        GCStartScreenReadinessCheckId id
    )
    {
        if (readiness == null)
        {
            return false;
        }

        GCStartScreenReadinessCheck check;
        return readiness.TryGetCheck(id, out check) && check.IsSatisfied;
    }

    private static GCStartScreenSetupActionResult RunFocusAction(GCStartScreenReadinessAction action)
    {
        var target = action != null ? action.target : null;
        if (target == null)
        {
            return CreateResult(
                "No checklist target is available to select.",
                MessageType.Warning,
                null,
                null,
                false,
                false
            );
        }

        return CreateResult(
            "Focused " + target.name + ".",
            MessageType.Info,
            null,
            target,
            true,
            false
        );
    }

    private static GCStartScreenSetupActionResult RunEnsureGamingCouch()
    {
        var result = GamingCouchSceneWiring.EnsureActiveSceneGamingCouch();
        return CreateResult(
            result.message,
            result.IsBlocked ? MessageType.Error : MessageType.Info,
            result.changed ? null : new[] { "No scene changes were needed; the existing GamingCouch object was reused." },
            result.gamingCouch != null ? result.gamingCouch.gameObject : null,
            false,
            true
        );
    }

    private static GCStartScreenSetupActionResult FromBuildSettingsSetupResult(
        GCActiveSceneBuildSettingsSetupResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Build Settings setup did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            result.IsBlocked ? MessageType.Error : MessageType.Info,
            result.details,
            null,
            false,
            true
        );
    }

    private static GCStartScreenSetupActionResult FromGameViewAspectSetupResult(
        GCGameViewAspectSetupResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Game View 16:9 setup did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            result.IsBlocked ? MessageType.Warning : MessageType.Info,
            result.details,
            null,
            false,
            true
        );
    }

    internal static GCStartScreenSetupActionResult FromWebGLExportSetupResult(
        GCWebGLExportSetupResult result
    )
    {
        if (result == null)
        {
            return CreateResult(
                "Web export settings did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            result.message,
            GetWebGLExportSetupResultMessageType(result),
            result.details,
            null,
            false,
            true
        );
    }

    internal static GCStartScreenSetupActionResult FromWebGLBuildSettingsProfileOutcome(
        GCWebGLPreviewApplyOutcome outcome
    )
    {
        if (outcome == null)
        {
            return CreateResult(
                "WebGL build settings preview did not return a result.",
                MessageType.Error,
                null,
                null,
                false,
                true
            );
        }

        return CreateResult(
            outcome.message,
            outcome.messageType,
            outcome.details,
            null,
            false,
            true
        );
    }

    private static MessageType GetActiveSceneResultMessageType(GCActiveSceneSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetExampleSceneCreationMessageType(GCExampleSceneCreationResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        if (result.IsCancelled)
        {
            return MessageType.Warning;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetWireExampleGameMessageType(GCWireExampleGameResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        if (result.IsCancelled)
        {
            return MessageType.Warning;
        }

        return result.IsPendingCompilation ? MessageType.Warning : MessageType.Info;
    }

    private static MessageType GetWebGLExportSetupResultMessageType(GCWebGLExportSetupResult result)
    {
        if (result.IsBlocked)
        {
            return MessageType.Error;
        }

        return result.HasWarning ? MessageType.Warning : MessageType.Info;
    }

    private static GCStartScreenSetupActionResult CreateResult(
        string message,
        MessageType messageType,
        string[] details,
        UnityEngine.Object focusTarget,
        bool shouldPingFocusTarget,
        bool shouldRefreshAndRepaint
    )
    {
        return new GCStartScreenSetupActionResult(
            message,
            messageType,
            details,
            focusTarget,
            shouldPingFocusTarget,
            shouldRefreshAndRepaint
        );
    }
}
