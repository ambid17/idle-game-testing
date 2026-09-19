using System.Globalization;
using Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: free-text "give" cheats for the two currencies. Wallet.Add/PrestigePoints.Add
    // already dispatch DollarsChangedEvent/PrestigePointsChangedEvent themselves, so no extra
    // event dispatch is needed here.
    public class DevPanelEconomyTab : MonoBehaviour
    {
        [SerializeField] private Button giveMoneyButton;
        [SerializeField] private Button givePrestigePointsButton;

        private void Start()
        {
            if (giveMoneyButton == null) Debug.LogError("DevPanelEconomyTab.giveMoneyButton is not assigned.");
            if (givePrestigePointsButton == null) Debug.LogError("DevPanelEconomyTab.givePrestigePointsButton is not assigned.");

            if (giveMoneyButton != null) giveMoneyButton.onClick.AddListener(OnGiveMoneyClicked);
            if (givePrestigePointsButton != null) givePrestigePointsButton.onClick.AddListener(OnGivePrestigePointsClicked);
        }

        private void OnGiveMoneyClicked()
        {
            Wallet.Instance.Add(100_000);
        }

        private void OnGivePrestigePointsClicked()
        {
            PrestigePoints.Instance.Add(10_000);
        }
    }
}
