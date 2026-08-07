using System.Collections.Generic;
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
        [SerializeField] private GameObject otherPanelToClose;

        private readonly Dictionary<BlockTypeId, OreRowUI> rows = new();
        private bool isOpen;

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
            if (playerInventory != null) playerInventory.InventoryChanged += Refresh;
        }

        private void OnDisable()
        {
            if (playerInventory != null) playerInventory.InventoryChanged -= Refresh;
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
            var database = GameManager.BlockTypeDatabase;
            if (database == null || rowPrefab == null || rowContainer == null) return;

            foreach (var blockType in database.BlockTypes)
            {
                if (blockType == null || blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(blockType.Id, displayName);
                rows[blockType.Id] = row;
            }
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            if (panelRoot != null) panelRoot.SetActive(open);
            if (open)
            {
                if (otherPanelToClose != null) otherPanelToClose.SetActive(false);
                Refresh();
            }
        }

        private void Refresh()
        {
            if (playerInventory == null) return;

            foreach (var kvp in rows)
            {
                playerInventory.OreCounts.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);
            }

            if (weightFillBar != null)
            {
                weightFillBar.fillAmount = playerInventory.MaxWeight > 0f
                    ? Mathf.Clamp01(playerInventory.CurrentWeight / playerInventory.MaxWeight)
                    : 0f;
            }

            if (weightLabel != null) weightLabel.text = $"{playerInventory.CurrentWeight:0}/{playerInventory.MaxWeight:0}";
            if (artifactLabel != null) artifactLabel.text = $"Artifacts: {playerInventory.ArtifactCount}";
        }
    }
}
