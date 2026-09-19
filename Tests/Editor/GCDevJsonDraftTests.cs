using DSB.GC.Dev;
using NUnit.Framework;

// The inspector's editable form of gc.dev.json. Its conversion is the only place the file's seed
// and seat names are shaped on the way back out, so the round trip is what has to hold.
public sealed class GCDevJsonDraftTests
{
    [Test]
    public void RandomSeedFileStaysRandomThroughTheRoundTrip()
    {
        var draft = GCDevJsonDraft.FromFile(CreateFile(GCDevJsonFile.RandomSeed));

        Assert.That(draft.usesRandomSeed, Is.True);
        Assert.That(draft.ToFile().seed, Is.EqualTo(GCDevJsonFile.RandomSeed));
    }

    [Test]
    public void FixedSeedFileKeepsItsIntegerThroughTheRoundTrip()
    {
        var draft = GCDevJsonDraft.FromFile(CreateFile("54321"));

        Assert.That(draft.usesRandomSeed, Is.False);
        Assert.That(draft.fixedSeed, Is.EqualTo(54321));
        Assert.That(draft.ToFile().seed, Is.EqualTo("54321"));
    }

    // A seed the draft cannot parse must not fall through as 0: the seed rule rejects 0, so the
    // draft would hand the store a file it refuses to write.
    [TestCase("not-a-number")]
    [TestCase("")]
    [TestCase("-5")]
    [TestCase("1000000000000")]
    public void UnparseableSeedFallsBackToASeedTheRuleAccepts(string seed)
    {
        var draft = GCDevJsonDraft.FromFile(CreateFile(seed));

        Assert.That(draft.usesRandomSeed, Is.False);
        Assert.That(draft.fixedSeed, Is.EqualTo(GCDevJsonFile.MinSeed));
        Assert.That(GCDevJsonValidation.ValidateData(draft.ToFile()).IsValid, Is.True);
    }

    [Test]
    public void SeatFieldsSurviveTheRoundTripAndNamesAreNormalized()
    {
        var draft = GCDevJsonDraft.FromFile(CreateFile("12345"));
        draft.seats[1].name = "  Ada  ";
        draft.seats[2].name = null;
        draft.seats[3].isBot = true;

        var file = draft.ToFile();

        Assert.That(file.entryKey, Is.EqualTo("duel"));
        Assert.That(file.devVersion, Is.EqualTo(GCDevJsonFile.SupportedDevVersion));
        Assert.That(file.seats, Has.Length.EqualTo(GCDevJsonFile.SeatCount));
        Assert.That(file.seats[1].name, Is.EqualTo("Ada"));
        Assert.That(file.seats[2].name, Is.EqualTo(string.Empty));
        Assert.That(file.seats[3].isBot, Is.True);
        Assert.That(file.seats[0].enabled, Is.True);
        Assert.That(file.seats[1].enabled, Is.False);
    }

    private static GCDevJsonFile CreateFile(string seed)
    {
        var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
        for (var index = 0; index < seats.Length; index++)
        {
            seats[index] = new GCDevJsonSeat("P" + (index + 1), index == 0, false);
        }

        return new GCDevJsonFile("duel", seed, seats);
    }
}
