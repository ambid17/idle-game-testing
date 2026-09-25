using System.Collections.Generic;
using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Tabbed audio/video/control options, opened from PauseMenuUI's Options button (tab
    // switching itself is TabGroupUI, shared with ControlCenterUI's dashboards). Audio and Video
    // controls read/write through SettingsService (PlayerPrefs-backed, applies immediately as the
    // player drags/clicks). Quality/Resolution use TMP_Dropdowns. The Controls tab is its own
    // component (KeybindsUI) backed by Settings.KeybindService.
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
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown refreshRateDropdown;

        private Resolution[] resolutions;
        // Screen.resolutions has one entry per supported refresh rate per resolution, so the
        // resolution dropdown lists each (width, height) once here, and currentRefreshRates
        // holds the refresh rates available for whichever resolution is currently selected.
        private readonly List<(int width, int height)> uniqueResolutions = new();
        private readonly List<RefreshRate> currentRefreshRates = new();
        private SettingsService settings => SettingsService.Instance;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError("OptionsUI.rendererRoot is not assigned.");
            if (backButton != null) backButton.onClick.AddListener(Close);

            resolutions = Screen.resolutions;
            BuildUniqueResolutions();
            BindAudioControls();
            BindVideoControls();
            PopulateQualityOptions();
            PopulateResolutionOptions();

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
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(settings.SetQualityLevel);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionSelected);
            if (refreshRateDropdown != null) refreshRateDropdown.onValueChanged.AddListener(OnRefreshRateSelected);
        }

        private void BuildUniqueResolutions()
        {
            uniqueResolutions.Clear();
            if (resolutions == null) return;
            foreach (Resolution res in resolutions)
            {
                if (!uniqueResolutions.Contains((res.width, res.height)))
                {
                    uniqueResolutions.Add((res.width, res.height));
                }
            }
        }

        private void OnResolutionSelected(int index)
        {
            if (index < 0 || index >= uniqueResolutions.Count) return;
            PopulateRefreshRateOptions(index);
            int refreshRateIndex = FindRefreshRateIndex(settings.RefreshRateNumerator, settings.RefreshRateDenominator);
            if (refreshRateIndex < 0) refreshRateIndex = 0;
            if (refreshRateDropdown != null) refreshRateDropdown.SetValueWithoutNotify(refreshRateIndex);
            ApplyResolution(index, refreshRateIndex);
        }

        private void OnRefreshRateSelected(int index)
        {
            int resolutionIndex = resolutionDropdown != null ? resolutionDropdown.value : 0;
            ApplyResolution(resolutionIndex, index);
        }

        private void ApplyResolution(int resolutionIndex, int refreshRateIndex)
        {
            if (resolutionIndex < 0 || resolutionIndex >= uniqueResolutions.Count) return;
            if (refreshRateIndex < 0 || refreshRateIndex >= currentRefreshRates.Count) return;
            (int width, int height) = uniqueResolutions[resolutionIndex];
            settings.SetResolution(width, height, currentRefreshRates[refreshRateIndex]);
        }

        private void PopulateQualityOptions()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        private void PopulateResolutionOptions()
        {
            if (resolutionDropdown == null) return;
            var options = new List<string>(uniqueResolutions.Count);
            foreach ((int width, int height) in uniqueResolutions)
            {
                options.Add($"{width} x {height}");
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }

        // Refresh rates are resolution-specific (a monitor may only support 144Hz at a lower
        // resolution, for example), so this repopulates whenever the selected resolution changes.
        private void PopulateRefreshRateOptions(int resolutionIndex)
        {
            currentRefreshRates.Clear();
            if (refreshRateDropdown == null) return;

            if (resolutionIndex >= 0 && resolutionIndex < uniqueResolutions.Count && resolutions != null)
            {
                (int width, int height) = uniqueResolutions[resolutionIndex];
                foreach (Resolution res in resolutions)
                {
                    if (res.width == width && res.height == height)
                    {
                        currentRefreshRates.Add(res.refreshRateRatio);
                    }
                }
            }

            var options = new List<string>(currentRefreshRates.Count);
            foreach (RefreshRate rate in currentRefreshRates)
            {
                options.Add($"{Mathf.RoundToInt((float)rate.value)} Hz");
            }

            refreshRateDropdown.ClearOptions();
            refreshRateDropdown.AddOptions(options);
        }

        private int FindRefreshRateIndex(int numerator, int denominator)
        {
            for (int i = 0; i < currentRefreshRates.Count; i++)
            {
                if (currentRefreshRates[i].numerator == (uint)numerator && currentRefreshRates[i].denominator == (uint)denominator) return i;
            }
            return -1;
        }

        private void RefreshFromSettings()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.SFXVolume);
            settings.SyncFullscreenState();
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);

            if (qualityDropdown != null)
            {
                int qualityIndex = Mathf.Clamp(settings.QualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
                qualityDropdown.SetValueWithoutNotify(qualityIndex);
            }

            if (resolutionDropdown != null)
            {
                int resolutionIndex = FindResolutionIndex(settings.ResolutionWidth, settings.ResolutionHeight);
                if (resolutionIndex < 0) resolutionIndex = FindResolutionIndex(Screen.currentResolution.width, Screen.currentResolution.height);
                if (resolutionIndex < 0) resolutionIndex = 0;
                resolutionDropdown.SetValueWithoutNotify(resolutionIndex);

                PopulateRefreshRateOptions(resolutionIndex);
                if (refreshRateDropdown != null)
                {
                    Resolution currentRes = Screen.currentResolution;
                    int refreshRateIndex = FindRefreshRateIndex(settings.RefreshRateNumerator, settings.RefreshRateDenominator);
                    if (refreshRateIndex < 0) refreshRateIndex = FindRefreshRateIndex((int)currentRes.refreshRateRatio.numerator, (int)currentRes.refreshRateRatio.denominator);
                    if (refreshRateIndex < 0) refreshRateIndex = 0;
                    refreshRateDropdown.SetValueWithoutNotify(refreshRateIndex);
                }
            }
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < uniqueResolutions.Count; i++)
            {
                if (uniqueResolutions[i].width == width && uniqueResolutions[i].height == height) return i;
            }
            return -1;
        }
    }
}
