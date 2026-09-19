using Economy;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: give carried ore to the player, or fill/clear the Depot bank directly.
    public class DevPanelResourceTab : MonoBehaviour
    {
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Transform oreRowContainer;
        [SerializeField] private DevOreRowUI oreRowPrefab;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private Button fillDepotButton;
        [SerializeField] private Button clearDepotButton;

        private void Start()
        {
            if (playerInventory == null) Debug.LogError("DevPanelResourceTab.playerInventory is not assigned.");
            if (oreRowContainer == null) Debug.LogError("DevPanelResourceTab.oreRowContainer is not assigned.");
            if (oreRowPrefab == null) Debug.LogError("DevPanelResourceTab.oreRowPrefab is not assigned.");
            if (amountInput == null) Debug.LogError("DevPanelResourceTab.amountInput is not assigned.");
            if (fillDepotButton == null) Debug.LogError("DevPanelResourceTab.fillDepotButton is not assigned.");
            if (clearDepotButton == null) Debug.LogError("DevPanelResourceTab.clearDepotButton is not assigned.");

            BuildOreRows();

            if (fillDepotButton != null) fillDepotButton.onClick.AddListener(OnFillDepotClicked);
            if (clearDepotButton != null) clearDepotButton.onClick.AddListener(() => Depot.Instance.ClearAll());
        }

        private void BuildOreRows()
        {
            if (oreRowContainer == null || oreRowPrefab == null) return;

            foreach (var blockType in GameManager.BlockTypeDatabase.BlockTypes)
            {
                if (blockType == null || blockType.Category != BlockCategory.Ore) continue;
                var row = Instantiate(oreRowPrefab, oreRowContainer);
                row.Bind(blockType, OnGiveOreClicked);
            }
        }

        private void OnGiveOreClicked(BlockType blockType)
        {
            if (playerInventory == null) return;
            int amount = ParseAmount();
            if (amount > 0) playerInventory.AddOre(blockType, amount);
        }

        private void OnFillDepotClicked()
        {
            int amount = ParseAmount();
            if (amount <= 0) return;

            foreach (var blockType in GameManager.BlockTypeDatabase.BlockTypes)
            {
                if (blockType != null && blockType.Category == BlockCategory.Ore)
                {
                    Depot.Instance.Deposit(blockType.Id, amount);
                }
            }
        }

        private int ParseAmount() =>
            amountInput != null && int.TryParse(amountInput.text, out var amount) ? amount : 0;
    }
}
