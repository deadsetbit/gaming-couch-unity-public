using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCActiveRunProjectionTests
{
    [TearDown]
    public void TearDown()
    {
        GCDevAppRuntimeOutputSettings.ResetForTests();
    }

    [Test]
    public void ProvidedPlayerIndexMappingCreatesGameFacingOptionsWithoutInventingSourceSeats()
    {
        var options = new GCPlayOptions
        {
            seed = 123,
            players = new[]
            {
                new GCPlayerOptions
                {
                    playerIndex = 1,
                    playerSeed = 333333,
                    type = GCPlayerType.player.ToString(),
                    color = GCPlayerColor.blue.ToString(),
                },
                new GCPlayerOptions
                {
                    playerIndex = 0,
                    playerSeed = 444444,
                    type = GCPlayerType.bot.ToString(),
                    color = GCPlayerColor.green.ToString(),
                },
            },
            usesProvidedPlayerIndexMapping = true,
        };

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(0).CapturedOrder, Is.EqualTo(1));
        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(0).StableKey, Is.EqualTo("0"));
        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(1).StableKey, Is.EqualTo("1"));
        Assert.That(projection.PlayerIndexMapping.TryGetPlayerIndexForSourceSeat(1, out _), Is.False);
        Assert.That(projection.GameFacingPlayOptions.seed, Is.EqualTo(123));
        Assert.That(projection.GameFacingPlayOptions.players[0].playerIndex, Is.EqualTo(0));
        Assert.That(projection.GameFacingPlayOptions.players[0].playerSeed, Is.EqualTo(444444));
        Assert.That(projection.GameFacingPlayOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[0].color, Is.EqualTo(GCPlayerColor.green.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].playerIndex, Is.EqualTo(1));
        Assert.That(projection.GameFacingPlayOptions.players[1].playerSeed, Is.EqualTo(333333));
        Assert.That(projection.GameFacingPlayOptions.players[1].type, Is.EqualTo(GCPlayerType.player.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));

        Assert.That(projection.MappedSeatIdentities[0].sourceSeatIndex, Is.EqualTo(0));
        Assert.That(projection.MappedSeatIdentities[0].stableKey, Is.EqualTo("0"));
        Assert.That(projection.MappedSeatIdentities[0].label, Is.Null);
        Assert.That(projection.MappedSeatIdentities[0].playerType, Is.EqualTo(GCPlayerType.bot));
        Assert.That(projection.MappedSeatIdentities[0].playerColor, Is.EqualTo(GCPlayerColor.green));
        Assert.That(projection.MappedSeatIdentities[1].sourceSeatIndex, Is.EqualTo(0));
        Assert.That(projection.MappedSeatIdentities[1].stableKey, Is.EqualTo("1"));
        Assert.That(projection.MappedSeatIdentities[1].label, Is.Null);
        Assert.That(projection.MappedSeatIdentities[1].playerType, Is.EqualTo(GCPlayerType.player));
        Assert.That(projection.MappedSeatIdentities[1].playerColor, Is.EqualTo(GCPlayerColor.blue));
    }

    [Test]
    public void ExplicitSeatIdentitiesProjectionKeepsPrivateMappingAndCopiesPlatformData()
    {
        var platformData = CreatePlatformData();
        var options = new GCPlayOptions
        {
            seed = 111,
            players = new[]
            {
                CreatePlayer(GCPlayerType.player, GCPlayerColor.blue),
                CreatePlayer(GCPlayerType.player, GCPlayerColor.green),
                CreatePlayer(GCPlayerType.bot, GCPlayerColor.brown),
            },
            runtimeOutput = new GCRuntimeOutputOptions
            {
                stateSnapshots = false,
                screenSpace = false,
            },
            platformData = platformData,
        };
        var seatIdentities = new[]
        {
            CreateSeatIdentity(1, "1", GCPlayerType.player, GCPlayerColor.blue),
            CreateSeatIdentity(3, "3", GCPlayerType.player, GCPlayerColor.green),
            CreateSeatIdentity(8, "8", GCPlayerType.bot, GCPlayerColor.brown),
        };

        var projection = GCActiveRunProjection.Create(options, seatIdentities);

        Assert.That(projection.PlayerIndexMapping.TryGetPlayerIndexForSourceSeat(8, out var playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(0));
        Assert.That(projection.PlayerIndexMapping.TryGetPlayerIndexForSourceSeat(1, out playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(1));
        Assert.That(projection.PlayerIndexMapping.TryGetPlayerIndexForSourceSeat(3, out playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(2));
        Assert.That(projection.MappedSeatIdentities[0].sourceSeatIndex, Is.EqualTo(8));
        Assert.That(projection.MappedSeatIdentities[1].sourceSeatIndex, Is.EqualTo(1));
        Assert.That(projection.MappedSeatIdentities[2].sourceSeatIndex, Is.EqualTo(3));
        Assert.That(projection.GameFacingPlayOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[0].color, Is.EqualTo(GCPlayerColor.brown.ToString()));
        Assert.That(projection.GameFacingPlayOptions.runtimeOutput.stateSnapshots, Is.False);
        Assert.That(projection.GameFacingPlayOptions.runtimeOutput.screenSpace, Is.False);
        Assert.That(projection.PlatformData, Is.SameAs(projection.GameFacingPlayOptions.platformData));
        Assert.That(projection.PlatformData, Is.Not.SameAs(platformData));

        platformData.game.key = "mutated";
        platformData.entries[0].entryKey = "mutated";
        platformData.playerColors.blue.@base[0] = 99;

        Assert.That(projection.PlatformData.game.key, Is.EqualTo("contract-game"));
        Assert.That(projection.PlatformData.entries[0].entryKey, Is.EqualTo("duel"));
        Assert.That(projection.PlatformData.playerColors.blue.@base, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void PlayersJsonProjectionPreservesPreMappedPlayerIndexOrder()
    {
        var options = GCPlayOptions.CreateFromJSON(
            "{\"seed\":424242,\"players\":[" +
            "{\"playerIndex\":1,\"playerSeed\":333333,\"type\":\"player\",\"color\":\"blue\"}," +
            "{\"playerIndex\":0,\"playerSeed\":444444,\"type\":\"bot\",\"color\":\"green\"}" +
            "]}"
        );

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(options.usesProvidedPlayerIndexMapping, Is.True);
        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(0).CapturedOrder, Is.EqualTo(1));
        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(0).StableKey, Is.EqualTo("0"));
        Assert.That(projection.PlayerIndexMapping.GetByPlayerIndex(1).StableKey, Is.EqualTo("1"));
        Assert.That(projection.GameFacingPlayOptions.players[0].playerIndex, Is.EqualTo(0));
        Assert.That(projection.GameFacingPlayOptions.players[0].playerSeed, Is.EqualTo(444444));
        Assert.That(projection.GameFacingPlayOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[0].color, Is.EqualTo(GCPlayerColor.green.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].playerIndex, Is.EqualTo(1));
        Assert.That(projection.GameFacingPlayOptions.players[1].playerSeed, Is.EqualTo(333333));
        Assert.That(projection.GameFacingPlayOptions.players[1].type, Is.EqualTo(GCPlayerType.player.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));
    }

    [Test]
    public void ProjectionRejectsNonCurrentRosterProperty()
    {
        var exception = Assert.Throws<System.ArgumentException>(
            () => GCPlayOptions.CreateFromJSON(
                "{\"seed\":424242,\"roster\":[" +
                "{\"playerIndex\":1,\"playerSeed\":333333,\"type\":\"player\",\"color\":\"blue\"}," +
                "{\"playerIndex\":0,\"playerSeed\":444444,\"type\":\"bot\",\"color\":\"green\"}" +
                "]}"
            )
        );

        Assert.That(exception.Message, Does.Contain("players[] entries with playerIndex"));
    }

    [Test]
    public void ProjectionAppliesEditorRuntimeOutputOverride()
    {
        var options = new GCPlayOptions
        {
            seed = 123,
            players = new[]
            {
                CreatePlayer(GCPlayerType.player, GCPlayerColor.blue),
            },
            runtimeOutput = new GCRuntimeOutputOptions
            {
                runtimeLogCapture = GCRuntimeUnityLogCaptureMode.Full,
            },
        };
        GCDevAppRuntimeOutputSettings.SetRuntimeLogCaptureMode(GCRuntimeUnityLogCaptureMode.WarningAndError);

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(
            projection.GameFacingPlayOptions.runtimeOutput.runtimeLogCapture,
            Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError)
        );
    }

    private static GCPlayerOptions CreatePlayer(GCPlayerType playerType, GCPlayerColor playerColor)
    {
        return new GCPlayerOptions
        {
            type = playerType.ToString(),
            color = playerColor.ToString(),
        };
    }

    private static GCSeatIdentity CreateSeatIdentity(
        int sourceSeatIndex,
        string stableKey,
        GCPlayerType playerType,
        GCPlayerColor playerColor
    )
    {
        return new GCSeatIdentity
        {
            sourceSeatIndex = sourceSeatIndex,
            stableKey = stableKey,
            label = "Seat " + sourceSeatIndex,
            playerType = playerType,
            playerColor = playerColor,
        };
    }

    private static GCPlatformRuntimeView CreatePlatformData()
    {
        var playerColors = GCPlatformRuntimePlayerColors.CreateDefault();
        playerColors.blue = new GCPlatformRuntimePlayerColor(
            new[] { 1, 2, 3 },
            new[] { 4, 5, 6 },
            new[] { 7, 8, 9 }
        );

        return GCPlatformRuntimeView.CreateValid(
            GCPlatformRuntimeSource.Valid(1, "/tmp/gc.platform.json"),
            new GCPlatformRuntimeGame("contract-game", "Contract Game"),
            new GCPlatformRuntimePlatform("unity"),
            "duel",
            new[]
            {
                new GCPlatformRuntimeEntry("duel", "Duel", 1, 2, true),
            },
            playerColors
        );
    }
}
