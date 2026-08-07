using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Storage depot panel per GameDesignDoc "Map Layout > buildings > storage depot": opening it
    // (via BuildingInteractedEvent from the Depot building) deposits everything the player is
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
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            playerInventory = FindAnyObjectByType<PlayerInventory>();

            BuildRows();
            if (sellAllButton != null) sellAllButton.onClick.AddListener(() => Depot.Instance.SellAll());
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<DepotChangedEvent>(Refresh);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<DepotChangedEvent>(Refresh);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (depotBuilding == null || evt.Type != depotBuilding.Type) return;
            Open();
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
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(blockType.Id, displayName);
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

        private void OnSellRequested(SellRequestedEvent evt) => Depot.Instance.Sell(evt.Id, evt.Fraction);

        private void Refresh()
        {
            foreach (var kvp in rows)
            {
                Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);

                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
                kvp.Value.SetValue(blockType.Value * count);
            }

            OnDollarsChanged();
        }

        private void OnDollarsChanged()
        {
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
