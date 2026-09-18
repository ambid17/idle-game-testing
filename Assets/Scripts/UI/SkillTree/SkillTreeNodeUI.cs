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

        public SkillTreeNodeViewModel ViewModel { get; private set; }

        public void Bind(SkillTreeNodeViewModel viewModel, Action<SkillTreeNodeViewModel> onClicked)
        {
            gameObject.name = $"SkillTreeNode_{viewModel.DisplayName}";
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClicked?.Invoke(ViewModel));
            }

            if(displayNameLabel != null) displayNameLabel.text = viewModel.DisplayName;
            Refresh(viewModel);

            if(viewModel.Source is UpgradeDefinition)
            {
                currencyIcon.sprite = UpgradeManager.Instance.CurrencyIcon;
            }
            else
            {
                currencyIcon.sprite = PrestigeUpgradeManager.Instance.CurrencyIcon;
            }
        }

        public void Refresh(SkillTreeNodeViewModel viewModel)
        {
            ViewModel = viewModel;
            if (viewModel == null) return;

            if (icon != null) icon.sprite = viewModel.Icon;
            if (levelBadge != null) levelBadge.text = $"{viewModel.Level}/{viewModel.MaxLevel}";
            if (border != null)
            {
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
            }

            costLabel.gameObject.SetActive(!viewModel.IsMaxed);
            currencyIcon.gameObject.SetActive(!viewModel.IsMaxed);

            costLabel.color = viewModel.CanPurchase ? affordableColor : unaffordableColor;
            costLabel.text = viewModel.CostLabel;
        }
    }
}
