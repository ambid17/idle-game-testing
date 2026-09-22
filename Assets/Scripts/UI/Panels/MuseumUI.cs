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
    // Museum panel per GameDesignDoc "Map Layout > buildings > museum" / "# Prestige": artifacts are
    // the Museum's currency directly (no turn-in/conversion step), spent on the permanent perk tree
    // via the radial skill tree (see Assets/Docs/skillTreeImplementation.md). Perk purchases are
    // queued only - PrestigeUpgradeManager doesn't apply them until a prestige is actually triggered.
    // Per CLAUDE.md's UI panel rule, this controller stays enabled on the Panel GameObject and only
    // toggles the child rendererRoot. Blocks player input while open (like ControlCenterUI) since
    // "Prestige Now" is a destructive, irreversible action that shouldn't be one accidental click away.
    public class MuseumUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Button closeButton;

        [Header("Perk tree")]
        [SerializeField] private SkillTreePanelUI skillTreePanel;

        [Header("Artifact currency")]
        [SerializeField] private TMP_Text artifactCountLabel;

        [Header("Prestige trigger")]
        [SerializeField] private Button prestigeNowButton;
        [SerializeField] private MuseumPrestigeConfirmUI prestigeConfirm;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (prestigeNowButton != null) prestigeNowButton.onClick.AddListener(() => GameManager.EventService.Dispatch<PrestigeConfirmationRequestedEvent>());
            if (prestigeConfirm != null) prestigeConfirm.Initialize(ConfirmPrestige);

            if (skillTreePanel != null) skillTreePanel.Initialize(new MuseumSkillTreeSource());

            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradePurchased);
            GameManager.EventService.Add<PrestigeUpgradeQueuedEvent>(OnPrestigeUpgradeQueued);
            GameManager.EventService.Add<PrestigePurchaseRequestedEvent>(OnPrestigePurchaseRequested);
            GameManager.EventService.Add<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Add<PrestigeConfirmationRequestedEvent>(OnPrestigeConfirmationRequested);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<PrestigeUpgradePurchasedEvent>(OnPrestigeUpgradePurchased);
            GameManager.EventService.Remove<PrestigeUpgradeQueuedEvent>(OnPrestigeUpgradeQueued);
            GameManager.EventService.Remove<PrestigePurchaseRequestedEvent>(OnPrestigePurchaseRequested);
            GameManager.EventService.Remove<ArtifactCountChangedEvent>(RefreshArtifactCount);
            GameManager.EventService.Remove<PrestigeConfirmationRequestedEvent>(OnPrestigeConfirmationRequested);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.InteractableType == InteractableType.Building_Museum)
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
            if (skillTreePanel != null) skillTreePanel.Close();
            if (prestigeConfirm != null) prestigeConfirm.Close();
        }

        private void OnPrestigePurchaseRequested(PrestigePurchaseRequestedEvent evt) => PrestigeUpgradeManager.Instance.TryPurchase(evt.Definition);
        private void OnPrestigeUpgradePurchased(PrestigeUpgradePurchasedEvent evt) => RefreshAll();
        private void OnPrestigeUpgradeQueued(PrestigeUpgradeQueuedEvent evt) => RefreshAll();

        // MuseumUI owns the confirm sub-panel per the plan's "destructive action needs an explicit
        // confirm, not a single misclick" requirement - PrestigeManager only requests it.
        private void OnPrestigeConfirmationRequested()
        {
            if (prestigeConfirm != null) prestigeConfirm.Show();
        }

        // PrestigeManager.ExecutePrestige commits any queued perk purchases before touching anything
        // else - see its comment for why that ordering matters for map-gen perks.
        private void ConfirmPrestige() => PrestigeManager.Instance.ExecutePrestige();

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => Close();

        private void RefreshAll()
        {
            RefreshNonTreeUI();
            if (skillTreePanel != null) skillTreePanel.RefreshAll();
        }

        private void RefreshNonTreeUI() => RefreshArtifactCount();

        private void RefreshArtifactCount()
        {
            if (artifactCountLabel != null) artifactCountLabel.text = $"Stellar Credits: {Wallet.Instance.ArtifactCount}";
        }
    }
}
