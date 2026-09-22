using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Progression tab row: label showing the current level plus Max/+1/-1 controls. Kept separate
    // from DevUpgradeRowUI (shared by the Tutorial replay list, which has no notion of "level") so
    // that shared prefab doesn't grow controls only the Market/Prestige upgrade lists need.
    public class DevProgressionUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button maxButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Button decrementButton;

        private string displayName;
        private Func<int> getLevel;

        public void Bind(string displayName, Func<int> getLevel, Action onMaxClicked, Action onIncrementClicked, Action onDecrementClicked)
        {
            this.displayName = displayName;
            this.getLevel = getLevel;

            if (maxButton != null) maxButton.onClick.AddListener(() => { onMaxClicked(); Refresh(); });
            if (incrementButton != null) incrementButton.onClick.AddListener(() => { onIncrementClicked(); Refresh(); });
            if (decrementButton != null) decrementButton.onClick.AddListener(() => { onDecrementClicked(); Refresh(); });

            Refresh();
        }

        public void Refresh()
        {
            if (nameLabel == null || getLevel == null) return;
            nameLabel.text = $"{displayName} (Lv {getLevel()})";
        }
    }
}
