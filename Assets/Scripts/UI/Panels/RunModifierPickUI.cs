using System.Collections.Generic;
using Economy;
using RunModifiers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Second step of "Prestige Now" (after MuseumPrestigeConfirmUI): the player picks the next
    // run's modifier from PrestigeManager.PendingOffers - picking one executes the prestige. A
    // nested modal like the confirm, so Escape backs out of just this step.
    public class RunModifierPickUI : ModalBase
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform cardContainer;
        [Tooltip("Inactive template card - cloned once per offer.")]
        [SerializeField] private RunModifierOfferCardUI cardTemplate;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollLabel;
        [SerializeField] private Button backButton;

        private readonly List<RunModifierOfferCardUI> cards = new();

        private void Awake()
        {
            if (root == null) Debug.LogError("RunModifierPickUI: root is not assigned.");
            if (cardContainer == null) Debug.LogError("RunModifierPickUI: cardContainer is not assigned.");
            if (cardTemplate == null) Debug.LogError("RunModifierPickUI: cardTemplate is not assigned.");
            if (rerollButton == null) Debug.LogError("RunModifierPickUI: rerollButton is not assigned.");
            if (rerollLabel == null) Debug.LogError("RunModifierPickUI: rerollLabel is not assigned.");
            if (backButton == null) Debug.LogError("RunModifierPickUI: backButton is not assigned.");

            rerollButton.onClick.AddListener(OnRerollClicked);
            backButton.onClick.AddListener(Close);
            cardTemplate.gameObject.SetActive(false);
            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            SetOpened();
            Rebuild();
        }

        public override void Close()
        {
            if (root != null) root.SetActive(false);
            SetClosed();
        }

        private void Rebuild()
        {
            // Deactivate as well - Destroy only lands at end of frame, and a reroll rebuilds mid-frame.
            foreach (var card in cards)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            cards.Clear();

            foreach (var offer in PrestigeManager.Instance.PendingOffers())
            {
                var def = GameManager.RunModifierDatabase.Get(offer.ModifierId);
                if (def == null)
                {
                    Debug.LogError($"RunModifierPickUI: offered modifier '{offer.ModifierId}' is not in the RunModifierDatabase.");
                    continue;
                }

                var card = Instantiate(cardTemplate, cardContainer);
                card.gameObject.SetActive(true);
                card.Bind(def, offer, OnPicked);
                cards.Add(card);
            }

            int rerolls = PrestigeManager.Instance.RerollsRemaining;
            // Hidden until the Museum reroll perk is owned at all; greyed out once this prestige's rerolls are spent.
            rerollButton.gameObject.SetActive(PrestigeUpgradeManager.Instance.Prestige_RunModifierRerolls > 0);
            rerollButton.interactable = rerolls > 0;
            rerollLabel.text = $"Reroll ({rerolls})";
        }

        private void OnRerollClicked()
        {
            if (PrestigeManager.Instance.TryRerollOffers()) Rebuild();
        }

        private void OnPicked(RunModifierState offer)
        {
            Close();
            PrestigeManager.Instance.ExecutePrestige(offer);
        }
    }
}
