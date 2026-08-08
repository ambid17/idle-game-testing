using MapGeneration;
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

        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private Rigidbody2D rb;
        private CapsuleCollider2D capsuleCollider;
        private PlayerHealth health;
        private bool wasGrounded;
        private float peakFallSpeed;

        public bool IsGrounded { get; private set; }
        public bool IsFlying { get; private set; }
        public float Fuel { get; private set; }
        public float FuelFraction => fuelMax > 0f ? Fuel / fuelMax : 0f;
        private Vector2 movementInput;
        public Vector2 MovementInput => movementInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.WakeUp();
            health = GetComponent<PlayerHealth>();
            Fuel = fuelMax;

            capsuleCollider = GetComponent<CapsuleCollider2D>();
            groundCheckOffset = capsuleCollider.size.y * 0.5f * Vector2.down;
            groundCheckSize = new Vector2(capsuleCollider.size.x * 0.5f, 0.1f);

        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Debug.LogError("no keyboard found");
                return;
            }

            bool wHeld = keyboard.wKey.isPressed;
            bool aHeld = keyboard.aKey.isPressed;
            bool dHeld = keyboard.dKey.isPressed;

            float horizontalInput = 0f;
            if (aHeld) horizontalInput -= 1f;
            if (dHeld) horizontalInput += 1f;
            movementInput = new Vector2(horizontalInput, wHeld ? 1f : 0f);

        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            IsFlying = movementInput.y > 0 && Fuel > 0f;

            float horizontalSpeed = IsFlying ? flySpeed : groundSpeed;
            float verticalVelocity = IsFlying ? jetpackLiftSpeed : rb.linearVelocity.y;
            float horizontalVelocity = ClampHorizontalVelocity(movementInput.x * horizontalSpeed);
            rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);

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

        // Keeps the player's collider within the mine's horizontal extent. Read live off
        // World.GridWidth rather than cached, since the grid-width upgrade widens it over time.
        private float ClampHorizontalVelocity(float horizontalVelocity)
        {
            float halfWidth = capsuleCollider.size.x * 0.5f;
            float minX = halfWidth;
            float maxX = mapGenerationService.World.GridWidth * mapGenerationService.CellSize - halfWidth;

            float predictedX = rb.position.x + horizontalVelocity * Time.fixedDeltaTime;
            if (predictedX < minX && horizontalVelocity < 0f) return 0f;
            if (predictedX > maxX && horizontalVelocity > 0f) return 0f;
            return horizontalVelocity;
        }

        private bool CheckGrounded()
        {
            Vector2 origin = (Vector2)transform.position + groundCheckOffset;
            var collided = Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayer);
            Debug.Log($"Ground check collided with {collided?.gameObject.name ?? "nothing"}");
            return collided != null;
        }

        // Draw ground check
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + groundCheckOffset, groundCheckSize);
        }
    }
}
