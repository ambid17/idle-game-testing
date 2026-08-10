using Economy;
using Events;
using Interaction;
using TMPro;
using UI.SkillTree;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Market panel per GameDesignDoc "Map Layout > buildings > market": purchase Mining/Economy/
    // Automation/Progression upgrades with Dollars via the radial skill tree (see
    // Assets/Docs/skillTreeImplementation.md). Skill-tree gating (previous tier required) is
    // enforced by UpgradeManager.IsUnlocked; purchase clicks route through
    // SkillTreeDetailModalUI -> MarketSkillTreeSource -> PurchaseRequestedEvent below.
    public class MarketUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text dollarsLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private SkillTreePanelUI skillTreePanel;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (skillTreePanel != null) skillTreePanel.Initialize(new MarketSkillTreeSource());
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<PurchaseRequestedEvent>(OnPurchaseRequested);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BuildingInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<PurchaseRequestedEvent>(OnPurchaseRequested);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(BuildingInteractedEvent evt)
        {
            if (evt.Type == InteractableType.Market)
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
            panelRoot.SetActive(true);
            RefreshDollars();
            if (skillTreePanel != null) skillTreePanel.Open();
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
            RefreshDollars();
            if (skillTreePanel != null) skillTreePanel.RefreshAll();
        }

        private void RefreshDollars()
        {
            if (dollarsLabel != null) dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
