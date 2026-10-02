using Economy;
using Events;
using Player;
using TMPro;
using UI.Reuseable;
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
        [Tooltip("The active run modifier (RunModifiers.RunModifierService.ActiveSummary) - blank when there is none.")]
        [SerializeField] private TMP_Text runModifierLabel;
        [Tooltip("Hover tooltip on runModifierLabel showing the active modifier's full description.")]
        [SerializeField] private HoverTooltipTrigger runModifierTooltipTrigger;
        [SerializeField] private TMP_Text runModifierTooltipLabel;

        [Header("Critical fuel warning")]
        [SerializeField] private Color criticalFuelColor = new Color(1f, 0.25f, 0.2f, 1f);
        [Tooltip("Wobble frequency (radians/sec) of the fuel number while fuel is critical.")]
        [SerializeField] private float fuelJiggleSpeed = 30f;
        [Tooltip("Max wobble angle (degrees) at empty; it ramps up from half this at the critical threshold.")]
        [SerializeField] private float fuelJiggleAngle = 8f;
        [Tooltip("Max extra scale of the pulse at empty.")]
        [SerializeField] private float fuelJigglePulse = 0.15f;

        private Color fuelLabelBaseColor;
        private Quaternion fuelLabelBaseRotation;
        private Vector3 fuelLabelBaseScale;

        private void Start()
        {
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerInventory == null) playerInventory = FindAnyObjectByType<PlayerInventory>();
            CheckNullRefs();

            fuelLabelBaseColor = fuelLabel.color;
            fuelLabelBaseRotation = fuelLabel.rectTransform.localRotation;
            fuelLabelBaseScale = fuelLabel.rectTransform.localScale;

            renderer.SetActive(true);
            RefreshDollars();
            RefreshArtifactCount();
            RefreshWeight();
            RefreshRunModifier();
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
            if (runModifierLabel == null) Debug.LogError("HUDUI: no runModifierLabel found in scene.");
            if (runModifierTooltipTrigger == null) Debug.LogError("HUDUI: no runModifierTooltipTrigger found in scene.");
            if (runModifierTooltipLabel == null) Debug.LogError("HUDUI: no runModifierTooltipLabel found in scene.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Add<InventoryChangedEvent>(RefreshWeight);
            GameManager.EventService.Add<UpgradePurchasedEvent>(HandleUpdatePurchased);
            GameManager.EventService.Add<RunModifierChangedEvent>(RefreshRunModifier);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DollarsChangedEvent>(RefreshDollars);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Remove<InventoryChangedEvent>(RefreshWeight);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(HandleUpdatePurchased);
            GameManager.EventService.Remove<RunModifierChangedEvent>(RefreshRunModifier);
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
            // Ceil so the number only reads 0 when the tank is truly empty - with the grace-period
            // slow drain the player can spend a while below 0.5, and "0" while still flying reads
            // like a bug.
            fuelLabel.text = $"{Mathf.CeilToInt(playerController.Fuel)}";
            RefreshFuelWarning();
        }

        // Persistent counterpart to the one-shot low/critical fuel notifications: the fuel number
        // stays red and wobbles for as long as fuel is critical, getting more frantic toward empty.
        private void RefreshFuelWarning()
        {
            var labelTransform = fuelLabel.rectTransform;
            if (!playerController.IsFuelCritical)
            {
                fuelLabel.color = fuelLabelBaseColor;
                labelTransform.localRotation = fuelLabelBaseRotation;
                labelTransform.localScale = fuelLabelBaseScale;
                return;
            }

            float urgency = Mathf.Lerp(0.5f, 1f, 1f - Mathf.Clamp01(playerController.FuelFraction / PlayerController.CriticalFuelWarningFraction));
            float t = Time.unscaledTime;
            float angle = Mathf.Sin(t * fuelJiggleSpeed) * fuelJiggleAngle * urgency;
            float pulse = 1f + Mathf.Abs(Mathf.Sin(t * fuelJiggleSpeed * 0.25f)) * fuelJigglePulse * urgency;

            fuelLabel.color = criticalFuelColor;
            labelTransform.localRotation = fuelLabelBaseRotation * Quaternion.Euler(0f, 0f, angle);
            labelTransform.localScale = fuelLabelBaseScale * pulse;
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
            dollarsLabel.text = $"{Wallet.Instance.Dollars:0}";
        }

        private void RefreshArtifactCount()
        {
            artifactCountLabel.text = $"{Wallet.Instance.ArtifactCount}";
        }

        private void RefreshRunModifier()
        {
            var service = GameManager.RunModifierService;
            runModifierLabel.text = service.ActiveSummary();

            var def = service.ActiveDefinition;
            runModifierTooltipTrigger.Active = def != null;
            runModifierTooltipLabel.text = def != null
                ? $"<b>{def.DisplayName}</b>\n{service.Describe(def, service.ActiveState)}"
                : "";
        }

        private void RefreshDepth()
        {
            float depth = playerController.transform.position.y;
            int layerIndex = GameManager.LayerConfigProvider.GetLayerIndexAtWorldY(depth, GameManager.MapGenerationService.CellSize);
            depthLabel.text = $"Depth: {depth:0}m\nLayer: {layerIndex + 1}";
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
