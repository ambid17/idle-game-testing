using System.Collections.Generic;
using MapGeneration;
using UnityEngine;

namespace RunModifiers
{
    // Rolls the modifiers offered at prestige. Deterministic for (seed, rerollIndex) so reopening
    // the prestige screen - or reloading the game - shows the same offers; only an explicit reroll
    // (a Museum perk) changes them. Offer rules:
    //  - every offer is a different modifier, and at most one per non-None family;
    //  - with 2+ offers, at least one Blessing and at least one Gamble;
    //  - the heirloom modifier (the current run's, when the Heirloom perk is owned) always takes a
    //    slot, with freshly rolled parameters.
    public static class RunModifierOfferRoller
    {
        public static List<RunModifierState> Roll(RunModifierDatabase database, LayerConfigProvider layers, int seed, int rerollIndex, int count, string heirloomId)
        {
            var rng = new System.Random(unchecked((int)MapRng.HashCell(seed, rerollIndex, 0, 0, 0x5EED)));
            var picked = new List<RunModifierDefinition>();

            var heirloom = database.Get(heirloomId);
            if (heirloom != null) picked.Add(heirloom);

            if (count >= 2)
            {
                TryPick(database, rng, picked, RunModifierKind.Blessing, true);
                TryPick(database, rng, picked, RunModifierKind.Gamble, true);
            }
            while (picked.Count < count && TryPick(database, rng, picked, RunModifierKind.Blessing, false)) { }

            // Shuffle so the guaranteed Blessing/Gamble slots aren't always in the same place.
            for (int i = picked.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (picked[i], picked[j]) = (picked[j], picked[i]);
            }

            var offers = new List<RunModifierState>();
            foreach (var def in picked) offers.Add(RollParameters(def, layers, rng));
            return offers;
        }

        // Weighted pick among modifiers not yet picked and not sharing a family with one that was.
        // With requireKind, only modifiers of that kind count - and it's skipped (returns true) if
        // one of that kind is already in the list.
        private static bool TryPick(RunModifierDatabase database, System.Random rng, List<RunModifierDefinition> picked, RunModifierKind kind, bool requireKind)
        {
            if (requireKind && picked.Exists(p => p.Kind == kind)) return true;

            var candidates = new List<RunModifierDefinition>();
            float total = 0f;
            foreach (var def in database.Modifiers)
            {
                if (def == null || def.OfferWeight <= 0f || picked.Contains(def)) continue;
                if (requireKind && def.Kind != kind) continue;
                if (def.Family != RunModifierFamily.None && picked.Exists(p => p.Family == def.Family)) continue;
                candidates.Add(def);
                total += def.OfferWeight;
            }
            if (candidates.Count == 0) return false;

            double target = rng.NextDouble() * total;
            foreach (var def in candidates)
            {
                target -= def.OfferWeight;
                if (target > 0) continue;
                picked.Add(def);
                return true;
            }
            picked.Add(candidates[^1]);
            return true;
        }

        // A single modifier with freshly rolled parameters, bypassing the offer rules - for the Dev
        // Panel's "prestige with modifier" buttons.
        public static RunModifierState RollFor(RunModifierDefinition def, LayerConfigProvider layers, int seed)
        {
            var rng = new System.Random(unchecked((int)MapRng.HashCell(seed, 0, 0, 0, 0xDE7)));
            return RollParameters(def, layers, rng);
        }

        private static RunModifierState RollParameters(RunModifierDefinition def, LayerConfigProvider layers, System.Random rng)
        {
            var state = new RunModifierState { ModifierId = def.Id };

            if (def.UsesOreA || def.UsesOreB)
            {
                var pool = OrePool(layers, def.OrePoolMinLayer, def.OrePoolMaxLayer);
                if (pool.Count == 0) Debug.LogError($"RunModifier '{def.Id}' has an empty ore pool (layers {def.OrePoolMinLayer}-{def.OrePoolMaxLayer}).");
                else
                {
                    int a = rng.Next(pool.Count);
                    state.OreA = pool[a];
                    if (pool.Count > 1)
                    {
                        int b = rng.Next(pool.Count - 1);
                        state.OreB = pool[b >= a ? b + 1 : b];
                    }
                }
            }

            if (def.UsesTargetLayer) state.TargetLayer = rng.Next(def.TargetLayerMin, Mathf.Max(def.TargetLayerMin, def.TargetLayerMax) + 1);
            if (def.IsContract) state.ContractAmount = rng.Next(def.ContractAmountMin, Mathf.Max(def.ContractAmountMin, def.ContractAmountMax) + 1);
            return state;
        }

        // Distinct ores authored in the OreTables of layers [minLayer, maxLayer], in table order.
        private static List<BlockTypeId> OrePool(LayerConfigProvider layers, int minLayer, int maxLayer)
        {
            var pool = new List<BlockTypeId>();
            for (int layer = minLayer; layer <= maxLayer; layer++)
            {
                var config = layers.GetConfig(layer);
                if (config == null) continue;
                foreach (var entry in config.OreTable)
                {
                    if (!WeightedTables.IsOreEntry(entry) || entry.Weight <= 0f || pool.Contains(entry.BlockType.Id)) continue;
                    pool.Add(entry.BlockType.Id);
                }
            }
            return pool;
        }
    }
}
