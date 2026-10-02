using Atmosphere;
using UnityEngine;

namespace Buildings
{
    // The three round signs over the Market's entrance light up one after another, with an
    // all-together double blink now and then - like a shop sign. The lights are additive glows
    // laid over the building art (no art was cut out for this), so each one just brightens the
    // sign underneath it.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MarketSignLights : MonoBehaviour
    {
        [Tooltip("Centre of each sign, in the building's local space (sprite units from its pivot).")]
        [SerializeField] private Vector2[] signPositions =
        {
            new(-0.1123f, 0.1514f),
            new(0f, 0.1631f),
            new(0.1133f, 0.1514f),
        };
        [SerializeField] private Color[] signColors =
        {
            new(1f, 0.25f, 0.3f, 1f),
            new(0.3f, 0.55f, 1f, 1f),
            new(0.3f, 1f, 0.45f, 1f),
        };
        [Tooltip("Glow diameter in the building's local units.")]
        [SerializeField] private float glowDiameter = 0.2f;
        [Tooltip("Seconds each sign stays lit during the chase.")]
        [SerializeField] private float stepSeconds = 0.45f;
        [Tooltip("Every this many chase laps, all the signs blink together twice instead.")]
        [SerializeField, Min(1)] private int lapsPerBlink = 4;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] private float litAlpha = 0.75f;

        private SpriteRenderer[] glows;

        private void Start()
        {
            if (signPositions.Length != signColors.Length)
            {
                Debug.LogError($"{nameof(MarketSignLights)} on {name}: signPositions and signColors must be the same length.");
                enabled = false;
                return;
            }

            var building = GetComponent<SpriteRenderer>();
            glows = new SpriteRenderer[signPositions.Length];
            for (int i = 0; i < glows.Length; i++)
            {
                var glow = GlowSprites.CreateGlow(transform, signColors[i], glowDiameter);
                glow.name = $"SignLight {i + 1}";
                glow.transform.localPosition = signPositions[i];
                glow.sortingLayerID = building.sortingLayerID;
                // Just in front of the building, still behind the player.
                glow.sortingOrder = building.sortingOrder + 1;
                glows[i] = glow;
            }
        }

        private void Update()
        {
            int count = glows.Length;
            float step = Time.time / stepSeconds;
            // One lap = each sign once; then a lap-long slot that is either a rest or the blink.
            int lapSteps = count * 2;
            int lap = Mathf.FloorToInt(step / lapSteps);
            float inLap = step % lapSteps;

            for (int i = 0; i < count; i++)
            {
                float lit;
                if (inLap < count)
                {
                    // Chase: a soft pulse centred on this sign's turn.
                    lit = Mathf.Clamp01(1f - Mathf.Abs(inLap - (i + 0.5f)) * 1.6f);
                }
                else if (lap % lapsPerBlink == lapsPerBlink - 1)
                {
                    // Two blinks across the slot.
                    lit = Mathf.Sin((inLap - count) / count * Mathf.PI * 4f) > 0f ? 1f : 0f;
                }
                else
                {
                    lit = 0f;
                }

                Color color = signColors[i];
                color.a = Mathf.Lerp(dimAlpha, litAlpha, lit);
                glows[i].color = color;
            }
        }
    }
}
