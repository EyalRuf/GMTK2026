using UnityEngine;

namespace NineLives
{
    /// Editor-placeable one-time pickup. Touch it to arm the next-corpse upgrade;
    /// it then disappears until the level restarts.
    [RequireComponent(typeof(BoxCollider))]
    public class UpgradePickup : MonoBehaviour, ILevelResettable
    {
        public UpgradeType upgrade = UpgradeType.Trampoline;
        [Tooltip("Bobs up and down so it reads as a pickup, not a level block.")]
        public float bobHeight = 0.2f;
        public float bobSpeed = 2f;

        System.Action<UpgradeType> onPickedUp;
        Vector3 baseLocalPos;
        bool taken;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            baseLocalPos = transform.localPosition;
        }

        public void Init(System.Action<UpgradeType> callback) { onPickedUp = callback; }

        public void ResetToInitial()
        {
            taken = false;
            transform.localPosition = baseLocalPos;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (taken) return;
            var p = baseLocalPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            taken = true;
            onPickedUp?.Invoke(upgrade);
            gameObject.SetActive(false);
        }
    }
}
