using Events;
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
        private Vector3 spawnPosition;

        public bool IsGrounded { get; private set; }
        public bool IsFlying { get; private set; }
        public float Fuel { get; private set; }
        public float FuelFraction => fuelMax > 0f ? Fuel / fuelMax : 0f;
        public float FuelMax => fuelMax;
        public float FuelMissing => fuelMax - Fuel;

        // Used by Fuel Drones (Automation.FuelDrone) and RefuelingUI's manual purchase button -
        // both deposit fuel into the player through this rather than touching Fuel directly.
        public void AddFuel(float amount)
        {
            if (amount <= 0f) return;
            Fuel = Mathf.Min(fuelMax, Fuel + amount);
        }
        private Vector2 movementInput;
        public Vector2 MovementInput => movementInput;
        private Keyboard keyboard;


        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.WakeUp();
            health = GetComponent<PlayerHealth>();
            Fuel = fuelMax;
            spawnPosition = transform.position;

            capsuleCollider = GetComponent<CapsuleCollider2D>();
            groundCheckOffset = capsuleCollider.size.y * 0.5f * Vector2.down;
            groundCheckSize = new Vector2(capsuleCollider.size.x * 0.5f, 0.1f);
            keyboard = Keyboard.current;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Add<PlayerRevivedEvent>(HandleRevived);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<PlayerDiedEvent>(HandleDied);
            GameManager.EventService.Remove<PlayerRevivedEvent>(HandleRevived);
        }

        private void HandleDied()
        {
            movementInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }

        private void HandleRevived()
        {
            Fuel = fuelMax;
            peakFallSpeed = 0f;
            rb.linearVelocity = Vector2.zero;
            transform.position = spawnPosition;
        }

        // Restore for SaveService - restores last-quit Fuel/position. Deliberately does NOT touch
        // spawnPosition, which is the level's respawn-on-death anchor captured once in Awake() from
        // the scene's authored Player transform - a different concept from "where the player was
        // standing when they quit."
        public void RestoreFromSaveData(float fuel, Vector3 position)
        {
            Fuel = Mathf.Clamp(fuel, 0f, fuelMax);
            rb.position = position;
            transform.position = position;
            rb.linearVelocity = Vector2.zero;
            peakFallSpeed = 0f;
        }

        private void Update()
        {
            if (health.IsDead) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                GameManager.EventService.Dispatch<UICloseEvent>();
            }

            // Unlike death (which zeroes movementInput once via HandleDied), blocking can start/end
            // mid-motion, so it has to actively zero the stale input each frame it's active -
            // otherwise FixedUpdate would keep applying whatever direction was held when the modal
            // opened.
            if (InputBlocker.IsBlocked)
            {
                movementInput = Vector2.zero;
                return;
            }

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
            if (health.IsDead) return;

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

            if (Fuel <= 0f)
            {
                health.Kill();
            }
        }

        private void TrackFallDamage()
        {
            if (!IsGrounded)
            {
                peakFallSpeed = Mathf.Max(peakFallSpeed, -rb.linearVelocity.y);
                return;
            }

            if (!wasGrounded && peakFallSpeed > fallDamageVelocityThreshold)
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
