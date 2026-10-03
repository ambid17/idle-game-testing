using System;
using Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // "Prestige Now" confirm sub-modal for MuseumUI: a destructive, irreversible action shouldn't
    // be one accidental click away. Extracted into its own component (rather than staying inline
    // in MuseumUI) so it can share ModalBase with the other nested modals - Escape closes just
    // this confirm, not the whole Museum panel, while it's open.
    //
    // Also keeps a new player from resetting for nothing: the first Resonance is refused until at
    // least one Attunement is queued, and later empty-queue Resonances are allowed (saving up for
    // an expensive perk is legitimate) but warned about when something was affordable.
    public class MuseumPrestigeConfirmUI : ModalBase
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private TMP_Text noButtonLabel;

        private const string NoText = "No";
        private const string BackText = "Back";
        private const string WarningColor = "#FFC94D";
        private const string FirstResonanceNeedsQueueText = "Your first Resonance should carry something through it. Queue at least one Attunement in the Perk Tree first.";
        private const string FirstResonanceNeedsArtifactsText = "Your first Resonance should carry something through it. Find more artifacts, then queue an Attunement in the Perk Tree.";

        private Action onConfirm;

        // Runs on the first Show (the root starts inactive), after Show has already used the
        // serialized refs - so the null checks here only report, nothing below depends on them.
        private void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null) noButton.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
            if (messageLabel == null) Debug.LogError("MuseumPrestigeConfirmUI.messageLabel is not assigned.");
            if (yesButton == null) Debug.LogError("MuseumPrestigeConfirmUI.yesButton is not assigned.");
            if (noButton == null) Debug.LogError("MuseumPrestigeConfirmUI.noButton is not assigned.");
            if (noButtonLabel == null) Debug.LogError("MuseumPrestigeConfirmUI.noButtonLabel is not assigned.");
        }

        public void Initialize(Action onConfirm) => this.onConfirm = onConfirm;

        public void Show()
        {
            var upgrades = PrestigeUpgradeManager.Instance;
            bool blocked = PrestigeManager.Instance.PrestigeCount == 0 && !upgrades.HasQueuedUpgrades;

            // Set before the root activates so GamepadFocus doesn't land on a hidden Yes.
            yesButton.gameObject.SetActive(!blocked);
            noButtonLabel.text = blocked ? BackText : NoText;

            if (blocked)
            {
                messageLabel.text = upgrades.AffordableUpgradeCount > 0 ? FirstResonanceNeedsQueueText : FirstResonanceNeedsArtifactsText;
            }
            else
            {
                // The warning grows with the story (Story.StoryManager).
                messageLabel.text = GameManager.StoryManager.ResonancePrompt() + SavingsNote(upgrades);
            }

            if (root != null) root.SetActive(true);
            SetOpened();
        }

        // Empty-queue warning (only when something was affordable - otherwise saving is the only
        // option) plus how many unspent artifacts carry over.
        private static string SavingsNote(PrestigeUpgradeManager upgrades)
        {
            string note = "";

            int affordable = upgrades.AffordableUpgradeCount;
            if (!upgrades.HasQueuedUpgrades && affordable > 0)
            {
                string perks = affordable == 1 ? "1 Attunement" : $"{affordable} Attunements";
                note += $"\n\n<color={WarningColor}>Nothing queued - you can afford {perks}.</color>";
            }

            int artifacts = Wallet.Instance.ArtifactCount;
            if (artifacts > 0)
            {
                note += note.Length == 0 ? "\n\n" : "\n";
                note += artifacts == 1 ? "Your 1 unspent artifact carries over." : $"Your {artifacts} unspent artifacts carry over.";
            }

            return note;
        }

        public override void Close()
        {
            if (root != null) root.SetActive(false);
            SetClosed();
        }

        private void OnYesClicked()
        {
            onConfirm?.Invoke();
            Close();
        }
    }
}
