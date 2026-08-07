using UnityEngine;

namespace Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;

        public float MaxHp => maxHp;
        public float CurrentHp { get; private set; }

        private void Awake() => CurrentHp = maxHp;

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        }
    }
}
