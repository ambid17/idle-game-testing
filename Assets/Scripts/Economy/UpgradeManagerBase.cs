using System.Collections.Generic;
using UnityEngine;

namespace Economy
{
    // Shared purchase/level-tracking logic for UpgradeManager and PrestigeUpgradeManager - the two
    // families differ only in currency source, save key (DisplayName vs Id), event type, and (for
    // UpgradeManager only) a "kept tier" baseline on top of purchased levels. Everything else
    // (CanPurchase/TryPurchase/IsMaxed/IsUnlocked/GetNextCost/level lookups) lives here once.
    // CRTP (TSelf) is needed because Singleton<T> requires the concrete MonoBehaviour type.
    public abstract class UpgradeManagerBase<TSelf, TDefinition, TEffect> : Singleton<TSelf>
        where TSelf : UpgradeManagerBase<TSelf, TDefinition, TEffect>
        where TDefinition : UpgradeDefinitionBase
    {
        private readonly Dictionary<string, int> levels = new();

        protected abstract double CurrentCurrency { get; }
        protected abstract bool TrySpendCurrency(double amount);
        protected abstract string KeyOf(TDefinition def);
        protected abstract TDefinition Find(TEffect effect);
        protected abstract TDefinition Find(string key);
        protected abstract TDefinition PrerequisiteOf(TDefinition def);
        protected abstract void DispatchPurchased(TDefinition def, int newLevel);

        // PrestigeUpgradeManager fires the same event for a live purchase and a save restore
        // (comment on its old SetLevel: "every listener reacts identically"); UpgradeManager
        // overrides this to fire UpgradeLoadedEvent instead, matching its previous behavior.
        protected virtual void DispatchLoaded(TDefinition def, int newLevel) => DispatchPurchased(def, newLevel);

        protected int RawLevel(TDefinition def) => def != null && levels.TryGetValue(KeyOf(def), out var lvl) ? lvl : 0;

        // UpgradeManager overrides this to add its PrestigeUpgradeManager "kept tier" baseline on
        // top of RawLevel. PrestigeUpgradeManager has no such baseline, so it uses this default.
        // This is the level gameplay effects should read (LevelOf) - for PrestigeUpgradeManager
        // that means applied/committed levels only, never a not-yet-prestiged queued purchase.
        protected virtual int EffectiveLevel(TDefinition def) => RawLevel(def);

        // Level used for purchase-time bookkeeping (next cost, maxed-out gating, whether a
        // prerequisite counts as unlocked) - defaults to EffectiveLevel. PrestigeUpgradeManager
        // overrides this to also count not-yet-applied queued levels, so a player can plan/queue an
        // entire branch before any of it actually takes effect, without letting EffectiveLevel (and
        // therefore the live gameplay value) move early.
        protected virtual int PurchaseLevel(TDefinition def) => EffectiveLevel(def);

        protected int LevelOf(TEffect effect) => EffectiveLevel(Find(effect));

        protected float EffectValuePerLevelOf(TEffect effect)
        {
            var def = Find(effect);
            if (def == null)
            {
                Debug.LogError($"{typeof(TSelf).Name}.EffectValuePerLevelOf: No {typeof(TDefinition).Name} found for effect {effect}. Check that the database is properly populated.");
                return 0;
            }
            return def.EffectValuePerLevel;
        }

        protected bool IsMaxedEffect(TEffect effect)
        {
            var def = Find(effect);
            if (def == null)
            {
                Debug.LogError($"{typeof(TSelf).Name}.IsMaxedEffect: No {typeof(TDefinition).Name} found for effect {effect}. Check that the database is properly populated.");
                return true;
            }
            return IsMaxed(def);
        }

        public bool IsMaxed(TDefinition def) => def != null && PurchaseLevel(def) >= def.MaxLevel;

        // A plain prerequisite just needs one level purchased; capstones (RequirePrerequisiteMaxed)
        // need the prerequisite fully maxed first.
        public bool IsUnlocked(TDefinition def)
        {
            if (def == null) return false;
            var prerequisite = PrerequisiteOf(def);
            if (prerequisite == null) return true;
            return def.RequirePrerequisiteMaxed ? IsMaxed(prerequisite) : PurchaseLevel(prerequisite) > 0;
        }

        public double GetNextCost(TDefinition def) => def.GetCost(PurchaseLevel(def));

        public bool CanPurchase(TDefinition def)
        {
            if (def == null || IsMaxed(def) || !IsUnlocked(def)) return false;
            return CurrentCurrency >= GetNextCost(def);
        }

        public string GetPurchaseBlockedReason(TDefinition def)
        {
            if (def == null)
                return "Invalid Definition";
            if (IsMaxed(def))
                return "Maxed";
            if(!IsUnlocked(def))
                return $"Requires {def.Prerequisite.DisplayName}";
            if (CurrentCurrency < GetNextCost(def))
                return $"Insufficient Funds";
            return null;
        }

        public bool TryPurchase(TDefinition def)
        {
            if (!CanPurchase(def)) return false;

            double cost = GetNextCost(def);
            if (!TrySpendCurrency(cost)) return false;

            int newLevel = PurchaseLevel(def) + 1;
            RecordPurchase(def, KeyOf(def), newLevel);
            return true;
        }

        // Writes a newly-bought level. UpgradeManager (Market) keeps the default: apply immediately
        // and fire DispatchPurchased. PrestigeUpgradeManager overrides this to write into a separate
        // queued-levels store instead of levels/EffectiveLevel, so the purchase is paid for now but
        // has no gameplay effect until PrestigeManager.ExecutePrestige commits the queue.
        protected virtual void RecordPurchase(TDefinition def, string key, int newLevel)
        {
            levels[key] = newLevel;
            DispatchPurchased(def, newLevel);
        }

        // Bulk restore for SaveService. Unlike UpgradeManager's old un-guarded SetLevelFromSave,
        // this discards a level for a key with no matching definition (stale save data - a renamed
        // or removed upgrade) instead of dispatching an event with a null Definition.
        public void SetLevel(string key, int level)
        {
            if (string.IsNullOrEmpty(key) || level < 0) return;

            var def = Find(key);
            if (def == null)
            {
                Debug.LogError($"{typeof(TSelf).Name}.SetLevel: no {typeof(TDefinition).Name} found for key '{key}'. Save data may be stale (renamed/removed upgrade) - level discarded.");
                return;
            }

            levels[key] = level;
            DispatchLoaded(def, level);
        }

        public IEnumerable<KeyValuePair<string, int>> AllLevels => levels;

        protected void ClearLevels() => levels.Clear();
    }
}
