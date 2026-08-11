using Economy;
using Events;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // Quantity + Start step of the "select recipe" flow: slider bounded by how many units the
    // Depot's current ore can afford, same Update()-driven maxValue pattern as
    // RefuelingUI.purchaseAmountSlider. Never starts a job itself - dispatches
    // ProcessingStartRequestedEvent for ProcessingUI to act on, matching
    // SkillTreeDetailModalUI's buy-button-dispatches-a-request-event shape.
    public class ProcessingRecipeDetailModalUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button closeButton;

        private int slotIndex;
        private ProcessingRecipeDefinition recipe;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (quantitySlider != null) quantitySlider.onValueChanged.AddListener(_ => RefreshLabel());
            if (root != null) root.SetActive(false);
        }

        public void Show(int slotIndex, ProcessingRecipeDefinition recipe)
        {
            this.slotIndex = slotIndex;
            this.recipe = recipe;
            if (root != null) root.SetActive(true);
            if (nameLabel != null) nameLabel.text = recipe.DisplayName;
            if (quantitySlider != null) quantitySlider.value = 0;
            RefreshMax();
            RefreshLabel();
        }

        public void Close()
        {
            recipe = null;
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (root == null || !root.activeSelf || recipe == null) return;
            RefreshMax();
        }

        private void RefreshMax()
        {
            if (quantitySlider == null || recipe == null) return;
            quantitySlider.maxValue = MaxAffordableQuantity(recipe);
            if (quantitySlider.value > quantitySlider.maxValue) quantitySlider.value = quantitySlider.maxValue;
        }

        private static int MaxAffordableQuantity(ProcessingRecipeDefinition recipe)
        {
            int max = -1;
            foreach (var ingredient in recipe.Ingredients)
            {
                Depot.Instance.StoredOres.TryGetValue(ingredient.Material, out var stored);
                int affordable = ingredient.Count > 0 ? stored / ingredient.Count : 0;
                if (max < 0 || affordable < max) max = affordable;
            }
            return Mathf.Max(0, max);
        }

        private void RefreshLabel()
        {
            if (quantityLabel == null || quantitySlider == null || recipe == null) return;
            int quantity = Mathf.RoundToInt(quantitySlider.value);
            float duration = recipe.DurationPerUnit * quantity / Mathf.Max(0.01f, UpgradeManager.Instance.ProcessingSpeedMultiplier);
            quantityLabel.text = $"{quantity} ({duration:0.#}s)";
            if (startButton != null) startButton.interactable = quantity > 0;
        }

        private void OnStartClicked()
        {
            if (recipe == null || quantitySlider == null) return;
            int quantity = Mathf.RoundToInt(quantitySlider.value);
            if (quantity <= 0) return;

            GameManager.EventService.Dispatch(new ProcessingStartRequestedEvent(slotIndex, recipe, quantity));
            Close();
        }
    }
}
