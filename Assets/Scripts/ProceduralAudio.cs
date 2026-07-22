using UnityEngine;

namespace NineLives
{
    /// Placeholder blips generated in code — no audio assets to import.
    public static class ProceduralAudio
    {
        const int Rate = 44100;

        public static AudioClip Jump()    => Tone("sfx_jump", 520f, 900f, 0.12f, 0.35f);
        public static AudioClip Bounce()  => Tone("sfx_bounce", 300f, 780f, 0.16f, 0.4f);
        public static AudioClip Death()   => Tone("sfx_death", 440f, 90f, 0.35f, 0.45f);
        public static AudioClip Plate()   => Tone("sfx_plate", 660f, 660f, 0.06f, 0.3f);
        public static AudioClip Land()    => Tone("sfx_land", 200f, 140f, 0.07f, 0.3f);
        public static AudioClip Win()     => Arp("sfx_win", new[] { 523f, 659f, 784f, 1046f }, 0.4f, 0.4f);
        public static AudioClip Fail()    => Arp("sfx_fail", new[] { 440f, 349f, 262f }, 0.4f, 0.4f);
        public static AudioClip Tick()    => Tone("sfx_tick", 1200f, 1200f, 0.04f, 0.25f);

        static AudioClip Tone(string name, float f0, float f1, float dur, float vol)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t);
                float env = Mathf.Sin(Mathf.PI * t);          // fade in/out
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * ((float)i / Rate)));
                data[i] = sq * env * vol * 0.5f;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arp(string name, float[] notes, float dur, float vol)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            int per = n / notes.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = Mathf.Min(i / per, notes.Length - 1);
                float lt = (float)(i % per) / per;
                float env = Mathf.Sin(Mathf.PI * lt);
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * notes[idx] * ((float)i / Rate)));
                data[i] = sq * env * vol * 0.5f;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
