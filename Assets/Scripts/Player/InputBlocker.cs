using UnityEngine;

namespace Player
{
    // Full-screen modals (OfflineEarningsUI, ControlCenterUI) set this while open; PlayerController
    // and PlayerMining check IsBlocked alongside their existing PlayerHealth.IsDead gate so the
    // player can't move/mine underneath an open modal. Plain static rather than a MonoBehaviour/
    // event since it's simple polled on/off state, matching how IsGrounded/IsFlying are already
    // plain polled properties rather than event-driven.
    public static class InputBlocker
    {
        private static int blockCount;

        public static bool IsBlocked => blockCount > 0;

        // Reference-counted so two modals opening/closing in overlapping order can't accidentally
        // unblock input while another is still open.
        public static void SetBlocked(bool blocked)
        {
            blockCount = blocked ? blockCount + 1 : Mathf.Max(0, blockCount - 1);
        }
    }
}
