using UnityEngine;

namespace Settings
{
    // Persists and applies user-configurable audio/video settings via PlayerPrefs, backing the
    // Options screen (UI.OptionsUI) opened from the pause menu. MasterVolume drives
    // AudioListener.volume; Music/SFX volumes are read by Audio.AudioService, which is told to
    // re-apply them whenever they change here.
    public class SettingsService : Singleton<SettingsService>
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string MusicVolumeKey = "Settings.MusicVolume";
        private const string SFXVolumeKey = "Settings.SFXVolume";
        private const string FullscreenKey = "Settings.Fullscreen";
        private const string QualityLevelKey = "Settings.QualityLevel";
        private const string ResolutionWidthKey = "Settings.ResolutionWidth";
        private const string ResolutionHeightKey = "Settings.ResolutionHeight";
        private const string RefreshRateNumeratorKey = "Settings.RefreshRateNumerator";
        private const string RefreshRateDenominatorKey = "Settings.RefreshRateDenominator";

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SFXVolume { get; private set; } = 1f;
        public bool Fullscreen { get; private set; }
        public int QualityLevel { get; private set; }
        // -1 until the player explicitly picks a resolution - avoids forcing a resolution change
        // on boot before any choice has been made. Storing width/height/refresh rate directly
        // (rather than an index into Screen.resolutions) since that array's order/contents can
        // vary between sessions and contains one entry per supported refresh rate per resolution.
        public int ResolutionWidth { get; private set; } = -1;
        public int ResolutionHeight { get; private set; } = -1;
        public int RefreshRateNumerator { get; private set; }
        public int RefreshRateDenominator { get; private set; } = 1;

        protected override void Initialize()
        {
            base.Initialize();
            Load();
            Apply();
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            GameManager.AudioService.ApplyVolumes();
        }

        public void SetSFXVolume(float value)
        {
            SFXVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
            GameManager.AudioService.ApplyVolumes();
        }

        public void SetFullscreen(bool value)
        {
            Fullscreen = value;
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        // Screen.fullScreen can change outside of SetFullscreen (e.g. the platform's own
        // fullscreen hotkey), leaving the cached Fullscreen value stale. Called before the
        // Options screen displays its toggle so the UI reflects reality.
        public void SyncFullscreenState()
        {
            Fullscreen = Screen.fullScreen;
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
        }

        public void SetQualityLevel(int qualityIndex)
        {
            QualityLevel = qualityIndex;
            QualitySettings.SetQualityLevel(qualityIndex, true);
            PlayerPrefs.SetInt(QualityLevelKey, qualityIndex);
        }

        public void SetResolution(int width, int height, RefreshRate refreshRate)
        {
            ResolutionWidth = width;
            ResolutionHeight = height;
            RefreshRateNumerator = (int)refreshRate.numerator;
            RefreshRateDenominator = (int)refreshRate.denominator;
            Screen.SetResolution(width, height, Screen.fullScreenMode, refreshRate);
            PlayerPrefs.SetInt(ResolutionWidthKey, width);
            PlayerPrefs.SetInt(ResolutionHeightKey, height);
            PlayerPrefs.SetInt(RefreshRateNumeratorKey, RefreshRateNumerator);
            PlayerPrefs.SetInt(RefreshRateDenominatorKey, RefreshRateDenominator);
        }

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            QualityLevel = PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel());
            ResolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey, -1);
            ResolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey, -1);
            RefreshRateNumerator = PlayerPrefs.GetInt(RefreshRateNumeratorKey, 0);
            RefreshRateDenominator = PlayerPrefs.GetInt(RefreshRateDenominatorKey, 1);
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            Screen.fullScreen = Fullscreen;
            QualitySettings.SetQualityLevel(QualityLevel, true);

            if (ResolutionWidth > 0 && ResolutionHeight > 0)
            {
                RefreshRate refreshRate = new RefreshRate { numerator = (uint)RefreshRateNumerator, denominator = (uint)Mathf.Max(1, RefreshRateDenominator) };
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, Screen.fullScreenMode, refreshRate);
            }
        }
    }
}
