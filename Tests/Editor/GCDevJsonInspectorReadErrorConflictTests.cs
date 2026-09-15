using System;
using System.IO;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCDevJsonInspectorReadErrorConflictTests
{
    private const string ValidDevJson =
        "{\n" +
        "  \"devVersion\": 2,\n" +
        "  \"entryKey\": \"duel\",\n" +
        "  \"seed\": \"12345\",\n" +
        "  \"seats\": [\n" +
        "    { \"name\": \"P1\", \"enabled\": true, \"isBot\": false },\n" +
        "    { \"name\": \"P2\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P3\", \"enabled\": true, \"isBot\": false },\n" +
        "    { \"name\": \"P4\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P5\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P6\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P7\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P8\", \"enabled\": true, \"isBot\": false }\n" +
        "  ]\n" +
        "}\n";

    private const string ValidPlatformJson =
        "{\n" +
        "  \"platformDataVersion\": 1,\n" +
        "  \"game\": {\n" +
        "    \"key\": \"contract-game\",\n" +
        "    \"name\": \"Contract Game\",\n" +
        "    \"entries\": {\n" +
        "      \"duel\": { \"name\": \"Contract Entry\", \"minPlayers\": 1, \"maxPlayers\": 4, \"botSupport\": true }\n" +
        "    }\n" +
        "  },\n" +
        "  \"platform\": { \"id\": \"unity\" },\n" +
        "  \"properties\": { \"colors\": { \"players\": {} } }\n" +
        "}\n";

    private string rootPath;

    [SetUp]
    public void SetUp()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "GCDevJsonInspectorReadErrorConflictTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (rootPath != null && Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    // --- Unit tests: change-detection policy on GCRootJsonFileStamp ---

    [Test]
    public void ReadErrorStampReportsNoExternalChange()
    {
        var healthy = GCRootJsonFileStamp.Read(WriteDevJson(ValidDevJson));
        var errorStamp = GCRootJsonFileStamp.CreateReadErrorStampForTests(healthy.path, "IOException: locked");

        // A transient read error must be treated as no change, otherwise a dirty draft is
        // falsely flagged as conflicted (the reported P1 bug).
        Assert.That(errorStamp.IsExternalChangeFrom(healthy), Is.False);

        // IsSameAs stays a pure value-equality method: the two stamps genuinely differ.
        Assert.That(errorStamp.IsSameAs(healthy), Is.False);
    }

    [Test]
    public void RecoveryToHealthyStampReportsExternalChange()
    {
        var healthy = GCRootJsonFileStamp.Read(WriteDevJson(ValidDevJson));
        var errorStamp = GCRootJsonFileStamp.CreateReadErrorStampForTests(healthy.path, "IOException: locked");

        // Recovering from an error baseline to a healthy read is a real, actionable change.
        Assert.That(healthy.IsExternalChangeFrom(errorStamp), Is.True);
    }

    [Test]
    public void DifferentContentReportsExternalChange()
    {
        var first = GCRootJsonFileStamp.Read(WriteDevJson(ValidDevJson));
        var mutated = ValidDevJson.Replace("\"seed\": \"12345\"", "\"seed\": \"54321\"");
        var second = GCRootJsonFileStamp.Read(WriteDevJson(mutated));

        // Guard against over-broadening: genuine content changes must still be detected.
        Assert.That(second.IsExternalChangeFrom(first), Is.True);
        Assert.That(second.IsSameAs(first), Is.False);
    }

    [Test]
    public void DeletionReportsExternalChange()
    {
        var healthy = GCRootJsonFileStamp.Read(WriteDevJson(ValidDevJson));
        var missing = GCRootJsonFileStamp.Read(Path.Combine(rootPath, "does-not-exist.json"));

        // A genuine deletion (exists == false, readError == null) is not a read error and
        // must still report as changed.
        Assert.That(missing.readError, Is.Null);
        Assert.That(missing.exists, Is.False);
        Assert.That(missing.IsExternalChangeFrom(healthy), Is.True);
    }

    // --- Integration test: poller must not enter conflict on a transient read error ---

    [Test]
    public void PollWithTransientReadErrorDoesNotFalselyConflictDirtyDraft()
    {
        WriteDevJson(ValidDevJson);
        File.WriteAllText(Path.Combine(rootPath, GCPlatformDataFile.FileName), ValidPlatformJson);

        var resolver = new FakeLocalProjectRootResolver(rootPath);
        var devStore = new GCDevJsonStore(resolver);
        var platformDataStore = new GCPlatformDataStore(resolver);

        var forceDevReadError = false;
        Func<GCRootJsonFileStamp> devFileStampReader = () =>
            forceDevReadError
                ? GCRootJsonFileStamp.CreateReadErrorStampForTests(devStore.ResolveFilePath(), "IOException: transient lock")
                : devStore.ReadFileStamp();

        var state = new GCDevJsonInspectorState(
            devStore,
            platformDataStore,
            devFileStampReader,
            platformDataStore.ReadFileStamp);

        Assert.That(state.HasDraft, Is.True, "Precondition: a valid gc.dev.json draft should load.");
        Assert.That(state.HasConflict, Is.False, "Precondition: freshly loaded state is not conflicted.");

        // Make the draft dirty in edit mode.
        state.Draft.seats[1].enabled = !state.Draft.seats[1].enabled;
        Assert.That(state.IsDirty, Is.True, "Precondition: draft mutation should make the state dirty.");

        // Next poll observes a transient read failure on gc.dev.json.
        forceDevReadError = true;
        state.PollForExternalChanges(true);

        // Pre-fix the error stamp differed from the baseline, tripping EnterConflict();
        // the fix treats a read-error stamp as no change, so the draft stays clean.
        Assert.That(state.HasConflict, Is.False, "A transient read error must not flag a false conflict.");
        Assert.That(state.IsDirty, Is.True, "The dirty draft must be preserved across the transient error.");

        // Recovery: the read succeeds again with the file unchanged.
        forceDevReadError = false;
        state.PollForExternalChanges(true);

        Assert.That(state.HasConflict, Is.False, "Recovery from a transient error must remain conflict-free.");
        Assert.That(state.IsDirty, Is.True, "The dirty draft must survive recovery.");
    }

    private string WriteDevJson(string contents)
    {
        var path = Path.Combine(rootPath, GCDevJsonFile.FileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class FakeLocalProjectRootResolver : IGCLocalProjectRootResolver
    {
        private readonly string rootPath;

        internal FakeLocalProjectRootResolver(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public string ResolveProjectRootPath()
        {
            return rootPath;
        }

        public string ResolveProjectName()
        {
            return "Inspector Read Error Tests";
        }
    }
}
