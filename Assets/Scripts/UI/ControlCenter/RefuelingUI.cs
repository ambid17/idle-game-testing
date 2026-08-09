using Automation;
using Economy;
using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "refueling" tab: fuel meter (HUDUI's fill-bar approach), cost display, a
    // slider for how many units to buy, and a purchase button that spends Wallet dollars and calls
    // PlayerController.AddFuel directly - matching DepotUI.sellAllButton's precedent of same-panel
    // buttons calling logic directly rather than round-tripping through a request event.
    public class RefuelingUI : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Image fuelFillBar;
        [SerializeField] private TMP_Text fuelLabel;
        [SerializeField] private TMP_Text costPerUnitLabel;
        [SerializeField] private Slider purchaseAmountSlider;
        [SerializeField] private TMP_Text purchaseAmountLabel;
        [SerializeField] private Button purchaseButton;

        private AutomationConfig config => GameManager.AutomationConfig;

        private void Start()
        {
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerController == null) Debug.LogError("RefuelingUI: no PlayerController found in scene.");

            if (purchaseButton != null) purchaseButton.onClick.AddListener(Purchase);
            if (purchaseAmountSlider != null) purchaseAmountSlider.onValueChanged.AddListener(_ => RefreshPurchaseLabel());
            if (costPerUnitLabel != null) costPerUnitLabel.text = $"${config.FuelCostPerUnit:0.##}/unit";
        }

        private void OnEnable() => GameManager.EventService.Add<DollarsChangedEvent>(RefreshPurchaseLabel);
        private void OnDisable() => GameManager.EventService.Remove<DollarsChangedEvent>(RefreshPurchaseLabel);

        private void Update()
        {
            if (playerController == null) return;

            if (fuelFillBar != null) fuelFillBar.fillAmount = Mathf.Clamp01(playerController.FuelFraction);
            if (fuelLabel != null) fuelLabel.text = $"{playerController.Fuel:0}/{playerController.FuelMax:0}";
            if (purchaseAmountSlider != null) purchaseAmountSlider.maxValue = Mathf.Max(0f, playerController.FuelMissing);
        }

        private void RefreshPurchaseLabel()
        {
            if (purchaseAmountLabel == null || purchaseAmountSlider == null) return;
            double cost = purchaseAmountSlider.value * config.FuelCostPerUnit;
            purchaseAmountLabel.text = $"{purchaseAmountSlider.value:0} units (${cost:0.##})";
        }

        private void Purchase()
        {
            if (playerController == null || purchaseAmountSlider == null) return;

            float units = purchaseAmountSlider.value;
            if (units <= 0f) return;

            if (!Wallet.Instance.TrySpend(units * config.FuelCostPerUnit)) return;
            playerController.AddFuel(units);
        }
    }
}
