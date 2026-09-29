using System.Collections.Generic;
using Player;
using UnityEngine;

namespace UI
{
    // HUD row of the player's active lasting power-up buffs (PlayerPowerUps.GetActiveBuffs - Drill
    // Overdrive, Lucky Strike). Slots are a fixed pool laid out by the row's layout group; unused
    // ones are hidden, and the whole row hides while nothing is active. Polled every frame like
    // AbilityHudUI, since the timers change continuously.
    public class PowerUpBuffBarUI : MonoBehaviour
    {
        [SerializeField] private PlayerPowerUps playerPowerUps;
        [SerializeField] private GameObject content;
        [SerializeField] private PowerUpBuffSlotUI[] slots;

        private readonly List<PowerUpBuffStatus> activeBuffs = new();

        private void Start()
        {
            if (playerPowerUps == null) Debug.LogError($"{nameof(PowerUpBuffBarUI)} is missing playerPowerUps.");
            if (content == null) Debug.LogError($"{nameof(PowerUpBuffBarUI)} is missing content.");
            if (slots == null || slots.Length == 0) Debug.LogError($"{nameof(PowerUpBuffBarUI)} has no slots assigned.");
        }

        private void Update()
        {
            playerPowerUps.GetActiveBuffs(activeBuffs);
            content.SetActive(activeBuffs.Count > 0);
            if (activeBuffs.Count == 0) return;

            if (activeBuffs.Count > slots.Length) Debug.LogError($"{nameof(PowerUpBuffBarUI)} has {slots.Length} slots but {activeBuffs.Count} active buffs.");

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < activeBuffs.Count) slots[i].Show(activeBuffs[i]);
                else slots[i].Hide();
            }
        }
    }
}
