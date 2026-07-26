using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Owns game flow: levels, lives, the death countdown, corpses, and the HUD.
    /// Everything lives in the scene now — player, camera, HUD, menu and all levels are
    /// pre-placed and enabled/disabled/reset, not instantiated/destroyed at runtime. The
    /// one exception is corpses, which are pooled under `corpseRoot`.
    public class GameManager : MonoBehaviour
    {
        public GameConfig config;
        [Tooltip("Drag the in-scene level objects here, in play order. Each needs a LevelRoot.")]
        public List<LevelRoot> levels = new();
        [Tooltip("Pre-placed Player object in the scene (starts disabled).")]
        public PlayerController player;
        [Tooltip("Pre-placed MainCamera object (Camera + CameraFollow).")]
        public CameraFollow cam;
        [Tooltip("Pre-placed HUD object (starts disabled).")]
        public HUD hud;
        [Tooltip("Pre-placed MenuUI object.")]
        public MenuUI menu;
        [Tooltip("Pre-placed ScreenWipe object: the diagonal cut to black between levels.")]
        public ScreenWipe wipe;
        [Tooltip("Corpse prefab: pooled at runtime under corpseRoot, never destroyed.")]
        public GameObject corpsePrefab;
        [Tooltip("Empty scene object the corpse pool is parented under.")]
        public Transform corpseRoot;
        [Tooltip("AudioSource for looping background music; volume is driven by the music slider.")]
        public AudioSource musicSource;

        enum State { MainMenu, Intro, Playing, Dying, LevelClear, GameOver, GameWin }
        State state;
        float stateTimer;

        readonly InputReader input = new();
        int levelIndex;
        int livesLeft;
        LifeTimer timer = new();
        GameObject levelInstanceGo;
        LevelRoot levelInstance;
        readonly List<Corpse> corpsePool = new();

        CorpseCarry corpseCarry;
        AudioSource audio;
        bool paused;

        UpgradeType pendingUpgrade = UpgradeType.None;

        bool timerStarted;
        float graceLeft;
        int lastWholeSecond;
        float soulInterval;
        float nextBoundary;
        bool hasDiedThisLevel;
        bool transitioning;
        bool deferEntryEvent;
        Vector3 lastDeathFeet;
        bool lastDeathUnrecoverable;
        float gameOverDeathLeft;

        // Jump/land/death SFX moved to FXManager (event-driven). Bounce/plate/win/fail/tick stay here.
        AudioClip sBounce, sPlate, sWin, sFail, sTick;

        void Start()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                Debug.LogWarning("GameManager has no GameConfig assigned; using defaults.");
            }

            if (levels.Count == 0)
                Debug.LogError("GameManager has no levels assigned.");

            Physics.gravity = new Vector3(0f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.40f);
            ConfigureScene();

            AudioListener.volume = SaveData.MasterVolume;
            audio.volume = SaveData.SfxVolume;
            if (musicSource != null)
            {
                musicSource.volume = SaveData.MusicVolume;
                musicSource.loop = true;
                musicSource.Play();
            }

            foreach (var lvl in levels) if (lvl != null) lvl.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            player.gameObject.SetActive(false);
            state = State.MainMenu;
            menu.ShowMainMenu();
        }

        /// Wires the pre-placed scene objects together (no instantiation) and builds SFX.
        void ConfigureScene()
        {
            player.Configure(config);
            cam.Configure(config, player);

            corpseCarry = player.GetComponent<CorpseCarry>();
            corpseCarry.Configure(config, player, cam.GetComponent<Camera>());
            player.DeathSequenceReady += OnTrapDeathReady;
            GameEvents.TrapHit += OnTrapHitShake;

            audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            sBounce = ProceduralAudio.Bounce(); sPlate = ProceduralAudio.Plate();
            sWin = ProceduralAudio.Win(); sFail = ProceduralAudio.Fail();
            sTick = ProceduralAudio.Tick();

            menu.BuildUI(config, levels, audio, musicSource, OnMenuLevelChosen, OnMenuResume, OnMenuBackToMenu);
        }

        void StartLevel(int i)
        {
            // Restart / game over / the debug level keys all land here mid-nowhere; make sure no
            // wipe is left covering the screen. Inside a transition the cover is deliberate.
            if (!transitioning && wipe != null) wipe.Clear();
            levelIndex = i;
            SaveData.HighestUnlockedLevel = Mathf.Max(SaveData.HighestUnlockedLevel, i);
            if (corpseCarry != null) corpseCarry.DropHeld();
            ClearCorpses();
            livesLeft = config.livesPerLevel;
            hasDiedThisLevel = false;

            // Enable the chosen level, disable the rest.
            for (int k = 0; k < levels.Count; k++)
                if (levels[k] != null) levels[k].gameObject.SetActive(k == i);

            levelInstance = levels[i];
            levelInstanceGo = levelInstance.gameObject;

            var bounds = levelInstanceGo.GetComponentInChildren<CameraBounds>(true);
            if (bounds == null) Debug.LogError($"Level '{levelInstance.name}' has no CameraBounds in its children.");
            cam.SetBounds(bounds);

            // Wipe every persisting level object back to its authored state (platforms,
            // gates, plates, pickups, exit) — the reset that Destroy/Instantiate used to give
            // for free.
            foreach (var r in levelInstanceGo.GetComponentsInChildren<ILevelResettable>(true))
                r.ResetToInitial();

            var exit = levelInstanceGo.GetComponentInChildren<LevelExit>(true);
            if (exit != null) exit.Init(OnExitReached);
            else Debug.LogError($"Level '{levelInstance.name}' has no LevelExit in its children.");

            foreach (var pickup in levelInstanceGo.GetComponentsInChildren<UpgradePickup>(true))
                pickup.Init(config, OnUpgradePickedUp);
            foreach (var pickup in levelInstanceGo.GetComponentsInChildren<RubberPickup>(true))
                pickup.Init(config);
            pendingUpgrade = UpgradeType.None;
            corpseCarry.SetEnabled(false);
            player.JumpMultiplier = 1f;

            hud.SetLevel(levelInstance.levelName, i + 1, levels.Count);
            hud.SetLives(livesLeft, config.livesPerLevel);
            hud.SetHint(levelInstance.hint);
            hud.SetChromeVisible(!config.hideLevelUI);

            soulInterval = levelInstance.timer;
            float totalDuration = soulInterval * config.livesPerLevel;
            timer.Restart(totalDuration);
            timer.Stop();
            nextBoundary = totalDuration - soulInterval;
            hud.BuildSouls(config.livesPerLevel);
            hud.ShowTimerText(config.showTimerSeconds);
            hud.SetTimer(timer.Remaining, timer.Normalized);
            hud.SetSouls(timer.Remaining, soulInterval);

            BeginLife();
            EnterState(State.Intro, config.hideLevelUI ? 0.5f : 1.9f);
            if (!config.hideLevelUI)
                hud.Banner($"LEVEL {i + 1}", levelInstance.levelName, GreyboxFactory.Exit);
        }

        void BeginLife()
        {
            var spawnFeet = levelInstance.EntryFeet;
            if (config.respawnAtDeathSpot && hasDiedThisLevel && !lastDeathUnrecoverable)
            {
                // Prefer straight up (on top of the corpse you just left); only step aside to the
                // left when the capsule doesn't fit there — under a low ceiling or a stacked corpse.
                Vector3 above = lastDeathFeet + Vector3.up * config.respawnOffsetY;
                spawnFeet = IsSpawnClear(above)
                    ? above
                    : ResolveSpawnClearance(lastDeathFeet + Vector3.left * config.respawnOffsetX);
            }
            player.Spawn(spawnFeet);
            // During a level transition the entry animation is held back until the wipe has
            // revealed the new level — EnterLevelSequence raises it. Respawns raise it here — it's
            // also what the Animator's CorpseState relies on to transition back out (see
            // PlayerAnimator.controller: CorpseState -> LevelEntry on the LevelEnter trigger).
            if (!deferEntryEvent) GameEvents.RaiseLevelEntered(spawnFeet);
            cam.Snap();
            timerStarted = false;
            graceLeft = config.respawnGrace;
            timer.Stop();
            lastWholeSecond = Mathf.CeilToInt(timer.Remaining - nextBoundary);
        }

        /// A dropped corpse can settle right on the death-spot respawn location; stepping up
        /// until the player capsule is clear stops it from spawning wedged inside a corpse/wall.
        Vector3 ResolveSpawnClearance(Vector3 feet)
        {
            for (int i = 0; i < 20; i++)
            {
                if (IsSpawnClear(feet)) return feet;
                feet += Vector3.up * 0.5f;
            }
            return feet;
        }

        bool IsSpawnClear(Vector3 feet)
        {
            Vector3 p0 = feet + Vector3.up * config.playerRadius;
            Vector3 p1 = feet + Vector3.up * (config.playerHeight - config.playerRadius);
            return !Physics.CheckCapsule(p0, p1, config.playerRadius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            input.Sample();

            // A level transition owns the game entirely while it runs: no pause, no restart, no
            // debug level jumping, no camera panning — any of those mid-wipe strands a black
            // panel over the screen. The transition coroutine ticks the player itself.
            if (transitioning) return;

            if (cam != null) cam.SetLookInput(input.LookUpHeld, input.LookDownHeld);

            if (state == State.MainMenu) return;

            if (input.PausePressed && state != State.GameWin)
            {
                TogglePause();
                return;
            }

            if (paused) return;

            float dt = Time.deltaTime;

            if (input.RestartPressed && state != State.Intro)
            {
                StartLevel(levelIndex);
                return;
            }

            if (input.PrevLevelPressed && levelIndex > 0) { StartLevel(levelIndex - 1); return; }
            if (input.NextLevelPressed && levelIndex + 1 < levels.Count) { StartLevel(levelIndex + 1); return; }

            stateTimer -= dt;

            switch (state)
            {
                case State.Intro:
                    // controls locked until the level banner clears; countdown not yet running
                    TickPlayer(dt, allowDeath: false, allowInput: false);
                    if (stateTimer <= 0f) { hud.HideBanner(); state = State.Playing; }
                    break;

                case State.Playing:
                    TickPlayer(dt, allowDeath: true);
                    UpdateCountdown(dt);
                    break;

                case State.Dying:
                    if (stateTimer <= 0f)
                    {
                        if (livesLeft <= 0) EnterGameOver();
                        else { BeginLife(); state = State.Playing; }
                    }
                    break;

                // LevelClear is driven entirely by ExitSequence (which runs with `transitioning`
                // set, so this switch isn't reached); the state is just a re-entry guard.
                case State.LevelClear:
                    break;

                case State.GameOver:
                    TickGameOverDeath(dt);
                    if (stateTimer <= 0f) StartLevel(levelIndex);
                    break;

                case State.GameWin:
                    if (input.JumpPressed) StartLevel(0);
                    break;
            }

            hud.SetTimer(timer.Remaining, timer.Normalized);
            hud.SetSouls(timer.Remaining, soulInterval);
            hud.SetLives(livesLeft, config.livesPerLevel);
        }

        void TickPlayer(float dt, bool allowDeath, bool allowInput = true)
        {
            // A trap hit is already mid-sequence (knockback + hit reaction): control is locked and
            // no other death check applies until GameManager.OnTrapDeathReady takes over.
            if (player.IsDying)
            {
                player.Tick(default, dt);
                return;
            }

            // Locked during the level-intro banner: still tick so gravity settles the cat, but
            // ignore every button so a held key doesn't fire the instant control returns.
            if (!allowInput)
            {
                player.Tick(default, dt);
                return;
            }

            corpseCarry.Sample(input);
            var mi = new MotorInput { Move = input.Move, JumpPressed = input.JumpPressed, JumpHeld = input.JumpHeld, JumpReleased = input.JumpReleased };
            player.Tick(mi, dt);

            // Jump / land SFX+VFX now flow through GameEvents -> FXManager (raised in
            // PlayerController). Bounce isn't part of the FX spec, so it stays here.
            if (player.BouncedThisStep) audio.PlayOneShot(sBounce);

            if (!allowDeath) return;

            if (player.FeetPosition.y < config.killPlaneY) DieAutomatic();
            else if (input.SacrificePressed) DieManual();
        }

        /// A DeathTrap's knockback + hit-reaction has finished; continue with the exact same
        /// death/respawn flow as walking off the map (unrecoverable, spawns a corpse in place).
        void OnTrapDeathReady(DeathInfo info) => DieAutomatic();

        void OnTrapHitShake(Vector3 _) => cam.Shake(config.hitShakeDuration, config.hitShakeMagnitude);

        /// Manual sacrifice (Q): consumes the current soul/time slot, then respawns next to the corpse.
        void DieManual()
        {
            ConsumeSoul();
            Die(unrecoverable: false, spawnCorpse: true);
        }

        /// Environmental death (out of bounds, death zone, or any other automatic trigger): consumes
        /// the same soul/time slot as a manual sacrifice, but always respawns at the level entry.
        void DieAutomatic()
        {
            ConsumeSoul();
            Die(unrecoverable: true, spawnCorpse: true);
        }

        /// Snaps the timer down to the current soul boundary and advances to the next slot — the
        /// shared soul/time deduction used by every death that isn't a natural timeout.
        void ConsumeSoul()
        {
            timer.SetRemaining(nextBoundary);
            nextBoundary -= soulInterval;
        }

        void UpdateCountdown(float dt)
        {
            if (!timerStarted)
            {
                graceLeft -= dt;
                if (graceLeft <= 0f) { timer.Resume(); timerStarted = true; }
                return;
            }

            timer.Tick(dt);

            float remainingInSoul = timer.Remaining - nextBoundary;
            int whole = Mathf.CeilToInt(remainingInSoul);
            if (whole != lastWholeSecond && remainingInSoul <= 3f && remainingInSoul > 0f)
                audio.PlayOneShot(sTick, 0.6f);
            lastWholeSecond = whole;

            if (timer.Remaining <= nextBoundary + 0.0001f) ExpireSoulBoundary();
        }

        /// A soul's slot has run out naturally (the countdown reached the boundary on its own,
        /// with no death involved).
        void ExpireSoulBoundary()
        {
            nextBoundary -= soulInterval;

            // Timed spawn off: the soul is consumed silently — no corpse, no death, no respawn.
            // The player keeps going on the same continuous run; only a real death (fall/trap) or
            // manual sacrifice ends the current life.
            if (!config.isTimedCorpseSpawn)
            {
                livesLeft--;
                hud.SetLives(livesLeft, config.livesPerLevel);
                if (livesLeft <= 0) EnterGameOver();
                return;
            }

            Die(spawnCorpse: true);
        }

        void Die(bool unrecoverable = false, bool spawnCorpse = true)
        {
            hud.HideBanner();
            corpseCarry.DropHeld();
            lastDeathFeet = player.FeetPosition;
            lastDeathUnrecoverable = unrecoverable;
            hasDiedThisLevel = true;
            var kind = pendingUpgrade == UpgradeType.Trampoline ? CorpseKind.Trampoline : CorpseKind.Normal;
            Vector2 deathVelocity = player.Velocity;
            // Freeze the animator-driving params before the death event fires so the Die trigger's
            // pose actually sticks instead of being stomped by this frame's live Speed/Grounded.
            player.EnterDeathPose();
            // Recoverable death (sacrifice / natural timeout) = soul-leaves-body sequence;
            // environmental death = poof. SpawnCorpse raises CorpseSpawned when a body appears.
            if (unrecoverable) GameEvents.RaisePoofDeath(lastDeathFeet);
            else GameEvents.RaiseSacrificeDeath(lastDeathFeet);
            // Disable just the collider (not the whole object — that would also stop the Animator
            // from processing the trigger it was just given) so the corpse's clearance search
            // doesn't treat the dying player as an obstacle to dodge around.
            player.SetColliderEnabled(false);
            if (spawnCorpse) SpawnCorpse(lastDeathFeet, deathVelocity, kind);
            player.SetColliderEnabled(true);
            timer.Stop();
            timerStarted = false;
            livesLeft--;
            pendingUpgrade = UpgradeType.None;
            corpseCarry.SetEnabled(false);
            player.JumpMultiplier = 1f;
            EnterState(State.Dying, 0.45f);
        }

        void OnUpgradePickedUp(UpgradeType upgrade)
        {
            pendingUpgrade = upgrade;
            corpseCarry.SetEnabled(upgrade == UpgradeType.Carry);
            player.JumpMultiplier = upgrade == UpgradeType.Trampoline ? config.trampolinePlayerJumpMultiplier : 1f;
        }

        void OnExitReached()
        {
            if (transitioning) return;
            if (state != State.Playing && state != State.Intro) return;
            audio.PlayOneShot(sWin);
            timer.Stop();
            StartCoroutine(ExitSequence());
        }

        // --- Level transition ---------------------------------------------------------------
        // Reaching the exit: control dies, the cat and the pad play their exit animations, the
        // diagonal cut sweeps to black, the next level is swapped in behind it, the cut sweeps
        // off, the cat plays its entry animation, and only then does input come back.

        IEnumerator ExitSequence()
        {
            transitioning = true;
            EnterState(State.LevelClear, float.PositiveInfinity);
            bool last = levelIndex + 1 >= levels.Count;

            // The cat's exit animation; LevelExit fires the pad's own animator on contact.
            GameEvents.RaiseLevelExited(player.FeetPosition);
            if (!config.hideLevelUI)
                hud.Banner(last ? "FINAL EXIT" : "EXIT!", last ? "" : "Nice.", GreyboxFactory.Exit);

            yield return HoldLocked(config.levelExitAnimTime);

            wipe.Cover(config.wipeCoverTime);
            yield return HoldWhileWiping();

            hud.HideBanner();
            if (last)
            {
                EnterWin();
                wipe.Reveal(config.wipeRevealTime);
            }
            else yield return EnterLevelSequence(levelIndex + 1);

            transitioning = false;
        }

        /// Swaps in level `index` behind a covered screen, reveals it, plays the cat's entry
        /// animation, then hands control back through the normal Intro unlock. The very first
        /// level in the stack has nothing to transition *from*, so it skips the whole entry beat.
        IEnumerator EnterLevelSequence(int index)
        {
            bool playEntry = index != 0;

            deferEntryEvent = playEntry;
            StartLevel(index);
            deferEntryEvent = false;

            if (!playEntry) { wipe.Clear(); yield break; }

            // StartLevel already set Intro with its own duration; hold it open until the reveal
            // and the entry animation are done, then let it run out normally.
            EnterState(State.Intro, float.PositiveInfinity);

            yield return HoldLocked(config.wipeBlackHoldTime);
            wipe.Reveal(config.wipeRevealTime);
            yield return HoldWhileWiping();

            GameEvents.RaiseLevelEntered(player.FeetPosition);
            EnterState(State.Intro, config.levelEntryAnimTime);
        }

        IEnumerator HoldLocked(float seconds)
        {
            while (seconds > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                seconds -= dt;
                TickLocked(dt);
                yield return null;
            }
        }

        IEnumerator HoldWhileWiping()
        {
            while (wipe.IsBusy)
            {
                TickLocked(Time.unscaledDeltaTime);
                yield return null;
            }
        }

        /// Keeps gravity and animation running on the cat during a transition while every button
        /// is ignored — the same deal as the level-intro lock, just driven from the coroutine.
        void TickLocked(float dt)
        {
            if (player != null && player.gameObject.activeSelf)
                TickPlayer(dt, allowDeath: false, allowInput: false);
        }

        void EnterGameOver()
        {
            audio.PlayOneShot(sFail);
            EnterState(State.GameOver, 1.6f);
            hud.Banner("OUT OF LIVES", "Resetting the level…", GreyboxFactory.Hazard);

            // Reaching game over on the timer (rather than through Die()) leaves the cat alive and
            // standing there — it'd idle under the banner. Kill it in place: death event -> death
            // animation + FX, then hide it once the clip is done.
            timer.Stop();
            timerStarted = false;
            gameOverDeathLeft = 0f;
            if (player.gameObject.activeSelf)
            {
                corpseCarry.DropHeld();
                player.EnterDeathPose();
                GameEvents.RaiseSacrificeDeath(player.FeetPosition);
                gameOverDeathLeft = config.gameOverDeathAnimTime;
            }
        }

        /// Lets gravity settle the corpse-to-be while the death animation plays (IsDying means
        /// Tick takes no input), then hides it before the animator exit-times back to Idle.
        void TickGameOverDeath(float dt)
        {
            if (gameOverDeathLeft <= 0f) return;
            gameOverDeathLeft -= dt;
            player.Tick(default, dt);
            if (gameOverDeathLeft <= 0f) player.gameObject.SetActive(false);
        }

        void EnterWin()
        {
            audio.PlayOneShot(sWin);
            state = State.GameWin;
            player.gameObject.SetActive(false);
            hud.Banner("NINE LIVES SPENT WELL", "Press Space to play again", GreyboxFactory.Exit);
        }

        void EnterState(State s, float dur) { state = s; stateTimer = dur; }

        void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            if (paused) menu.ShowPause();
            else menu.Hide();
        }

        void OnMenuLevelChosen(int idx)
        {
            StopAllCoroutines();
            transitioning = false;
            paused = false;
            Time.timeScale = 1f;
            menu.Hide();
            hud.gameObject.SetActive(true);

            // Level 0 has no entry beat, so it's a straight cut from the menu as before.
            if (idx == 0) { StartLevel(0); return; }

            wipe.SetCoveredInstant();
            StartCoroutine(MenuEnterSequence(idx));
        }

        IEnumerator MenuEnterSequence(int idx)
        {
            transitioning = true;
            yield return EnterLevelSequence(idx);
            transitioning = false;
        }

        void OnMenuResume()
        {
            paused = false;
            Time.timeScale = 1f;
            menu.Hide();
        }

        void OnMenuBackToMenu()
        {
            StopAllCoroutines();
            transitioning = false;
            if (wipe != null) wipe.Clear();
            paused = false;
            Time.timeScale = 1f;
            if (corpseCarry != null) corpseCarry.DropHeld();
            ClearCorpses();
            if (levelInstanceGo != null) { levelInstanceGo.SetActive(false); levelInstanceGo = null; levelInstance = null; }
            player.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            state = State.MainMenu;
            menu.ShowMainMenu();
        }

        void SpawnCorpse(Vector3 feet, Vector2 vel, CorpseKind kind)
        {
            Vector3 desired = feet + Vector3.up * (config.corpseSize.y * 0.5f);
            Vector3 resolved = ResolveCorpseSpawnClearance(desired, config.corpseSize * 0.5f);

            var corpse = GetPooledCorpse();
            var go = corpse.gameObject;
            go.name = kind == CorpseKind.Trampoline ? "Corpse_Trampoline" : "Corpse";
            go.transform.localScale = config.corpseSize;
            // Position while the pooled object is still inactive: with physics off, activating it
            // starts the rigidbody fresh at the death spot — no ContinuousDynamic sweep from its
            // last resting place through the floor (which would leave it stuck mid-level).
            go.transform.position = resolved;
            go.SetActive(true);
            corpse.Init(config, vel, kind);
            GameEvents.RaiseCorpseSpawned(resolved);
        }

        /// Reuse a disabled pooled corpse, or grow the pool by one. Corpses are the only
        /// runtime-dynamic object; the pool bounds instantiation to at most livesPerLevel ever.
        Corpse GetPooledCorpse()
        {
            foreach (var c in corpsePool)
            {
                if (c == null || c.gameObject.activeSelf) continue;
                ReturnToPoolRoot(c);
                return c;
            }
            var go = Instantiate(corpsePrefab, corpseRoot);
            go.SetActive(false);
            var corpse = go.GetComponent<Corpse>();
            corpsePool.Add(corpse);
            return corpse;
        }

        /// If the naive spawn point overlaps level geometry, PhysX depenetration can fling the
        /// corpse's rigidbody far from the death spot on the first physics step (Corpse.Init also
        /// caps maxDepenetrationVelocity as a backstop). Search a widening ring of points around
        /// the death spot — straight up first, since that's the natural "give it room to fall"
        /// direction — for a clear spot instead of spawning wedged into a wall/gate.
        const int CorpseSearchRingDirs = 12;

        Vector3 ResolveCorpseSpawnClearance(Vector3 center, Vector3 halfExtents)
        {
            if (!Physics.CheckBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                return center;

            for (int step = 1; step <= 15; step++)
            {
                float radius = step * 0.35f;
                for (int i = 0; i < CorpseSearchRingDirs; i++)
                {
                    // Start straight up (i=0) and fan out evenly around the circle from there.
                    float angle = (Mathf.PI / 2f) + (2f * Mathf.PI * i / CorpseSearchRingDirs);
                    Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Vector3 candidate = center + dir * radius;
                    if (!Physics.CheckBox(candidate, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        return candidate;
                }
            }
            return center;
        }

        void ClearCorpses()
        {
            foreach (var c in corpsePool)
            {
                if (c == null) continue;
                ReturnToPoolRoot(c);
                c.gameObject.SetActive(false);
            }
        }

        /// A settled corpse parents itself onto whatever moving thing it came to rest on, which puts
        /// it inside the level hierarchy. Pull it back under corpseRoot before anything disables a
        /// level, or it gets deactivated with its carrier while still counting as in-use (activeSelf
        /// stays true), and the pool leaks a corpse per level switch.
        void ReturnToPoolRoot(Corpse c)
        {
            c.Detach();
            if (c.transform.parent != corpseRoot) c.transform.SetParent(corpseRoot, true);
        }
    }
}
