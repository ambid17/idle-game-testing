using Automation;
using Economy;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Control Center "Supplies" tab: replaces the old slider-based RefuelingUI/HealUI with plain
    // buy-1/fill-to-max buttons for both jetpack fuel and player HP - sliders were fiddly to use
    // for a value most players just want at max or nudged up by one.
    public class ResourceRefillUI : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerHealth playerHealth;

        [SerializeField] private Image fuelFillBar;
        [SerializeField] private TMP_Text fuelLabel;
        [SerializeField] private Button buyFuelUnitButton;
        [SerializeField] private TMP_Text buyFuelUnitLabel;
        [SerializeField] private Button fillFuelButton;
        [SerializeField] private TMP_Text fillFuelLabel;

        [SerializeField] private Image hpFillBar;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Button buyHpUnitButton;
        [SerializeField] private TMP_Text buyHpUnitLabel;
        [SerializeField] private Button fillHpButton;
        [SerializeField] private TMP_Text fillHpLabel;

        private AutomationConfig config => GameManager.AutomationConfig;

        private void Start()
        {
            if (playerController == null) Debug.LogError("ResourceRefillUI: playerController not assigned in the Inspector.");
            if (playerHealth == null) Debug.LogError("ResourceRefillUI: playerHealth not assigned in the Inspector.");

            if (buyFuelUnitButton != null) buyFuelUnitButton.onClick.AddListener(BuyFuelUnit);
            if (fillFuelButton != null) fillFuelButton.onClick.AddListener(FillFuel);
            if (buyHpUnitButton != null) buyHpUnitButton.onClick.AddListener(BuyHpUnit);
            if (fillHpButton != null) fillHpButton.onClick.AddListener(FillHp);
        }

        private void Update()
        {
            if (playerController != null)
            {
                float fuelMissing = Mathf.Max(0f, playerController.FuelMissing);

                if (fuelFillBar != null) fuelFillBar.fillAmount = Mathf.Clamp01(playerController.FuelFraction);
                if (fuelLabel != null) fuelLabel.text = $"{playerController.Fuel:0}/{playerController.FuelMax:0}";
                if (buyFuelUnitButton != null) buyFuelUnitButton.interactable = fuelMissing > 0f;
                if (fillFuelButton != null) fillFuelButton.interactable = fuelMissing > 0f;
                if (buyFuelUnitLabel != null) buyFuelUnitLabel.text = $"Buy 1 (${config.FuelCostPerUnit:0.##})";
                if (fillFuelLabel != null) fillFuelLabel.text = $"Fill (${fuelMissing * config.FuelCostPerUnit:0.##})";
            }

            if (playerHealth != null)
            {
                float hpMissing = Mathf.Max(0f, playerHealth.MaxHp - playerHealth.CurrentHp);

                if (hpFillBar != null) hpFillBar.fillAmount = playerHealth.MaxHp > 0f ? Mathf.Clamp01(playerHealth.CurrentHp / playerHealth.MaxHp) : 0f;
                if (hpLabel != null) hpLabel.text = $"{playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}";
                if (buyHpUnitButton != null) buyHpUnitButton.interactable = hpMissing > 0f;
                if (fillHpButton != null) fillHpButton.interactable = hpMissing > 0f;
                if (buyHpUnitLabel != null) buyHpUnitLabel.text = $"Buy 1 (${config.HpCostPerUnit:0.##})";
                if (fillHpLabel != null) fillHpLabel.text = $"Fill (${hpMissing * config.HpCostPerUnit:0.##})";
            }
        }

        private void BuyFuelUnit()
        {
            if (playerController == null || playerController.FuelMissing <= 0f) return;
            if (!Wallet.Instance.TrySpend(config.FuelCostPerUnit)) return;
            playerController.AddFuel(1f);
        }

        private void FillFuel()
        {
            if (playerController == null) return;
            float units = playerController.FuelMissing;
            if (units <= 0f) return;
            if (!Wallet.Instance.TrySpend(units * config.FuelCostPerUnit)) return;
            playerController.AddFuel(units);
        }

        private void BuyHpUnit()
        {
            if (playerHealth == null || playerHealth.MaxHp - playerHealth.CurrentHp <= 0f) return;
            if (!Wallet.Instance.TrySpend(config.HpCostPerUnit)) return;
            playerHealth.AddHp(1f);
        }

        private void FillHp()
        {
            if (playerHealth == null) return;
            float units = playerHealth.MaxHp - playerHealth.CurrentHp;
            if (units <= 0f) return;
            if (!Wallet.Instance.TrySpend(units * config.HpCostPerUnit)) return;
            playerHealth.AddHp(units);
        }
    }
}
