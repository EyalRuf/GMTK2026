using UnityEngine;

namespace NineLives
{
    /// "Rubber body" pickup. Touch it and the cat's SpringSquash starts showing exaggerated
    /// squash-and-stretch for the rest of the life; it clears automatically on death (the cat's
    /// SpringSquash resets itself when the player object respawns). Hides itself and respawns
    /// after GameConfig.powerupRespawnTime seconds.
    [RequireComponent(typeof(BoxCollider))]
    public class RubberPickup : MonoBehaviour, ILevelResettable
    {
        [Tooltip("Bobs up and down so it reads as a pickup, not a level block.")]
        public float bobHeight = 0.2f;
        public float bobSpeed = 2f;

        GameConfig config;
        Collider col;
        Vector3 baseLocalPos;
        bool taken;
        float respawnTimer;

        void Awake()
        {
            col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            baseLocalPos = transform.localPosition;
        }

        public void Init(GameConfig config) { this.config = config; }

        public void ResetToInitial()
        {
            taken = false;
            respawnTimer = 0f;
            transform.localPosition = baseLocalPos;
            SetVisible(true);
        }

        void Update()
        {
            if (taken)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f)
                {
                    taken = false;
                    SetVisible(true);
                }
                return;
            }
            var p = baseLocalPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var squash = player.GetComponentInChildren<SpringSquash>(true);
            if (squash != null) squash.SetRubber(true);
            taken = true;
            respawnTimer = config != null ? config.powerupRespawnTime : 10f;
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            col.enabled = visible;
        }
    }
}
