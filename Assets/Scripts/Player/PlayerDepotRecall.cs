using Economy;
using UnityEngine;

namespace Player
{
    // Active ability unlocked by the Museum's Survival_DepotRecall perk: portals the player to
    // the Depot (PlayerPortalTravel, shared with the Portal power-up). The base cooldown is scaled by
    // PrestigeUpgradeManager.Survival_DepotRecallCooldownMultiplier (halved per level past the
    // first), read at the moment of use. Works while stranded without fuel, since getting out of a
    // hole is the point. The cooldown isn't saved - a reload starts it ready. Input is handled by
    // PlayerAbilities.
    [RequireComponent(typeof(PlayerPortalTravel))]
    public class PlayerDepotRecall : PlayerAbility
    {
        [SerializeField] private float baseCooldownSeconds = 300f;

        private PlayerPortalTravel portalTravel;

        private PrestigeUpgradeManager prestigeUpgrades => PrestigeUpgradeManager.Instance;

        public override string DisplayName => "Depot Recall";
        // Same icon as the Museum perk that unlocks it.
        public override Sprite Icon => GameManager.PrestigeUpgradeDatabase.Find(PrestigeUpgradeEffect.Survival_DepotRecall).Icon;
        public override bool IsUnlocked => prestigeUpgrades.Survival_DepotRecallUnlocked;
        public override float CooldownSeconds => baseCooldownSeconds * prestigeUpgrades.Survival_DepotRecallCooldownMultiplier;

        private void Awake()
        {
            portalTravel = GetComponent<PlayerPortalTravel>();
        }

        // Refused (no cooldown spent) while a trip is already underway.
        protected override bool Activate() => portalTravel.TryTravelToDepot();
    }
}
