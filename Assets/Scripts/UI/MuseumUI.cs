using System.Collections.Generic;
using Economy;
using Events;
using Interaction;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Museum panel per GameDesignDoc "Map Layout > buildings > museum" / "# Prestige": turn
    // artifacts in for Prestige points, spend them on the permanent perk tree (mirrors MarketUI's
    // tab-filter-by-branch layout), and trigger a prestige. Per CLAUDE.md's UI panel rule, this
    // controller stays enabled on the Panel GameObject and only toggles the child rendererRoot.
    // Blocks player input while open (like ControlCenterUI) since "Prestige Now" is a destructive,
    // irreversible action that shouldn't be one accidental click away.
    public class MuseumUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;

        [Header("Perk tree")]
        [SerializeField] private Transform scrollViewContent;
        [SerializeField] private PrestigeUpgradeNodeUI nodePrefab;
        [SerializeField] private TMP_Text prestigePointsLabel;
        [SerializeField] private Button miningButton;
        [SerializeField] private Button economyButton;
        [SerializeField] private Button idleButton;
        [SerializeField] private Button prestigeButton;
        [SerializeField] private Button progressionButton;
        [SerializeField] private Button survivalButton;

        [Header("Artifact turn-in")]
        [SerializeField] private TMP_Text artifactCountLabel;

        [Header("Prestige trigger")]
        [SerializeField] private Button prestigeNowButton;
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private readonly List<PrestigeUpgradeNodeUI> nodes = new();
        private PrestigeUpgradeDatabase upgradeDatabase => GameManager.PrestigeUpgradeDatabase;
        private PrestigeUpgradeBranch activeTab = PrestigeUpgradeBranch.Mining;

        private void Start()
        {
            BuildNodes();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (prestigeNowButton != null) prestigeNowButton.onClick.AddListener(() => PrestigeManager.Instance.RequestPrestige());
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmPrestige);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(() => confirmRoot.SetActive(false));

            miningButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Mining));
            economyButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Economy));
            idleButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Idle));
            prestigeButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Prestige));
            progressionButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Progression));
            survivalButton.onClick.AddListener(() => SetTab(PrestigeUpgradeBranch.Survival));

            if (confirmRoot != null) confirmRoot.SetActive(false);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradePurchased);
            GameManager.EventService.Add<PrestigePointsChangedEvent>(OnPrestigePointsChanged);
            GameManager.EventService.Add<PrestigePurchaseRequestedEvent>(OnPrestigePurchaseRequested);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Add<PrestigeConfirmationRequestedEvent>(OnPrestigeConfirmationRequested);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradePurchased);
            GameManager.EventService.Remove<PrestigePointsChangedEvent>(OnPrestigePointsChanged);
            GameManager.EventService.Remove<PrestigePurchaseRequestedEvent>(OnPrestigePurchaseRequested);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Remove<PrestigeConfirmationRequestedEvent>(OnPrestigeConfirmationRequested);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (evt.Type == InteractableType.Museum)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void BuildNodes()
        {
            if (nodePrefab == null)
            {
                Debug.LogError("MuseumUI: nodePrefab is not assigned.");
                return;
            }

            foreach (var def in upgradeDatabase.Upgrades)
            {
                if (def == null) continue;

                var node = Instantiate(nodePrefab, scrollViewContent);
                node.Bind(def);
                node.gameObject.SetActive(def.Branch == activeTab);
                nodes.Add(node);
            }
        }

        private void Open()
        {
            if (rendererRoot == null || rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            RefreshAll();
        }

        private void SetTab(PrestigeUpgradeBranch branch)
        {
            if (activeTab == branch) return;

            activeTab = branch;
            foreach (var node in nodes)
            {
                node.gameObject.SetActive(node.Definition.Branch == branch);
            }

            RefreshAll();
        }

        private void Close()
        {
            if (rendererRoot == null || !rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            if (confirmRoot != null) confirmRoot.SetActive(false);
        }

        private void OnPrestigePurchaseRequested(PrestigePurchaseRequestedEvent evt) => PrestigeUpgradeManager.Instance.TryPurchase(evt.Definition);
        private void OnPrestigeUpgradePurchased(PrestigeUpgradePurchasedEvent evt) => RefreshAll();
        private void OnPrestigePointsChanged() => RefreshAll();

        // MuseumUI owns the confirm sub-panel per the plan's "destructive action needs an explicit
        // confirm, not a single misclick" requirement - PrestigeManager only requests it.
        private void OnPrestigeConfirmationRequested()
        {
            if (confirmRoot != null) confirmRoot.SetActive(true);
        }

        // Auto-turns-in any remaining artifacts first so the player never silently loses banked
        // value to a hard reset they just confirmed.
        private void ConfirmPrestige()
        {
            Museum.Instance.TurnInArtifacts();
            PrestigeManager.Instance.ExecutePrestige();
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => Close();

        private void RefreshAll()
        {
            foreach (var node in nodes) node.Refresh(PrestigeUpgradeManager.Instance);
            if (prestigePointsLabel != null) prestigePointsLabel.text = $"{PrestigePoints.Instance.Points:0.##} pts";
            RefreshArtifactCount();
        }

        private void RefreshArtifactCount()
        {
            if (artifactCountLabel != null) artifactCountLabel.text = $"Artifacts: {Wallet.Instance.ArtifactCount}";
        }
    }
}
