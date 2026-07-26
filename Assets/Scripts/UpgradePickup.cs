using UnityEngine;

namespace NineLives
{
    /// Editor-placeable pickup. Touch it to arm the next-corpse upgrade; it hides itself and
    /// respawns after GameConfig.powerupRespawnTime seconds.
    [RequireComponent(typeof(BoxCollider))]
    public class UpgradePickup : MonoBehaviour, ILevelResettable
    {
        public UpgradeType upgrade = UpgradeType.Trampoline;
        [Tooltip("Bobs up and down so it reads as a pickup, not a level block.")]
        public float bobHeight = 0.2f;
        public float bobSpeed = 2f;

        System.Action<UpgradeType> onPickedUp;
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

        public void Init(GameConfig config, System.Action<UpgradeType> callback)
        {
            this.config = config;
            onPickedUp = callback;
        }

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
            if (other.GetComponentInParent<PlayerController>() == null) return;
            taken = true;
            respawnTimer = config != null ? config.powerupRespawnTime : 10f;
            onPickedUp?.Invoke(upgrade);
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            col.enabled = visible;
        }
    }
}
