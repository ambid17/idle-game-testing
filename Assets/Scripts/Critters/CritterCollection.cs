using System;
using System.Collections.Generic;
using Economy;
using Events;
using UnityEngine;

namespace Critters
{
    [Serializable]
    public class CritterCountEntry
    {
        public CritterId Id;
        public int Count;
    }

    [Serializable]
    public class AutomatonHatEntry
    {
        public int AutomatonIndex;
        public HatId Hat;
    }

    // Save-file shape for CritterCollection (a field of Persistence.GameSaveData).
    [Serializable]
    public class CritterSaveData
    {
        public List<CritterId> Jar = new();
        public List<CritterCountEntry> TurnedIn = new();
        // (x, y, layerIndex) of each caught critter's pocket seed cell - see CritterSpawner.
        public List<Vector3Int> CaughtSpawns = new();
        public List<AutomatonHatEntry> AutomatonHats = new();
        public bool MetShopkeeper;
    }

    // Everything the player owns critter-wise: the jar of caught-but-not-yet-delivered critters,
    // the lifetime collection (species turned in at the Critter Shop, with counts), which pocket
    // critters were already caught this run (so they don't respawn), and each automaton's hat.
    // The collection, hats and shopkeeper intro survive prestige; the jar and caught-spawn set
    // belong to the old seed's mine and are cleared with it. Pure-logic singleton, not
    // GameManager-registered, matching Wallet/Depot/ChestRegistry.
    public class CritterCollection : Singleton<CritterCollection>
    {
        private static CritterDatabase database => GameManager.CritterDatabase;

        private readonly List<CritterId> jar = new();
        private readonly Dictionary<CritterId, int> turnedIn = new();
        private readonly HashSet<Vector3Int> caughtSpawns = new();
        private readonly Dictionary<int, HatId> automatonHats = new();

        public IReadOnlyList<CritterId> Jar => jar;
        public bool HasMetShopkeeper { get; private set; }

        public int SpeciesCollected
        {
            get
            {
                int count = 0;
                foreach (var kvp in turnedIn)
                {
                    if (kvp.Value > 0) count++;
                }
                return count;
            }
        }

        public double JarValue
        {
            get
            {
                double total = 0;
                foreach (var id in jar)
                {
                    var definition = database.Get(id);
                    if (definition != null) total += definition.TurnInValue;
                }
                return total;
            }
        }

        private void OnEnable() => GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        private void OnDisable() => GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);

        public int GetTurnedInCount(CritterId id) => turnedIn.TryGetValue(id, out int count) ? count : 0;
        public bool IsDiscovered(CritterId id) => GetTurnedInCount(id) > 0;
        public bool IsSpawnCaught(Vector3Int spawnKey) => caughtSpawns.Contains(spawnKey);
        public bool IsHatUnlocked(HatDefinition hat) => hat != null && SpeciesCollected >= hat.UnlockAtSpeciesCount;

        public void Catch(CritterDefinition critter, Vector3Int spawnKey, Vector3 position)
        {
            if (critter == null)
            {
                Debug.LogError("CritterCollection.Catch: critter is null.");
                return;
            }

            jar.Add(critter.Id);
            caughtSpawns.Add(spawnKey);
            GameManager.EventService.Dispatch(new CritterCaughtEvent(critter, position));
            GameManager.EventService.Dispatch<CritterCollectionChangedEvent>();
        }

        // Empties the jar into the collection and pays each critter's TurnInValue (duplicates
        // included). Returns the dispatched event so the shop can react to the newly discovered
        // species and freshly unlocked hats.
        public CrittersTurnedInEvent TurnInJar()
        {
            int speciesBefore = SpeciesCollected;
            var newSpecies = new List<CritterDefinition>();
            double payout = 0;

            foreach (var id in jar)
            {
                var definition = database.Get(id);
                if (definition == null)
                {
                    Debug.LogError($"CritterCollection.TurnInJar: no CritterDefinition for {id}.");
                    continue;
                }

                if (!IsDiscovered(id)) newSpecies.Add(definition);
                turnedIn[id] = GetTurnedInCount(id) + 1;
                payout += definition.TurnInValue;
            }

            int count = jar.Count;
            jar.Clear();
            if (payout > 0) Wallet.Instance.Add(payout);

            int speciesAfter = SpeciesCollected;
            var newHats = new List<HatDefinition>();
            foreach (var hat in database.Hats)
            {
                if (hat.UnlockAtSpeciesCount > speciesBefore && hat.UnlockAtSpeciesCount <= speciesAfter) newHats.Add(hat);
            }

            var evt = new CrittersTurnedInEvent(count, payout, newSpecies, newHats);
            GameManager.EventService.Dispatch(evt);
            GameManager.EventService.Dispatch<CritterCollectionChangedEvent>();
            return evt;
        }

        public void MarkShopkeeperMet() => HasMetShopkeeper = true;

        // Dev Panel: one of every species straight into the jar, so the next TurnInJar discovers
        // them all (and unlocks every hat) through the normal payout/event path.
        public void DevAddAllSpeciesToJar()
        {
            foreach (var critter in database.Critters)
            {
                if (critter != null) jar.Add(critter.Id);
            }
            GameManager.EventService.Dispatch<CritterCollectionChangedEvent>();
        }

        // Automatons are identified by their 1-based DisplayIndex (see AutomationSpawner), which is
        // stable across reloads since the spawner always rebuilds them in order.
        public HatId GetAutomatonHat(int automatonIndex)
        {
            if (!automatonHats.TryGetValue(automatonIndex, out var hat)) return HatId.None;
            // A hat that's somehow locked (e.g. a hand-edited save) is just not worn.
            return hat == HatId.None || IsHatUnlocked(database.GetHat(hat)) ? hat : HatId.None;
        }

        public void SetAutomatonHat(int automatonIndex, HatId hat)
        {
            automatonHats[automatonIndex] = hat;
            GameManager.EventService.Dispatch<AutomatonHatsChangedEvent>();
        }

        private void OnPrestigeCompleted(PrestigeCompletedEvent evt)
        {
            jar.Clear();
            caughtSpawns.Clear();
            GameManager.EventService.Dispatch<CritterCollectionChangedEvent>();
        }

        public CritterSaveData ToSaveData()
        {
            var data = new CritterSaveData { MetShopkeeper = HasMetShopkeeper };
            data.Jar.AddRange(jar);
            foreach (var kvp in turnedIn) data.TurnedIn.Add(new CritterCountEntry { Id = kvp.Key, Count = kvp.Value });
            data.CaughtSpawns.AddRange(caughtSpawns);
            foreach (var kvp in automatonHats) data.AutomatonHats.Add(new AutomatonHatEntry { AutomatonIndex = kvp.Key, Hat = kvp.Value });
            return data;
        }

        public void RestoreFromSaveData(CritterSaveData data)
        {
            jar.Clear();
            turnedIn.Clear();
            caughtSpawns.Clear();
            automatonHats.Clear();
            HasMetShopkeeper = false;

            if (data != null)
            {
                HasMetShopkeeper = data.MetShopkeeper;
                jar.AddRange(data.Jar);
                foreach (var entry in data.TurnedIn) turnedIn[entry.Id] = entry.Count;
                foreach (var key in data.CaughtSpawns) caughtSpawns.Add(key);
                foreach (var entry in data.AutomatonHats) automatonHats[entry.AutomatonIndex] = entry.Hat;
            }

            GameManager.EventService.Dispatch<CritterCollectionChangedEvent>();
            GameManager.EventService.Dispatch<AutomatonHatsChangedEvent>();
        }
    }
}
