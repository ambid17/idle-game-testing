using Economy;
using Events;
using Interaction;
using Player;
using TMPro;
using UI.SkillTree;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Museum panel per GameDesignDoc "Map Layout > buildings > museum" / "# Prestige": turn
    // artifacts in for Prestige points, spend them on the permanent perk tree via the radial skill
    // tree (see Assets/Docs/skillTreeImplementation.md), and trigger a prestige. Per CLAUDE.md's UI
    // panel rule, this controller stays enabled on the Panel GameObject and only toggles the child
    // rendererRoot. Blocks player input while open (like ControlCenterUI) since "Prestige Now" is a
    // destructive, irreversible action that shouldn't be one accidental click away.
    public class MuseumUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;

        [Header("Perk tree")]
        [SerializeField] private TMP_Text prestigePointsLabel;
        [SerializeField] private SkillTreePanelUI skillTreePanel;

        [Header("Artifact turn-in")]
        [SerializeField] private TMP_Text artifactCountLabel;

        [Header("Prestige trigger")]
        [SerializeField] private Button prestigeNowButton;
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (prestigeNowButton != null) prestigeNowButton.onClick.AddListener(() => PrestigeManager.Instance.RequestPrestige());
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmPrestige);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(() => confirmRoot.SetActive(false));

            if (skillTreePanel != null) skillTreePanel.Initialize(new MuseumSkillTreeSource());

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

        private void Open()
        {
            if (rendererRoot == null || rendererRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            RefreshNonTreeUI();
            if (skillTreePanel != null) skillTreePanel.Open();
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
            RefreshNonTreeUI();
            if (skillTreePanel != null) skillTreePanel.RefreshAll();
        }

        private void RefreshNonTreeUI()
        {
            if (prestigePointsLabel != null) prestigePointsLabel.text = $"{PrestigePoints.Instance.Points:0.##} pts";
            RefreshArtifactCount();
        }

        private void RefreshArtifactCount()
        {
            if (artifactCountLabel != null) artifactCountLabel.text = $"Artifacts: {Wallet.Instance.ArtifactCount}";
            prestigeNowButton.interactable = Wallet.Instance.ArtifactCount > 0;
        }
    }
}
