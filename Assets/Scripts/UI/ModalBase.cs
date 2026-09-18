using Events;
using UnityEngine;

namespace UI
{
    // Base for any modal that Escape should be able to close on its own, one press before it
    // closes whatever panel is underneath (see PlayerController.Update and ModalTracker).
    // Derived classes call Opened() right after making their root visible and Closed() right
    // after hiding it (inside their own Close() override) - both are idempotent so a modal can
    // be re-Show()n while already open (e.g. SkillTreePanelUI.RebuildDetailModal) or Close()d
    // twice in one frame without double-counting ModalTracker.
    public abstract class ModalBase : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        protected virtual void OnEnable() =>
            GameManager.EventService.Add<ModalCloseRequestedEvent>(OnModalCloseRequested);

        protected virtual void OnDisable()
        {
            GameManager.EventService.Remove<ModalCloseRequestedEvent>(OnModalCloseRequested);
            // Safety net: decrements the tracker even if something hid this modal's root
            // without going through its own Close() (e.g. a parent panel deactivating).
            Closed();
        }

        private void OnModalCloseRequested()
        {
            if (IsOpen) Close();
        }

        public abstract void Close();

        protected void Opened()
        {
            if (IsOpen) return;
            IsOpen = true;
            ModalTracker.SetOpen(true);
        }

        protected void Closed()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ModalTracker.SetOpen(false);
        }
    }
}
