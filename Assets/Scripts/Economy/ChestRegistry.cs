using System.Collections.Generic;

namespace Economy
{
    // Tracks every currently-spawned Chest so SaveService can persist position/contents without a
    // scene-wide Find. Pure-logic singleton, not GameManager-registered, matching
    // Wallet/Depot/UpgradeManager/Automation.OreCarrierRegistry.
    public class ChestRegistry : Singleton<ChestRegistry>
    {
        private readonly List<Chest> activeChests = new();

        public IReadOnlyList<Chest> ActiveChests => activeChests;

        public void Register(Chest chest)
        {
            if (chest == null || activeChests.Contains(chest)) return;
            activeChests.Add(chest);
        }

        public void Unregister(Chest chest)
        {
            if (chest == null) return;
            activeChests.Remove(chest);
        }
    }
}
