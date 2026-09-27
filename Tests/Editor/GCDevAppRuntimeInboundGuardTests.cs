#if UNITY_EDITOR
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

// Guards the DevApp inbound router against missing-key messages. JsonUtility.FromJson
// auto-instantiates [Serializable] class fields, so a `payload == null` /
// `payload.inputs == null` check alone can never fire: a payload-less or field-less
// message deserializes to a non-null, zero-valued instance. The router scans the raw
// JSON for the keys, so a missing-key message is ignored instead of applying zeros.
// The scans must stay whitespace-agnostic: the DevApp's compact encoding is not a
// contract (it is JSON.stringify with no indent argument today, and its own README
// documents the spaced form), so the pretty-printed cases below are part of the guard.
// These types live behind #if UNITY_EDITOR.
public sealed class GCDevAppRuntimeInboundGuardTests
{
    [Test]
    public void PayloadLessTimescaleStateIsIgnoredAndDoesNotUnpause()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // Without the presence probe this deserializes to timescale=0 (clamped to 0.1
        // downstream) and paused=false, unpausing a paused game via shouldApplyPause.
        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\"}",
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_timescale_payload"));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.None));
        Assert.That(decision.shouldApplyPause, Is.False);
    }

    [Test]
    public void EmptyPayloadTimescaleStateIsIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{}}",
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_timescale_payload"));
        Assert.That(decision.shouldApplyPause, Is.False);
    }

    [Test]
    public void PartialTimescaleStateMissingTimescaleIsIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{\"paused\":false}}",
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_timescale_payload"));
        Assert.That(decision.shouldApplyPause, Is.False);
    }

    [Test]
    public void WellFormedTimescaleStateStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{\"timescale\":0.5,\"paused\":true}}",
            Context(isPaused: false)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.TimescaleState));
        Assert.That(decision.timescale, Is.EqualTo(0.5f));
        Assert.That(decision.paused, Is.True);
        Assert.That(decision.shouldApplyPause, Is.True);
    }

    [Test]
    public void FieldLessInputIsIgnoredAndAppliesNoFrame()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // Payload + valid playerIndex present, but no "inputs": was applied as a zeroed frame.
        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerIndex\":0}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_input_payload"));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.None));
    }

    [Test]
    public void PayloadLessInputIsIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\"}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_input_payload"));
    }

    [Test]
    public void WellFormedInputStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerIndex\":1,\"inputs\":{\"a0\":1.0}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.playerIndex, Is.EqualTo(1));
        Assert.That(decision.inputs.a0, Is.EqualTo(1.0f));
    }

    [Test]
    public void ExplicitEmptyInputsObjectStillAppliesNeutralFrame()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // Presence, not value: an explicit (empty) "inputs" object is a valid neutral
        // frame -- only a MISSING inputs key is ignored.
        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerIndex\":0,\"inputs\":{}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.playerIndex, Is.EqualTo(0));
        Assert.That(decision.inputs.a0, Is.EqualTo(0f));
        Assert.That(decision.inputs.b0, Is.EqualTo(0));
    }

    [Test]
    public void PayloadLessRuntimeOutputOptionsIsIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"runtime_output_options\"}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_runtime_output_payload"));
    }

    [Test]
    public void WellFormedRuntimeOutputOptionsStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"runtime_output_options\",\"payload\":{\"runtimeOutput\":{\"runtimeLogCapture\":\"warning_and_error\"}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions));
        Assert.That(decision.runtimeLogCaptureMode, Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError));
    }

    [Test]
    public void ExplicitNullInputsDoesNotThrow()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // The presence probe passes for "inputs":null, so the explicit null-check is what keeps
        // BuildControllerInputs from dereferencing a field JsonUtility left null.
        Assert.DoesNotThrow(() => inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerIndex\":0,\"inputs\":null}}",
            Context()
        ));
    }

    [Test]
    public void ExplicitNullRuntimeOutputDoesNotThrow()
    {
        var inbound = new GCDevAppRuntimeInbound();

        Assert.DoesNotThrow(() => inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"runtime_output_options\",\"payload\":{\"runtimeOutput\":null}}",
            Context()
        ));
    }

    [Test]
    public void PresentZeroTimescaleAndPausedStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // present-but-zero must be applied (distinct from a MISSING key defaulting to zero, which is
        // ignored): "timescale":0 with "paused":false, from a currently-paused game.
        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{\"timescale\":0,\"paused\":false}}",
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.TimescaleState));
        Assert.That(decision.timescale, Is.EqualTo(0f));
        Assert.That(decision.paused, Is.False);
        Assert.That(decision.shouldApplyPause, Is.True);
    }

    [Test]
    public void SpacedDevToolMessageStillRoutes()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{ \"type\": \"gcdevtool\", \"action\": \"restart\" }",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Restart));
    }

    [Test]
    public void PrettyPrintedTimescaleStateStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"gcdevtool\",",
                "  \"action\": \"timescale_state\",",
                "  \"payload\": {",
                "    \"timescale\": 0.5,",
                "    \"paused\": true",
                "  }",
                "}"
            ),
            Context(isPaused: false)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.TimescaleState));
        Assert.That(decision.timescale, Is.EqualTo(0.5f));
        Assert.That(decision.paused, Is.True);
        Assert.That(decision.shouldApplyPause, Is.True);
    }

    [Test]
    public void PrettyPrintedTimescaleStateMissingTimescaleIsStillIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        // The tolerant scan must still tell an absent key from a present one, or the
        // missing-key guard degrades into "always applies".
        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"gcdevtool\",",
                "  \"action\": \"timescale_state\",",
                "  \"payload\": {",
                "    \"paused\": false",
                "  }",
                "}"
            ),
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_timescale_payload"));
        Assert.That(decision.shouldApplyPause, Is.False);
    }

    [Test]
    public void PrettyPrintedInputStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"gcdevtool\",",
                "  \"action\": \"input\",",
                "  \"payload\": {",
                "    \"playerIndex\": 1,",
                "    \"inputs\": {",
                "      \"a0\": 1.0",
                "    }",
                "  }",
                "}"
            ),
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.playerIndex, Is.EqualTo(1));
        Assert.That(decision.inputs.a0, Is.EqualTo(1.0f));
    }

    [Test]
    public void PrettyPrintedInputMissingInputsIsStillIgnored()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"gcdevtool\",",
                "  \"action\": \"input\",",
                "  \"payload\": {",
                "    \"playerIndex\": 1",
                "  }",
                "}"
            ),
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("missing_input_payload"));
    }

    [Test]
    public void PrettyPrintedRuntimeOutputOptionsStillApplies()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"gcdevtool\",",
                "  \"action\": \"runtime_output_options\",",
                "  \"payload\": {",
                "    \"runtimeOutput\": {",
                "      \"runtimeLogCapture\": \"warning_and_error\"",
                "    }",
                "  }",
                "}"
            ),
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions));
        Assert.That(decision.runtimeLogCaptureMode, Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError));
    }

    [Test]
    public void PrettyPrintedForeignMessageTypeStaysUnhandled()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            PrettyPrinted(
                "{",
                "  \"type\": \"runtimes_update\",",
                "  \"runtimes\": []",
                "}"
            ),
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Unhandled));
        Assert.That(decision.reason, Is.EqualTo("unsupported_message_type"));
    }

    // Mirrors JSON.stringify(message, null, 2): two-space indentation, a space after every
    // colon, and newlines between members.
    private static string PrettyPrinted(params string[] lines)
    {
        return string.Join("\n", lines);
    }

    private static GCDevAppRuntimeInboundContext Context(bool isPaused = false)
    {
        return new GCDevAppRuntimeInboundContext
        {
            isPaused = isPaused,
        };
    }
}
#endif
