using System.Collections.Generic;
using Economy;
using Interaction;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Storage depot panel per GameDesignDoc "Map Layout > buildings > storage depot": opening it
    // (via BuildingInteractable.Interacted on the Depot building) deposits everything the player is
    // carrying, then shows the depot's accrued minerals with per-type sell (any percentage, or all)
    // plus a sell-everything button.
    public class DepotUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private BuildingInteractable depotBuilding;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject otherPanelToClose;

        private readonly Dictionary<BlockTypeId, OreRowUI> rows = new();
        private PlayerInventory playerInventory;

        private void Start()
        {
            playerInventory = FindAnyObjectByType<PlayerInventory>();

            BuildRows();
            if (depotBuilding != null) depotBuilding.Interacted += Open;
            if (sellAllButton != null) sellAllButton.onClick.AddListener(() => Depot.Instance.SellAll());
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            Depot.Instance.DepotChanged += Refresh;
            Wallet.Instance.DollarsChanged += OnDollarsChanged;
        }

        private void OnDisable()
        {
            Depot.Instance.DepotChanged -= Refresh;
            Wallet.Instance.DollarsChanged -= OnDollarsChanged;
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
                row.SellRequested += OnSellRequested;
                rows[blockType.Id] = row;
            }
        }

        private void Open()
        {
            if (playerInventory != null) Depot.Instance.Deposit(playerInventory.WithdrawAllOre());

            if (otherPanelToClose != null) otherPanelToClose.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnSellRequested(BlockTypeId id, float fraction) => Depot.Instance.Sell(id, fraction);

        private void Refresh()
        {
            var database = GameManager.BlockTypeDatabase;

            foreach (var kvp in rows)
            {
                Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);

                var blockType = database != null ? database.Get((byte)kvp.Key) : null;
                kvp.Value.SetValue(blockType != null ? blockType.Value * count : 0f);
            }

            OnDollarsChanged(Wallet.Instance.Dollars);
        }

        private void OnDollarsChanged(double dollars)
        {
            if (dollarsLabel != null) dollarsLabel.text = $"${dollars:0.##}";
        }
    }
}
