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
    // (via BuildingInteractedEvent from the Depot building) shows the depot's accrued minerals with
    // per-type sell (any percentage, or all) plus a sell-everything button. A separate, explicit
    // "Deposit All" button banks whatever the player is currently carrying into the depot without
    // selling any of it - kept as its own action (rather than an implicit side effect of opening
    // the panel) so depositing reads as an intentional player choice. Also shows Processing Center
    // goods in a second, parallel row list (goodsRowContainer/goodsRowPrefab/sellAllGoodsButton)
    // since Depot banks them separately from ore - see Depot.cs's StoredGoods.
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
            CheckNullRefs();

            playerInventory = FindAnyObjectByType<PlayerInventory>();

            BuildOreRows();
            BuildGoodsRows();

            sellAllButton.onClick.AddListener(() => Depot.Instance.SellAll());
            sellAllGoodsButton.onClick.AddListener(() => Depot.Instance.SellAllGoods());
            closeButton.onClick.AddListener(Close);

            panelRoot.SetActive(false);
        }

        private void CheckNullRefs()
        {
            if (panelRoot == null) Debug.LogError("DepotUI.panelRoot is not assigned.");
            if (rowContainer == null) Debug.LogError("DepotUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("DepotUI.rowPrefab is not assigned.");
            if (goodsRowContainer == null) Debug.LogError("DepotUI.goodsRowContainer is not assigned.");
            if (goodsRowPrefab == null) Debug.LogError("DepotUI.goodsRowPrefab is not assigned.");
            if (dollarsLabel == null) Debug.LogError("DepotUI.dollarsLabel is not assigned.");
            if (sellAllButton == null) Debug.LogError("DepotUI.sellAllButton is not assigned.");
            if (sellAllButtonLabel == null) Debug.LogError("DepotUI.sellAllButtonLabel is not assigned.");
            if (sellAllGoodsButton == null) Debug.LogError("DepotUI.sellAllGoodsButton is not assigned.");
            if (sellAllGoodsButtonLabel == null) Debug.LogError("DepotUI.sellAllGoodsButtonLabel is not assigned.");
            if (closeButton == null) Debug.LogError("DepotUI.closeButton is not assigned.");
            if (recipeDatabase == null) Debug.LogError("DepotUI.recipeDatabase is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<DepotChangedEvent>(Refresh);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Add<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<DepotChangedEvent>(Refresh);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Remove<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType != InteractableType.Building_Depot)
            {
                Close();
                return;
            }

            switch (evt.InteractionType)
            {
                case InteractionType.Primary:
                    Open();
                    break;
                case InteractionType.Secondary:
                    DepositAll();
                    // TODO: show toast with ore deposited, and animate inventory weight bar emptying
                    break;
                case InteractionType.Tertiary:
                    DepositAll();
                    Depot.Instance.SellAll();
                    // TODO: show toast with ore deposited, animate inventory weight bar emptying, and money made from selling it
                    break;
                default:
                    Close();
                    break;
            }

        }

        private void BuildOreRows()
        {
            foreach (var blockType in blockTypeDatabase.BlockTypes)
            {
                if (blockType.Category != BlockCategory.Ore) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(blockType);
                row.gameObject.name = $"OreRow_{blockType.name}";
                rows[blockType.Id] = row;
            }
        }

        private void BuildGoodsRows()
        {
            foreach (var recipe in recipeDatabase.Recipes)
            {
                var row = Instantiate(goodsRowPrefab, goodsRowContainer);
                row.Bind(recipe);
                row.gameObject.name = $"GoodsRow_{recipe.name}";
                goodsRows[recipe.Id] = row;
            }
        }

        private void Open()
        {
            if (panelRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if(!panelRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            panelRoot.SetActive(false);
        }

        private void OnSellRequested(SellRequestedEvent evt) => Depot.Instance.Sell(evt.Id, evt.Fraction);
        private void OnSellGoodsRequested(SellGoodsRequestedEvent evt) => Depot.Instance.SellGood(evt.Id, evt.Fraction);

        // Explicit action (as opposed to an implicit side effect of opening the panel) so banking
        // carried ore reads as an intentional player choice, distinct from selling it.
        private void DepositAll()
        {
            Depot.Instance.Deposit(playerInventory.WithdrawAllOre());
        }

        private void Refresh()
        {
            var totalValue = 0f;
            foreach (var kvp in rows)
            {
                Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var count);
                totalValue += kvp.Value.SetCount(count);
            }

            sellAllButtonLabel.text = $"Sell All (${totalValue:0})";

            var totalGoodsValue = 0f;
            foreach (var kvp in goodsRows)
            {
                Depot.Instance.StoredGoods.TryGetValue(kvp.Key, out var count);
                totalGoodsValue += kvp.Value.SetCount(count);
            }

            sellAllGoodsButtonLabel.text = $"Sell All Goods (${totalGoodsValue:0})";

            OnDollarsChanged();
        }

        private void OnDollarsChanged()
        {
            dollarsLabel.text = $"${Wallet.Instance.Dollars:0}";
        }
    }
}
