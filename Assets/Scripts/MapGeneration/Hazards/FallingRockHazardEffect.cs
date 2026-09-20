using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Telegraph-then-impact, spawned by HazardEffectResolver at a mined FallingRock cell's world
    // position. Fully local and short-lived per the agreed "mining-triggered only" scope - no
    // lasting rubble/obstruction, just a warning window then a single damage event at the landing
    // location (Player.HazardDamageHandler owns the actual damage).
    public class FallingRockHazardEffect : MonoBehaviour
    {
        [SerializeField] private float telegraphSeconds = 0.7f;
        [SerializeField] private float impactRadius = 2.5f;
        [SerializeField] private Color telegraphColor = new(1f, 0.7f, 0.1f);

        [Tooltip("Optional - assign a warning sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the effect still runs without a visual.")]
        [SerializeField] private SpriteRenderer visual;

        public void Begin(int layerIndex, int x, int y)
        {
            if (visual != null) visual.color = telegraphColor;
            StartCoroutine(Run(layerIndex, x, y));
        }

        private IEnumerator Run(int layerIndex, int x, int y)
        {
            yield return new WaitForSeconds(telegraphSeconds);
            GameManager.EventService.Dispatch(new FallingRockImpactEvent(layerIndex, x, y, impactRadius));
            Destroy(gameObject);
        }
    }
}
