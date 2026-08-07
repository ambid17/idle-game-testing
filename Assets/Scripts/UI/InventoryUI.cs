using System.Collections.Generic;
using Events;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    // Tab-toggled inventory panel per GameDesignDoc "Inventory": shows a count of each ore type,
    // a weight meter, and the artifact count (artifacts can't be stored/sold at the Depot, only
    // turned in at the Museum, so they get no sell UI here).
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Image weightFillBar;
        [SerializeField] private TMP_Text weightLabel;
        [SerializeField] private TMP_Text artifactLabel;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;

        private readonly Dictionary<BlockTypeId, OreRowUI> uiRowsByType = new();
        private bool isOpen;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        public bool IsOpen => isOpen;
        public void Close() => SetOpen(false);

        private void Start()
        {
            BuildRows();
            SetOpen(false);
            Refresh();
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<InventoryChangedEvent>(Refresh);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<InventoryChangedEvent>(Refresh);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                SetOpen(!isOpen);
            }
        }

        private void BuildRows()
        {
            if (rowPrefab == null || rowContainer == null)
            {
                Debug.LogError("DepotUI.BuildRows: Missing rowPrefab, or rowContainer. Cannot build ore rows.");
                return;
            }

            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType == null || blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.Id.ToString() : blockType.DisplayName;
                row.Bind(blockType.Id, displayName);
                uiRowsByType[blockType.Id] = row;
            }
        }

        private void SetOpen(bool open)
        {
            GameManager.EventService.Dispatch<InventoryOpenedEvent>();
            isOpen = open;
            panelRoot.SetActive(open);
        }

        private void Refresh()
        {
            foreach (var kvp in uiRowsByType)
            {
                playerInventory.OreCounts.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);
            }

            weightFillBar.fillAmount = playerInventory.MaxWeight > 0f
                ? Mathf.Clamp01(playerInventory.CurrentWeight / playerInventory.MaxWeight)
                : 0f;
            weightLabel.text = $"{playerInventory.CurrentWeight:0}/{playerInventory.MaxWeight:0}";
            artifactLabel.text = $"Artifacts: {playerInventory.ArtifactCount}";
        }
    }
}
