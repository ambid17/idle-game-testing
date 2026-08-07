using System.Collections.Generic;
using Economy;
using Events;
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
        [SerializeField] private Transform scrollViewContent;
        [SerializeField] private UpgradeNodeUI nodePrefab;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject otherPanelToClose;
        [SerializeField] private Button miningButton;
        [SerializeField] private Button economyButton;
        [SerializeField] private Button automationButton;
        [SerializeField] private Button progressionButton;

        private readonly List<UpgradeNodeUI> nodes = new();
        private UpgradeDatabase upgradeDatabase => GameManager.UpgradeDatabase;
        private UpgradeBranch activeTab = UpgradeBranch.Mining;


        private void Start()
        {
            BuildNodes();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);

            miningButton.onClick.AddListener(() => SetTab(UpgradeBranch.Mining));
            economyButton.onClick.AddListener(() => SetTab(UpgradeBranch.Economy));
            automationButton.onClick.AddListener(() => SetTab(UpgradeBranch.Automation));
            progressionButton.onClick.AddListener(() => SetTab(UpgradeBranch.Progression));
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<PurchaseRequestedEvent>(OnPurchaseRequested);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<PurchaseRequestedEvent>(OnPurchaseRequested);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (marketBuilding == null || evt.Type != marketBuilding.Type) return;
            Open();
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


                var node = Instantiate(nodePrefab, scrollViewContent);
                node.Bind(def);
                nodes.Add(node);
            }
        }

        private void Open()
        {
            if (otherPanelToClose != null) otherPanelToClose.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshAll();
        }

        private void SetTab(UpgradeBranch branch)
        {
            activeTab = branch;
            foreach (var node in nodes)
            {
                node.gameObject.SetActive(node.Definition.Branch == branch);
            }

            RefreshAll();
        }

        private void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnPurchaseRequested(PurchaseRequestedEvent evt) => UpgradeManager.Instance.TryPurchase(evt.Definition);

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => RefreshAll();
        private void OnDollarsChanged() => RefreshAll();

        private void RefreshAll()
        {
            foreach (var node in nodes) node.Refresh(UpgradeManager.Instance);
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
