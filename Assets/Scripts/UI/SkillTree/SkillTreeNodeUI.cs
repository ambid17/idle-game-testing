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

        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color partialColor = Color.green;
        [SerializeField] private Color maxedColor = new Color(1f, 0.84f, 0f);

        public SkillTreeNodeViewModel ViewModel { get; private set; }

        public void Bind(SkillTreeNodeViewModel viewModel, Action<SkillTreeNodeViewModel> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClicked?.Invoke(ViewModel));
            }
            Refresh(viewModel);
        }

        public void Refresh(SkillTreeNodeViewModel viewModel)
        {
            ViewModel = viewModel;
            if (viewModel == null) return;

            if (icon != null) icon.sprite = viewModel.Icon;
            if (levelBadge != null) levelBadge.text = $"{viewModel.Level}/{viewModel.MaxLevel}";
            if (border != null)
            {
                border.color = !viewModel.IsUnlocked ? lockedColor
                    : viewModel.IsMaxed ? maxedColor
                    : viewModel.Level > 0 ? partialColor
                    : unlockedColor;
            }
        }
    }
}
