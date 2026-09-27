using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;
using UnityEngine;

public sealed class GamingCouchEditorInputTests
{
    [Test]
    public void ControllerInputsExposesOnlyCurrentInputShortcuts()
    {
        var propertyNames = typeof(GCControllerInputs)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name);

        Assert.That(
            propertyNames,
            Is.EquivalentTo(new[] { "RawData", "leftX", "leftY", "primary", "secondary", "alt" })
        );
    }

    [Test]
    public void ControllerInputsDataExposesOnlyCurrentInputFields()
    {
        var fieldNames = typeof(GCControllerInputsData)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name);

        Assert.That(
            fieldNames,
            Is.EquivalentTo(new[] { "a0", "a1", "b0", "b1", "b2" })
        );
    }

    [Test]
    public void ControllerInputsReadsLeftStickAxesDirectly()
    {
        var inputs = new GCControllerInputs(new GCControllerInputsData
        {
            a0 = 0.25f,
            a1 = -0.5f,
        });

        Assert.That(inputs.leftX, Is.EqualTo(0.25f));
        Assert.That(inputs.leftY, Is.EqualTo(-0.5f));
    }

    [Test]
    public void KeyboardInputWinsOverExternalInputForEditorControlledPlayer()
    {
        var externalInputs = new GCControllerInputsData
        {
            a0 = -0.75f,
            a1 = 0.25f,
            b0 = 1,
        };
        var keyboardInputs = new GCControllerInputsData
        {
            a0 = 1.0f,
            a1 = 0.0f,
            b0 = 0,
        };

        var resolved = GamingCouch.ResolveEditorInputs(keyboardInputs, externalInputs, 0.15f);

        Assert.That(resolved.a0, Is.EqualTo(1.0f));
        Assert.That(resolved.a1, Is.EqualTo(0.0f));
        Assert.That(resolved.b0, Is.EqualTo(0));
    }

    [Test]
    public void ExternalInputStillAppliesWhenKeyboardInputIsIdle()
    {
        var externalInputs = new GCControllerInputsData
        {
            a0 = -0.75f,
            a1 = 0.25f,
            b0 = 1,
        };
        var keyboardInputs = new GCControllerInputsData();

        var resolved = GamingCouch.ResolveEditorInputs(keyboardInputs, externalInputs, 0.15f);

        Assert.That(resolved.a0, Is.EqualTo(-0.75f));
        Assert.That(resolved.a1, Is.EqualTo(0.25f));
        Assert.That(resolved.b0, Is.EqualTo(1));
    }

    [Test]
    public void ReleasedKeyboardInputDoesNotBecomeExternalFallbackInput()
    {
        const int playerIndex = 1;
        var gameFacingInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = 1.0f,
                b0 = 1,
            }),
        };
        var externalInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>();
        var releasedKeyboardInputs = new GCControllerInputsData();

        GamingCouch.ApplyEditorInputsForPlayer(
            playerIndex,
            releasedKeyboardInputs,
            gameFacingInputsByPlayerIndex,
            externalInputsByPlayerIndex,
            0.15f
        );

        var resolved = gameFacingInputsByPlayerIndex[playerIndex].RawData;
        Assert.That(resolved.a0, Is.EqualTo(0.0f));
        Assert.That(resolved.a1, Is.EqualTo(0.0f));
        Assert.That(resolved.b0, Is.EqualTo(0));
        Assert.That(resolved.b1, Is.EqualTo(0));
    }

    [Test]
    public void ExternalInputFallbackUsesExternalInputCache()
    {
        const int playerIndex = 1;
        var gameFacingInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = 1.0f,
                b0 = 1,
            }),
        };
        var externalInputsByPlayerIndex = new Dictionary<int, GCControllerInputs>
        {
            [playerIndex] = new GCControllerInputs(new GCControllerInputsData
            {
                a0 = -0.75f,
                a1 = 0.25f,
                b1 = 1,
            }),
        };
        var idleKeyboardInputs = new GCControllerInputsData();

        GamingCouch.ApplyEditorInputsForPlayer(
            playerIndex,
            idleKeyboardInputs,
            gameFacingInputsByPlayerIndex,
            externalInputsByPlayerIndex,
            0.15f
        );

        var resolved = gameFacingInputsByPlayerIndex[playerIndex].RawData;
        Assert.That(resolved.a0, Is.EqualTo(-0.75f));
        Assert.That(resolved.a1, Is.EqualTo(0.25f));
        Assert.That(resolved.b0, Is.EqualTo(0));
        Assert.That(resolved.b1, Is.EqualTo(1));
    }

    [Test]
    public void DevAppInputApplyPreservesB2AndAcceptsNeutralRelease()
    {
        var gamingCouch = GamingCouchEditorTestSupport.CreateGamingCouch("GamingCouch devapp input test");
        try
        {
            SetPrivateField(
                gamingCouch,
                "playerIndexMapping",
                GCActiveRunProjection.Create(CreatePlayOptions(123, GCPlayerType.player)).PlayerIndexMapping
            );

            gamingCouch.ApplyExternalPlayerInput(
                0,
                new GCControllerInputsData
                {
                    a0 = 0.75f,
                    a1 = -0.5f,
                    b0 = 1,
                    b2 = 1,
                },
                "test_input"
            );

            var activeInputs = gamingCouch.GetInputsByPlayerIndex(0).RawData;
            Assert.That(activeInputs.a0, Is.EqualTo(0.75f));
            Assert.That(activeInputs.a1, Is.EqualTo(-0.5f));
            Assert.That(activeInputs.b0, Is.EqualTo(1));
            Assert.That(activeInputs.b2, Is.EqualTo(1));

            gamingCouch.ApplyExternalPlayerInput(0, new GCControllerInputsData(), "test_input");

            var neutralInputs = gamingCouch.GetInputsByPlayerIndex(0).RawData;
            Assert.That(neutralInputs.a0, Is.EqualTo(0f));
            Assert.That(neutralInputs.a1, Is.EqualTo(0f));
            Assert.That(neutralInputs.b0, Is.EqualTo(0));
            Assert.That(neutralInputs.b1, Is.EqualTo(0));
            Assert.That(neutralInputs.b2, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(gamingCouch.gameObject);
        }
    }

    [Test]
    public void TryParsePlayerInputMessageParsesIndexAndJson()
    {
        var parsed = GamingCouch.TryParsePlayerInputMessage("2|{\"a0\":1}", out var playerIndex, out var inputsJson);

        Assert.That(parsed, Is.True);
        Assert.That(playerIndex, Is.EqualTo(2));
        Assert.That(inputsJson, Is.EqualTo("{\"a0\":1}"));
    }

    [Test]
    public void TryParsePlayerInputMessageRejectsCultureSensitiveIndex()
    {
        // NumberStyles.None + InvariantCulture: no thousands separators, whitespace, or sign,
        // so a host locale can never change how the index is read.
        Assert.That(GamingCouch.TryParsePlayerInputMessage("1,000|{}", out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage(" 1|{}", out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage("+1|{}", out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage("-1|{}", out _, out _), Is.False);
    }

    [Test]
    public void TryParsePlayerInputMessageRejectsMalformedMessageInsteadOfThrowing()
    {
        Assert.That(GamingCouch.TryParsePlayerInputMessage(null, out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage("", out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage("2", out _, out _), Is.False);
        Assert.That(GamingCouch.TryParsePlayerInputMessage("abc|{}", out _, out _), Is.False);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static GCPlayOptions CreatePlayOptions(int seed, params GCPlayerType[] playerTypes)
    {
        var players = new GCPlayerOptions[playerTypes.Length];
        for (var index = 0; index < playerTypes.Length; index++)
        {
            players[index] = new GCPlayerOptions
            {
                playerIndex = index,
                type = playerTypes[index].ToString(),
                color = GCPlayerColor.blue.ToString(),
            };
        }

        return new GCPlayOptions
        {
            players = players,
            seed = seed,
        };
    }
}
