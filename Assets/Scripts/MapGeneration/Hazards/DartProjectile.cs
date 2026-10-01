using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Spawned by StructureTrapResolver at a Dart Trap block when a Pressure Plate is stepped on.
    // Waits a beat (the player's window to react), then flies out of the trap one open cell at a
    // time until it meets something solid, dispatching DartImpactEvent for each cell it enters -
    // Player.HazardDamageHandler owns the "is the player actually there" check and the damage,
    // same split as FallingRockHazardEffect.
    public class DartProjectile : MonoBehaviour
    {
        [SerializeField] private float fireDelaySeconds = 0.25f;
        [SerializeField] private float cellsPerSecond = 14f;

        [Tooltip("Dart sprite, drawn pointing right - the projectile rotates to face its direction of travel.")]
        [SerializeField] private SpriteRenderer visual;

        private void Start()
        {
            if (visual == null) Debug.LogError($"{nameof(DartProjectile)} on {name} has no visual assigned.");
        }

        // direction is in cell space: +x right, +y down (deeper).
        public void Begin(int layerIndex, int trapX, int trapY, Vector2Int direction) =>
            StartCoroutine(Run(layerIndex, trapX, trapY, direction));

        private IEnumerator Run(int layerIndex, int x, int y, Vector2Int direction)
        {
            var mapGen = GameManager.MapGenerationService;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg);

            visual.enabled = false;
            yield return new WaitForSeconds(fireDelaySeconds);
            visual.enabled = true;

            float secondsPerCell = 1f / cellsPerSecond;
            while (mapGen.IsOpenCell(layerIndex, x + direction.x, y + direction.y))
            {
                Vector3 from = transform.position;
                x += direction.x;
                y += direction.y;
                Vector3 to = mapGen.CellToWorldCenter(layerIndex, x, y);

                float elapsed = 0f;
                while (elapsed < secondsPerCell)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, elapsed / secondsPerCell);
                    yield return null;
                }

                GameManager.EventService.Dispatch(new DartImpactEvent(layerIndex, x, y));
            }

            Destroy(gameObject);
        }
    }
}
