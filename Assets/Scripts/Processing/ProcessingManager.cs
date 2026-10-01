using System.Collections.Generic;
using System.Linq;
using Economy;
using Events;
using MapGeneration;
using Persistence;
using UnityEngine;

namespace Processing
{
    // A batch is crafted one unit at a time: each finished unit is banked in the Depot straight
    // away and comes off Remaining, so cancelling only gives up the units not yet made.
    public class ProcessingJob
    {
        public ProcessingRecipeDefinition Recipe;
        // Units still to be made, including the one in progress.
        public int Remaining;
        // Seconds left on the unit in progress, out of UnitDuration.
        public float UnitTimeRemaining;
        public float UnitDuration;

        // Snapshot of what StartJob pulled from the Depot per unit, so CancelJob refunds exactly
        // that for the unmade units instead of recomputing from the recipe (which could drift if
        // recipes are rebalanced mid-job).
        public IReadOnlyDictionary<BlockTypeId, int> IngredientsPerUnit;
    }

    // Processing Center per Assets/Docs/processingImplementation.md. Singleton so it needs no
    // scene wiring, matching Depot/Wallet/UpgradeManager. Slots is index-addressed (null = empty)
    // rather than a queue, since the UI needs to address/cancel a specific concurrent slot.
    public class ProcessingManager : Singleton<ProcessingManager>
    {
        private readonly List<ProcessingJob> slots = new();

        // 1 free base slot per the doc's "processing queue: allows multiple recipes to be running
        // at once" - the upgrade adds more on top.
        public int SlotCount => 1 + UpgradeManager.Instance.Processing_QueueSlotCount;
        public IReadOnlyList<ProcessingJob> Slots => slots;

        // Guarantees `slots` covers at least the 1 free base slot immediately, not just after the
        // first StartJob/RestoreFromSaveData call. Without this, a brand-new game (no save file
        // yet, so SaveService.ApplyLoadedData never calls RestoreFromSaveData) leaves `slots` at
        // Count == 0 while SlotCount == 1, and the first Processing panel open throws
        // ArgumentOutOfRangeException indexing Slots[0] before the player ever gets to start a job.
        protected override void Initialize()
        {
            base.Initialize();
            EnsureSlotCapacity();
        }

        // Jobs that finished (auto-deposited) since the player last opened the Processing panel -
        // drives the completion badge above the building. ProcessingUI.Open() clears this.
        private int uncollectedCompletions;
        public int UncollectedCompletions => uncollectedCompletions;

        public void ClearUncollectedCompletions()
        {
            if (uncollectedCompletions == 0) return;
            uncollectedCompletions = 0;
            GameManager.EventService.Dispatch(new ProcessingCompletionCountChangedEvent(uncollectedCompletions));
        }

        private void MarkCompletionUncollected()
        {
            uncollectedCompletions++;
            GameManager.EventService.Dispatch(new ProcessingCompletionCountChangedEvent(uncollectedCompletions));
        }

        public bool IsRecipeUnlocked(ProcessingRecipeDefinition recipe) =>
            recipe.RequiredUpgrade != null && UpgradeManager.Instance.IsMaxed(recipe.RequiredUpgrade);

        public bool HasAnyRecipeUnlocked() => GameManager.ProcessingRecipeDatabase.Recipes.Any(IsRecipeUnlocked);

        private void EnsureSlotCapacity()
        {
            while (slots.Count < SlotCount) slots.Add(null);
        }

        // Without this, buying the Processing Queue Slots upgrade raises SlotCount immediately but
        // leaves the backing `slots` list at its old (smaller) size - EnsureSlotCapacity was only
        // ever called from StartJob/RestoreFromSaveData. ProcessingUI.BuildSlots() then spawns one
        // ProcessingQueueSlotUI per SlotCount and indexes Slots[slotIndex] for each, throwing
        // ArgumentOutOfRangeException on the freshly-purchased slot until some job elsewhere
        // happened to grow the list first.
        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect == UpgradeEffect.Processing_QueueSlots) EnsureSlotCapacity();
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

            float unitDuration = UnitDuration(recipe);
            slots[slotIndex] = new ProcessingJob
            {
                Recipe = recipe,
                Remaining = quantity,
                UnitTimeRemaining = unitDuration,
                UnitDuration = unitDuration,
                IngredientsPerUnit = ScaleIngredients(recipe, 1)
            };

            GameManager.EventService.Dispatch(new ProcessingJobStartedEvent(slotIndex, recipe, quantity));
            return true;
        }

        // Refunds the ore for every unit not finished yet (the one in progress included); units
        // already made stay banked in the Depot.
        public void CancelJob(int slotIndex)
        {
            var job = slots[slotIndex];
            var refund = new Dictionary<BlockTypeId, int>();
            foreach (var kvp in job.IngredientsPerUnit) refund[kvp.Key] = kvp.Value * job.Remaining;
            Depot.Instance.Deposit(refund);
            slots[slotIndex] = null;
            GameManager.EventService.Dispatch(new ProcessingJobCancelledEvent(slotIndex));
        }

        private void Update()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var job = slots[i];
                if (job == null) continue;

                job.UnitTimeRemaining -= Time.deltaTime;
                if (job.UnitTimeRemaining <= 0f) CompleteUnit(i, job);
            }
        }

        // One unit per frame at most - the leftover time carries into the next unit, so a very
        // short recipe still averages out to the right rate.
        private void CompleteUnit(int slotIndex, ProcessingJob job)
        {
            Depot.Instance.DepositGood(job.Recipe.Id, 1);
            job.Remaining--;
            GameManager.EventService.Dispatch(new ProcessingUnitCompletedEvent(slotIndex, job.Recipe));

            if (job.Remaining > 0)
            {
                job.UnitTimeRemaining += job.UnitDuration;
                return;
            }

            slots[slotIndex] = null;
            GameManager.EventService.Dispatch(new ProcessingJobCompletedEvent(slotIndex, job.Recipe));
            MarkCompletionUncollected();
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

        // Seconds to craft one unit at the current Processing speed upgrade level.
        public float UnitDuration(ProcessingRecipeDefinition recipe) =>
            recipe.DurationPerUnit / Mathf.Max(0.01f, UpgradeManager.Instance.Processing_SpeedMultiplier);

        // Restore for SaveService. The ore for these jobs was already deducted from the Depot last
        // session (and that deduction is what's reflected in the saved Depot totals), so this
        // rebuilds IngredientsPerUnit for correct Cancel-refund behavior without consuming
        // anything again. elapsedSeconds (real time since last save) is worked off unit by unit;
        // every unit that would have finished while away is banked immediately.
        public void RestoreFromSaveData(IReadOnlyList<ProcessingJobSaveEntry> savedJobs, float elapsedSeconds, int savedUncollectedCompletions)
        {
            slots.Clear();
            EnsureSlotCapacity();
            uncollectedCompletions = savedUncollectedCompletions;
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

                // Saves from before units were crafted one at a time stored the whole batch's
                // remaining time here, hence the clamp.
                float unitDuration = UnitDuration(recipe);
                float unitTimeRemaining = Mathf.Min(entry.TimeRemainingSeconds, unitDuration);

                int finished = 0;
                if (elapsedSeconds >= unitTimeRemaining)
                {
                    float overflow = elapsedSeconds - unitTimeRemaining;
                    finished = Mathf.Min(entry.Quantity, 1 + Mathf.FloorToInt(overflow / unitDuration));
                    unitTimeRemaining = unitDuration - overflow % unitDuration;
                }
                else unitTimeRemaining -= elapsedSeconds;

                if (finished > 0)
                {
                    Depot.Instance.DepositGood(recipe.Id, finished);
                    GameManager.EventService.Dispatch(new ProcessingUnitCompletedEvent(entry.SlotIndex, recipe));
                }

                if (finished >= entry.Quantity)
                {
                    GameManager.EventService.Dispatch(new ProcessingJobCompletedEvent(entry.SlotIndex, recipe));
                    MarkCompletionUncollected();
                    continue;
                }

                slots[entry.SlotIndex] = new ProcessingJob
                {
                    Recipe = recipe,
                    Remaining = entry.Quantity - finished,
                    UnitTimeRemaining = unitTimeRemaining,
                    UnitDuration = unitDuration,
                    IngredientsPerUnit = ScaleIngredients(recipe, 1)
                };
            }
        }
    }
}
