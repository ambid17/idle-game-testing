using Automation;
using Economy;
using Events;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "heal" tab: HP meter, cost display, a slider for how many HP to buy, and a
    // purchase button that spends Wallet dollars and calls PlayerHealth.AddHp directly - mirrors
    // RefuelingUI (fuel and HP are the two player-owned refill resources on this panel).
    public class HealUI : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Image hpFillBar;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private TMP_Text costPerUnitLabel;
        [SerializeField] private Slider purchaseAmountSlider;
        [SerializeField] private TMP_Text purchaseAmountLabel;
        [SerializeField] private Button purchaseButton;

        private AutomationConfig config => GameManager.AutomationConfig;

        private void Start()
        {
            if (playerHealth == null) Debug.LogError("HealUI: playerHealth not assigned in the Inspector.");

            if (purchaseButton != null) purchaseButton.onClick.AddListener(Purchase);
            if (purchaseAmountSlider != null) purchaseAmountSlider.onValueChanged.AddListener(_ => RefreshPurchaseLabel());
            if (costPerUnitLabel != null) costPerUnitLabel.text = $"${config.HpCostPerUnit:0.##}/unit";
        }

        private void OnEnable() => GameManager.EventService.Add<DollarsChangedEvent>(RefreshPurchaseLabel);
        private void OnDisable() => GameManager.EventService.Remove<DollarsChangedEvent>(RefreshPurchaseLabel);

        private void Update()
        {
            if (playerHealth == null) return;

            float missing = Mathf.Max(0f, playerHealth.MaxHp - playerHealth.CurrentHp);

            if (hpFillBar != null) hpFillBar.fillAmount = playerHealth.MaxHp > 0f ? Mathf.Clamp01(playerHealth.CurrentHp / playerHealth.MaxHp) : 0f;
            if (hpLabel != null) hpLabel.text = $"{playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}";
            if (purchaseAmountSlider != null) purchaseAmountSlider.maxValue = missing;
            if (purchaseButton != null) purchaseButton.interactable = missing > 0f;
        }

        private void RefreshPurchaseLabel()
        {
            if (purchaseAmountLabel == null || purchaseAmountSlider == null) return;
            double cost = purchaseAmountSlider.value * config.HpCostPerUnit;
            purchaseAmountLabel.text = $"{purchaseAmountSlider.value:0} units (${cost:0.##})";
        }

        private void Purchase()
        {
            if (playerHealth == null || purchaseAmountSlider == null) return;

            float units = purchaseAmountSlider.value;
            if (units <= 0f) return;

            if (!Wallet.Instance.TrySpend(units * config.HpCostPerUnit)) return;
            playerHealth.AddHp(units);
        }
    }
}
