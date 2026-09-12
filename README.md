Unity integration for the Gaming Couch platform.

This is the guide for **game developers** building a Unity game for Gaming Couch. It takes you from
install to a playable example and then documents each topic you need as your game grows.

## Contents

- [How your game runs](#how-your-game-runs)
- [Install](#install)
- [Quick Setup](#quick-setup)
- [The game script](#the-game-script)
- [Players and colors](#players-and-colors)
- [Player inputs](#player-inputs)
- [Player placement](#player-placement)
- [The HUD](#the-hud)
- [Game flow](#game-flow)
- [Build and upload](#build-and-upload)
- [FAQ](#faq)
- [What next?](#what-next)
- [Creating a Unity project from scratch](#creating-a-unity-project-from-scratch)
- [Documentation](#documentation)

## How your game runs

Gaming Couch runs your game as a WebGL build inside the platform. You do not build any menus, lobby,
controller UI, or scoreboard yourself — the platform provides them and hands your game everything it
needs:

- **Players and their inputs.** The platform decides who is playing and streams each player's
  controller state to your game. Your game reads players and inputs; it never manages joining,
  leaving, or names.
- **The hosted HUD.** Player nametags, the scoreboard, off-screen indicators, points, and meters are
  rendered by the platform. Your game feeds it values through the `GCPlayer` state calls (`SetScore`,
  `SetLives`, and so on); it does not draw any of it.
- **Colors.** Each player has an assigned color your game uses to draw that player.

The lifecycle your game participates in:

1. **Setup** — the platform tells your game to prepare (load your level/mode). You configure the game
   and HUD, then signal you are done.
2. **Play** — the platform hands you the round's players and a seed. You spawn the players and start
   the round.
3. **Playing** — you read inputs each frame and report state changes (score, elimination, finish) so
   the hosted HUD stays in sync.
4. **Game over** — you tell the platform the final placement.

The rest of this guide shows how to do each step.

## Install

Import this package with Unity's _Package Manager_ → _Add package from git URL_, then follow
[Quick Setup](#quick-setup).

**Requirements:**

- **Unity 6 (`6000.0`).** This development line targets Unity 6 so Gaming Couch web export can remove
  the Unity splash/logo branding.
- **Web Build Support module** (formerly _WebGL Build Support_). Not installed by default — add it
  from _Unity Hub → (gear on your Unity version) → Add modules → Web Build Support_, then reopen the
  project. The Start Screen flags this and links to Unity Hub if the module is missing. Then, from
  _Build Settings_, switch the platform to Web.
- **16:9 aspect ratio.** Fix the Game window to 16:9 (top of the Game window) — the platform is fixed
  to a 16:9 aspect ratio.

## Quick Setup

The fastest way to a working scene is the tooling — you do not need to wire anything by hand:

1. Open **`GamingCouch → Start Screen`**.
2. Choose **`Create New Example Scene`** (also on the `GamingCouch` menu directly).
3. Press **Play**.

`Create New Example Scene` offers to save your current scene, creates a fresh scene, wires the
`GamingCouch` object, and generates a barebones example `Game` listener (`GCExampleTemplate`) plus a
stock player prefab. The new scene shows an on-screen note in Game View (and logs a clickable console
message) pointing to the generated `GCExampleTemplate` file under `Assets/GamingCouch/GCExample` —
read it to see the whole platform contract in one file, then grow it into your own game. Players spawn
only at runtime, so the Game View is otherwise empty until you press Play.

> **New here? Start from the generated example.** `Create New Example Scene` gives you
> `GCExampleTemplate`: a barebones *wiring demo* — the minimum a listener does to complete the platform
> loop, which you copy and grow into your own game. Want a fuller reference? Run `Wire Example Game` to
> swap in `GCExampleGame` + `GCExamplePlayer`, a small but complete *playable loop* (scoring, rounds,
> per-player input). The snippets below are the same wiring, isolated topic by topic, for when you set
> up your own scene.

Other menu entries you will use:

- `GamingCouch → Create GamingCouch GameObject` — add just the `GamingCouch` object to an existing
  scene.
- `GamingCouch → WebGL Build → …` — the web export and build helpers (see [Build and upload](#build-and-upload)).

## The game script

Your game is driven by one listener script (the "Game" object wired into the `GamingCouch` object's
`Listener` field) plus a player script that extends `GCPlayer`.

Declare a player store, typed to your player script, to hold the round's players:

```C#
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;

// Replace "Player" with your player script name if it differs.
private GCPlayerStore<Player> playerStore = new GCPlayerStore<Player>();
```

**Setup** — the platform calls `GamingCouchSetup` when it is time to prepare your game. Load your
level/mode, configure the game and HUD, then call `SetupDone()`:

```C#
private void GamingCouchSetup(GCSetupOptions options)
{
    // Load your level / mode based on the options here.

    GamingCouch.Instance.SetupGameVersus(
        new GCGameVersusSetupOptions()
        {
            // How players are ranked — see "Player placement".
            placementCriteria = new GCPlacementSortCriteria[] {
                GCPlacementSortCriteria.EliminatedDescending,
                GCPlacementSortCriteria.ScoreDescending,
                GCPlacementSortCriteria.Finished
            },

            // Configure the hosted HUD — see "The HUD".
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

**Play** — the platform calls `GamingCouchPlay` with the round's players. Spawn them and start:

```C#
private void GamingCouchPlay(GCPlayOptions options)
{
    // Instantiates and configures players from the player prefab linked to the GamingCouch object.
    GamingCouch.Instance.SetupPlayers<Player>(options.players, (player) =>
    {
        playerStore.AddPlayer(player);
    });

    StartMyGameNow();
}
```

**Game over** — when the round ends:

```C#
GamingCouch.Instance.GameOver();
```

> If you prefer to wire an existing scene by hand instead of using [Quick Setup](#quick-setup): add the
> `GamingCouch` object (`GamingCouch → Create GamingCouch GameObject`), create a "Game" object with your
> listener script and link it to the `Listener` field, and create a player prefab whose script extends
> `DSB.GC.GCPlayer` and link it to the `Player Prefab` field.

## Players and colors

When a player is instantiated, its game-facing properties are already set. They are available from
`Awake` onward: Gaming Couch instantiates the player while it is inactive, sets the properties, and
only then activates the object, so both `Awake` and `Start` see the final values.

```C#
public class Player : GCPlayer
{
    private void Start()
    {
        GetComponent<SpriteRenderer>().color = ColorBase;
    }
}
```

Key player properties (see the full list in the [API documentation for GCPlayer](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.8/api/DSB.GC.GCPlayer.html)):

| Member                                                              | Type            | Notes                                                                                                                                                        |
| ------------------------------------------------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Index`                                                             | `int`           | Zero-based player index for this round — the identity you use everywhere (inputs, lookups). Platform IDs and player names are **not** available to game code |
| `PlayerType` / `IsBot`                                              | enum / `bool`   | Whether the player is a bot                                                                                                                                  |
| `PlayerSeed`                                                        | `int`           | Per-player deterministic seed                                                                                                                                |
| `ColorBase` / `ColorDark` / `ColorLight` / `ColorOffWhite`          | `Color`         | Color variants for this player — access on the instance                                                                                                      |
| `Score` / `Lives` / `Meter`                                         | `int`           | Current values                                                                                                                                               |
| `Status` / `StatusText`                                             | enum / `string` | Player status                                                                                                                                                |
| `EliminationState` / `FinishState`, `IsEliminated`, `IsFinished`, … |                 | Read-only state flags                                                                                                                                        |

Access color variants on the player instance:

```C#
player.ColorBase
player.ColorDark
player.ColorLight
player.ColorOffWhite
```

**The player store** gives you filtered views of the round's players. `playerStore.Players` is all of
them; convenience collections narrow by bot/non-bot and by eliminated/finished (permanent vs
revokable) state — e.g. `PlayersUneliminated`, `PlayersBot`, `PlayersFinishedPermanent`. Look one up by
index with `playerStore.GetPlayerByIndex(index)` (returns `null` if there is no such player). See the
[API documentation for GCPlayerStore](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.8/api/DSB.GC.GCPlayerStore-1.html)
for the complete set.

`GCPlayer` also exposes change events (`OnScoreChanged`, `OnLivesChanged`, `OnMeterChanged`,
`OnStatusChanged`, `OnEliminationStateChanged`, `OnFinishStateChanged`) if you want to react to state
changes.

## Player inputs

Read each player's inputs in your `Update` loop, addressing players by `Index`:

```C#
private void Update()
{
    foreach (var player in playerStore.Players)
    {
        var inputs = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
        if (inputs == null) continue;

        // Move the player with the left stick and act on the primary button.
        MyMove(player, inputs.leftX, inputs.leftY);
        if (inputs.primary) MyJump(player);
    }
}
```

`GCControllerInputs` members:

| Member            | Type    | Notes                                               |
| ----------------- | ------- | --------------------------------------------------- |
| `leftX` / `leftY` | `float` | Left stick axes, `-1.0`–`1.0`                       |
| `primary`         | `bool`  | Primary action button (A on an Xbox-style layout)   |
| `secondary`       | `bool`  | Secondary action button (B on an Xbox-style layout) |
| `alt`             | `bool`  | Special/accessibility button — see below            |

> **Design for `primary`/`secondary` first.** `alt` is a special button that should not be used for
> core mechanics (such as combat) because it is less accessible on touch-screen controllers. Use it for
> occasional actions like "reset player" when stuck. It also has **no keyboard mapping in the editor**,
> so you cannot exercise it during local editor play — design your game to work without it.

## Player placement

You do not sort players yourself. Define the ranking in `SetupGameVersus` via `placementCriteria`, then
drive it by calling the `GCPlayer` state methods. Placement is evaluated by each criterion in order:

| `GCPlacementSortCriteria`             | Ranks by                            |
| ------------------------------------- | ----------------------------------- |
| `Score` / `ScoreDescending`           | Score, ascending / descending       |
| `Eliminated` / `EliminatedDescending` | Elimination, ascending / descending |
| `Finished` / `FinishedDescending`     | Finish, ascending / descending      |

Set state with the `GCPlayer` methods:

```C#
// Score
player.SetScore(0, "Dropped all coins");
player.AddScore(1, "Collected a coin");
player.SubtractScore(2, "Pushed off the edge");

// Elimination — permanent, or revokable when the player can recover.
player.SetEliminatedPermanent("Out of bounds");
player.SetEliminatedRevokable("Tagged");
player.SetRevokeEliminated("Respawned");

// Finish — permanent, or revokable when finish can be rolled back.
player.SetFinishedPermanent("Finish line");
player.SetFinishedRevokable("Checkpoint finish");
player.SetRevokeFinished("Checkpoint invalidated");
```

Revokable state counts while active and can be revoked; permanent state cannot.

## The HUD

The HUD is rendered by the hosted platform from the values you set. You configure it in
`SetupGameVersus` and keep it in sync by calling `GCPlayer` state methods during play.

> **The HUD only renders inside the Gaming Couch platform.** You cannot see it in the editor or in a
> plain Unity/WebGL build — the only way to verify HUD behavior is to run in the platform.

**Configure the Players HUD.** `GCHudPlayersConfig` chooses how each player's value and meter are
shown:

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

- With `PointsSmall`, the HUD reflects the score you set via `SetScore`/`AddScore`.
- With `Lives`, it reflects `SetLives`; with `Status`, it reflects `SetStatus`.
- With `Text`, override `GetHudValueText()` on your `GCPlayer` subclass to return the string to show.
- A `Bar` meter reflects `SetMeter` (`0`–`100`).

> **`PointsSmall` requires `maxScore`.** Setting `valueTypeEnum = PlayersHudValueType.PointsSmall`
> without a `maxScore` greater than `0` throws at setup. Set `maxScore` (or `SetGameMaxScore`) when you
> show points.

**Player position and overhead anchors.** So the HUD can place per-player overlays, attach small
components to your player objects that report where each player is on screen:

- **`GCPlayerPosition`** — add it to a transform that marks the player's world position. It drives the
  off-screen indicator (when a player leaves the view) and lets the HUD dim a player's elements when
  they are underneath the HUD. Options: `disableWhenEliminated` (default on), `disableWhenOutOfScreen`.
  If it is not on the player itself, set the player with `GCPlayerPosition.SetPlayer(player)`.
- **`GCPlayerOverhead`** — add it (often to a child object offset above the player's head) to anchor
  the player's overhead HUD such as nametag, points, and meter. If it is not on the player itself, set
  the player with `GCPlayerOverhead.SetPlayer(player)`.

## Game flow

The platform calls your listener in this order: `GamingCouchSetup` (prepare) → you call `SetupDone()`
→ `GamingCouchPlay` (spawn players, start) → you call `GameOver()` when the round ends. The platform
also pauses/resumes your game on its own; you do not draw the pause UI.

`GamingCouch.Instance` exposes the run-level helpers you need along the way — for example `SetupPlayers`
(with an optional spawn-position/rotation overload via `GCPlayerSpawnProperties`), `SetGameMaxScore`,
`GameSeed`, and `Restart`. See the
[API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.8/api/DSB.GC.GamingCouch.html) for the
full surface.

## Build and upload

When you are ready to build for Gaming Couch:

1. Run **`GamingCouch → WebGL Build → Preview web export settings`** (or the web export row on the Start
   Screen). It shows a preview of the build-target, template, splash/logo, and release-profile changes
   it will apply, then applies them. The installer is no-overwrite: it fills in missing template files
   but keeps your local template edits. If it cannot switch the build target to WebGL automatically, it
   leaves a warning — run it again or switch to WebGL manually before building.
2. Build your WebGL project. For quick iteration, `Preview dev build settings (fast build)` and
   `Preview release build settings (slow build)` prepare fast/production profiles.
3. Upload the build output. Alongside `index.html`, a Gaming Couch build writes the small metadata
   files the platform needs at upload — they are generated for you; you do not edit them by hand.

## FAQ

**Is online multiplayer supported?**
Online multiplayer is not currently supported.

To test online it can be feasible over screen sharing in discord, google meet, etc. for this, you can share the mobile controller from the Gaming Couch DevApp or upload the build to gaming couch.

**Can I test the HUD in the editor?**
Currently no. The HUD is rendered by the hosted platform, so it only appears when your game runs inside Gaming
Couch — not in the editor or a plain WebGL build. See [The HUD](#the-hud).

This is planned for the near future to be able to visualize the HUD in the editor as well.

## What next?

- Grow the generated example: after [Quick Setup](#quick-setup), read and extend `GCExampleTemplate` under `Assets/GamingCouch/GCExample` (or run `Wire Example Game` for the fuller `GCExampleGame` + `GCExamplePlayer`).
- Browse the full [API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.8/api).

## Creating a Unity project from scratch

To set up a new project from scratch:

- Create a new Unity project with the "Universal 3D" (URP) template, or optionally "Universal 2D" (URP).
- Follow [Install](#install) and [Quick Setup](#quick-setup).

## Documentation

**For game developers**

- This README — the integration guide.
- [API documentation](https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.8/api) — generated per-member reference.
- Example project — generate one inside the Unity editor with `GamingCouch → Create New Example Scene`; it creates an editable `GCExampleTemplate` under `Assets/GamingCouch/GCExample` (run `GamingCouch → Wire Example Game` for the full `GCExampleGame` + `GCExamplePlayer`).

**Internal (platform & DevApp maintainers)**

- [Platform runtime contract](Documentation~/platform-runtime-contract.md) — the JS↔Unity wire spec.
- [DevApp / local-play contract](Documentation~/devapp-local-play-contract.md) — `gc.dev.json`/`gc.platform.json` schemas and the DevApp protocol.
