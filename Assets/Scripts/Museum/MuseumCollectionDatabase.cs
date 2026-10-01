using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace Museum
{
    // Every artifact rune and player accessory. Registered on GameManager (GameManager.MuseumCollectionDatabase).
    [CreateAssetMenu(fileName = "MuseumCollectionDatabase", menuName = "Museum/Museum Collection Database")]
    public class MuseumCollectionDatabase : ScriptableObject
    {
        // Salt for MapRng.HashCell - distinct from every ChunkGenerator salt so the rune pick doesn't
        // correlate with anything else rolled for the cell.
        private const int RuneSalt = 0x52554E45;

        [Tooltip("Indexed by RuneDefinition.Index - entry i must have Index i (Validate errors otherwise).")]
        public List<RuneDefinition> Runes = new();
        [Tooltip("Kept sorted by UnlockAtRuneCount (Validate errors otherwise) - UI shows them in this order.")]
        public List<AccessoryDefinition> Accessories = new();

        private Dictionary<AccessoryId, AccessoryDefinition> accessoriesById;

        public int RuneCount => Runes.Count;

        public RuneDefinition GetRune(int index) => index >= 0 && index < Runes.Count ? Runes[index] : null;

        // The rune on the artifact tablet at this cell. Pure (seed + cell only), so the tile view, the
        // miners and the debris all agree without the map storing anything extra.
        public RuneDefinition GetRuneAt(int seed, int layerIndex, int x, int y) =>
            Runes[(int)(MapRng.HashCell(seed, layerIndex, x, y, RuneSalt) % (uint)Runes.Count)];

        // Same, for the current world.
        public RuneDefinition GetRuneAt(int layerIndex, int x, int y) =>
            GetRuneAt(GameManager.MapGenerationService.World.Seed, layerIndex, x, y);

        public AccessoryDefinition GetAccessory(AccessoryId id)
        {
            if (accessoriesById == null)
            {
                accessoriesById = new Dictionary<AccessoryId, AccessoryDefinition>();
                foreach (var accessory in Accessories)
                {
                    if (accessory != null) accessoriesById[accessory.Id] = accessory;
                }
            }
            accessoriesById.TryGetValue(id, out var definition);
            return definition;
        }

        public void Validate()
        {
            if (Runes.Count == 0) Debug.LogError("MuseumCollectionDatabase has no runes.");
            for (int i = 0; i < Runes.Count; i++)
            {
                var rune = Runes[i];
                if (rune == null)
                {
                    Debug.LogError($"MuseumCollectionDatabase.Runes[{i}] is null.");
                    continue;
                }
                if (rune.Index != i) Debug.LogError($"RuneDefinition '{rune.name}' has Index {rune.Index} but sits at Runes[{i}].");
                if (rune.Icon == null) Debug.LogError($"RuneDefinition '{rune.name}' has no Icon.");
                if (rune.Tile == null) Debug.LogError($"RuneDefinition '{rune.name}' has no Tile.");
            }

            var seen = new HashSet<AccessoryId>();
            int lastUnlock = 0;
            foreach (var accessory in Accessories)
            {
                if (accessory == null)
                {
                    Debug.LogError("MuseumCollectionDatabase contains a null AccessoryDefinition.");
                    continue;
                }
                if (accessory.Id == AccessoryId.None) Debug.LogError($"AccessoryDefinition '{accessory.name}' has AccessoryId.None.");
                if (!seen.Add(accessory.Id)) Debug.LogError($"MuseumCollectionDatabase has a duplicate AccessoryId {accessory.Id}.");
                if (accessory.Sprite == null) Debug.LogError($"AccessoryDefinition '{accessory.name}' has no Sprite.");
                if (accessory.UnlockAtRuneCount < lastUnlock) Debug.LogError($"MuseumCollectionDatabase.Accessories is not sorted by UnlockAtRuneCount (at '{accessory.name}').");
                if (accessory.UnlockAtRuneCount > Runes.Count) Debug.LogError($"AccessoryDefinition '{accessory.name}' unlocks at {accessory.UnlockAtRuneCount} runes but only {Runes.Count} exist.");
                lastUnlock = accessory.UnlockAtRuneCount;
            }
        }
    }
}
