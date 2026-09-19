using Economy;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.SkillTree
{
    // One node in the pannable/zoomable skill tree. Purely a view - border color is driven off
    // fields SkillTreePanelUI already sourced from UpgradeManager/PrestigeUpgradeManager via an
    // ISkillTreeSource, so unlock/purchase logic is never reimplemented here.
    public class SkillTreeNodeUI : MonoBehaviour
    {
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text levelBadge;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text displayNameLabel;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text costLabel;

        [Header("Border Colors")]
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color partialColor = Color.green;
        [SerializeField] private Color maxedColor = new Color(1f, 0.84f, 0f);

        [Header("Cost Text Colors")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = Color.red;

        // Set by the skill tree editor tool when a node is baked into the scene at edit time, so
        // SkillTreePanelUI can match this pre-placed instance back to its view model's Source
        // (an UpgradeDefinition/PrestigeUpgradeDefinition) on every RefreshAll without needing to
        // recreate the node. Also kept in sync at runtime by Bind() for nodes built dynamically.
        [SerializeField] private UpgradeDefinitionBase upgradeDefinition;
        public UpgradeDefinitionBase UpgradeDefinition => upgradeDefinition;

        public SkillTreeNodeViewModel ViewModel { get; private set; }

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
        }

        public void Bind(SkillTreeNodeViewModel viewModel, Action<SkillTreeNodeViewModel> onClicked)
        {
            upgradeDefinition = viewModel.UpgradeDefinition;
            gameObject.name = $"SkillTreeNode_{viewModel.DisplayName}";
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(ViewModel));

            displayNameLabel.text = viewModel.DisplayName;
            Refresh(viewModel);

            currencyIcon.sprite = viewModel.CurrencyIcon;
        }

        public void Refresh(SkillTreeNodeViewModel viewModel)
        {
            ViewModel = viewModel;
            if (viewModel == null)
            {
                Debug.LogError($"{nameof(SkillTreeNodeUI)}.Refresh(), viewModel was passed null.");
                return;
            }

            icon.sprite = viewModel.Icon;
            levelBadge.text = $"{viewModel.Level}/{viewModel.MaxLevel}";

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
            else if (viewModel.Level > 0)
            {
                border.color = partialColor;
                levelBadge.color = partialColor;
            }
            else
            {
                border.color = unlockedColor;
                levelBadge.color = unlockedColor;
            }

            costLabel.gameObject.SetActive(!viewModel.IsMaxed);
            currencyIcon.gameObject.SetActive(!viewModel.IsMaxed);

            costLabel.color = viewModel.CanPurchase ? affordableColor : unaffordableColor;
            costLabel.text = viewModel.CostLabel;
        }
    }
}
