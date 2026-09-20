using Economy;
using Events;
using MapGeneration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One row of an ore listing, shared by InventoryUI (name + count only) and DepotUI
    // (name + count + value + sell buttons). Fields left unassigned on a given prefab variant
    // are simply skipped. Sell fractions are fixed presets (half/all) rather than a free slider -
    // still covers GameDesignDoc's "sell any percentage... or all of them" without the extra
    // Slider sub-hierarchy.
    public class OreRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text valueLabel;

        private BlockType blockType;

        private void Start()
        {
            if(icon == null) Debug.LogError($"OreRowUI.icon is not assigned on {gameObject.name}.");
            if (nameLabel == null) Debug.LogError($"OreRowUI.nameLabel is not assigned on {gameObject.name}.");
            if (countLabel == null) Debug.LogError($"OreRowUI.countLabel is not assigned on {gameObject.name}.");
            if (valueLabel == null) Debug.LogError($"OreRowUI.valueLabel is not assigned on {gameObject.name}.");
        }

        public void Bind(BlockType blockType)
        {
            this.blockType = blockType;
            nameLabel.text = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
            icon.sprite = blockType.Icon;
        }

        public float SetCount(int count)
        {
            countLabel.text = count.ToString();

            var blockValue = blockType.Value;
            var totalValue = blockValue * UpgradeManager.Instance.SellValueMultiplier * count;
            valueLabel.text = $"${totalValue:0}";

            return totalValue;
        }

        // Used by MinerDashboardUI's ore/min table - same row prefab as DepotUI/InventoryUI, just
        // fed a rate instead of a count.
        public void SetRate(float perMinute)
        {
            if (countLabel != null) countLabel.text = $"{perMinute:0.#}/min";

            var blockValue = blockType.Value;
            var totalValue = blockValue * perMinute;
            if (valueLabel != null) valueLabel.text = $"${totalValue:0}/min";
        }
    }
}
