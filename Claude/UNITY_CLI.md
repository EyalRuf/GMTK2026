# UNITY_CLI.md — Driving the live Unity Editor from the terminal

> Lets you read compile errors, set inspector fields, create assets, edit prefabs, enter play
> mode and take screenshots — without asking the user to do it. Requires **Unity 6.0+** and the
> **Editor open with the project**.

## Setup (do this before the jam starts)

```powershell
# Windows. Only a beta channel exists, so the channel var is mandatory.
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
# restart the terminal, then with the project open in Unity:
unity pipeline install
# alt-tab into Unity so it imports the package, then confirm:
unity pipeline list      # want "Server Reachable: true"
```

macOS/Linux: `curl -fsSL .../install.sh | UNITY_CLI_CHANNEL=beta bash`.
(Unity's docs page mislabels the bash line as the Windows one — ignore that.)

Installs a standalone binary to `%LOCALAPPDATA%\Unity\bin` on user PATH, no admin, auth inherited
from Hub. Adds `com.unity.pipeline` to the project manifest — the only repo change.

## Every session starts here

```powershell
unity status                                    # Port/State/Project — empty means not connected
unity command console --tail 20 --level error   # compile errors
unity command recompile                         # force recompile (works while unfocused)
unity command recompile_status                  # {status, failed, errors[]}
unity list                                      # all ~140 tools, with descriptions
```

`Server Reachable: false` but Editor running → Unity hasn't imported the package. Alt-tab into it.

## Syntax

```powershell
unity command <tool> --key value --flag true
```

- Args are the tool's own `--snake_case` parameters, not CLI flags. `unity command <tool> --help`
  shows the *wrapper's* options, not the tool's — get real parameter names from `unity list`.
- No bare `--` before args (it parses as an empty-named param).
- `--format json` for parseable output. `--timeout <seconds>` (default 30) for slow ops.

## The tools that matter

| Need | Tool |
|---|---|
| Compile errors / logs | `console`, `get_console_logs`, `recompile`, `recompile_status` |
| Editor state | `editor_status` (compiling, domainReloadInProgress, playMode) |
| Wire an inspector field | `set_serialized_field`, `get_serialized_fields`, `set_component_properties` |
| Create content assets | `create_asset` (Unity assigns GUID + writes `.meta`), `create_folder` |
| Find things | `find_assets` (type/name → path+GUID), `find_gameobjects`, `get_scene_hierarchy`, `search` |
| Prefabs | `save_prefab_contents` (isolated stage, nested-safe), `create_prefab`, `create_prefab_variant`, `instantiate_prefab` |
| Scene | `open_scene`, `save_scene`, `save_all`, `create_gameobject(s)`, `add_component`, `set_transform`, `set_parent` |
| Verify | `editor_play` / `editor_stop`, `screenshot`, `capture_game_view`, `run_tests` |
| Anything else | `eval_file` (arbitrary C# via Roslyn, no domain reload) |

Prefer structured tools over `eval` — they carry Undo, validation, and `dry_run`.

## Gotchas

- **Domain reloads kill in-flight commands.** Anything that recompiles makes the server briefly
  unreachable; a concurrent command dies with `timed out` / `No Unity Editor instances found`.
  **The operation usually completed anyway.** Poll `recompile_status` / `editor_status` before
  retrying, or you'll double-apply.
- **Long ops are async.** `build`, `bake_*`, `package_add/remove`, `switch_build_target` return
  immediately — poll their `*_status`. `package_*` accepts `--wait true`.
- **Destructive tools need `--confirm true`**, and most accept `--dry_run true`. Dry-run anything
  irreversible; the plan it prints is worth reading.
- **Settings tools cover only a subset of keys and no-op silently** on unknown ones — reporting
  *"No changes specified; nothing to apply"* rather than erroring. If a settings tool does
  nothing, that's why. Drop to `eval_file`.
- **`eval_file` takes `--file`** (not `--path`), accepts absolute paths outside the project, and
  the file is a bare statement body — no class/method wrapper, top-level `return` works. Prefer
  it to inline `eval` on Windows: PowerShell mangles quotes passing C#/JSON to a native exe.
- **`console` and `get_console_logs` are separate buffers**; `clear_console` doesn't empty
  `console`'s ring buffer. Trust `timestampUtc`, not position or a clear.
- **The Editor stops ticking when unfocused.** `recompile` works around it; `set_autotick` forces
  a throttled tick if something else stalls.

## Removing it

```powershell
unity command package_remove --name com.unity.pipeline --confirm true --wait true
unity self-uninstall
```
