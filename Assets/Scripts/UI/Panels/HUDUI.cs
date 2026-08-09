using Economy;
using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Always-on HUD per GameDesignDoc: fuel and health change continuously during play so they're
    // polled every frame (same fillAmount-bar approach as InventoryUI's weight meter); dollars only
    // change on discrete earn/spend actions so that stays event-driven off DollarsChangedEvent.
    public class HUDUI : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Image fuelFillBar;
        [SerializeField] private TMP_Text fuelLabel;
        [SerializeField] private Image healthFillBar;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private Image weightFillBar;
        [SerializeField] private TMP_Text weightLabel;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private TMP_Text artifactCountLabel;

        private void Start()
        {
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();
            if (playerController == null) Debug.LogError("HUDUI: no PlayerController found in scene.");
            if (playerHealth == null) Debug.LogError("HUDUI: no PlayerHealth found in scene.");
            if (playerInventory == null) Debug.LogError("HUDUI: no PlayerInventory found in scene.");

            RefreshDollars();
            RefreshArtifactCount();
            RefreshWeight();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Add<InventoryChangedEvent>(RefreshWeight);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Remove<InventoryChangedEvent>(RefreshWeight);
        }

        private void Update()
        {
            RefreshFuel();
            RefreshHealth();
        }

        private void RefreshFuel()
        {
            if (playerController == null) return;
            if (fuelFillBar != null) fuelFillBar.fillAmount = Mathf.Clamp01(playerController.FuelFraction);
            if (fuelLabel != null) fuelLabel.text = $"{playerController.Fuel:0}";
        }

        private void RefreshHealth()
        {
            if (playerHealth == null) return;
            float fraction = playerHealth.MaxHp > 0f ? Mathf.Clamp01(playerHealth.CurrentHp / playerHealth.MaxHp) : 0f;
            if (healthFillBar != null) healthFillBar.fillAmount = fraction;
            if (healthLabel != null) healthLabel.text = $"{playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}";
        }

        private void RefreshWeight()
        {
            if (playerInventory == null) return;
            float fraction = playerInventory.MaxWeight > 0f
                ? Mathf.Clamp01(playerInventory.CurrentWeight / playerInventory.MaxWeight)
                : 0f;
            if (weightFillBar != null) weightFillBar.fillAmount = fraction;
            if (weightLabel != null) weightLabel.text = $"{playerInventory.CurrentWeight:0}/{playerInventory.MaxWeight:0}";
        }

        private void RefreshDollars()
        {
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }

        private void RefreshArtifactCount()
        {
            if (artifactCountLabel != null) artifactCountLabel.text = $"Artifacts: {Wallet.Instance.ArtifactCount}";
        }
    }
}
