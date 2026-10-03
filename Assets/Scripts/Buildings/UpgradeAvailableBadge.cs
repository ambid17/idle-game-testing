using System.Linq;
using Economy;
using UnityEngine;

namespace Buildings
{
    // A bobbing arrow over the Market or Museum while at least one of its upgrades can be bought
    // right now, so the player knows a trip back is worth it. Polled a few times a second (the
    // affordability of every upgrade depends on the wallet, the levels and the prerequisites, so
    // there's no single event to listen to). Scene-placed child of the building - when the
    // building is hidden (the Museum before its reveal), so is this. Pops in when it appears.
    public class UpgradeAvailableBadge : MonoBehaviour
    {
        private enum Shop { Market, Museum }

        [SerializeField] private Shop shop;
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private float pollSeconds = 0.5f;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float popSeconds = 0.3f;

        private Vector3 iconBasePosition;
        private Vector3 iconBaseScale;
        private float nextPollTime;
        private bool available;
        private float shownAt;

        private void Awake()
        {
            if (icon == null) Debug.LogError($"{nameof(UpgradeAvailableBadge)} on {name} is missing its icon reference.");
            iconBasePosition = icon.transform.localPosition;
            iconBaseScale = icon.transform.localScale;
            icon.enabled = false;
        }

        private void OnEnable() => nextPollTime = 0f;

        private void Update()
        {
            if (Time.unscaledTime >= nextPollTime)
            {
                nextPollTime = Time.unscaledTime + pollSeconds;
                bool now = AnyAffordable();
                if (now && !available) shownAt = Time.unscaledTime;
                available = now;
                icon.enabled = available;
            }
            if (!available) return;

            float age = Time.unscaledTime - shownAt;
            float pop = age < popSeconds ? Effects.Easing.OutBack(age / popSeconds) : 1f;
            icon.transform.localScale = iconBaseScale * pop;
            icon.transform.localPosition = iconBasePosition + Vector3.up * (bobHeight * Mathf.Sin(Time.unscaledTime * bobSpeed));
        }

        private bool AnyAffordable()
        {
            return shop == Shop.Market
                ? GameManager.UpgradeDatabase.Upgrades.Any(def => def != null && UpgradeManager.Instance.CanPurchase(def))
                : GameManager.PrestigeUpgradeDatabase.Upgrades.Any(def => def != null && PrestigeUpgradeManager.Instance.CanPurchase(def));
        }
    }
}
