using System.Collections.Generic;
using Events;
using MapGeneration;
using Player;
using UnityEngine;

namespace UI
{
    // Always-visible bottom-left HUD readout of carried ore per GameDesignDoc "Inventory" - one
    // small icon+count row per Ore-category BlockType, built once from BlockTypeDatabase and then
    // just toggled/refreshed as counts change. Unlike InventoryUI (the full Tab panel), this never
    // hides based on input - only individual rows hide when their count is 0, so the list only
    // ever shows ore the player actually has.
    //
    // Doubles as the ore-pickup notification (Palworld-style tally): a count increase isn't shown
    // right away - it accumulates as "+N" beside the row while pickups keep coming, and once that
    // ore goes quiet for GainHoldSeconds the count ticks up (AnimatedCounter) while the "+N" fades.
    // Decreases (deposit, death, a StorageDrone draining the player) come off the shown count
    // first so a running tally survives a mid-mining drain; emptying the row drops the tally.
    public class HudInventoryUI : MonoBehaviour
    {
        private const float GainHoldSeconds = 2.5f;
        private const float GainFadeSeconds = 0.6f;

        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;

        private readonly Dictionary<BlockTypeId, RowState> rowsByType = new();

        private class RowState
        {
            public OreRowUI Row;
            // What the count label shows (or is ticking toward) - excludes Pending.
            public int Shown;
            // Gained but not yet folded into Shown.
            public int Pending;
            // Amount on the "+N" label - stays after Pending is committed, until the fade ends.
            public int GainLabelAmount;
            public float IdleSeconds;
            public bool Fading;

            public int LastKnownCount => Shown + Pending;
        }
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            if (playerInventory == null) Debug.LogError("HudInventoryUI.playerInventory is not assigned.");
            if (rowContainer == null) Debug.LogError("HudInventoryUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("HudInventoryUI.rowPrefab is not assigned.");

            BuildRows();
            SnapToInventory();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<InventoryChangedEvent>(Refresh);
            GameManager.EventService.Add<LoadCompletedEvent>(SnapToInventory);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<InventoryChangedEvent>(Refresh);
            GameManager.EventService.Remove<LoadCompletedEvent>(SnapToInventory);
        }

        private void BuildRows()
        {
            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType == null || blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(blockType);
                row.gameObject.SetActive(false);
                row.gameObject.name = $"Hud_OreRow_{blockType.DisplayName}";
                rowsByType[blockType.Id] = new RowState { Row = row };
            }
        }

        private void Refresh()
        {
            foreach (var kvp in rowsByType)
            {
                playerInventory.OreCounts.TryGetValue(kvp.Key, out var count);
                var state = kvp.Value;

                if (count > state.LastKnownCount) AddGain(state, count - state.LastKnownCount);
                else if (count < state.LastKnownCount) ApplyLoss(state, state.LastKnownCount - count);

                state.Row.gameObject.SetActive(count > 0);
            }
        }

        // Initial build and save loads set the counts outright - restoring a save isn't a pickup.
        private void SnapToInventory()
        {
            foreach (var kvp in rowsByType)
            {
                playerInventory.OreCounts.TryGetValue(kvp.Key, out var count);
                Snap(kvp.Value, count, instant: true);
                kvp.Value.Row.gameObject.SetActive(count > 0);
            }
        }

        private static void AddGain(RowState state, int amount)
        {
            // A pickup during the fade starts a fresh tally - the previous one is already counted.
            if (state.Fading) state.GainLabelAmount = 0;

            state.Pending += amount;
            state.GainLabelAmount += amount;
            state.IdleSeconds = 0f;
            state.Fading = false;
            state.Row.SetGain(state.GainLabelAmount, 1f);
        }

        private static void ApplyLoss(RowState state, int amount)
        {
            if (amount >= state.LastKnownCount)
            {
                Snap(state, 0, instant: false);
                return;
            }

            int fromShown = Mathf.Min(amount, state.Shown);
            state.Shown -= fromShown;
            state.Pending -= amount - fromShown;
            state.Row.SetCount(state.Shown);
        }

        private static void Snap(RowState state, int count, bool instant)
        {
            state.Shown = count;
            state.Pending = 0;
            state.GainLabelAmount = 0;
            state.Fading = false;
            state.Row.SetCount(count, instant);
            state.Row.SetGain(0, 0f);
        }

        // Unscaled like AnimatedCounter, so a tally still resolves while the game is paused.
        private void Update()
        {
            foreach (var state in rowsByType.Values)
            {
                if (state.GainLabelAmount == 0) continue;

                state.IdleSeconds += Time.unscaledDeltaTime;
                if (state.IdleSeconds < GainHoldSeconds) continue;

                if (!state.Fading)
                {
                    state.Fading = true;
                    state.Shown += state.Pending;
                    state.Pending = 0;
                    state.Row.SetCount(state.Shown);
                }

                float alpha = 1f - Mathf.Clamp01((state.IdleSeconds - GainHoldSeconds) / GainFadeSeconds);
                if (alpha <= 0f) state.GainLabelAmount = 0;
                state.Row.SetGain(state.GainLabelAmount, alpha);
            }
        }
    }
}
