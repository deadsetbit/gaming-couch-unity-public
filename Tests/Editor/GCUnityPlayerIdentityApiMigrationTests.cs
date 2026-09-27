using System;
using System.Linq;
using System.Reflection;
using DSB.GC;
using NUnit.Framework;

public sealed class GCUnityPlayerIdentityApiMigrationTests
{
    [Test]
    public void PlayerOptionsExposeOnlyGameFacingIdentityFields()
    {
        var fieldNames = typeof(GCPlayerOptions)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.That(fieldNames, Is.EqualTo(new[] { "color", "playerIndex", "playerSeed", "type" }));
        Assert.That(typeof(GCPlayOptions).GetField("players").FieldType, Is.EqualTo(typeof(GCPlayerOptions[])));
    }

    [Test]
    public void LegacyPlayerOptionsTypeIsNotPartOfPublicApi()
    {
        var removedType = typeof(GCPlayerOptions).Assembly.GetType("DSB.GC.GC" + "Active" + "PlayerOptions");
        Assert.That(removedType, Is.Null);
    }

    [Test]
    public void RemovedIdentityApisFailAtSourceWithMigrationGuidance()
    {
        AssertObsoleteError(
            typeof(GCPlayer).GetProperty("Id"),
            "GCPlayer.Index"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetProperty("PlayerName"),
            "Player names are platform-owned"
        );
        AssertObsoleteError(
            typeof(GamingCouch).GetMethod("GetInputsByPlayerId"),
            "GetInputsByPlayerIndex"
        );
        AssertObsoleteError(
            typeof(GCPlayerStore<GCPlayer>).GetMethod("GetPlayerById"),
            "GetPlayerByIndex"
        );
    }

    [Test]
    public void IndexNamedApisUsePlayerIndexParameters()
    {
        Assert.That(
            typeof(GamingCouch).GetMethod("GetInputsByPlayerIndex").GetParameters()[0].Name,
            Is.EqualTo("playerIndex")
        );
        Assert.That(
            typeof(GCPlayerStore<GCPlayer>).GetMethod("GetPlayerByIndex").GetParameters()[0].Name,
            Is.EqualTo("playerIndex")
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).IsPublic,
            Is.False
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).GetField("playerIndex", BindingFlags.Instance | BindingFlags.Public),
            Is.Not.Null
        );
        Assert.That(
            typeof(GCPlayerSetupOptions).GetField("index", BindingFlags.Instance | BindingFlags.Public),
            Is.Null
        );
    }

    private static void AssertObsoleteError(MemberInfo member, string expectedGuidance)
    {
        Assert.That(member, Is.Not.Null);

        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null);
        Assert.That(obsolete.IsError, Is.True);
        Assert.That(obsolete.Message, Does.Contain(expectedGuidance));
    }
}
