using System.Collections.Generic;
using UnityEngine;

namespace Critters
{
    // Every critter species and automaton hat. Registered on GameManager (GameManager.CritterDatabase).
    // Which critters live in which layer is authored per MapGeneration.LayerConfig.CritterTable, not here.
    [CreateAssetMenu(fileName = "CritterDatabase", menuName = "Critters/Critter Database")]
    public class CritterDatabase : ScriptableObject
    {
        public List<CritterDefinition> Critters = new();
        [Tooltip("Kept sorted by UnlockAtSpeciesCount (Validate errors otherwise) - UI shows them in this order.")]
        public List<HatDefinition> Hats = new();

        private Dictionary<CritterId, CritterDefinition> crittersById;
        private Dictionary<HatId, HatDefinition> hatsById;

        public int SpeciesCount => Critters.Count;

        public CritterDefinition Get(CritterId id)
        {
            if (crittersById == null) BuildLookups();
            crittersById.TryGetValue(id, out var definition);
            return definition;
        }

        public HatDefinition GetHat(HatId id)
        {
            if (hatsById == null) BuildLookups();
            hatsById.TryGetValue(id, out var definition);
            return definition;
        }

        private void BuildLookups()
        {
            crittersById = new Dictionary<CritterId, CritterDefinition>();
            foreach (var critter in Critters)
            {
                if (critter != null) crittersById[critter.Id] = critter;
            }

            hatsById = new Dictionary<HatId, HatDefinition>();
            foreach (var hat in Hats)
            {
                if (hat != null) hatsById[hat.Id] = hat;
            }
        }

        public void Validate()
        {
            var seenCritters = new HashSet<CritterId>();
            foreach (var critter in Critters)
            {
                if (critter == null)
                {
                    Debug.LogError("CritterDatabase contains a null CritterDefinition.");
                    continue;
                }
                if (critter.Id == CritterId.None) Debug.LogError($"CritterDefinition '{critter.name}' has CritterId.None.");
                if (!seenCritters.Add(critter.Id)) Debug.LogError($"CritterDatabase has a duplicate CritterId {critter.Id}.");
                if (critter.Sprite == null) Debug.LogError($"CritterDefinition '{critter.name}' has no Sprite.");
            }

            var seenHats = new HashSet<HatId>();
            int lastUnlock = 0;
            foreach (var hat in Hats)
            {
                if (hat == null)
                {
                    Debug.LogError("CritterDatabase contains a null HatDefinition.");
                    continue;
                }
                if (hat.Id == HatId.None) Debug.LogError($"HatDefinition '{hat.name}' has HatId.None.");
                if (!seenHats.Add(hat.Id)) Debug.LogError($"CritterDatabase has a duplicate HatId {hat.Id}.");
                if (hat.Sprite == null) Debug.LogError($"HatDefinition '{hat.name}' has no Sprite.");
                if (hat.UnlockAtSpeciesCount < lastUnlock) Debug.LogError($"CritterDatabase.Hats is not sorted by UnlockAtSpeciesCount (at '{hat.name}').");
                if (hat.UnlockAtSpeciesCount > Critters.Count) Debug.LogError($"HatDefinition '{hat.name}' unlocks at {hat.UnlockAtSpeciesCount} species but only {Critters.Count} exist.");
                lastUnlock = hat.UnlockAtSpeciesCount;
            }
        }
    }
}
