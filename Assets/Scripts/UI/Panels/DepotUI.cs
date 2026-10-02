using System.Collections.Generic;
using Audio;
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
    // (via BuildingInteractedEvent from the Depot building) shows the depot's accrued minerals with
    // per-type sell (any percentage, or all) plus a sell-everything button. A separate, explicit
    // "Deposit All" button banks whatever the player is currently carrying into the depot without
    // selling any of it - kept as its own action (rather than an implicit side effect of opening
    // the panel) so depositing reads as an intentional player choice. Processing Center goods are
    // banked in the Depot too, but are listed and sold on that building's Exchange tab instead.
    public class DepotUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private TMP_Text sellAllButtonLabel;
        [SerializeField] private Button depositButton;
        [SerializeField] private Button closeButton;

        private readonly Dictionary<BlockTypeId, OreRowUI> rows = new();
        private PlayerInventory playerInventory;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            CheckNullRefs();

            playerInventory = FindAnyObjectByType<PlayerInventory>();

            BuildOreRows();

            sellAllButton.onClick.AddListener(() => Depot.Instance.SellAll());
            depositButton.onClick.AddListener(DepositAll);
            closeButton.onClick.AddListener(Close);

            panelRoot.SetActive(false);
        }

        private void CheckNullRefs()
        {
            if (panelRoot == null) Debug.LogError("DepotUI.panelRoot is not assigned.");
            if (rowContainer == null) Debug.LogError("DepotUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("DepotUI.rowPrefab is not assigned.");
            if (dollarsLabel == null) Debug.LogError("DepotUI.dollarsLabel is not assigned.");
            if (sellAllButton == null) Debug.LogError("DepotUI.sellAllButton is not assigned.");
            if (sellAllButtonLabel == null) Debug.LogError("DepotUI.sellAllButtonLabel is not assigned.");
            if (depositButton == null) Debug.LogError("DepotUI.depositButton is not assigned.");
            if (closeButton == null) Debug.LogError("DepotUI.closeButton is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<DepotChangedEvent>(Refresh);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Add<SellLockToggleRequestedEvent>(OnSellLockToggleRequested);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<DepotChangedEvent>(Refresh);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Remove<SellLockToggleRequestedEvent>(OnSellLockToggleRequested);
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
                row.EnableSellLock();
                row.gameObject.name = $"OreRow_{blockType.name}";
                rows[blockType.Id] = row;
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
        private void OnSellLockToggleRequested(SellLockToggleRequestedEvent evt) => Depot.Instance.SetSellLocked(evt.Id, !Depot.Instance.IsSellLocked(evt.Id));

        // Explicit action (as opposed to an implicit side effect of opening the panel) so banking
        // carried ore reads as an intentional player choice, distinct from selling it.
        private void DepositAll()
        {
            var withdrawn = playerInventory.WithdrawAllOre();
            Depot.Instance.Deposit(withdrawn);
            if (withdrawn.Count > 0) GameManager.AudioService.Play(SoundId.Deposit);
        }

        private void Refresh()
        {
            var totalValue = 0f;
            foreach (var kvp in rows)
            {
                // Rows stay hidden until the ore has been banked at least once.
                kvp.Value.gameObject.SetActive(Depot.Instance.IsOreDiscovered(kvp.Key));
                Depot.Instance.StoredOres.TryGetValue(kvp.Key, out var count);
                var rowValue = kvp.Value.SetCount(count);

                // Sell-locked ores stay out of the Sell All total, matching what Depot.SellAll pays.
                bool locked = Depot.Instance.IsSellLocked(kvp.Key);
                kvp.Value.SetSellLocked(locked);
                if (!locked) totalValue += rowValue;
            }

            sellAllButtonLabel.text = $"Sell All (${totalValue:0})";

            OnDollarsChanged();
        }

        private void OnDollarsChanged()
        {
            dollarsLabel.text = $"${Wallet.Instance.Dollars:0}";
        }
    }
}
