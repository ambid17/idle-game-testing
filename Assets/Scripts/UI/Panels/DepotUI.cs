using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using MapGeneration;
using Player;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Storage depot panel per GameDesignDoc "Map Layout > buildings > storage depot": opening it
    // (via BuildingInteractedEvent from the Depot building) deposits everything the player is
    // carrying, then shows the depot's accrued minerals with per-type sell (any percentage, or all)
    // plus a sell-everything button. Also shows Processing Center goods in a second, parallel row
    // list (goodsRowContainer/goodsRowPrefab/sellAllGoodsButton) since Depot banks them separately
    // from ore - see Depot.cs's StoredGoods.
    public class DepotUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;
        [SerializeField] private Transform goodsRowContainer;
        [SerializeField] private GoodsRowUI goodsRowPrefab;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private TMP_Text sellAllButtonLabel;
        [SerializeField] private Button sellAllGoodsButton;
        [SerializeField] private TMP_Text sellAllGoodsButtonLabel;
        [SerializeField] private Button closeButton;

        private readonly Dictionary<BlockTypeId, OreRowUI> rows = new();
        private readonly Dictionary<ProcessingRecipeId, GoodsRowUI> goodsRows = new();
        private PlayerInventory playerInventory;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;
        private ProcessingRecipeDatabase recipeDatabase => GameManager.ProcessingRecipeDatabase;

        private void Start()
        {
            playerInventory = FindAnyObjectByType<PlayerInventory>();

            BuildRows();
            BuildGoodsRows();
            if (sellAllButton != null) sellAllButton.onClick.AddListener(() => Depot.Instance.SellAll());
            if (sellAllGoodsButton != null) sellAllGoodsButton.onClick.AddListener(() => Depot.Instance.SellAllGoods());
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<DepotChangedEvent>(Refresh);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Add<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<DepotChangedEvent>(Refresh);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Remove<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if(evt.Type == InteractableType.Depot)
            {
                Open();
            }
            else
            {
                Close();
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
                if (blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(blockType.Id, displayName);
                row.gameObject.name = $"Row_{blockType.name}";
                rows[blockType.Id] = row;
            }
        }

        private void BuildGoodsRows()
        {
            if (goodsRowPrefab == null || goodsRowContainer == null) return;
            if (recipeDatabase == null)
            {
                Debug.LogError("DepotUI.BuildGoodsRows: GameManager.ProcessingRecipeDatabase is not assigned.");
                return;
            }

            foreach (var recipe in recipeDatabase.Recipes)
            {
                if (recipe == null) continue;

                var row = Instantiate(goodsRowPrefab, goodsRowContainer);
                string displayName = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.name : recipe.DisplayName;
                row.Bind(recipe.Id, displayName);
                row.gameObject.name = $"Row_{recipe.name}";
                goodsRows[recipe.Id] = row;
            }
        }

        private void Open()
        {
            Depot.Instance.Deposit(playerInventory.WithdrawAllOre());
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if(panelRoot == null || !panelRoot.activeSelf) return;
            panelRoot.SetActive(false);
        }

        private void OnSellRequested(SellRequestedEvent evt) => Depot.Instance.Sell(evt.Id, evt.Fraction);
        private void OnSellGoodsRequested(SellGoodsRequestedEvent evt) => Depot.Instance.SellGood(evt.Id, evt.Fraction);

        private void Refresh()
        {
            var totalValue = 0f;
            foreach (var kvp in rows)
            {
                Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);

                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
                var value = blockType.Value * count;
                kvp.Value.SetValue(value);
                totalValue += value;
            }

            sellAllButtonLabel.text = $"Sell All (${totalValue:0.##})";

            var totalGoodsValue = 0f;
            foreach (var kvp in goodsRows)
            {
                Depot.Instance.StoredGoods.TryGetValue(kvp.Key, out var count);
                kvp.Value.SetCount(count);

                var recipe = recipeDatabase != null ? recipeDatabase.Get(kvp.Key) : null;
                var value = (recipe != null ? recipe.SaleValue : 0f) * count;
                kvp.Value.SetValue(value);
                totalGoodsValue += value;
            }

            if (sellAllGoodsButtonLabel != null) sellAllGoodsButtonLabel.text = $"Sell All Goods (${totalGoodsValue:0.##})";

            OnDollarsChanged();
        }

        private void OnDollarsChanged()
        {
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
