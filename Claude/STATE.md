# STATE.md — current state of the game

**Last updated:** 2026-07-23

## Core loop

Side-scrolling puzzle platformer. Play a cat with 9 lives; get from entry (A) to the green
exit (B). Each life has a death countdown; at 0 (or press Q to sacrifice) the cat dies and
leaves a **corpse** — a physics box. Corpses hold pressure plates and act as platforms.
Respawn is at the entry; corpses persist. Run out of 9 lives → the level resets (corpses cleared).
8 levels play back-to-back (`Level_1` … `Level_8_Springboard`).

## What works right now

- **Movement** — `PlatformerMotor` (plain C#): accel/decel, coyote time, jump buffer, snappy fall
  gravity. Driven by `PlayerController` (CharacterController, in Update).
  **Jump is always charge-and-release**, moving or not: press starts the charge, release fires it,
  height lerps `baseJumpHeight`→`jumpHeight` over `jumpHoldTime`. A tap shorter than
  `quickJumpMaxHold` is the quick jump (base height, `QuickJump` anim); longer counts as charged
  (`JumpRelease` anim). While charging you plant your feet, but only after a `chargeGraceTime`
  window where you still move at `chargeGraceSpeedMultiplier`, then coast down at
  `chargePlantDeceleration`. `Charging` is true any time jump is held on the ground, so the
  `ChargingJump` animator state plays from Idle *and* from Movement.
- **Death countdown** — `LifeTimer`; per-level duration, resets each life, tick SFX under 3s.
- **Corpses** — `Corpse.cs`: physics box, freezes to a solid platform after settling. **Normal
  corpses are no longer bouncy** — just a solid climbable platform (the base, power-less case).
- **Upgrade pickups** — one-time trigger pickups (`UpgradePickup.cs`, prefabs
  `Assets/Prefabs/Upgrade_Trampoline.prefab` / `Upgrade_Carry.prefab`, not yet placed in any
  level — drag into a level prefab to use) that arm an effect for the player's *current life
  only*; consumed together the moment that life ends (death), reverting to the powerless base.
  Picking up a second upgrade replaces the pending one.
  - **Trampoline** — the *next* corpse this life spawns as a bouncy trampoline (`CorpseKind`)
    instead of a plain platform; only a fast fall triggers the bounce (`trampolineBounce*` in
    config), a plain walk-on/landing doesn't launch you.
  - **Carry** — for the rest of this life, right-click (or gamepad RB) picks up the nearest
    settled corpse (any type) within range and holds it (`CorpseCarry.cs`, slower move speed
    while held, `carrySpeedMultiplier`); right-click again gently sets it down in place;
    left-click (or RT) throws it toward the mouse direction (direction only, not power) —
    a `LineRenderer` previews the ballistic arc while held. Dying while holding drops the
    corpse in place.
- **Pressure plates + movers** — `PressurePlate` (trigger-collider weight check) drives
  `LinkedMover` gates/lifts via a dragged-in `plates` list (no more link ids).
- **Moving platforms** — `MovingPlatform.cs`: shuttles between its start position and
  `start + moveOffset` at `speed`, reversing at each end (optional `waitTime` pause). With no
  `plates` dragged in it moves continuously forever; with plates linked it only moves while any
  is pressed (freezes in place otherwise) — same start/stop puzzle idea as `LinkedMover`, but
  looping instead of open/close. `PlayerController` rides it by tracking the platform's position
  delta each frame while grounded on it (works for horizontal and vertical travel); corpses are
  **not** carried by it yet. Prefab: `Assets/Prefabs/MovingPlatform.prefab` (orange, default
  scale 3×0.5×3, default path +4 on X) — drag into a level like any other block, tweak
  `moveOffset`/`speed`/`waitTime`/`plates` in the Inspector.
- **Levels are hand-editable in the Unity Editor**, not code. Each level is a **prefab**
  under `Assets/Levels/` built from draggable building blocks in `Assets/Prefabs/`: `Block`,
  `Block_Alt`, `Plate`, `Gate`, `Lift`, `ExitPad`, `Entry`, `MovingPlatform`. Double-click a level
  prefab to edit it in prefab mode — move/scale/duplicate blocks, drag plates onto a Gate/Lift's
  `Plates` list in the Inspector, done. `LevelRoot` (on the prefab root) holds
  `levelName`/`hint`/`timer`/`entry`/`exit`. `GameManager.levelPrefabs` is a reorderable list —
  drag prefabs in to add/reorder/remove levels; it instantiates one at a time at runtime.
- **Main menu + pause menu** — `MenuUI.cs` (mouse-driven, real UGUI `Button`/`Slider`, spawns an
  `EventSystem` + `InputSystemUIInputModule`). Main menu: Start/Continue (resumes at
  `SaveData.HighestUnlockedLevel`, saved via `PlayerPrefs` so it survives WebGL), Level Select
  (locked levels greyed out unless `GameConfig.unlockAllLevelsForTesting`), Settings (master/
  music/effects volume sliders, saved + applied live). Pause (Esc) adds Continue/Level
  Select/Settings/Back to Menu, sharing the same Level Select and Settings screens.
- **Parallax** — `ParallaxLayer.cs`: reusable component, one per visual layer. Multiplier is
  apparent on-screen movement vs the play surface: `1` = moves 1:1 with camera (world-static),
  `>1` foreground, `<1` background, `0` pinned to camera. Per-axis enable + H/V multipliers, all
  Inspector-tunable. Anchors on the first active frame (after the camera snaps) and re-anchors on
  `OnEnable`, so each level is independent and layers can be duplicated freely; no reset
  registration needed. Uses `Camera.main` unless a `cameraTransform` is set. Every level prefab now
  has a `Parallax` child (centered on the level's bounds) holding 5 instances of
  `Assets/Prefabs/ParallaxLayer.prefab` — `Foreground_1/2` (Z −6/−3, mult 1.3/1.15, low-alpha
  tints so gameplay shows through) and `Background_1/2/3` (Z 8/16/28, mult 0.75/0.5/0.3). Depth
  ordering is by world Z under the perspective camera (transparent sprites sort by camera distance,
  and level geometry occludes backgrounds via the depth buffer). Layers share
  `Assets/Sprites/Parallax/Parallax_Placeholder.png` (swap each layer's sprite for real art later).
- **Death traps** — `DeathTrap.cs`: drop on any hazard (spikes/lava/saw/poison) to make it an
  instant kill. Per-instance Inspector fields: knockback away-from-trap or fixed direction,
  force, vertical lift, control-lock duration, optional hit SFX/VFX overrides. On player contact
  (trigger or collision) it builds a `DeathInfo` and calls `PlayerController.Die(info)` — the
  reusable instant-kill entry point any future hazard reuses.
  - **Flow**: `PlayerController.Die()` sets `IsDying`, applies the knockback velocity straight to
    the motor (existing gravity/deceleration carries the arc — no separate knockback physics
    needed), raises `GameEvents.TrapHit` (drives the new `HitReaction` animator state + default
    procedural hit SFX, or the trap's override clip/prefab if set), then after
    `controlLockDuration` raises `DeathSequenceReady`. `GameManager` subscribes to that and calls
    `DieAutomatic()` — **identical result to falling off the map** (unrecoverable, corpse spawns in
    place, respawn at entry/death-spot per existing rules) except the player dies in place instead
    of falling. `GameManager.TickPlayer` feeds zeroed input and skips fall/sacrifice checks while
    `player.IsDying`.
  - **No checkpoints** — respawn location is unchanged (level entry, or death-spot if
    `respawnAtDeathSpot`); user explicitly deferred checkpoints as out of jam scope.
  - **Not yet placed on any hazard** — the component exists and compiles clean; drag it onto
    spikes/lava/saw geometry (with a trigger or solid collider) to activate. Not playtested in
    Play mode yet.
  - Animator: new `Hit` trigger + `HitReaction` state (placeholder empty clip, same pattern as the
    other 14 states) on `AnyState`; interrupts into `DeathRespawn` on `Die`, or exit-times back to
    `Idle1` if `Die` never comes.
- **Lava waterfall** — `LavaWaterfall.cs` (`ExecuteAlways`) + `Assets/Prefabs/LavaWaterfall.prefab`.
  Drag the prefab into a level; the root raycasts down (`hitMask`/`castRadius`/`maxLength`) and
  stretches the `Stream` quad to the hit distance, feeding the world length into the shader via a
  `MaterialPropertyBlock` (`_Length`) so flow density stays constant at any height. `NineLives/
  LavaWaterfall` shader (`Mat_LavaWaterfall`) is procedural (no textures): downward fbm flow,
  distortion, HDR emission, soft side/top/bottom alpha fades (wobbly dissolving bottom, no clip),
  edge glow — all Inspector-tunable. The `Splash` child particle system snaps to the exact impact
  point (`NineLives/LavaSplash` additive sprite shader / `Mat_LavaSplash`) and disables itself when
  nothing is hit. Angle a fall by rotating the root; set `updateInterval` >0 or leave 0 for
  per-frame recast (moving floors). Not yet placed in any level or playtested in Play mode.
- **HUD** — `HUD.cs`: level label, big timer + bar, lives pips, hint, banners.
- **Audio** — `ProceduralAudio.cs`: all SFX generated in code (jump/bounce/death/plate/win/etc).
  Background music now works: `GameManager.musicSource` (public `AudioSource` field, assigned +
  wired on the scene's `GameManager`) is set to loop and starts playing in `Start()`, volume
  driven by `SaveData.MusicVolume` and the Settings music slider (`MenuUI.BuildUI` now takes a
  `music` `AudioSource` param alongside `sfx`, live-updates `.volume` on slider change).
- **Scene is now the source of truth (scene-based architecture)** — `Assets/Scenes/Game.unity`
  holds every persistent object pre-placed, each on its own root: `GameManager`, `MainCamera`,
  `Player` (starts disabled, has `CorpseCarry`), `HUD` (disabled), `MenuUI`, `Sun`, `Corpses`
  (corpse-pool parent), and `Levels` → all 8 level prefab-instances (spaced 300u apart on X, all
  disabled). Nothing is instantiated/destroyed at runtime except pooled corpses. `GameManager`
  fields (`player`/`cam`/`hud`/`menu`/`levels[]`/`corpseRoot`/`corpsePrefab`) are scene refs.
  Level switching = disable all, enable the target, reset it. `StartLevel` walks the level's
  `GetComponentsInChildren<ILevelResettable>(true)` and calls `ResetToInitial()`.
  - **`ILevelResettable`** (`ILevelResettable.cs`) — implemented by `MovingPlatform`,
    `LinkedMover`, `PressurePlate`, `UpgradePickup`, `LevelExit`. Restores each back to its
    `Awake`-captured authored state (position, closed gate, released plate, un-taken pickup,
    un-triggered exit) — the reset Destroy/Instantiate used to give for free.
  - **Corpses are pooled** under `Corpses`: `GameManager.GetPooledCorpse()` reuses a disabled one
    or grows the pool (bounded by `livesPerLevel`); `ClearCorpses()` disables them all;
    `Corpse.Init()` now fully resets state so a reused corpse comes back clean. Positioned while
    inactive so activation starts PhysX fresh at the death spot (no sweep-through-floor).
  - Globals reset for free via the existing `StartLevel` path: player via `Spawn`, camera via
    `Snap`, HUD via `SetLevel`/`BuildSouls`, timer via `Restart`. Sun light + ambient are scene
    settings now, not built in code.

## FX + animation foundation (placeholder hooks, ready for a polish pass)

Event-driven so effects never live inside gameplay logic. Gameplay only *raises* events; the
presentation layer *listens*.

- **`GameEvents.cs`** — static hub, the decoupling boundary. Events: Footstep, Jumped, Landed,
  HardLanded, SacrificeDeath, CorpseSpawned, PoofDeath, LevelEntered, LevelExited, plus throw
  hooks (ChargeStarted, Thrown) that nothing raises yet (mechanic unbuilt). Raised from
  `PlayerController` (jump/land/hardland/footstep-cadence) and `GameManager` (deaths → soul vs
  poof, corpse spawn, level entry/exit).
- **`FXManager.cs`** (scene object `FXManager`) — the only place events become effects. Subscribes
  to all events, spawns a pooled placeholder `OneShotVFX` particle at the event position and plays
  the matching SFX. Ten VFX prefab fields wired in-scene → `Assets/VFX/FX_*.prefab` (shared
  `Mat_VFX`, Sprites/Default). SFX come from new `ProceduralAudio` placeholder tones.
- **Jump/land/death SFX moved out of `GameManager`** into FXManager (via events) — no double-play.
  Bounce/plate/win/fail/tick still play in GameManager (not part of the FX spec).
- **`PlayerAnimator.controller`** (`Assets/Animations/`) — 14 placeholder states (Idle1 default,
  Idle2, Movement, QuickJump, ChargingJump, JumpRelease, Falling, HardLanding, CorpseState,
  DeathRespawn, LevelEntry, LevelExit, HoldToThrow, Throw) + params + a working transition
  skeleton. Clips are **empty** (they key a non-existent child, so they have length but move
  nothing → don't fight PlayerController's own scale/facing). Author real motion into
  `Assets/Animations/Clips/Anim_*.anim` later.
- **`PlayerAnimatorDriver.cs`** (on Player) — sets Speed/Grounded/Charging/Falling every frame and
  fires triggers off GameEvents. Exposes `AnimEvent_*` methods to wire as animation events on the
  real clips later (drive footstep/land/jump off the animation instead of physics).
- **Known limitation**: `Die()` deactivates the player *before* raising death events, so the
  animator's Die/Corpse triggers are missed (the driver is disabled) — fine for now since the
  player is invisible during death; FXManager (always active) still plays the death VFX/SFX. To
  play a death animation on the player later, raise the death event before deactivating.
- **Not yet playtested in Play mode** — compiles clean, all refs verified wired via CLI.

## Everything visualized is now a prefab/material asset (drag-and-drop art later)

Prompted by the user: anything a future art pass would touch needs to be an Inspector-editable
asset, not a `GameObject.CreatePrimitive` + generated `Material` in code.

- `Assets/Prefabs/Player.prefab` — CharacterController + PlayerController + a `CatMesh`/`Ear`
  child mesh using `Mat_Player`/`Mat_Player_Ear`. `PlayerController.Configure()` only resizes the
  existing mesh from `GameConfig` now; it no longer builds it.
- `Assets/Prefabs/Corpse.prefab` — Rigidbody + BoxCollider + Corpse, with the four material
  variants (`Mat_Corpse`, `Mat_Corpse_Settled`, `Mat_Corpse_Trampoline`,
  `Mat_Corpse_Trampoline_Settled`) wired as serialized fields on `Corpse` instead of generated
  in `Init()`/`Freeze()`.
- `Assets/Prefabs/HUD.prefab` / `Assets/Prefabs/MenuUI.prefab` — the full Canvas hierarchies
  (every text/Image/Button/Slider) are hand-built prefabs now; `HUD.cs`/`MenuUI.cs` are just
  `[SerializeField]`-bound data-binding components with no `new GameObject(...)` UI construction
  left. Both use a shared swappable 9-sliced sprite, `Assets/Sprites/UI_Panel.png`.
  - **All player-facing text is TextMeshPro** (`TextMeshProUGUI`, font `LiberationSans SDF`) — the
    legacy `UnityEngine.UI.Text` was fully migrated out. HUD/MenuUI text fields are typed `TMP_Text`.
    The only `UnityEngine.UI.Text` left in the project is inside DOTween's optional module files
    (third-party, not used by our UI). To restyle, swap the TMP font asset / material.
- `Plate.prefab`'s cap and the two `Upgrade_*.prefab` pickups now have their materials
  (`Mat_Plate`/`Mat_PlateHit`, `Mat_Upgrade_Trampoline`/`Mat_Upgrade_Carry`) assigned directly on
  the prefab instead of via `GreyboxFactory.Make()` in `Awake()`.
- `GameManager` gained four Inspector fields — `playerPrefab`, `corpsePrefab`, `hudPrefab`,
  `menuPrefab` — and instantiates those instead of building GameObjects by hand. All four are
  already wired on the scene's `GameManager` component.
- **Remaining gap**: `CorpseCarry.cs` still makes its throw-trajectory `LineRenderer` material
  via `GreyboxFactory.Make()` at runtime — left as-is since it's a debug-style preview line, not
  an object with its own visual identity. Flag it if that changes.
- `GreyboxFactory.Box()`/`.Make()` are still there for anything not yet converted, but nothing in
  the shipped systems calls them anymore except the line above.

## Half-done / known broken

- **Scene-based architecture not yet playtested in Play mode** — compiles clean, all scene refs
  verified wired via the CLI, but nobody has entered Play mode since the conversion. Highest-risk
  spots to watch: (1) corpse pooling physics (reused corpse settling correctly at the death spot,
  no leftover kinematic/collider state); (2) level reset on restart / re-entry (platforms, gates,
  plates, taken pickups all returning to authored state); (3) globals (HUD/camera) showing the
  right per-level state with no carryover.
- Not yet playtested since the menu + asset-ification pass either — no one has clicked through
  Start → Level Select → Settings → Pause → Back to Menu in Play mode yet.
- **Upgrade pickups aren't placed in any level yet** — the prefabs exist but no level has one
  dragged in. Needs an Editor pass to actually place them where they're meant to teach/solve.
- Not yet playtested for *feel* — movement/jump/timer numbers are first-guess. Tune in
  `Assets/Settings/GameConfig.asset` (live-editable in play mode).
- Idle-dying at the entry stacks a corpse where you respawn (harmless, but looks odd).
- Upgrade pickups now render with real `Mat_Upgrade_Trampoline`/`Mat_Upgrade_Carry` materials
  (emissive magenta/teal) — still flat greybox color, real art assets planned later, per user.

## Next up (ordered)

1. Playtest the new main menu / pause / level select / settings flow in the Editor (mouse
   clicks, slider drag, locked-level greying, Continue resuming at the right level).
2. Place `Upgrade_Trampoline`/`Upgrade_Carry` prefabs into levels and playtest the upgrade loop.
3. Playtest feel; tune `GameConfig` (speed, jumpHeight, timeToApex, per-level Timer).
4. Confirm each level is solvable as intended; adjust in the Editor via the level prefabs.
5. Juice: squash/stretch, death poof particle, screen shake — Sunday-only per CLAUDE.md.
   FX/animation hooks now exist (see FX section) — polish = filling `Anim_*.anim` clips and the
   `FX_*` particle prefabs with real art, no new plumbing needed.

## Decisions already made — don't re-litigate

- Respawn at entry; sacrifice via Q; timer fixed per level & resets each life; out-of-lives
  restarts the level clearing corpses. (User chose all four.)
- **Levels are hand-editable prefabs**, not procedural C# data (superseded the original
  code-only approach after the user asked for in-Editor level editing). Plate→mover linking is
  by dragging plate references into a `Plates` list, not by matching id numbers.
- **Upgrade system**: one-time pickup, replaces any unspent upgrade, effects last only the
  current life and are consumed together on death. Carry ability can pick up *any* settled
  corpse regardless of type. Throw uses mouse-direction aiming with fixed power (not
  distance-scaled). All confirmed by user, don't re-litigate the control scheme.
- **Progress/settings persistence is a single `SaveData` static class over `PlayerPrefs`**
  (`HighestUnlockedLevel`, `MasterVolume`, `MusicVolume`, `SfxVolume`), chosen for WebGL
  compatibility. `HighestUnlockedLevel` doubles as both the Level Select unlock gate and the
  Continue-button resume point — don't split these into two fields without a reason.
- **Everything visualized must be a prefab/material/sprite asset**, never a
  `GameObject.CreatePrimitive` + code-generated `Material`/sprite — user wants a later art pass
  to be pure asset-swapping, no code changes. Applies project-wide, not just to menus.

## Gotchas hit so far

- `eval_file` body allows **no `using` directives** — fully-qualify types (`UnityEditor.…`).
  Local functions inside the eval body work fine, though. It also requires the file to have a
  **`.cs` extension** — `.csx` is rejected outright.
- `capture_game_view` does **not** capture Screen-Space-Overlay UI; the HUD is there, just not
  in that screenshot. It returns base64 inline (doesn't write the file); decode it manually.
- **Never delete `.cs`/`.meta` files directly on disk (`rm`) while the Editor is open** — it
  crashed the Editor process outright once, mid-reimport. Prefer `AssetDatabase.DeleteAsset`
  via `eval_file`, or accept the Editor needs a manual reopen after.
- Building prefabs via `eval_file` + `SerializedObject`/`PrefabUtility.SaveAsPrefabAsset` is much
  faster than one MCP tool call per GameObject/component — write one script per prefab, wire
  `[SerializeField]` fields by name with `SerializedObject.FindProperty(...).objectReferenceValue`,
  and it'll throw (script reports failure) if a field name typo'd, so a clean "done" result is a
  real correctness signal, not just "it ran."

## Key files

| File | What |
|---|---|
| `Assets/Scripts/GameConfig.cs` | All tunable numbers (ScriptableObject). |
| `Assets/Scripts/SaveData.cs` | PlayerPrefs-backed progress + volume settings. |
| `Assets/Scripts/PlatformerMotor.cs` | Pure movement math, no Unity deps. |
| `Assets/Scripts/PlayerController.cs` | CharacterController + motor + corpse bounce; visual is `Player.prefab`. |
| `Assets/Scripts/GameManager.cs` | Flow: menu state, lives, countdown; enables/disables/resets pre-placed scene objects; pools corpses. |
| `Assets/Scripts/ILevelResettable.cs` | `ResetToInitial()` contract for level content; GameManager walks it on level (re)entry. |
| `Assets/Scripts/LevelRoot.cs` | Marks a level's root; entry/exit/timer/name/hint. |
| `Assets/Scripts/Corpse.cs` / `PressurePlate.cs` / `LinkedMover.cs` / `MovingPlatform.cs` | The mechanics. |
| `Assets/Scripts/DeathTrap.cs` / `DeathInfo.cs` | Generic instant-kill hazard component; drop-on knockback/hit-reaction config. |
| `Assets/Scripts/ParallaxLayer.cs` | Reusable per-layer parallax; `ParallaxLayer.prefab` + a `Parallax` group in every level. |
| `Assets/Scripts/GameEvents.cs` | Static event hub: gameplay↔FX/animation decoupling boundary. |
| `Assets/Scripts/FXManager.cs` / `OneShotVFX.cs` | Event→VFX+SFX; pooled placeholder particles. |
| `Assets/Scripts/PlayerAnimatorDriver.cs` | Drives `PlayerAnimator.controller` from player state. |
| `Assets/Animations/` / `Assets/VFX/` | Placeholder controller/clips + particle prefabs (swap for art). |
| `Assets/Scripts/HUD.cs` / `PauseMenu.cs` (class `MenuUI`) | Data-bound UI logic; visuals live in `HUD.prefab` / `MenuUI.prefab`. |
| `Assets/Prefabs/*.prefab` | Draggable building blocks + Player/Corpse/HUD/MenuUI/Upgrade_*. |
| `Assets/Materials/*.mat` | All palette materials — swap these for real art later. |
| `Assets/Sprites/UI_Panel.png` | Shared 9-sliced UI sprite used by HUD + MenuUI. |
| `Assets/Levels/*.prefab` | The levels — edit these directly in the Editor. |
| `Assets/Settings/GameConfig.asset` | The live tuning knobs. |
