using UnityEngine;

namespace UI
{
    // Tracks whether any modal nested inside a panel (ProcessingRecipeListModalUI,
    // MuseumPrestigeConfirmUI) or standalone (TutorialModalUI,
    // WorldTutorialPopupUI) is currently open, so PlayerController.Update can close just the
    // modal on Escape before it closes the panel underneath. Ref-counted like
    // Player.InputBlocker, in case more than one modal is ever open at once.
    public static class ModalTracker
    {
        private static int openCount;

        public static bool IsAnyModalOpen => openCount > 0;

        public static void SetOpen(bool open) =>
            openCount = open ? openCount + 1 : Mathf.Max(0, openCount - 1);
    }
}
