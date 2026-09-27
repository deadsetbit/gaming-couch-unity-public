using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DSB.GC.RuntimeMessages
{
    internal enum GCDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    internal static class GCDiagnosticSourceAreas
    {
        internal const string Api = "api";
        internal const string Mapping = "mapping";
        internal const string State = "state";
        internal const string RuntimeMessages = "runtime_messages";
        internal const string ScreenSpace = "screen_space";
        internal const string Metadata = "metadata";
        internal const string RuntimeLog = "runtime_log";

        private static readonly HashSet<string> KnownSourceAreas = new HashSet<string>(StringComparer.Ordinal)
        {
            Api,
            Mapping,
            State,
            RuntimeMessages,
            ScreenSpace,
            Metadata,
            RuntimeLog,
        };

        internal static bool IsKnown(string sourceArea)
        {
            return KnownSourceAreas.Contains(sourceArea);
        }
    }

    internal static class GCDiagnosticCodes
    {
        internal const string RemovedIdentityApi = "gc.api.removed_identity_api";
        internal const string RemovedPlayerNameApi = "gc.api.removed_player_name_api";
        internal const string RemovedStateApi = "gc.api.removed_state_api";
        internal const string RemovedStoreApi = "gc.api.removed_store_api";
        internal const string UnsupportedMultiplayerApi = "gc.api.unsupported_multiplayer_api";
        internal const string LegacyRuntimePayload = "gc.api.legacy_runtime_payload";
        internal const string InvalidPlayerIndex = "gc.mapping.invalid_player_index";
        internal const string UnmappedParticipant = "gc.mapping.unmapped_participant";
        internal const string DuplicateElimination = "gc.state.duplicate_elimination";
        internal const string DuplicateFinish = "gc.state.duplicate_finish";
        internal const string InvalidRevoke = "gc.state.invalid_revoke";
        internal const string InvalidTransition = "gc.state.invalid_transition";
        internal const string PostGameOverMutation = "gc.state.post_game_over_mutation";
        internal const string ClampedValue = "gc.state.clamped_value";
        internal const string MalformedMessage = "gc.runtime.malformed_message";
        internal const string UnknownMessage = "gc.runtime.unknown_message";
        internal const string InvalidGameOverPlacement = "gc.runtime.invalid_game_over_placement";
        internal const string MalformedScreenSpace = "gc.runtime.malformed_screen_space";
        internal const string MissingPlatformData = "gc.metadata.missing_platform_data";
        internal const string InvalidPlatformData = "gc.metadata.invalid_platform_data";
        internal const string FallbackActive = "gc.metadata.fallback_active";
        internal const string RuntimeLog = "gc.log.runtime_log";
        internal const string RuntimeWarning = "gc.log.runtime_warning";
        internal const string RuntimeError = "gc.log.runtime_error";

        private static readonly HashSet<string> KnownCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            RemovedIdentityApi,
            RemovedPlayerNameApi,
            RemovedStateApi,
            RemovedStoreApi,
            UnsupportedMultiplayerApi,
            LegacyRuntimePayload,
            InvalidPlayerIndex,
            UnmappedParticipant,
            DuplicateElimination,
            DuplicateFinish,
            InvalidRevoke,
            InvalidTransition,
            PostGameOverMutation,
            ClampedValue,
            MalformedMessage,
            UnknownMessage,
            InvalidGameOverPlacement,
            MalformedScreenSpace,
            MissingPlatformData,
            InvalidPlatformData,
            FallbackActive,
            RuntimeLog,
            RuntimeWarning,
            RuntimeError,
        };

        internal static bool IsKnown(string code)
        {
            return KnownCodes.Contains(code);
        }

        internal static bool IsValidSourceArea(string code, string sourceArea)
        {
            if (!IsKnown(code) || !GCDiagnosticSourceAreas.IsKnown(sourceArea))
            {
                return false;
            }

            if (string.Equals(code, MalformedScreenSpace, StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.ScreenSpace, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.api.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.Api, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.mapping.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.Mapping, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.state.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.State, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.runtime.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.RuntimeMessages, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.metadata.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.Metadata, StringComparison.Ordinal);
            }

            if (code.StartsWith("gc.log.", StringComparison.Ordinal))
            {
                return string.Equals(sourceArea, GCDiagnosticSourceAreas.RuntimeLog, StringComparison.Ordinal);
            }

            return false;
        }
    }

    internal sealed class GCDiagnosticFields
    {
        internal const int MaxFieldCount = 16;
        internal const int MaxKeyLength = 64;
        internal const int MaxStringLength = 256;
        internal const int MaxArrayLength = 16;

        private readonly List<Entry> entries = new List<Entry>();

        internal int Count
        {
            get { return entries.Count; }
        }

        internal GCDiagnosticFields Add(string key, object value)
        {
            ValidateKey(key);
            ValidateValue(value);

            if (entries.Count >= MaxFieldCount)
            {
                throw new ArgumentException("Diagnostic fields are bounded to " + MaxFieldCount + " entries.");
            }

            entries.Add(new Entry(key, value));
            return this;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{");
            for (var index = 0; index < entries.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                GCRuntimeJson.AppendString(builder, entries[index].key);
                builder.Append(":");
                AppendValueJson(builder, entries[index].value);
            }
            builder.Append("}");
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Diagnostic field keys are required.");
            }

            if (key.Length > MaxKeyLength)
            {
                throw new ArgumentException("Diagnostic field keys are bounded to " + MaxKeyLength + " characters.");
            }

            if (string.Equals(key, "playerId", StringComparison.Ordinal) ||
                string.Equals(key, "playerIds", StringComparison.Ordinal) ||
                string.Equals(key, "platformPlayerId", StringComparison.Ordinal) ||
                string.Equals(key, "platformPlayerIds", StringComparison.Ordinal))
            {
                throw new ArgumentException("Public diagnostics must not expose platform player IDs.");
            }
        }

        private static void ValidateValue(object value)
        {
            if (value == null)
            {
                return;
            }

            if (value is string stringValue)
            {
                if (stringValue.Length > MaxStringLength)
                {
                    throw new ArgumentException("Diagnostic strings are bounded to " + MaxStringLength + " characters.");
                }

                return;
            }

            if (value is bool ||
                value is int ||
                value is long ||
                value is float ||
                value is double)
            {
                ValidateFiniteNumber(value);
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                var count = 0;
                foreach (var item in enumerable)
                {
                    count++;
                    if (count > MaxArrayLength)
                    {
                        throw new ArgumentException("Diagnostic arrays are bounded to " + MaxArrayLength + " entries.");
                    }

                    if (item is IEnumerable && !(item is string))
                    {
                        throw new ArgumentException("Diagnostic arrays must be flat.");
                    }

                    ValidateValue(item);
                }

                return;
            }

            throw new ArgumentException("Diagnostic fields only support primitive values and flat primitive arrays.");
        }

        private static void ValidateFiniteNumber(object value)
        {
            if (value is float floatValue && (float.IsNaN(floatValue) || float.IsInfinity(floatValue)))
            {
                throw new ArgumentException("Diagnostic numbers must be finite.");
            }

            if (value is double doubleValue && (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue)))
            {
                throw new ArgumentException("Diagnostic numbers must be finite.");
            }
        }

        private static void AppendValueJson(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string stringValue)
            {
                GCRuntimeJson.AppendString(builder, stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
                return;
            }

            if (value is int intValue)
            {
                builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long longValue)
            {
                builder.Append(longValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float floatValue)
            {
                builder.Append(floatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is double doubleValue)
            {
                builder.Append(doubleValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            var enumerable = (IEnumerable)value;
            builder.Append("[");
            var index = 0;
            foreach (var item in enumerable)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                AppendValueJson(builder, item);
                index++;
            }
            builder.Append("]");
        }

        private sealed class Entry
        {
            internal readonly string key;
            internal readonly object value;

            internal Entry(string key, object value)
            {
                this.key = key;
                this.value = value;
            }
        }
    }

    internal sealed class GCDiagnosticContext
    {
        internal int? playerIndex;
        internal GCDiagnosticMappingContext mapping;
        internal GCDiagnosticFields details;
        internal GCDiagnosticFields debug;

        internal GCDiagnosticContext WithPlayerIndex(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Diagnostic playerIndex must be non-negative.");
            }

            playerIndex = value;
            return this;
        }

        internal GCDiagnosticContext WithMapping(GCDiagnosticMappingContext value)
        {
            mapping = value ?? throw new ArgumentNullException(nameof(value));
            return this;
        }

        internal GCDiagnosticContext AddDetail(string key, object value)
        {
            if (details == null)
            {
                details = new GCDiagnosticFields();
            }

            details.Add(key, value);
            return this;
        }

        internal GCDiagnosticContext AddDebug(string key, object value)
        {
            if (debug == null)
            {
                debug = new GCDiagnosticFields();
            }

            debug.Add(key, value);
            return this;
        }
    }

    internal sealed class GCDiagnosticMappingContext
    {
        internal const int MaxMappingIdLength = 64;
        internal const int MaxOffendingReferenceLength = 128;

        private string mappingId;
        private long? seed;
        private int? participantCount;
        private string offendingReference;

        internal GCDiagnosticMappingContext WithMappingId(string value)
        {
            ValidateBoundedString(value, MaxMappingIdLength, nameof(value));
            mappingId = value;
            return this;
        }

        internal GCDiagnosticMappingContext WithSeed(long value)
        {
            seed = value;
            return this;
        }

        internal GCDiagnosticMappingContext WithParticipantCount(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Diagnostic mapping participant count must be non-negative.");
            }

            participantCount = value;
            return this;
        }

        internal GCDiagnosticMappingContext WithOffendingReference(string value)
        {
            ValidateBoundedString(value, MaxOffendingReferenceLength, nameof(value));
            offendingReference = value;
            return this;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{");
            var hasPrevious = false;

            AppendOptionalString(builder, "mappingId", mappingId, ref hasPrevious);
            AppendOptionalLong(builder, "seed", seed, ref hasPrevious);
            AppendOptionalLong(builder, "participantCount", participantCount, ref hasPrevious);
            AppendOptionalString(builder, "offendingReference", offendingReference, ref hasPrevious);

            builder.Append("}");
        }

        private static void ValidateBoundedString(string value, int maxLength, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Diagnostic mapping values are required.", paramName);
            }

            if (value.Length > maxLength)
            {
                throw new ArgumentException("Diagnostic mapping values are bounded to " + maxLength + " characters.", paramName);
            }
        }

        private static void AppendOptionalString(StringBuilder builder, string key, string value, ref bool hasPrevious)
        {
            if (value == null)
            {
                return;
            }

            AppendSeparator(builder, ref hasPrevious);
            GCRuntimeJson.AppendString(builder, key);
            builder.Append(":");
            GCRuntimeJson.AppendString(builder, value);
        }

        private static void AppendOptionalLong(StringBuilder builder, string key, long? value, ref bool hasPrevious)
        {
            if (!value.HasValue)
            {
                return;
            }

            AppendSeparator(builder, ref hasPrevious);
            GCRuntimeJson.AppendString(builder, key);
            builder.Append(":").Append(value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendSeparator(StringBuilder builder, ref bool hasPrevious)
        {
            if (hasPrevious)
            {
                builder.Append(",");
            }

            hasPrevious = true;
        }
    }

    internal static class GCDiagnostics
    {
        internal const int MaxMessageLength = 512;

        internal static string Emit(
            string code,
            GCDiagnosticSeverity severity,
            string sourceArea,
            string message,
            GCDiagnosticContext context = null
        )
        {
            ValidateDiagnostic(code, sourceArea, message, context);
            var payloadJson = BuildPayloadJson(severity, sourceArea, message, context);
            var envelopeJson = GCRuntimeOutput.EmitDiagnosticPayload(code, payloadJson, context?.playerIndex);
            MirrorToUnityConsole(code, severity, message);
            return envelopeJson;
        }

        private static void ValidateDiagnostic(
            string code,
            string sourceArea,
            string message,
            GCDiagnosticContext context
        )
        {
            if (!GCDiagnosticCodes.IsKnown(code))
            {
                throw new ArgumentException("Unknown GC diagnostic code: " + code, nameof(code));
            }

            if (!GCDiagnosticSourceAreas.IsKnown(sourceArea))
            {
                throw new ArgumentException("Unknown GC diagnostic source area: " + sourceArea, nameof(sourceArea));
            }

            if (!GCDiagnosticCodes.IsValidSourceArea(code, sourceArea))
            {
                throw new ArgumentException("GC diagnostic code " + code + " cannot use source area " + sourceArea + ".", nameof(sourceArea));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Length > MaxMessageLength)
            {
                throw new ArgumentException("Diagnostic messages are bounded to " + MaxMessageLength + " characters.", nameof(message));
            }

            if (context != null && context.playerIndex.HasValue && context.playerIndex.Value < 0)
            {
                throw new ArgumentException("Diagnostic playerIndex must be non-negative.", nameof(context));
            }
        }

        private static string BuildPayloadJson(
            GCDiagnosticSeverity severity,
            string sourceArea,
            string message,
            GCDiagnosticContext context
        )
        {
            var builder = new StringBuilder();
            builder.Append("{\"severity\":");
            GCRuntimeJson.AppendString(builder, FormatSeverity(severity));
            builder.Append(",\"sourceArea\":");
            GCRuntimeJson.AppendString(builder, sourceArea);
            builder.Append(",\"message\":");
            GCRuntimeJson.AppendString(builder, message);

            if (context != null && context.mapping != null)
            {
                builder.Append(",\"mapping\":");
                context.mapping.AppendJson(builder);
            }

            if (context != null && context.details != null && context.details.Count > 0)
            {
                builder.Append(",\"details\":");
                context.details.AppendJson(builder);
            }

            if (context != null && context.debug != null && context.debug.Count > 0)
            {
                builder.Append(",\"debug\":");
                context.debug.AppendJson(builder);
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatSeverity(GCDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case GCDiagnosticSeverity.Info:
                    return "info";
                case GCDiagnosticSeverity.Warning:
                    return "warning";
                case GCDiagnosticSeverity.Error:
                    return "error";
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown diagnostic severity.");
            }
        }

        private static void MirrorToUnityConsole(string code, GCDiagnosticSeverity severity, string message)
        {
            var mirroredMessage = "[GC] Diagnostic " + code + ": " + message;
            switch (severity)
            {
                case GCDiagnosticSeverity.Warning:
                    Debug.LogWarning(mirroredMessage);
                    break;
                case GCDiagnosticSeverity.Error:
                    Debug.LogError(mirroredMessage);
                    break;
            }
        }
    }

    internal static class GCUnityLogCapture
    {
        private const int MaxCapturedLogsPerFrame = 20;
        private const string LegacyWarningAndErrorMode = "warningAndError";

        private static string captureMode = GCRuntimeUnityLogCaptureMode.Off;
        private static bool isSubscribed;
        private static int currentFrameIndex = -1;
        private static int capturedLogsThisFrame;

        internal static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, GCRuntimeUnityLogCaptureMode.Full, StringComparison.Ordinal))
            {
                return GCRuntimeUnityLogCaptureMode.Full;
            }

            if (string.Equals(mode, GCRuntimeUnityLogCaptureMode.WarningAndError, StringComparison.Ordinal) ||
                string.Equals(mode, LegacyWarningAndErrorMode, StringComparison.Ordinal))
            {
                return GCRuntimeUnityLogCaptureMode.WarningAndError;
            }

            if (string.Equals(mode, GCRuntimeUnityLogCaptureMode.ErrorOnly, StringComparison.Ordinal) ||
                string.Equals(mode, "error", StringComparison.Ordinal))
            {
                return GCRuntimeUnityLogCaptureMode.ErrorOnly;
            }

            return GCRuntimeUnityLogCaptureMode.Off;
        }

        internal static void Configure(string mode)
        {
            captureMode = NormalizeMode(mode);
            currentFrameIndex = -1;
            capturedLogsThisFrame = 0;

            if (string.Equals(captureMode, GCRuntimeUnityLogCaptureMode.Off, StringComparison.Ordinal))
            {
                Unsubscribe();
                return;
            }

            Subscribe();
        }

        internal static void ResetForTests()
        {
            captureMode = GCRuntimeUnityLogCaptureMode.Off;
            currentFrameIndex = -1;
            capturedLogsThisFrame = 0;
            Unsubscribe();
        }

        internal static void CaptureForTests(string condition, string stackTrace, LogType type)
        {
            CaptureUnityLog(condition, stackTrace, type);
        }

        private static void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            Application.logMessageReceived += CaptureUnityLog;
            isSubscribed = true;
        }

        private static void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            Application.logMessageReceived -= CaptureUnityLog;
            isSubscribed = false;
        }

        private static void CaptureUnityLog(string condition, string stackTrace, LogType type)
        {
            if (!ShouldCapture(type) || IsDiagnosticMirror(condition) || IsRateLimited())
            {
                return;
            }

            string code;
            GCDiagnosticSeverity severity;
            string message;
            switch (type)
            {
                case LogType.Log:
                    code = GCDiagnosticCodes.RuntimeLog;
                    severity = GCDiagnosticSeverity.Info;
                    message = "Unity log captured.";
                    break;
                case LogType.Warning:
                    code = GCDiagnosticCodes.RuntimeWarning;
                    severity = GCDiagnosticSeverity.Warning;
                    message = "Unity warning captured.";
                    break;
                default:
                    code = GCDiagnosticCodes.RuntimeError;
                    severity = GCDiagnosticSeverity.Error;
                    message = "Unity error captured.";
                    break;
            }

            var context = new GCDiagnosticContext()
                .AddDetail("logType", type.ToString())
                .AddDetail("frameIndex", Time.frameCount)
                .AddDebug("condition", GCRuntimePayloadBounds.Truncate(condition ?? "", GCDiagnosticFields.MaxStringLength));

            if (!string.IsNullOrEmpty(stackTrace))
            {
                context.AddDebug("stackTrace", GCRuntimePayloadBounds.Truncate(stackTrace, GCDiagnosticFields.MaxStringLength));
            }

            GCDiagnostics.Emit(
                code,
                severity,
                GCDiagnosticSourceAreas.RuntimeLog,
                message,
                context
            );
        }

        private static bool ShouldCapture(LogType type)
        {
            if (string.Equals(captureMode, GCRuntimeUnityLogCaptureMode.Full, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(captureMode, GCRuntimeUnityLogCaptureMode.WarningAndError, StringComparison.Ordinal))
            {
                return type == LogType.Warning || IsError(type);
            }

            if (string.Equals(captureMode, GCRuntimeUnityLogCaptureMode.ErrorOnly, StringComparison.Ordinal))
            {
                return IsError(type);
            }

            return false;
        }

        private static bool IsError(LogType type)
        {
            return type == LogType.Error || type == LogType.Assert || type == LogType.Exception;
        }

        // DevApp logs its own transport traffic, so capturing those lines would feed
        // every emitted message back in as a new diagnostic within the same frame.
        private static bool IsDiagnosticMirror(string condition)
        {
            return !string.IsNullOrEmpty(condition) &&
                (condition.StartsWith("[GC] Diagnostic ", StringComparison.Ordinal) ||
                    condition.StartsWith("[GCDevApp]", StringComparison.Ordinal));
        }

        private static bool IsRateLimited()
        {
            if (currentFrameIndex != Time.frameCount)
            {
                currentFrameIndex = Time.frameCount;
                capturedLogsThisFrame = 0;
            }

            if (capturedLogsThisFrame >= MaxCapturedLogsPerFrame)
            {
                return true;
            }

            capturedLogsThisFrame++;
            return false;
        }
    }
}
