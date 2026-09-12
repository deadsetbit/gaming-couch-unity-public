using NUnit.Framework;

public sealed class GamingCouchGameViewAspectComputeChangedTests
{
    [Test]
    public void ComputeChangedReportsNoChangeWhenWindowAlreadyShowsTargetIndex()
    {
        // A freshly opened (or already-open) Game View may already default to the 16:9
        // target entry. When the pre-set index already equals the target, selecting it
        // changes nothing, so `changed` must be false. The old inline logic computed this
        // from the stale context index (-1 when the window was just opened) and reported true.
        const int targetIndex = 3;

        Assert.That(GamingCouchGameViewAspect.ComputeChanged(targetIndex, targetIndex), Is.False);
    }

    [Test]
    public void ComputeChangedReportsChangeWhenNoSelectionOrDifferentIndex()
    {
        const int targetIndex = 3;

        // -1 is the "not open / unreadable" sentinel and a genuinely different current
        // index both represent a real change to 16:9.
        Assert.That(GamingCouchGameViewAspect.ComputeChanged(-1, targetIndex), Is.True);
        Assert.That(GamingCouchGameViewAspect.ComputeChanged(1, targetIndex), Is.True);
    }
}
