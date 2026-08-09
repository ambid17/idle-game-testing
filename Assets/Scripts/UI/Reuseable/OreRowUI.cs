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
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text valueLabel;

        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        public BlockTypeId BlockTypeId { get; private set; }

        public void Bind(BlockTypeId id, string displayName)
        {
            BlockTypeId = id;
            nameLabel.text = displayName;
        }

        public void SetCount(int count)
        {
            countLabel.text = count.ToString();

            var blockValue = blockTypeDatabase.Get((byte)BlockTypeId)?.Value ?? 0;
            var totalValue = blockValue * count;
            valueLabel.text = $"${totalValue:0.##}";
        }

        public void SetValue(float value)
        {
            if (valueLabel != null) valueLabel.text = $"${value:0.##}";
        }
    }
}
