using DSB.GC;
using NUnit.Framework;

public sealed class GCPlayerSeedTests
{
    // The exact seeds below are cross-checked against ExpectedSeed(), a fully independent
    // re-derivation of the trim + lower-invariant + FNV-1a/32 + (hash % 999999 + 1) pipeline that
    // never calls into GCPlayerSeed, so the assertions pin the derivation rather than restate it.
    //
    // Unicode normalization is intentionally NOT pinned: on Unity's globalization-invariant runtime
    // the String.Normalize(FormKC) in GCPlayerSeed.NormalizePlayerName is not a dependable
    // compatibility fold, so exact folding is not a stable contract worth asserting. The oracle
    // therefore omits normalization and only ASCII inputs (where normalization is a no-op
    // regardless) are pinned to exact values.

    [Test]
    public void FromPlayerNameMatchesRequiredExactSeedFixture()
    {
        Assert.That(GCPlayerSeed.ComputeFnv1A32("alice"), Is.EqualTo(2267157479u));
        Assert.That(GCPlayerSeed.ComputeFnv1A32("bob"), Is.EqualTo(2261164244u));
        Assert.That(GCPlayerSeed.ComputeFnv1A32("player-a"), Is.EqualTo(2775468930u));

        Assert.That(GCPlayerSeed.FromPlayerName("Alice"), Is.EqualTo(159747));
        Assert.That(GCPlayerSeed.FromPlayerName("Bob"), Is.EqualTo(166506));
        Assert.That(GCPlayerSeed.FromPlayerName("player-a"), Is.EqualTo(471706));

        // Same values, re-derived independently of GCPlayerSeed.
        Assert.That(GCPlayerSeed.FromPlayerName("Alice"), Is.EqualTo(ExpectedSeed("Alice")));
        Assert.That(GCPlayerSeed.FromPlayerName("Bob"), Is.EqualTo(ExpectedSeed("Bob")));
        Assert.That(GCPlayerSeed.FromPlayerName("player-a"), Is.EqualTo(ExpectedSeed("player-a")));
    }

    [Test]
    public void FromPlayerNameTrimsWhitespaceAndLowersCasingBeforeHashing()
    {
        // "  Alice  " and "ALICE" both normalize to "alice", so both collapse onto the
        // exact seed pinned for "Alice".
        Assert.That(GCPlayerSeed.FromPlayerName("  Alice  "), Is.EqualTo(159747));
        Assert.That(GCPlayerSeed.FromPlayerName("ALICE"), Is.EqualTo(159747));
        Assert.That(GCPlayerSeed.FromPlayerName("  Alice  "), Is.EqualTo(GCPlayerSeed.FromPlayerName("Alice")));
        Assert.That(GCPlayerSeed.FromPlayerName("ALICE"), Is.EqualTo(GCPlayerSeed.FromPlayerName("Alice")));
        Assert.That(GCPlayerSeed.FromPlayerName("  Alice  "), Is.EqualTo(ExpectedSeed("  Alice  ")));
    }

    [Test]
    public void FromPlayerNameIsDeterministicForRepeatedInput()
    {
        var first = GCPlayerSeed.FromPlayerName("Alice");
        Assert.That(GCPlayerSeed.FromPlayerName("Alice"), Is.EqualTo(first));
        Assert.That(GCPlayerSeed.FromPlayerName("Alice"), Is.EqualTo(first));
    }

    [TestCase("Alice")]
    [TestCase("player-a")]
    [TestCase("ＡＢＣ")]
    [TestCase("   ")]
    [TestCase("")]
    [TestCase(null)]
    public void FromPlayerNameStaysWithinSeedRange(string playerName)
    {
        var seed = GCPlayerSeed.FromPlayerName(playerName);
        Assert.That(seed, Is.GreaterThanOrEqualTo(GCPlayerSeed.MinSeed));
        Assert.That(seed, Is.LessThanOrEqualTo(GCPlayerSeed.MaxSeed));
    }

    [TestCase(1)]
    [TestCase(500000)]
    [TestCase(999999)]
    public void NormalizeOrFallbackReturnsInRangeSeedUnchanged(int playerSeed)
    {
        // A valid provided seed wins outright; name and fallback index are ignored.
        Assert.That(GCPlayerSeed.NormalizeOrFallback(playerSeed, "Alice", 3), Is.EqualTo(playerSeed));
    }

    [TestCase(0)]
    [TestCase(-5)]
    [TestCase(1000000)]
    public void NormalizeOrFallbackDerivesFromPlayerNameWhenSeedOutOfRange(int outOfRangeSeed)
    {
        // Out-of-range seed with a usable name falls through to the name derivation,
        // matching FromPlayerName("Alice").
        Assert.That(GCPlayerSeed.NormalizeOrFallback(outOfRangeSeed, "Alice", 3), Is.EqualTo(159747));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void NormalizeOrFallbackUsesFallbackIndexWhenSeedOutOfRangeAndNameBlank(string blankName)
    {
        // With no valid seed and a blank/whitespace name, the fallback index seeds the
        // value as (index % 999999) + 1: index 5 -> 6.
        Assert.That(GCPlayerSeed.NormalizeOrFallback(0, blankName, 5), Is.EqualTo(6));
    }

    [Test]
    public void NormalizeOrFallbackClampsNegativeFallbackIndexToBaseSeed()
    {
        // Negative fallback indices clamp to 0 before ToSeed, yielding MinSeed (1);
        // index 0 lands on the same base seed, index 3 -> 4.
        Assert.That(GCPlayerSeed.NormalizeOrFallback(0, null, -7), Is.EqualTo(GCPlayerSeed.MinSeed));
        Assert.That(GCPlayerSeed.NormalizeOrFallback(0, null, 0), Is.EqualTo(GCPlayerSeed.MinSeed));
        Assert.That(GCPlayerSeed.NormalizeOrFallback(0, "", 3), Is.EqualTo(4));
    }

    // Independent re-derivation of the seed (no call into GCPlayerSeed): trim, lower-invariant,
    // FNV-1a/32 over UTF-8, then % 999999 + 1. Deliberately does NOT Unicode-normalize -- Unity's
    // globalization-invariant runtime performs no folding, so this mirrors production's effective
    // behavior. Used to cross-check the pinned exact values.
    private static int ExpectedSeed(string name)
    {
        var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;
        var hash = offsetBasis;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(normalized))
        {
            hash ^= b;
            hash = unchecked(hash * prime);
        }

        return (int)(hash % 999999u) + 1;
    }
}
