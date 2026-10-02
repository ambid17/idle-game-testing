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
        ManualRespawn,
        DartTrap,
        Crusher
    }

    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        // Emergency Shielding's level-1 recharge time; higher levels shorten it (see ShieldRegenSeconds).
        [SerializeField] private float shieldBaseRegenSeconds = 60f;
        [SerializeField] private float shieldMinRegenSeconds = 5f;

        // Base maxHp plus the market's Core Integrity flat bonus.
        public float MaxHp => maxHp + UpgradeManager.Instance.Movement_CoreIntegrityMaxHpBonus;
        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }

        // GameDesignDoc "Prestige > Survival": a single shield charge that regenerates over time
        // and fully absorbs a hit instead of it reducing CurrentHp.
        public int CurrentShieldCharges { get; private set; }
        private float shieldRegenTimer;
        private int MaxShieldCharges => PrestigeUpgradeManager.Instance != null && PrestigeUpgradeManager.Instance.Survival_ShieldUnlocked ? 1 : 0;
        private float ShieldRegenSeconds => Mathf.Max(shieldMinRegenSeconds, shieldBaseRegenSeconds - PrestigeUpgradeManager.Instance.Survival_ShieldRegenReductionSeconds);

        private void Awake()
        {
            CurrentHp = MaxHp;
            CurrentShieldCharges = MaxShieldCharges;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
            GameManager.EventService.Add<UpgradePurchasedEvent>(HandleUpgradePurchased);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(HandleUpgradePurchased);
        }

        // Buying a Core Integrity level grants its added max HP immediately, so the purchase reads
        // as a gain rather than as a newly-missing chunk of the health bar.
        private void HandleUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (evt.Definition == null || evt.Definition.Effect != UpgradeEffect.Movement_CoreIntegrity) return;
            AddHp(evt.Definition.EffectValuePerLevel);
        }

        private void Update()
        {
            if (IsDead) return;
            HandleShieldRegen();
        }

        private void HandleShieldRegen()
        {
            if (CurrentShieldCharges >= MaxShieldCharges) return;
            shieldRegenTimer += Time.deltaTime;
            if (shieldRegenTimer < ShieldRegenSeconds) return;
            shieldRegenTimer = 0f;
            CurrentShieldCharges++;
            GameManager.EventService.Dispatch(new ShieldChargeChangedEvent(CurrentShieldCharges, MaxShieldCharges));
        }

        public void TakeDamage(float amount, DeathReason reason)
        {
            // Market "Core Stability": reduces all incoming damage by a percentage, as does the
            // story's Reseal ending.
            amount *= UpgradeManager.Instance.Movement_CoreStabilityDamageMultiplier * GameManager.StoryManager.DamageTakenMultiplier;
            if (amount <= 0f || IsDead) return;

            if (CurrentShieldCharges > 0)
            {
                CurrentShieldCharges--;
                shieldRegenTimer = 0f;
                GameManager.EventService.Dispatch(new ShieldChargeChangedEvent(CurrentShieldCharges, MaxShieldCharges));
                GameManager.EventService.Dispatch(new NotificationEvent("Shield absorbed the hit!", NotificationUrgency.TimeSensitive));
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
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
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
            CurrentHp = MaxHp;
            CurrentShieldCharges = MaxShieldCharges;
            shieldRegenTimer = 0f;
        }

        // Restore for SaveService - always resolves alive (resuming into a dead state on load is
        // an unwanted edge case, not a design goal), flooring a saved 0 HP up to maxHp rather than
        // reloading instantly dead.
        public void RestoreFromSaveData(float currentHp)
        {
            IsDead = false;
            CurrentHp = currentHp > 0f ? Mathf.Min(currentHp, MaxHp) : MaxHp;
        }
    }
}
