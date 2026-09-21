using System.Collections;
using Events;
using UnityEngine;
using UnityEngine.Serialization;

namespace MapGeneration
{
    // Spawned by HazardEffectResolver once a FallingRock's own cell is reported via
    // HazardTriggeredEvent - which now only happens when the block directly beneath the rock gets
    // mined out (see MapGenerationService.MineCell's "check above" hook). The rock itself can
    // never be mined directly (MineWorld.TryMineCell refuses BlockTypeId.FallingRock), so this is
    // the only way a fall ever starts.
    //
    // Sequence: jiggle in place as a warning, clear the rock's own cell out of the map, then fall
    // one grid cell at a time until the cell below is solid (or the bottom of the generated
    // world), dealing contact damage to anything it passes close to along the way and a bigger
    // one-off impact where it lands. No lasting rubble/obstruction once it lands - matches the
    // "mining-triggered only, fully local" scope agreed for the other hazard effects in this
    // folder. Stacked rocks (a FallingRock resting directly on another) do cascade: clearing this
    // rock's own cell (see MapGenerationService.ClearFallingRockOrigin) re-runs the same "check
    // above" hook MineCell uses, so whatever rock was resting on top starts its own jiggle the
    // moment this one lets go, staggered one Begin() per level of the stack rather than all at once.
    public class FallingRockHazardEffect : MonoBehaviour
    {
        // The existing FallingRock prefab already has a serialized value under the old field name
        // (telegraphSeconds) - preserve it rather than silently reverting to the new default (see
        // BlockType.CustomBehavior for the same gotcha discovered elsewhere in this pass).
        [FormerlySerializedAs("telegraphSeconds")]
        [SerializeField] private float jiggleSeconds = 0.5f;
        [SerializeField] private float jiggleMagnitude = 0.08f;
        [SerializeField] private float fallSecondsPerCell = 0.12f;

        [Tooltip("Small - this is a real touch check while the rock is mid-fall, not a lingering proximity radius.")]
        [SerializeField] private float contactRadius = 0.75f;

        [Tooltip("Wider one-off radius applied once, when the rock lands.")]
        [SerializeField] private float impactRadius = 2.5f;

        [SerializeField] private Color telegraphColor = new(1f, 0.7f, 0.1f);

        [Tooltip("Optional - assign a rock sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the effect still runs without a visual.")]
        [SerializeField] private SpriteRenderer visual;

        public void Begin(int layerIndex, int x, int y) => StartCoroutine(Run(layerIndex, x, y));

        private IEnumerator Run(int layerIndex, int x, int y)
        {
            yield return Jiggle();

            var mapGen = GameManager.MapGenerationService;
            if (mapGen == null)
            {
                Destroy(gameObject);
                yield break;
            }

            // The rock leaves its own cell the instant it starts falling - frees the cell in the
            // map data (so it can't be re-triggered off the same spot) and repaints the tile as
            // empty.
            mapGen.ClearFallingRockOrigin(layerIndex, x, y);
            if (visual != null) visual.color = Color.white;

            yield return Fall(mapGen, layerIndex, x, y);
            Destroy(gameObject);
        }

        private IEnumerator Jiggle()
        {
            if (visual != null) visual.color = telegraphColor;

            Vector3 origin = transform.position;
            float elapsed = 0f;
            while (elapsed < jiggleSeconds)
            {
                elapsed += Time.deltaTime;
                float offsetX = Mathf.Sin(elapsed * 40f) * jiggleMagnitude;
                transform.position = origin + new Vector3(offsetX, 0f, 0f);
                yield return null;
            }
            transform.position = origin;
        }

        private IEnumerator Fall(MapGenerationService mapGen, int layerIndex, int x, int y)
        {
            while (true)
            {
                Vector3 belowWorldPos = mapGen.CellToWorldCenter(layerIndex, x, y) + Vector3.down * mapGen.CellSize;
                bool belowResolved = mapGen.TryWorldToCellInBounds(belowWorldPos, out int belowLayer, out int belowX, out int belowY);

                // Out of bounds (fell past the bottom of the generated world) or a solid block
                // below - either way, this is where the rock comes to rest.
                if (!belowResolved || mapGen.GetBlockTypeAt(belowLayer, belowX, belowY) != null) break;

                yield return MoveOneCell(mapGen.CellToWorldCenter(layerIndex, x, y), mapGen.CellToWorldCenter(belowLayer, belowX, belowY));
                layerIndex = belowLayer;
                x = belowX;
                y = belowY;

                // Dispatched once per cell it now occupies - "touches the player during its fall",
                // not a lingering proximity effect (that's what impactRadius on landing is for).
                GameManager.EventService.Dispatch(new FallingRockImpactEvent(layerIndex, x, y, contactRadius));
            }

            transform.position = mapGen.CellToWorldCenter(layerIndex, x, y);
            GameManager.EventService.Dispatch(new FallingRockImpactEvent(layerIndex, x, y, impactRadius));
        }

        private IEnumerator MoveOneCell(Vector3 from, Vector3 to)
        {
            float elapsed = 0f;
            while (elapsed < fallSecondsPerCell)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, elapsed / fallSecondsPerCell);
                yield return null;
            }
            transform.position = to;
        }
    }
}
