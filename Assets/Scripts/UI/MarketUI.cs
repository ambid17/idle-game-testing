using System.Collections.Generic;
using Economy;
using Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Market panel per GameDesignDoc "Map Layout > buildings > market": purchase Mining/Economy
    // upgrades with Dollars. Skill-tree gating (previous tier required) is enforced by
    // UpgradeManager.IsUnlocked; this just renders every UpgradeDefinition in
    // GameManager.UpgradeDatabase (split into its Mining/Economy containers) and forwards
    // purchase clicks.
    public class MarketUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private BuildingInteractable marketBuilding;
        [SerializeField] private Transform miningContainer;
        [SerializeField] private Transform economyContainer;
        [SerializeField] private UpgradeNodeUI nodePrefab;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject otherPanelToClose;

        private readonly List<UpgradeNodeUI> nodes = new();
        private UpgradeDatabase upgradeDatabase => GameManager.UpgradeDatabase;
        

        private void Start()
        {
            BuildNodes();
            if (marketBuilding != null) marketBuilding.Interacted += Open;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.UpgradePurchased += OnUpgradePurchased;
            if (Wallet.Instance != null) Wallet.Instance.DollarsChanged += OnDollarsChanged;
        }

        private void OnDisable()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.UpgradePurchased -= OnUpgradePurchased;
            if (Wallet.Instance != null) Wallet.Instance.DollarsChanged -= OnDollarsChanged;
        }

        private void BuildNodes()
        {
            if (nodePrefab == null)
            {
                Debug.LogError("MarketUI: nodePrefab is not assigned.");
                return;
            }

            foreach (var def in upgradeDatabase.Upgrades)
            {
                if (def == null) continue;

                var container = def.Branch == UpgradeBranch.Economy ? economyContainer : miningContainer;
                if (container == null) continue;

                var node = Instantiate(nodePrefab, container);
                node.Bind(def);
                node.PurchaseRequested += OnPurchaseRequested;
                nodes.Add(node);
            }
        }

        private void Open()
        {
            if (otherPanelToClose != null) otherPanelToClose.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshAll();
        }

        private void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnPurchaseRequested(UpgradeDefinition def) => UpgradeManager.Instance.TryPurchase(def);

        private void OnUpgradePurchased(UpgradeDefinition def, int level) => RefreshAll();
        private void OnDollarsChanged() => RefreshAll();

        private void RefreshAll()
        {
            foreach (var node in nodes) node.Refresh(UpgradeManager.Instance);
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
