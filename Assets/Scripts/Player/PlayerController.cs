using Economy;
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
        private float baseGravityScale;

        // GameDesignDoc "Survival" branch - read on demand each frame rather than cached/pushed,
        // matching every other UpgradeManager/PrestigeUpgradeManager consumer in the project.
        private UpgradeManager upgrades => UpgradeManager.Instance;
        private PrestigeUpgradeManager prestigeUpgrades => PrestigeUpgradeManager.Instance;

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

        // GameDesignDoc "Survival > Increase fuel cap" (Movement_FuelInventory): flat bonus added
        // to the serialized base capacity.
        private float EffectiveFuelMax => fuelMax + (upgrades != null ? upgrades.FuelCapacityBonus : 0f);
        public float FuelFraction => EffectiveFuelMax > 0f ? Fuel / EffectiveFuelMax : 0f;
        public float FuelMax => EffectiveFuelMax;
        public float FuelMissing => EffectiveFuelMax - Fuel;

        // Used by Fuel Drones (Automation.FuelDrone) and ResourceRefillUI's manual purchase buttons -
        // both deposit fuel into the player through this rather than touching Fuel directly.
        public void AddFuel(float amount)
        {
            if (amount <= 0f) return;
            Fuel = Mathf.Min(EffectiveFuelMax, Fuel + amount);
        }
        private Vector2 movementInput;
        public Vector2 MovementInput => movementInput;
        private Keyboard keyboard;


        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.WakeUp();
            baseGravityScale = rb.gravityScale;
            health = GetComponent<PlayerHealth>();
            Fuel = EffectiveFuelMax;
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

        private void HandleDied(PlayerDiedEvent evt)
        {
            movementInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }

        private void HandleRevived()
        {
            Fuel = EffectiveFuelMax;
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
            Fuel = Mathf.Clamp(fuel, 0f, EffectiveFuelMax);
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

            // GameDesignDoc "Survival > Increase fly speed/Increase move speed": market multipliers
            // scale the base, the prestige perk adds a further flat bonus on top.
            float baseHorizontalSpeed = IsFlying
                ? flySpeed * (upgrades != null ? upgrades.FlightSpeedMultiplier : 1f)
                : groundSpeed * (upgrades != null ? upgrades.MoveSpeedMultiplier : 1f);
            float horizontalSpeed = baseHorizontalSpeed + (prestigeUpgrades != null ? prestigeUpgrades.MoveSpeedBonus : 0f);
            ApplyHorizontalMovementForce(movementInput.x * horizontalSpeed);

            // GameDesignDoc "Survival > Increase fall speed" (Movement_GravityIncrease): reset to
            // base * multiplier rather than compounding, since this runs every FixedUpdate.
            rb.gravityScale = baseGravityScale * (upgrades != null ? upgrades.GravityMultiplier : 1f);

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
                // GameDesignDoc "Survival > fuel efficiency": FuelEfficiencyMultiplier is a drain
                // *reduction* (1 - upgrade), so a maxed upgrade approaches zero drain, not zero fuel.
                float efficiency = upgrades != null ? upgrades.FuelEfficiencyMultiplier : 1f;
                Fuel = Mathf.Max(0f, Fuel - fuelDrainPerSecond * efficiency * dt);
            }
            else if (IsGrounded)
            {
                Fuel = Mathf.Min(EffectiveFuelMax, Fuel + fuelRegenPerSecondGrounded * dt);
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
                health.Kill(DeathReason.OutOfFuel);
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
                // GameDesignDoc "Survival > Decrease fall damage": market multiplier and the
                // prestige perk both reduce the per-unit damage, applied multiplicatively.
                float marketReduction = upgrades != null ? upgrades.FallDamageReductionMultiplier : 1f;
                float prestigeReduction = prestigeUpgrades != null ? Mathf.Max(0f, 1f - prestigeUpgrades.FallDamageReduction) : 1f;
                float effectiveDamagePerUnit = fallDamagePerExcessUnit * marketReduction * prestigeReduction;
                health.TakeDamage((lastFallSpeed - fallDamageVelocityThreshold) * effectiveDamagePerUnit, DeathReason.FallDamage);
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
