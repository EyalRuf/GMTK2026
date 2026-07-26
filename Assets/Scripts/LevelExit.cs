using UnityEngine;

namespace NineLives
{
    /// Green pad at point B. Fires when the player touches it.
    public class LevelExit : MonoBehaviour, ILevelResettable
    {
        [Tooltip("Animator on the ExitPad root. Gets a 'Play' trigger the moment the pad is reached.")]
        [SerializeField] Animator padAnimator;

        static readonly int tPlay = Animator.StringToHash("Play");

        System.Action onReached;
        bool done;

        public void Init(System.Action callback) { onReached = callback; }

        /// Restarting the same level doesn't disable/re-enable the object, so the pad would stay
        /// stuck in its reached pose — put it back on Idle explicitly.
        public void ResetToInitial()
        {
            done = false;
            if (padAnimator != null && padAnimator.isActiveAndEnabled)
            {
                padAnimator.ResetTrigger(tPlay);
                padAnimator.Play("Idle", 0, 0f);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (done) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            done = true;
            if (padAnimator != null) padAnimator.SetTrigger(tPlay);
            onReached?.Invoke();
        }
    }
}
