using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GamingCouchEditorSeedParseTests
{
    [Test]
    public void TryParseSeedAcceptsPlainInteger()
    {
        var parsed = GCDevJsonLocalPlaySessionProvider.TryParseSeed("12345", out var value);

        Assert.That(parsed, Is.True);
        Assert.That(value, Is.EqualTo(12345));
    }

    [Test]
    public void TryParseSeedRejectsThousandsSeparator()
    {
        var parsed = GCDevJsonLocalPlaySessionProvider.TryParseSeed("1,000", out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void TryParseSeedRejectsSurroundingWhitespace()
    {
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed(" 5", out _), Is.False);
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("5 ", out _), Is.False);
    }

    [Test]
    public void TryParseSeedRejectsLeadingSign()
    {
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("+5", out _), Is.False);
        Assert.That(GCDevJsonLocalPlaySessionProvider.TryParseSeed("-5", out _), Is.False);
    }

    // The seed rule the file itself is held to (ADR 0010): "random", or an integer string inside
    // MinSeed..MaxSeed. TryParseSeed above is only the integer half of it.
    [TestCase("random")]
    [TestCase("1")]
    [TestCase("12345")]
    [TestCase("999999")]
    public void SeedRuleAcceptsRandomAndTheWholeIntegerRange(string seed)
    {
        var validation = GCDevJsonValidation.ValidateData(CreateDevJsonWithSeed(seed));

        Assert.That(FindIssue(validation, GCDevJsonIssueCode.InvalidSeed), Is.Null);
        Assert.That(validation.IsValid, Is.True);
    }

    [TestCase("0")]
    [TestCase("1000000")]
    [TestCase("")]
    [TestCase((string)null)]
    [TestCase("Random")]
    [TestCase("random ")]
    [TestCase("1e3")]
    public void SeedRuleRaisesInvalidSeedForEverythingElse(string seed)
    {
        var validation = GCDevJsonValidation.ValidateData(CreateDevJsonWithSeed(seed));
        var issue = FindIssue(validation, GCDevJsonIssueCode.InvalidSeed);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(issue, Is.Not.Null, "no InvalidSeed issue was raised for seed: " + seed);
        Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Error));
        Assert.That(issue.message, Does.Contain("\"random\""));
        Assert.That(issue.message, Does.Contain("999999"));
    }

    // Valid in every field but the seed, so an InvalidSeed issue can only come from the seed.
    private static GCDevJsonFile CreateDevJsonWithSeed(string seed)
    {
        var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
        for (var index = 0; index < seats.Length; index++)
        {
            seats[index] = new GCDevJsonSeat("P" + (index + 1), index == 0, false);
        }

        return new GCDevJsonFile("duel", seed, seats);
    }

    private static GCDevJsonIssue FindIssue(GCDevJsonValidationResult validation, GCDevJsonIssueCode code)
    {
        if (validation == null || validation.issues == null)
        {
            return null;
        }

        for (var index = 0; index < validation.issues.Length; index++)
        {
            if (validation.issues[index] != null && validation.issues[index].code == code)
            {
                return validation.issues[index];
            }
        }

        return null;
    }
}
