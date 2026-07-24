using UnityEngine;

namespace NineLives
{
    /// Green pad at point B. Fires when the player touches it.
    public class LevelExit : MonoBehaviour, ILevelResettable
    {
        System.Action onReached;
        bool done;

        public void Init(System.Action callback) { onReached = callback; }

        public void ResetToInitial() => done = false;

        void OnTriggerEnter(Collider other)
        {
            if (done) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            done = true;
            onReached?.Invoke();
        }
    }
}
