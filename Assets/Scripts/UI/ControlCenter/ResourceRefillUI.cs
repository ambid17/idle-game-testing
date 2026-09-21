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
            float fuelMissing = Mathf.Max(0f, playerController.FuelMissing);

            fuelFillBar.fillAmount = Mathf.Clamp01(playerController.FuelFraction);
            fuelLabel.text = $"{playerController.Fuel:0}/{playerController.FuelMax:0}";
            buyFuelUnitButton.interactable = fuelMissing > 0f;
            fillFuelButton.interactable = fuelMissing > 0f;
            buyFuelUnitLabel.text = $"Buy 1 (${config.FuelCostPerUnit:0})";
            fillFuelLabel.text = $"Fill (${fuelMissing * config.FuelCostPerUnit:0})";

            float hpMissing = Mathf.Max(0f, playerHealth.MaxHp - playerHealth.CurrentHp);

            hpFillBar.fillAmount = playerHealth.MaxHp > 0f ? Mathf.Clamp01(playerHealth.CurrentHp / playerHealth.MaxHp) : 0f;
            hpLabel.text = $"{playerHealth.CurrentHp:0}/{playerHealth.MaxHp:0}";
            buyHpUnitButton.interactable = hpMissing > 0f;
            fillHpButton.interactable = hpMissing > 0f;
            buyHpUnitLabel.text = $"Buy 1 (${config.HpCostPerUnit:0})";
            fillHpLabel.text = $"Fill (${hpMissing * config.HpCostPerUnit:0})";
        }

        private void BuyFuelUnit()
        {
            if (playerController.FuelMissing <= 0f) return;
            if (!Wallet.Instance.TrySpend(config.FuelCostPerUnit)) return;
            playerController.AddFuel(1f);
        }

        private void FillFuel()
        {
            float units = playerController.FuelMissing;
            if (units <= 0f) return;
            if (!Wallet.Instance.TrySpend(units * config.FuelCostPerUnit)) return;
            playerController.AddFuel(units);
        }

        

        private void BuyHpUnit()
        {
            if (playerHealth.MaxHp - playerHealth.CurrentHp <= 0f) return;
            if (!Wallet.Instance.TrySpend(config.HpCostPerUnit)) return;
            playerHealth.AddHp(1f);
        }

        private void FillHp()
        {
            float units = playerHealth.MaxHp - playerHealth.CurrentHp;
            if (units <= 0f) return;
            if (!Wallet.Instance.TrySpend(units * config.HpCostPerUnit)) return;
            playerHealth.AddHp(units);
        }


        public void TryFillFuel()
        {
            if (playerController.FuelMissing <= 0f) return;

            var maxPurchaseableUnits = Mathf.FloorToInt((float)(Wallet.Instance.Dollars / config.FuelCostPerUnit));
            var unitsToFill = Mathf.Min(maxPurchaseableUnits, Mathf.FloorToInt(playerController.FuelMissing));
            if (!Wallet.Instance.TrySpend(unitsToFill * config.FuelCostPerUnit)) return;
            playerController.AddFuel(unitsToFill);
        }

        public void TryFillHp()
        {
            if(playerHealth.CurrentHp >=playerHealth.MaxHp) return;
            var maxPurchaseableUnits = Mathf.FloorToInt((float)(Wallet.Instance.Dollars / config.HpCostPerUnit));
            var unitsToFill = Mathf.Min(maxPurchaseableUnits, Mathf.FloorToInt(playerHealth.MaxHp - playerHealth.CurrentHp));
            if (!Wallet.Instance.TrySpend(unitsToFill * config.HpCostPerUnit)) return;
            playerHealth.AddHp(unitsToFill);
        }
    }
}
