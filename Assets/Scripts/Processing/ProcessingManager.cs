using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;
using Persistence;
using UnityEngine;

namespace Processing
{
    public class ProcessingJob
    {
        public ProcessingRecipeDefinition Recipe;
        public int Quantity;
        public float TimeRemaining;
        public float TotalDuration;

        // Snapshot of what StartJob actually pulled from the Depot, so CancelJob refunds exactly
        // that instead of recomputing from the recipe (which could drift if recipes are rebalanced
        // mid-job).
        public IReadOnlyDictionary<BlockTypeId, int> ConsumedIngredients;
    }

    // Processing Center per Assets/Docs/processingImplementation.md. Singleton so it needs no
    // scene wiring, matching Depot/Wallet/UpgradeManager. Slots is index-addressed (null = empty)
    // rather than a queue, since the UI needs to address/cancel a specific concurrent slot.
    public class ProcessingManager : Singleton<ProcessingManager>
    {
        private readonly List<ProcessingJob> slots = new();

        // 1 free base slot per the doc's "processing queue: allows multiple recipes to be running
        // at once" - the upgrade adds more on top.
        public int SlotCount => 1 + UpgradeManager.Instance.ProcessingQueueSlotCount;
        public IReadOnlyList<ProcessingJob> Slots => slots;

        public bool IsRecipeUnlocked(ProcessingRecipeDefinition recipe) =>
            recipe.RequiredUpgrade != null && UpgradeManager.Instance.IsMaxed(recipe.RequiredUpgrade);

        private void EnsureSlotCapacity()
        {
            while (slots.Count < SlotCount) slots.Add(null);
        }

        public bool StartJob(int slotIndex, ProcessingRecipeDefinition recipe, int quantity)
        {
            EnsureSlotCapacity();

            if (recipe == null || quantity <= 0)
            {
                Debug.LogError("ProcessingManager.StartJob: recipe is null or quantity <= 0.");
                return false;
            }
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                Debug.LogError($"ProcessingManager.StartJob: slotIndex {slotIndex} out of range (SlotCount={SlotCount}).");
                return false;
            }
            if (slots[slotIndex] != null)
            {
                Debug.LogError($"ProcessingManager.StartJob: slot {slotIndex} already has an active job.");
                return false;
            }
            if (!IsRecipeUnlocked(recipe))
            {
                Debug.LogError($"ProcessingManager.StartJob: recipe {recipe.DisplayName} is not unlocked.");
                return false;
            }

            var required = ScaleIngredients(recipe, quantity);
            if (!Depot.Instance.TryConsume(required)) return false;

            float totalDuration = ComputeDuration(recipe, quantity);
            slots[slotIndex] = new ProcessingJob
            {
                Recipe = recipe,
                Quantity = quantity,
                TimeRemaining = totalDuration,
                TotalDuration = totalDuration,
                ConsumedIngredients = required
            };

            GameManager.EventService.Dispatch(new ProcessingJobStartedEvent(slotIndex, recipe, quantity));
            return true;
        }

        public void CancelJob(int slotIndex)
        {
            var job = slots[slotIndex];
            Depot.Instance.Deposit(job.ConsumedIngredients);
            slots[slotIndex] = null;
            GameManager.EventService.Dispatch(new ProcessingJobCancelledEvent(slotIndex));
        }

        private void Update()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var job = slots[i];
                if (job == null) continue;

                job.TimeRemaining -= Time.deltaTime;
                if (job.TimeRemaining <= 0f) CompleteJob(i, job);
            }
        }

        private void CompleteJob(int slotIndex, ProcessingJob job)
        {
            Depot.Instance.DepositGood(job.Recipe.Id, job.Quantity);
            slots[slotIndex] = null;
            GameManager.EventService.Dispatch(new ProcessingJobCompletedEvent(slotIndex, job.Recipe, job.Quantity));
        }

        private static Dictionary<BlockTypeId, int> ScaleIngredients(ProcessingRecipeDefinition recipe, int quantity)
        {
            var scaled = new Dictionary<BlockTypeId, int>();
            foreach (var ingredient in recipe.Ingredients)
            {
                scaled[ingredient.Material] = ingredient.Count * quantity;
            }
            return scaled;
        }

        private static float ComputeDuration(ProcessingRecipeDefinition recipe, int quantity) =>
            recipe.DurationPerUnit * quantity / Mathf.Max(0.01f, UpgradeManager.Instance.ProcessingSpeedMultiplier);

        // Restore for SaveService. The ore for these jobs was already deducted from the Depot last
        // session (and that deduction is what's reflected in the saved Depot totals), so this
        // rebuilds ConsumedIngredients for correct Cancel-refund behavior without consuming
        // anything again. elapsedSeconds (real time since last save) is subtracted from each job's
        // remaining time; anything that would have finished completes immediately.
        public void RestoreFromSaveData(IReadOnlyList<ProcessingJobSaveEntry> savedJobs, float elapsedSeconds)
        {
            slots.Clear();
            EnsureSlotCapacity();
            if (savedJobs == null) return;

            var database = GameManager.ProcessingRecipeDatabase;
            foreach (var entry in savedJobs)
            {
                if (entry.SlotIndex < 0 || entry.SlotIndex >= slots.Count) continue;

                var recipe = database != null ? database.Get(entry.RecipeId) : null;
                if (recipe == null)
                {
                    Debug.LogError($"ProcessingManager.RestoreFromSaveData: no recipe found for {entry.RecipeId}. Skipping saved job.");
                    continue;
                }

                float timeRemaining = entry.TimeRemainingSeconds - elapsedSeconds;
                if (timeRemaining <= 0f)
                {
                    Depot.Instance.DepositGood(recipe.Id, entry.Quantity);
                    GameManager.EventService.Dispatch(new ProcessingJobCompletedEvent(entry.SlotIndex, recipe, entry.Quantity));
                    continue;
                }

                slots[entry.SlotIndex] = new ProcessingJob
                {
                    Recipe = recipe,
                    Quantity = entry.Quantity,
                    TimeRemaining = timeRemaining,
                    TotalDuration = ComputeDuration(recipe, entry.Quantity),
                    ConsumedIngredients = ScaleIngredients(recipe, entry.Quantity)
                };
            }
        }
    }
}
