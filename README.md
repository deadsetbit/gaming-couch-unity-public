Unity integration for the Gaming Couch platform.

This guide is for game developers building a Unity game for Gaming Couch. It takes you from install to
a playable example, then covers each topic you need as your game grows.

## Contents

- [How your game runs](#how-your-game-runs)
- [Requirements](#requirements)
- [Install](#install)
- [Quick setup](#quick-setup)
- [The game script](#the-game-script)
- [Players and colors](#players-and-colors)
- [Player inputs](#player-inputs)
- [Player placement](#player-placement)
- [The HUD](#the-hud)
- [Game flow](#game-flow)
- [Build and upload](#build-and-upload)
- [FAQ](#faq)
- [More documentation](#more-documentation)
- [Install the package by hand](#install-the-package-by-hand)

## How your game runs

Gaming Couch runs your game as a WebGL build inside the platform. The platform provides the menus,
lobby, controller UI and scoreboard, so you do not build them. It gives your game:

- **Players and their inputs.** The platform decides who plays and sends each player's controller
  state to your game. Your game reads players and inputs. It does not handle joining, leaving or
  names.
- **The HUD.** The platform draws player nametags, the scoreboard, off-screen indicators, points and
  meters. Your game sets the values with `GCPlayer` calls such as `SetScore` and `SetLives`.
- **Colors.** Each player has an assigned color. Use it to draw that player.

A round goes through four steps:

1. **Setup.** The platform asks your game to prepare, for example to load a level. You configure the
   game and HUD, then report that you are done.
2. **Play.** The platform gives you the round's players and a seed. You spawn the players and start
   the round.
3. **Playing.** You read inputs every frame and report score, elimination and finish, so the HUD
   stays in sync.
4. **Game over.** You tell the platform the round has ended.

The rest of this guide shows how to do each step.

## Requirements

- **Unity 6 (`6000.0`).** The package needs Unity 6 so the web export can remove the Unity splash
  screen.
- **Web Build Support module** (formerly _WebGL Build Support_). Unity does not install it by default.
  Add it from _Unity Hub → (gear on your Unity version) → Add modules → Web Build Support_, then reopen
  the project. The Start Screen tells you if it is missing and links to Unity Hub.
- **A 16:9 Game window.** The platform always shows games at 16:9, so set the Game window to 16:9 at
  the top of the window.

For a new project, create it in Unity Hub with the "Universal 3D" (URP) or "Universal 2D" (URP)
template.

## Install

DevApp installs this package into your project and offers an update when the platform expects a newer
version.

1. Sign in at [devspace.gamingcouch.com](https://devspace.gamingcouch.com) and create your game. No
   account yet? Ask for one on [Discord](https://discord.gg/UqSX9ZGz5u).
2. Download DevApp from the **Downloads** page and sign in.
3. In DevApp, pick your game and add your Unity project folder. Press **Install package** when DevApp
   offers it.
4. Open the project in Unity and continue with [Quick setup](#quick-setup).

To add the package yourself instead, see [Install the package by hand](#install-the-package-by-hand).

## Quick setup

The editor tools create a working scene for you:

1. Open **`GamingCouch → Start Screen`**.
2. Press **`Create Example Scene`** and pick one of its two options. Both are also in the menu under
   `GamingCouch → Create Example Scene`.
   - **Template** creates `GCTemplateScene` with `GCExampleTemplate`, an empty game that logs each
     lifecycle step. Start your own game from this one.
   - **Example Game** creates `GCExampleGameScene`, a complete game. Read it to learn the SDK.
3. Press **Play**.

Each option offers to save your current scene, then creates a new scene with the `GamingCouch`
object set up. It generates the example scripts and player prefab under `Assets/GamingCouch/GCExample`.

- You can run the options in any order and keep both. Each one only touches its own files.
- Running an option again moves its previous scene, scripts and prefab to the Trash and generates
  new copies. Before you edit a generated script, move it to your own folder and rename the file and
  class, for example to `Game.cs` and `Game`. Rename the file in Unity's Project window, so the scene
  and prefab keep their links to it.
- On the first run Unity compiles the generated scripts before it sets up the scene. The Start Screen
  tells you when the scene is ready and saved.
- Both options log a clickable link to the generated files in the Console. The template scene also
  shows it as a note in the Game View. Players spawn only at runtime, so the Game View is otherwise
  empty until you press Play.

The template is the smallest listener that completes a round, in one file you can copy and grow. The
example game has the same kind of listener, with a real game's rules in a separate mode script. The
sections after this one show the same code topic by topic, for when you set up your own scene.

### The example game

The code is in five scripts. Only the first one calls `GamingCouch.Instance`.

- `GCExampleGame` is the listener. It sets up the game in `GamingCouchSetup`, spawns the players in
  `GamingCouchPlay`, reads controller input and calls `GameOver` when the mode has a winner.
- `GCExampleSurvivalMode` holds the rules: spawn positions, bots, the ring, eliminations and the
  tie-break. It also chooses the HUD and placement settings.
- `GCExamplePlayer` is the ball. It handles the player colour, movement, pushing and sprint stamina.
  The HUD meter shows the stamina.
- `GCExampleRing` runs the ring cycles and draws the ring.
- `GCExampleBot` picks stick and sprint input for bots. Each bot gets its own caution and aggression
  from its seed, so bots do not all play alike.

### Other menu items

- `GamingCouch → Create GamingCouch GameObject` adds only the `GamingCouch` object to an existing
  scene.
- `GamingCouch → WebGL Build → …` holds the web export and build settings. See
  [Build and upload](#build-and-upload).

## The game script

The `GamingCouch` object has a `Listener` field that holds your game's GameObject. The package calls
`GamingCouchSetup` and `GamingCouchPlay` on that GameObject with `SendMessage`, so they can be private
methods on any script attached to it. Quick setup fills in the field for you. Select the `GamingCouch`
object in the example scene to see it.

Your game needs two scripts:

- A listener script on the GameObject in the `Listener` field.
- A player script that extends `GCPlayer`.

Declare a player store in the listener to hold the round's players, typed to your player script:

```C#
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;

// Replace "Player" with your player script name if it differs.
private GCPlayerStore<Player> playerStore = new GCPlayerStore<Player>();
```

**Setup.** The platform calls `GamingCouchSetup` when your game should prepare. Load your level,
configure the game and HUD, then call `SetupDone()`:

```C#
private void GamingCouchSetup(GCSetupOptions options)
{
    // Load your level based on the options here.

    GamingCouch.Instance.SetupGameVersus(
        new GCGameVersusSetupOptions()
        {
            // How players are ranked. See "Player placement".
            placementCriteria = new GCPlacementSortCriteria[] {
                GCPlacementSortCriteria.EliminatedDescending,
                GCPlacementSortCriteria.ScoreDescending,
                GCPlacementSortCriteria.Finished
            },

            // HUD settings. See "The HUD".
            hud = new GCGameHudOptions()
            {
                players = new GCHudPlayersConfig(),
                isPlayersAutoUpdateEnabled = true, // default true
            }
        }
    );

    GamingCouch.Instance.SetupDone(); // required when setup is finished
}
```

**Play.** The platform calls `GamingCouchPlay` with the round's players. Spawn them and start:

```C#
private void GamingCouchPlay(GCPlayOptions options)
{
    // Creates the players from the player prefab linked to the GamingCouch object.
    GamingCouch.Instance.SetupPlayers<Player>(options.players, (player) =>
    {
        playerStore.AddPlayer(player);
    });

    StartMyGameNow();
}
```

**Game over.** Call this when the round ends:

```C#
GamingCouch.Instance.GameOver();
```

To set up an existing scene by hand instead of using [Quick setup](#quick-setup):

1. Add the `GamingCouch` object with `GamingCouch → Create GamingCouch GameObject`.
2. Create a "Game" object with your listener script and link it to the `Listener` field.
3. Create a player prefab whose script extends `DSB.GC.GCPlayer` and link it to the `Player Prefab`
   field.

## Players and colors

Gaming Couch creates each player inactive, sets its properties, then activates it. Both `Awake` and
`Start` see the final values.

```C#
public class Player : GCPlayer
{
    private void Start()
    {
        GetComponent<SpriteRenderer>().color = ColorBase;
    }
}
```

The main player properties are below. The [GCPlayer API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.15/api/DSB.GC.GCPlayer.html)
has the full list.

| Member                                                              | Type            | Notes                                                                                                                              |
| ------------------------------------------------------------------- | --------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `Index`                                                             | `int`           | Zero-based player index for this round. Use it everywhere you identify a player. Game code cannot see platform IDs or player names |
| `PlayerType` / `IsBot`                                              | enum / `bool`   | Whether the player is a bot                                                                                                        |
| `PlayerSeed`                                                        | `int`           | A fixed seed for this player, for repeatable randomness                                                                            |
| `ColorBase` / `ColorDark` / `ColorLight` / `ColorOffWhite`          | `Color`         | The player's color in four shades                                                                                                  |
| `Score` / `Lives` / `Meter`                                         | `int`           | Current values                                                                                                                     |
| `Status` / `StatusText`                                             | enum / `string` | Player status                                                                                                                      |
| `EliminationState` / `FinishState`, `IsEliminated`, `IsFinished`, … |                 | Read-only state                                                                                                                    |

**The player store** gives you filtered lists of the round's players. `playerStore.Players` has all of
them. Other lists filter by bot or human, and by eliminated or finished state (permanent or
revokable), for example `PlayersUneliminated`, `PlayersBot` and `PlayersFinishedPermanent`. Find a
player by index with `playerStore.GetPlayerByIndex(index)`, which returns `null` if there is no such
player. The [GCPlayerStore API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.15/api/DSB.GC.GCPlayerStore-1.html)
lists them all.

`GCPlayer` also has change events you can subscribe to: `OnScoreChanged`, `OnLivesChanged`,
`OnMeterChanged`, `OnStatusChanged`, `OnEliminationStateChanged` and `OnFinishStateChanged`.

## Player inputs

Read each player's inputs in `Update`, by player `Index`:

```C#
private void Update()
{
    foreach (var player in playerStore.Players)
    {
        var inputs = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
        if (inputs == null) continue;

        // Move the player with the left stick and sprint while primary is held.
        MyMove(player, inputs.leftX, inputs.leftY);
        MySprint(player, inputs.primary);
    }
}
```

`GCControllerInputs` members:

| Member            | Type    | Notes                                               |
| ----------------- | ------- | --------------------------------------------------- |
| `leftX` / `leftY` | `float` | Left stick axes, from `-1.0` to `1.0`               |
| `primary`         | `bool`  | Primary action button (A on an Xbox-style layout)   |
| `secondary`       | `bool`  | Secondary action button (B on an Xbox-style layout) |
| `alt`             | `bool`  | Extra button for rare actions. See below            |

Build your controls on `primary` and `secondary`. `alt` is harder to press on touch-screen
controllers, so do not use it for core actions such as combat. Use it for rare actions, such as
resetting a stuck player. It has no keyboard key in the editor, so your game must work without it.

### The editor playtest keyboard

Your game never reads Unity input for players. The platform and DevApp send input to your game as
data. The package reads the keyboard only to drive a player during editor play. Number keys `1` to `8`
choose which seat the keyboard drives, using the seat numbers DevApp shows.

The keys depend on your project's **Active Input Handling** setting. The package works with Old, New
and Both, and Quick setup does not change the setting.

- With `com.unity.inputsystem` installed and Active Input Handling set to New or Both, use WASD or the
  arrow keys to move, `Space` for primary and `Left Ctrl` for secondary. The axes jump straight to -1,
  0 or 1.
- Otherwise the package reads the Input Manager, with the axis and button names set on the
  `GamingCouch` component. The defaults are `Horizontal`, `Vertical`, `Jump` and `Fire1`. The Input
  Manager ramps a key axis up to full over a few frames.

If you set Active Input Handling to New without installing `com.unity.inputsystem`, the editor keyboard
does not work. The package logs why when the first game starts. DevApp controllers still work.

## Player placement

You do not sort players yourself. Set `placementCriteria` in `SetupGameVersus`, then call the
`GCPlayer` state methods during play. The platform ranks players by each criterion in order:

| `GCPlacementSortCriteria` | Who wins?                       |
| ------------------------- | ------------------------------- |
| `EliminatedDescending`    | The **last** player eliminated  |
| `Eliminated`              | The **first** player eliminated |
| `ScoreDescending`         | The **highest** score           |
| `Score`                   | The **lowest** score            |
| `Finished`                | The **first** player to finish  |
| `FinishedDescending`      | The **last** player to finish   |

A player who is never eliminated or never finished counts as later than everyone. If players tie on
one criterion, the next one in the list decides.

If you do not set `placementCriteria`, the default is `EliminatedDescending`, `ScoreDescending`,
`Finished`: survivors first, then the highest score, then the first to finish. Setting your own list
replaces the default. It does not add to it.

Put the criterion that decides your game first:

- A last-one-standing game: `EliminatedDescending`
- A points game: `ScoreDescending`
- A race: `Finished`

Add more criteria after it only if your game needs a tie-break.

Set state with the `GCPlayer` methods:

```C#
// Score
player.SetScore(0, "Dropped all coins");
player.AddScore(1, "Collected a coin");
player.SubtractScore(2, "Pushed off the edge");

// Elimination: permanent, or revokable when the player can come back.
player.SetEliminatedPermanent("Out of bounds");
player.SetEliminatedRevokable("Tagged");
player.SetRevokeEliminated("Respawned");

// Finish: permanent, or revokable when a finish can be undone.
player.SetFinishedPermanent("Finish line");
player.SetFinishedRevokable("Checkpoint finish");
player.SetRevokeFinished("Checkpoint invalidated");
```

> [!IMPORTANT]
> Placement only sees state set through the `GCPlayer` methods above. A score kept in your own
> variable does not count, and hiding or destroying a player's GameObject does not eliminate them.
> Call the method at the moment it happens in the game, because elimination and finish rank by when
> you called it.

Revokable state counts toward placement until you revoke it. Permanent state cannot be revoked.

## The HUD

The platform draws the HUD from the values you set. Configure it in `SetupGameVersus` and update it
with the `GCPlayer` state methods during play.

The HUD only appears when your game runs inside Gaming Couch. You cannot see it in the editor or in a
plain WebGL build.

### Players HUD

`GCHudPlayersConfig` sets how each player's value and meter appear:

| Field           | Type                  | Values                                           |
| --------------- | --------------------- | ------------------------------------------------ |
| `valueTypeEnum` | `PlayersHudValueType` | `None`, `PointsSmall`, `Status`, `Text`, `Lives` |
| `meterTypeEnum` | `PlayersHudMeterType` | `None`, `Bar`                                    |

```C#
GamingCouch.Instance.SetupGameVersus(
    new GCGameVersusSetupOptions()
    {
        maxScore = 10, // required when showing points
        hud = new GCGameHudOptions()
        {
            players = new GCHudPlayersConfig()
            {
                valueTypeEnum = PlayersHudValueType.PointsSmall,
            }
        }
    }
);
```

- `PointsSmall` shows the score from `SetScore` and `AddScore`. It needs `maxScore` above
  `0` in `SetupGameVersus`, or setup throws an error.
- `Lives` shows the value from `SetLives`.
- `Status` shows the value from `SetStatus`.
- `Text` shows the string your `GCPlayer` subclass returns from `GetHudValueText()`.
- A `Bar` meter shows the value from `SetMeter`, from `0` to `100`.

### Player position and overhead anchors

The HUD needs to know where each player is on screen. Add these components to your player objects:

- **`GCPlayerPosition`** marks the player's position in the world. The HUD uses it for the off-screen
  indicator, and to dim a player's HUD elements when they cover the player. Its options are
  `disableWhenEliminated` (on by default) and `disableWhenOutOfScreen`. If the component is not on the
  player object itself, call `GCPlayerPosition.SetPlayer(player)`.
- **`GCPlayerOverhead`** marks where the nametag, points and meter go. Put it on a child object above
  the player's head. If the component is not on the player object itself, call
  `GCPlayerOverhead.SetPlayer(player)`.

## Game flow

The platform calls your listener in this order:

1. `GamingCouchSetup`. Prepare the game, then call `SetupDone()`.
2. `GamingCouchPlay`. Spawn the players and start.
3. Call `GameOver()` when the round ends.

The platform pauses and resumes your game itself and draws the pause screen.

`GamingCouch.Instance` has other helpers for a run, such as `SetupPlayers` (with an overload that takes
spawn positions and rotations through `GCPlayerSpawnProperties`), `SetGameMaxScore`, `GameSeed` and
`Restart`. See the [GamingCouch API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.15/api/DSB.GC.GamingCouch.html)
for all of them.

## Build and upload

1. Run **`GamingCouch → WebGL Build → Preview web export settings`**, or the web export row on the
   Start Screen. It lists the changes it will make to the build target, WebGL template, splash screen
   and release profile, then applies them. It adds missing template files but keeps your own edits to
   the template. If it cannot switch the build target to Web, it shows a warning. Run it again or
   switch the target yourself in _File → Build Profiles_.
2. Build for Web in _File → Build Profiles_. `Preview dev build settings (fast build)` and
   `Preview release build settings (slow build)` under `GamingCouch → WebGL Build` switch between a
   fast build for testing and a slower production build.
3. In the [Dashboard](https://devspace.gamingcouch.com), open your game and upload the build folder
   as it is. The build writes the metadata files Gaming Couch needs next to `index.html`. Do not edit
   them.

## FAQ

**My game does not start in the editor.**
Check the Console. "GamingCouch listener not set" means the `Listener` field on the `GamingCouch`
object is empty. "SendMessage GamingCouchSetup has no receiver!" means no script on that GameObject
has a `GamingCouchSetup` method. Check the spelling against [The game script](#the-game-script).

**Is online multiplayer supported?**
No. To play with someone remotely, share your screen over Discord, Google Meet or a similar tool, and
share the phone controller from DevApp. You can also upload the build to Gaming Couch.

**Which Active Input Handling should my project use?**
Any of the three works. See [The editor playtest keyboard](#the-editor-playtest-keyboard) for the
differences.

**Can I see the HUD in the editor?**
Not yet. The platform draws the HUD, so it only appears when your game runs inside Gaming Couch. We
plan to show it in the editor too. See [The HUD](#the-hud).

## More documentation

- [API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.15/api) for
  every class and member.
- The generated examples from [Quick setup](#quick-setup), under `Assets/GamingCouch/GCExample`.
  Extend `GCExampleTemplate` into your own game, and read `GCExampleGame` and the scripts next to it
  for a full game.

## Install the package by hand

Use this to add the package yourself instead of through DevApp's **Install package** button. You still
need DevApp to play in the editor. It writes `gc.dev.json` to the project root, and the package blocks
Play Mode without that file.

In Unity, open _Package Manager → Add package from git URL_ and enter the URL with a release tag:

```
https://github.com/deadsetbit/gaming-couch-unity-public.git#unity-<version>
```

Pick a version from the [tags](https://github.com/deadsetbit/gaming-couch-unity-public/tags). Unity
does not offer updates for a git URL, so to move to a newer release, change the tag. Then continue with
[Quick setup](#quick-setup).
