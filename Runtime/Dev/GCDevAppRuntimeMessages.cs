#if UNITY_EDITOR
using System;
using DSB.GC;
using DSB.GC.RuntimeMessages;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal static class GCDevAppRuntimeMessages
    {
        internal const string RuntimeRegisterType = "runtime_register";
        internal const string RuntimeSnapshotType = "runtime_snapshot";
        internal const string RuntimeKind = "unity_editor";
        internal const string RendererMode = "external";
        internal const string DisplayName = "Unity Editor";

        internal static RuntimeRegisterMessage BuildRuntimeRegisterMessage(
            long timestamp,
            IGCLocalProjectRootResolver projectRootResolver
        )
        {
            projectRootResolver = projectRootResolver ?? new GCUnityLocalProjectRootResolver();
            var packageIdentity = GCEditorPackageIdentity.Resolve();
            return new RuntimeRegisterMessage
            {
                type = RuntimeRegisterType,
                timestamp = timestamp,
                runtimeKind = RuntimeKind,
                projectRootPath = projectRootResolver.ResolveProjectRootPath(),
                projectName = projectRootResolver.ResolveProjectName(),
                platform = packageIdentity.platform,
                gameProtocolVersion = packageIdentity.gameProtocolVersion,
                integrationName = packageIdentity.packageName,
                integrationVersion = packageIdentity.packageVersion,
                rendererMode = RendererMode,
                displayName = DisplayName,
            };
        }

        internal static RuntimeSnapshotState BuildRuntimeSnapshotState(
            string runId,
            bool isRunning,
            GCSeatIdentity[] seatIdentities,
            bool paused,
            float timescale
        )
        {
            return new RuntimeSnapshotState
            {
                runId = isRunning ? runId : null,
                isRunning = isRunning,
                capabilities = BuildRuntimeCapabilities(),
                seats = isRunning ? BuildRuntimeSeats(seatIdentities) : Array.Empty<RuntimeSeatMessage>(),
                paused = paused,
                timescale = timescale,
            };
        }

        internal static RuntimeSnapshotMessage BuildRuntimeSnapshotMessage(long timestamp, RuntimeSnapshotState state)
        {
            state = state ?? BuildRuntimeSnapshotState(null, false, null, false, Time.timeScale);
            return new RuntimeSnapshotMessage
            {
                type = RuntimeSnapshotType,
                timestamp = timestamp,
                runId = state.runId,
                isRunning = state.isRunning,
                capabilities = state.capabilities,
                seats = state.seats ?? Array.Empty<RuntimeSeatMessage>(),
                paused = state.paused,
                timescale = state.timescale,
            };
        }

        internal static string BuildRuntimeSnapshotSignature(RuntimeSnapshotState state)
        {
            return JsonUtility.ToJson(state ?? BuildRuntimeSnapshotState(null, false, null, false, Time.timeScale));
        }

        private static RuntimeCapabilitiesMessage BuildRuntimeCapabilities()
        {
            return new RuntimeCapabilitiesMessage
            {
                restart = true,
                pause = true,
                timescale = true,
            };
        }

        private static RuntimeSeatMessage[] BuildRuntimeSeats(GCSeatIdentity[] seatIdentities)
        {
            if (seatIdentities == null || seatIdentities.Length == 0)
            {
                return Array.Empty<RuntimeSeatMessage>();
            }

            var seats = new RuntimeSeatMessage[seatIdentities.Length];
            for (var index = 0; index < seatIdentities.Length; index++)
            {
                var seatIdentity = seatIdentities[index];
                var sourceSeatIndex = seatIdentity.sourceSeatIndex > 0 ? seatIdentity.sourceSeatIndex : index + 1;
                seats[index] = new RuntimeSeatMessage
                {
                    playerIndex = index,
                    seatIndex = sourceSeatIndex,
                    label = string.IsNullOrWhiteSpace(seatIdentity.label) ? "Seat " + sourceSeatIndex : seatIdentity.label,
                    type = ResolveSeatType(seatIdentity.playerType),
                };
            }

            return seats;
        }

        private static string ResolveSeatType(GCPlayerType playerType)
        {
            return playerType == GCPlayerType.bot ? "bot" : "player";
        }

    }

    // The DevApp keys per-run bookkeeping off runId: it refuses a second game-over for a runId it
    // has already accepted, and only clears per-run diagnostics when the id changes. The id must
    // therefore change exactly once per run and stay put across socket reconnects, so it is owned
    // here rather than by the websocket connection.
    internal static class GCDevAppRunIdentity
    {
        internal static string CurrentRunId { get; private set; }

        internal static void BeginRun()
        {
            CurrentRunId = Guid.NewGuid().ToString("N");
        }
    }

    internal static class GCDevAppRuntimeOutputSettings
    {
        private static string runtimeLogCaptureMode = GCRuntimeUnityLogCaptureMode.Off;
        private static bool hasRuntimeLogCaptureModeOverride;

        internal static void SetRuntimeLogCaptureMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                return;
            }

            runtimeLogCaptureMode = GCUnityLogCapture.NormalizeMode(mode);
            hasRuntimeLogCaptureModeOverride = true;
        }

        // GCActiveRunProjection calls this once per Play() -- restart included -- so it is also the
        // run-start signal reachable from the dev integration, and where the run id is minted.
        internal static GCRuntimeOutputOptions Apply(GCRuntimeOutputOptions options)
        {
            GCDevAppRunIdentity.BeginRun();

            var outputOptions = options ?? new GCRuntimeOutputOptions();
            if (!hasRuntimeLogCaptureModeOverride)
            {
                return outputOptions;
            }

            outputOptions.runtimeLogCapture = runtimeLogCaptureMode;
            return outputOptions;
        }

        internal static void ResetForTests()
        {
            runtimeLogCaptureMode = GCRuntimeUnityLogCaptureMode.Off;
            hasRuntimeLogCaptureModeOverride = false;
        }
    }

    [Serializable]
    public class RuntimeCapabilitiesMessage
    {
        public bool restart;
        public bool pause;
        public bool timescale;
    }

    [Serializable]
    public class RuntimeSeatMessage
    {
        public int playerIndex;
        public int seatIndex;
        public string label;
        public string type;
    }

    [Serializable]
    public class RuntimeRegisterMessage
    {
        public string type;
        public long timestamp;
        public string runtimeKind;
        public string projectRootPath;
        public string projectName;
        public string platform;
        public int gameProtocolVersion;
        public string integrationName;
        public string integrationVersion;
        public string rendererMode;
        public string displayName;
    }

    [Serializable]
    public class RuntimeSnapshotState
    {
        public string runId;
        public bool isRunning;
        public RuntimeCapabilitiesMessage capabilities;
        public RuntimeSeatMessage[] seats;
        public bool paused;
        public float timescale;
    }

    [Serializable]
    public class RuntimeSnapshotMessage
    {
        public string type;
        public long timestamp;
        public string runId;
        public bool isRunning;
        public RuntimeCapabilitiesMessage capabilities;
        public RuntimeSeatMessage[] seats;
        public bool paused;
        public float timescale;
    }

}
#endif
