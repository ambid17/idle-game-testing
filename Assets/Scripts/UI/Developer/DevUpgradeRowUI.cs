using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Generic "label + one button" row shared by DevPanelProgressionTab (upgrades/prestige
    // upgrades) and DevPanelTimeTutorialTab (tutorial replay list).
    public class DevUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button actionButton;

        public void Bind(string displayName, Action onActionClicked)
        {
            if (nameLabel != null) nameLabel.text = displayName;
            if (actionButton != null) actionButton.onClick.AddListener(() => onActionClicked());
        }
    }
}
