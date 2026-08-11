using Economy;
using Events;
using Processing;
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
    // RefuelingUI's fill-bar approach - discrete state changes (job started/completed/cancelled)
    // are still handled by ProcessingUI via events, which calls Refresh() to swap view states.
    public class ProcessingQueueSlotUI : MonoBehaviour
    {
        [SerializeField] private Image recipeIcon;
        [SerializeField] private Button selectRecipeButton;
        [SerializeField] private TMP_Text recipeNameLabel;
        [SerializeField] private TMP_Text ingredientsLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Slider recipeSizeSlider;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;

        private int slotIndex;
        private ProcessingRecipeDefinition selectedRecipe;

        public void Bind(int slotIndex, System.Action<int> onSelectRecipeClicked)
        {
            this.slotIndex = slotIndex;

            if (selectRecipeButton != null)
            {
                selectRecipeButton.onClick.RemoveAllListeners();
                selectRecipeButton.onClick.AddListener(() => onSelectRecipeClicked?.Invoke(this.slotIndex));
            }
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }
            if (recipeSizeSlider != null)
            {
                recipeSizeSlider.wholeNumbers = true;
                recipeSizeSlider.minValue = 1;
            }

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
            var job = ActiveJob();
            bool active = job != null;

            progressFill.gameObject.SetActive(active);
            progressLabel.gameObject.SetActive(active);
            recipeSizeSlider.gameObject.SetActive(!active);
            actionButtonLabel.text = active ? "Cancel" : "Start";

            var recipe = active ? job.Recipe : selectedRecipe;
            recipeIcon.sprite = recipe != null ? recipe.Icon : null;
            recipeNameLabel.text = recipe != null ? recipe.DisplayName : "No Recipe Selected";

            if (active)
            {
                ingredientsLabel.text = FormatIngredients(job.Recipe);
                actionButton.interactable = true;
                UpdateProgress(job);
                return;
            }

            recipeSizeSlider.value = recipeSizeSlider.minValue;

            if (recipe == null)
            {
                ingredientsLabel.text = string.Empty;
                actionButton.interactable = false;
                return;
            }

            RefreshIdleDynamic();
        }

        private void Update()
        {
            var job = ActiveJob();
            if (job != null)
            {
                UpdateProgress(job);
                return;
            }

            if (selectedRecipe != null) RefreshIdleDynamic();
        }

        private ProcessingJob ActiveJob() =>
            ProcessingManager.Instance.Slots.Count > slotIndex ? ProcessingManager.Instance.Slots[slotIndex] : null;

        // Max craftable and ingredient shortages both depend on the Depot's live stock, so this
        // is re-run every frame while idle with a recipe selected, matching how the progress bar
        // is re-run every frame while a job is active.
        private void RefreshIdleDynamic()
        {
            int maxCraftable = MaxCraftableQuantity(selectedRecipe);
            recipeSizeSlider.maxValue = Mathf.Max(1, maxCraftable);
            if (recipeSizeSlider.value > recipeSizeSlider.maxValue) recipeSizeSlider.value = recipeSizeSlider.maxValue;

            ingredientsLabel.text = FormatIngredients(selectedRecipe);
            actionButton.interactable = maxCraftable >= 1;
        }

        private void OnActionButtonClicked()
        {
            var job = ActiveJob();
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
            float fraction = job.TotalDuration > 0f ? 1f - Mathf.Clamp01(job.TimeRemaining / job.TotalDuration) : 1f;
            progressFill.fillAmount = fraction;
            progressLabel.text = $"{Mathf.Max(0f, job.TimeRemaining):0.#}s";
        }

        private static int MaxCraftableQuantity(ProcessingRecipeDefinition recipe)
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

        private static string FormatIngredients(ProcessingRecipeDefinition recipe)
        {
            var parts = new string[recipe.Ingredients.Count];
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                var ingredient = recipe.Ingredients[i];
                Depot.Instance.StoredOres.TryGetValue(ingredient.Material, out var stored);
                string line = $"{ingredient.Count} {ingredient.Material}";
                parts[i] = stored < ingredient.Count ? $"<color=#FF5C5C>{line}</color>" : line;
            }
            return string.Join("\n", parts);
        }
    }
}
