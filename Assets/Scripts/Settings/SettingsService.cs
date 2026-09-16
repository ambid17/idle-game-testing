using UnityEngine;

namespace Settings
{
    // Persists and applies user-configurable audio/video settings via PlayerPrefs, backing the
    // Options screen (UI.OptionsUI) opened from the pause menu. MasterVolume is the only setting
    // with an audible effect today (drives AudioListener.volume) - Music/SFX volumes are stored
    // for future AudioSource-driven music/sfx to read, since the project has no audio content yet.
    public class SettingsService : Singleton<SettingsService>
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string MusicVolumeKey = "Settings.MusicVolume";
        private const string SFXVolumeKey = "Settings.SFXVolume";
        private const string FullscreenKey = "Settings.Fullscreen";
        private const string QualityLevelKey = "Settings.QualityLevel";
        private const string ResolutionIndexKey = "Settings.ResolutionIndex";

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SFXVolume { get; private set; } = 1f;
        public bool Fullscreen { get; private set; }
        public int QualityLevel { get; private set; }
        // -1 until the player explicitly picks a resolution - avoids forcing a resolution change
        // on boot before any choice has been made.
        public int ResolutionIndex { get; private set; }

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
        }

        public void SetSFXVolume(float value)
        {
            SFXVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
        }

        public void SetFullscreen(bool value)
        {
            Fullscreen = value;
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        public void SetQualityLevel(int qualityIndex)
        {
            QualityLevel = qualityIndex;
            QualitySettings.SetQualityLevel(qualityIndex, true);
            PlayerPrefs.SetInt(QualityLevelKey, qualityIndex);
        }

        public void SetResolution(int resolutionIndex, Resolution resolution)
        {
            ResolutionIndex = resolutionIndex;
            Screen.SetResolution(resolution.width, resolution.height, Fullscreen);
            PlayerPrefs.SetInt(ResolutionIndexKey, resolutionIndex);
        }

        private void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            QualityLevel = PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel());
            ResolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, -1);
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            Screen.fullScreen = Fullscreen;
            QualitySettings.SetQualityLevel(QualityLevel, true);
        }
    }
}
