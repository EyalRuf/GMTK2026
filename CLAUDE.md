# CLAUDE.md — <GAME NAME> (game jam, <DATES>)

<!--
  KEEP THIS UNDER ~110 LINES. It is re-sent on EVERY request, for three days.
  Output style and the git prohibition live in ~/.claude/CLAUDE.md — don't duplicate them here.
-->

## What this is

`<one sentence: genre, core verb, win condition>`

Unity `6000.4.1f1` · URP · 3D · new Input System
Jam: `<jam name>`, submission deadline `<date + time + timezone>`.

**Scope is fixed and small.** This ships Sunday. Prefer the ugly thing that works over the clean
thing that isn't done. Don't propose refactors, abstractions, or "we could later…" unless asked.

**Git is not your concern at all** (see `~/.claude/CLAUDE.md`). Don't run it, don't track what's
committed, don't mention it.

## Keep me honest

The user does not read process docs — **you are the process.** They're tired and time-boxed, so
notice these and say something. **One sentence, once. If they say do it anyway, do it and drop
it** — a nag that repeats is worse than no nag.

- **Session sprawl** — two-plus unrelated features in one session, or you're re-reading files
  from way back: "worth `/clear`ing before the next thing."
- **Scope creep** — the ask adds a system instead of finishing one, or won't fit the hours left.
  Say so with a rough cost and a smaller version. Don't refuse — flag once, then build it.
- **Same bug, 3+ attempts** — stop patching. Say the context is polluted and recommend `/clear`
  plus a fresh description with the error text.
- **Never been built** — if it's Saturday and the game has never produced a build, say so.
  Discovering a broken build on Sunday night is the classic jam death.
- **Feature freeze** — Sunday morning onward, only bugs and juice. Push back on new systems.
- **Repeat gotcha** — if something bites twice, add one line to Conventions below.
- **Asked to hand-edit `.unity` / `.prefab` / `.asset` YAML** — refuse, use the CLI instead.

## The Unity Editor — read freely, never write uninvited

The Unity CLI is installed and the Editor is usually open. See [UNITY_CLI.md](UNITY_CLI.md).

**Reading is always allowed — do it constantly, without asking.** This is the whole point of the
CLI and it costs the user nothing:

```powershell
unity status                                    # connected?
unity command console --tail 20 --level error   # did it compile?
unity command recompile                         # then poll recompile_status
```

Also free: `editor_status`, `get_serialized_fields`, `get_scene_hierarchy`, `find_assets`,
`find_gameobjects`, `search`, `screenshot` / `capture_game_view`, `get_*_settings`.

**Never ask the user whether something compiled.** Check it yourself. If the Editor isn't
running, say so and ask them to open it — don't guess.

**Writing to the Editor needs explicit instruction or permission.** The user manages the Editor
and needs to know its state. Before any of these, say exactly what you'd change and wait:

- `set_serialized_field`, `set_component_properties`, `add_component`, `remove_component`
- `create_asset`, `create_folder`, `write_text_file`, `delete_asset`, `move_asset`
- anything touching prefabs or scenes, including `save_scene` / `save_all`
- `editor_play` / `editor_stop` — this takes over their Editor
- `package_add` / `package_remove`, any `set_*_settings`, `set_authoring_root`
- `eval` / `eval_file` that mutates anything

**Already permission — don't re-ask:**
- The instruction names the change ("wire the HUD into PlayerController"). Just do it.
- They say go ahead, or grant a standing okay for the current task.

**Ask once**, batching everything you intend to change — not a question per field. Once approved,
**make the change yourself**; never hand back a list of Inspector clicks. Don't leave the Editor
dirty without saying so.

## Architecture rules (these exist to keep the feedback loop fast)

- **Logic in plain C# classes; MonoBehaviours stay thin.** A `MonoBehaviour` that just reads
  input and calls into a plain class can be verified without entering play mode.
- **Never hand-edit `.unity`, `.prefab`, or `.asset` YAML.** Enormous, fragile, GUID soup, and it
  burns tokens. Use the CLI's asset/prefab tools — they go through `AssetDatabase`.
- **Tunable numbers in one place** (`GameConfig` ScriptableObject or a static class) so the user
  can balance without a code round-trip.
- **Delete freely.** No dormant or feature-flagged fallback paths — that's a long-project habit
  that wastes jam time. Recovery is the user's problem, not a reason to hedge.

## Working agreement

- **One feature per session**, then the user `/clear`s.
- **Don't re-read files you've read this session.** Don't re-derive decisions already made —
  they're in [STATE.md](STATE.md).
- **In code, don't ask permission for reversible things** — just do them. This does NOT extend to
  the Editor (above).
- **Ask before**: changing the core loop, adding a package, restructuring scenes, or touching
  more than ~4 files.
- Match surrounding code style. No comment blocks explaining obvious code.

## Conventions

- UGUI: RectTransform anchors, **not** `VerticalLayoutGroup`/`LayoutElement`, for anything that
  resizes or reveals at runtime. VLG fights runtime layout changes.
- `<add gotchas here AS THEY BITE — one line each, immediately>`

## STATE.md

[STATE.md](STATE.md) is the current state of the game — **rewritten in place**, never appended
to. Read it at session start; update it when something meaningful changes. Under 100 lines: what
exists, what's half-done, what's next, known bugs. It is NOT a changelog.
