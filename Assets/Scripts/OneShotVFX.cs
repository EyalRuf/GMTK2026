using UnityEngine;

namespace NineLives
{
    /// Placeholder one-shot particle burst. FXManager pools these: Play() enables + plays the
    /// ParticleSystem, then the object deactivates itself once the system finishes so it can be
    /// reused. Swap the ParticleSystem/material on the prefab for real art later — no code change.
    [RequireComponent(typeof(ParticleSystem))]
    public class OneShotVFX : MonoBehaviour
    {
        ParticleSystem ps;
        float dieAt;
        bool playing;

        void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
        }

        public void Play(Vector3 position, Color? tint = null)
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            transform.position = position;
            gameObject.SetActive(true);
            if (tint.HasValue)
            {
                var main = ps.main;
                main.startColor = tint.Value;
            }
            ps.Clear(true);
            ps.Play(true);
            var m = ps.main;
            dieAt = Time.unscaledTime + m.duration + m.startLifetime.constantMax + 0.1f;
            playing = true;
        }

        void Update()
        {
            if (!playing) return;
            if (Time.unscaledTime >= dieAt)
            {
                playing = false;
                gameObject.SetActive(false);
            }
        }
    }
}
