using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Settings
{
    [Serializable]
    public class KeyIcon
    {
        public Key Key;
        public Sprite Sprite;
    }

    [Serializable]
    public class GamepadButtonIcon
    {
        // Control path as the Input System stores it in a binding, e.g. "<Gamepad>/buttonSouth".
        public string ControlPath;
        public Sprite Sprite;
    }

    // Key -> prompt sprite (keyboard-&-mouse_sheet_default) and gamepad control path -> prompt
    // sprite, so on-screen prompts like Interaction.InteractionPromptRow show whatever the player
    // has bound on the device they're using, not a hardcoded letter. Entries without a sprite
    // fall back to FallbackSprite / GamepadFallbackSprite.
    // Follows Tutorial.TutorialDatabase's lazy-lookup-dictionary pattern.
    [CreateAssetMenu(fileName = "KeyIconDatabase", menuName = "Settings/Key Icon Database")]
    public class KeyIconDatabase : ScriptableObject
    {
        public List<KeyIcon> Icons = new();
        public Sprite FallbackSprite;
        public List<GamepadButtonIcon> GamepadIcons = new();
        public Sprite GamepadFallbackSprite;

        private Dictionary<Key, Sprite> spritesByKey;
        private Dictionary<string, Sprite> spritesByGamepadPath;

        // Icon for action's binding on the device the player is currently using.
        public Sprite GetIcon(GameAction action)
        {
            var keybinds = GameManager.KeybindService;
            var scheme = keybinds.CurrentScheme;
            string path = keybinds.GetBindingPath(action, scheme);
            if (path == null) return scheme == InputScheme.Gamepad ? GamepadFallbackSprite : FallbackSprite;

            if (scheme == InputScheme.Gamepad) return GetGamepadIcon(path);

            var keyboard = Keyboard.current;
            var keyControl = keyboard != null ? InputControlPath.TryFindControl(keyboard, path) as KeyControl : null;
            return keyControl != null ? GetIcon(keyControl.keyCode) : FallbackSprite;
        }

        public Sprite GetGamepadIcon(string controlPath)
        {
            if (spritesByGamepadPath == null) BuildLookup();
            return spritesByGamepadPath.TryGetValue(controlPath, out var sprite) ? sprite : GamepadFallbackSprite;
        }

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

            spritesByGamepadPath = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (var icon in GamepadIcons)
            {
                if (icon == null || icon.Sprite == null || string.IsNullOrEmpty(icon.ControlPath)) continue;
                spritesByGamepadPath[icon.ControlPath] = icon.Sprite;
            }
        }

        public void Validate()
        {
            if (FallbackSprite == null)
            {
                Debug.LogError("KeyIconDatabase.FallbackSprite is not assigned.");
            }
            if (GamepadFallbackSprite == null)
            {
                Debug.LogError("KeyIconDatabase.GamepadFallbackSprite is not assigned.");
            }
            foreach (var icon in GamepadIcons)
            {
                if (icon == null || icon.Sprite == null)
                {
                    Debug.LogError($"KeyIconDatabase has a gamepad entry with no sprite ({icon?.ControlPath}).");
                }
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
