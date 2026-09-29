using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One buff icon on the HUD's PowerUpBuffBarUI. drainOverlay is a Filled/Radial360 image (like
    // AbilitySlotUI's cooldown sweep) whose fillAmount is the spent fraction, so the overlay creeps
    // over the icon as the buff runs out. The label shows remaining seconds ("12s") or charges ("x7").
    public class PowerUpBuffSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image drainOverlay;
        [SerializeField] private TMP_Text remainingLabel;

        private int shownRemaining = -1;
        private PowerUpBuffUnit shownUnit;

        private void Start()
        {
            if (icon == null) Debug.LogError($"{nameof(PowerUpBuffSlotUI)} on {name} is missing icon.");
            if (drainOverlay == null) Debug.LogError($"{nameof(PowerUpBuffSlotUI)} on {name} is missing drainOverlay.");
            if (remainingLabel == null) Debug.LogError($"{nameof(PowerUpBuffSlotUI)} on {name} is missing remainingLabel.");
        }

        public void Show(PowerUpBuffStatus buff)
        {
            gameObject.SetActive(true);
            icon.sprite = buff.Icon;
            drainOverlay.fillAmount = 1f - Mathf.Clamp01(buff.RemainingFraction);

            if (buff.Remaining == shownRemaining && buff.Unit == shownUnit) return;

            shownRemaining = buff.Remaining;
            shownUnit = buff.Unit;
            remainingLabel.text = buff.Unit == PowerUpBuffUnit.Seconds ? $"{buff.Remaining}s" : $"x{buff.Remaining}";
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
