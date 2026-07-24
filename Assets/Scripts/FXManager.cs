using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// The one place that turns game events into visible/audible effects. Subscribes to
    /// GameEvents, spawns a pooled placeholder VFX at the event position and plays the matching
    /// SFX. Gameplay never touches this — it only raises events. To polish later: drop new
    /// ParticleSystem prefabs onto the fields below (and real AudioClips), no code change needed.
    public class FXManager : MonoBehaviour
    {
        [Header("Movement VFX")]
        [SerializeField] OneShotVFX footstepVfx;
        [SerializeField] OneShotVFX jumpVfx;
        [SerializeField] OneShotVFX landVfx;
        [SerializeField] OneShotVFX hardLandVfx;

        [Header("Death VFX")]
        [SerializeField] OneShotVFX soulVfx;        // sacrifice: soul leaves body
        [SerializeField] OneShotVFX corpseSpawnVfx; // corpse form materialises
        [SerializeField] OneShotVFX poofVfx;        // environmental death poof

        [Header("Level VFX")]
        [SerializeField] OneShotVFX levelEntryVfx;
        [SerializeField] OneShotVFX levelExitVfx;

        [Header("Throw VFX (hooks — mechanic not built yet)")]
        [SerializeField] OneShotVFX throwVfx;

        [Header("Audio")]
        [Tooltip("Optional. Leave null to use a private AudioSource driven by SfxVolume.")]
        [SerializeField] AudioSource sfxSource;

        [Tooltip("Seconds after the soul leaves before the corpse-spawn effect plays.")]
        [SerializeField] float corpseSpawnDelay = 0.18f;

        [Header("SFX — drop a clip to override the procedural placeholder; empty = placeholder tone")]
        [SerializeField] AudioClip footstepClip;
        [SerializeField] AudioClip jumpClip;
        [SerializeField] AudioClip landClip;
        [SerializeField] AudioClip hardLandClip;
        [SerializeField] AudioClip soulClip;
        [SerializeField] AudioClip corpseSpawnClip;
        [SerializeField] AudioClip poofClip;
        [SerializeField] AudioClip levelEntryClip;
        [SerializeField] AudioClip levelExitClip;
        [SerializeField] AudioClip throwClip;

        // Pool: one recycling clone list per source prefab, keyed by the authored instance.
        readonly Dictionary<OneShotVFX, List<OneShotVFX>> pools = new();

        AudioClip sFootstep, sJump, sLand, sHardLand, sSoul, sCorpse, sPoof, sEntry, sExit, sThrow;

        void Awake()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.volume = SaveData.SfxVolume;
            }

            // Assigned clip wins; otherwise fall back to the code-generated placeholder tone.
            sFootstep = footstepClip != null ? footstepClip : ProceduralAudio.Footstep();
            sJump = jumpClip != null ? jumpClip : ProceduralAudio.Jump();
            sLand = landClip != null ? landClip : ProceduralAudio.Land();
            sHardLand = hardLandClip != null ? hardLandClip : ProceduralAudio.HardLand();
            sSoul = soulClip != null ? soulClip : ProceduralAudio.Soul();
            sCorpse = corpseSpawnClip != null ? corpseSpawnClip : ProceduralAudio.CorpseSpawn();
            sPoof = poofClip != null ? poofClip : ProceduralAudio.Poof();
            sEntry = levelEntryClip != null ? levelEntryClip : ProceduralAudio.LevelEntry();
            sExit = levelExitClip != null ? levelExitClip : ProceduralAudio.LevelExit();
            sThrow = throwClip != null ? throwClip : ProceduralAudio.Throw();
        }

        void OnEnable()
        {
            GameEvents.Footstep += OnFootstep;
            GameEvents.Jumped += OnJumped;
            GameEvents.Landed += OnLanded;
            GameEvents.HardLanded += OnHardLanded;
            GameEvents.SacrificeDeath += OnSacrifice;
            GameEvents.CorpseSpawned += OnCorpseSpawned;
            GameEvents.PoofDeath += OnPoof;
            GameEvents.LevelEntered += OnLevelEntered;
            GameEvents.LevelExited += OnLevelExited;
            GameEvents.Thrown += OnThrown;
        }

        void OnDisable()
        {
            GameEvents.Footstep -= OnFootstep;
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.HardLanded -= OnHardLanded;
            GameEvents.SacrificeDeath -= OnSacrifice;
            GameEvents.CorpseSpawned -= OnCorpseSpawned;
            GameEvents.PoofDeath -= OnPoof;
            GameEvents.LevelEntered -= OnLevelEntered;
            GameEvents.LevelExited -= OnLevelExited;
            GameEvents.Thrown -= OnThrown;
        }

        void OnFootstep(Vector3 p) { Spawn(footstepVfx, p); Play(sFootstep, 0.35f); }
        void OnJumped(Vector3 p) { Spawn(jumpVfx, p); Play(sJump); }
        void OnLanded(Vector3 p) { Spawn(landVfx, p); Play(sLand, 0.7f); }
        void OnHardLanded(Vector3 p) { Spawn(hardLandVfx, p); Play(sHardLand); }
        void OnSacrifice(Vector3 p) { Spawn(soulVfx, p); Play(sSoul); }
        void OnCorpseSpawned(Vector3 p) => StartCoroutine(CorpseSpawnAfterDelay(p));
        void OnPoof(Vector3 p) { Spawn(poofVfx, p); Play(sPoof); }
        void OnLevelEntered(Vector3 p) { Spawn(levelEntryVfx, p); Play(sEntry); }
        void OnLevelExited(Vector3 p) { Spawn(levelExitVfx, p); Play(sExit); }
        void OnThrown(Vector3 p, Vector3 dir) { Spawn(throwVfx, p); Play(sThrow); }

        System.Collections.IEnumerator CorpseSpawnAfterDelay(Vector3 p)
        {
            if (corpseSpawnDelay > 0f) yield return new WaitForSeconds(corpseSpawnDelay);
            Spawn(corpseSpawnVfx, p);
            Play(sCorpse);
        }

        void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }

        /// Pull a free clone from the prefab's pool (or grow it), position and fire it.
        void Spawn(OneShotVFX prefab, Vector3 position)
        {
            if (prefab == null) return;
            if (!pools.TryGetValue(prefab, out var list))
            {
                list = new List<OneShotVFX>();
                pools[prefab] = list;
            }

            OneShotVFX free = null;
            foreach (var v in list)
                if (v != null && !v.gameObject.activeSelf) { free = v; break; }

            if (free == null)
            {
                free = Instantiate(prefab, transform);
                list.Add(free);
            }
            free.Play(position);
        }
    }
}
