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
        // Optional - only HudInventoryRow has one, for HudInventoryUI's "+N" pickup tally.
        [SerializeField] private TMP_Text gainLabel;
        // Optional - only DepotRow has these, for the padlock that keeps an ore out of every sale.
        [SerializeField] private Button lockButton;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;

        private static readonly Color UnlockedIconColor = new(1f, 1f, 1f, 0.45f);
        private const float LockedValueAlpha = 0.4f;

        private BlockType blockType;
        // Optional - only present on prefab variants that want the count/value to tick towards
        // new numbers instead of snapping. Null is a valid "not wired up" state, not an error.
        private AnimatedCounter countAnimator;
        private AnimatedCounter valueAnimator;

        private void Start()
        {
            if(icon == null) Debug.LogError($"OreRowUI.icon is not assigned on {gameObject.name}.");
            if (nameLabel == null) Debug.LogError($"OreRowUI.nameLabel is not assigned on {gameObject.name}.");
            if (countLabel == null) Debug.LogError($"OreRowUI.countLabel is not assigned on {gameObject.name}.");
            if (valueLabel == null) Debug.LogError($"OreRowUI.valueLabel is not assigned on {gameObject.name}.");

            countAnimator = countLabel != null ? countLabel.GetComponent<AnimatedCounter>() : null;
            valueAnimator = valueLabel != null ? valueLabel.GetComponent<AnimatedCounter>() : null;
            valueAnimator?.SetFormatter(v => $"${v:0}");
        }

        public void Bind(BlockType blockType)
        {
            this.blockType = blockType;
            nameLabel.text = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
            icon.SetIcon(blockType.Icon, blockType.IconBackground);
        }

        public float SetCount(int count, bool instant = false)
        {
            if (countAnimator != null) countAnimator.SetValue(count, instant);
            else countLabel.text = count.ToString();

            var blockValue = blockType.Value;
            var totalValue = blockValue * UpgradeManager.Instance.Economy_SellValueMultiplier(blockType) * GameManager.RunModifierService.SellValueMultiplier(blockType.Id) * count;
            if (valueAnimator != null) valueAnimator.SetValue(totalValue, instant);
            else valueLabel.text = $"${totalValue:0}";

            return totalValue;
        }

        // The padlock ships inactive on the prefab since MinerDashboardUI/OfflineEarningsUI share
        // DepotRow and have nothing to sell - only DepotUI opts in.
        public void EnableSellLock()
        {
            if (lockButton == null || lockIcon == null || lockedSprite == null || unlockedSprite == null)
            {
                Debug.LogError($"OreRowUI: sell lock references are not assigned on {gameObject.name}.");
                return;
            }

            lockButton.gameObject.SetActive(true);
            lockButton.onClick.AddListener(() => GameManager.EventService.Dispatch(new SellLockToggleRequestedEvent(blockType.Id)));
        }

        // Locked rows show the closed padlock and a dimmed value, since that value is no longer
        // part of any sale.
        public void SetSellLocked(bool locked)
        {
            lockIcon.sprite = locked ? lockedSprite : unlockedSprite;
            lockIcon.color = locked ? Color.white : UnlockedIconColor;
            valueLabel.alpha = locked ? LockedValueAlpha : 1f;
        }

        // Shows "+amount" beside the count at the given opacity; amount 0 hides it so the row's
        // layout collapses back to just icon + count.
        public void SetGain(int amount, float alpha)
        {
            if (gainLabel == null) return;

            gainLabel.gameObject.SetActive(amount > 0);
            gainLabel.text = $"+{amount}";
            gainLabel.alpha = alpha;
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
