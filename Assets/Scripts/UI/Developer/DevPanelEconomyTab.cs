using System.Globalization;
using Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: free-text "give" cheats for the two currencies. Wallet.Add/AddArtifacts already
    // dispatch DollarsChangedEvent/ArtifactCountChangedEvent themselves, so no extra event dispatch
    // is needed here.
    public class DevPanelEconomyTab : MonoBehaviour
    {
        [SerializeField] private Button giveMoneyButton;
        // FormerlySerializedAs preserves the existing scene wiring - this used to grant Prestige
        // Points before that currency was removed in favor of spending artifacts directly.
        [FormerlySerializedAs("givePrestigePointsButton")]
        [SerializeField] private Button giveArtifactsButton;

        private void Start()
        {
            if (giveMoneyButton == null) Debug.LogError("DevPanelEconomyTab.giveMoneyButton is not assigned.");
            if (giveArtifactsButton == null) Debug.LogError("DevPanelEconomyTab.giveArtifactsButton is not assigned.");

            if (giveMoneyButton != null) giveMoneyButton.onClick.AddListener(OnGiveMoneyClicked);
            if (giveArtifactsButton != null) giveArtifactsButton.onClick.AddListener(OnGiveArtifactsClicked);
        }

        private void OnGiveMoneyClicked()
        {
            Wallet.Instance.Add(100_000);
        }

        private void OnGiveArtifactsClicked()
        {
            Wallet.Instance.AddArtifacts(100);
        }
    }
}
