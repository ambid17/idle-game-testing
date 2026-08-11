using System;
using Events;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // One processing queue row: idle shows a "Select Recipe" button, active shows the running
    // recipe + a live progress bar + a Cancel button. Reads ProcessingManager.Slots directly in
    // Update() for the progress fill (a continuous value), matching RefuelingUI's fill-bar
    // approach - discrete state changes (job started/completed/cancelled) are still handled by
    // ProcessingUI via events, which calls Refresh() to swap between the two view states.
    public class ProcessingQueueSlotUI : MonoBehaviour
    {
        [SerializeField] private Image recipeIcon;
        [SerializeField] private Button selectRecipeButton;
        [SerializeField] private TMP_Text recipeNameLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Slider recipeSizeSlider;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;

        private int slotIndex;
        private ProcessingRecipeDefinition selectedRecipe;

        public void Bind(int slotIndex, Action<int> onSelectRecipeClicked)
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
                actionButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new ProcessingCancelRequestedEvent(this.slotIndex)));
            }

            Refresh();
        }

        public void Refresh()
        {
            var job = ProcessingManager.Instance.Slots.Count > slotIndex ? ProcessingManager.Instance.Slots[slotIndex] : null;
            bool active = job != null;

            progressFill.gameObject.SetActive(active);
            progressLabel.gameObject.SetActive(active);
            recipeSizeSlider.gameObject.SetActive(!active);
            actionButtonLabel.text = active ? "Cancel" : "Start";

            if (!active) return;

            recipeIcon.sprite = job.Recipe.Icon;
            recipeNameLabel.text = $"{job.Recipe.DisplayName} x{job.Quantity}";
            UpdateProgress(job);
        }

        private void Update()
        {
            var job = ProcessingManager.Instance.Slots.Count > slotIndex ? ProcessingManager.Instance.Slots[slotIndex] : null;
            if (job == null) return;
            UpdateProgress(job);
        }

        private void UpdateProgress(ProcessingJob job)
        {
            float fraction = job.TotalDuration > 0f ? 1f - Mathf.Clamp01(job.TimeRemaining / job.TotalDuration) : 1f;
            progressFill.fillAmount = fraction;
            progressLabel.text = $"{Mathf.Max(0f, job.TimeRemaining):0.#}s";
        }
    }
}
