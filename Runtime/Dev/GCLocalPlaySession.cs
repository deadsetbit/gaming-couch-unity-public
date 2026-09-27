#if UNITY_EDITOR
using System;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal enum GCLocalPlaySessionBoundary
    {
        UnityPlayModeEntry,
        GamingCouchRestart,
    }

    internal enum GCLocalPlaySessionIssueSeverity
    {
        Warning,
        Error,
    }

    internal sealed class GCLocalPlaySessionIssue
    {
        internal readonly GCLocalPlaySessionIssueSeverity severity;
        internal readonly string message;
        internal readonly string path;
        internal readonly int seatIndex;
        internal readonly string fieldName;
        internal readonly string entryKey;
        internal readonly string code;

        private GCLocalPlaySessionIssue(
            GCLocalPlaySessionIssueSeverity severity,
            string message,
            string path,
            int seatIndex,
            string fieldName,
            string entryKey,
            string code
        )
        {
            this.severity = severity;
            this.message = message;
            this.path = path;
            this.seatIndex = seatIndex;
            this.fieldName = fieldName;
            this.entryKey = entryKey;
            this.code = code;
        }

        internal static GCLocalPlaySessionIssue Error(
            string message,
            string path,
            int seatIndex = 0,
            string fieldName = null,
            string entryKey = null,
            string code = null
        )
        {
            return new GCLocalPlaySessionIssue(
                GCLocalPlaySessionIssueSeverity.Error,
                message,
                path,
                seatIndex,
                fieldName,
                entryKey,
                code
            );
        }

        internal static GCLocalPlaySessionIssue Warning(
            string message,
            string path,
            int seatIndex = 0,
            string fieldName = null,
            string entryKey = null,
            string code = null
        )
        {
            return new GCLocalPlaySessionIssue(
                GCLocalPlaySessionIssueSeverity.Warning,
                message,
                path,
                seatIndex,
                fieldName,
                entryKey,
                code
            );
        }
    }

    internal sealed class GCLocalPlaySessionValidationResult
    {
        internal readonly GCLocalPlaySessionIssue[] issues;

        private GCLocalPlaySessionValidationResult(GCLocalPlaySessionIssue[] issues)
        {
            this.issues = issues ?? Array.Empty<GCLocalPlaySessionIssue>();
        }

        internal bool IsValid
        {
            get { return ErrorCount == 0; }
        }

        internal int ErrorCount
        {
            get { return CountIssues(GCLocalPlaySessionIssueSeverity.Error); }
        }

        internal int WarningCount
        {
            get { return CountIssues(GCLocalPlaySessionIssueSeverity.Warning); }
        }

        internal static GCLocalPlaySessionValidationResult Valid()
        {
            return new GCLocalPlaySessionValidationResult(Array.Empty<GCLocalPlaySessionIssue>());
        }

        internal static GCLocalPlaySessionValidationResult FromIssue(GCLocalPlaySessionIssue issue)
        {
            if (issue == null)
            {
                return Valid();
            }

            return new GCLocalPlaySessionValidationResult(new[] { issue });
        }

        internal static GCLocalPlaySessionValidationResult FromIssues(GCLocalPlaySessionIssue[] issues)
        {
            return new GCLocalPlaySessionValidationResult(issues);
        }

        private int CountIssues(GCLocalPlaySessionIssueSeverity severity)
        {
            var count = 0;
            for (var index = 0; index < issues.Length; index++)
            {
                if (issues[index] != null && issues[index].severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }

    internal sealed class GCLocalPlaySessionCaptureResult
    {
        internal readonly bool success;
        internal readonly GCSetupOptions setupOptions;
        internal readonly GCPlayOptions playOptions;
        internal readonly GCSeatIdentity[] seatIdentities;
        internal readonly GCLocalPlaySessionValidationResult validation;
        internal readonly string path;

        private GCLocalPlaySessionCaptureResult(
            bool success,
            GCSetupOptions setupOptions,
            GCPlayOptions playOptions,
            GCSeatIdentity[] seatIdentities,
            GCLocalPlaySessionValidationResult validation,
            string path
        )
        {
            this.success = success;
            this.setupOptions = setupOptions;
            this.playOptions = playOptions;
            this.seatIdentities = seatIdentities ?? Array.Empty<GCSeatIdentity>();
            this.validation = validation ?? GCLocalPlaySessionValidationResult.Valid();
            this.path = path;
        }

        internal static GCLocalPlaySessionCaptureResult Succeeded(
            GCSetupOptions setupOptions,
            GCPlayOptions playOptions,
            GCSeatIdentity[] seatIdentities,
            GCLocalPlaySessionValidationResult validation,
            string path
        )
        {
            return new GCLocalPlaySessionCaptureResult(true, setupOptions, playOptions, seatIdentities, validation, path);
        }

        internal static GCLocalPlaySessionCaptureResult Failed(
            string path,
            GCLocalPlaySessionValidationResult validation
        )
        {
            return new GCLocalPlaySessionCaptureResult(false, null, null, Array.Empty<GCSeatIdentity>(), validation, path);
        }
    }

    internal sealed class GCLocalPlaySessionPreflightResult
    {
        internal readonly bool success;
        internal readonly string message;
        internal readonly string path;
        internal readonly GCLocalPlaySessionValidationResult validation;

        private GCLocalPlaySessionPreflightResult(
            bool success,
            string message,
            string path,
            GCLocalPlaySessionValidationResult validation
        )
        {
            this.success = success;
            this.message = message;
            this.path = path;
            this.validation = validation ?? GCLocalPlaySessionValidationResult.Valid();
        }

        internal static GCLocalPlaySessionPreflightResult Succeeded()
        {
            return new GCLocalPlaySessionPreflightResult(true, null, null, GCLocalPlaySessionValidationResult.Valid());
        }

        internal static GCLocalPlaySessionPreflightResult Failed(
            string message,
            string path,
            GCLocalPlaySessionValidationResult validation
        )
        {
            return new GCLocalPlaySessionPreflightResult(false, message, path, validation);
        }
    }

    internal interface IGCLocalPlaySessionProvider
    {
        GCLocalPlaySessionCaptureResult Capture();
        GCLocalPlaySessionPreflightResult Validate(GCLocalPlaySessionBoundary context);
    }

    internal static class GCLocalPlaySession
    {
        private static Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandler;
        private static IGCLocalPlaySessionProvider provider;
        private static Action captureSucceededHandler;
        private static GCLocalPlaySessionCaptureResult activeCapture;

        internal static void RegisterProvider(IGCLocalPlaySessionProvider sessionProvider)
        {
            provider = sessionProvider;
        }

        internal static void RegisterPreflightHandler(
            Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> handler
        )
        {
            preflightHandler = handler;
        }

        internal static void RegisterCaptureSucceededHandler(Action handler)
        {
            captureSucceededHandler = handler;
        }

        internal static bool CaptureForRuntimeEntry()
        {
            activeCapture = Capture();
            LogCaptureIssues(activeCapture);

            if (activeCapture.success)
            {
                NotifyCaptureSucceeded();
            }

            return activeCapture.success;
        }

        internal static bool CaptureForRestart()
        {
            return CaptureForRuntimeEntry();
        }

        internal static GCLocalPlaySessionCaptureResult Capture()
        {
            if (provider == null)
            {
                return FailedForMissingProvider();
            }

            try
            {
                var result = provider.Capture();
                return result ?? GCLocalPlaySessionCaptureResult.Failed(
                    null,
                    GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                        "Local Play Contract capture did not produce a result.",
                        null
                    ))
                );
            }
            catch (Exception exception)
            {
                return FailedForCaptureException(exception);
            }
        }

        internal static bool TryRequireCapturedSetupOptions(string source, out GCSetupOptions setupOptions)
        {
            if (HasUsableActiveCapture() && activeCapture.setupOptions != null)
            {
                setupOptions = activeCapture.setupOptions;
                return true;
            }

            setupOptions = null;
            Debug.LogError(
                "[GamingCouch] " + source +
                " blocked because root gc.dev.json did not produce valid editor setup options. Fix gc.dev.json and re-enter Play Mode."
            );
            return false;
        }

        internal static bool TryRequireCapturedPlayOptions(
            string source,
            out GCPlayOptions playOptions,
            out GCSeatIdentity[] seatIdentities
        )
        {
            if (HasUsableActiveCapture() && activeCapture.playOptions != null)
            {
                playOptions = activeCapture.playOptions;
                seatIdentities = activeCapture.seatIdentities;
                return true;
            }

            playOptions = null;
            seatIdentities = Array.Empty<GCSeatIdentity>();
            Debug.LogError(
                "[GamingCouch] " + source +
                " blocked because root gc.dev.json is missing, invalid, or rejected by valid platform data gates. Fix gc.dev.json and re-enter Play Mode."
            );
            return false;
        }

        internal static bool TryRunRestartPreflight()
        {
            var result = RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);
            if (result.success)
            {
                return true;
            }

            LogBlockedBoundary(result);
            return false;
        }

        internal static GCLocalPlaySessionPreflightResult RunPreflight(GCLocalPlaySessionBoundary context)
        {
            if (preflightHandler != null)
            {
                try
                {
                    var result = preflightHandler(context);
                    if (result != null && !result.success)
                    {
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    return FailedForException(context, exception);
                }
            }

            return ValidateRootJson(context);
        }

        internal static GCLocalPlaySessionPreflightResult ValidateRootJson(GCLocalPlaySessionBoundary context)
        {
            if (provider == null)
            {
                return FailedPreflightForMissingProvider(context);
            }

            try
            {
                var result = provider.Validate(context);
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                return FailedForException(context, exception);
            }

            var message = GetBoundaryDisplayName(context) +
                          " blocked because Local Play Contract validation did not produce a result.";
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: "ReadError"
                ))
            );
        }

        internal static void LogBlockedBoundary(GCLocalPlaySessionPreflightResult result)
        {
            if (result == null)
            {
                return;
            }

            Debug.LogError("[GamingCouch] " + result.message);
            LogSessionIssues(result.validation);
        }

        internal static string GetBoundaryDisplayName(GCLocalPlaySessionBoundary context)
        {
            return context == GCLocalPlaySessionBoundary.GamingCouchRestart
                ? "Gaming Couch restart"
                : "Unity Play Mode entry";
        }

        internal static IDisposable OverrideForTests(
            IGCLocalPlaySessionProvider providerOverride,
            Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandlerOverride,
            Action captureSucceededHandlerOverride
        )
        {
            return new TestOverride(providerOverride, preflightHandlerOverride, captureSucceededHandlerOverride);
        }

        internal static GCLocalPlaySessionCaptureResult GetActiveCaptureForTests()
        {
            return activeCapture;
        }

        private static bool HasUsableActiveCapture()
        {
            return activeCapture != null &&
                   activeCapture.success &&
                   activeCapture.setupOptions != null &&
                   activeCapture.playOptions != null;
        }

        private static GCLocalPlaySessionCaptureResult FailedForMissingProvider()
        {
            return GCLocalPlaySessionCaptureResult.Failed(
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    "Local Play Contract provider is not registered.",
                    null,
                    code: "ReadError"
                ))
            );
        }

        private static GCLocalPlaySessionPreflightResult FailedPreflightForMissingProvider(
            GCLocalPlaySessionBoundary context
        )
        {
            var message = GetBoundaryDisplayName(context) +
                          " blocked because the Local Play Contract provider is not registered.";
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: "ReadError"
                ))
            );
        }

        private static GCLocalPlaySessionCaptureResult FailedForCaptureException(Exception exception)
        {
            var message = "gc.dev.json could not be read because editor JSON capture failed: " + exception.Message;
            return GCLocalPlaySessionCaptureResult.Failed(
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: "ReadError"
                ))
            );
        }

        private static GCLocalPlaySessionPreflightResult FailedForException(
            GCLocalPlaySessionBoundary context,
            Exception exception
        )
        {
            var message = GetBoundaryDisplayName(context) +
                          " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCLocalPlaySessionValidationResult.FromIssue(GCLocalPlaySessionIssue.Error(
                    message,
                    null,
                    code: "ReadError"
                ))
            );
        }

        private static void NotifyCaptureSucceeded()
        {
            if (captureSucceededHandler == null)
            {
                return;
            }

            try
            {
                captureSucceededHandler();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[GamingCouch] Could not update editor JSON state after capture: " + exception.Message);
            }
        }

        private static void LogCaptureIssues(GCLocalPlaySessionCaptureResult capture)
        {
            if (capture == null)
            {
                return;
            }

            LogSessionIssues(capture.validation);

            if (!capture.success && !HasAnyIssues(capture.validation))
            {
                Debug.LogError("[GamingCouch] Editor play capture failed. Fix root gc.dev.json and re-enter Play Mode.");
            }
        }

        private static void LogSessionIssues(GCLocalPlaySessionValidationResult validation)
        {
            var issues = validation != null ? validation.issues : null;
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

                var message = FormatIssue(issue);
                if (issue.severity == GCLocalPlaySessionIssueSeverity.Warning)
                {
                    Debug.LogWarning("[GamingCouch] " + message);
                }
                else
                {
                    Debug.LogError("[GamingCouch] " + message);
                }
            }
        }

        private static string FormatIssue(GCLocalPlaySessionIssue issue)
        {
            var message = issue.message ?? "Local Play Contract issue.";
            if (!string.IsNullOrEmpty(issue.code))
            {
                message = issue.code + ": " + message;
            }

            if (issue.seatIndex > 0)
            {
                message += " Seat " + issue.seatIndex + ".";
            }

            if (!string.IsNullOrEmpty(issue.fieldName))
            {
                message += " Field: " + issue.fieldName + ".";
            }

            if (!string.IsNullOrEmpty(issue.entryKey))
            {
                message += " Entry: " + issue.entryKey + ".";
            }

            if (!string.IsNullOrEmpty(issue.path))
            {
                message += " Path: " + issue.path + ".";
            }

            return message;
        }

        private static bool HasAnyIssues(GCLocalPlaySessionValidationResult validation)
        {
            return validation != null && validation.issues != null && validation.issues.Length > 0;
        }

        private sealed class TestOverride : IDisposable
        {
            private readonly Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> previousPreflightHandler;
            private readonly IGCLocalPlaySessionProvider previousProvider;
            private readonly Action previousCaptureSucceededHandler;
            private readonly GCLocalPlaySessionCaptureResult previousActiveCapture;
            private bool disposed;

            internal TestOverride(
                IGCLocalPlaySessionProvider providerOverride,
                Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandlerOverride,
                Action captureSucceededHandlerOverride
            )
            {
                previousPreflightHandler = preflightHandler;
                previousProvider = provider;
                previousCaptureSucceededHandler = captureSucceededHandler;
                previousActiveCapture = activeCapture;

                preflightHandler = preflightHandlerOverride;
                provider = providerOverride;
                captureSucceededHandler = captureSucceededHandlerOverride;
                activeCapture = null;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                preflightHandler = previousPreflightHandler;
                provider = previousProvider;
                captureSucceededHandler = previousCaptureSucceededHandler;
                activeCapture = previousActiveCapture;
                disposed = true;
            }
        }
    }
}
#endif
