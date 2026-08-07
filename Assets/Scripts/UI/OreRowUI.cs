using System;
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
        [SerializeField] private Button sellHalfButton;
        [SerializeField] private Button sellAllButton;

        public BlockTypeId BlockTypeId { get; private set; }

        // (id, fraction 0-1)
        public event Action<BlockTypeId, float> SellRequested;

        public void Bind(BlockTypeId id, string displayName)
        {
            BlockTypeId = id;
            if (nameLabel != null) nameLabel.text = displayName;
            if (sellHalfButton != null) sellHalfButton.onClick.AddListener(() => SellRequested?.Invoke(BlockTypeId, 0.5f));
            if (sellAllButton != null) sellAllButton.onClick.AddListener(() => SellRequested?.Invoke(BlockTypeId, 1f));
        }

        public void SetCount(int count)
        {
            if (countLabel != null) countLabel.text = count.ToString();
        }

        public void SetValue(float value)
        {
            if (valueLabel != null) valueLabel.text = $"${value:0.##}";
        }
    }
}
