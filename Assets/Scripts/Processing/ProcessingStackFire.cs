using UnityEngine;

namespace Processing
{
    // The Processing Center's smokestack fire: a looping flame/smoke flipbook plus the fire-lit
    // version of the stack's rim (both split out of the building art by
    // Tools/Buildings/make_processing_stack_fire.py), shown only while at least one
    // ProcessingManager slot has a job running. Idle, the stack is cold and bare. Lives on the
    // Processing Center building.
    public class ProcessingStackFire : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer buildingSprite;
        [SerializeField] private SpriteRenderer fire;
        [SerializeField] private SpriteRenderer litRim;
        [SerializeField] private Sprite[] fireFrames;
        [SerializeField] private float fps = 12f;
        [Tooltip("How long the fire takes to flare up when a job starts and die down when the last one ends.")]
        [SerializeField] private float fadeSeconds = 0.4f;

        private readonly SpriteFlipbook flipbook = new();
        private float intensity;

        private void Awake()
        {
            if (buildingSprite == null) Debug.LogError("ProcessingStackFire.buildingSprite is not assigned.");
            if (fire == null) Debug.LogError("ProcessingStackFire.fire is not assigned.");
            if (litRim == null) Debug.LogError("ProcessingStackFire.litRim is not assigned.");
            if (fireFrames == null || fireFrames.Length == 0) Debug.LogError("ProcessingStackFire.fireFrames is empty.");
        }

        // Polled rather than driven by the job events: jobs restored from a save start without a
        // ProcessingJobStartedEvent.
        private static bool IsProcessing()
        {
            var slots = ProcessingManager.Instance.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null) return true;
            }
            return false;
        }

        private void Update()
        {
            intensity = Mathf.MoveTowards(intensity, IsProcessing() ? 1f : 0f, Time.deltaTime / fadeSeconds);

            bool burning = intensity > 0f;
            fire.enabled = burning;
            litRim.enabled = burning;
            if (!burning) return;

            flipbook.Tick(fire, fireFrames, fps);

            // Follows the building's own alpha too, for the reveal cinematic's fade-in.
            Color color = buildingSprite.color;
            color.a *= intensity;
            fire.color = color;
            litRim.color = color;
        }
    }
}
