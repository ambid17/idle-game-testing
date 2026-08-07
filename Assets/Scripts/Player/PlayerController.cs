using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // WASD movement + jetpack per GameDesignDoc "Mechanics": A/D move (mining direction is
    // resolved by PlayerMining, gated on IsGrounded exposed here), W flies using fuel at a
    // higher horizontal speed than grounded movement, and un-slowed falls deal fall damage.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float groundSpeed = 5f;
        [SerializeField] private float flySpeed = 8f;
        [SerializeField] private float jetpackLiftSpeed = 6f;

        [Header("Jetpack Fuel")]
        [SerializeField] private float fuelMax = 100f;
        [SerializeField] private float fuelDrainPerSecond = 20f;
        [SerializeField] private float fuelRegenPerSecondGrounded = 25f;

        [Header("Fall Damage")]
        [SerializeField] private float fallDamageVelocityThreshold = 12f;
        [SerializeField] private float fallDamagePerExcessUnit = 2f;

        [Header("Ground Check")]
        [SerializeField] private Vector2 groundCheckOffset = new(0f, -0.5f);
        [SerializeField] private Vector2 groundCheckSize = new(0.9f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D rb;
        private PlayerHealth health;
        private bool wasGrounded;
        private float peakFallSpeed;

        public bool IsGrounded { get; private set; }
        public bool IsFlying { get; private set; }
        public float Fuel { get; private set; }
        public float FuelFraction => fuelMax > 0f ? Fuel / fuelMax : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<PlayerHealth>();
            Fuel = fuelMax;
        }

        private void FixedUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Debug.LogError("no keyboard found");
                return;
            }
            IsGrounded = CheckGrounded();

            bool wHeld = keyboard.wKey.isPressed;
            bool aHeld = keyboard.aKey.isPressed;
            bool dHeld = keyboard.dKey.isPressed;

            IsFlying = wHeld && Fuel > 0f;

            float moveInput = 0f;
            if (aHeld) moveInput -= 1f;
            if (dHeld) moveInput += 1f;

            float horizontalSpeed = IsFlying ? flySpeed : groundSpeed;
            float verticalVelocity = IsFlying ? jetpackLiftSpeed : rb.linearVelocity.y;
            rb.linearVelocity = new Vector2(moveInput * horizontalSpeed, verticalVelocity);

            UpdateFuel(Time.fixedDeltaTime);
            TrackFallDamage();

            wasGrounded = IsGrounded;
        }

        private void UpdateFuel(float dt)
        {
            if (IsFlying)
            {
                Fuel = Mathf.Max(0f, Fuel - fuelDrainPerSecond * dt);
            }
            else if (IsGrounded)
            {
                Fuel = Mathf.Min(fuelMax, Fuel + fuelRegenPerSecondGrounded * dt);
            }
        }

        private void TrackFallDamage()
        {
            if (!IsGrounded)
            {
                peakFallSpeed = Mathf.Max(peakFallSpeed, -rb.linearVelocity.y);
                return;
            }

            if (!wasGrounded && peakFallSpeed > fallDamageVelocityThreshold && health != null)
            {
                health.TakeDamage((peakFallSpeed - fallDamageVelocityThreshold) * fallDamagePerExcessUnit);
            }

            peakFallSpeed = 0f;
        }

        private bool CheckGrounded()
        {
            Vector2 origin = (Vector2)transform.position + groundCheckOffset;
            return Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayer);
        }

        // Draw ground check
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + groundCheckOffset, groundCheckSize);
        }
    }
}
