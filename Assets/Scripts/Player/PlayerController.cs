using Events;
using UI;
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
        [SerializeField] private float jetpackForce = 15f;
        // Horizontal accel used to close the gap to target speed via AddForce. High enough to feel
        // near-instant on open ground, but - unlike a hard rb.linearVelocity assignment - a wall's
        // contact response can actually oppose this force instead of being overwritten every
        // FixedUpdate, which is what let the player pop up and over walls they ran into.
        [SerializeField] private float moveAcceleration = 80f;

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

        private const float LowFuelWarningFraction = 0.5f;

        private Rigidbody2D rb;
        private CapsuleCollider2D capsuleCollider;
        private PlayerHealth health;
        private bool wasGrounded;
        private bool wasInputBlocked;
        private float lastFallSpeed;
        private Vector3 spawnPosition;

        // Frictionless while flying only - grounded movement keeps the collider's authored
        // (default) material so the player doesn't slide around on the ground. Without this, the
        // AddForce-based horizontal push (see ApplyHorizontalMovementForce) presses the player into
        // a wall hard enough that normal friction there resists the jetpack's vertical thrust too,
        // pinning them in place instead of letting them fly up the wall.
        private PhysicsMaterial2D flyingMaterial;
        private PhysicsMaterial2D groundedMaterial;

        public bool IsGrounded { get; private set; }
        public bool IsFlying { get; private set; }
        public float Fuel { get; private set; }
        public float FuelFraction => fuelMax > 0f ? Fuel / fuelMax : 0f;
        public float FuelMax => fuelMax;
        public float FuelMissing => fuelMax - Fuel;

        // Used by Fuel Drones (Automation.FuelDrone) and ResourceRefillUI's manual purchase buttons -
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

            groundedMaterial = capsuleCollider.sharedMaterial;
            flyingMaterial = new PhysicsMaterial2D("PlayerFlyingMaterial") { friction = 0f, bounciness = 0f };
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
            lastFallSpeed = 0f;
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
            lastFallSpeed = 0f;
        }

        private void Update()
        {
            if (health.IsDead || keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // Three tiers: a modal nested inside (or standalone atop) a panel closes first;
                // only once none is open does Escape fall through to closing the panel itself,
                // or - if nothing was open at all - opening the pause menu. See UI.ModalTracker.
                if (ModalTracker.IsAnyModalOpen)
                {
                    GameManager.EventService.Dispatch<ModalCloseRequestedEvent>();
                }
                else
                {
                    bool panelWasOpen = InputBlocker.IsBlocked;
                    GameManager.EventService.Dispatch<UICloseEvent>();
                    if (!panelWasOpen)
                    {
                        GameManager.EventService.Dispatch<PauseMenuOpenRequestedEvent>();
                    }
                }
            }

            // Dev Panel hotkey (UI.DevPanelUI) - backquote matches the console log viewer's
            // pre-existing key. Editor/Development Build only, and only when nothing else already
            // has input blocked, matching PauseMenuOpenRequestedEvent's guard above so it never
            // fights another open modal for the screen.
            if (keyboard.backquoteKey.wasPressedThisFrame && (Debug.isDebugBuild || Application.isEditor) && !InputBlocker.IsBlocked)
            {
                GameManager.EventService.Dispatch<DevPanelOpenRequestedEvent>();
            }

            // Unlike death (which zeroes movementInput once via HandleDied), blocking can start/end
            // mid-motion, so it has to actively zero the stale input each frame it's active -
            // otherwise FixedUpdate would keep applying whatever direction was held when the modal
            // opened.
            bool isBlocked = InputBlocker.IsBlocked;
            if (isBlocked != wasInputBlocked)
            {
                // Zeroing movementInput alone only stops new input - it doesn't stop gravity or
                // whatever velocity the player already had (e.g. mid-fall) from continuing to
                // integrate every FixedUpdate, which is what let a tutorial popup fly off-screen
                // with the player before they could click it. Switching to Kinematic removes the
                // Rigidbody2D from physics simulation entirely for the duration of the block.
                SetPhysicsFrozen(isBlocked);
                wasInputBlocked = isBlocked;
            }

            if (isBlocked)
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
            // The Kinematic switch in Update already removes the Rigidbody2D from physics
            // simulation, but this also skips fuel drain/regen and fall-damage tracking so a
            // blocked modal doesn't silently cost fuel or attribute fall damage to time spent paused.
            if (health.IsDead || InputBlocker.IsBlocked) return;

            IsGrounded = CheckGrounded();
            IsFlying = movementInput.y > 0 && Fuel > 0f;

            capsuleCollider.sharedMaterial = !IsGrounded ? flyingMaterial : groundedMaterial;

            float horizontalSpeed = IsFlying ? flySpeed : groundSpeed;
            ApplyHorizontalMovementForce(movementInput.x * horizontalSpeed);

            // Jetpack pushes rather than snapping vertical velocity, so gravity still pulls
            // against it - lets the player feather W for a soft landing instead of a hard cutoff.
            if (IsFlying)
            {
                var force = rb.linearVelocityY > 0f ? jetpackForce : jetpackForce * 2;
                rb.AddForce(Vector2.up * force, ForceMode2D.Force);
            }

            UpdateFuel(Time.fixedDeltaTime);
            TrackFallDamage();

            wasGrounded = IsGrounded;
        }

        private void UpdateFuel(float dt)
        {
            float previousFuelFraction = FuelFraction;
            if (IsFlying)
            {
                Fuel = Mathf.Max(0f, Fuel - fuelDrainPerSecond * dt);
            }
            else if (IsGrounded)
            {
                Fuel = Mathf.Min(fuelMax, Fuel + fuelRegenPerSecondGrounded * dt);
            }

            // Edge-triggered: only fires the tick fuel first crosses at/below half, not every
            // tick while it stays low - otherwise this would keep resetting HudToastUI's display
            // timer and could drown out other notifications (e.g. inventory-full) sharing the
            // same toast.
            if (FuelFraction <= LowFuelWarningFraction && previousFuelFraction > LowFuelWarningFraction)
            {
                GameManager.EventService.Dispatch(new HudNotificationEvent("Fuel is running low!"));
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
                // Overwrite rather than take a max - we want the velocity from the last airborne
                // frame (right before impact), not the highest speed reached anywhere in the fall.
                lastFallSpeed = -rb.linearVelocity.y;
                return;
            }

            if (!wasGrounded && lastFallSpeed > fallDamageVelocityThreshold)
            {
                health.TakeDamage((lastFallSpeed - fallDamageVelocityThreshold) * fallDamagePerExcessUnit);
            }

            lastFallSpeed = 0f;
        }

        // Accelerates toward targetVelocityX via AddForce instead of snapping rb.linearVelocity.
        // Capped by moveAcceleration so a wall's contact response can win against this force rather
        // than being overwritten wholesale every fixed step.
        private void ApplyHorizontalMovementForce(float targetVelocityX)
        {
            float velocityDiff = targetVelocityX - rb.linearVelocity.x;
            float maxForce = moveAcceleration * rb.mass;
            float force = Mathf.Clamp(velocityDiff * rb.mass / Time.fixedDeltaTime, -maxForce, maxForce);
            rb.AddForce(new Vector2(force, 0f), ForceMode2D.Force);
        }

        private void SetPhysicsFrozen(bool frozen)
        {
            if (frozen)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        private bool CheckGrounded()
        {
            Vector2 origin = (Vector2)transform.position + groundCheckOffset;
            var collided = Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundLayer);
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
