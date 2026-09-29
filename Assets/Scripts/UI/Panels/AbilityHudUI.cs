using Player;
using Settings;
using TMPro;
using UnityEngine;

namespace UI
{
    // HUD strip for the player's active abilities (PlayerAbilities): the current ability in the
    // middle with its cooldown sweep, and the previous/next ones dimmed on either side. With two
    // abilities only the "next" side is shown (previous would be the same one), and the whole strip
    // hides until at least one ability is unlocked. Polled every frame like HUDUI's bars, since
    // cooldowns change continuously.
    public class AbilityHudUI : MonoBehaviour
    {
        [SerializeField] private PlayerAbilities playerAbilities;
        [SerializeField] private GameObject content;
        [SerializeField] private AbilitySlotUI currentSlot;
        [SerializeField] private AbilitySlotUI previousSlot;
        [SerializeField] private AbilitySlotUI nextSlot;
        [SerializeField] private TMP_Text useKeyLabel;
        [SerializeField] private TMP_Text cycleKeyLabel;

        private void Start()
        {
            if (playerAbilities == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing playerAbilities.");
            if (content == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing content.");
            if (currentSlot == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing currentSlot.");
            if (previousSlot == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing previousSlot.");
            if (nextSlot == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing nextSlot.");
            if (useKeyLabel == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing useKeyLabel.");
            if (cycleKeyLabel == null) Debug.LogError($"{nameof(AbilityHudUI)} is missing cycleKeyLabel.");
        }

        private void Update()
        {
            int count = playerAbilities.UnlockedCount;
            content.SetActive(count > 0);
            if (count == 0) return;

            currentSlot.Show(playerAbilities.Current, dimmed: false);
            previousSlot.Show(count >= 3 ? playerAbilities.Previous : null, dimmed: true);
            nextSlot.Show(count >= 2 ? playerAbilities.Next : null, dimmed: true);

            // Polled so rebinds and controller/keyboard switches show up without extra events.
            var keybinds = GameManager.KeybindService;
            SetText(useKeyLabel, keybinds.GetDisplayName(GameAction.UseAbility));
            cycleKeyLabel.gameObject.SetActive(count >= 2);
            if (count >= 2) SetText(cycleKeyLabel, keybinds.GetDisplayName(GameAction.CycleAbility));
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }
    }
}
