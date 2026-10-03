using Economy;
using Events;
using Settings;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // One node in the pannable/zoomable skill tree. Purely a view - border color is driven off
    // fields SkillTreePanelUI already sourced from UpgradeManager/PrestigeUpgradeManager via an
    // ISkillTreeSource, so unlock/purchase logic is never reimplemented here. Hovering shows
    // SkillTreeTooltipUI (via SkillTreePanelUI); clicking the node's own button purchases
    // directly - there's no separate detail modal to open first. Controller selection shows the
    // tooltip like hovering and pans the tree to the node. Click-dragging starting on a node pans
    // the tree just like dragging the empty background (and cancels the click, so it won't buy).
    public class SkillTreeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IBeginDragHandler, IDragHandler
    {
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text levelBadge;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text displayNameLabel;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text costLabel;
        // Replaces the upgrade's own icon until its prerequisite is met, so the tree doesn't
        // reveal what an upgrade is (or costs) before it can actually be bought.
        [SerializeField] private Sprite lockedIcon;

        [Header("Border Colors")]
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color partialColor = Color.green;
        [SerializeField] private Color maxedColor = new Color(1f, 0.84f, 0f);

        [Header("Cost Text Colors")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = Color.red;

        [Header("Juice")]
        [Tooltip("The border breathes toward this while the node can be bought.")]
        [SerializeField] private Color affordableGlowColor = new(0.45f, 1f, 1f, 1f);
        [SerializeField] private float affordableBreatheSpeed = 3f;
        [Tooltip("Icon scale at the peak of the purchase punch.")]
        [SerializeField] private float purchasePunchScale = 1.35f;
        [SerializeField] private float punchSeconds = 0.35f;

        private Color borderBaseColor;
        private float punchAge = -1f;
        private float punchStrength;
        private float flashAge = -1f;

        // Set by the skill tree editor tool when a node is baked into the scene at edit time, so
        // SkillTreePanelUI can match this pre-placed instance back to its view model's Source
        // (an UpgradeDefinition/PrestigeUpgradeDefinition) on every RefreshAll without needing to
        // recreate the node. Also kept in sync at runtime by Bind() for nodes built dynamically.
        [SerializeField] private UpgradeDefinitionBase upgradeDefinition;
        public UpgradeDefinitionBase UpgradeDefinition => upgradeDefinition;

        public SkillTreeNodeViewModel ViewModel { get; private set; }

        private Action<SkillTreeNodeUI> onHoverEnter;
        private Action<SkillTreeNodeUI> onHoverExit;
        private Action<SkillTreeNodeUI> onSelected;
        private Action<PointerEventData> onDragged;

        private void Start()
        {
            CheckNullRefs();
        }

        private void CheckNullRefs()
        {
            if (border == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(border)} is not assigned in the inspector.");
            if (icon == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(icon)} is not assigned in the inspector.");
            if (levelBadge == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(levelBadge)} is not assigned in the inspector.");
            if (button == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(button)} is not assigned in the inspector.");
            if (displayNameLabel == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(displayNameLabel)} is not assigned in the inspector.");
            if (currencyIcon == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(currencyIcon)} is not assigned in the inspector.");
            if (costLabel == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(costLabel)} is not assigned in the inspector.");
            if (lockedIcon == null) Debug.LogError($"{nameof(SkillTreeNodeUI)}.{nameof(lockedIcon)} is not assigned in the inspector.");
        }

        public void Bind(
            SkillTreeNodeViewModel viewModel,
            Action<SkillTreeNodeViewModel> onPurchaseClicked,
            Action<SkillTreeNodeUI> onHoverEnter,
            Action<SkillTreeNodeUI> onHoverExit,
            Action<SkillTreeNodeUI> onSelected,
            Action<PointerEventData> onDragged)
        {
            upgradeDefinition = viewModel.UpgradeDefinition;
            gameObject.name = $"SkillTreeNode_{viewModel.DisplayName}";

            this.onHoverEnter = onHoverEnter;
            this.onHoverExit = onHoverExit;
            this.onSelected = onSelected;
            this.onDragged = onDragged;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onPurchaseClicked?.Invoke(ViewModel));

            displayNameLabel.text = viewModel.DisplayName;
            Refresh(viewModel);

            currencyIcon.sprite = viewModel.CurrencyIcon;
        }

        public void OnPointerEnter(PointerEventData eventData) => onHoverEnter?.Invoke(this);
        public void OnPointerExit(PointerEventData eventData) => onHoverExit?.Invoke(this);
        public void OnSelect(BaseEventData eventData)
        {
            // Clicking selects the Button too - only controller navigation should pan to the
            // node; a mouse click already has the tooltip up from hovering.
            if (eventData is PointerEventData) return;
            onSelected?.Invoke(this);
        }
        public void OnDeselect(BaseEventData eventData) => onHoverExit?.Invoke(this);

        // The Button is the drag target too (pointerPress == pointerDrag), so the input module
        // wouldn't cancel the click on its own - releasing after a pan would purchase.
        public void OnBeginDrag(PointerEventData eventData) => eventData.eligibleForClick = false;
        public void OnDrag(PointerEventData eventData) => onDragged?.Invoke(eventData);

        private void OnEnable()
        {
            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);
            punchAge = flashAge = -1f;
            icon.rectTransform.localScale = Vector3.one;
            if (ViewModel != null) border.color = borderBaseColor;
        }

        // Just bought (a level, or queued one): the icon punches, the border flashes white, and a
        // glow and gold sparkles burst out behind it. effectsParent: where the particles go (the
        // tree's content, so they pan/zoom with it and outlive a node rebuild).
        public void PlayPurchased(RectTransform effectsParent)
        {
            Punch(1f);
            flashAge = 0f;
            Vector2 center = effectsParent.InverseTransformPoint(transform.position);
            float size = ((RectTransform)transform).rect.width;
            UiFx.GlowFlash(effectsParent, center, size * 2.2f, new Color(1f, 0.9f, 0.5f, 0.8f), 0.45f);
            UiFx.SparkleBurst(effectsParent, center, ViewModel.IsMaxed ? 22 : 14, size * 0.35f, size * 4f, size * 0.32f, ViewModel.IsMaxed ? UiFx.Gold : UiFx.Cyan);
        }

        // Its prerequisite was just bought, revealing it: a smaller pop and a cyan flash.
        public void PlayUnlocked(RectTransform effectsParent)
        {
            Punch(0.7f);
            flashAge = 0f;
            Vector2 center = effectsParent.InverseTransformPoint(transform.position);
            float size = ((RectTransform)transform).rect.width;
            UiFx.GlowFlash(effectsParent, center, size * 1.8f, new Color(0.45f, 1f, 1f, 0.6f), 0.4f);
            UiFx.SparkleBurst(effectsParent, center, 8, size * 0.4f, size * 2.5f, size * 0.25f, UiFx.Cyan);
        }

        // Just became affordable while the tree is open: a little ping so the eye finds it.
        public void PlayBecameAffordable() => Punch(0.4f);

        private void Punch(float strength)
        {
            punchAge = 0f;
            punchStrength = strength;
        }

        private void Update()
        {
            if (ViewModel == null) return;
            float dt = Time.unscaledDeltaTime;

            if (punchAge >= 0f)
            {
                punchAge += dt;
                float k = Mathf.Clamp01(punchAge / punchSeconds);
                // Snaps up fast, then a damped wobble back to rest.
                float wobble = Mathf.Exp(-6f * k) * Mathf.Cos(k * Mathf.PI * 3f);
                icon.rectTransform.localScale = Vector3.one * (1f + (purchasePunchScale - 1f) * punchStrength * wobble);
                if (k >= 1f)
                {
                    punchAge = -1f;
                    icon.rectTransform.localScale = Vector3.one;
                }
            }

            var color = borderBaseColor;
            if (ViewModel.CanPurchase)
            {
                float breathe = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * affordableBreatheSpeed);
                color = Color.Lerp(borderBaseColor, affordableGlowColor, breathe * 0.75f);
            }
            if (flashAge >= 0f)
            {
                flashAge += dt;
                float flash = 1f - Mathf.Clamp01(flashAge / 0.3f);
                color = Color.Lerp(color, Color.white, flash);
                if (flash <= 0f) flashAge = -1f;
            }
            border.color = color;
        }

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt)
        {
            if (ViewModel != null) Refresh(ViewModel);
        }

        public void Refresh(SkillTreeNodeViewModel viewModel)
        {
            ViewModel = viewModel;
            if (viewModel == null)
            {
                Debug.LogError($"{nameof(SkillTreeNodeUI)}.Refresh(), viewModel was passed null.");
                return;
            }

            icon.sprite = viewModel.IsUnlocked ? viewModel.Icon : lockedIcon;
            displayNameLabel.gameObject.SetActive(viewModel.IsUnlocked);
            levelBadge.text = viewModel.QueuedLevel > 0
                ? $"{viewModel.Level}+{viewModel.QueuedLevel}/{viewModel.MaxLevel}"
                : $"{viewModel.Level}/{viewModel.MaxLevel}";

            if (!viewModel.IsUnlocked)
            {
                border.color = lockedColor;
                levelBadge.color = lockedColor;
            }
            else if (viewModel.IsMaxed)
            {
                border.color = maxedColor;
                levelBadge.color = maxedColor;
            }
            else if (viewModel.Level > 0 || viewModel.QueuedLevel > 0)
            {
                border.color = partialColor;
                levelBadge.color = partialColor;
            }
            else
            {
                border.color = unlockedColor;
                levelBadge.color = unlockedColor;
            }
            borderBaseColor = border.color;

            bool showCost = viewModel.IsUnlocked && !viewModel.IsMaxed;
            costLabel.gameObject.SetActive(showCost);
            currencyIcon.gameObject.SetActive(showCost);

            costLabel.color = viewModel.CanPurchase ? affordableColor : unaffordableColor;
            costLabel.text = viewModel.CostLabel;

            // A non-interactable Selectable can't be navigated to, so on a controller every node
            // stays selectable (to read its tooltip) - purchasing is still validated by
            // UpgradeManager/PrestigeUpgradeManager.TryPurchase, and the cost color shows affordability.
            button.interactable = viewModel.CanPurchase || GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad;
        }
    }
}
