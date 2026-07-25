using TMPro;
using UnityEngine;

namespace NineLives
{
    /// World-space message that floats above the player's head. Lives on the Player prefab
    /// (`LevelMsg` child); levels decide *what* it says by placing `MessageTrigger` volumes.
    /// While shown it pulses between `minAlpha` and `maxAlpha` so the eye is drawn to it, then
    /// fades out. All timings are Inspector-tunable for playtesting.
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerMessage : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] CanvasGroup group;

        [Header("Timing")]
        [Tooltip("Fallback display time when a trigger doesn't set its own.")]
        [SerializeField] float defaultDuration = 2f;
        [Tooltip("Alpha pulses per second while the message is up.")]
        [SerializeField] float flashesPerSecond = 2f;
        [SerializeField] float fadeInTime = 0.15f;
        [SerializeField] float fadeOutTime = 0.3f;

        [Header("Look")]
        [Range(0f, 1f)][SerializeField] float minAlpha = 0.15f;
        [Range(0f, 1f)][SerializeField] float maxAlpha = 1f;
        [Tooltip("Position above the player, in player-local space. Negative Z is toward the "
               + "camera — it keeps the text in front of level geometry.")]
        [SerializeField] Vector3 offset = new Vector3(0f, 1.15f, -1.5f);
        [Tooltip("Gentle vertical bob so the message doesn't read as flat UI.")]
        [SerializeField] float bobHeight = 0.06f;
        [SerializeField] float bobSpeed = 2f;

        float timer;
        float duration;
        float rate;
        bool showing;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            transform.localPosition = offset;
        }

        void OnEnable() { GameEvents.LevelEntered += OnLevelEntered; }
        void OnDisable() { GameEvents.LevelEntered -= OnLevelEntered; }
        void OnLevelEntered(Vector3 _) => Hide();

        /// `duration`/`flashRate` <= 0 fall back to the serialized defaults.
        public void Show(string text, float durationOverride = 0f, float flashRate = 0f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            label.text = text;
            duration = durationOverride > 0f ? durationOverride : defaultDuration;
            rate = flashRate > 0f ? flashRate : flashesPerSecond;
            timer = 0f;
            showing = true;
        }

        public void Hide()
        {
            showing = false;
            timer = 0f;
            group.alpha = 0f;
        }

        void LateUpdate()
        {
            if (!showing)
            {
                if (group.alpha > 0f) group.alpha = 0f;
                return;
            }

            timer += Time.deltaTime;
            if (timer >= duration)
            {
                Hide();
                return;
            }

            // Pulse, scaled by an in/out envelope so it doesn't pop on or cut off.
            float pulse = Mathf.Lerp(minAlpha, maxAlpha,
                0.5f + 0.5f * Mathf.Cos(timer * rate * Mathf.PI * 2f));
            float envIn = fadeInTime > 0f ? Mathf.Clamp01(timer / fadeInTime) : 1f;
            float envOut = fadeOutTime > 0f ? Mathf.Clamp01((duration - timer) / fadeOutTime) : 1f;
            group.alpha = pulse * envIn * envOut;

            var p = offset;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }
    }
}
