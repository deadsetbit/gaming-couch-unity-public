# Platform runtime contract (JS ↔ Unity wire spec)

> **Internal: Gaming Couch platform (client/SDK) maintainers — game developers do not need this.**
> "Internal" is an audience label, not secrecy — this document ships with the package.
> Third-party game developers should read the package [README](../README.md) instead; nothing
> here is required to build and ship a game. This document is the complete wire contract between the Gaming Couch
> JavaScript host (client/SDK) and this Unity package.

This is a living contract reference. Every schema below carries `file:line` provenance against the
package's code (line numbers are pointers — the named type/method is the durable anchor). It states
the *shape*, not the *why*: decision rationale lives in the package repository's architecture decision
records, which are internal and do not ship with the package.

JSON examples use **placeholder** package identity values (e.g. `com.example.game`, `1.2.3`): the real
`package.json` name/version is never copied into documentation, so a release cannot leave these examples
stale. `platform` (`"unity"`) and `gameProtocolVersion` (`1`) are protocol constants, not package metadata,
and are shown literally.

## Contents

- [1. Boot SendMessage surface and lifecycle order](#1-boot-sendmessage-surface-and-lifecycle-order)
- [2. `GCSetupOptions` (setup payload)](#2-gcsetupoptions-setup-payload)
- [3. `GCPlayOptions` (play payload)](#3-gcplayoptions-play-payload)
- [4. Boot roster validation](#4-boot-roster-validation)
- [5. `platformData` — `GCPlatformRuntimeView`](#5-platformdata--gcplatformruntimeview)
- [6. Input wire format and button semantics](#6-input-wire-format-and-button-semantics)
- [7. `runtime_messages` envelope, records, and payloads](#7-runtime_messages-envelope-records-and-payloads)
- [8. `gc.diagnostic` payload and code catalog](#8-gcdiagnostic-payload-and-code-catalog)
- [9. `screen_space` envelope and anchors](#9-screen_space-envelope-and-anchors)
- [10. jslib bridge — `window.gamingCouch*` callbacks](#10-jslib-bridge--windowgamingcouch-callbacks)
- [11. Sidecars and template gating](#11-sidecars-and-template-gating)
- [12. Web export template contract](#12-web-export-template-contract)
- [13. `gameProtocolVersion` policy](#13-gameprotocolversion-policy)
- [14. Upload validation (cross-repo expectation)](#14-upload-validation-cross-repo-expectation)
- [Appendix A. Unsupported online-multiplayer surface](#appendix-a-unsupported-online-multiplayer-surface)

---

## 1. Boot SendMessage surface and lifecycle order

The host drives Unity through `unityInstance.SendMessage("GamingCouch", "<method>", <arg>)`. The
receiving methods live on the `GamingCouch` component (`Runtime/GamingCouch.cs`, region "Methods
called by the GamingCouch platform", L244–561). They are `private`; `SendMessage` reaches them by name.

| Order | Host → Unity (`SendMessage`) | Arg | Effect | Provenance |
|---|---|---|---|---|
| 1 | `GamingCouchSetupOptions` | `GCSetupOptions` JSON string | Stored only; **not** forwarded to the game yet (waits for `Start` so Unity is initialized and the splash shows) | `GamingCouch.cs:248` |
| 2 | `GamingCouchSetup` | *(none)* | Forwards the stored options to the game listener's `GamingCouchSetup(GCSetupOptions)` | `GamingCouch.cs:277`, forward at `:298` |
| 3 | `GamingCouchPlay` | `GCPlayOptions` JSON string | Parses and forwards to the game listener's `GamingCouchPlay(GCPlayOptions)` | `GamingCouch.cs:328`, forward at `:434` |
| — | `GamingCouchPause` | `"true"` / `"false"` | Toggles pause; sets `Time.timeScale` to 0 when paused | `GamingCouch.cs:353` |
| — | `GamingCouchInputs` | `"playerIndex\|inputsJson"` | Per-player input frame (see §6) | `GamingCouch.cs:518` |

Unity → host signals during this flow are the jslib callbacks in §10.

**Lifecycle order (happy path):**

1. WebGL build loads. A `[RuntimeInitializeOnLoadMethod(BeforeSplashScreen)]` bootstrap forwards the
   baked runtime identity to `window.gamingCouchRegisterRuntimeInfo` (and optional
   `window.gamingCouchRegisterUnityBuildInfo`); `GamingCouchInstanceStarted()` fires as a payload-free
   startup signal (`GamingCouch.cs:223`).
2. Host sends `GamingCouchSetupOptions` (stored) → then `GamingCouchSetup`.
3. Game listener's `GamingCouchSetup` loads its mode/level, calls
   `GamingCouch.Instance.SetupGameVersus(...)` and then `SetupDone()`, which signals
   `window.gamingCouchSetupDone()`.
4. Host sends `GamingCouchPlay`. Game listener's `GamingCouchPlay` spawns players
   (`SetupPlayers<T>`) and starts the round.
5. During play the host streams inputs (`GamingCouchInputs`) and pause toggles (`GamingCouchPause`);
   Unity streams `runtime_messages` and `screen_space` outward (§7, §9).
6. `GameOver()` emits the terminal `game_over` runtime message (§7).

The identity boundary is strict-current: the package accepts only current identity, and adapting
legacy identity is the client/SDK's job.

---

## 2. `GCSetupOptions` (setup payload)

Deserialized by `GCSetupOptions.CreateFromJSON` (`Runtime/GCSetupOptions.cs:6-16`).

| Field | Type | Notes |
|---|---|---|
| `mode` | int enum `GCMode` | `1` = Development, `2` = Production (`GamingCouch.cs:23`). Defaults to Production |
| `clientId` | uint | Host-assigned client id |
| `isServer` | bool | Server role flag (online-multiplayer only; see Appendix A) |
| `gameModeId` | string | Game-mode selector passed through to the game |

```json
{ "mode": 2, "clientId": 1, "isServer": false, "gameModeId": "versus" }
```

---

## 3. `GCPlayOptions` (play payload)

Deserialized by `GCPlayOptions.CreateFromJSON` (`Runtime/GCPlayOptions.cs`; class `GCPlayOptions`
fields at L512–529, `CreateFromJSON` at L534–572). Parsing goes through the internal
`GCPlayOptionsTransport` (L931–938).

| Field | Type | Notes |
|---|---|---|
| `players` | `GCPlayerOptions[]` | Round roster. Required: the `players` field must be present and non-null (see §4). Note: `CreateFromJSON` does **not** reject an empty `[]` — an empty array parses and skips index validation (`GCPlayOptions.cs:553-562`); the platform is expected to send at least one player |
| `seed` | int | Round seed, `1`–`999999`; deterministic level/FX generation (L513–527) |
| `runtimeOutput` | `GCRuntimeOutputOptions` | Output toggles; defaults applied if omitted |
| `platformData` | `GCPlatformRuntimeView` | Platform metadata view; falls back to a "missing" view if absent (see §5) |

**`GCPlayerOptions`** (`GCPlayOptions.cs:11-17`) — one roster entry:

| Field | Type | Notes |
|---|---|---|
| `playerIndex` | int | Zero-based, dense, run-scoped participant index (the only public player identity) |
| `playerSeed` | int | Deterministic per-player seed, `1`–`999999` — the same range as `seed`. The platform generates it inside that range (SDK `UnityPlayerIdentityMap.createUnityPlayerSeed`, `(fnv1a32(name) % 999999) + 1`); an out-of-range value is normalized rather than rejected (`GCPlayerSeed.NormalizeOrFallback`, `Runtime/GCPlayerSeed.cs:16-29`) |
| `type` | string | Parsed to `GCPlayerType` (`player`, `bot`; the enum also has a default `unset`, `GamingCouch.cs:29`) |
| `color` | string | Parsed to `GCPlayerColor` (`blue red green yellow purple pink cyan brown`, `GamingCouch.cs:27`) |

**`GCRuntimeOutputOptions`** (`GCPlayOptions.cs:19-25`):

| Field | Type | Default | Notes |
|---|---|---|---|
| `stateSnapshots` | bool | `true` | Enables `runtime_messages` state output |
| `screenSpace` | bool | `true` | Enables `screen_space` output |
| `runtimeLogCapture` | string | `"off"` | One of `off`, `error_only`, `warning_and_error`, `full` (`GCRuntimeUnityLogCaptureMode`, L27-34). The parser also accepts the legacy aliases `error` → `error_only` and `warningAndError` → `warning_and_error` (`GCUnityLogCapture.NormalizeMode`) |

```json
{
  "players": [
    { "playerIndex": 0, "playerSeed": 111, "type": "player", "color": "blue" },
    { "playerIndex": 1, "playerSeed": 222, "type": "bot",    "color": "red"  }
  ],
  "seed": 424242,
  "runtimeOutput": { "stateSnapshots": true, "screenSpace": true, "runtimeLogCapture": "off" },
  "platformData": { "…": "see section 5" }
}
```

> **Cross-repo note (client/SDK):** `platformData` arrives **inside** this `GamingCouchPlay` payload,
> not as a separate message.

> **Known cross-repo discrepancy — `seed` ranges disagree.** Hosted play generates and validates both
> `seed` and `playerSeed` inside `1`–`999999` (SDK `ACTIVE_RUN_SEED` in
> `sdk/src/plugins/platform/unity/adapters/UnityPlayerIdentityMap.ts`), and this package rejects a
> `gc.dev.json` seed outside that range as a validation error (`GCDevJsonFile.MaxSeed`; see the
> [DevApp local-play contract §1](devapp-local-play-contract.md#1-gcdevjson-schema)). The DevApp's
> run-configuration UI, however, accepts and randomizes a fixed **round** seed anywhere in
> `1`–`Number.MAX_SAFE_INTEGER` (`RUN_CONFIGURATION_MAX_FIXED_SEED` in
> `devspace/devapp/src/frontend/ui/domain/runConfiguration/runConfiguration.ts`), so a DevApp-chosen
> seed above `999999` is written to `gc.dev.json` and then refused by this package. This is a `seed`
> mismatch only — `playerSeed` is unaffected — and the fix belongs in the DevApp, not here.

---

## 4. Boot roster validation

`GCPlayOptions.CreateFromJSON` rejects malformed rosters before the game sees them (constants at
`GCPlayOptions.cs:497-510`; checks at L534–639):

| Rule | Error message constant | When |
|---|---|---|
| Roster present | `MissingPlayersPayloadErrorMessage` | No top-level `players` field, or null |
| No legacy entries | `LegacyPlayersPayloadErrorMessage` | Any `players[]` object contains `playerId` or `name` (raw JSON scan, L601-605) |
| `playerIndex` present | `MissingPlayerIndexPayloadErrorMessage` | A `players[]` object omits `playerIndex` |
| Dense, in range | `DensePlayerIndexPayloadErrorMessage` | Any `playerIndex < 0` or `>= players.length` (L621-639) |
| Unique | `DuplicatePlayerIndexPayloadErrorMessage` | Duplicate `playerIndex` values |

Legacy `playerId`/`name` roster adaptation is **not** performed here — the client/SDK must translate
before invoking Unity.

---

## 5. `platformData` — `GCPlatformRuntimeView`

Full shape at `GCPlayOptions.cs`, class `GCPlatformRuntimeView` (L43–172) and its nested types.
This is the runtime-facing projection of `gc.platform.json`; the **capture/producer** side of that
schema is owned by the [DevApp local-play contract](devapp-local-play-contract.md). When platform
data is missing or invalid, Unity substitutes a read-only fallback view (see
[DevApp local-play contract §4](devapp-local-play-contract.md#4-fallback-platform-view)).

**`GCPlatformRuntimeView`** (L43-58):

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | int | Current `1` (`CurrentSchemaVersion`, L46) |
| `validationState` | string | `valid` / `missing` / `invalid` (`GCPlatformRuntimeValidationState`, L36-41) |
| `fallbackActive` | bool | `true` when the fallback view is in effect |
| `source` | `GCPlatformRuntimeSource` | Provenance of the metadata |
| `game` | `GCPlatformRuntimeGame` | `{ key, name }` (L232-252) |
| `platform` | `GCPlatformRuntimePlatform` | `{ id }`; `id` is `"unity"` (L254-272) |
| `selectedEntryKey` | string | Selected entry; `"notdefined"` in fallback |
| `entries` | `GCPlatformRuntimeEntry[]` | Available entries |
| `playerColors` | `GCPlatformRuntimePlayerColors` | Per-color RGB triples |

**`GCPlatformRuntimeSource`** (L174-229):

| Field | Type | Notes |
|---|---|---|
| `fileName` | string | `"gc.platform.json"` |
| `platformDataVersion` | int | The version declared by `gc.platform.json`, including on a fallback view when that field parsed; `-1` (`PlatformDataVersionUnavailable`) only when no version could be read at all. A version concept **distinct** from `gameProtocolVersion` and the sidecar `schemaVersion` |
| `path` | string | Source path (may be null) |
| `message` | string | Fallback/validation message (may be null) |
| `fieldName` | string | Offending field for invalid data (may be null) |

**`GCPlatformRuntimeEntry`** (L274-320):

| Field | Type | Notes |
|---|---|---|
| `entryKey` | string | Entry id |
| `name` | string | Display name |
| `minPlayers` | int | Production minimum (not locally enforced; see DevApp contract) |
| `maxPlayers` | int | Maximum enabled seats |
| `botSupport` | bool | Whether bots are allowed on this entry |

**`GCPlatformRuntimePlayerColors`** (L384-492) has one field per built-in color
(`blue red green yellow purple pink cyan brown`), each a **`GCPlatformRuntimePlayerColor`** (L322-382):

| Field | Type | Notes |
|---|---|---|
| `base` | int[3] | RGB `0–255` (serialized key `base`; C# field is `@base`) |
| `muted` | int[3] | RGB `0–255` |
| `mutedDarker` | int[3] | RGB `0–255` |

**Fallback view values** (`CreateFallbackMissing` / `GCPlatformRuntimeEntry.Fallback`, L83-112, L302-311):
`validationState` `missing`, `fallbackActive` `true`, `game`/`selectedEntryKey` `notdefined`, one entry
`notdefined` with `minPlayers 1 / maxPlayers 8 / botSupport true`, and default player colors.

`source.platformDataVersion` is **not** forced to `-1` on a fallback view: when the file parsed far
enough to read a `platformDataVersion`, the fallback reports that declared version alongside
`fallbackActive: true` and the `invalid`/`missing` state (`BuildFallback`,
`Editor/GCPlatformDataFile.cs`); `-1` is reserved for the case where no version could be read — a
missing file, or a file that failed before that field. `fallbackActive`/`validationState` already say
"do not trust this data", so the declared version is the more useful debugging signal, where a magic
`-1` that no consumer reads would only invite mistaking the sentinel for a real version. Runtime
normalization only floors the value at `-1` (`GCPlatformRuntimeSource.NormalizeForRuntime`, L220-227).

```json
{
  "schemaVersion": 1,
  "validationState": "valid",
  "fallbackActive": false,
  "source": { "fileName": "gc.platform.json", "platformDataVersion": 3, "path": null, "message": null, "fieldName": null },
  "game": { "key": "my-game", "name": "My Game" },
  "platform": { "id": "unity" },
  "selectedEntryKey": "versus",
  "entries": [ { "entryKey": "versus", "name": "Versus", "minPlayers": 2, "maxPlayers": 8, "botSupport": true } ],
  "playerColors": { "blue": { "base": [0,0,0], "muted": [0,0,0], "mutedDarker": [0,0,0] }, "…": {} }
}
```

---

## 6. Input wire format and button semantics

Per-player input arrives as `GamingCouchInputs("playerIndex|inputsJson")`. The message is split on
`|`; `playerIndex` is parsed invariant-culture / `NumberStyles.None`; a malformed message is dropped,
not thrown (`GamingCouch.cs:518-560`, parser `TryParsePlayerInputMessage` at `:537`). `inputsJson`
deserializes to `GCControllerInputsData` (`Runtime/GCControllerInputsData.cs:13-40`).

| Wire field | Type | Exposed to games as | Provenance |
|---|---|---|---|
| `a0` | float | `GCControllerInputs.leftX` | `GCControllerInputs.cs:25` |
| `a1` | float | `GCControllerInputs.leftY` | `GCControllerInputs.cs:31` |
| `b0` | int `0/1` | `GCControllerInputs.primary` (`b0 == 1`) | `GCControllerInputs.cs:35` |
| `b1` | int `0/1` | `GCControllerInputs.secondary` (`b1 == 1`) | `GCControllerInputs.cs:39` |
| `b2` | int `0/1` | `GCControllerInputs.alt` (`b2 == 1`) | `GCControllerInputs.cs:49` |

**Buttons are integers `0/1` on the wire, not booleans** — the package compares `== 1`. Axes are
floats, `-1.0`–`1.0`.

`b2`/`alt` is present on the wire but is **never originated by this package in the editor** — there is
no keyboard binding for it in local play (editor input mapping at `GamingCouch.cs`, `ApplyDevAppInput`
path). The physical/touch mapping of `b2` (which button, on which controller) is owned by the client
repo, not this package. Game-facing semantics for `alt` (an accessibility button, not for core
mechanics) live in the package [README](../README.md#player-inputs) and the `GCControllerInputs.alt`
XML docs.

Example message: `0|{"a0":-1.0,"a1":0.0,"b0":1,"b1":0,"b2":0}`

---

## 7. `runtime_messages` envelope, records, and payloads

Runtime state and diagnostics flow out through `window.gamingCouchRuntimeMessages` as batched
envelopes.

**Transition records are an ordered history and are never coalesced.** Every accepted player
state change is queued as its own transition record in occurrence order, so an eliminate-then-
respawn cannot collapse into "nothing changed". Only latest-state projections — state snapshots
and HUD output — coalesce to the current value at a flush. Batch caps may change *when* a flush
happens but never remove or merge a transition record. A consumer needing history reads the
transition records; one needing current truth reads snapshots, **and must not infer from a single
snapshot that no intermediate changes occurred**. Duplicate-value requests are rejected as no-ops
at the transition gate, so the stream carries only real changes.

Builder:
`Runtime/RuntimeMessages/GCRuntimeMessages.cs`.

**Envelope** (`BuildEnvelopeJson`, L787-817):

```json
{ "type": "runtime_messages", "v": 1, "messages": [ /* records */ ] }
```

**Record** (`GCRuntimeMessageRecord.ToJson`, L592-658):

| Field | Type | Notes |
|---|---|---|
| `type` | string | `gc.player` / `gc.state` / `gc.game` / `gc.diagnostic` (`GCRuntimeMessageTypes`, L16-22) |
| `name` | string | See message-name table below |
| `seq` | long | One-based sequence within the active run |
| `ms` | long | Milliseconds since run start (non-negative) |
| `playerIndex` | int | **Optional** — present only for player-scoped messages |
| `data` | object | Payload; shape depends on `type`/`name` |

**Message names** (`GCRuntimeMessageNames`, L24-34):
`snapshot`, `score_changed`, `lives_changed`, `status_changed`, `meter_changed`,
`elimination_changed`, `finish_changed`, `game_over`.

**Batching:** player transitions accumulate and flush at `MaxPendingMessagesPerBatch = 128`
(`:664`, `QueuePlayerTransition` at `:727`); the runtime also flushes on `LateUpdate`. Diagnostics
flush immediately (`EmitDiagnosticPayload`, L117-122).

### 7.1 State snapshot (`gc.state` / `snapshot`)

`GCRuntimeStateSnapshotPayload.ToJson` (L256-280), player entries via
`GCRuntimeStateSnapshotPlayer.AppendJson` (L300-316). **Note the JSON keys differ from field names**:
`statusText → text`, `eliminationState → elimination`, `finishState → finish`.

```json
{
  "game": { "status": "playing" },
  "players": [
    { "playerIndex": 0, "score": 3, "lives": 2, "status": "Neutral", "text": "",
      "meter": -1, "placement": 1, "elimination": "None", "finish": "None" }
  ]
}
```
`game.status` is `pending_setup` / `setup_done` / `playing` / `game_over` (`ToSnapshotGameStatus`, L425-440).

### 7.2 Player transitions (`gc.player` / `*_changed`)

Int transitions (`score_changed`, `lives_changed`, `meter_changed`) — `GCRuntimeTransitionPayload.BuildIntJson` (L461-470):
```json
{ "from": 2, "to": 3, "reason": "Collected a coin" }
```
`status_changed` nests `{status,text}` for `from`/`to` (`BuildStatusJson`/`AppendStatusValue`, L485-512).
`reason` is optional and truncated to 256 chars (`GCRuntimePayloadBounds`, L443-457).

### 7.3 Game over (`gc.game` / `game_over`)

`GCRuntimeGameOverPlacementPayload.BuildJson` (L534-554). The array is player indices ordered by final
placement (first = 1st place):
```json
{ "playersByPlacement": [2, 0, 1] }
```
**The result is an object, never a bare array.** Legacy builds ended a game by emitting a bare
array that receivers read as platform player IDs, so a bare array of zero-based player indices
would be shape-identical and ambiguous — `playerIndex: 0` cannot be told apart from an old
positive platform ID. The wrapper keeps new results structurally distinct from that bridge and
leaves room for further result fields without a version field. A receiver must not accept a bare
array as a current result; the legacy bare array stays a client/SDK migration bridge interpreted
as platform IDs and never gains new result semantics.

The payload must be a permutation of all active player indices; an invalid placement is rejected
with a diagnostic rather than emitted.

**First-accepted-wins:** once accepted, later submissions are rejected with a
`gc.runtime.invalid_game_over_placement` diagnostic and ignored — even byte-identical ones, and
even re-entrant ones during the accept callback. A rejected submission before any acceptance does
not consume the slot (`TrySubmitGameOverPlacement`, L129-201; guard `gameOverPlacementAccepted` at
L155/193). Pending transitions and a final state snapshot are flushed immediately before the
`game_over` record, in one envelope.

---

## 8. `gc.diagnostic` payload and code catalog

Emitted as `gc.diagnostic` records via `GCDiagnostics.Emit(code, severity, sourceArea, message,
context)` (`Runtime/RuntimeMessages/GCDiagnostics.cs:520-533`). The **`code` is the record `name`**;
the `data` payload is (`BuildPayloadJson`, L573-608):

| Field | Type | Notes |
|---|---|---|
| `severity` | string | `info` / `warning` / `error` (`FormatSeverity`, L610-623) |
| `sourceArea` | string | `api` / `mapping` / `state` / `runtime_messages` / `screen_space` / `metadata` / `runtime_log` (`GCDiagnosticSourceAreas`, L17-42) |
| `message` | string | Bounded length |
| `mapping` | object | **Optional** context |
| `details` | object | **Optional** context |
| `debug` | object | **Optional** context |

```json
{ "severity": "warning", "sourceArea": "metadata", "message": "gc.platform.json was not found.", "details": { "path": "…" } }
```

The code catalog is **closed** — `Emit` throws on an unknown code, an unknown source area, or a
code/source-area mismatch (`ValidateDiagnostic`, L535-571). The left column below is the **code
prefix** (the record `name`); each prefix maps 1:1 to the matching `sourceArea` string
(`gc.api.*` → `api`, `gc.state.*` → `state`, `gc.runtime.*` → `runtime_messages`, `gc.log.*` →
`runtime_log`, etc.). Codes (`GCDiagnosticCodes`, L44-69):

| Code prefix | Codes |
|---|---|
| `gc.api.*` | `removed_identity_api`, `removed_player_name_api`, `removed_state_api`, `removed_store_api`, `unsupported_multiplayer_api`, `legacy_runtime_payload` |
| `gc.mapping.*` | `invalid_player_index`, `unmapped_participant` |
| `gc.state.*` | `duplicate_elimination`, `duplicate_finish`, `invalid_revoke`, `invalid_transition`, `post_game_over_mutation`, `clamped_value` |
| `gc.runtime.*` | `malformed_message`, `unknown_message`, `invalid_game_over_placement`, `malformed_screen_space`¹ |
| `gc.metadata.*` | `missing_platform_data`, `invalid_platform_data`, `fallback_active` |
| `gc.log.*` | `runtime_log`, `runtime_warning`, `runtime_error` |

¹ **Exception to the prefix→area mapping:** `gc.runtime.malformed_screen_space` is emitted with
`sourceArea: "screen_space"`, not `runtime_messages` — special-cased in
`GCDiagnosticCodes.IsValidSourceArea` (`GCDiagnostics.cs:111-114`).

**Platform-player-id keys are hard-rejected.** Context field keys `playerId`, `playerIds`,
`platformPlayerId`, `platformPlayerIds` throw (`ValidateKey`, L207-213) — public diagnostics must
never expose platform player IDs.

---

## 9. `screen_space` envelope and anchors

Screen-space presentation anchors flow out through `window.gamingCouchScreenSpace`, one envelope per
frame (`GCRuntimeScreenSpaceOutput`, L908-1004). Anchors are produced by HUD components
(`Runtime/Hud/GCHud.cs`).

**Envelope** (`BuildEnvelopeJson`, L936-980):

```json
{ "type": "screen_space", "v": 1, "frame": 1234, "ms": 5678, "anchors": [ /* anchors */ ] }
```

**Anchor** (`GCRuntimeScreenSpaceAnchor.AppendJson`, L864-905):

| Field | Type | Notes |
|---|---|---|
| `type` | string | `playerOverhead` / `playerPosition` (`GCRuntimeScreenSpaceAnchorTypes`, L36-46) |
| `playerIndex` | int | Non-negative |
| `x` | float | Clamped `0–1` |
| `y` | float | Clamped `0–1` |
| `offscreen` | bool | Whether the anchor is off the visible screen |

Anchors must be **unique per `(type, playerIndex)`** within an envelope (`ValidateUniqueAnchors`,
L987-1003); duplicates throw.

---

## 10. jslib bridge — `window.gamingCouch*` callbacks

Unity → host callbacks are declared in `Plugins/GamingCouch.jslib` (L1-114). All eight:

| Callback | Payload | Fired when | Provenance |
|---|---|---|---|
| `gamingCouchInstanceStarted` | *(none)* | Boot startup signal | jslib L2; `GamingCouch.cs:237` |
| `gamingCouchRegisterRuntimeInfo` | runtime-info object | Baked identity forwarded before splash | jslib L10 |
| `gamingCouchRegisterUnityBuildInfo` | build-info object | Optional baked build diagnostics | jslib L28 |
| `gamingCouchSetupDone` | *(none)* | Game called `SetupDone()` | jslib L49 |
| `gamingCouchSetupHud` | HUD config object | HUD configured for hosted rendering | jslib L57; `GCHud.cs:178` |
| `gamingCouchSendProjectInfo` | project name string | Boot, right after `instanceStarted` — **the host does not implement this handler; the call is inert** (see below) | jslib L74; `GamingCouch.cs:415` |
| `gamingCouchRuntimeMessages` | `runtime_messages` envelope (§7) | Batch flush | jslib L83 |
| `gamingCouchScreenSpace` | `screen_space` envelope (§9) | Per frame | jslib L99 |

`registerUnityBuildInfo`, `sendProjectInfo`, `runtimeMessages`, and `screenSpace` are no-ops if the
host has not defined the handler (silent early-return); the others log a console error.

`gamingCouchSendProjectInfo` currently has **no receiver anywhere in the platform** — no client, SDK,
backend, or DevApp code defines `window.gamingCouchSendProjectInfo`. Every hosted player build still
makes the call at boot (`Application.productName`, non-editor branch of `Start`), where it takes the
silent early-return above, so the notification is fire-and-forget with nothing depending on it. It is
kept as a declared surface a future host may pick up; nothing in the package changes behavior either
way, and it is not a `gameProtocolVersion` concern — a bump is reserved for contract changes that
games and SDK adapters cannot translate (§13).

---

## 11. Sidecars and template gating

WebGL export writes two sidecars with deliberately different weights.

### `gc.runtime-info.json` — narrow identity gate

Written to the build output root next to `index.html` **only** when the Gaming Couch template
(`PROJECT:GamingCouch`) is selected, and deleted as stale on other-template WebGL builds
(`Editor/GamingCouchWebGLRuntimeInfoSidecar.cs`). Shape (`Runtime/GCRuntimeInfo.cs:8-14`,
serializer L16-36):

| Field | Type |
|---|---|
| `platform` | string (`"unity"`) |
| `packageName` | string (from `package.json`) |
| `packageVersion` | string (from `package.json`) |
| `gameProtocolVersion` | int (`1`) |

```json
{ "platform": "unity", "packageName": "com.example.game", "packageVersion": "1.2.3", "gameProtocolVersion": 1 }
```

The same payload is baked into the runtime resource `Runtime/Resources/GamingCouchRuntimeInfo.json`
and forwarded before splash via `window.gamingCouchRegisterRuntimeInfo` (kept in sync with
`package.json` by a release-time guard in the package repository).

### `gc.unity-build-info.json` — non-gating build diagnostics

Written for **every** WebGL build (any template), never part of the identity gate. Section shape
(`Editor/GCUnityBuildInfoSidecar.cs`, class `GCUnityBuildInfoSidecar` L465-472):

| Section | Type | Contents |
|---|---|---|
| `schemaVersion` | int | Diagnostics schema version (own version, independent of `gameProtocolVersion`) |
| `generator` | string | Generator identity |
| `identity` | object | `platform`, `packageName`, `packageVersion`, `gameProtocolVersion` (L474-481) |
| `buildEnvironment` | object | `unityEditorVersion`, `target`, `targetGroup`, `webGL{template,settings}`, `host{editorPlatform,osFamily,operatingSystem}` (L483-506) |
| `buildResult` | object | Build summary + path-normalized/redacted output path (L508-522) |

Build diagnostic paths are normalized before serialization: build-output-relative, project-relative,
`${USER_HOME}`-prefixed, or redacted — never emitted verbatim.

---

## 12. Web export template contract

The Gaming Couch web export template is a production/upload shell only. Constants at
`Editor/GamingCouchWebGLExportSetup.cs:208-212`:

- Template name `GamingCouch`; selected as `PROJECT:GamingCouch` (`ProjectTemplateIdentifier`).
- Installed to the project-local `Assets/WebGLTemplates/GamingCouch` (no-overwrite install).
- The shell is `index.html` only — it shows loading progress and errors, and provides **no**
  standalone playtest harness, no GamingCouch JS callback shims, no local fixtures, no controller
  simulation, and no DevApp communication (`Editor/WebGLTemplates/GamingCouch/index.html`).

WebGL compression is intentionally disabled in the export profiles — do not "fix" it to Brotli.

---

## 13. `gameProtocolVersion` policy

`gameProtocolVersion` identifies the JS↔Unity integration contract; `packageVersion` is diagnostics
only. **Bump `gameProtocolVersion` only when this wire contract itself changes** — not for package
metadata, sidecar generation, or upload-validation changes. Documenting existing schemas (this
document) does not change the contract and requires no bump.

The current recommendation is to keep `gameProtocolVersion` at `1` when the client/SDK already
translates legacy hosted payloads into current Unity payloads before invoking the package. Any bump
is always a release-owner decision.

---

## 14. Upload validation (cross-repo expectation)

> **Cross-repo note — main-repo behavior, not enforced by this package.** The Gaming Couch main repo
> owns upload processing. This section records what this package *expects* of it, not code that runs
> here.

Main-repo upload processing preserves the root `gc.runtime-info.json` and, for Unity uploads,
**requires and validates** it: `platform: "unity"`, non-empty `packageName`, SemVer `packageVersion`,
and `gameProtocolVersion: 1`. `gc.unity-build-info.json` is preserved when present but is **not**
required or validated in this slice. The hosted SDK reads `gc.runtime-info.json` before
`createUnityInstance` and rejects startup if the later runtime callback identity differs.

---

## Appendix A. Unsupported online-multiplayer surface

> **Internal migration surface — not a supported runtime contract.** Reserved for temporary internal
> migration of legacy games that already used the old multiplayer path. Do not use for new work. The
> package [README](../README.md) says only "Online multiplayer is not currently supported."

Default package builds keep `GamingCouch.OnlineMultiplayerSupport` as a false-returning compatibility
probe (`GamingCouch.cs:90`); `OnlineMultiplayerServerReady()` and `OnlineMultiplayerClientReady()`
throw unsupported-API errors and emit `gc.api.unsupported_multiplayer_api` diagnostics
(`CreateUnsupportedMultiplayerApiException`, `GamingCouch.cs:618-632`). The Netcode for GameObjects
helper assembly is not compiled.

The legacy path compiles only under the `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` scripting define, which
also enables the NGO transport jslibs (`Runtime/Unity/NGO/Transport/GCServer.jslib`,
`Runtime/Unity/NGO/Transport/GCClient.jslib`). Under the define, `mode`/`isServer` from
`GCSetupOptions` (§2) select the server/client role.
