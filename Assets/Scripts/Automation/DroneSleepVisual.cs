using UnityEngine;

namespace Automation
{
    // Communicates "this drone has nothing to do": once its ISleepableDrone parent has been idle
    // for fallAsleepDelay, the drone settles down into a slow breathing bob, swaps to its
    // powered-down sprite (dark lamp eye with a closed lid, thrusters off) and puffs floating Z's.
    // Waking snaps the awake sprite back with a little upward jolt, so the player can see work
    // just showed up.
    //
    // Lives on the drone's "Visual" child rather than the root: the drone scripts drive the root
    // transform via GridPathMover, so the bob offsets only this child's localPosition.
    // Sprites come from Tools/Automaton/make_drone_sleep_sprites.py.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DroneSleepVisual : MonoBehaviour
    {
        [SerializeField] private Sprite awakeSprite;
        [SerializeField] private Sprite sleepSprite;
        [SerializeField] private Sprite zSprite;

        [Header("Timing")]
        [Tooltip("Seconds of continuous idleness before dozing off - stops the drone flickering asleep between quick jobs.")]
        [SerializeField] private float fallAsleepDelay = 2.5f;
        [SerializeField] private float settleDuration = 0.8f;

        [Header("Breathing")]
        [SerializeField] private float sinkDepth = 0.08f;
        [SerializeField] private float breatheAmplitude = 0.025f;
        [SerializeField] private float breatheFrequency = 0.45f;
        [SerializeField] private Color sleepTint = new(0.8f, 0.8f, 0.9f, 1f);

        [Header("Wake jolt")]
        [SerializeField] private float wakeJoltHeight = 0.12f;
        [SerializeField] private float wakeJoltDuration = 0.25f;

        [Header("Z particles")]
        [SerializeField] private float zEmitInterval = 1.1f;
        [SerializeField] private float zLifetime = 2.2f;
        [SerializeField] private float zRiseDistance = 0.55f;
        [SerializeField] private float zDriftAmplitude = 0.07f;
        [SerializeField] private Vector2 zSpawnOffset = new(0.22f, 0.42f);
        [SerializeField] private Vector2 zScaleRange = new(0.5f, 1f);
        [SerializeField] private Color zColor = new(0.85f, 0.92f, 1f, 1f);

        private const int ZPoolSize = 3;

        private struct ZParticle
        {
            public SpriteRenderer Renderer;
            public float Age;
            public bool Alive;
        }

        private ISleepableDrone drone;
        private SpriteRenderer spriteRenderer;
        private readonly ZParticle[] zParticles = new ZParticle[ZPoolSize];

        private float idleTimer;
        private float sleepWeight;   // 0 awake -> 1 fully settled
        private float zEmitTimer;
        private float wakeJoltTimer;
        private bool isAsleep;

        // Per-drone phase so drones stacked on the same idle anchor don't breathe and puff Z's
        // in lockstep (their Z's would overlap exactly).
        private float phase;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            phase = Random.value;
            drone = GetComponentInParent<ISleepableDrone>();
            if (drone == null) Debug.LogError($"{nameof(DroneSleepVisual)} on {name} has no {nameof(ISleepableDrone)} in its parents.");
            if (awakeSprite == null || sleepSprite == null || zSprite == null) Debug.LogError($"{nameof(DroneSleepVisual)} on {name} is missing a sprite reference.");

            for (int i = 0; i < ZPoolSize; i++)
            {
                var go = new GameObject($"SleepZ {i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = zSprite;
                sr.sortingLayerID = spriteRenderer.sortingLayerID;
                sr.sortingOrder = spriteRenderer.sortingOrder + 1;
                sr.enabled = false;
                zParticles[i].Renderer = sr;
            }
        }

        private void LateUpdate()
        {
            UpdateSleepState();
            UpdatePose();
            UpdateZParticles();
        }

        private void UpdateSleepState()
        {
            idleTimer = drone.IsIdle ? idleTimer + Time.deltaTime : 0f;
            bool shouldSleep = idleTimer >= fallAsleepDelay;

            if (shouldSleep && !isAsleep)
            {
                isAsleep = true;
                zEmitTimer = zEmitInterval * phase; // first Z shortly after dozing off, staggered per drone
                spriteRenderer.sprite = sleepSprite;
            }
            else if (!shouldSleep && isAsleep)
            {
                isAsleep = false;
                sleepWeight = 0f;
                wakeJoltTimer = wakeJoltDuration;
                spriteRenderer.sprite = awakeSprite;
                ClearZParticles();
            }

            if (isAsleep) sleepWeight = Mathf.MoveTowards(sleepWeight, 1f, Time.deltaTime / settleDuration);
        }

        private void UpdatePose()
        {
            float settle = Mathf.SmoothStep(0f, 1f, sleepWeight);
            float breathe = Mathf.Sin((Time.time * breatheFrequency + phase) * 2f * Mathf.PI) * breatheAmplitude;
            float y = (-sinkDepth + breathe) * settle;

            if (wakeJoltTimer > 0f)
            {
                wakeJoltTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(wakeJoltTimer / wakeJoltDuration);
                y += Mathf.Sin(t * Mathf.PI) * wakeJoltHeight;
            }

            transform.localPosition = new Vector3(0f, y, 0f);
            spriteRenderer.color = Color.Lerp(Color.white, sleepTint, settle);
        }

        private void UpdateZParticles()
        {
            if (isAsleep && sleepWeight >= 1f)
            {
                zEmitTimer += Time.deltaTime;
                if (zEmitTimer >= zEmitInterval)
                {
                    zEmitTimer = 0f;
                    EmitZ();
                }
            }

            for (int i = 0; i < ZPoolSize; i++)
            {
                ref var z = ref zParticles[i];
                if (!z.Alive) continue;

                z.Age += Time.deltaTime;
                float t = z.Age / zLifetime;
                if (t >= 1f)
                {
                    z.Alive = false;
                    z.Renderer.enabled = false;
                    continue;
                }

                // Local position is relative to this (bobbing) transform; subtract our own offset
                // so the Z's rise in the drone's frame without inheriting the breathing bob.
                float drift = Mathf.Sin(t * Mathf.PI * 2f) * zDriftAmplitude;
                var pos = new Vector3(zSpawnOffset.x + drift + t * 0.12f, zSpawnOffset.y + t * zRiseDistance, 0f);
                z.Renderer.transform.localPosition = pos - transform.localPosition;
                z.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(zScaleRange.x, zScaleRange.y, t);

                // Quick fade in, long fade out.
                float alpha = t < 0.15f ? t / 0.15f : 1f - Mathf.InverseLerp(0.5f, 1f, t);
                z.Renderer.color = new Color(zColor.r, zColor.g, zColor.b, zColor.a * alpha);
            }
        }

        private void ClearZParticles()
        {
            for (int i = 0; i < ZPoolSize; i++)
            {
                zParticles[i].Alive = false;
                zParticles[i].Renderer.enabled = false;
            }
        }

        private void EmitZ()
        {
            for (int i = 0; i < ZPoolSize; i++)
            {
                if (zParticles[i].Alive) continue;
                zParticles[i].Alive = true;
                zParticles[i].Age = 0f;
                zParticles[i].Renderer.enabled = true;
                return;
            }
        }
    }
}
