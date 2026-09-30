using System;
using MapGeneration;

namespace RunModifiers
{
    // The active run modifier plus its rolled parameters - owned by MineWorld (it shapes the
    // world's generation) and saved alongside the seed in map.json, so restored/regenerated chunks
    // come out identical. Empty ModifierId = no modifier (first run, or a save from before modifiers).
    [Serializable]
    public class RunModifierState
    {
        public string ModifierId;
        public BlockTypeId OreA;
        public BlockTypeId OreB;
        public int TargetLayer = -1;

        public int ContractAmount;
        public int ContractProgress;
        public bool ContractCompleted;

        // How many times the NEXT prestige's offers have been rerolled this run (see
        // PrestigeManager) - lives here so it survives a reload and can't be reset by reopening.
        public int OfferRerollsUsed;

        public bool IsActive => !string.IsNullOrEmpty(ModifierId);

        public RunModifierState Clone() => (RunModifierState)MemberwiseClone();
    }
}
