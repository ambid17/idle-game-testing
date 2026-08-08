using Events;
using UnityEngine;

namespace Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;

        public float MaxHp => maxHp;
        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake() => CurrentHp = maxHp;

        private void OnEnable() => GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
        private void OnDisable() => GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            if (CurrentHp <= 0f) Kill();
        }

        // Also called directly when fuel runs out (PlayerController.UpdateFuel) - fuel and HP are
        // independent lose conditions per GameDesignDoc, both funnel into the same death event.
        public void Kill()
        {
            if (IsDead) return;
            IsDead = true;
            CurrentHp = 0f;
            GameManager.EventService.Dispatch<PlayerDiedEvent>();
        }

        private void HandleRevived()
        {
            IsDead = false;
            CurrentHp = maxHp;
        }
    }
}
