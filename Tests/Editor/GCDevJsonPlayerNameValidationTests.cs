using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCDevJsonPlayerNameValidationTests
{
    [Test]
    public void WhitespacePaddedNameValidWhenTrimmedIsAcceptedConsistentlyWithMinLengthPolicy()
    {
        // Trimmed content ("12345678") is exactly PlayerNameMaxLength (8) and thus valid,
        // but the raw string is longer than the max because of surrounding whitespace.
        // Min-length already checks the trimmed length, so max-length must too or the
        // rule is asymmetric: this padded name would be rejected on raw length alone.
        var paddedButValid = "  12345678  ";

        Assert.That(paddedButValid.Trim().Length, Is.EqualTo(GCDevJsonFile.PlayerNameMaxLength));
        Assert.That(paddedButValid.Length, Is.GreaterThan(GCDevJsonFile.PlayerNameMaxLength));
        Assert.That(GCDevJsonValidation.IsValidPlayerName(paddedButValid), Is.True);
    }

    [Test]
    public void NameWhoseTrimmedContentExceedsMaxLengthIsRejected()
    {
        // No padding: the actual content is over the limit and must be rejected under
        // either policy, guarding against trimming that would accept over-length names.
        var tooLong = new string('a', GCDevJsonFile.PlayerNameMaxLength + 1);

        Assert.That(GCDevJsonValidation.IsValidPlayerName(tooLong), Is.False);
    }

    [Test]
    public void WhitespaceOnlyNameIsRejectedConsistentlyOnTrimmedLength()
    {
        // Raw length passes the max, but trimmed length is below the min. Both bounds
        // now use the trimmed length, so this is rejected the same way as before.
        var whitespaceOnly = "   ";

        Assert.That(whitespaceOnly.Length, Is.LessThanOrEqualTo(GCDevJsonFile.PlayerNameMaxLength));
        Assert.That(whitespaceOnly.Trim().Length, Is.LessThan(GCDevJsonFile.PlayerNameMinLength));
        Assert.That(GCDevJsonValidation.IsValidPlayerName(whitespaceOnly), Is.False);
    }

    [Test]
    public void NullNameIsRejected()
    {
        Assert.That(GCDevJsonValidation.IsValidPlayerName(null), Is.False);
    }

    [Test]
    public void PlainInBoundsNameIsAccepted()
    {
        Assert.That(GCDevJsonValidation.IsValidPlayerName("abcd"), Is.True);
    }
}
