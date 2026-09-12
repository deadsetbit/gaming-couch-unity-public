using System;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCRuntimeDiagnosticsTests
{
    private double nowSeconds;

    [SetUp]
    public void SetUp()
    {
        nowSeconds = 10.0;
        GCRuntimeMessageOutput.ResetForTests(() => nowSeconds);
        GCRuntimeMessageOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
    }

    [TearDown]
    public void TearDown()
    {
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
    }

    [Test]
    public void DiagnosticWarningEmitsRuntimeMessagesEnvelopeWithDiagnosticPayload()
    {
        string emittedJson = null;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += json => emittedJson = json;
        nowSeconds = 10.125;

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.state\.clamped_value: Lives were clamped\.")
        );

        var envelopeJson = GCDiagnostics.Emit(
            GCDiagnosticCodes.ClampedValue,
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.State,
            "Lives were clamped.",
            new GCDiagnosticContext()
                .WithPlayerIndex(0)
                .AddDetail("field", "lives")
                .AddDetail("inputValue", -2)
                .AddDetail("allowedRange", new[] { 0, 99 })
                .AddDebug("note", "test")
        );

        Assert.That(envelopeJson, Is.EqualTo(emittedJson));
        Assert.That(envelopeJson, Does.Contain("\"type\":\"runtime_messages\""));
        Assert.That(envelopeJson, Does.Contain("\"v\":1"));
        Assert.That(envelopeJson, Does.Contain("\"type\":\"gc.diagnostic\""));
        Assert.That(envelopeJson, Does.Contain("\"name\":\"gc.state.clamped_value\""));
        Assert.That(envelopeJson, Does.Contain("\"seq\":1"));
        Assert.That(envelopeJson, Does.Contain("\"ms\":125"));
        Assert.That(envelopeJson, Does.Contain("\"ms\":125,\"playerIndex\":0,\"data\":{\"severity\":\"warning\""));
        Assert.That(envelopeJson, Does.Contain("\"severity\":\"warning\""));
        Assert.That(envelopeJson, Does.Contain("\"sourceArea\":\"state\""));
        Assert.That(envelopeJson, Does.Contain("\"message\":\"Lives were clamped.\""));
        Assert.That(envelopeJson, Does.Not.Contain("\"message\":\"Lives were clamped.\",\"playerIndex\":0"));
        Assert.That(envelopeJson, Does.Contain("\"details\":{\"field\":\"lives\",\"inputValue\":-2,\"allowedRange\":[0,99]}"));
        Assert.That(envelopeJson, Does.Contain("\"debug\":{\"note\":\"test\"}"));
    }

    [Test]
    public void DiagnosticSequenceIsOneBasedMonotonicPerActiveRun()
    {
        nowSeconds = 20.0;
        GCRuntimeMessageOutput.BeginActiveRun();
        nowSeconds = 20.001;
        var first = GCDiagnostics.Emit(
            GCDiagnosticCodes.FallbackActive,
            GCDiagnosticSeverity.Info,
            GCDiagnosticSourceAreas.Metadata,
            "Fallback metadata is active."
        );

        nowSeconds = 20.250;
        var second = GCDiagnostics.Emit(
            GCDiagnosticCodes.MissingPlatformData,
            GCDiagnosticSeverity.Info,
            GCDiagnosticSourceAreas.Metadata,
            "Platform data is missing."
        );

        Assert.That(first, Does.Contain("\"seq\":1"));
        Assert.That(first, Does.Contain("\"ms\":1"));
        Assert.That(second, Does.Contain("\"seq\":2"));
        Assert.That(second, Does.Contain("\"ms\":250"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DiagnosticPayloadSupportsBoundedMappingContext()
    {
        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.mapping\.invalid_player_index: Player index is outside the active mapping\.")
        );

        var envelopeJson = GCDiagnostics.Emit(
            GCDiagnosticCodes.InvalidPlayerIndex,
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.Mapping,
            "Player index is outside the active mapping.",
            new GCDiagnosticContext()
                .WithPlayerIndex(0)
                .WithMapping(new GCDiagnosticMappingContext()
                    .WithMappingId("run-map-1")
                    .WithSeed(12345)
                    .WithParticipantCount(2)
                    .WithOffendingReference("playerIndex:9"))
        );

        Assert.That(envelopeJson, Does.Contain("\"sourceArea\":\"mapping\""));
        Assert.That(envelopeJson, Does.Contain("\"playerIndex\":0,\"data\":{\"severity\":\"warning\",\"sourceArea\":\"mapping\""));
        Assert.That(envelopeJson, Does.Not.Contain("\"message\":\"Player index is outside the active mapping.\",\"playerIndex\":0"));
        Assert.That(envelopeJson, Does.Contain("\"mapping\":{\"mappingId\":\"run-map-1\",\"seed\":12345,\"participantCount\":2,\"offendingReference\":\"playerIndex:9\"}"));
    }

    [Test]
    public void DiagnosticWarningAndErrorMirrorToConsoleEvenWhenPackageLogLevelIsNone()
    {
        GCLog.logLevel = LogLevel.None;

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.runtime\.malformed_message: Malformed runtime message\.")
        );
        GCDiagnostics.Emit(
            GCDiagnosticCodes.MalformedMessage,
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.RuntimeMessages,
            "Malformed runtime message."
        );

        LogAssert.Expect(
            LogType.Error,
            new Regex(@"\[GC\] Diagnostic gc\.runtime\.invalid_game_over_placement: Game-over placement was rejected\.")
        );
        GCDiagnostics.Emit(
            GCDiagnosticCodes.InvalidGameOverPlacement,
            GCDiagnosticSeverity.Error,
            GCDiagnosticSourceAreas.RuntimeMessages,
            "Game-over placement was rejected."
        );
    }

    [Test]
    public void DiagnosticInfoIsStructuredOnly()
    {
        GCDiagnostics.Emit(
            GCDiagnosticCodes.FallbackActive,
            GCDiagnosticSeverity.Info,
            GCDiagnosticSourceAreas.Metadata,
            "Fallback metadata is active."
        );

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PlatformMetadataFallbackEmitsPersistentRuntimeDiagnostics()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        var view = GCPlatformRuntimeView.CreateFallback(
            GCPlatformRuntimeValidationState.Missing,
            GCPlatformRuntimeSource.Fallback(
                "gc.platform.json was not found.",
                "/tmp/gc.platform.json",
                null
            )
        );

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.metadata\.missing_platform_data: gc\.platform\.json was not found\.")
        );
        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.metadata\.fallback_active: Fallback platform metadata is active\.")
        );

        GamingCouch.EmitPlatformMetadataDiagnostics(view);

        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.metadata.missing_platform_data\""));
        Assert.That(emitted[0], Does.Contain("\"severity\":\"warning\""));
        Assert.That(emitted[0], Does.Contain("\"sourceArea\":\"metadata\""));
        Assert.That(emitted[0], Does.Contain("\"details\":{\"validationState\":\"missing\",\"selectedEntryKey\":\"notdefined\"}"));
        Assert.That(emitted[1], Does.Contain("\"name\":\"gc.metadata.fallback_active\""));
        Assert.That(emitted[1], Does.Contain("\"ms\":0"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void UnityLogCaptureIsOffByDefault()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;

        GCUnityLogCapture.CaptureForTests("Ignored normal log.", "", LogType.Log);
        GCUnityLogCapture.CaptureForTests("Ignored warning.", "", LogType.Warning);

        Assert.That(emitted, Is.Empty);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void UnityWarningAndErrorCaptureEmitsStructuredLogDiagnostics()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeMessageOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.WarningAndError,
        });
        nowSeconds = 10.250;

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.log\.runtime_warning: Unity warning captured\.")
        );
        GCUnityLogCapture.CaptureForTests("Physics warning.", "stack line", LogType.Warning);
        GCUnityLogCapture.CaptureForTests("Normal development log.", "", LogType.Log);

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"ms\":250"));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.log.runtime_warning\""));
        Assert.That(emitted[0], Does.Contain("\"severity\":\"warning\""));
        Assert.That(emitted[0], Does.Contain("\"sourceArea\":\"runtime_log\""));
        Assert.That(emitted[0], Does.Contain("\"debug\":{\"condition\":\"Physics warning.\",\"stackTrace\":\"stack line\"}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void UnityFullLogCaptureIncludesNormalLogsAsInfo()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeMessageOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
        });

        GCUnityLogCapture.CaptureForTests("Normal development log.", "", LogType.Log);

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"name\":\"gc.log.runtime_log\""));
        Assert.That(emitted[0], Does.Contain("\"severity\":\"info\""));
        Assert.That(emitted[0], Does.Contain("\"sourceArea\":\"runtime_log\""));
        Assert.That(emitted[0], Does.Contain("\"message\":\"Unity log captured.\""));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void UnityLogCaptureSkipsDiagnosticMirrorLogs()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeMessageOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
        });

        GCUnityLogCapture.CaptureForTests(
            "[GC] Diagnostic gc.state.clamped_value: Lives were clamped.",
            "",
            LogType.Warning
        );

        Assert.That(emitted, Is.Empty);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void UnityLogCaptureIsRateLimitedPerFrame()
    {
        var emitted = new System.Collections.Generic.List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeMessageOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
        });

        for (var index = 0; index < 25; index++)
        {
            GCUnityLogCapture.CaptureForTests("Normal development log " + index, "", LogType.Log);
        }

        Assert.That(emitted, Has.Count.EqualTo(20));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DiagnosticValidationRejectsUnknownCodeAndSourceArea()
    {
        Assert.Throws<ArgumentException>(() => GCDiagnostics.Emit(
            "gc.state.not_in_catalog",
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.State,
            "Invalid code."
        ));

        Assert.Throws<ArgumentException>(() => GCDiagnostics.Emit(
            GCDiagnosticCodes.ClampedValue,
            GCDiagnosticSeverity.Warning,
            "hud",
            "Invalid source area."
        ));

        Assert.Throws<ArgumentException>(() => GCDiagnostics.Emit(
            GCDiagnosticCodes.ClampedValue,
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.RuntimeMessages,
            "Mismatched source area."
        ));

        Assert.Throws<ArgumentException>(() => GCDiagnostics.Emit(
            GCDiagnosticCodes.MalformedScreenSpace,
            GCDiagnosticSeverity.Warning,
            GCDiagnosticSourceAreas.RuntimeMessages,
            "Screen-space diagnostics use the screen_space source area."
        ));
    }

    [Test]
    public void DiagnosticFieldsRejectNestedDataAndNegativePlayerIndex()
    {
        Assert.Throws<ArgumentException>(() => new GCDiagnosticContext().AddDetail(
            "nested",
            new object[] { new[] { "not-flat" } }
        ));

        Assert.Throws<ArgumentOutOfRangeException>(() => new GCDiagnosticContext().WithPlayerIndex(-1));
    }

    // The ADR 0006 privacy boundary: every platform player-id key is rejected on both field
    // channels. A fifth key belongs here as one more [TestCase].
    [TestCase("playerId")]
    [TestCase("playerIds")]
    [TestCase("platformPlayerId")]
    [TestCase("platformPlayerIds")]
    public void DiagnosticFieldsRejectPlatformPlayerIdKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => new GCDiagnosticContext().AddDetail(key, 123));
        Assert.Throws<ArgumentException>(() => new GCDiagnosticContext().AddDebug(key, "value"));
    }

    [Test]
    public void DiagnosticMappingContextRejectsUnboundedValues()
    {
        Assert.Throws<ArgumentException>(() => new GCDiagnosticMappingContext().WithMappingId(""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GCDiagnosticMappingContext().WithParticipantCount(-1));
        Assert.Throws<ArgumentException>(() => new GCDiagnosticMappingContext().WithMappingId(
            new string('m', GCDiagnosticMappingContext.MaxMappingIdLength + 1)
        ));
        Assert.Throws<ArgumentException>(() => new GCDiagnosticMappingContext().WithOffendingReference(
            new string('r', GCDiagnosticMappingContext.MaxOffendingReferenceLength + 1)
        ));
    }
}
