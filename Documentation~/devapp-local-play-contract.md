# DevApp / local-play contract

> **Internal: Gaming Couch DevApp maintainers — game developers do not need this.**
> "Internal" is an audience label, not secrecy — this document ships with the package.
> Third-party game developers should read the package [README](../README.md); nothing here is
> required to build a game. This document is the local-development contract between the Gaming Couch DevApp and this Unity
> package: the on-disk `gc.dev.json` / `gc.platform.json` schemas, how seats become players, and the
> DevApp WebSocket protocol as this package implements it.

Living contract reference. Every schema carries `file:line` provenance (the named type/method is the
durable anchor; line numbers drift). Rationale lives in the package repository's architecture
decision records, which are internal and do not ship with the package. The **received-at-boot** side of these payloads (`GCPlayOptions`,
`platformData` as Unity sees them) is owned by the
[platform runtime contract](platform-runtime-contract.md); this document owns the **producer/capture**
side and links across per the overlap rule (capture/schema → here; wire/received → platform doc).

JSON examples use placeholder package identity values; the real `package.json` name/version is never
copied into documentation.

## Contents

- [1. `gc.dev.json` schema](#1-gcdevjson-schema)
- [2. `gc.platform.json` schema](#2-gcplatformjson-schema)
- [3. Validation and gating](#3-validation-and-gating)
- [4. Fallback platform view](#4-fallback-platform-view)
- [5. Seat → player capture](#5-seat--player-capture)
- [6. Deterministic player-index shuffle](#6-deterministic-player-index-shuffle)
- [7. The `players[]` vs `seatIdentities[]` ordering trap](#7-the-players-vs-seatidentities-ordering-trap)
- [8. DevApp WebSocket protocol (package-side spec)](#8-devapp-websocket-protocol-package-side-spec)
- [9. ContractFixtures — executable spec](#9-contractfixtures--executable-spec)

---

## 1. `gc.dev.json` schema

Root project file (`gc.dev.json`) that drives Unity editor local play. **Unity never creates,
bootstraps, or repairs it** — create/update it through the DevApp, which owns the canonical file.
Model: `Editor/GCDevJsonFile.cs:6-70`; parse/validate:
`Editor/GCDevJsonValidation.cs`.

| Field | Type | Rule | Provenance |
|---|---|---|---|
| `devVersion` | int | Must equal `2` (`SupportedDevVersion`); pinned, **not** inspector-editable | `GCDevJsonFile.cs:9`, check `GCDevJsonValidation.cs:248` |
| `entryKey` | string | Must be a valid entry key | `GCDevJsonValidation.cs:253` |
| `seed` | string | `"random"` or an integer `1`–`999999` (as a string) | `GCDevJsonFile.cs:13-15`, check `:258` |
| `seats` | array | Exactly `8` seat records | `GCDevJsonFile.cs:10`, check `GCDevJsonValidation.cs:418-420` |

Seat record (`GCDevJsonSeat`, `GCDevJsonFile.cs:82-99`; fields at `:84-86`):

| Field | Type | Rule |
|---|---|---|
| `name` | string | Length `1`–`8` (`PlayerNameMinLength`/`MaxLength`, `GCDevJsonFile.cs:11-12`) |
| `enabled` | bool | At least one seat must be `true` (`GCDevJsonValidation.cs:445-448`) |
| `isBot` | bool | Bot seat marker |

The Unity inspector edits only `entryKey`, `seed`, `seats` and **preserves unknown top-level fields**
on write (unknown-field-preserving writes, `Editor/GCDevJsonStore.cs`).

```json
{
  "devVersion": 2,
  "entryKey": "versus",
  "seed": "random",
  "seats": [
    { "name": "P1", "enabled": true,  "isBot": false },
    { "name": "P2", "enabled": true,  "isBot": true  },
    { "name": "P3", "enabled": false, "isBot": false }
    /* … exactly 8 seat records … */
  ]
}
```

---

## 2. `gc.platform.json` schema

Root project file describing the production platform metadata the game would ship with. Parsed at
`Editor/GCDevJsonValidation.cs:683-739`; model `Editor/GCPlatformDataFile.cs`. **Field-level failures
are Warnings, not Errors** — invalid platform data degrades to the fallback view (§4) rather than
blocking raw `gc.dev.json` editing (`InvalidFieldsReadResult`, `:751-762`).

| JSON path | Type | Notes | Provenance |
|---|---|---|---|
| `platformDataVersion` | int ≥ 0 | Metadata version | `:683` |
| `game.key` | string | Non-empty game key | `:695` |
| `game.name` | string | Non-empty display name | `:701` |
| `game.entries.<entryKey>` | object | Map of entry key → entry | `:714`, `TryReadEntries` |
| `game.entries.<key>.name` | string | Entry display name | — |
| `game.entries.<key>.minPlayers` | int ≥ 0 | Production minimum (see gating) | `:806` |
| `game.entries.<key>.maxPlayers` | int ≥ minPlayers | Max enabled seats | `:813` |
| `game.entries.<key>.botSupport` | bool | Bot allowance | `:820` |
| `platform.id` | string | Must be `"unity"` for local play (`GCPlatformDataFile.cs:13`) | `:708` |
| `properties.colors.players.<color>.base` | int[3] | RGB `0–255` | `:869` |
| `properties.colors.players.<color>.muted` | int[3] | RGB `0–255` | `:870` |
| `properties.colors.players.<color>.mutedDarker` | int[3] | RGB `0–255` | `:871` |

```json
{
  "platformDataVersion": 3,
  "game": {
    "key": "my-game",
    "name": "My Game",
    "entries": {
      "versus": { "name": "Versus", "minPlayers": 2, "maxPlayers": 8, "botSupport": true }
    }
  },
  "platform": { "id": "unity" },
  "properties": {
    "colors": { "players": { "blue": { "base": [0,0,0], "muted": [0,0,0], "mutedDarker": [0,0,0] } } }
  }
}
```

---

## 3. Validation and gating

Two distinct layers — **do not conflate them** (this is the precise version of the imprecise
README wording, findings A3):

**Structural rules — always enforced** on any `gc.dev.json`, independent of platform data
(`AddDataIssues` + `AddSeatIssues`, `GCDevJsonValidation.cs:240-264, 416-449`):

- `devVersion == 2`, valid `entryKey`, valid `seed`.
- Exactly 8 seats; each `name` length `1`–`8`.
- **At least one enabled seat** (`NoEnabledSeats` Error, `:445-448`). This is a structural rule, **not**
  a platform-data gate.

**Platform-data gate — only when `gc.platform.json` is valid** (`AddPlatformDataContextIssues`,
`:451-522`):

| Condition | Result |
|---|---|
| `platform.id != "unity"` | **Warning** → local play uses fallback platform metadata (`:471-481`) |
| Selected `entryKey` not in `game.entries` | **Error** (`:484-495`) |
| enabled seat count `> entry.maxPlayers` | **Error** (`:499-509`) |
| `entry.minPlayers` | **Not locally enforced** — playtests may run with one enabled seat (`:498`) |
| enabled bot seats on an entry with `botSupport: false` | **Warning** (`:511-521`) |

When platform data is missing or invalid, Unity shows a warning and keeps raw `gc.dev.json` editing
available for structurally valid files.

---

## 4. Fallback platform view

When `gc.platform.json` is missing/invalid, runtime code receives a read-only fallback
`GCPlatformRuntimeView` (shape owned by the
[platform contract §5](platform-runtime-contract.md#5-platformdata--gcplatformruntimeview)).
Fallback values (`Runtime/GCPlayOptions.cs:83-112, 302-311`):

- `validationState` `missing` (or `invalid`), `fallbackActive: true`.
- `game`/`selectedEntryKey` `"notdefined"`; `platform.id` `"unity"`.
- One entry `notdefined` with `minPlayers 1 / maxPlayers 8 / botSupport true`.
- `source.platformDataVersion`: the version the file declared, when `platformDataVersion` itself
  parsed (`BuildFallback`, `Editor/GCPlatformDataFile.cs`) — so an `invalid` fallback typically
  carries a real version; `-1` (unavailable) only when no version could be read at all, as with a
  missing file. Default player colors.

Unity emits `gc.metadata.*` diagnostics for the fallback and never writes or repairs
`gc.platform.json`.

---

## 5. Seat → player capture

Play-mode capture turns the 8-seat roster into a dense player roster (`Editor/GCDevJsonLocalPlaySessionProvider.cs`,
capture at `:455-501`):

- **Skip disabled seats.** Iterate seats in order; enabled seats get a dense **zero-based**
  `playerIndex` (capture order).
- **Fixed seat → color map by seat position** (`SeatColors`, `:262-272`): seat 1 → `blue`, 2 → `red`,
  3 → `green`, 4 → `yellow`, 5 → `purple`, 6 → `pink`, 7 → `cyan`, 8 → `brown`
  (fixture `valid-full-roster-seat-color-map`).
- `playerSeed` is derived from the normalized seat name — FNV-1a32 of the name mapped into the
  `1`–`999999` seed range (`GCPlayerSeed.FromPlayerName` → `ToSeed`, `Runtime/GCPlayerSeed.cs:11, 34-36`).
- Each captured player gets a parallel **`GCSeatIdentity`** carrying **1-based** seat provenance
  (`Runtime/Dev/GCSeatIdentity.cs`; built in `GCDevJsonLocalPlaySessionProvider.cs`):

| `GCSeatIdentity` field | Value |
|---|---|
| `sourceSeatIndex` | 1-based seat number |
| `stableKey` | 1-based seat number as string |
| `label` | `"Seat N"` |
| `playerType` | `player` / `bot` |
| `playerColor` | seat color from the map above |

Capture stability: the captured roster holds until restart or the next Play Mode entry.

---

## 6. Deterministic player-index shuffle

The captured roster is shuffled into game-facing `playerIndex` order deterministically
(`Runtime/GCPlayerIndexMapping.cs:42-101`):

1. For each captured participant, compute `Hash = FNV-1a32("{seed}:{stableKey}")`
   (`:74`; `GCFnv1A32.Compute`).
2. Sort by unsigned `Hash` ascending, tie-break by captured order
   (`OrderBy(Hash).ThenBy(CapturedOrder)`, `:85-87`).
3. Assign `playerIndex` by sorted position.

The same `seed` + same roster always yields the same mapping. (Hosted play instead uses the
provided `playerIndex` values directly — `CreateFromProvidedPlayerIndices`, `:103-143` — but that is
the platform-doc path, not local capture.)

---

## 7. The `players[]` vs `seatIdentities[]` ordering trap

> **Trap — these two arrays are indexed differently.**

The game-facing `players[]` produced by `CreateGameFacingPlayOptions` (`GCPlayerIndexMapping.cs:150-170`)
is in **post-shuffle `playerIndex` order**. The `seatIdentities[]` array captured in §5 stays in
**capture order**. Because the shuffle reorders participants, `players[i]` and `seatIdentities[i]` are
generally **not the same participant** — `players[i]` corresponds to the captured participant at
`entry.CapturedOrder`, not to `seatIdentities[i]`.

To map a game-facing player back to its seat, use the mapping's `sourceSeatIndex`/`CapturedOrder`
(`GCPlayerIndexMappingEntry`, `:299-330`), never positional index equality. The fixture
`valid-sparse-roster-capture` (§9) exercises exactly this.

---

## 8. DevApp WebSocket protocol (package-side spec)

> **Package-side spec — "what this package sends and accepts."** The DevApp repo owns the server
> behavior and should link here. This section documents the Unity editor's side only, with provenance
> against this repository. (Backlog: optionally upstream a canonical protocol spec to the DevApp repo,
> after which this doc would defer to it.)

**Connection:** the editor integration connects to `ws://localhost:3167/ws` and appends
`identity=runtime` (`Runtime/Dev/GCDevAppIntegration.cs:25, 91`).

### 8.1 Outbound: `runtime_register`

Sent on connect (`GCDevAppRuntimeMessages.cs:17-38`, type `RuntimeRegisterMessage` `:173-187`):

| Field | Type | Notes |
|---|---|---|
| `type` | string | `"runtime_register"` |
| `timestamp` | long | |
| `runtimeKind` | string | `"unity_editor"` |
| `projectRootPath` | string | Resolved project root |
| `projectName` | string | |
| `platform` | string | `"unity"` |
| `gameProtocolVersion` | int | `1` |
| `integrationName` | string | `package.json` name |
| `integrationVersion` | string | `package.json` version |
| `rendererMode` | string | `"external"` |
| `displayName` | string | `"Unity Editor"` |

### 8.2 Outbound: `runtime_snapshot`

Run/pause/seat state (`GCDevAppRuntimeMessages.cs:40-73`, type `RuntimeSnapshotMessage` `:200-211`):

| Field | Type | Notes |
|---|---|---|
| `type` | string | `"runtime_snapshot"` |
| `timestamp` | long | |
| `runId` | string | Minted once per run, including a restart, and stable across socket reconnects. Null until the first `Play()` — the runtime is still visible to the DevApp in that window, which treats a missing run id as not-yet-eligible rather than an error. |
| `isRunning` | bool | |
| `capabilities` | object | `{ restart, pause, timescale }` all `true` (`:156-162, 80-88`) |
| `seats` | array | Empty when not running |
| `paused` | bool | |
| `timescale` | float | |

Seat entry (`RuntimeSeatMessage`, `:164-171`): `{ playerIndex (0-based), seatIndex (1-based),
label, type }`.

### 8.3 Inbound: `gcdevtool` actions (text)

Text messages `{ "type": "gcdevtool", "action": ..., "payload": ... }`
(`Runtime/Dev/GCDevAppRuntimeInbound.cs:194-199, 311-329`). Actions:

| `action` | Effect |
|---|---|
| `restart` | Restart the run |
| `input` | Apply a text input payload |
| `timescale_state` | Set pause/timescale |
| `runtime_output_options` | Update runtime output options (e.g. log-capture mode) |

Unknown types/actions are dropped (`unsupported_message_type` / `unsupported_devtool_action`).

### 8.4 Inbound: binary compact controller input frame

A binary WebSocket message, exactly **16 bytes**, type byte `0x44`
(`GCDevAppRuntimeInbound.cs:202-308`):

| Bytes | Field | Encoding |
|---|---|---|
| 0 | type | `0x44` |
| 1–2 | `playerIndex` | uint16 LE |
| 3–6 | `seq` | uint32 LE |
| 7–10 | `timestampMs` | uint32 LE |
| 11–12 | `a0` (left X) | int16 LE, clamped ±1000, ÷1000 → float |
| 13–14 | `a1` (left Y) | int16 LE, clamped ±1000, ÷1000 → float |
| 15 | buttons | bitmask: bit0→`b0`, bit1→`b1`, bit2→`b2` |

Frames with a `seq` ≤ the last seen for that `playerIndex` are ignored as stale (`:250-256`). The
decoded values populate `GCControllerInputsData` (`b*` as int `0/1`) — the same shape the platform
delivers over the WebGL input wire (see [platform contract §6](platform-runtime-contract.md#6-input-wire-format-and-button-semantics)).

---

## 9. ContractFixtures — executable spec

`ContractFixtures/LocalPlay/` holds the executable specification for local play, replayed by
`Tests/Editor/GCDevJsonContractFixtureTests.cs`.
The seven cases:

| Fixture | Covers |
|---|---|
| `valid-sparse-roster-capture` | Skip-disabled capture + shuffle + the §7 ordering trap |
| `valid-full-roster-seat-color-map` | All eight seats enabled: the whole §7 seat → color map |
| `missing-platform-data-warning-only` | Warning-only degrade to fallback |
| `platform-data-max-player-gate-failure` | `> maxPlayers` gate Error (§3) |
| `unsupported-dev-version-failure` | `devVersion != 2` Error |
| `wrong-seat-count-failure` | Seat count ≠ 8 Error |
| `preserving-write-unrelated-top-level-fields` | Unknown-field-preserving write |

Cite the fixture name next to any schema example this file adds; a fixture that disagrees with the doc
is the source of truth.
