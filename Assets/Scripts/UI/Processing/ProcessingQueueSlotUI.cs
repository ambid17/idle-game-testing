using Economy;
using Events;
using Processing;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // One processing queue slot per Assets/Docs/processingImplementation.md: idle shows the
    // selected recipe (or an empty image if none picked yet), its ingredients (red if the Depot
    // is short), and a 1-to-max-craftable slider; active shows the running recipe + a live
    // progress bar + a Cancel button. Reads ProcessingManager.Slots directly in Update() for the
    // progress fill and the idle slider's max (both continuous, Depot-driven values), matching
    // a continuous fill-bar approach - discrete state changes (job started/completed/cancelled)
    // are still handled by ProcessingUI via events, which calls Refresh() to swap view states.
    public class ProcessingQueueSlotUI : MonoBehaviour
    {
        [SerializeField] private Image recipeIcon;
        [SerializeField] private Button selectRecipeButton;
        [SerializeField] private TMP_Text recipeNameLabel;

        [SerializeField] private GameObject progressFillParent;
        [SerializeField] private Image progressFillMask;
        [SerializeField] private TMP_Text progressLabel;

        [SerializeField] private Slider recipeSizeSlider;
        [SerializeField] private TMP_Text recipeSizeLabel;

        [SerializeField] private TMP_Text ingredientsLabel;

        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;

        private int slotIndex;
        private ProcessingRecipeDefinition selectedRecipe;

        // What the recipe-picker modal highlights: the running job's recipe, else the one picked.
        public ProcessingRecipeDefinition SelectedRecipe => ActiveJob != null ? ActiveJob.Recipe : selectedRecipe;

        private void Start()
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnActionButtonClicked);

            recipeSizeSlider.wholeNumbers = true;
            recipeSizeSlider.minValue = 1;
            recipeSizeSlider.onValueChanged.RemoveAllListeners();
            recipeSizeSlider.onValueChanged.AddListener((f) => OnRecipeSizeChanged());
        }

        public void Bind(int slotIndex, System.Action<int> onSelectRecipeClicked)
        {
            this.slotIndex = slotIndex;

            selectRecipeButton.onClick.RemoveAllListeners();
            selectRecipeButton.onClick.AddListener(() => onSelectRecipeClicked?.Invoke(this.slotIndex));

            Refresh();
        }

        // Called by ProcessingUI when a recipe is picked from the selection modal.
        public void SetRecipe(ProcessingRecipeDefinition recipe)
        {
            selectedRecipe = recipe;
            Refresh();
        }

        public void Refresh()
        {
            bool active = ActiveJob != null;
            bool hasRecipe = selectedRecipe != null;
            progressFillParent.SetActive(active);

            selectRecipeButton.interactable = !active;

            recipeSizeSlider.gameObject.SetActive(hasRecipe && !active);
            recipeSizeLabel.gameObject.SetActive(hasRecipe && !active);
            recipeSizeLabel.text = $"{recipeSizeSlider.value:0}/{recipeSizeSlider.maxValue:0}";
            actionButtonLabel.text = active ? "Cancel" : "Start";

            var recipe = active ? ActiveJob.Recipe : selectedRecipe;
            recipeIcon.sprite = recipe != null ? recipe.Icon : null;
            recipeNameLabel.text = recipe != null ? recipe.DisplayName : "No Recipe Selected";

            ingredientsLabel.gameObject.SetActive(hasRecipe && !active);

            if (active)
            {
                actionButton.interactable = true;
                return;
            }

            recipeSizeSlider.value = recipeSizeSlider.minValue;

            if (recipe == null)
            {
                ingredientsLabel.text = string.Empty;
                actionButton.interactable = false;
                return;
            }

            RefreshCraftableQuantity();
        }

        private void Update()
        {
            if (ActiveJob != null)
            {
                UpdateProgress(ActiveJob);
                return;
            }
        }

        private ProcessingJob ActiveJob => ProcessingManager.Instance.Slots[slotIndex];

        private void RefreshCraftableQuantity()
        {
            int maxCraftable = MaxCraftableQuantity(selectedRecipe);
            recipeSizeSlider.maxValue = Mathf.Max(1, maxCraftable);
            if (recipeSizeSlider.value > recipeSizeSlider.maxValue) recipeSizeSlider.value = recipeSizeSlider.maxValue;

            if (maxCraftable == 0)
            {
                ingredientsLabel.text = $"<color=red>Insufficient materials available</color>";
            }
            else
            {
                ingredientsLabel.text = FormatIngredients(selectedRecipe);
            }
            actionButton.interactable = maxCraftable >= 1;

            recipeSizeLabel.text = $"{recipeSizeSlider.value:0}/{recipeSizeSlider.maxValue:0}";
        }

        private void OnActionButtonClicked()
        {
            var job = ActiveJob;
            if (job != null)
            {
                GameManager.EventService.Dispatch(new ProcessingCancelRequestedEvent(slotIndex));
                return;
            }

            if (selectedRecipe == null) return;
            int quantity = Mathf.RoundToInt(recipeSizeSlider.value);
            if (quantity <= 0) return;

            GameManager.EventService.Dispatch(new ProcessingStartRequestedEvent(slotIndex, selectedRecipe, quantity));
        }

        private void UpdateProgress(ProcessingJob job)
        {
            // The bar fills once per unit, and the label counts down the units still to be made.
            float fraction = job.UnitDuration > 0f ? 1f - Mathf.Clamp01(job.UnitTimeRemaining / job.UnitDuration) : 1f;
            progressFillMask.fillAmount = fraction;
            progressLabel.text = $"{job.Remaining} left\n{Mathf.Max(0f, job.UnitTimeRemaining):0.#}s";
        }

        // The scarcest ingredient decides - one missing ore makes the whole recipe uncraftable.
        private static int MaxCraftableQuantity(ProcessingRecipeDefinition recipe)
        {
            int max = int.MaxValue;
            foreach (var ingredient in recipe.Ingredients)
            {
                Depot.Instance.StoredOres.TryGetValue(ingredient.Material, out var stored);
                max = Mathf.Min(max, stored / ingredient.Count);
            }
            return max == int.MaxValue ? 0 : max;
        }

        // Beyond this many ingredients the per-ore list no longer fits the slot.
        private const int MaxListedIngredients = 4;

        private string FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            int quantity = (int)recipeSizeSlider.value;
            if (recipe.Ingredients.Count > MaxListedIngredients && recipe.Ingredients.TrueForAll(i => i.Count == recipe.Ingredients[0].Count))
            {
                return $"Cost:\n- {recipe.Ingredients[0].Count * quantity} each of {recipe.Ingredients.Count} ores";
            }

            var parts = new List<string>();
            parts.Add("Cost:");
            foreach (var ingredient in recipe.Ingredients)
            {
                var ingredientCount = ingredient.Count * quantity;
                string line = $"- {ingredientCount} {ingredient.Material}";
                parts.Add(line);
            }
            return string.Join("\n", parts);
        }

        private void OnRecipeSizeChanged()
        {
            recipeSizeLabel.text = $"{recipeSizeSlider.value:0}/{recipeSizeSlider.maxValue:0}";
            ingredientsLabel.text = FormatIngredients(selectedRecipe);
        }
    }
}
