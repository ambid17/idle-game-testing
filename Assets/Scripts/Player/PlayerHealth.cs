using Economy;
using Events;
using UnityEngine;

namespace Player
{
    // Surfaced by DeathUI to explain the death screen's cause-of-death line.
    public enum DeathReason
    {
        Unknown,
        OutOfFuel,
        FallDamage,
        Explosive,
        FallingRock,
        GasPocket,
        Lava,
        ManualRespawn
    }

    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float shieldRegenSeconds = 30f;

        public float MaxHp => maxHp;
        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }

        // GameDesignDoc "Prestige > Survival": one-time shield charges that regenerate over time
        // and fully absorb a hit instead of it reducing CurrentHp.
        public int CurrentShieldCharges { get; private set; }
        private float shieldRegenTimer;
        private int MaxShieldCharges => PrestigeUpgradeManager.Instance != null ? PrestigeUpgradeManager.Instance.ShieldChargeCount : 0;

        private void Awake()
        {
            CurrentHp = maxHp;
            CurrentShieldCharges = MaxShieldCharges;
        }

        private void OnEnable() => GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
        private void OnDisable() => GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);

        private void Update()
        {
            if (IsDead) return;
            HandleShieldRegen();
        }

        private void HandleShieldRegen()
        {
            if (CurrentShieldCharges >= MaxShieldCharges) return;
            shieldRegenTimer += Time.deltaTime;
            if (shieldRegenTimer < shieldRegenSeconds) return;
            shieldRegenTimer = 0f;
            CurrentShieldCharges++;
            GameManager.EventService.Dispatch(new ShieldChargeChangedEvent(CurrentShieldCharges, MaxShieldCharges));
        }

        public void TakeDamage(float amount, DeathReason reason)
        {
            if (amount <= 0f || IsDead) return;

            if (CurrentShieldCharges > 0)
            {
                CurrentShieldCharges--;
                shieldRegenTimer = 0f;
                GameManager.EventService.Dispatch(new ShieldChargeChangedEvent(CurrentShieldCharges, MaxShieldCharges));
                GameManager.EventService.Dispatch(new HudNotificationEvent("Shield absorbed the hit!"));
                return;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            GameManager.EventService.Dispatch(new PlayerDamagedEvent(amount));
            if (CurrentHp <= 0f) Kill(reason);
        }

        // Guards against IsDead so this can't double as a silent resurrection path outside
        // PlayerRevivedEvent/HandleRevived's ownership of that transition.
        public void AddHp(float amount)
        {
            if (amount <= 0f || IsDead) return;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        }

        // Also called directly when fuel runs out (PlayerController.UpdateFuel) - fuel and HP are
        // independent lose conditions per GameDesignDoc, both funnel into the same death event.
        public void Kill(DeathReason reason)
        {
            if (IsDead) return;
            IsDead = true;
            CurrentHp = 0f;
            GameManager.EventService.Dispatch(new PlayerDiedEvent(reason));
        }

        private void HandleRevived()
        {
            IsDead = false;
            CurrentHp = maxHp;
            CurrentShieldCharges = MaxShieldCharges;
            shieldRegenTimer = 0f;
        }

        // Restore for SaveService - always resolves alive (resuming into a dead state on load is
        // an unwanted edge case, not a design goal), flooring a saved 0 HP up to maxHp rather than
        // reloading instantly dead.
        public void RestoreFromSaveData(float currentHp)
        {
            IsDead = false;
            CurrentHp = currentHp > 0f ? Mathf.Min(currentHp, maxHp) : maxHp;
        }
    }
}
