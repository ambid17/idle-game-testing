using UnityEngine;

namespace Player
{
    // Base for an active ability the player fires with UseAbility (Q by default). Lives on the
    // Player alongside PlayerAbilities, which owns input (use + cycling) and picks the current
    // ability; subclasses only say whether they're unlocked, how long their cooldown is, and what
    // happens on activation. UI.AbilityHudUI reads Icon/CooldownFraction every frame.
    public abstract class PlayerAbility : MonoBehaviour
    {
        private float readyTime;
        // Snapshotted at activation so an upgrade bought mid-cooldown doesn't make the radial
        // indicator jump.
        private float activeCooldownDuration;

        public abstract string DisplayName { get; }
        public abstract Sprite Icon { get; }
        public abstract bool IsUnlocked { get; }
        public abstract float CooldownSeconds { get; }

        public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);
        public bool IsReady => CooldownRemaining <= 0f;
        // 1 right after activation, 0 when ready.
        public float CooldownFraction => activeCooldownDuration > 0f ? Mathf.Clamp01(CooldownRemaining / activeCooldownDuration) : 0f;

        // Called by PlayerAbilities; starts the cooldown only if the ability actually fired.
        public bool TryActivate()
        {
            if (!IsReady || !Activate()) return false;

            activeCooldownDuration = CooldownSeconds;
            readyTime = Time.time + activeCooldownDuration;
            return true;
        }

        protected abstract bool Activate();
    }
}
