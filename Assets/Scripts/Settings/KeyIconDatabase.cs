using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Settings
{
    [Serializable]
    public class KeyIcon
    {
        public Key Key;
        public Sprite Sprite;
    }

    // Key -> prompt sprite (keyboard-&-mouse_sheet_default) so on-screen prompts like
    // Interaction.InteractionPromptRow show whatever key the player has bound, not a hardcoded
    // letter. Keys without an entry fall back to FallbackSprite (the sheet's "ANY" key).
    // Follows Tutorial.TutorialDatabase's lazy-lookup-dictionary pattern.
    [CreateAssetMenu(fileName = "KeyIconDatabase", menuName = "Settings/Key Icon Database")]
    public class KeyIconDatabase : ScriptableObject
    {
        public List<KeyIcon> Icons = new();
        public Sprite FallbackSprite;

        private Dictionary<Key, Sprite> spritesByKey;

        public Sprite GetIcon(Key key)
        {
            if (spritesByKey == null) BuildLookup();
            return spritesByKey.TryGetValue(key, out var sprite) ? sprite : FallbackSprite;
        }

        private void BuildLookup()
        {
            spritesByKey = new Dictionary<Key, Sprite>();
            foreach (var icon in Icons)
            {
                if (icon == null || icon.Sprite == null) continue;
                spritesByKey[icon.Key] = icon.Sprite;
            }
        }

        public void Validate()
        {
            if (FallbackSprite == null)
            {
                Debug.LogError("KeyIconDatabase.FallbackSprite is not assigned.");
            }
            if (Icons == null || Icons.Count == 0)
            {
                Debug.LogError("KeyIconDatabase has no icons.");
                return;
            }
            foreach (var icon in Icons)
            {
                if (icon == null || icon.Sprite == null)
                {
                    Debug.LogError($"KeyIconDatabase has an entry with no sprite ({icon?.Key}).");
                }
            }
        }
    }
}
