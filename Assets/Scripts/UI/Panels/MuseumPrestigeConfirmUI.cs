using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // "Prestige Now" confirm sub-modal for MuseumUI: a destructive, irreversible action shouldn't
    // be one accidental click away. Extracted into its own component (rather than staying inline
    // in MuseumUI) so it can share ModalBase with the other nested modals - Escape closes just
    // this confirm, not the whole Museum panel, while it's open.
    public class MuseumPrestigeConfirmUI : ModalBase
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onConfirm;

        private void Awake()
        {
            if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null) noButton.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
        }

        public void Initialize(Action onConfirm) => this.onConfirm = onConfirm;

        public void Show()
        {
            if (root != null) root.SetActive(true);
            SetOpened();
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
