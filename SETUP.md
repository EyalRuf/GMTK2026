# SETUP.md — before the theme drops

All theme-independent. Doing any of it during the jam costs you prime hours.

## Do tonight

- [ ] New Unity project. **Lock now: 2D/3D, URP vs Built-in, new Input System vs legacy.**
      Changing these later invalidates shader/material/input code already written.
- [ ] `git init`, Unity `.gitignore`, first commit, push to a remote. Jam machines die.
- [ ] **Install the Unity CLI + pipeline package** — see [UNITY_CLI.md](UNITY_CLI.md). ~15 min,
      and it's the highest-leverage item here by a wide margin. Verify:
      `unity status` shows `ready`, and `unity command console --tail 5` returns something.
- [ ] Copy `CLAUDE.md`, `UNITY_CLI.md`, `STATE.md` into the repo root. Leave STATE.md empty.
- [ ] Confirm `~/.claude/CLAUDE.md` exists (output style + the git prohibition).
- [ ] Import anything you already know you'll want (DOTween, a font, an audio lib). Package
      installs trigger domain reloads that break in-flight Claude commands.

## The moment the theme drops

Fill in CLAUDE.md's `<placeholders>` — genre/core loop line, deadline — **before your first
prompt.** Claude re-reading a stale one-liner all weekend is a slow leak.

## The four things that matter during the jam

Everything else is encoded in CLAUDE.md; Claude will raise it when relevant.

1. **`/clear` between features.** Every turn re-sends the whole conversation. This is the
   biggest lever after the CLI.
2. **Commit at every working state — only you can.** Claude is forbidden from touching git, so
   nothing is saved unless you do it. Checkpoint at every `/clear`.
3. **Give error text, not symptoms.** Or just say "check the console yourself."
4. **Batch requests.** Five small prompts each pay full context cost; one prompt with five
   tweaks pays it once.