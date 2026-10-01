using System;
using System.Collections.Generic;
using Events;
using UnityEngine;

namespace Museum
{
    [Serializable]
    public class EquippedAccessoryEntry
    {
        public AccessorySlot Slot;
        public AccessoryId Accessory;
    }

    // Save-file shape for RuneCollection (a field of Persistence.GameSaveData).
    [Serializable]
    public class MuseumSaveData
    {
        // Rune indices found in the mine but not yet turned in at the Museum.
        public List<int> FoundRunes = new();
        public List<int> DonatedRunes = new();
        public List<EquippedAccessoryEntry> Equipped = new();
        public bool MetCurator;
    }

    // The player's artifact-rune progress: runes found in the mine but not yet shown to the curator,
    // runes donated to the Museum (which unlock accessories), and the accessory worn in each slot.
    // Nothing here resets on prestige - a found rune is waiting for the curator even after the mine
    // it came from is gone. Pure-logic singleton, not GameManager-registered, matching
    // Critters.CritterCollection.
    public class RuneCollection : Singleton<RuneCollection>
    {
        private static MuseumCollectionDatabase database => GameManager.MuseumCollectionDatabase;

        private readonly HashSet<int> found = new();
        private readonly HashSet<int> donated = new();
        private readonly Dictionary<AccessorySlot, AccessoryId> equipped = new();

        public bool HasMetCurator { get; private set; }
        public int FoundCount => found.Count;
        public int DonatedCount => donated.Count;

        public bool IsFound(int runeIndex) => found.Contains(runeIndex);
        public bool IsDonated(int runeIndex) => donated.Contains(runeIndex);
        public bool IsAccessoryUnlocked(AccessoryDefinition accessory) => accessory != null && donated.Count >= accessory.UnlockAtRuneCount;

        // A rune tablet was mined (by the player or an automaton). Returns whether it's a rune the
        // player had never found before - duplicates still pay their artifact, they just add nothing here.
        public bool RecordFound(RuneDefinition rune, bool byPlayer)
        {
            if (rune == null)
            {
                Debug.LogError("RuneCollection.RecordFound: rune is null.");
                return false;
            }

            bool isNew = !donated.Contains(rune.Index) && found.Add(rune.Index);
            GameManager.EventService.Dispatch(new RuneFoundEvent(rune, isNew, byPlayer));
            if (isNew) GameManager.EventService.Dispatch<RuneCollectionChangedEvent>();
            return isNew;
        }

        // Moves every found rune into the Museum's collection. Returns the dispatched event so the
        // curator can react to each rune and any accessories it unlocked.
        public RunesTurnedInEvent TurnIn()
        {
            int before = donated.Count;
            var newRunes = new List<RuneDefinition>();
            foreach (int index in found)
            {
                var rune = database.GetRune(index);
                if (rune == null)
                {
                    Debug.LogError($"RuneCollection.TurnIn: no RuneDefinition at index {index}.");
                    continue;
                }
                donated.Add(index);
                newRunes.Add(rune);
            }
            found.Clear();
            newRunes.Sort((a, b) => a.Index.CompareTo(b.Index));

            int after = donated.Count;
            var newAccessories = new List<AccessoryDefinition>();
            foreach (var accessory in database.Accessories)
            {
                if (accessory.UnlockAtRuneCount > before && accessory.UnlockAtRuneCount <= after) newAccessories.Add(accessory);
            }

            var evt = new RunesTurnedInEvent(newRunes, newAccessories);
            GameManager.EventService.Dispatch(evt);
            GameManager.EventService.Dispatch<RuneCollectionChangedEvent>();
            return evt;
        }

        public void MarkCuratorMet() => HasMetCurator = true;

        public AccessoryId GetEquipped(AccessorySlot slot)
        {
            if (!equipped.TryGetValue(slot, out var id)) return AccessoryId.None;
            // One that's somehow locked (e.g. a hand-edited save) just isn't worn.
            return IsAccessoryUnlocked(database.GetAccessory(id)) ? id : AccessoryId.None;
        }

        public bool IsEquipped(AccessoryDefinition accessory) => accessory != null && GetEquipped(accessory.Slot) == accessory.Id;

        // Wears the accessory in its slot (replacing whatever was there), or takes it off if it's
        // already worn.
        public void ToggleEquipped(AccessoryDefinition accessory)
        {
            if (accessory == null)
            {
                Debug.LogError("RuneCollection.ToggleEquipped: accessory is null.");
                return;
            }
            if (!IsAccessoryUnlocked(accessory)) return;

            equipped[accessory.Slot] = IsEquipped(accessory) ? AccessoryId.None : accessory.Id;
            GameManager.EventService.Dispatch<PlayerAccessoriesChangedEvent>();
        }

        // Dev Panel: finds every rune not yet donated, so the next TurnIn completes the collection
        // (and unlocks every accessory) through the normal curator path.
        public void DevFindAllRunes()
        {
            foreach (var rune in database.Runes)
            {
                if (!donated.Contains(rune.Index)) found.Add(rune.Index);
            }
            GameManager.EventService.Dispatch<RuneCollectionChangedEvent>();
        }

        public MuseumSaveData ToSaveData()
        {
            var data = new MuseumSaveData { MetCurator = HasMetCurator };
            data.FoundRunes.AddRange(found);
            data.DonatedRunes.AddRange(donated);
            foreach (var kvp in equipped) data.Equipped.Add(new EquippedAccessoryEntry { Slot = kvp.Key, Accessory = kvp.Value });
            return data;
        }

        public void RestoreFromSaveData(MuseumSaveData data)
        {
            found.Clear();
            donated.Clear();
            equipped.Clear();
            HasMetCurator = false;

            if (data != null)
            {
                HasMetCurator = data.MetCurator;
                foreach (int index in data.DonatedRunes)
                {
                    if (database.GetRune(index) != null) donated.Add(index);
                }
                foreach (int index in data.FoundRunes)
                {
                    if (database.GetRune(index) != null && !donated.Contains(index)) found.Add(index);
                }
                foreach (var entry in data.Equipped) equipped[entry.Slot] = entry.Accessory;
            }

            GameManager.EventService.Dispatch<RuneCollectionChangedEvent>();
            GameManager.EventService.Dispatch<PlayerAccessoriesChangedEvent>();
        }
    }
}
