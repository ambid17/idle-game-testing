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
            CheckNullRefs();
            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);
            skillTreePanel.Initialize(new MarketSkillTreeSource());
        }

        private void CheckNullRefs()
        {
            if (panelRoot == null) Debug.LogError($"{nameof(MarketUI)}.{nameof(panelRoot)} is not assigned in the inspector.");
            if (dollarsLabel == null) Debug.LogError($"{nameof(MarketUI)}.{nameof(dollarsLabel)} is not assigned in the inspector.");
            if (closeButton == null) Debug.LogError($"{nameof(MarketUI)}.{nameof(closeButton)} is not assigned in the inspector.");
            if (skillTreePanel == null) Debug.LogError($"{nameof(MarketUI)}.{nameof(skillTreePanel)} is not assigned in the inspector.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Add<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Add<PurchaseRequestedEvent>(OnPurchaseRequested);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<UICloseEvent>(Close);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerInteractedEvent>(OnBuildingInteracted);
            GameManager.EventService.Remove<DollarsChangedEvent>(OnDollarsChanged);
            GameManager.EventService.Remove<PurchaseRequestedEvent>(OnPurchaseRequested);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<UICloseEvent>(Close);
        }

        private void OnBuildingInteracted(PlayerInteractedEvent evt)
        {
            if (evt.Type == InteractableType.Building_Market)
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
            if (panelRoot.activeSelf) return;
            InputBlocker.SetBlocked(true);
            panelRoot.SetActive(true);
            RefreshDollars();
            skillTreePanel.Open();
        }

        private void Close()
        {
            if (!panelRoot.activeSelf) return;
            InputBlocker.SetBlocked(false);
            panelRoot.SetActive(false);
            if (skillTreePanel != null) skillTreePanel.Close();
        }

        private void OnPurchaseRequested(PurchaseRequestedEvent evt) => UpgradeManager.Instance.TryPurchase(evt.Definition);

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => RefreshAll();
        private void OnDollarsChanged() => RefreshAll();

        private void RefreshAll()
        {
            RefreshDollars();
            skillTreePanel.RefreshAll();
        }

        private void RefreshDollars()
        {
            dollarsLabel.text = $"${Wallet.Instance.Dollars:0.##}";
        }
    }
}
