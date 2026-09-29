using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

public sealed class GCUnsupportedMultiplayerApiTests
{
    private GamingCouch gamingCouch;

    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 1.0);
        GCRuntimeMessageOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
        gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GCUnsupportedMultiplayerApiTests");
    }

    [TearDown]
    public void TearDown()
    {
        if (gamingCouch != null)
        {
            UnityEngine.Object.DestroyImmediate(gamingCouch.gameObject);
        }

        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
    }

    [Test]
    public void MultiplayerCapabilityProbeReturnsFalseByDefault()
    {
        Assert.That(gamingCouch.OnlineMultiplayerSupport, Is.False);
    }

    [Test]
    public void InspectorDoesNotExposeLegacyMultiplayerToggle()
    {
        Assert.That(GamingCouchInspectorHost.ShouldDrawPropertyPath("onlineMultiplayerSupport"), Is.False);
    }

    [Test]
    public void ServerReadyThrowsUnsupportedErrorAndEmitsDiagnosticByDefault()
    {
        IgnoreWhenUnsupportedMultiplayerOptInIsActive();
        AssertUnsupportedMultiplayerCall(
            "OnlineMultiplayerServerReady",
            () => gamingCouch.OnlineMultiplayerServerReady()
        );
    }

    [Test]
    public void ClientReadyThrowsUnsupportedErrorAndEmitsDiagnosticByDefault()
    {
        IgnoreWhenUnsupportedMultiplayerOptInIsActive();
        AssertUnsupportedMultiplayerCall(
            "OnlineMultiplayerClientReady",
            () => gamingCouch.OnlineMultiplayerClientReady()
        );
    }

    [Test]
    public void NgoAssemblyRequiresUnsupportedMultiplayerOptInDefine()
    {
        var asmdef = File.ReadAllText(FindPackageFile("Runtime/Unity/NGO/dsb.gc.unity.ngo.asmdef"));
        var defineConstraints = ExtractJsonArrayBlock(asmdef, "\"defineConstraints\"");

        Assert.That(defineConstraints, Does.Contain("\"GC_ENABLE_UNSUPPORTED_MULTIPLAYER\""));
        Assert.That(
            defineConstraints,
            Does.Not.Contain("\"GC_UNITY_NETCODE_GAMEOBJECTS\""),
            "GC_UNITY_NETCODE_GAMEOBJECTS is produced by this asmdef's versionDefines and is not safe as a defineConstraints entry."
        );
        Assert.That(asmdef, Does.Contain("\"versionDefines\""));
        Assert.That(asmdef, Does.Contain("\"define\": \"GC_UNITY_NETCODE_GAMEOBJECTS\""));
    }

    [Test]
    public void NgoWebGlPluginsRequireUnsupportedMultiplayerOptInDefine()
    {
        AssertPluginRequiresUnsupportedMultiplayerOptIn("Runtime/Unity/NGO/Transport/GCClient.jslib.meta");
        AssertPluginRequiresUnsupportedMultiplayerOptIn("Runtime/Unity/NGO/Transport/GCServer.jslib.meta");
    }

    private static void AssertUnsupportedMultiplayerCall(string apiName, TestDelegate call)
    {
        string emittedJson = null;
        Action<string> runtimeMessagesHandler = json => emittedJson = json;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += runtimeMessagesHandler;

        try
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[GC\] Diagnostic gc\.api\.unsupported_multiplayer_api: Gaming Couch online multiplayer APIs are unsupported")
            );

            var exception = Assert.Throws<NotSupportedException>(call);

            Assert.That(exception.Message, Does.Contain("Gaming Couch online multiplayer APIs are unsupported"));
            Assert.That(exception.Message, Does.Contain("GC_ENABLE_UNSUPPORTED_MULTIPLAYER"));
            Assert.That(emittedJson, Does.Contain("\"type\":\"gc.diagnostic\""));
            Assert.That(emittedJson, Does.Contain("\"name\":\"gc.api.unsupported_multiplayer_api\""));
            Assert.That(emittedJson, Does.Contain("\"severity\":\"error\""));
            Assert.That(emittedJson, Does.Contain("\"sourceArea\":\"api\""));
            Assert.That(emittedJson, Does.Contain("\"api\":\"" + apiName + "\""));
            Assert.That(emittedJson, Does.Contain("\"optInDefine\":\"GC_ENABLE_UNSUPPORTED_MULTIPLAYER\""));
            Assert.That(emittedJson, Does.Contain("\"supportStatus\":\"unsupported_temporary_internal_migration\""));
        }
        finally
        {
            GCRuntimeMessageOutput.RuntimeMessagesEmitted -= runtimeMessagesHandler;
        }
    }

    private void IgnoreWhenUnsupportedMultiplayerOptInIsActive()
    {
        var optInField = typeof(GamingCouch).GetField(
            "onlineMultiplayerSupport",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (optInField != null)
        {
            Assert.Ignore("Default unsupported multiplayer behavior is not active while GC_ENABLE_UNSUPPORTED_MULTIPLAYER is defined.");
        }
    }

    private static string FindPackageFile(string relativePath)
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouch).Assembly);
        if (packageInfo == null)
        {
            throw new FileNotFoundException("Could not locate Gaming Couch package.", relativePath);
        }

        return Path.Combine(packageInfo.resolvedPath, relativePath);
    }

    private static void AssertPluginRequiresUnsupportedMultiplayerOptIn(string relativePath)
    {
        var meta = File.ReadAllText(FindPackageFile(relativePath));

        Assert.That(meta, Does.Contain("defineConstraints:"));
        Assert.That(meta, Does.Contain("- GC_ENABLE_UNSUPPORTED_MULTIPLAYER"));
    }

    private static string ExtractJsonArrayBlock(string json, string propertyName)
    {
        var propertyIndex = json.IndexOf(propertyName, StringComparison.Ordinal);
        Assert.That(propertyIndex, Is.GreaterThanOrEqualTo(0), "Missing " + propertyName + " in asmdef.");

        var arrayStartIndex = json.IndexOf('[', propertyIndex);
        Assert.That(arrayStartIndex, Is.GreaterThanOrEqualTo(0), "Missing array for " + propertyName + ".");

        var arrayEndIndex = json.IndexOf(']', arrayStartIndex);
        Assert.That(arrayEndIndex, Is.GreaterThan(arrayStartIndex), "Unterminated array for " + propertyName + ".");

        return json.Substring(arrayStartIndex, arrayEndIndex - arrayStartIndex + 1);
    }
}
