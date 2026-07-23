using UnityEngine;

namespace NineLives
{
    /// Persists progress + audio settings via PlayerPrefs (works in WebGL builds too).
    public static class SaveData
    {
        const string KeyHighestUnlocked = "NineLives.HighestUnlockedLevel";
        const string KeyMaster = "NineLives.MasterVolume";
        const string KeyMusic = "NineLives.MusicVolume";
        const string KeySfx = "NineLives.SfxVolume";

        public static int HighestUnlockedLevel
        {
            get => PlayerPrefs.GetInt(KeyHighestUnlocked, 0);
            set { PlayerPrefs.SetInt(KeyHighestUnlocked, value); PlayerPrefs.Save(); }
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 1f);
            set { PlayerPrefs.SetFloat(KeyMaster, value); PlayerPrefs.Save(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusic, 1f);
            set { PlayerPrefs.SetFloat(KeyMusic, value); PlayerPrefs.Save(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeySfx, 1f);
            set { PlayerPrefs.SetFloat(KeySfx, value); PlayerPrefs.Save(); }
        }
    }
}
