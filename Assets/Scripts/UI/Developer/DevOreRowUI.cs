using System;
using MapGeneration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Row for DevPanelResourceTab's per-ore "Give" list - mirrors DevUpgradeRowUI's shape but
    // binds a BlockType (plus icon) and passes it back through the callback, since the caller
    // needs to know which ore type was clicked.
    public class DevOreRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button giveButton;

        public void Bind(BlockType blockType, Action<BlockType> onGiveClicked)
        {
            if (icon != null) icon.sprite = blockType.Icon;
            if (nameLabel != null) nameLabel.text = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
            if (giveButton != null) giveButton.onClick.AddListener(() => onGiveClicked(blockType));
        }
    }
}
