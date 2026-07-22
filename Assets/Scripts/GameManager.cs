using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Owns game flow: levels, lives, the death countdown, corpses, and the HUD.
    /// Builds the entire scene (camera, light, player, UI) at runtime — the scene
    /// only needs one GameObject carrying this component.
    public class GameManager : MonoBehaviour
    {
        public GameConfig config;

        enum State { Intro, Playing, Dying, LevelClear, GameOver, GameWin }
        State state;
        float stateTimer;

        readonly InputReader input = new();
        List<LevelDef> levels;
        int levelIndex;
        int livesLeft;
        LifeTimer timer = new();
        LevelRuntime level;
        readonly List<GameObject> corpses = new();

        PlayerController player;
        CameraFollow cam;
        HUD hud;
        AudioSource audio;

        bool timerStarted;
        float graceLeft;
        int lastWholeSecond;

        AudioClip sJump, sBounce, sDeath, sPlate, sLand, sWin, sFail, sTick;

        void Start()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                Debug.LogWarning("GameManager has no GameConfig assigned; using defaults.");
            }

            Physics.gravity = new Vector3(0f, -32f, 0f);
            BuildScene();

            levels = Levels.Build();
            StartLevel(0);
        }

        void BuildScene()
        {
            // Camera
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var c = camGo.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            c.fieldOfView = 60f;
            cam = camGo.AddComponent<CameraFollow>();

            // Light
            var lightGo = new GameObject("Sun");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.97f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.40f);

            // Player
            var pGo = new GameObject("Player");
            player = pGo.AddComponent<PlayerController>();
            player.Configure(config);
            cam.Configure(config, player);

            // Audio
            audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            sJump = ProceduralAudio.Jump(); sBounce = ProceduralAudio.Bounce();
            sDeath = ProceduralAudio.Death(); sPlate = ProceduralAudio.Plate();
            sLand = ProceduralAudio.Land(); sWin = ProceduralAudio.Win();
            sFail = ProceduralAudio.Fail(); sTick = ProceduralAudio.Tick();

            // HUD
            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(transform, false);
            hud = hudGo.AddComponent<HUD>();
            hud.BuildUI();
        }

        void StartLevel(int i)
        {
            levelIndex = i;
            ClearCorpses();
            level?.Destroy();
            livesLeft = config.livesPerLevel;

            var def = levels[i];
            level = LevelRuntime.Build(def, config, OnExitReached);

            hud.SetLevel(def.Name, i + 1, levels.Count);
            hud.SetLives(livesLeft, config.livesPerLevel);
            hud.SetHint(def.Hint);
            hud.SetTimer(def.Timer, 1f);

            BeginLife();
            EnterState(State.Intro, 1.9f);
            hud.Banner($"LEVEL {i + 1}", def.Name, GreyboxFactory.Exit);
        }

        void BeginLife()
        {
            player.Spawn(level.Entry);
            cam.Snap();
            timerStarted = false;
            graceLeft = config.respawnGrace;
            timer.Restart(levels[levelIndex].Timer);
            timer.Stop();
            lastWholeSecond = Mathf.CeilToInt(levels[levelIndex].Timer);
        }

        void Update()
        {
            input.Sample();
            float dt = Time.deltaTime;

            if (input.RestartPressed && state != State.Intro)
            {
                StartLevel(levelIndex);
                return;
            }

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
            hud.SetLives(livesLeft, config.livesPerLevel);
        }

        void TickPlayer(float dt, bool allowDeath)
        {
            var mi = new MotorInput { Move = input.Move, JumpPressed = input.JumpPressed, JumpHeld = input.JumpHeld };
            player.Tick(mi, dt);

            if (player.JumpedThisStep) audio.PlayOneShot(sJump);
            if (player.BouncedThisStep) audio.PlayOneShot(sBounce);
            if (player.LandedThisStep) audio.PlayOneShot(sLand, 0.7f);

            if (!allowDeath) return;

            if (player.FeetPosition.y < config.killPlaneY) Die();
            else if (input.SacrificePressed) Die();
        }

        void UpdateCountdown(float dt)
        {
            if (!timerStarted)
            {
                graceLeft -= dt;
                if (graceLeft <= 0f) { timer.Restart(levels[levelIndex].Timer); timerStarted = true; }
                return;
            }

            int whole = Mathf.CeilToInt(timer.Remaining);
            if (whole != lastWholeSecond && timer.Remaining <= 3f && timer.Remaining > 0f)
                audio.PlayOneShot(sTick, 0.6f);
            lastWholeSecond = whole;

            if (timer.Tick(dt)) Die();
        }

        void Die()
        {
            SpawnCorpse(player.FeetPosition, player.Velocity);
            player.gameObject.SetActive(false);
            audio.PlayOneShot(sDeath);
            timer.Stop();
            timerStarted = false;
            livesLeft--;
            EnterState(State.Dying, 0.45f);
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

        void SpawnCorpse(Vector3 feet, Vector2 vel)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Corpse";
            go.transform.position = feet + Vector3.up * (config.corpseSize.y * 0.5f);
            go.transform.localScale = config.corpseSize;
            go.GetComponent<MeshRenderer>().sharedMaterial = GreyboxFactory.Make(GreyboxFactory.CorpseCol, 0.2f);
            go.AddComponent<Rigidbody>();
            go.AddComponent<Corpse>().Init(config, vel);
            corpses.Add(go);
        }

        void ClearCorpses()
        {
            foreach (var c in corpses) if (c) Destroy(c);
            corpses.Clear();
        }
    }
}
