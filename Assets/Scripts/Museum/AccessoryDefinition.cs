using UnityEngine;

namespace Museum
{
    // Player accessories, unlocked by donating runes to the Museum curator. Explicit, append-only
    // values - equipped accessories are saved as these ints (MuseumSaveData).
    public enum AccessoryId
    {
        None = 0,
        PithHelmet = 1,
        Monocle = 2,
        CuratorCape = 3,
        RuneGoggles = 4,
        RuneHalo = 5,
    }

    // One accessory per slot can be worn at once (Player.PlayerAccessories). Saved as ints.
    public enum AccessorySlot
    {
        Head = 0,
        Face = 1,
        Back = 2,
    }

    [CreateAssetMenu(fileName = "Accessory", menuName = "Museum/Accessory Definition")]
    public class AccessoryDefinition : ScriptableObject
    {
        public AccessoryId Id;
        public string DisplayName;
        public AccessorySlot Slot;
        [Tooltip("Drawn facing right (the player sprite's unflipped direction). Pivot bottom-center.")]
        public Sprite Sprite;
        [Tooltip("Unlocks once this many different runes have been donated to the Museum.")]
        [Min(1)] public int UnlockAtRuneCount = 1;
        [Tooltip("World-space width the sprite is scaled to.")]
        [Range(0.05f, 2f)] public float Width = 0.5f;
        [Tooltip("Where the sprite's pivot sits, in world units from the top-center of the player's current frame, while facing right (mirrored when facing left).")]
        public Vector2 Offset;
        [Tooltip("Degrees, while facing right (mirrored when facing left).")]
        public float Rotation;
        [Tooltip("Draw behind the player's body instead of in front (capes).")]
        public bool BehindBody;
    }
}
