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
        Vector3 lastDeathFeet;
        bool lastDeathUnrecoverable;

        AudioClip sJump, sBounce, sDeath, sPlate, sLand, sWin, sFail, sTick;

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

            audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            sJump = ProceduralAudio.Jump(); sBounce = ProceduralAudio.Bounce();
            sDeath = ProceduralAudio.Death(); sPlate = ProceduralAudio.Plate();
            sLand = ProceduralAudio.Land(); sWin = ProceduralAudio.Win();
            sFail = ProceduralAudio.Fail(); sTick = ProceduralAudio.Tick();

            menu.BuildUI(config, levels, audio, musicSource, OnMenuLevelChosen, OnMenuResume, OnMenuBackToMenu);
        }

        void StartLevel(int i)
        {
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

            // Wipe every persisting level object back to its authored state (platforms,
            // gates, plates, pickups, exit) — the reset that Destroy/Instantiate used to give
            // for free.
            foreach (var r in levelInstanceGo.GetComponentsInChildren<ILevelResettable>(true))
                r.ResetToInitial();

            var exit = levelInstanceGo.GetComponentInChildren<LevelExit>(true);
            if (exit != null) exit.Init(OnExitReached);
            else Debug.LogError($"Level '{levelInstance.name}' has no LevelExit in its children.");

            foreach (var pickup in levelInstanceGo.GetComponentsInChildren<UpgradePickup>(true))
                pickup.Init(OnUpgradePickedUp);
            pendingUpgrade = UpgradeType.None;
            corpseCarry.SetEnabled(false);
            player.JumpMultiplier = 1f;

            hud.SetLevel(levelInstance.levelName, i + 1, levels.Count);
            hud.SetLives(livesLeft, config.livesPerLevel);
            hud.SetHint(levelInstance.hint);

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
            EnterState(State.Intro, 1.9f);
            hud.Banner($"LEVEL {i + 1}", levelInstance.levelName, GreyboxFactory.Exit);
        }

        void BeginLife()
        {
            var spawnFeet = levelInstance.EntryFeet;
            if (config.respawnAtDeathSpot && hasDiedThisLevel && !lastDeathUnrecoverable)
                spawnFeet = ResolveSpawnClearance(lastDeathFeet + Vector3.left * config.respawnOffsetX);
            player.Spawn(spawnFeet);
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
                Vector3 p0 = feet + Vector3.up * config.playerRadius;
                Vector3 p1 = feet + Vector3.up * (config.playerHeight - config.playerRadius);
                if (!Physics.CheckCapsule(p0, p1, config.playerRadius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                    return feet;
                feet += Vector3.up * 0.5f;
            }
            return feet;
        }

        void Update()
        {
            input.Sample();

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
                    // player can already move during intro; countdown not yet running
                    TickPlayer(dt, allowDeath: false);
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

                case State.LevelClear:
                    if (stateTimer <= 0f)
                    {
                        if (levelIndex + 1 < levels.Count) StartLevel(levelIndex + 1);
                        else EnterWin();
                    }
                    break;

                case State.GameOver:
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

        void TickPlayer(float dt, bool allowDeath)
        {
            corpseCarry.Sample(input);
            var mi = new MotorInput { Move = input.Move, JumpPressed = input.JumpPressed, JumpHeld = input.JumpHeld, JumpReleased = input.JumpReleased };
            player.Tick(mi, dt);

            if (player.JumpedThisStep) audio.PlayOneShot(sJump);
            if (player.BouncedThisStep) audio.PlayOneShot(sBounce);
            if (player.LandedThisStep) audio.PlayOneShot(sLand, 0.7f);

            if (!allowDeath) return;

            if (player.FeetPosition.y < config.killPlaneY) DieAutomatic();
            else if (input.SacrificePressed) DieManual();
        }

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
            corpseCarry.DropHeld();
            lastDeathFeet = player.FeetPosition;
            lastDeathUnrecoverable = unrecoverable;
            hasDiedThisLevel = true;
            var kind = pendingUpgrade == UpgradeType.Trampoline ? CorpseKind.Trampoline : CorpseKind.Normal;
            Vector2 deathVelocity = player.Velocity;
            // Deactivate (and with it, the CharacterController collider) before spawning the
            // corpse so its clearance search doesn't treat the dying player as an obstacle to
            // dodge around — it can land right on the death spot instead of nearby.
            player.gameObject.SetActive(false);
            if (spawnCorpse) SpawnCorpse(lastDeathFeet, deathVelocity, kind);
            audio.PlayOneShot(sDeath);
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
            if (state != State.Playing && state != State.Intro) return;
            audio.PlayOneShot(sWin);
            timer.Stop();
            bool last = levelIndex + 1 >= levels.Count;
            EnterState(State.LevelClear, 1.7f);
            hud.Banner(last ? "FINAL EXIT" : "EXIT!", last ? "" : "Nice.", GreyboxFactory.Exit);
        }

        void EnterGameOver()
        {
            audio.PlayOneShot(sFail);
            EnterState(State.GameOver, 1.6f);
            hud.Banner("OUT OF LIVES", "Resetting the level…", GreyboxFactory.Hazard);
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
            paused = false;
            Time.timeScale = 1f;
            menu.Hide();
            hud.gameObject.SetActive(true);
            StartLevel(idx);
        }

        void OnMenuResume()
        {
            paused = false;
            Time.timeScale = 1f;
            menu.Hide();
        }

        void OnMenuBackToMenu()
        {
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
        }

        /// Reuse a disabled pooled corpse, or grow the pool by one. Corpses are the only
        /// runtime-dynamic object; the pool bounds instantiation to at most livesPerLevel ever.
        Corpse GetPooledCorpse()
        {
            foreach (var c in corpsePool)
                if (c != null && !c.gameObject.activeSelf) return c;
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
            foreach (var c in corpsePool) if (c != null) c.gameObject.SetActive(false);
        }
    }
}
