using System.Linq;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;

// Seat numbers are what the developer reads in DevApp's run configuration. Game-facing player
// indices are a seed-dependent permutation of them, so anything that follows what the developer can
// see has to go through the seat number rather than the player index.
public sealed class GCSeatNumberingTests
{
    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 1.0);
        GCRuntimeMessageOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
    }

    [TearDown]
    public void TearDown()
    {
        GCDevAppRuntimeOutputSettings.ResetForTests();
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
    }

    [Test]
    public void TheDefaultSeatIsSeatOneWhateverTheSeed()
    {
        var resolvedPlayerIndices = new int[SeedsThatShuffleDifferently.Length];

        for (var index = 0; index < SeedsThatShuffleDifferently.Length; index++)
        {
            var seatIdentities = CreateRun(SeedsThatShuffleDifferently[index]);

            Assert.That(
                GCSeatNumbering.ResolveDefaultSeatNumber(seatIdentities),
                Is.EqualTo(1),
                "the default seat moved under seed " + SeedsThatShuffleDifferently[index]
            );
            Assert.That(
                GCSeatNumbering.TryGetPlayerIndex(seatIdentities, 1, out var playerIndex),
                Is.True,
                "seat 1 did not resolve under seed " + SeedsThatShuffleDifferently[index]
            );
            Assert.That(seatIdentities[playerIndex].sourceSeatIndex, Is.EqualTo(1));
            Assert.That(seatIdentities[playerIndex].playerColor, Is.EqualTo(GCPlayerColor.blue));
            resolvedPlayerIndices[index] = playerIndex;
        }

        // Without this the test could pass on a mapping that never shuffles at all.
        Assert.That(
            resolvedPlayerIndices.Distinct().Count(),
            Is.GreaterThan(1),
            "the seeds chosen do not move seat 1 between player indices, so this proves nothing"
        );
    }

    [Test]
    public void EverySeatNumberResolvesToThePlayerIndexDevAppShowsForIt()
    {
        var seatIdentities = CreateRun(111);
        var snapshot = GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
            "run-1",
            true,
            seatIdentities,
            false,
            1.0f
        );

        Assert.That(snapshot.seats, Has.Length.EqualTo(seatIdentities.Length));
        foreach (var seat in snapshot.seats)
        {
            Assert.That(
                GCSeatNumbering.TryGetPlayerIndex(seatIdentities, seat.seatIndex, out var playerIndex),
                Is.True,
                "seat " + seat.seatIndex + " is shown in DevApp but does not resolve"
            );
            Assert.That(playerIndex, Is.EqualTo(seat.playerIndex), "seat " + seat.seatIndex);
        }
    }

    [Test]
    public void ABotSeatResolvesLikeAnyOtherSeat()
    {
        var seatIdentities = CreateRun(111);

        Assert.That(GCSeatNumbering.TryGetPlayerIndex(seatIdentities, 3, out var playerIndex), Is.True);
        Assert.That(seatIdentities[playerIndex].playerType, Is.EqualTo(GCPlayerType.bot));
    }

    [Test]
    public void ASeatNumberOutsideTheRunDoesNotResolve()
    {
        var seatIdentities = CreateRun(111);

        Assert.That(GCSeatNumbering.TryGetPlayerIndex(seatIdentities, 4, out _), Is.False);
        Assert.That(GCSeatNumbering.TryGetPlayerIndex(seatIdentities, 0, out _), Is.False);
    }

    // A seat keeps its number when it is disabled in DevApp, so a run need not contain seat 1. The
    // keyboard has to land on a live seat anyway, or it drives nothing and says nothing.
    [Test]
    public void TheDefaultSeatFallsToTheLowestSeatTheRunHas()
    {
        var seatIdentities = GCActiveRunProjection.Create(
            CreatePlayOptions(111, GCPlayerType.player, GCPlayerType.bot),
            CreateSeatIdentities(
                (3, GCPlayerType.player, GCPlayerColor.brown),
                (5, GCPlayerType.bot, GCPlayerColor.green)
            )
        ).MappedSeatIdentities;

        var defaultSeatNumber = GCSeatNumbering.ResolveDefaultSeatNumber(seatIdentities);

        Assert.That(defaultSeatNumber, Is.EqualTo(3));
        Assert.That(GCSeatNumbering.TryGetPlayerIndex(seatIdentities, defaultSeatNumber, out _), Is.True);
    }

    // A hosted-shaped run carries no local seat index, and DevApp numbers those seats by position.
    [Test]
    public void SeatsWithoutALocalSeatIndexAreNumberedByPosition()
    {
        var playOptions = CreatePlayOptions(
            111,
            GCPlayerType.player,
            GCPlayerType.player,
            GCPlayerType.bot
        );
        playOptions.usesProvidedPlayerIndexMapping = true;
        var seatIdentities = GCActiveRunProjection.Create(playOptions).MappedSeatIdentities;

        for (var playerIndex = 0; playerIndex < seatIdentities.Length; playerIndex++)
        {
            Assert.That(
                GCSeatNumbering.TryGetPlayerIndex(seatIdentities, playerIndex + 1, out var resolved),
                Is.True
            );
            Assert.That(resolved, Is.EqualTo(playerIndex));
        }
    }

    private static readonly int[] SeedsThatShuffleDifferently = { 111, 222, 333, 4242 };

    private static GCSeatIdentity[] CreateRun(int seed)
    {
        return GCActiveRunProjection.Create(
            CreatePlayOptions(seed, GCPlayerType.player, GCPlayerType.player, GCPlayerType.bot),
            CreateSeatIdentities(
                (1, GCPlayerType.player, GCPlayerColor.blue),
                (2, GCPlayerType.player, GCPlayerColor.green),
                (3, GCPlayerType.bot, GCPlayerColor.brown)
            )
        ).MappedSeatIdentities;
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

    private static GCSeatIdentity[] CreateSeatIdentities(
        params (int sourceSeatIndex, GCPlayerType playerType, GCPlayerColor playerColor)[] source
    )
    {
        var identities = new GCSeatIdentity[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            identities[index] = new GCSeatIdentity
            {
                sourceSeatIndex = source[index].sourceSeatIndex,
                stableKey = source[index].sourceSeatIndex.ToString(),
                label = "Seat " + source[index].sourceSeatIndex,
                playerType = source[index].playerType,
                playerColor = source[index].playerColor,
            };
        }

        return identities;
    }
}
