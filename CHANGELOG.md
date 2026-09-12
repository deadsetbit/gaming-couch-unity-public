# Changelog

## [Unreleased]

### Added

- The platform runtime contract and the DevApp / local-play contract now ship with the package, under `Documentation~/`, and are linked from the README. They are the JS-to-Unity wire spec and the `gc.dev.json` / `gc.platform.json` spec; previously they were readable only in the package's own repository.

### Changed

- Documentation, changelog and licence links in the package manifest, and every API reference link in the README, now point into this release's own folder under `https://deadsetbit.github.io/gaming-couch-unity-public/`. Each release's documentation is published separately and is never overwritten, so a pinned install reads the documentation for the version it actually has rather than for whatever shipped most recently.
- Every documentation page states the version it documents and carries a link to <https://gamingcouch.com>.

## [0.1.0-alpha.7] - 2026-09-10

### Changed

- Renamed `ApplyDevAppInput` to `ApplyExternalPlayerInput`. It is the shared entry point for hosted platform input as well as DevApp input, so the old name described half of what it does. Hosted platform inputs also report as `platform_input` rather than `devapp_input` in diagnostics, so the two sources can be told apart.
- Narrowed `IGCPlayerStore`'s collection properties to `IReadOnlyList`. `GCPlayerStore` keeps its `List` properties and satisfies the interface explicitly, so a game that declares the concrete store and calls `List` members on it is unaffected. The derived collections are rebuilt whenever a transition is accepted, so enumerating one while eliminating players still throws -- that hazard is now documented per property.
- Wire Example Game asks before moving an existing folder at one of the example paths to the Trash, matching what Create Example Scene has always done. All four entry points -- the menu item, the checklist action, the start-screen action and the method itself -- share one dialog, and declining is reported as a warning rather than a failure. The swap is also a single named undo group, so one undo restores all of it instead of part.
- A reused `index.html` whose contents do not match the packaged WebGL template is reported as differing instead of being accepted silently, which was hiding template drift across package upgrades. Nothing is overwritten.
- The DevApp connection's `serverUrl` is no longer a serialized inspector field.

### Removed

- Removed the Codex test bridge (`Editor/GamingCouchCodexTestBridge.cs`) and its `Tools/run-open-unity-tests.py` runner. The bridge let an agent run this package's tests inside an already-open Editor by polling a request file under the user's application-data directory. It was internal agent tooling that happened to ship inside the package's `Editor/` folder, and it was inert for anyone who had not opted in, so removing it changes no package behavior. The `.gamingcouch/codex-bridge.enabled` marker file and the `GAMINGCOUCH_CODEX_TEST_BRIDGE` environment variable no longer do anything and can be deleted. The Unity CLI covers package validation (`unity test`), and its `com.unity.pipeline` channel is the supported way to drive a live Editor; `AGENTS.md` documents the flow.

### Fixed

- Fixed the package failing to compile on Unity 6000.3 with `error CS1061: 'SerializedProperty' does not contain a definition for 'objectReferenceEntityIdValue'`. The scene-wiring missing-reference check gated that property behind `UNITY_6000_3_OR_NEWER`, but while the `EntityId` type ships in 6000.3, the `SerializedProperty.objectReferenceEntityIdValue` accessor only arrives in 6000.4. The guard is now `UNITY_6000_4_OR_NEWER`, which leaves 6000.3 on the deprecated but still available instance-ID path.
- Fixed the Codex test bridge activating in every Editor with this package installed. It was gated behind `#if UNITY_INCLUDE_TESTS`, which is active in a package consumer's Editor, so simply installing the package started a background file-polling bridge that created directories outside the project, wrote a token manifest into the user's application-data directory, and (on macOS/Linux) tightened permissions there. The bridge is now opt-in per host project -- a `.gamingcouch/codex-bridge.enabled` marker file or the `GAMINGCOUCH_CODEX_TEST_BRIDGE` environment variable -- and is completely inert without one.
- Fixed the test bridge restricting a directory it does not own: its permission walk rooted at the local application-data directory itself, so on macOS/Linux it chmod-ed `~/.local/share` to 0700. It now creates, symlink-rejects and 0700-restricts only `<localappdata>/Gaming Couch` and below.
- Fixed `GamingCouch.Instance.Clear()` dropping the run-scoped player-index mapping, which did not reset the run but permanently disarmed it: every later platform inputs message was silently dropped and `GameOver()` could never submit a placement. The round-reset pattern -- `Clear()` followed by `SetupPlayers` without a fresh `Play()` -- works again.
- Fixed `GCPlayerStore.Clear()` leaving player game objects alive outside play mode. It called `Object.Destroy`, which defers outside play mode (and logs "Destroy may not be called from edit mode"), so the store emptied while the objects lingered in the scene. It now destroys immediately outside play mode and iterates a snapshot, so a `GCPlayer` subclass that touches the store from `OnDestroy` cannot break the loop mid-clear. Player builds are unaffected -- this only showed up in the Editor and editor tooling.
- Fixed the DevApp devtool router applying zeroed values for keys a message omits. `JsonUtility` cannot express an absent key, so a payload-less `timescale_state` was applied as timescale 0 (clamped downstream) and unpaused, silently unpausing a paused game. Missing-key devtool messages are now ignored.
- Fixed the `GamingCouch` inspector re-running the full Start Screen readiness scan -- disk reads, a whole-scene component walk, GameView reflection and WebGL template stats -- on every repaint. It is now cached and refreshed at most twice a second.
- Fixed customised keyboard bindings resetting silently on package upgrade. The serialized input fields were renamed (`a0`/`a1`/`b0`/`b1` to `axisX`/`axisY`/`buttonPrimary`/`buttonSecondary`) with no `[FormerlySerializedAs]`, so Unity dropped the stored values on deserialize.
- Fixed `SetupPlayers` aborting mid-roster when a player's `type` differed in case, for example `"Bot"`. `_InternalSetPlayerProperties` used a case-sensitive throwing `Enum.Parse` while the rest of the roster pipeline used the tolerant resolver, and the rethrow left a deactivated instantiated object behind. It now uses the same resolver.
- Fixed template setup failing outright in a project that contains its own type named `GCPlayer`. The stock-player lookup returned the first simple-name match across every assembly, so a namesake shadowed the real type; namesakes that fail the base-type check are now skipped.
- Fixed seat names being persisted to `gc.dev.json` untrimmed. A padded name passed Unity's 1-8 character rule, but the DevApp checks the minimum on the trimmed value and the maximum on the raw one, so it rejected the file -- and that rejection is whole-file: the project dropped to action-required, seat config became uneditable and the local runner refused to start, recoverable only by hand-editing the JSON. Trimming now happens in the draft's file conversion, which write, validation and the dirty comparison all pass through, and an already-padded file reads as dirty on load so applying normalizes it.
- Fixed the game-over result of every run after the first being silently dropped while the DevApp is attached. `runId` was minted on each WebSocket open and a Unity restart keeps the socket open, so the id never changed between runs -- the DevApp refuses a second game-over for the same id and only clears per-run diagnostics when it changes. The id is now minted once per run, restart included, and survives a reconnect.
- Fixed the runtime rejecting DevApp messages that were not byte-identical to the compact form. The inbound router matched the exact bytes of `"type":"gcdevtool"` before parsing, so a pretty-printed or spaced message was dropped. The probe is now a fast path that falls through to the parser, and the payload key probes tolerate whitespace the same way.
- Fixed concurrent `SendAsync` calls on one `ClientWebSocket` when two runtime outputs landed in the same frame, which both .NET and Mono reject and which surfaced only through logging that is off by default. Sends now go through one queue drained by a single pump.
- Fixed two races around DevApp reconnects and capped inbound message reassembly. A receive completing just before a close could loop back onto the new socket, and a snapshot coroutine outliving its connection could clear a newer send's in-flight flag; both now bail on a connection epoch. The fragmented-message accumulators grew unbounded until `EndOfMessage` and are now capped, with the remainder discarded so a truncated tail is never reassembled into a message of its own.
- Fixed a malformed platform pause payload throwing inside the JS-to-Unity dispatch. `GamingCouchPause` parsed with a throwing `bool.Parse` while the sibling inputs path was already hardened.
- Fixed `GCPlayerStore.Clear()` leaving the store half-cleared: it never unsubscribed `AcceptedTransition`, and a game-destroyed GameObject could abort the loop, after which the next `AddPlayer` threw.
- Fixed the bake-time JSON writer shipping in player builds. `GCRuntimeInfo.cs` held an editor-only writer that was not `#if`-guarded even though only Editor code calls it; it is now wrapped in `UNITY_EDITOR`.
- Fixed a re-entrant log capture loop with `webSocketLogging` and full log capture both on: a `[GCDevApp][WebSocket]` line became a diagnostic, became a runtime message, and was logged again in the same frame.
- Fixed `gc.dev.json` and the two sidecar files being written non-atomically while external tools and the 0.25s stamp poller read them. All three now write to a temp file and rename.
- Fixed a read-only `Assets` folder throwing straight out of `OnGUI`. `InstallTemplateFiles` caught nothing; filesystem failures are now blocked reasons naming the path. The jslib's runtime-messages, screen-space and setup-hud imports also parsed JSON bare while their siblings wrapped it, so malformed JSON threw out of the bridge.
- Fixed a successful WebGL apply raising a modal error dialog when a row had been deselected. Readiness folded deliberately-skipped rows into its blocking flag; the apply seam now softens its own status when every still-changed row is one the caller deselected, while readiness itself stays honest.
- Fixed the WebGL template shipping a hardcoded `<title>App</title>`.
- Fixed the inspector rendering validation issues through a hand-rolled formatter that dropped the gate prefix and path suffix the start-screen window shows, and fixed UNC paths being over-redacted because only drive-absolute paths took the Windows comparison.
- Fixed `CS0649` appearing in every consumer's console for `GCPlayer.playerName`.
- Fixed the start screen recomputing its scene list twice per repaint and allocating four `GUIStyle`s per row, and the pending-setup poller running a full assembly and type sweep on every editor tick on the default template path. The stock player type is now memoized, cached only on success so a tick during compilation stays free to find it later.

## [0.1.0-alpha.6] - 2026-07-18

### Changed

- Rebaked the WebGL runtime-info payload (`Runtime/Resources/GamingCouchRuntimeInfo.json`) and package version to `0.1.0-alpha.6`; `gameProtocolVersion` stays `1`.

### Added

- Added a one-command version-bump release protocol (`Tools/bump-version.py` plus `npm run release:*` shortcuts) that bumps `package.json`, re-bakes the runtime-info payload, verifies, commits, and creates the `unity-<version>` tag. This is maintainer tooling and does not change package runtime behavior.

### Removed

- Removed the Gaming Couch Unity Template link from the package documentation reference list.

## [0.1.0-alpha.5] - 2026-07-14

### Added

- Added a "Gaming Couch scenes" section at the top of the Start Screen that lists project scenes containing a `GamingCouch` component, highlights the active scene, and lets you open a pre-existing scene without opening each scene to detect it. Unity crash-recovery `_Recovery` backups are excluded from the list.
- Added a "Wire example game" action (Start Screen button and menu) that upgrades the barebones template scene in place into the full playable example, swapping the listener component and repointing the player prefab without overwriting existing work.

### Changed

- Restructured the generated example into two self-contained, swappable game scripts sharing one scene: a barebones `GCExampleTemplate` (wiring demo, stock `GCPlayer`) and a full `GCExampleGame` (playable loop, `GCExamplePlayer`). Each script is itself the platform listener and implements the `SendMessage` lifecycle methods directly, with no adapter or base class, and the example now compiles against the real runtime API so generation cannot silently drift from it. Renamed `GCGameExample` to `GCExampleGame` and `GCPlayerExample` to `GCExamplePlayer` accordingly.
- Changed "Create New Example Scene" to reset the example to a single canonical `GCExampleScene` instead of adding a scene alongside the existing one. The action is now idempotent: it moves previous example scenes and any leftover blocking folders to the Trash (recoverable) and reuses valid existing example scripts and the player prefab rather than deleting and regenerating them.
- Hid the Active Scene Name/Path summary in the Start Screen whenever the active scene already appears in the Gaming Couch scenes list, keeping it only as a fallback for an active scene with no `GamingCouch` component.

### Fixed

- Fixed the WebGL runtime attestation reporting a stale version: the baked `Runtime/Resources/GamingCouchRuntimeInfo.json` was left at `0.1.0-alpha.3` after the `0.1.0-alpha.4` version bump and is now rebaked to match the released package version.
- Fixed editor console noise and IMGUI layout errors during example scene setup by guarding the `[ExecuteInEditMode]` log initialization behind play mode and running Start Screen actions on the next editor tick instead of mid-`OnGUI`.
- Fixed example-folder cleanup so it is honestly recoverable: imported folders are moved to the Trash, a raw un-imported folder is removed only when empty, and a non-empty un-imported folder is refused rather than permanently deleted.

## [0.1.0-alpha.4] - 2026-07-09

### Changed

- Added WebGL runtime-info drift detection: exports now write canonical root `gc.runtime-info.json`, bake the same payload into an early `GamingCouchRegisterRuntimeInfo` callback before splash screen, keep `GamingCouchInstanceStarted()` payload-free, let the hosted SDK fail startup on sidecar/runtime drift, and make upload validation require and validate `gc.runtime-info.json`.
- Updated Unity build diagnostics: `gc.unity-build-info.json` now has runtime identity, build environment, host OS diagnostics, and build result sections; upload processing preserves it when present, but upload validation does not require or validate it.
- Documented the Unity package as strict-current at its runtime boundary: hosted play payloads must arrive with current active-player identity, runtime input must use `playerIndex`, and legacy `players[]`/`playerId` adaptation belongs in the Gaming Couch client/SDK before Unity is invoked.
- Clarified that source-seat mapping remains supported for local editor play because seats are local setup, not hosted platform identity.
- Documented retained obsolete Unity APIs as compile-time errors with migration guidance toward `GCPlayer.Index`, `playerIndex`, current player-state APIs, and current HUD/runtime-state paths.
- Renamed the clean WebGL export workflow to Gaming Couch web export settings across editor setup code, menus, tests, and documentation.

### Removed

- Removed the right stick input API: `GCControllerInputs.rightX`/`rightY` and the underlying `a2`/`a3` axis fields no longer exist.
- Removed unused controller input fields `b3` and DPad `b12`–`b15`; `leftX`/`leftY` now read the left stick axes only, without DPad fallback.

### Release Notes

- Protocol-version decision: the current recommendation is no `gameProtocolVersion` bump if client/SDK adapters already translate legacy payloads and send current payloads to this package. Any bump remains a release-owner/user decision based on rollout risk and adapter compatibility.

## [0.1.0-alpha.3] - 2026-05-26

### Added

- Added the Gaming Couch Start Screen with quick-start setup actions that scaffold a working example scene from scratch: the `GamingCouch` object, an example game listener, an example player prefab, generated example scripts, and the scene wiring between them.
- Added Start Screen project-readiness reporting so setup surfaces which pieces are still missing.

### Fixed

- Fixed editor keyboard input priority during local editor play.
- Fixed DevApp runtime game-over and reconnect handling.
- Fixed a WebGL export preview compile error.

## [0.1.0-alpha.2] - 2026-05-09

### Added

- Added root `gc.dev.json` sync for Unity editor local play settings.
- Added a file-backed `GamingCouch` inspector flow for entry, seed, and eight-seat roster edits.
- Added warning-only `gc.platform.json` light-read behavior when platform data is missing or invalid.
- Added valid platform data gates for Apply and Play, including Unity platform, selected entry, and entry `maxPlayers`.
- Added editor Play Mode and Gaming Couch restart gates that auto-apply valid non-conflicted drafts and block invalid or conflicted drafts.
- Added editor-only `com.unity.nuget.newtonsoft-json` dependency for structured JSON parsing and unknown-field-preserving `gc.dev.json` writes.
- Added Gaming Couch web export settings documentation for the Unity 6 workflow, project-local template install, release defaults, no-overwrite behavior, warning-only build-target policy, and v1 playtest exclusion.

### Changed

- This development line now targets Unity 6 for Gaming Couch web export settings and splash/logo removal.
- Unity editor local play settings now use root `gc.dev.json` as the source of truth instead of old scene-serialized entry/player settings.
- Unity requires an existing root `gc.dev.json`; it does not create, bootstrap, or repair local project JSON files.
- Unity editor local playtests may run with one enabled seat even when production platform data declares a higher `minPlayers`.
