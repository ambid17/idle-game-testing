using Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // Hover tooltip for a skill tree node: name/description/level/cost + why a purchase is
    // blocked, if it is. Read-only - purchasing happens by clicking the node itself
    // (SkillTreeNodeUI/SkillTreePanelUI.OnNodePurchaseClicked), which routes into the same
    // pipeline the classic tabbed UI already uses (via ISkillTreeSource.RequestPurchase ->
    // PurchaseRequestedEvent/PrestigePurchaseRequestedEvent). Not a ModalBase: it never blocks
    // input or needs an Escape close, it just follows the hovered node and hides on pointer exit.
    public class SkillTreeTooltipUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text purchaseBlockReasonLabel;
        [SerializeField] private Vector2 anchorOffset = new Vector2(24f, 24f);

        // Just the definition identity - never a cached snapshot of its level/cost/affordability.
        // Refresh() re-queries source.GetDetails(current) live every time, so this can't go stale.
        private UpgradeDefinitionBase upgradeDefinition;
        private ISkillTreeSource skillTreeSource;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            root.SetActive(false);
        }

        private void Start()
        {
            CheckNullRefs();
        }

        private void CheckNullRefs()
        {
            if (root == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(root)} is not assigned in the inspector.");
            if (card == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(card)} is not assigned in the inspector.");
            if (nameLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(nameLabel)} is not assigned in the inspector.");
            if (descriptionLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(descriptionLabel)} is not assigned in the inspector.");
            if (levelLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(levelLabel)} is not assigned in the inspector.");
            if (costLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(costLabel)} is not assigned in the inspector.");
            if (purchaseBlockReasonLabel == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}.{nameof(purchaseBlockReasonLabel)} is not assigned in the inspector.");
            if (canvas == null) Debug.LogError($"{nameof(SkillTreeTooltipUI)}: no parent Canvas found, tooltip positioning will be inaccurate.");
        }

        public void Initialize(ISkillTreeSource source) => this.skillTreeSource = source;

        public void Show(UpgradeDefinitionBase definition, RectTransform anchor)
        {
            upgradeDefinition = definition;
            root.SetActive(true);
            PositionNear(anchor);
            Refresh();
        }

        public void Hide()
        {
            upgradeDefinition = null;
            root.SetActive(false);
        }

        public void Refresh()
        {
            if (upgradeDefinition == null || skillTreeSource == null || !root.activeSelf) return;

            var details = skillTreeSource.GetDetails(upgradeDefinition);
            nameLabel.text = details.DisplayName;
            descriptionLabel.text = details.Description;
            levelLabel.text = $"{details.Level}/{details.MaxLevel}";
            costLabel.text = details.CostLabel;
            purchaseBlockReasonLabel.text = details.CanPurchase ? "" : details.PurchaseBlockedReason;
        }

        // Places the tooltip card next to the hovered node, converted through screen space so it
        // works regardless of the pan/zoom transform the node itself lives under, then clamps it
        // to stay fully inside this tooltip's own parent rect so it can't drift off-panel at the
        // tree's edges.
        private void PositionNear(RectTransform anchor)
        {
            if (anchor == null || card == null) return;
            var parent = card.parent as RectTransform;
            if (parent == null) return;

            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, cam, out Vector2 localPoint)) return;

            localPoint += anchorOffset;

            float halfWidth = card.rect.width * 0.5f;
            float halfHeight = card.rect.height * 0.5f;
            Rect parentRect = parent.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, parentRect.xMin + halfWidth, parentRect.xMax - halfWidth);
            localPoint.y = Mathf.Clamp(localPoint.y, parentRect.yMin + halfHeight, parentRect.yMax - halfHeight);

            card.anchoredPosition = localPoint;
        }
    }
}
