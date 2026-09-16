using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Tabbed audio/video/control options, opened from PauseMenuUI's Options button (tab
    // switching itself is TabGroupUI, shared with ControlCenterUI's dashboards). Audio and Video
    // controls read/write through SettingsService (PlayerPrefs-backed, applies immediately as the
    // player drags/clicks). Quality/Resolution use prev/next cycle buttons rather than a dropdown -
    // this project has no TMP_Dropdown template asset, and a cycler matches the plain
    // button-driven look already used everywhere else (see DeathUI/ControlCenterUI). Controls is a
    // static legend rather than a rebinding UI - the project has no Input Actions asset, every key
    // is hardcoded via UnityEngine.InputSystem.Keyboard.
    public class OptionsUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button backButton;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Video")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Text qualityValueLabel;
        [SerializeField] private Button qualityPrevButton;
        [SerializeField] private Button qualityNextButton;
        [SerializeField] private TMP_Text resolutionValueLabel;
        [SerializeField] private Button resolutionPrevButton;
        [SerializeField] private Button resolutionNextButton;

        private Resolution[] resolutions;
        private int qualityIndex;
        private int resolutionIndex;
        private SettingsService settings => SettingsService.Instance;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("OptionsUI.rendererRoot is not assigned.");
            if (backButton != null) backButton.onClick.AddListener(Close);

            resolutions = Screen.resolutions;
            BindAudioControls();
            BindVideoControls();

            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        public void Open()
        {
            if (rendererRoot == null || rendererRoot.activeSelf) return;
            RefreshFromSettings();
            rendererRoot.SetActive(true);
        }

        public void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;
            rendererRoot.SetActive(false);
        }

        private void BindAudioControls()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(settings.SetMasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(settings.SetMusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(settings.SetSFXVolume);
        }

        private void BindVideoControls()
        {
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(settings.SetFullscreen);
            if (qualityPrevButton != null) qualityPrevButton.onClick.AddListener(() => StepQuality(-1));
            if (qualityNextButton != null) qualityNextButton.onClick.AddListener(() => StepQuality(1));
            if (resolutionPrevButton != null) resolutionPrevButton.onClick.AddListener(() => StepResolution(-1));
            if (resolutionNextButton != null) resolutionNextButton.onClick.AddListener(() => StepResolution(1));
        }

        private void StepQuality(int direction)
        {
            int count = QualitySettings.names.Length;
            if (count <= 0) return;
            qualityIndex = (qualityIndex + direction + count) % count;
            settings.SetQualityLevel(qualityIndex);
            RefreshQualityLabel();
        }

        private void StepResolution(int direction)
        {
            if (resolutions == null || resolutions.Length == 0) return;
            resolutionIndex = (resolutionIndex + direction + resolutions.Length) % resolutions.Length;
            settings.SetResolution(resolutionIndex, resolutions[resolutionIndex]);
            RefreshResolutionLabel();
        }

        private void RefreshFromSettings()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.SFXVolume);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);

            qualityIndex = Mathf.Clamp(settings.QualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            RefreshQualityLabel();

            resolutionIndex = settings.ResolutionIndex >= 0 ? settings.ResolutionIndex : FindCurrentResolutionIndex();
            RefreshResolutionLabel();
        }

        private int FindCurrentResolutionIndex()
        {
            if (resolutions == null) return 0;
            Resolution current = Screen.currentResolution;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == current.width && resolutions[i].height == current.height) return i;
            }
            return 0;
        }

        private void RefreshQualityLabel()
        {
            if (qualityValueLabel == null) return;
            string[] names = QualitySettings.names;
            qualityValueLabel.text = names.Length > 0 && qualityIndex >= 0 && qualityIndex < names.Length
                ? names[qualityIndex]
                : "-";
        }

        private void RefreshResolutionLabel()
        {
            if (resolutionValueLabel == null) return;
            if (resolutions == null || resolutionIndex < 0 || resolutionIndex >= resolutions.Length)
            {
                resolutionValueLabel.text = "-";
                return;
            }
            Resolution res = resolutions[resolutionIndex];
            resolutionValueLabel.text = $"{res.width} x {res.height}";
        }
    }
}
