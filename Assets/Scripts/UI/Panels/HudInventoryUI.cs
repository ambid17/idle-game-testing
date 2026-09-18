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
    public class HudInventoryUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;

        private readonly Dictionary<BlockTypeId, OreRowUI> rowsByType = new();
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            if (playerInventory == null) Debug.LogError("HudInventoryUI.playerInventory is not assigned.");
            if (rowContainer == null) Debug.LogError("HudInventoryUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("HudInventoryUI.rowPrefab is not assigned.");

            BuildRows();
            Refresh();
        }

        private void OnEnable() => GameManager.EventService.Add<InventoryChangedEvent>(Refresh);
        private void OnDisable() => GameManager.EventService.Remove<InventoryChangedEvent>(Refresh);

        private void BuildRows()
        {
            if (rowPrefab == null || rowContainer == null) return;

            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType == null || blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(blockType);
                row.gameObject.SetActive(false);
                rowsByType[blockType.Id] = row;
            }
        }

        private void Refresh()
        {
            if (playerInventory == null) return;

            foreach (var kvp in rowsByType)
            {
                playerInventory.OreCounts.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);
                kvp.Value.gameObject.SetActive(count > 0);
            }
        }
    }
}
