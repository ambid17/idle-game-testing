using System.Collections;
using System.Collections.Generic;
using Atmosphere;
using Events;
using MapGeneration;
using Museum;
using UnityEngine;

namespace Player
{
    // Purely cosmetic "juice" for the player's digging, driven by PlayerMining:
    //  - Hit: a few chips flick off the block face toward the player on every pickaxe hit.
    //  - Break: a burst of chunks (tinted by the block, plus dirt-coloured ones for ores) and a dust
    //    puff, a screen shake scaled by the block's effective health, and a brief hit-stop on hard blocks.
    //    Artifact tablets also shed little copies of themselves, carved with the same rune.
    //  - Pickup: collected ore/artifacts pop out of the cell as tinted nuggets that home in on the
    //    player. The inventory is credited immediately by PlayerMining as before - this is visual only.
    // Shake goes through GameManager.CameraShake (which honours the Options "Screen Shake" toggle).
    public class DigFeedback : MonoBehaviour
    {
        private struct Nugget
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Age;
        }

        private const int DebrisSheetRows = 4;
        private const int MaxNuggets = 24;

        [Header("Rendering")]
        [Tooltip("Alpha-blended material (Sprites/Default) carrying the DebrisChips sheet - 4 chip shapes stacked vertically.")]
        [SerializeField] private Material debrisMaterial;
        [Tooltip("Small rune tablets stacked vertically, row = RuneDefinition.Index (Tools/Artifacts/make_rune_tablets.py).")]
        [SerializeField] private Texture2D runeDebrisSheet;
        [Tooltip("Grayscale nugget sprite, tinted with the block's MinimapColor.")]
        [SerializeField] private Sprite nuggetSprite;
        [Tooltip("Debris and nuggets draw above terrain, fog and the player.")]
        [SerializeField] private int sortingOrder = 6;

        [Header("Debris")]
        [SerializeField, Min(0)] private int chipsPerHit = 3;
        [SerializeField, Min(0)] private int chunksPerBreak = 12;
        [Tooltip("Share of an ore's break chunks tinted with the ore colour - the rest use the dirt colour of the tile background.")]
        [SerializeField, Range(0f, 1f)] private float oreChunkFraction = 0.45f;
        [SerializeField, Min(0)] private int dustPerBreak = 5;
        [Tooltip("Mini rune tablets an artifact sheds when it breaks, on top of its chunks.")]
        [SerializeField, Min(0)] private int runeTabletsPerBreak = 5;

        [Header("Screen shake (scaled by the broken block's effective health)")]
        [Tooltip("Effective health (BlockType.Health x layer BlockHealth) at which shake starts, and at which it's strongest.")]
        [SerializeField] private Vector2 shakeHealthRange = new(0.4f, 3f);
        [SerializeField, Min(0f)] private float minShakeForce = 0.04f;
        [SerializeField, Min(0f)] private float maxShakeForce = 0.2f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.15f;

        [Header("Hit-stop (brief freeze when a hard block breaks)")]
        [SerializeField, Min(0f)] private float hitStopHealthThreshold = 1.5f;
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.05f;
        [Tooltip("Minimum real time between hit-stops, so vein mining or fast digging can't stutter.")]
        [SerializeField, Min(0f)] private float hitStopCooldown = 0.4f;

        [Header("Pickup nuggets")]
        [SerializeField, Min(0.05f)] private float nuggetSize = 0.4f;
        [Tooltip("Seconds a nugget arcs out of the cell before homing in on the player.")]
        [SerializeField, Min(0f)] private float nuggetPopSeconds = 0.3f;
        [SerializeField, Min(0f)] private float nuggetHomingAcceleration = 60f;
        [SerializeField, Min(0.5f)] private float nuggetMaxSpeed = 22f;

        [Header("Artifact found")]
        [SerializeField] private Color artifactBeamColor = new(1f, 0.85f, 0.35f, 0.8f);
        [Tooltip("Game speed during the brief slow-motion beat (1 = none).")]
        [SerializeField, Range(0.05f, 1f)] private float artifactSlowMoScale = 0.3f;
        [Tooltip("Real seconds the slow-motion lasts.")]
        [SerializeField, Min(0f)] private float artifactSlowMoSeconds = 0.45f;
        [Tooltip("Height of the tablet (world units) as it spins up out of the cell.")]
        [SerializeField, Min(0.05f)] private float artifactTabletSize = 0.8f;
        [SerializeField] private float artifactRiseHeight = 1.1f;
        [Tooltip("Real seconds the tablet takes to rise before it flies to the HUD.")]
        [SerializeField, Min(0.05f)] private float artifactRiseSeconds = 0.75f;
        [SerializeField] private float artifactBeamHeight = 6f;

        [Header("Treasure chest")]
        [Tooltip("The chest with its lid open - swapped in for the block's own (closed) icon when it pops.")]
        [SerializeField] private Sprite openChestSprite;
        [Tooltip("Width of the chest (world units).")]
        [SerializeField, Min(0.05f)] private float chestSize = 0.9f;

        private ParticleSystem debrisSystem;
        private ParticleSystem dustSystem;
        private ParticleSystem sparkleSystem;
        // One per rune, made the first time that rune breaks - each shows a single row of runeDebrisSheet.
        private readonly Dictionary<int, ParticleSystem> runeDebrisSystems = new();
        private Material runeDebrisMaterial;
        private readonly List<Nugget> nuggets = new();
        private readonly Stack<Transform> nuggetPool = new();
        private Coroutine hitStopRoutine;
        private float nextHitStopTime;
        private bool slowMoActive;
        private Color dirtColor;

        private void Awake()
        {
            if (debrisMaterial == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its debrisMaterial reference.");
            if (nuggetSprite == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its nuggetSprite reference.");
            if (runeDebrisSheet == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its runeDebrisSheet reference.");
            if (openChestSprite == null) Debug.LogError($"{nameof(DigFeedback)} on {name} is missing its openChestSprite reference.");
        }

        private void Start()
        {
            dirtColor = GameManager.BlockTypeDatabase.Get((byte)BlockTypeId.Dirt).MinimapColor;

            debrisSystem = CreateSystem("Dig Debris", debrisMaterial, 300, gravity: 2.2f);
            ConfigureDebris(debrisSystem, DebrisSheetRows, ParticleSystemAnimationRowMode.Random, 0);

            runeDebrisMaterial = new Material(debrisMaterial) { name = "Rune Debris", mainTexture = runeDebrisSheet };

            var dustMaterial = new Material(debrisMaterial) { name = "Dig Dust", mainTexture = GlowSprites.SoftDot.texture };
            dustSystem = CreateSystem("Dig Dust", dustMaterial, 100, gravity: -0.02f);
            FadeOutAtEnd(dustSystem, 0.2f);
            var dustSize = dustSystem.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

            sparkleSystem = CreateSystem("Pickup Sparkles", GlowSprites.ParticleMaterial, 150, gravity: 0f);
            FadeOutAtEnd(sparkleSystem, 0.3f);
        }

        private void OnDisable()
        {
            // A hit-stop or slow-motion beat cut short (scene unload, player disabled) must not
            // leave the game frozen or slowed.
            if (hitStopRoutine == null && !slowMoActive) return;
            if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
            slowMoActive = false;
            Time.timeScale = 1f;
        }

        private void Update() => UpdateNuggets(Time.deltaTime);

        // A pickaxe hit on the block at cellCenter, dug in digDirection (from the player toward the block).
        public void Hit(Vector3 cellCenter, Vector2Int digDirection, BlockType block)
        {
            Vector3 away = -(Vector2)digDirection;
            Vector3 face = cellCenter + away * 0.45f;
            for (int i = 0; i < chipsPerHit; i++)
            {
                var velocity = away * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1f, 2.5f) + (Vector3)(Random.insideUnitCircle * 0.8f);
                EmitDebris(face + (Vector3)(Random.insideUnitCircle * 0.2f), velocity, Random.Range(0.08f, 0.14f), Random.Range(0.35f, 0.55f), ChipColor(block));
            }
        }

        // The block at cellCenter broke. primary = the block the player was working on (vein-mined
        // bonus cells pass false: they get debris, but no extra shake or hit-stop).
        // rune: the tablet's rune when block is an Artifact, else null.
        public void Break(Vector3 cellCenter, Vector2Int digDirection, BlockType block, float effectiveHealth, bool primary, RuneDefinition rune = null)
        {
            if (rune != null) EmitRuneTablets(cellCenter, rune);

            for (int i = 0; i < chunksPerBreak; i++)
            {
                var offset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
                var velocity = offset.normalized * Random.Range(1f, 3.5f) + Vector3.up * Random.Range(1.5f, 3.5f);
                EmitDebris(cellCenter + offset, velocity, Random.Range(0.12f, 0.24f), Random.Range(0.6f, 1.1f), ChipColor(block));
            }

            var dust = Color.Lerp(dirtColor, Color.white, 0.35f);
            dust.a = 0.45f;
            for (int i = 0; i < dustPerBreak; i++)
            {
                var velocity = (Vector3)(Random.insideUnitCircle * 0.6f) + Vector3.up * 0.2f;
                Emit(dustSystem, cellCenter + (Vector3)(Random.insideUnitCircle * 0.3f), velocity, Random.Range(0.5f, 0.9f), Random.Range(0.5f, 0.8f), dust, 0f);
            }

            if (!primary) return;

            float strength = Mathf.InverseLerp(shakeHealthRange.x, shakeHealthRange.y, effectiveHealth);
            if (effectiveHealth >= shakeHealthRange.x)
            {
                GameManager.CameraShake.Shake((Vector2)digDirection * Mathf.Lerp(minShakeForce, maxShakeForce, strength), shakeSeconds);
            }

            if (effectiveHealth >= hitStopHealthThreshold && hitStopSeconds > 0f && Time.unscaledTime >= nextHitStopTime && hitStopRoutine == null)
            {
                nextHitStopTime = Time.unscaledTime + hitStopCooldown;
                hitStopRoutine = StartCoroutine(HitStop());
            }
        }

        // Nuggets popping out of cellCenter and flying to the player - one per unit collected.
        // sprite: drawn untinted in place of the generic nugget (an artifact's rune tablet).
        public void Pickup(Vector3 cellCenter, BlockType block, int count, Sprite sprite = null)
        {
            for (int i = 0; i < count && nuggets.Count < MaxNuggets; i++)
            {
                var nuggetTransform = nuggetPool.Count > 0 ? nuggetPool.Pop() : CreateNugget();
                nuggetTransform.position = cellCenter;
                var body = nuggetTransform.GetComponent<SpriteRenderer>();
                body.sprite = sprite != null ? sprite : nuggetSprite;
                body.color = sprite != null ? Color.white : block.MinimapColor;
                var glowColor = block.MinimapColor;
                glowColor.a = 0.5f;
                nuggetTransform.GetChild(0).GetComponent<SpriteRenderer>().color = glowColor;
                nuggetTransform.gameObject.SetActive(true);

                nuggets.Add(new Nugget
                {
                    Transform = nuggetTransform,
                    Velocity = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(3.5f, 5f), 0f),
                    Age = 0f,
                });
            }
        }

        // An artifact was dug up at cellCenter: time slows for a beat, a beam of light shoots up out
        // of the cell and the tablet spins up through it, then flies off to the HUD's artifact
        // counter (UI.HudFlyIconsUI). Runs on real time, so the slow-motion doesn't slow it too.
        public void ArtifactFound(Vector3 cellCenter, Sprite tablet)
        {
            StartCoroutine(ArtifactSlowMo());
            StartCoroutine(ArtifactFlourish(cellCenter, tablet));
        }

        // The chest block at cellCenter was mined: the chest squashes down, pops its lid and the
        // ore that went into the bag (loot) fountains out of it and flies to the player.
        public void TreasureChest(Vector3 cellCenter, BlockType chestBlock, IReadOnlyDictionary<BlockType, int> loot)
        {
            StartCoroutine(TreasureChestPop(cellCenter, chestBlock, loot));
        }

        private IEnumerator ArtifactSlowMo()
        {
            // Breaking the tablet's block may have started a hit-stop - let that finish first, and
            // never fight over the clock with anything else that changed it.
            yield return new WaitForSecondsRealtime(hitStopSeconds + 0.03f);
            if (!Mathf.Approximately(Time.timeScale, 1f)) yield break;

            slowMoActive = true;
            Time.timeScale = artifactSlowMoScale;
            yield return new WaitForSecondsRealtime(artifactSlowMoSeconds);

            const float easeOutSeconds = 0.2f;
            for (float t = 0f; t < easeOutSeconds && slowMoActive; t += Time.unscaledDeltaTime)
            {
                Time.timeScale = Mathf.Lerp(artifactSlowMoScale, 1f, t / easeOutSeconds);
                yield return null;
            }
            if (!slowMoActive) yield break;

            Time.timeScale = 1f;
            slowMoActive = false;
        }

        private IEnumerator ArtifactFlourish(Vector3 cellCenter, Sprite tablet)
        {
            var gold = new Color(artifactBeamColor.r, artifactBeamColor.g, artifactBeamColor.b, 1f);
            GameManager.WorldEffects.SparkleBurst(cellCenter, 14, 0.2f, 3f, gold);

            // A radial glow stretched tall reads as a soft-edged beam.
            var beam = GlowSprites.CreateGlow(transform.parent, artifactBeamColor, 1f);
            beam.name = "Artifact Beam";
            beam.sortingOrder = sortingOrder;
            beam.transform.position = cellCenter + Vector3.up * (artifactBeamHeight * 0.4f);

            var tabletRenderer = new GameObject("Artifact Tablet").AddComponent<SpriteRenderer>();
            tabletRenderer.transform.SetParent(transform.parent, false);
            tabletRenderer.sprite = tablet;
            tabletRenderer.sortingOrder = sortingOrder + 2;
            float tabletScale = artifactTabletSize / tablet.bounds.size.y;

            Vector3 position = cellCenter;
            float sparkleTimer = 0f;
            for (float t = 0f; t < artifactRiseSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / artifactRiseSeconds;
                position = cellCenter + Vector3.up * (artifactRiseHeight * Effects.Easing.OutCubic(k));
                // Two full flips about its vertical axis, like a tossed coin, ending face-on.
                float flip = Mathf.Cos(k * Mathf.PI * 4f);
                float size = tabletScale * Mathf.Lerp(0.5f, 1f, Effects.Easing.OutBack(Mathf.Min(1f, k * 2f)));
                tabletRenderer.transform.position = position;
                tabletRenderer.transform.localScale = new Vector3(size * flip, size, 1f);

                // Snaps on, then thins and fades as the tablet reaches the top.
                float beamStrength = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
                beam.color = new Color(artifactBeamColor.r, artifactBeamColor.g, artifactBeamColor.b, artifactBeamColor.a * beamStrength);
                beam.transform.localScale = new Vector3(Mathf.Lerp(0.5f, 1.3f, beamStrength), artifactBeamHeight, 1f);

                sparkleTimer -= Time.unscaledDeltaTime;
                if (sparkleTimer <= 0f)
                {
                    sparkleTimer = 0.05f;
                    Vector3 offset = Random.insideUnitCircle * (artifactTabletSize * 0.5f);
                    GameManager.WorldEffects.Sparkle(position + offset, Vector3.up * Random.Range(0.5f, 1.5f), Random.Range(0.15f, 0.3f), 0.5f, gold);
                }
                yield return null;
            }

            Destroy(beam.gameObject);
            Destroy(tabletRenderer.gameObject);
            GameManager.WorldEffects.SparkleBurst(position, 10, 0.15f, 2.5f, gold);
            GameManager.EventService.Dispatch(new HudIconFlyRequestedEvent(tablet, position));
        }

        private IEnumerator TreasureChestPop(Vector3 cellCenter, BlockType chestBlock, IReadOnlyDictionary<BlockType, int> loot)
        {
            var chest = new GameObject("Treasure Chest Effect").AddComponent<SpriteRenderer>();
            chest.transform.SetParent(transform.parent, false);
            chest.transform.position = cellCenter;
            chest.sprite = chestBlock.Icon;
            chest.sortingOrder = sortingOrder;
            float scale = chestSize / chestBlock.Icon.bounds.size.x;

            // Anticipation: squashes down before it pops.
            const float squashSeconds = 0.14f;
            for (float t = 0f; t < squashSeconds; t += Time.deltaTime)
            {
                float squash = 0.25f * Mathf.Sin(t / squashSeconds * Mathf.PI * 0.5f);
                chest.transform.localScale = new Vector3(scale * (1f + squash), scale * (1f - squash), 1f);
                yield return null;
            }

            // The open art is taller (lid up) at the same width, so it keeps the chest body's size.
            chest.sprite = openChestSprite;
            scale = chestSize / openChestSprite.bounds.size.x;
            var gold = new Color(1f, 0.85f, 0.35f, 1f);
            GameManager.WorldEffects.SparkleBurst(cellCenter + Vector3.up * 0.2f, 14, 0.15f, 3.5f, gold);

            // One nugget per ore, fed out over the hop so it reads as a fountain rather than one clump.
            var nuggets = new List<BlockType>();
            foreach (var entry in loot)
            {
                for (int i = 0; i < entry.Value; i++) nuggets.Add(entry.Key);
            }
            int released = 0;

            const float popSeconds = 0.45f;
            for (float t = 0f; t < popSeconds; t += Time.deltaTime)
            {
                float k = t / popSeconds;
                float spring = 0.3f * Mathf.Exp(-5f * k) * Mathf.Cos(k * Mathf.PI * 4f);
                chest.transform.localScale = new Vector3(scale * (1f - spring * 0.6f), scale * (1f + spring), 1f);
                chest.transform.position = cellCenter + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.18f);

                int due = Mathf.Min(nuggets.Count, Mathf.CeilToInt(k / 0.7f * nuggets.Count));
                for (; released < due; released++) Pickup(cellCenter + Vector3.up * 0.15f, nuggets[released], 1);
                yield return null;
            }
            for (; released < nuggets.Count; released++) Pickup(cellCenter + Vector3.up * 0.15f, nuggets[released], 1);
            chest.transform.position = cellCenter;
            chest.transform.localScale = Vector3.one * scale;

            yield return new WaitForSeconds(0.5f);

            const float vanishSeconds = 0.18f;
            for (float t = 0f; t < vanishSeconds; t += Time.deltaTime)
            {
                chest.transform.localScale = Vector3.one * (scale * (1f - t / vanishSeconds));
                yield return null;
            }
            GameManager.WorldEffects.Puff(cellCenter, 5, chestSize * 0.6f);
            Destroy(chest.gameObject);
        }

        private IEnumerator HitStop()
        {
            // Only freeze from normal speed - never stomp on anything else that changed timeScale.
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                hitStopRoutine = null;
                yield break;
            }

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        private void UpdateNuggets(float dt)
        {
            Vector3 target = transform.position;
            for (int i = nuggets.Count - 1; i >= 0; i--)
            {
                var nugget = nuggets[i];
                nugget.Age += dt;
                Vector3 position = nugget.Transform.position;

                if (nugget.Age < nuggetPopSeconds)
                {
                    nugget.Velocity += Vector3.down * (14f * dt);
                }
                else
                {
                    // Steer toward the player, accelerating, so the pop arc bends smoothly into a dash.
                    Vector3 toTarget = target - position;
                    nugget.Velocity += toTarget.normalized * (nuggetHomingAcceleration * dt);
                    nugget.Velocity = Vector3.ClampMagnitude(nugget.Velocity, nuggetMaxSpeed);
                    // Bleed off sideways drift so nuggets don't orbit the player.
                    nugget.Velocity = Vector3.Lerp(nugget.Velocity, toTarget.normalized * nugget.Velocity.magnitude, 6f * dt);
                }

                position += nugget.Velocity * dt;
                nugget.Transform.position = position;
                // Slight squash-and-stretch pulse while flying.
                float pulse = 1f + 0.12f * Mathf.Sin(nugget.Age * 30f);
                nugget.Transform.localScale = new Vector3(nuggetSize * pulse, nuggetSize / pulse, 1f);

                bool arrived = nugget.Age >= nuggetPopSeconds && (target - position).sqrMagnitude < 0.3f * 0.3f;
                // Safety net: a nugget that somehow never arrives still cleans itself up.
                if (arrived || nugget.Age > 3f)
                {
                    if (arrived) EmitSparkles(position, nugget.Transform.GetComponent<SpriteRenderer>().color);
                    nugget.Transform.gameObject.SetActive(false);
                    nuggetPool.Push(nugget.Transform);
                    nuggets.RemoveAt(i);
                    continue;
                }
                nuggets[i] = nugget;
            }
        }

        private Transform CreateNugget()
        {
            var go = new GameObject("Pickup Nugget");
            go.transform.SetParent(transform.parent, false);
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = nuggetSprite;
            body.sortingOrder = sortingOrder + 1;

            var glow = GlowSprites.CreateGlow(go.transform, Color.white, 2.2f);
            glow.sortingOrder = sortingOrder;
            return go.transform;
        }

        private void EmitSparkles(Vector3 position, Color color)
        {
            var sparkle = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                var velocity = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(1f, 2.5f));
                Emit(sparkleSystem, position, velocity, Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.4f), sparkle, 0f);
            }
        }

        // Ores and artifacts shed a mix of their own colour and the dirt of their tile background.
        private Color ChipColor(BlockType block)
        {
            bool hasDirtBackground = block.Category == BlockCategory.Ore || block.Category == BlockCategory.Artifact;
            var color = hasDirtBackground && Random.value > oreChunkFraction ? dirtColor : block.MinimapColor;
            // Small per-chip brightness variation keeps a burst from looking flat.
            float shade = Random.Range(0.85f, 1.1f);
            return new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
        }

        private void EmitDebris(Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            Emit(debrisSystem, position, velocity, size, lifetime, color, Random.Range(0f, 360f));
        }

        // Bigger, slower-tumbling and longer-lived than the chunks, so the carved rune stays readable.
        private void EmitRuneTablets(Vector3 cellCenter, RuneDefinition rune)
        {
            if (!runeDebrisSystems.TryGetValue(rune.Index, out var system))
            {
                system = CreateSystem($"Rune Debris {rune.Index}", runeDebrisMaterial, 30, gravity: 2.2f);
                ConfigureDebris(system, GameManager.MuseumCollectionDatabase.RuneCount, ParticleSystemAnimationRowMode.Custom, rune.Index);
                runeDebrisSystems[rune.Index] = system;
            }

            for (int i = 0; i < runeTabletsPerBreak; i++)
            {
                var offset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.3f), 0f);
                var velocity = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(3f, 4.5f), 0f);
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = cellCenter + offset,
                    velocity = velocity,
                    startSize = Random.Range(0.36f, 0.44f),
                    startLifetime = Random.Range(1.2f, 1.6f),
                    startColor = Color.white,
                    rotation = Random.Range(-25f, 25f),
                    angularVelocity = Random.Range(-200f, 200f),
                    applyShapeToPosition = false,
                };
                system.Emit(emitParams, 1);
            }
        }

        // A vertical strip of debris shapes that bounce off the ground and fade out at the end.
        // Random rows (chips) pick a shape per particle; a Custom row pins every particle to one.
        private static void ConfigureDebris(ParticleSystem system, int rows, ParticleSystemAnimationRowMode rowMode, int rowIndex)
        {
            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 1;
            sheet.numTilesY = rows;
            sheet.animation = ParticleSystemAnimationType.SingleRow;
            sheet.rowMode = rowMode;
            sheet.rowIndex = rowIndex;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = LayerMask.GetMask("Ground");
            collision.bounce = 0.35f;
            collision.dampen = 0.35f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;
            FadeOutAtEnd(system, 0.75f);
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = rotation,
                angularVelocity = Random.Range(-360f, 360f),
                applyShapeToPosition = false,
            };
            system.Emit(emitParams, 1);
        }

        private ParticleSystem CreateSystem(string systemName, Material material, int maxParticles, float gravity)
        {
            var go = new GameObject(systemName);
            // Not parented to the player: world-space particles shouldn't inherit its sorting group or scale.
            go.transform.SetParent(transform.parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;

            system.Play();
            return system;
        }

        // Full opacity (on top of each particle's start colour) until fadeStart, then out to 0.
        private static void FadeOutAtEnd(ParticleSystem system, float fadeStart)
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }
    }
}
