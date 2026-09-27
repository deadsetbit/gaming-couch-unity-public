using System;
using System.IO;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCDevJsonIssueFormatterTests
{
    private const string DevJsonWithBotSeat =
        "{\n" +
        "  \"devVersion\": 2,\n" +
        "  \"entryKey\": \"duel\",\n" +
        "  \"seed\": \"12345\",\n" +
        "  \"seats\": [\n" +
        "    { \"name\": \"P1\", \"enabled\": true, \"isBot\": false },\n" +
        "    { \"name\": \"P2\", \"enabled\": true, \"isBot\": true },\n" +
        "    { \"name\": \"P3\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P4\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P5\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P6\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P7\", \"enabled\": false, \"isBot\": false },\n" +
        "    { \"name\": \"P8\", \"enabled\": false, \"isBot\": false }\n" +
        "  ]\n" +
        "}\n";

    private const string PlatformJsonWithoutBotSupport =
        "{\n" +
        "  \"platformDataVersion\": 2,\n" +
        "  \"game\": {\n" +
        "    \"key\": \"contract-game\",\n" +
        "    \"name\": \"Contract Game\",\n" +
        "    \"entries\": {\n" +
        "      \"duel\": { \"name\": \"Head to Head\", \"minPlayers\": 1, \"maxPlayers\": 4, \"botSupport\": false }\n" +
        "    }\n" +
        "  },\n" +
        "  \"platform\": { \"id\": \"unity\" },\n" +
        "  \"properties\": { \"colors\": { \"players\": {}, \"seatOrder\": [\"blue\", \"red\", \"green\", \"cyan\", \"yellow\", \"purple\", \"pink\", \"brown\"] } }\n" +
        "}\n";

    private string rootPath;

    [SetUp]
    public void SetUp()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "GCDevJsonIssueFormatterTests_" + Guid.NewGuid().ToString("N"));
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

    [Test]
    public void BotSupportWarningLeadsWithTheModeAndRemediesAndKeepsDiagnosticsLast()
    {
        File.WriteAllText(Path.Combine(rootPath, GCDevJsonFile.FileName), DevJsonWithBotSeat);
        File.WriteAllText(Path.Combine(rootPath, GCPlatformDataFile.FileName), PlatformJsonWithoutBotSupport);

        var readResult = new GCDevJsonStore(new FakeLocalProjectRootResolver(rootPath)).Read();

        Assert.That(readResult.IsValid, Is.True);
        Assert.That(readResult.validation.issues, Has.Length.EqualTo(1));
        var issue = readResult.validation.issues[0];
        Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Warning));
        Assert.That(issue.code, Is.EqualTo(GCDevJsonIssueCode.PlatformDataBotSupportDisabled));

        var formatted = GCDevJsonIssueFormatter.Format(issue);
        var paragraphs = formatted.Split(new[] { "\n\n" }, StringSplitOptions.None);

        Assert.That(paragraphs, Has.Length.EqualTo(4));
        Assert.That(paragraphs[0], Is.EqualTo("Bot seats are on for the \"Head to Head\" game mode, but its private entry in Dashboard has Bot Support off."));
        Assert.That(paragraphs[1], Does.Contain("turn on Bot Support for its private entry in Dashboard"));
        Assert.That(paragraphs[1], Does.Contain("does not add bots"));
        Assert.That(paragraphs[1], Does.Contain("DevApp playtest settings"));
        Assert.That(paragraphs[2], Does.Contain("return to DevApp"));
        Assert.That(paragraphs[2], Does.Contain("Pull platform data from Dashboard"));
        Assert.That(
            paragraphs[3],
            Is.EqualTo(
                "Details: PlatformDataBotSupportDisabled. Entry: duel. Field: seats. Path: " +
                Path.Combine(rootPath, GCDevJsonFile.FileName) + "."
            )
        );
    }

    [Test]
    public void OtherDiagnosticsKeepTheCodeFirstFormat()
    {
        var gateIssue = GCDevJsonIssue.Error(
            GCDevJsonIssueCode.PlatformDataEnabledSeatsAboveMaximum,
            "gc.dev.json must enable at most 2 seats for \"duel\".",
            "/project/gc.dev.json",
            0,
            "seats",
            "duel"
        );
        var seatIssue = GCDevJsonIssue.Error(
            GCDevJsonIssueCode.InvalidSeatFields,
            "Seat name is invalid.",
            "/project/gc.dev.json",
            3,
            "name"
        );

        Assert.That(
            GCDevJsonIssueFormatter.Format(gateIssue),
            Is.EqualTo("Valid platform data gate failed. PlatformDataEnabledSeatsAboveMaximum: gc.dev.json must enable at most 2 seats for \"duel\". Field: seats. Path: /project/gc.dev.json.")
        );
        Assert.That(
            GCDevJsonIssueFormatter.Format(seatIssue),
            Is.EqualTo("InvalidSeatFields: Seat name is invalid. Seat 3. Field: name. Path: /project/gc.dev.json.")
        );
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
            return "Issue Formatter Tests";
        }
    }
}
