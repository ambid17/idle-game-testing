using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One ability icon on the HUD's AbilityHudUI strip, with a MOBA-style radial cooldown sweep:
    // cooldownOverlay is a Filled/Radial360 image (counter-clockwise from the top) whose fillAmount
    // is the remaining cooldown fraction, so the icon is revealed clockwise as it recharges.
    // Side slots (previous/next ability) are drawn dimmed and without the countdown text.
    public class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownLabel;
        [SerializeField] private Color dimmedColor = new(0.5f, 0.5f, 0.5f, 0.75f);

        private int shownSeconds = -1;

        private void Start()
        {
            if (icon == null) Debug.LogError($"{nameof(AbilitySlotUI)} on {name} is missing icon.");
            if (cooldownOverlay == null) Debug.LogError($"{nameof(AbilitySlotUI)} on {name} is missing cooldownOverlay.");
        }

        public void Show(PlayerAbility ability, bool dimmed)
        {
            if (ability == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            icon.sprite = ability.Icon;
            icon.color = dimmed ? dimmedColor : Color.white;
            cooldownOverlay.fillAmount = ability.CooldownFraction;

            if (cooldownLabel == null) return;

            int seconds = dimmed || ability.IsReady ? 0 : Mathf.CeilToInt(ability.CooldownRemaining);
            if (seconds == shownSeconds) return;

            shownSeconds = seconds;
            cooldownLabel.text = seconds <= 0 ? "" : seconds >= 60 ? $"{seconds / 60}:{seconds % 60:00}" : seconds.ToString();
        }
    }
}
