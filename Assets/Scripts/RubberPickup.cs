using UnityEngine;

namespace NineLives
{
    /// Self-contained "rubber body" pickup. Touch it and the cat's SpringSquash starts showing
    /// exaggerated squash-and-stretch for the rest of the life; it clears automatically on death
    /// (the cat's SpringSquash resets itself when the player object respawns). No GameManager or
    /// GameConfig involvement — drop the prefab in a level and it works.
    [RequireComponent(typeof(BoxCollider))]
    public class RubberPickup : MonoBehaviour, ILevelResettable
    {
        [Tooltip("Bobs up and down so it reads as a pickup, not a level block.")]
        public float bobHeight = 0.2f;
        public float bobSpeed = 2f;

        Vector3 baseLocalPos;
        bool taken;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            baseLocalPos = transform.localPosition;
        }

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
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var squash = player.GetComponentInChildren<SpringSquash>(true);
            if (squash != null) squash.SetRubber(true);
            taken = true;
            gameObject.SetActive(false);
        }
    }
}
