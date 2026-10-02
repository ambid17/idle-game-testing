using System.Collections.Generic;
using Events;
using Museum;
using UnityEngine;

namespace Player
{
    // Dresses the player in the Museum accessories they're wearing (Museum.RuneCollection.GetEquipped):
    // one child sprite per slot, positioned from the top-center of the body's current animation
    // frame so it bobs along, mirrored with the body's flip, and following its visibility and tint
    // (death, portal travel, damage flash). Back-slot pieces draw behind the body, the rest in front.
    // Sits on the player's body SpriteRenderer, next to PlayerAnimation.
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAccessories : MonoBehaviour
    {
        private class Worn
        {
            public SpriteRenderer Renderer;
            public AccessoryDefinition Accessory;
        }

        private static readonly AccessorySlot[] Slots = { AccessorySlot.Head, AccessorySlot.Face, AccessorySlot.Back };

        private SpriteRenderer bodyRenderer;
        private readonly Dictionary<AccessorySlot, Worn> worn = new();

        private void Awake()
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            foreach (var slot in Slots)
            {
                var go = new GameObject($"Accessory ({slot})");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                worn[slot] = new Worn { Renderer = renderer };
            }
        }

        // The pieces currently worn, for effects that need a copy of the dressed player (PlayerDeathEffect).
        public IEnumerable<SpriteRenderer> WornRenderers
        {
            get
            {
                foreach (var entry in worn.Values)
                {
                    if (entry.Accessory != null) yield return entry.Renderer;
                }
            }
        }

        private void OnEnable() => GameManager.EventService.Add<PlayerAccessoriesChangedEvent>(Refresh);
        private void OnDisable() => GameManager.EventService.Remove<PlayerAccessoriesChangedEvent>(Refresh);

        private void Start() => Refresh();

        private void Refresh()
        {
            var database = GameManager.MuseumCollectionDatabase;
            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

            foreach (var slot in Slots)
            {
                var entry = worn[slot];
                entry.Accessory = database.GetAccessory(RuneCollection.Instance.GetEquipped(slot));
                if (entry.Accessory == null || entry.Accessory.Sprite == null)
                {
                    entry.Accessory = null;
                    entry.Renderer.enabled = false;
                    continue;
                }

                entry.Renderer.sprite = entry.Accessory.Sprite;
                entry.Renderer.sortingOrder = bodyRenderer.sortingOrder + (entry.Accessory.BehindBody ? -1 : 1);
                // Scale to the accessory's authored world width, undoing the player's own scale.
                float scale = entry.Accessory.Width / Mathf.Max(0.0001f, entry.Accessory.Sprite.bounds.size.x) / lossyX;
                entry.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // After PlayerAnimation's Update has picked this frame's sprite and facing.
        private void LateUpdate()
        {
            bool flipped = bodyRenderer.flipX;
            float facing = flipped ? -1f : 1f;
            float lossyX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float top = SpriteOpaqueBounds.Top(bodyRenderer.sprite);

            foreach (var entry in worn.Values)
            {
                if (entry.Accessory == null) continue;

                var renderer = entry.Renderer;
                renderer.enabled = bodyRenderer.enabled;
                renderer.color = bodyRenderer.color;
                renderer.flipX = flipped;

                var offset = entry.Accessory.Offset / lossyX;
                renderer.transform.localPosition = new Vector3(offset.x * facing, top + offset.y, 0f);
                renderer.transform.localRotation = Quaternion.Euler(0f, 0f, entry.Accessory.Rotation * facing);
            }
        }
    }
}
