using System;
using System.Collections;
using System.Reflection;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;

public sealed class GCDevAppIntegrationReconnectTests
{
    [Test]
    public void ConnectReEnablesReconnectAfterManualDisconnect()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            integration.Disconnect();
            Assert.That(GetShouldReconnect(integration), Is.False);

            SetIsConnecting(integration, true);
            integration.Connect();

            Assert.That(GetShouldReconnect(integration), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ScheduledReconnectStopsWhenReconnectIsSuppressedDuringDelay()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect suppression test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            var reconnectRoutine = InvokeScheduleReconnect(integration);
            Assert.That(reconnectRoutine.MoveNext(), Is.True);
            Assert.That(reconnectRoutine.Current, Is.TypeOf<WaitForSeconds>());

            integration.Disconnect();

            Assert.That(reconnectRoutine.MoveNext(), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ScheduledReconnectUsesConnectGuardWhenAlreadyConnecting()
    {
        var gameObject = new GameObject("GCDevAppIntegration reconnect stale schedule test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            SetIsConnecting(integration, true);
            var reconnectRoutine = InvokeScheduleReconnect(integration);

            Assert.That(reconnectRoutine.MoveNext(), Is.True);
            Assert.That(reconnectRoutine.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(reconnectRoutine.MoveNext(), Is.False);
            Assert.That(GetShouldReconnect(integration), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void EnabledIntegrationSubscribesScreenSpaceOutputForDevAppPublishing()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 0);
        var gameObject = new GameObject("GCDevAppIntegration screen-space hook test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            if (HasScreenSpaceHandler(integration))
            {
                InvokePrivateMethod(integration, "OnDisable");
            }

            Assert.That(HasScreenSpaceHandler(integration), Is.False);

            InvokePrivateMethod(integration, "OnEnable");

            Assert.That(HasScreenSpaceHandler(integration), Is.True, DescribeScreenSpaceHandlers());

            InvokePrivateMethod(integration, "OnDisable");

            Assert.That(HasScreenSpaceHandler(integration), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            GCRuntimeMessageOutput.ResetForTests(null);
        }
    }

    [Test]
    public void RunStartMintsAFreshRunId()
    {
        // The DevApp refuses a second game-over for a runId it has already accepted, so every run
        // -- restart included -- has to arrive under a new one.
        GCActiveRunProjection.Create(CreateSinglePlayerPlayOptions());
        var firstRunId = GCDevAppRunIdentity.CurrentRunId;

        GCActiveRunProjection.Create(CreateSinglePlayerPlayOptions());

        Assert.That(firstRunId, Is.Not.Null.And.Not.Empty);
        Assert.That(GCDevAppRunIdentity.CurrentRunId, Is.Not.Null.And.Not.Empty);
        Assert.That(GCDevAppRunIdentity.CurrentRunId, Is.Not.EqualTo(firstRunId));
    }

    [Test]
    public void ClosingTheSocketKeepsTheRunIdOfTheLiveRun()
    {
        var gameObject = new GameObject("GCDevAppIntegration run id test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            GCActiveRunProjection.Create(CreateSinglePlayerPlayOptions());
            var runId = GCDevAppRunIdentity.CurrentRunId;

            InvokePrivateMethod(integration, "CloseWebSocket");

            Assert.That(GCDevAppRunIdentity.CurrentRunId, Is.EqualTo(runId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SendPumpRetiresWhenTheConnectionEpochMoves()
    {
        var gameObject = new GameObject("GCDevAppIntegration send pump test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            var pumpRoutine = InvokeSendPump(integration, epoch: -1);

            Assert.That(pumpRoutine.MoveNext(), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void RuntimeOutputIsNotQueuedWhileTheSocketIsDown()
    {
        var gameObject = new GameObject("GCDevAppIntegration outbound queue test");
        gameObject.SetActive(false);
        var integration = gameObject.AddComponent<GCDevAppIntegration>();

        try
        {
            // An active run publishes output every frame, so the queue must not accumulate it
            // while there is no socket to drain it.
            GCActiveRunProjection.Create(CreateSinglePlayerPlayOptions());
            InvokePrivateMethod(integration, "PublishRuntimeMessages", "{\"type\":\"runtime_messages\"}");

            Assert.That(GetOutboundQueueCount(integration), Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static GCPlayOptions CreateSinglePlayerPlayOptions()
    {
        return new GCPlayOptions
        {
            seed = 123,
            players = new[]
            {
                new GCPlayerOptions
                {
                    type = GCPlayerType.player.ToString(),
                    color = GCPlayerColor.blue.ToString(),
                },
            },
        };
    }

    private static int GetOutboundQueueCount(GCDevAppIntegration integration)
    {
        return ((ICollection)GetPrivateField("outboundQueue").GetValue(integration)).Count;
    }

    private static bool GetShouldReconnect(GCDevAppIntegration integration)
    {
        return (bool)GetPrivateField("shouldReconnect").GetValue(integration);
    }

    private static void SetIsConnecting(GCDevAppIntegration integration, bool value)
    {
        GetPrivateField("isConnecting").SetValue(integration, value);
    }

    private static IEnumerator InvokeScheduleReconnect(GCDevAppIntegration integration)
    {
        return (IEnumerator)GetPrivateMethod("ScheduleReconnect").Invoke(integration, null);
    }

    private static IEnumerator InvokeSendPump(GCDevAppIntegration integration, int epoch)
    {
        return (IEnumerator)GetPrivateMethod("PumpOutboundMessages").Invoke(integration, new object[] { epoch });
    }

    private static void InvokePrivateMethod(GCDevAppIntegration integration, string methodName, params object[] arguments)
    {
        GetPrivateMethod(methodName).Invoke(integration, arguments.Length == 0 ? null : arguments);
    }

    private static Delegate[] GetScreenSpaceHandlers()
    {
        var field = typeof(GCRuntimeScreenSpaceOutput).GetField(
            "ScreenSpaceEmitted",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, "Expected ScreenSpaceEmitted backing field to exist.");

        var handler = field.GetValue(null) as MulticastDelegate;
        return handler?.GetInvocationList() ?? Array.Empty<Delegate>();
    }

    private static bool HasScreenSpaceHandler(GCDevAppIntegration integration)
    {
        return Array.Exists(
            GetScreenSpaceHandlers(),
            handler =>
                ReferenceEquals(handler.Target, integration) &&
                handler.Method.Name == "PublishScreenSpace"
        );
    }

    private static string DescribeScreenSpaceHandlers()
    {
        var handlers = GetScreenSpaceHandlers();
        if (handlers.Length == 0)
        {
            return "No screen-space handlers are subscribed.";
        }

        return string.Join(
            ", ",
            Array.ConvertAll(
                handlers,
                handler => $"{handler.Target?.GetType().FullName ?? "<static>"}.{handler.Method.Name}"
            )
        );
    }

    private static FieldInfo GetPrivateField(string fieldName)
    {
        var field = typeof(GCDevAppIntegration).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Expected field {fieldName} to exist.");
        return field;
    }

    private static MethodInfo GetPrivateMethod(string methodName)
    {
        var method = typeof(GCDevAppIntegration).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null, $"Expected method {methodName} to exist.");
        return method;
    }
}
