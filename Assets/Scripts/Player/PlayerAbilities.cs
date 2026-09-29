using System.Collections.Generic;
using Events;
using Settings;
using UnityEngine;

namespace Player
{
    // Owns the player's active-ability input: UseAbility (Q) fires the selected ability,
    // CycleAbility (Tab) moves the selection through the unlocked ones. Abilities are the
    // PlayerAbility components on this GameObject, in component order. The selection is kept as a
    // reference rather than an index so unlocking a new ability doesn't shift it.
    // UI.AbilityHudUI reads Current/Previous/Next every frame.
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerAbilities : MonoBehaviour
    {
        private readonly List<PlayerAbility> unlocked = new();
        private PlayerAbility[] abilities;
        private PlayerHealth playerHealth;
        private PlayerAbility selected;

        public int UnlockedCount => unlocked.Count;
        public PlayerAbility Current => selected;
        public PlayerAbility Previous => AtOffset(-1);
        public PlayerAbility Next => AtOffset(1);

        private void Awake()
        {
            abilities = GetComponents<PlayerAbility>();
            playerHealth = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            RefreshUnlocked();

            if (playerHealth.IsDead || InputBlocker.IsBlocked || selected == null) return;

            var keybinds = GameManager.KeybindService;
            if (keybinds.WasPressedThisFrame(GameAction.CycleAbility))
            {
                selected = AtOffset(1);
            }

            if (keybinds.WasPressedThisFrame(GameAction.UseAbility) && !selected.TryActivate() && !selected.IsReady)
            {
                int seconds = Mathf.CeilToInt(selected.CooldownRemaining);
                GameManager.EventService.Dispatch(new NotificationEvent($"{selected.DisplayName} ready in {seconds / 60}:{seconds % 60:00}", NotificationUrgency.TimeSensitive));
            }
        }

        // Re-derived every frame (a handful of components) so purchases, prestige commits and save
        // loads all show up without listening for each of their events.
        private void RefreshUnlocked()
        {
            unlocked.Clear();
            foreach (var ability in abilities)
            {
                if (ability.IsUnlocked) unlocked.Add(ability);
            }

            if (selected == null || !unlocked.Contains(selected))
            {
                selected = unlocked.Count > 0 ? unlocked[0] : null;
            }
        }

        private PlayerAbility AtOffset(int offset)
        {
            if (selected == null) return null;
            int count = unlocked.Count;
            return unlocked[((unlocked.IndexOf(selected) + offset) % count + count) % count];
        }
    }
}
