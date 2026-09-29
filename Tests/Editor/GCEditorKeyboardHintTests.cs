using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCEditorKeyboardHintTests
{
    private const string MoveKeys = "WASD or arrow keys";
    private const string PrimaryKey = "Space";
    private const string SecondaryKey = "Left Ctrl";

    [Test]
    public void NamesTheSeatTheKeyboardDrivesAndItsKeys()
    {
        var seats = CreateSeats(
            (1, GCPlayerType.player, GCPlayerColor.blue),
            (2, GCPlayerType.bot, GCPlayerColor.red)
        );

        Assert.That(
            GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, null, seats, 1),
            Is.EqualTo(
                "Keyboard: seat 1 (blue)\n" +
                "Move: WASD or arrow keys\n" +
                "Primary: Space\n" +
                "Secondary: Left Ctrl\n" +
                "Switch seat: press 1-2\n" +
                "Click the Game view first"
            )
        );
    }

    [Test]
    public void SaysWhereTheKeysComeFrom()
    {
        var seats = CreateSeats((1, GCPlayerType.player, GCPlayerColor.blue));

        Assert.That(
            GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, "Input Manager default keys", seats, 1),
            Does.Contain("\nSecondary: Left Ctrl\nInput Manager default keys\n")
        );
    }

    [Test]
    public void OnABotSeatWarnsTheGameMayIgnoreTheKeys()
    {
        var seats = CreateSeats(
            (1, GCPlayerType.player, GCPlayerColor.blue),
            (2, GCPlayerType.bot, GCPlayerColor.red)
        );

        Assert.That(
            GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, null, seats, 2),
            Is.EqualTo(
                "Keyboard: seat 2 (red)\n" +
                "<color=#ff6b6b>Bot seat: the game may ignore these keys</color>\n" +
                "Move: WASD or arrow keys\n" +
                "Primary: Space\n" +
                "Secondary: Left Ctrl\n" +
                "Switch seat: press 1-2\n" +
                "Click the Game view first"
            )
        );
    }

    [Test]
    public void OffersEverySeatNumberInTheRunToSwitchTo()
    {
        var seats = CreateSeats(
            (5, GCPlayerType.player, GCPlayerColor.pink),
            (3, GCPlayerType.player, GCPlayerColor.blue),
            (8, GCPlayerType.bot, GCPlayerColor.red),
            (4, GCPlayerType.player, GCPlayerColor.yellow)
        );

        Assert.That(
            GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, null, seats, 3),
            Does.Contain("\nSwitch seat: press 3-8\n")
        );
    }

    [Test]
    public void OffersNoSwitchWithASingleSeat()
    {
        var seats = CreateSeats((1, GCPlayerType.player, GCPlayerColor.blue));

        Assert.That(
            GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, null, seats, 1),
            Does.Not.Contain("Switch seat")
        );
    }

    [Test]
    public void IsNullWhenTheKeyboardSeatIsNotInTheRun()
    {
        var seats = CreateSeats((1, GCPlayerType.player, GCPlayerColor.blue));

        Assert.That(GCEditorKeyboardHint.Build(MoveKeys, PrimaryKey, SecondaryKey, null, seats, 4), Is.Null);
    }

    private static GCSeatIdentity[] CreateSeats(
        params (int sourceSeatIndex, GCPlayerType playerType, GCPlayerColor playerColor)[] source
    )
    {
        var identities = new GCSeatIdentity[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            identities[index] = new GCSeatIdentity
            {
                sourceSeatIndex = source[index].sourceSeatIndex,
                playerType = source[index].playerType,
                playerColor = source[index].playerColor,
            };
        }

        return identities;
    }
}

// The same branch condition as GCEditorKeyboard, so these run only where the Input Manager reads the keys.
#if !(ENABLE_INPUT_SYSTEM && GC_INPUT_SYSTEM) && (ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM)
public sealed class GCEditorKeyboardInputManagerHintTests
{
    [Test]
    public void NamesTheKeysOfTheDefaultAxesAndButton()
    {
        Assert.That(GCEditorKeyboard.DescribeMoveKeys("Horizontal", "Vertical"), Is.EqualTo("WASD or arrow keys"));
        Assert.That(GCEditorKeyboard.DescribePrimaryKey("Jump"), Is.EqualTo("Space"));
        Assert.That(GCEditorKeyboard.DescribeSecondaryKey("Fire1"), Is.EqualTo("Left Ctrl"));
        Assert.That(
            GCEditorKeyboard.DescribeKeySource("Horizontal", "Vertical", "Jump", "Fire1"),
            Is.EqualTo("Input Manager default keys")
        );
    }

    [Test]
    public void NamesCustomAxesAndButtonsInsteadOfKeys()
    {
        Assert.That(
            GCEditorKeyboard.DescribeMoveKeys("MoveX", "MoveY"),
            Is.EqualTo("Input Manager axes MoveX and MoveY")
        );
        Assert.That(GCEditorKeyboard.DescribePrimaryKey("Dash"), Is.EqualTo("Input Manager button Dash"));
        Assert.That(GCEditorKeyboard.DescribeSecondaryKey("Block"), Is.EqualTo("Input Manager button Block"));
        Assert.That(GCEditorKeyboard.DescribeKeySource("MoveX", "MoveY", "Dash", "Block"), Is.Null);
    }
}
#endif
