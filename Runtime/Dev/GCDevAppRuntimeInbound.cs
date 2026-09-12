#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal sealed class GCDevAppRuntimeInboundContext
    {
        internal static readonly GCDevAppRuntimeInboundContext Empty = new GCDevAppRuntimeInboundContext();

        internal bool isPaused;
    }

    internal enum GCDevAppRuntimeInboundStatus
    {
        Unhandled,
        Ignored,
        Intent,
    }

    internal enum GCDevAppRuntimeInboundIntentKind
    {
        None,
        Restart,
        Input,
        TimescaleState,
        RuntimeOutputOptions,
    }

    public struct CompactControllerInputFrame
    {
        public int playerIndex;
        public uint seq;
        public uint timestampMs;
        public GCControllerInputsData inputs;
    }

    internal sealed class GCDevAppRuntimeInboundDecision
    {
        internal readonly GCDevAppRuntimeInboundStatus status;
        internal readonly GCDevAppRuntimeInboundIntentKind intentKind;
        internal readonly int playerIndex;
        internal readonly GCControllerInputsData inputs;
        internal readonly bool hasInputSequence;
        internal readonly uint inputSequence;
        internal readonly float timescale;
        internal readonly bool paused;
        internal readonly bool shouldApplyPause;
        internal readonly string runtimeLogCaptureMode;
        internal readonly string reason;

        private GCDevAppRuntimeInboundDecision(
            GCDevAppRuntimeInboundStatus status,
            GCDevAppRuntimeInboundIntentKind intentKind,
            int playerIndex,
            GCControllerInputsData inputs,
            bool hasInputSequence,
            uint inputSequence,
            float timescale,
            bool paused,
            bool shouldApplyPause,
            string runtimeLogCaptureMode,
            string reason
        )
        {
            this.status = status;
            this.intentKind = intentKind;
            this.playerIndex = playerIndex;
            this.inputs = inputs;
            this.hasInputSequence = hasInputSequence;
            this.inputSequence = inputSequence;
            this.timescale = timescale;
            this.paused = paused;
            this.shouldApplyPause = shouldApplyPause;
            this.runtimeLogCaptureMode = runtimeLogCaptureMode;
            this.reason = reason;
        }

        internal static GCDevAppRuntimeInboundDecision Unhandled(string reason)
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Unhandled,
                intentKind: GCDevAppRuntimeInboundIntentKind.None,
                playerIndex: -1,
                inputs: default,
                hasInputSequence: false,
                inputSequence: 0,
                timescale: 1f,
                paused: false,
                shouldApplyPause: false,
                runtimeLogCaptureMode: null,
                reason: reason
            );
        }

        internal static GCDevAppRuntimeInboundDecision Ignored(string reason)
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Ignored,
                intentKind: GCDevAppRuntimeInboundIntentKind.None,
                playerIndex: -1,
                inputs: default,
                hasInputSequence: false,
                inputSequence: 0,
                timescale: 1f,
                paused: false,
                shouldApplyPause: false,
                runtimeLogCaptureMode: null,
                reason: reason
            );
        }

        internal static GCDevAppRuntimeInboundDecision Restart()
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Intent,
                intentKind: GCDevAppRuntimeInboundIntentKind.Restart,
                playerIndex: -1,
                inputs: default,
                hasInputSequence: false,
                inputSequence: 0,
                timescale: 1f,
                paused: false,
                shouldApplyPause: false,
                runtimeLogCaptureMode: null,
                reason: null
            );
        }

        internal static GCDevAppRuntimeInboundDecision Input(
            int playerIndex,
            GCControllerInputsData inputs,
            bool hasInputSequence,
            uint inputSequence
        )
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Intent,
                intentKind: GCDevAppRuntimeInboundIntentKind.Input,
                playerIndex: playerIndex,
                inputs: inputs,
                hasInputSequence: hasInputSequence,
                inputSequence: inputSequence,
                timescale: 1f,
                paused: false,
                shouldApplyPause: false,
                runtimeLogCaptureMode: null,
                reason: null
            );
        }

        internal static GCDevAppRuntimeInboundDecision TimescaleState(
            float timescale,
            bool paused,
            bool shouldApplyPause
        )
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Intent,
                intentKind: GCDevAppRuntimeInboundIntentKind.TimescaleState,
                playerIndex: -1,
                inputs: default,
                hasInputSequence: false,
                inputSequence: 0,
                timescale: timescale,
                paused: paused,
                shouldApplyPause: shouldApplyPause,
                runtimeLogCaptureMode: null,
                reason: null
            );
        }

        internal static GCDevAppRuntimeInboundDecision RuntimeOutputOptions(string runtimeLogCaptureMode)
        {
            return new GCDevAppRuntimeInboundDecision(
                status: GCDevAppRuntimeInboundStatus.Intent,
                intentKind: GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions,
                playerIndex: -1,
                inputs: default,
                hasInputSequence: false,
                inputSequence: 0,
                timescale: 1f,
                paused: false,
                shouldApplyPause: false,
                runtimeLogCaptureMode: runtimeLogCaptureMode,
                reason: null
            );
        }
    }

    internal sealed class GCDevAppRuntimeInbound
    {
        private const string DevToolMessageType = "gcdevtool";
        // The compact form the DevApp emits today (JSON.stringify with no indent argument). A hit
        // is a fast path past the key scan; a miss proves nothing, because any whitespace around
        // the key or the colon breaks the literal, so it falls through to the parse.
        private const string DevToolTypeProbe = "\"type\":\"" + DevToolMessageType + "\"";
        private const string RestartAction = "restart";
        private const string InputAction = "input";
        private const string TimescaleStateAction = "timescale_state";
        private const string RuntimeOutputOptionsAction = "runtime_output_options";
        private const string UnsupportedMessageTypeReason = "unsupported_message_type";

        // JsonUtility auto-instantiates [Serializable] class fields, so a message that
        // omits "payload"/"inputs"/"runtimeOutput" (or a value field like "timescale")
        // still deserializes to a non-null, zero-valued instance. Scan the raw JSON for
        // these keys so a missing-key message is ignored instead of applying zeros. The scan
        // only has to tell an absent key from a zero value, which it can do without pinning
        // the sender's whitespace.
        private const string TypeKey = "\"type\"";
        private const string InputsKey = "\"inputs\"";
        private const string TimescaleKey = "\"timescale\"";
        private const string PausedKey = "\"paused\"";
        private const string RuntimeOutputKey = "\"runtimeOutput\"";

        internal const byte CompactControllerInputTypeByte = 0x44;
        internal const int CompactControllerInputByteLength = 16;
        private const float CompactControllerInputAxisScale = 1000f;

        private readonly Dictionary<int, uint> lastInputSeqByPlayerIndex = new Dictionary<int, uint>();

        internal void ResetInputSequences()
        {
            lastInputSeqByPlayerIndex.Clear();
        }

        internal GCDevAppRuntimeInboundDecision RouteTextMessage(
            string message,
            GCDevAppRuntimeInboundContext context
        )
        {
            context = context ?? GCDevAppRuntimeInboundContext.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled("empty_message");
            }

            // Pre-filter only: text without a type key at all is not a DevApp command and is not
            // worth parsing. Everything else goes to the data.type check below, which is what
            // actually decides -- and is whitespace-agnostic.
            if (!message.Contains(DevToolTypeProbe) && !ContainsJsonKey(message, TypeKey))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled(UnsupportedMessageTypeReason);
            }

            var data = JsonUtility.FromJson<GCDevAppRuntimeDevToolMessage>(message);
            if (data == null || !string.Equals(data.type, DevToolMessageType, StringComparison.Ordinal))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled(UnsupportedMessageTypeReason);
            }

            return RouteDevToolAction(data, message, context);
        }

        internal GCDevAppRuntimeInboundDecision RouteValidatedCompactControllerInputFrame(CompactControllerInputFrame inputFrame)
        {
            if (lastInputSeqByPlayerIndex.TryGetValue(inputFrame.playerIndex, out var lastSeq) &&
                inputFrame.seq <= lastSeq)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("stale_input_sequence");
            }

            lastInputSeqByPlayerIndex[inputFrame.playerIndex] = inputFrame.seq;
            return GCDevAppRuntimeInboundDecision.Input(
                inputFrame.playerIndex,
                inputFrame.inputs,
                true,
                inputFrame.seq
            );
        }

        internal static bool TryParseCompactControllerInputFrame(
            byte[] message,
            out CompactControllerInputFrame inputFrame
        )
        {
            inputFrame = default;
            if (message == null || message.Length != CompactControllerInputByteLength)
            {
                return false;
            }

            if (message[0] != CompactControllerInputTypeByte)
            {
                return false;
            }

            var offset = 1;
            var playerIndex = ReadUInt16LittleEndian(message, offset);
            offset += 2;
            var seq = ReadUInt32LittleEndian(message, offset);
            offset += 4;
            var timestampMs = ReadUInt32LittleEndian(message, offset);
            offset += 4;
            var a0 = Mathf.Clamp(ReadInt16LittleEndian(message, offset), -1000, 1000) / CompactControllerInputAxisScale;
            offset += 2;
            var a1 = Mathf.Clamp(ReadInt16LittleEndian(message, offset), -1000, 1000) / CompactControllerInputAxisScale;
            offset += 2;
            var buttons = message[offset];

            inputFrame = new CompactControllerInputFrame
            {
                playerIndex = playerIndex,
                seq = seq,
                timestampMs = timestampMs,
                inputs = new GCControllerInputsData
                {
                    a0 = a0,
                    a1 = a1,
                    b0 = (buttons & 1) != 0 ? 1 : 0,
                    b1 = (buttons & 2) != 0 ? 1 : 0,
                    b2 = (buttons & 4) != 0 ? 1 : 0,
                }
            };
            return true;
        }

        private static GCDevAppRuntimeInboundDecision RouteDevToolAction(
            GCDevAppRuntimeDevToolMessage message,
            string rawMessage,
            GCDevAppRuntimeInboundContext context
        )
        {
            switch (message.action)
            {
                case RestartAction:
                    return GCDevAppRuntimeInboundDecision.Restart();
                case InputAction:
                    return RouteTextInput(message.payload, rawMessage);
                case TimescaleStateAction:
                    return RouteTimescaleState(message.payload, rawMessage, context);
                case RuntimeOutputOptionsAction:
                    return RouteRuntimeOutputOptions(message.payload, rawMessage);
                default:
                    return GCDevAppRuntimeInboundDecision.Unhandled("unsupported_devtool_action");
            }
        }

        private static GCDevAppRuntimeInboundDecision RouteTextInput(
            GCDevAppRuntimeDevToolPayload payload,
            string rawMessage
        )
        {
            if (payload == null || !ContainsJsonKey(rawMessage, InputsKey) || payload.inputs == null)
            {
                // The presence probe covers a missing key; the payload.inputs null-check covers an
                // explicit "inputs":null, which leaves the field null for BuildControllerInputs to
                // dereference.
                return GCDevAppRuntimeInboundDecision.Ignored("missing_input_payload");
            }

            if (payload.playerIndex < 0 && payload.playerId > 0)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("legacy_player_id_unsupported");
            }

            if (payload.playerIndex < 0)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_player_index");
            }

            return GCDevAppRuntimeInboundDecision.Input(
                payload.playerIndex,
                BuildControllerInputs(payload.inputs),
                false,
                0
            );
        }

        private static GCDevAppRuntimeInboundDecision RouteTimescaleState(
            GCDevAppRuntimeDevToolPayload payload,
            string rawMessage,
            GCDevAppRuntimeInboundContext context
        )
        {
            if (payload == null ||
                !ContainsJsonKey(rawMessage, TimescaleKey) ||
                !ContainsJsonKey(rawMessage, PausedKey))
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_timescale_payload");
            }

            return GCDevAppRuntimeInboundDecision.TimescaleState(
                payload.timescale,
                payload.paused,
                context.isPaused != payload.paused
            );
        }

        private static GCDevAppRuntimeInboundDecision RouteRuntimeOutputOptions(
            GCDevAppRuntimeDevToolPayload payload,
            string rawMessage
        )
        {
            if (payload == null || !ContainsJsonKey(rawMessage, RuntimeOutputKey) || payload.runtimeOutput == null)
            {
                // As with inputs: an explicit "runtimeOutput":null passes the presence probe but
                // leaves the field null, and reading runtimeOutput.runtimeLogCapture would throw.
                return GCDevAppRuntimeInboundDecision.Ignored("missing_runtime_output_payload");
            }

            return GCDevAppRuntimeInboundDecision.RuntimeOutputOptions(payload.runtimeOutput.runtimeLogCapture);
        }

        // Matches a quoted key followed by its colon, tolerating whitespace on either side of the
        // colon, so pretty-printed JSON reads the same as the compact form.
        private static bool ContainsJsonKey(string rawMessage, string quotedKey)
        {
            if (rawMessage == null)
            {
                return false;
            }

            var searchIndex = 0;
            while (searchIndex <= rawMessage.Length - quotedKey.Length)
            {
                var keyIndex = rawMessage.IndexOf(quotedKey, searchIndex, StringComparison.Ordinal);
                if (keyIndex < 0)
                {
                    return false;
                }

                var cursor = keyIndex + quotedKey.Length;
                while (cursor < rawMessage.Length && char.IsWhiteSpace(rawMessage[cursor]))
                {
                    cursor += 1;
                }

                if (cursor < rawMessage.Length && rawMessage[cursor] == ':')
                {
                    return true;
                }

                searchIndex = keyIndex + 1;
            }

            return false;
        }

        private static GCControllerInputsData BuildControllerInputs(GCDevAppRuntimeInputData inputs)
        {
            return new GCControllerInputsData
            {
                a0 = inputs.a0,
                a1 = inputs.a1,
                b0 = inputs.b0 > 0.5f ? 1 : 0,
                b1 = inputs.b1 > 0.5f ? 1 : 0,
                b2 = inputs.b2 > 0.5f ? 1 : 0,
            };
        }

        private static ushort ReadUInt16LittleEndian(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static short ReadInt16LittleEndian(byte[] bytes, int offset)
        {
            return (short)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)(
                bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24)
            );
        }
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeDevToolMessage
    {
        public string type;
        public string action;
        public GCDevAppRuntimeDevToolPayload payload;
        public long timestamp;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeDevToolPayload
    {
        public float timescale;
        public bool paused;
        public int playerIndex = -1;
        public int playerId;
        public GCDevAppRuntimeInputData inputs;
        public GCDevAppRuntimeOutputOptionsMessage runtimeOutput;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeInputData
    {
        public float a0;
        public float a1;
        public float b0;
        public float b1;
        public float b2;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeOutputOptionsMessage
    {
        public string runtimeLogCapture;
    }
}
#endif
