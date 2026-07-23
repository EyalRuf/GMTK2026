using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NineLives
{
    /// Data-bound to the hand-built hierarchy in MenuUI.prefab (Main/Pause/LevelSelect/
    /// Settings panels) — no layout or GameObject construction here, just wiring.
    public class MenuUI : MonoBehaviour
    {
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject pausePanel;
        [SerializeField] GameObject levelSelectPanel;
        [SerializeField] GameObject settingsPanel;

        [SerializeField] Button startButton;
        [SerializeField] Text startButtonLabel;
        [SerializeField] Button mainLevelSelectButton;
        [SerializeField] Button mainSettingsButton;

        [SerializeField] Button pauseContinueButton;
        [SerializeField] Button pauseLevelSelectButton;
        [SerializeField] Button pauseSettingsButton;
        [SerializeField] Button pauseBackToMenuButton;

        [SerializeField] List<Button> levelButtons = new();
        [SerializeField] List<Text> levelButtonTexts = new();
        [SerializeField] Button levelSelectBackButton;

        [SerializeField] Slider masterSlider;
        [SerializeField] Text masterValueLabel;
        [SerializeField] Slider musicSlider;
        [SerializeField] Text musicValueLabel;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Text sfxValueLabel;
        [SerializeField] Button settingsBackButton;

        GameConfig config;
        List<LevelRoot> levels;
        AudioSource sfxSource;
        AudioSource musicSource;
        Action<int> onLevelChosen;
        Action onResume;
        Action onBackToMenu;
        bool cameFromPause;

        public void BuildUI(GameConfig cfg, List<LevelRoot> lvls, AudioSource sfx, AudioSource music,
            Action<int> levelChosen, Action resume, Action backToMenu)
        {
            config = cfg;
            levels = lvls;
            sfxSource = sfx;
            musicSource = music;
            onLevelChosen = levelChosen;
            onResume = resume;
            onBackToMenu = backToMenu;

            EnsureEventSystem();

            startButton.onClick.AddListener(() =>
                onLevelChosen?.Invoke(Mathf.Clamp(SaveData.HighestUnlockedLevel, 0, Mathf.Max(0, levels.Count - 1))));
            mainLevelSelectButton.onClick.AddListener(OpenLevelSelect);
            mainSettingsButton.onClick.AddListener(OpenSettings);

            pauseContinueButton.onClick.AddListener(() => onResume?.Invoke());
            pauseLevelSelectButton.onClick.AddListener(OpenLevelSelect);
            pauseSettingsButton.onClick.AddListener(OpenSettings);
            pauseBackToMenuButton.onClick.AddListener(() => onBackToMenu?.Invoke());

            for (int i = 0; i < levelButtons.Count; i++)
            {
                int idx = i;
                levelButtons[i].onClick.AddListener(() => onLevelChosen?.Invoke(idx));
            }
            levelSelectBackButton.onClick.AddListener(BackFromSub);
            settingsBackButton.onClick.AddListener(BackFromSub);

            masterSlider.value = SaveData.MasterVolume;
            musicSlider.value = SaveData.MusicVolume;
            sfxSlider.value = SaveData.SfxVolume;
            SetSliderLabel(masterValueLabel, masterSlider.value);
            SetSliderLabel(musicValueLabel, musicSlider.value);
            SetSliderLabel(sfxValueLabel, sfxSlider.value);

            masterSlider.onValueChanged.AddListener(v =>
            {
                SaveData.MasterVolume = v;
                AudioListener.volume = v;
                SetSliderLabel(masterValueLabel, v);
            });
            musicSlider.onValueChanged.AddListener(v =>
            {
                SaveData.MusicVolume = v;
                if (musicSource != null) musicSource.volume = v;
                SetSliderLabel(musicValueLabel, v);
            });
            sfxSlider.onValueChanged.AddListener(v =>
            {
                SaveData.SfxVolume = v;
                if (sfxSource != null) sfxSource.volume = v;
                SetSliderLabel(sfxValueLabel, v);
            });

            HideAll();
        }

        static void SetSliderLabel(Text label, float v) => label.text = Mathf.RoundToInt(v * 100) + "%";

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public void ShowMainMenu()
        {
            HideAll();
            cameFromPause = false;
            startButtonLabel.text = SaveData.HighestUnlockedLevel > 0 ? "CONTINUE" : "START";
            mainPanel.SetActive(true);
        }

        public void ShowPause()
        {
            HideAll();
            cameFromPause = true;
            pausePanel.SetActive(true);
        }

        public void Hide() => HideAll();

        void HideAll()
        {
            mainPanel.SetActive(false);
            pausePanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        void OpenLevelSelect()
        {
            HideAll();
            RefreshLevelButtons();
            levelSelectPanel.SetActive(true);
        }

        void OpenSettings()
        {
            HideAll();
            settingsPanel.SetActive(true);
        }

        void BackFromSub()
        {
            HideAll();
            if (cameFromPause) pausePanel.SetActive(true);
            else mainPanel.SetActive(true);
        }

        void RefreshLevelButtons()
        {
            int unlocked = SaveData.HighestUnlockedLevel;
            for (int i = 0; i < levelButtons.Count; i++)
            {
                bool isLevel = i < levels.Count;
                levelButtons[i].gameObject.SetActive(isLevel);
                if (!isLevel) continue;

                bool reachable = i <= unlocked || config.unlockAllLevelsForTesting;
                levelButtons[i].interactable = reachable;
                levelButtonTexts[i].text = $"LEVEL {i + 1} — {levels[i].levelName}";
                levelButtonTexts[i].color = reachable ? Color.white : new Color(1, 1, 1, 0.35f);
            }
        }
    }
}
