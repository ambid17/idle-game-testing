using Events;
using Interaction;
using MapGeneration;
using UnityEngine;

namespace Critters
{
    // A live critter wandering the empty pocket it spawned in (see CritterSpawner, which builds
    // these in code - no prefab). Movement is kinematic and grid-aware rather than physics-driven:
    // flyers pick open points within WanderRadius of home and drift to them along a clear line;
    // ground movers walk the floor, turn around at walls, ledges and the edge of their leash, and
    // fall if the ground under them is mined away. Hidden (and uncatchable) while its cell is
    // still fogged, though a glowing critter's glow shows through the fog as a hint.
    // Pressing Interact on it catches it into the player's jar (CritterCollection.Catch).
    public class Critter : MonoBehaviour, IInteractable
    {
        public InteractableType InteractableType => InteractableType.Critter;

        private const float VisibilityCheckInterval = 0.25f;
        private const float CatchAnimationSeconds = 0.3f;
        private const float Gravity = 20f;
        private const float ProbeStep = 0.2f;
        private const int FlyerTargetAttempts = 6;

        private static MapGenerationService map => GameManager.MapGenerationService;

        private CritterDefinition definition;
        private Vector3Int spawnKey;
        private Vector3 home;
        private float cellSize;

        private Transform visual;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glow;
        private Collider2D interactionCollider;
        private Color glowColor;

        private float halfHeight;
        private float halfWidth;
        private float facing = 1f;
        private float timeOffset;
        private float visibilityTimer;
        private bool caught;

        // Flyer state
        private Vector3 flyTarget;
        private Vector3 flyVelocity;
        private float pauseTimer;

        // Ground state
        private float verticalSpeed;
        private float walkTimer;
        private float hopProgress = -1f;
        private Vector3 hopFrom;
        private Vector3 hopTo;
        private float peekAmount;
        private bool peekRising;

        public CritterDefinition Definition => definition;

        // Called immediately after construction by CritterSpawner.
        public void Configure(CritterDefinition critterDefinition, Vector3Int critterSpawnKey, Vector3 homePosition, SpriteRenderer body, SpriteRenderer glowRenderer, Collider2D collider2d)
        {
            definition = critterDefinition;
            spawnKey = critterSpawnKey;
            home = homePosition;
            spriteRenderer = body;
            visual = body.transform;
            glow = glowRenderer;
            glowColor = glowRenderer != null ? glowRenderer.color : Color.clear;
            interactionCollider = collider2d;
            cellSize = map.CellSize;

            halfHeight = definition.Size * 0.5f;
            halfWidth = spriteRenderer.bounds.size.x * 0.5f;
            timeOffset = Random.value * 100f;
            facing = Random.value < 0.5f ? -1f : 1f;

            transform.position = definition.IsFlyer ? home : SnapToFloor(home);
            flyTarget = transform.position;
            pauseTimer = Random.Range(0f, 1.5f);
            walkTimer = Random.Range(1f, 3f);
            RefreshVisibility();
        }

        private void OnEnable() => GameManager.EventService.Add<PlayerInteractedEvent>(OnPlayerInteracted);

        private void OnDisable() => GameManager.EventService.Remove<PlayerInteractedEvent>(OnPlayerInteracted);

        private void OnPlayerInteracted(PlayerInteractedEvent evt)
        {
            if (caught || !ReferenceEquals(evt.Target, this) || evt.InteractionType != InteractionType.Primary) return;
            // Interact also advances dialog/closes panels - that press isn't a catch.
            if (Player.InputBlocker.IsBlocked) return;
            Catch();
        }

        private void Update()
        {
            if (caught) return;

            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer <= 0f)
            {
                visibilityTimer = VisibilityCheckInterval;
                RefreshVisibility();
            }

            switch (definition.Movement)
            {
                case CritterMovement.Hover:
                    UpdateFlyer(pauseMin: 0.4f, pauseMax: 1.4f, smoothTime: 0.35f, bobSpeed: 3f, bobAmount: 0.06f, jitter: 0f);
                    break;
                case CritterMovement.Flutter:
                    UpdateFlyer(pauseMin: 0f, pauseMax: 0.3f, smoothTime: 0.15f, bobSpeed: 14f, bobAmount: 0.05f, jitter: 0.6f);
                    break;
                case CritterMovement.Float:
                    UpdateFlyer(pauseMin: 1f, pauseMax: 2.5f, smoothTime: 1.2f, bobSpeed: 1.3f, bobAmount: 0.12f, jitter: 0f);
                    // Jellyfish-style pulse: squash as it rises, stretch as it sinks.
                    float pulse = Mathf.Sin((Time.time + timeOffset) * 2.6f) * 0.08f;
                    visual.localScale = new Vector3(BaseScale * (1f + pulse), BaseScale * (1f - pulse), 1f);
                    break;
                case CritterMovement.Crawl:
                    UpdateWalker(pauseChance: 0.35f, wiggle: 0f);
                    break;
                case CritterMovement.Slither:
                    UpdateWalker(pauseChance: 0.1f, wiggle: 1f);
                    break;
                case CritterMovement.Hop:
                    UpdateHopper();
                    break;
                case CritterMovement.Peek:
                    UpdatePeeker();
                    break;
            }

            spriteRenderer.flipX = facing < 0f;
            if (glow != null)
            {
                float flicker = 0.85f + 0.15f * Mathf.Sin((Time.time + timeOffset) * 4f);
                glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * flicker);
            }
        }

        private float BaseScale => definition.Size / Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.y);

        #region Flyers

        private void UpdateFlyer(float pauseMin, float pauseMax, float smoothTime, float bobSpeed, float bobAmount, float jitter)
        {
            var position = transform.position;
            if ((position - flyTarget).sqrMagnitude < 0.0025f)
            {
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    flyTarget = PickFlyTarget(position);
                    pauseTimer = Random.Range(pauseMin, pauseMax);
                }
            }

            var next = Vector3.SmoothDamp(position, flyTarget, ref flyVelocity, smoothTime, definition.Speed);
            if (jitter > 0f)
            {
                float t = (Time.time + timeOffset) * 9f;
                next += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (jitter * Time.deltaTime);
            }

            // Never drift into rock (e.g. jitter near a wall) - hold position and re-plan instead.
            if (map.IsOpenAt(next)) transform.position = next;
            else flyTarget = position;

            if (Mathf.Abs(flyVelocity.x) > 0.05f) facing = Mathf.Sign(flyVelocity.x);
            visual.localPosition = new Vector3(0f, Mathf.Sin((Time.time + timeOffset) * bobSpeed) * bobAmount, 0f);
        }

        private Vector3 PickFlyTarget(Vector3 from)
        {
            float radius = definition.WanderRadius * cellSize;
            for (int attempt = 0; attempt < FlyerTargetAttempts; attempt++)
            {
                var candidate = home + (Vector3)(Random.insideUnitCircle * radius);
                if (IsFlyPathClear(from, candidate)) return candidate;
            }
            return IsFlyPathClear(from, home) ? home : from;
        }

        // Samples the straight line (with the critter's own half-size of clearance at the end
        // point) so flyers never cut through a corner of solid ground.
        private bool IsFlyPathClear(Vector3 from, Vector3 to)
        {
            if (!map.IsOpenAt(to + Vector3.up * halfHeight) || !map.IsOpenAt(to - Vector3.up * halfHeight)) return false;

            float distance = Vector3.Distance(from, to);
            int steps = Mathf.CeilToInt(distance / ProbeStep);
            for (int i = 1; i <= steps; i++)
            {
                if (!map.IsOpenAt(Vector3.Lerp(from, to, i / (float)steps))) return false;
            }
            return true;
        }

        #endregion

        #region Ground movers

        private void UpdateWalker(float pauseChance, float wiggle)
        {
            if (ApplyGravity()) return;

            walkTimer -= Time.deltaTime;
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else
            {
                if (walkTimer <= 0f)
                {
                    walkTimer = Random.Range(1.5f, 4f);
                    if (Random.value < pauseChance) pauseTimer = Random.Range(0.8f, 2.5f);
                    if (Random.value < 0.3f) facing = -facing;
                }

                float step = definition.Speed * Time.deltaTime;
                if (CanWalk(facing, step)) transform.position += Vector3.right * (facing * step);
                else facing = -facing;
            }

            float t = (Time.time + timeOffset);
            bool moving = pauseTimer <= 0f;
            if (wiggle > 0f)
            {
                visual.localPosition = new Vector3(0f, Mathf.Sin(t * 8f) * 0.03f * wiggle, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 8f) * 8f * wiggle);
            }
            else
            {
                // Little scuttle bounce while walking.
                visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(Mathf.Sin(t * definition.Speed * 12f)) * 0.025f : 0f, 0f);
            }
        }

        private void UpdateHopper()
        {
            if (hopProgress >= 0f)
            {
                hopProgress += Time.deltaTime / HopDuration;
                float p = Mathf.Clamp01(hopProgress);
                var position = Vector3.Lerp(hopFrom, hopTo, p);
                position.y += Mathf.Sin(p * Mathf.PI) * 0.35f * cellSize;
                transform.position = position;
                // Stretch mid-air, squash on landing.
                float stretch = Mathf.Sin(p * Mathf.PI) * 0.15f;
                visual.localScale = new Vector3(BaseScale * (1f - stretch), BaseScale * (1f + stretch), 1f);
                if (hopProgress >= 1f) hopProgress = -1f;
                return;
            }

            if (ApplyGravity()) return;

            visual.localScale = new Vector3(BaseScale, BaseScale, 1f);
            pauseTimer -= Time.deltaTime;
            if (pauseTimer > 0f) return;

            pauseTimer = Random.Range(0.6f, 1.8f);
            if (Random.value < 0.25f) facing = -facing;

            float hopLength = definition.Speed * HopDuration;
            if (!CanWalk(facing, hopLength))
            {
                facing = -facing;
                if (!CanWalk(facing, hopLength)) return;
            }

            hopFrom = transform.position;
            hopTo = hopFrom + Vector3.right * (facing * hopLength);
            hopProgress = 0f;
        }

        private const float HopDuration = 0.4f;

        // Mole-style: pops up out of the floor, looks around, sinks back down. Stays put.
        private void UpdatePeeker()
        {
            if (ApplyGravity()) return;

            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                peekRising = !peekRising;
                pauseTimer = peekRising ? Random.Range(2f, 4f) : Random.Range(1.5f, 3f);
            }

            peekAmount = Mathf.MoveTowards(peekAmount, peekRising ? 1f : 0.15f, Time.deltaTime * 4f);
            if (peekRising && Random.value < Time.deltaTime * 0.8f) facing = -facing;

            // Grows up out of the floor: bottom edge pinned, height scaled.
            visual.localScale = new Vector3(BaseScale, BaseScale * peekAmount, 1f);
            visual.localPosition = new Vector3(0f, -halfHeight * (1f - peekAmount), 0f);
        }

        // Next step stays inside the pocket: open ahead, solid floor ahead, within the leash.
        private bool CanWalk(float direction, float distance)
        {
            var position = transform.position;
            float nextX = position.x + direction * distance;
            if (Mathf.Abs(nextX - home.x) > definition.WanderRadius * cellSize) return false;

            float frontX = nextX + direction * halfWidth;
            if (!map.IsOpenAt(new Vector3(frontX, position.y, 0f))) return false;

            float feetY = position.y - halfHeight;
            return !map.IsOpenAt(new Vector3(frontX, feetY - 0.1f, 0f));
        }

        // True while falling (the floor under it was mined away) - movement waits until it lands.
        private bool ApplyGravity()
        {
            var position = transform.position;
            float feetY = position.y - halfHeight;
            if (verticalSpeed == 0f && !map.IsOpenAt(new Vector3(position.x, feetY - 0.05f, 0f))) return false;

            verticalSpeed -= Gravity * Time.deltaTime;
            var next = position + Vector3.up * (verticalSpeed * Time.deltaTime);
            if (!map.IsOpenAt(new Vector3(next.x, next.y - halfHeight, 0f)))
            {
                transform.position = SnapToFloor(next);
                verticalSpeed = 0f;
                // Wherever it lands becomes its new home, so the leash doesn't drag it back up.
                home = transform.position;
                return false;
            }

            transform.position = next;
            return true;
        }

        // Rests the critter's feet on top of the solid cell under the given position.
        private Vector3 SnapToFloor(Vector3 position)
        {
            // Cell boundaries sit on whole multiples of cellSize. Feet in an open cell rest on that
            // cell's bottom edge; feet already sunk into solid ground pop up to its top edge.
            float feetY = position.y - halfHeight;
            float floorTop = map.IsOpenAt(new Vector3(position.x, feetY, 0f))
                ? Mathf.Floor(feetY / cellSize) * cellSize
                : Mathf.Ceil(feetY / cellSize) * cellSize;
            return new Vector3(position.x, floorTop + halfHeight, position.z);
        }

        #endregion

        private void RefreshVisibility()
        {
            bool visible = map.IsRevealedAt(transform.position);
            spriteRenderer.enabled = visible;
            interactionCollider.enabled = visible;
        }

        private void Catch()
        {
            caught = true;
            interactionCollider.enabled = false;
            CritterCollection.Instance.Catch(definition, spawnKey, transform.position);
            StartCoroutine(CatchAnimation());
        }

        // Quick pop-and-shrink "into the jar", then gone.
        private System.Collections.IEnumerator CatchAnimation()
        {
            float baseScale = BaseScale;
            for (float t = 0f; t < CatchAnimationSeconds; t += Time.deltaTime)
            {
                float p = t / CatchAnimationSeconds;
                float scale = p < 0.3f ? Mathf.Lerp(1f, 1.4f, p / 0.3f) : Mathf.Lerp(1.4f, 0f, (p - 0.3f) / 0.7f);
                visual.localScale = new Vector3(baseScale * scale, baseScale * scale, 1f);
                visual.localPosition += Vector3.up * (Time.deltaTime * 1.5f);
                if (glow != null) glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * (1f - p));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
