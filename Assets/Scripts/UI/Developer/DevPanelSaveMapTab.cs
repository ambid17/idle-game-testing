using MapGeneration;
using Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: force a save/load, regenerate the map with a chosen (or random) seed
    // without going through a full prestige reset, or wipe the save entirely and reload.
    public class DevPanelSaveMapTab : MonoBehaviour
    {
        [SerializeField] private Button saveNowButton;
        [SerializeField] private Button loadNowButton;
        [SerializeField] private TMP_InputField seedInput;
        [SerializeField] private Button regenerateMapButton;
        [SerializeField] private Button deleteSaveButton;
        [SerializeField] private Button reloadSceneButton;

        private void Start()
        {
            if (saveNowButton == null) Debug.LogError("DevPanelSaveMapTab.saveNowButton is not assigned.");
            if (loadNowButton == null) Debug.LogError("DevPanelSaveMapTab.loadNowButton is not assigned.");
            if (seedInput == null) Debug.LogError("DevPanelSaveMapTab.seedInput is not assigned.");
            if (regenerateMapButton == null) Debug.LogError("DevPanelSaveMapTab.regenerateMapButton is not assigned.");
            if (deleteSaveButton == null) Debug.LogError("DevPanelSaveMapTab.deleteSaveButton is not assigned.");
            if (reloadSceneButton == null) Debug.LogError("DevPanelSaveMapTab.reloadSceneButton is not assigned.");

            if (saveNowButton != null) saveNowButton.onClick.AddListener(() => SaveService.Instance.Save());
            if (loadNowButton != null) loadNowButton.onClick.AddListener(OnLoadNowClicked);
            if (regenerateMapButton != null) regenerateMapButton.onClick.AddListener(OnRegenerateMapClicked);
            if (deleteSaveButton != null) deleteSaveButton.onClick.AddListener(OnDeleteSaveClicked);
            if (reloadSceneButton != null) reloadSceneButton.onClick.AddListener(OnReloadSceneClicked);
        }

        private void OnLoadNowClicked() => SaveService.Instance.ApplyLoadedData(SaveService.Instance.Load());

        private void OnRegenerateMapClicked()
        {
            int seed = seedInput != null && int.TryParse(seedInput.text, out var parsed)
                ? parsed
                : Random.Range(int.MinValue, int.MaxValue);
            GameManager.MapGenerationService.PrestigeReset(seed);
        }

        private void OnDeleteSaveClicked()
        {
            SaveService.Instance.DeleteSaveData();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnReloadSceneClicked()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
