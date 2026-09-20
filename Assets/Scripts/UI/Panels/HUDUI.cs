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
        [SerializeField] private GameObject renderer;
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
        [SerializeField] private TMP_Text depthLabel;

        private void Start()
        {
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();
            CheckNullRefs();

            renderer.SetActive(true);
            RefreshDollars();
            RefreshArtifactCount();
            RefreshWeight();
        }

        private void CheckNullRefs()
        {
            if (renderer == null) Debug.LogError("HUDUI: no renderer found in scene.");
            if (playerController == null) Debug.LogError("HUDUI: no PlayerController found in scene.");
            if (playerHealth == null) Debug.LogError("HUDUI: no PlayerHealth found in scene.");
            if (playerInventory == null) Debug.LogError("HUDUI: no PlayerInventory found in scene.");
            if (depthLabel == null) Debug.LogError("HUDUI: no depthLabel found in scene.");

            if (fuelFillBar == null) Debug.LogError("HUDUI: no fuelFillBar found in scene.");
            if (fuelLabel == null) Debug.LogError("HUDUI: no fuelLabel found in scene.");
            if (healthFillBar == null) Debug.LogError("HUDUI: no healthFillBar found in scene.");
            if (healthLabel == null) Debug.LogError("HUDUI: no healthLabel found in scene.");
            if (weightFillBar == null) Debug.LogError("HUDUI: no weightFillBar found in scene.");
            if (weightLabel == null) Debug.LogError("HUDUI: no weightLabel found in scene.");
            if (dollarsLabel == null) Debug.LogError("HUDUI: no dollarsLabel found in scene.");
            if (artifactCountLabel == null) Debug.LogError("HUDUI: no artifactCountLabel found in scene.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Add<InventoryChangedEvent>(RefreshWeight);
            GameManager.EventService.Add<UpgradePurchasedEvent>(HandleUpdatePurchased);

        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Remove<InventoryChangedEvent>(RefreshWeight);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(HandleUpdatePurchased);
        }

        private void Update()
        {
            RefreshFuel();
            RefreshHealth();
            RefreshDepth();
        }

        private void RefreshFuel()
        {
            fuelFillBar.fillAmount = Mathf.Clamp01(playerController.FuelFraction);
            fuelLabel.text = $"{playerController.Fuel:0}";
        }

        private void RefreshHealth()
        {
            float fraction = playerHealth.MaxHp > 0f ? Mathf.Clamp01(playerHealth.CurrentHp / playerHealth.MaxHp) : 0f;
            healthFillBar.fillAmount = fraction;
            healthLabel.text = $"{playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}";
        }

        private void RefreshWeight()
        {
            float fraction = playerInventory.MaxWeight > 0f
                ? Mathf.Clamp01(playerInventory.CurrentWeight / playerInventory.MaxWeight)
                : 0f;
            weightFillBar.fillAmount = fraction;
            weightLabel.text = $"{playerInventory.CurrentWeight:0}/{playerInventory.MaxWeight:0}";
        }

        private void RefreshDollars()
        {
            dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }

        private void RefreshArtifactCount()
        {
            artifactCountLabel.text = $"{Wallet.Instance.ArtifactCount}";
        }

        private void RefreshDepth()
        {
            float depth = playerController.transform.position.y;
            depthLabel.text = $"Depth: {depth:0}m";
        }

        private void HandleUpdatePurchased(UpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Economy_InventoryCapacity)
            {
                RefreshWeight();
            }
        }
    }
}
