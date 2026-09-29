using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DSB.GC.RuntimeMessages
{
    internal static class GCRuntimeMessagePath
    {
        internal const string RuntimeMessages = "runtime_messages";
        internal const string ScreenSpace = "screen_space";
    }

    internal static class GCRuntimeMessageTypes
    {
        internal const string Player = "gc.player";
        internal const string State = "gc.state";
        internal const string Game = "gc.game";
        internal const string Diagnostic = "gc.diagnostic";
    }

    internal static class GCRuntimeMessageNames
    {
        internal const string Snapshot = "snapshot";
        internal const string ScoreChanged = "score_changed";
        internal const string LivesChanged = "lives_changed";
        internal const string StatusChanged = "status_changed";
        internal const string MeterChanged = "meter_changed";
        internal const string EliminationChanged = "elimination_changed";
        internal const string FinishChanged = "finish_changed";
        internal const string GameOver = "game_over";
    }

    internal static class GCRuntimeScreenSpaceAnchorTypes
    {
        internal const string PlayerOverhead = "playerOverhead";
        internal const string PlayerPosition = "playerPosition";

        internal static bool IsKnown(string anchorType)
        {
            return string.Equals(anchorType, PlayerOverhead, StringComparison.Ordinal) ||
                string.Equals(anchorType, PlayerPosition, StringComparison.Ordinal);
        }
    }

    internal sealed class GCRuntimeOutputConfiguration
    {
        internal bool stateSnapshotsEnabled = true;
        internal bool screenSpaceEnabled = true;
        internal string runtimeLogCaptureMode = GCRuntimeUnityLogCaptureMode.Off;

        internal static GCRuntimeOutputConfiguration FromOptions(GCRuntimeOutputOptions options)
        {
            if (options == null)
            {
                return new GCRuntimeOutputConfiguration();
            }

            return new GCRuntimeOutputConfiguration
            {
                stateSnapshotsEnabled = options.stateSnapshots,
                screenSpaceEnabled = options.screenSpace,
                runtimeLogCaptureMode = GCUnityLogCapture.NormalizeMode(options.runtimeLogCapture),
            };
        }
    }

    internal static class GCRuntimeOutput
    {
        private static bool isStateSnapshotPending;
        private static bool gameOverPlacementAccepted;
        private static bool isGameOverPlacementSubmissionInProgress;

        internal static event Action<string> RuntimeMessagesEmitted
        {
            add { GCRuntimeMessageOutput.RuntimeMessagesEmitted += value; }
            remove { GCRuntimeMessageOutput.RuntimeMessagesEmitted -= value; }
        }

        internal static event Action<string> ScreenSpaceEmitted
        {
            add { GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted += value; }
            remove { GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted -= value; }
        }

        internal static void BeginActiveRun()
        {
            BeginActiveRun(null);
        }

        internal static void BeginActiveRun(GCRuntimeOutputOptions outputOptions)
        {
            GCRuntimeMessageOutput.BeginActiveRun(outputOptions);
        }

        internal static bool IsStateSnapshotsEnabled => GCRuntimeMessageOutput.IsStateSnapshotsEnabled;
        internal static bool IsScreenSpaceEnabled => GCRuntimeMessageOutput.IsScreenSpaceEnabled;

        internal static void QueueStateSnapshot()
        {
            isStateSnapshotPending = true;
        }

        internal static void QueuePlayerTransition(string name, int playerIndex, string dataJson)
        {
            GCRuntimeMessageOutput.QueuePlayerTransition(name, playerIndex, dataJson);
        }

        internal static string FlushFrameOutput(Func<string> buildStateSnapshotPayloadJson)
        {
            FlushStateSnapshotToPending(buildStateSnapshotPayloadJson);
            return GCRuntimeMessageOutput.FlushPending();
        }

        internal static string EmitDiagnosticPayload(string name, string dataJson, int? playerIndex)
        {
            return GCRuntimeMessageOutput.FlushPendingWith(
                GCRuntimeMessageOutput.CreateRecord(GCRuntimeMessageTypes.Diagnostic, name, dataJson, playerIndex)
            );
        }

        internal static string EmitScreenSpace(long frameIndex, IReadOnlyList<GCRuntimeScreenSpaceAnchor> anchors)
        {
            return GCRuntimeScreenSpaceOutput.Emit(frameIndex, anchors);
        }

        internal static bool TrySubmitGameOverPlacement(
            int[] playerIndicesByPlacement,
            int playerCount,
            Func<int[], bool> validateGameOverPlacement,
            Action acceptGameOverPlacement,
            Func<string> buildStateSnapshotPayloadJson,
            out string runtimeMessagesJson
        )
        {
            runtimeMessagesJson = null;

            if (validateGameOverPlacement == null)
            {
                throw new ArgumentNullException(nameof(validateGameOverPlacement));
            }

            if (acceptGameOverPlacement == null)
            {
                throw new ArgumentNullException(nameof(acceptGameOverPlacement));
            }

            if (buildStateSnapshotPayloadJson == null)
            {
                throw new ArgumentNullException(nameof(buildStateSnapshotPayloadJson));
            }

            if (gameOverPlacementAccepted || isGameOverPlacementSubmissionInProgress)
            {
                EmitInvalidGameOverPlacementDiagnostic("Game-over placement was already accepted for this active run.");
                return false;
            }

            isGameOverPlacementSubmissionInProgress = true;
            try
            {
                if (!validateGameOverPlacement(playerIndicesByPlacement))
                {
                    EmitInvalidGameOverPlacementDiagnostic("Game-over placement was rejected.");
                    return false;
                }

                string gameOverPayloadJson;
                try
                {
                    gameOverPayloadJson = GCRuntimeGameOverPlacementPayload.BuildJson(playerIndicesByPlacement, playerCount);
                }
                catch (Exception exception)
                {
                    EmitInvalidGameOverPlacementDiagnostic(
                        "Game-over placement was rejected.",
                        new GCDiagnosticContext().AddDetail("reason", exception.Message)
                    );
                    return false;
                }

                acceptGameOverPlacement();
                QueueStateSnapshot();
                FlushStateSnapshotToPending(buildStateSnapshotPayloadJson);

                var gameOverRecord = GCRuntimeMessageOutput.CreateRecord(
                    GCRuntimeMessageTypes.Game,
                    GCRuntimeMessageNames.GameOver,
                    gameOverPayloadJson
                );
                gameOverPlacementAccepted = true;
                runtimeMessagesJson = GCRuntimeMessageOutput.FlushPendingWith(gameOverRecord);
                return true;
            }
            finally
            {
                isGameOverPlacementSubmissionInProgress = false;
            }
        }

        internal static void ResetForTests(Func<double> testRealtimeSecondsProvider)
        {
            GCRuntimeMessageOutput.ResetForTests(testRealtimeSecondsProvider);
        }

        internal static void HandleActiveRunBegan()
        {
            isStateSnapshotPending = false;
            gameOverPlacementAccepted = false;
            isGameOverPlacementSubmissionInProgress = false;
        }

        internal static void ResetRuntimeStateForTests()
        {
            isStateSnapshotPending = false;
            gameOverPlacementAccepted = false;
            isGameOverPlacementSubmissionInProgress = false;
        }

        private static void FlushStateSnapshotToPending(Func<string> buildStateSnapshotPayloadJson)
        {
            if (!isStateSnapshotPending)
            {
                return;
            }

            isStateSnapshotPending = false;
            if (!IsStateSnapshotsEnabled || buildStateSnapshotPayloadJson == null)
            {
                return;
            }

            var payloadJson = buildStateSnapshotPayloadJson();
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return;
            }

            GCRuntimeMessageOutput.QueueStateSnapshot(payloadJson);
        }

        private static void EmitInvalidGameOverPlacementDiagnostic(string message, GCDiagnosticContext context = null)
        {
            GCDiagnostics.Emit(
                GCDiagnosticCodes.InvalidGameOverPlacement,
                GCDiagnosticSeverity.Error,
                GCDiagnosticSourceAreas.RuntimeMessages,
                message,
                context
            );
        }
    }

    internal sealed class GCRuntimeStateSnapshotPayload
    {
        internal GCRuntimeStateSnapshotGame game;
        internal GCRuntimeStateSnapshotPlayer[] players;

        internal string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"game\":{\"status\":");
            GCRuntimeJson.AppendString(builder, game.status);
            builder.Append("},\"players\":[");

            for (var index = 0; index < players.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                players[index].AppendJson(builder);
            }

            builder.Append("]}");
            return builder.ToString();
        }
    }

    internal sealed class GCRuntimeStateSnapshotGame
    {
        internal string status;
    }

    internal sealed class GCRuntimeStateSnapshotPlayer
    {
        internal int playerIndex;
        internal int score;
        internal int lives;
        internal string status;
        internal string statusText;
        internal int meter;
        internal int placement;
        internal string eliminationState;
        internal string finishState;

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{\"playerIndex\":").Append(playerIndex);
            builder.Append(",\"score\":").Append(score);
            builder.Append(",\"lives\":").Append(lives);
            builder.Append(",\"status\":");
            GCRuntimeJson.AppendString(builder, status);
            builder.Append(",\"text\":");
            GCRuntimeJson.AppendString(builder, statusText);
            builder.Append(",\"meter\":").Append(meter);
            builder.Append(",\"placement\":").Append(placement);
            builder.Append(",\"elimination\":");
            GCRuntimeJson.AppendString(builder, eliminationState);
            builder.Append(",\"finish\":");
            GCRuntimeJson.AppendString(builder, finishState);
            builder.Append("}");
        }
    }

    internal static class GCRuntimeStateSnapshotBuilder
    {
        internal static GCRuntimeStateSnapshotPayload BuildPayload(
            GCStatus gameStatus,
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            var placementsByPlayer = BuildPlacementsByPlayer(players, playersByPlacement);
            var snapshotPlayers = new GCRuntimeStateSnapshotPlayer[players.Count];
            var seenPlayerIndices = new bool[players.Count];

            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index] ?? throw new ArgumentException("Runtime state snapshots cannot contain null players.", nameof(players));
                if (player.Index < 0 || player.Index >= players.Count)
                {
                    throw new ArgumentException("Runtime state snapshot playerIndex must be within the player range.", nameof(players));
                }

                if (seenPlayerIndices[player.Index])
                {
                    throw new ArgumentException("Runtime state snapshots cannot contain duplicate playerIndex values.", nameof(players));
                }

                seenPlayerIndices[player.Index] = true;

                if (!placementsByPlayer.TryGetValue(player, out var placement))
                {
                    throw new ArgumentException("Runtime state snapshot placements must contain every player exactly once.", nameof(playersByPlacement));
                }

                snapshotPlayers[index] = new GCRuntimeStateSnapshotPlayer
                {
                    playerIndex = player.Index,
                    score = player.Score,
                    lives = player.Lives,
                    status = GCPlayerEnumNames.Status(player.Status),
                    statusText = GCRuntimePayloadBounds.Truncate(player.StatusText ?? ""),
                    meter = player.Meter,
                    placement = placement,
                    eliminationState = GCPlayerEnumNames.EliminationState(player.EliminationState),
                    finishState = GCPlayerEnumNames.FinishState(player.FinishState),
                };
            }

            return new GCRuntimeStateSnapshotPayload
            {
                game = new GCRuntimeStateSnapshotGame
                {
                    status = ToSnapshotGameStatus(gameStatus),
                },
                players = snapshotPlayers,
            };
        }

        internal static string BuildPayloadJson(
            GCStatus gameStatus,
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            return BuildPayload(gameStatus, players, playersByPlacement).ToJson();
        }

        private static Dictionary<GCPlayer, int> BuildPlacementsByPlayer(
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            if (playersByPlacement == null)
            {
                throw new ArgumentNullException(nameof(playersByPlacement));
            }

            var placementsByPlayer = new Dictionary<GCPlayer, int>();
            var placement = 1;
            foreach (var player in playersByPlacement)
            {
                if (player == null)
                {
                    throw new ArgumentException("Runtime state snapshot placements cannot contain null players.", nameof(playersByPlacement));
                }

                if (placementsByPlayer.ContainsKey(player))
                {
                    throw new ArgumentException("Runtime state snapshot placements cannot contain duplicate players.", nameof(playersByPlacement));
                }

                placementsByPlayer[player] = placement;
                placement++;
            }

            if (placementsByPlayer.Count != players.Count)
            {
                throw new ArgumentException("Runtime state snapshot placements must match the player count.", nameof(playersByPlacement));
            }

            return placementsByPlayer;
        }

        private static string ToSnapshotGameStatus(GCStatus status)
        {
            switch (status)
            {
                case GCStatus.PendingSetup:
                    return "pending_setup";
                case GCStatus.SetupDone:
                    return "setup_done";
                case GCStatus.Playing:
                    return "playing";
                case GCStatus.GameOver:
                    return "game_over";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown GamingCouch status.");
            }
        }
    }

    internal static class GCRuntimePayloadBounds
    {
        internal const int MaxReasonTextLength = 256;
        internal const int MaxStatusTextLength = 256;

        internal static string Truncate(string value, int maxLength = MaxStatusTextLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? "";
            }

            return value.Substring(0, maxLength);
        }
    }

    internal static class GCRuntimeTransitionPayload
    {
        internal static string BuildIntJson(int playerIndex, int previousValue, int value, string reason)
        {
            ValidatePlayerIndex(playerIndex);
            var builder = new StringBuilder();
            builder.Append("{\"from\":").Append(previousValue);
            builder.Append(",\"to\":").Append(value);
            AppendReason(builder, reason);
            builder.Append("}");
            return builder.ToString();
        }

        internal static string BuildStringJson(int playerIndex, string previousValue, string value, string reason)
        {
            ValidatePlayerIndex(playerIndex);
            var builder = new StringBuilder();
            builder.Append("{\"from\":");
            GCRuntimeJson.AppendString(builder, previousValue);
            builder.Append(",\"to\":");
            GCRuntimeJson.AppendString(builder, value);
            AppendReason(builder, reason);
            builder.Append("}");
            return builder.ToString();
        }

        internal static string BuildStatusJson(
            int playerIndex,
            GCPlayerStatus previousStatus,
            string previousStatusText,
            GCPlayerStatus status,
            string statusText,
            string reason
        )
        {
            ValidatePlayerIndex(playerIndex);
            var builder = new StringBuilder();
            builder.Append("{\"from\":");
            AppendStatusValue(builder, previousStatus, previousStatusText);
            builder.Append(",\"to\":");
            AppendStatusValue(builder, status, statusText);
            AppendReason(builder, reason);
            builder.Append("}");
            return builder.ToString();
        }

        private static void AppendStatusValue(StringBuilder builder, GCPlayerStatus status, string statusText)
        {
            builder.Append("{\"status\":");
            GCRuntimeJson.AppendString(builder, GCPlayerEnumNames.Status(status));
            builder.Append(",\"text\":");
            GCRuntimeJson.AppendString(builder, GCRuntimePayloadBounds.Truncate(statusText ?? ""));
            builder.Append("}");
        }

        private static void AppendReason(StringBuilder builder, string reason)
        {
            if (reason == null)
            {
                return;
            }

            builder.Append(",\"reason\":");
            GCRuntimeJson.AppendString(builder, GCRuntimePayloadBounds.Truncate(reason, GCRuntimePayloadBounds.MaxReasonTextLength));
        }

        private static void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex), "Runtime transition playerIndex must be non-negative.");
            }
        }
    }

    internal static class GCRuntimeGameOverPlacementPayload
    {
        internal static string BuildJson(int[] playerIndicesByPlacement, int playerCount)
        {
            Validate(playerIndicesByPlacement, playerCount);

            var builder = new StringBuilder();
            builder.Append("{\"playersByPlacement\":[");
            for (var index = 0; index < playerIndicesByPlacement.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(playerIndicesByPlacement[index]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static void Validate(int[] playerIndicesByPlacement, int playerCount)
        {
            if (playerCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be non-negative.");
            }

            if (playerIndicesByPlacement == null)
            {
                throw new ArgumentNullException(nameof(playerIndicesByPlacement));
            }

            if (playerIndicesByPlacement.Length != playerCount)
            {
                throw new ArgumentException("Game-over placement must contain every player exactly once.", nameof(playerIndicesByPlacement));
            }

            var seen = new bool[playerCount];
            for (var index = 0; index < playerIndicesByPlacement.Length; index++)
            {
                var playerIndex = playerIndicesByPlacement[index];
                if (playerIndex < 0 || playerIndex >= playerCount)
                {
                    throw new ArgumentException("Game-over placement playerIndex is outside the player range.", nameof(playerIndicesByPlacement));
                }

                if (seen[playerIndex])
                {
                    throw new ArgumentException("Game-over placement cannot contain duplicate playerIndex values.", nameof(playerIndicesByPlacement));
                }

                seen[playerIndex] = true;
            }
        }
    }

    internal sealed class GCRuntimeMessageRecord
    {
        internal readonly string type;
        internal readonly string name;
        internal readonly long seq;
        internal readonly long ms;
        internal readonly string dataJson;
        internal readonly int? playerIndex;

        internal GCRuntimeMessageRecord(string type, string name, long seq, long ms, string dataJson, int? playerIndex)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("Runtime message type is required.", nameof(type));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Runtime message name is required.", nameof(name));
            }

            if (seq < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seq), "Runtime message sequence must be one-based.");
            }

            if (ms < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ms), "Runtime message time must be non-negative.");
            }

            if (string.IsNullOrWhiteSpace(dataJson))
            {
                throw new ArgumentException("Runtime message data JSON is required.", nameof(dataJson));
            }

            if (playerIndex.HasValue && playerIndex.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex), "Runtime message playerIndex must be non-negative.");
            }

            this.type = type;
            this.name = name;
            this.seq = seq;
            this.ms = ms;
            this.dataJson = dataJson;
            this.playerIndex = playerIndex;
        }

        internal string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, type);
            builder.Append(",\"name\":");
            GCRuntimeJson.AppendString(builder, name);
            builder.Append(",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"ms\":").Append(ms.ToString(CultureInfo.InvariantCulture));
            if (playerIndex.HasValue)
            {
                builder.Append(",\"playerIndex\":").Append(playerIndex.Value.ToString(CultureInfo.InvariantCulture));
            }

            builder.Append(",\"data\":").Append(dataJson);
            builder.Append("}");
            return builder.ToString();
        }
    }

    internal static class GCRuntimeMessageOutput
    {
        internal const int EnvelopeVersion = 1;
        internal const int MaxPendingMessagesPerBatch = 128;

        private static long sequence;
        private static bool hasActiveRun;
        private static double activeRunStartSeconds;
        private static Func<double> realtimeSecondsProvider = DefaultRealtimeSecondsProvider;
        private static readonly List<GCRuntimeMessageRecord> pendingMessages = new List<GCRuntimeMessageRecord>();
        private static GCRuntimeOutputConfiguration outputConfiguration = new GCRuntimeOutputConfiguration();

        internal static event Action<string> RuntimeMessagesEmitted;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchRuntimeMessages(string runtimeMessagesJson);
#endif

        internal static void BeginActiveRun()
        {
            BeginActiveRun(null);
        }

        internal static void BeginActiveRun(GCRuntimeOutputOptions outputOptions)
        {
            activeRunStartSeconds = NowSeconds();
            sequence = 0;
            hasActiveRun = true;
            pendingMessages.Clear();
            outputConfiguration = GCRuntimeOutputConfiguration.FromOptions(outputOptions);
            GCUnityLogCapture.Configure(outputConfiguration.runtimeLogCaptureMode);
            GCRuntimeOutput.HandleActiveRunBegan();
        }

        internal static bool IsStateSnapshotsEnabled => outputConfiguration.stateSnapshotsEnabled;
        internal static bool IsScreenSpaceEnabled => outputConfiguration.screenSpaceEnabled;

        internal static GCRuntimeMessageRecord CreateRecord(string type, string name, string dataJson)
        {
            return CreateRecord(type, name, dataJson, null);
        }

        internal static GCRuntimeMessageRecord CreateRecord(string type, string name, string dataJson, int? playerIndex)
        {
            EnsureActiveRun();
            return new GCRuntimeMessageRecord(
                type,
                name,
                ++sequence,
                ResolveRuntimeTimeMs(),
                dataJson,
                playerIndex
            );
        }

        internal static void QueueStateSnapshot(string dataJson)
        {
            if (!IsStateSnapshotsEnabled)
            {
                return;
            }

            QueueMessage(GCRuntimeMessageTypes.State, GCRuntimeMessageNames.Snapshot, dataJson, null);
        }

        internal static void QueuePlayerTransition(string name, int playerIndex, string dataJson)
        {
            QueueMessage(GCRuntimeMessageTypes.Player, name, dataJson, playerIndex);

            if (pendingMessages.Count >= MaxPendingMessagesPerBatch)
            {
                FlushPending();
            }
        }

        internal static void QueueMessage(string type, string name, string dataJson, int? playerIndex)
        {
            pendingMessages.Add(CreateRecord(type, name, dataJson, playerIndex));
        }

        internal static string FlushPending()
        {
            if (pendingMessages.Count == 0)
            {
                return null;
            }

            var messages = pendingMessages.ToArray();
            pendingMessages.Clear();
            return EmitBatch(messages);
        }

        internal static string FlushPendingWith(GCRuntimeMessageRecord message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            pendingMessages.Add(message);
            return FlushPending();
        }

        internal static string EmitSingle(string type, string name, string dataJson)
        {
            return EmitBatch(new[] { CreateRecord(type, name, dataJson) });
        }

        internal static string EmitBatch(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var json = BuildEnvelopeJson(messages);
            RuntimeMessagesEmitted?.Invoke(json);

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchRuntimeMessages(json);
#endif

            return json;
        }

        internal static string BuildEnvelopeJson(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var builder = new StringBuilder();
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, GCRuntimeMessagePath.RuntimeMessages);
            builder.Append(",\"v\":").Append(EnvelopeVersion);
            builder.Append(",\"messages\":[");

            for (var index = 0; index < messages.Count; index++)
            {
                if (messages[index] == null)
                {
                    throw new ArgumentException("Runtime message batches cannot contain null messages.", nameof(messages));
                }

                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(messages[index].ToJson());
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal static void ResetForTests(Func<double> testRealtimeSecondsProvider)
        {
            sequence = 0;
            hasActiveRun = false;
            activeRunStartSeconds = 0;
            pendingMessages.Clear();
            outputConfiguration = new GCRuntimeOutputConfiguration();
            realtimeSecondsProvider = testRealtimeSecondsProvider ?? DefaultRealtimeSecondsProvider;
            RuntimeMessagesEmitted = null;
            GCUnityLogCapture.ResetForTests();
            GCRuntimeScreenSpaceOutput.ResetForTests();
            GCRuntimeOutput.ResetRuntimeStateForTests();
        }

        private static void EnsureActiveRun()
        {
            if (!hasActiveRun)
            {
                BeginActiveRun();
            }
        }

        private static long ResolveRuntimeTimeMs()
        {
            var elapsedSeconds = Math.Max(0, NowSeconds() - activeRunStartSeconds);
            return (long)Math.Floor(elapsedSeconds * 1000.0);
        }

        internal static long ResolveRuntimeTimeMsForOutput()
        {
            EnsureActiveRun();
            return ResolveRuntimeTimeMs();
        }

        private static double NowSeconds()
        {
            return realtimeSecondsProvider();
        }

        private static double DefaultRealtimeSecondsProvider()
        {
            return Time.realtimeSinceStartupAsDouble;
        }
    }

    internal sealed class GCRuntimeScreenSpaceAnchor
    {
        internal readonly string anchorType;
        internal readonly int playerIndex;
        internal readonly float x;
        internal readonly float y;
        internal readonly bool isOffScreen;

        internal GCRuntimeScreenSpaceAnchor(string anchorType, int playerIndex, float x, float y, bool isOffScreen)
        {
            if (!GCRuntimeScreenSpaceAnchorTypes.IsKnown(anchorType))
            {
                throw new ArgumentException("Unknown screen_space anchor type.", nameof(anchorType));
            }

            if (playerIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex), "Screen-space playerIndex must be non-negative.");
            }

            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            {
                throw new ArgumentException("Screen-space coordinates must be finite.");
            }

            this.anchorType = anchorType;
            this.playerIndex = playerIndex;
            this.x = Mathf.Clamp01(x);
            this.y = Mathf.Clamp01(y);
            this.isOffScreen = isOffScreen;
        }

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, anchorType);
            builder.Append(",\"playerIndex\":").Append(playerIndex);
            builder.Append(",\"x\":").Append(x.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(",\"y\":").Append(y.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(",\"offscreen\":").Append(isOffScreen ? "true" : "false");
            builder.Append("}");
        }
    }

    internal static class GCRuntimeScreenSpaceOutput
    {
        internal const int EnvelopeVersion = 1;

        internal static event Action<string> ScreenSpaceEmitted;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchScreenSpace(string screenSpaceJson);
#endif

        internal static string Emit(long frameIndex, IReadOnlyList<GCRuntimeScreenSpaceAnchor> anchors)
        {
            if (!GCRuntimeMessageOutput.IsScreenSpaceEnabled)
            {
                return null;
            }

            var json = BuildEnvelopeJson(frameIndex, GCRuntimeMessageOutput.ResolveRuntimeTimeMsForOutput(), anchors);
            ScreenSpaceEmitted?.Invoke(json);

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchScreenSpace(json);
#endif

            return json;
        }

        internal static string BuildEnvelopeJson(long frameIndex, long runtimeTimeMs, IReadOnlyList<GCRuntimeScreenSpaceAnchor> anchors)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Screen-space frameIndex must be non-negative.");
            }

            if (runtimeTimeMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeTimeMs), "Screen-space runtimeTimeMs must be non-negative.");
            }

            if (anchors == null)
            {
                throw new ArgumentNullException(nameof(anchors));
            }

            ValidateUniqueAnchors(anchors);

            var builder = new StringBuilder();
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, GCRuntimeMessagePath.ScreenSpace);
            builder.Append(",\"v\":").Append(EnvelopeVersion);
            builder.Append(",\"frame\":").Append(frameIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"ms\":").Append(runtimeTimeMs.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"anchors\":[");

            for (var index = 0; index < anchors.Count; index++)
            {
                if (anchors[index] == null)
                {
                    throw new ArgumentException("Screen-space batches cannot contain null anchors.", nameof(anchors));
                }

                if (index > 0)
                {
                    builder.Append(",");
                }

                anchors[index].AppendJson(builder);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal static void ResetForTests()
        {
            ScreenSpaceEmitted = null;
        }

        private static void ValidateUniqueAnchors(IReadOnlyList<GCRuntimeScreenSpaceAnchor> anchors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < anchors.Count; index++)
            {
                if (anchors[index] == null)
                {
                    continue;
                }

                var key = anchors[index].anchorType + ":" + anchors[index].playerIndex;
                if (!seen.Add(key))
                {
                    throw new ArgumentException("Screen-space batches cannot contain duplicate anchor type/playerIndex pairs.", nameof(anchors));
                }
            }
        }
    }

    internal static class GCRuntimeJson
    {
        internal static void AppendString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
