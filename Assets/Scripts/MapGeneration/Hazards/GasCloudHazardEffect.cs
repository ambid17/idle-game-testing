using System.Collections;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Expands, lingers, then dissipates - spawned by HazardEffectResolver at a mined GasPocket
    // cell's world position. Dispatches a damage-tick event on an interval while it has any
    // radius; the "is the player actually in range" distance check lives in Player.
    // HazardDamageHandler (same place every other hazard's radius check already lives), not here -
    // this effect only owns its own expand/linger/dissipate timeline. Chain-ignition into Lava/
    // Explosive (GameDesignDoc) is deferred to a follow-up pass.
    public class GasCloudHazardEffect : MonoBehaviour
    {
        [SerializeField] private float expandSeconds = 1f;
        [SerializeField] private float lingerSeconds = 4f;
        [SerializeField] private float dissipateSeconds = 1.5f;
        [SerializeField] private float maxRadius = 3f;
        [SerializeField] private float tickIntervalSeconds = 0.5f;
        [SerializeField] private Color gasColor = new(0.5f, 0.9f, 0.3f, 0.5f);

        [Tooltip("Optional - assign a gas VFX sprite here (on a prefab wired into HazardEffectResolver) for real art. Safe to leave unset; the effect still runs without a visual.")]
        [SerializeField] private SpriteRenderer visual;

        private int layerIndex;
        private int cellX;
        private int cellY;
        private float currentRadius;

        public void Begin(int layerIndex, int x, int y)
        {
            this.layerIndex = layerIndex;
            cellX = x;
            cellY = y;
            if (visual != null) visual.color = gasColor;

            StartCoroutine(Run());
            StartCoroutine(TickDamage());
        }

        private IEnumerator Run()
        {
            yield return Lerp(0f, maxRadius, expandSeconds);
            yield return new WaitForSeconds(lingerSeconds);
            yield return Lerp(maxRadius, 0f, dissipateSeconds);
            Destroy(gameObject);
        }

        private IEnumerator Lerp(float from, float to, float seconds)
        {
            float t = 0f;
            while (seconds > 0f && t < seconds)
            {
                t += Time.deltaTime;
                SetRadius(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetRadius(to);
        }

        private void SetRadius(float radius)
        {
            currentRadius = radius;
            if (visual != null) visual.transform.localScale = Vector3.one * radius * 2f;
        }

        private IEnumerator TickDamage()
        {
            var wait = new WaitForSeconds(tickIntervalSeconds);
            while (true)
            {
                if (currentRadius > 0f)
                {
                    GameManager.EventService.Dispatch(new GasCloudDamageTickEvent(layerIndex, cellX, cellY, currentRadius));
                }
                yield return wait;
            }
        }
    }
}
