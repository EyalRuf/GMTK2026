using UnityEngine;

namespace NineLives
{
    /// One-shot box trigger that kicks off the ending cutscene: control drops, the camera frames
    /// devilTarget alongside the cat, then GameManager.EndingSequence takes over (fade to black,
    /// splash art + credits, fade back to the main menu). Placed once, in the final level.
    public class EndingTrigger : MonoBehaviour, ILevelResettable
    {
        [Tooltip("The devil sprite (the cat's owner) the ending camera frames alongside the cat.")]
        public Transform devilTarget;

        System.Action<Transform> onReached;
        bool done;

        public void Init(System.Action<Transform> callback) { onReached = callback; }

        public void ResetToInitial() => done = false;

        void OnTriggerEnter(Collider other)
        {
            if (done) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            done = true;
            onReached?.Invoke(devilTarget);
        }
    }
}
