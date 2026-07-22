# STATE.md — current state of the game

**Last updated:** 2026-07-23

## Core loop

Side-scrolling puzzle platformer. Play a cat with 9 lives; get from entry (A) to the green
exit (B). Each life has a death countdown; at 0 (or press Q to sacrifice) the cat dies and
leaves a **corpse** — a bouncy physics box. Corpses hold pressure plates and act as platforms.
Respawn is at the entry; corpses persist. Run out of 9 lives → the level resets (corpses cleared).
Three levels play back-to-back.

## What works right now

- **Movement** — `PlatformerMotor` (plain C#): accel/decel, coyote time, jump buffer, variable
  jump height, snappy fall gravity. Driven by `PlayerController` (CharacterController, in Update).
- **Death countdown** — `LifeTimer`; per-level duration, resets each life, tick SFX under 3s.
- **Corpses** — `Corpse.cs`: physics box, freezes to a solid platform after settling. Player
  bounces off them if landing fast (`corpseBounce*` in config).
- **Pressure plates + movers** — `PressurePlate` (OverlapBox weight check) drives `LinkedMover`
  gates/lifts by link id. L1 lowers a gate, L2 raises a lift.
- **3 levels** — authored as data in `LevelData.cs`, built as greybox at runtime by `LevelRuntime`.
  L1 Weigh In (body-on-plate), L2 Step Up (lift/climb to high exit), L3 Two Down (gate + climb).
- **HUD** — `HUD.cs`, built in code: level label, big timer + bar, lives pips, hint, banners.
- **Audio** — `ProceduralAudio.cs`: all SFX generated in code (jump/bounce/death/plate/win/etc).
- **Scene** — `Assets/Scenes/Game.unity`: one `Game` object w/ `GameManager` + `GameConfig`.
  Everything else (camera, light, player, UI) is built at runtime. In Build Settings, index 0.

## Half-done / known broken

- Nothing broken. Verified: compiles clean, runs, death+corpse+HUD confirmed in play mode.
- Not yet playtested for *feel* — movement/jump/timer numbers are first-guess. Tune in
  `Assets/Settings/GameConfig.asset` (live-editable in play mode).
- Idle-dying at the entry stacks a corpse where you respawn (harmless, but looks odd).

## Next up (ordered)

1. Playtest feel; tune `GameConfig` (speed, jumpHeight, timeToApex, per-level Timer).
2. Confirm each level is solvable as intended; adjust layouts in `LevelData.cs`.
3. Juice: squash/stretch, death poof particle, screen shake — Sunday-only per CLAUDE.md.

## Decisions already made — don't re-litigate

- Respawn at entry; sacrifice via Q; timer fixed per level & resets each life; out-of-lives
  restarts the level clearing corpses. (User chose all four.)
- Levels are **procedural greybox from C# data**, not hand-built scene YAML. Keeps them tunable
  and avoids fragile `.unity` edits.
- Only mechanics: corpse-as-weight (plates→gates/lifts) and corpse-as-platform (bouncy). No more.

## Gotchas hit so far

- `eval_file` body allows **no `using` directives** — fully-qualify types (`UnityEditor.…`).
- `capture_game_view` does **not** capture Screen-Space-Overlay UI; the HUD is there, just not
  in that screenshot. It returns base64 inline (doesn't write the file); decode it manually.

## Key files

| File | What |
|---|---|
| `Assets/Scripts/GameConfig.cs` | All tunable numbers (ScriptableObject). |
| `Assets/Scripts/PlatformerMotor.cs` | Pure movement math, no Unity deps. |
| `Assets/Scripts/PlayerController.cs` | CharacterController + motor + corpse bounce. |
| `Assets/Scripts/GameManager.cs` | Flow: levels, lives, countdown, corpses, builds scene. |
| `Assets/Scripts/LevelData.cs` | The 3 level definitions + authoring helpers. |
| `Assets/Scripts/LevelRuntime.cs` | Instantiates a level as greybox. |
| `Assets/Scripts/Corpse.cs` / `PressurePlate.cs` / `LinkedMover.cs` | The mechanics. |
| `Assets/Scripts/HUD.cs` | Runtime-built UI. |
| `Assets/Settings/GameConfig.asset` | The live tuning knobs. |
