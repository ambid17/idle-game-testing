using Economy;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Resolves the world-side effects of GameDesignDoc "Randomness blocks > hazardous" beyond flat
    // proximity damage - Player.HazardDamageHandler still owns all player damage. This is block
    // destruction (Explosive), spawning a local telegraphed/lingering effect (FallingRock/
    // GasPocket), and marking a persistent surface (Lava). Singleton, event-only coupling, matching
    // PowerUpEffectResolver's structure for the PowerUp side of the same HazardBehavior enum.
    public class HazardEffectResolver : Singleton<HazardEffectResolver>
    {
        [SerializeField] private int explosiveBlastRadius = 2;

        [Tooltip("Optional - if unset, a bare GameObject + component is created at runtime instead.")]
        [SerializeField] private FallingRockHazardEffect fallingRockEffectPrefab;

        [Tooltip("Optional - if unset, a bare GameObject + component is created at runtime instead.")]
        [SerializeField] private GasCloudHazardEffect gasCloudEffectPrefab;

        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private void OnEnable() => GameManager.EventService.Add<HazardTriggeredEvent>(OnHazardTriggered);
        private void OnDisable() => GameManager.EventService.Remove<HazardTriggeredEvent>(OnHazardTriggered);

        private void OnHazardTriggered(HazardTriggeredEvent evt)
        {
            switch (evt.Hazard)
            {
                case CustomBehavior.Explosive: ResolveExplosive(evt); break;
                case CustomBehavior.FallingRock: SpawnFallingRock(evt); break;
                case CustomBehavior.GasPocket: SpawnGasCloud(evt); break;
                case CustomBehavior.Lava: ResolveLava(evt); break;
            }
        }

        // GameDesignDoc "explosive: destroys blocks in a radius... you get to collect the minerals
        // destroyed by the explosion" - destroying a cell reuses MapGenerationService.MineCell, so
        // a destroyed Hazard/PowerUp block dispatches its own HazardTriggeredEvent/
        // PowerUpTriggeredEvent for free (chain reactions between Explosive blocks happen with no
        // extra code, naturally bounded since TryMineCell refuses an already-mined cell). Destroyed
        // ore is auto-credited straight to Wallet rather than added to inventory - there's no
        // guarantee the player (or the automaton that triggered this) is standing close enough to
        // physically collect it.
        private void ResolveExplosive(HazardTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            for (int dy = -explosiveBlastRadius; dy <= explosiveBlastRadius; dy++)
            {
                for (int dx = -explosiveBlastRadius; dx <= explosiveBlastRadius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (dx * dx + dy * dy > explosiveBlastRadius * explosiveBlastRadius) continue;

                    int x = evt.X + dx;
                    int y = evt.Y + dy;
                    var blockType = mapGenerationService.GetBlockTypeAt(evt.LayerIndex, x, y);
                    if (blockType == null) continue;

                    if (!mapGenerationService.MineCell(evt.LayerIndex, x, y)) continue;
                    if (blockType.Category == BlockCategory.Ore) CreditOreValue(blockType);
                }
            }
        }

        private static void CreditOreValue(BlockType blockType)
        {
            if (Wallet.Instance == null || UpgradeManager.Instance == null || PrestigeUpgradeManager.Instance == null) return;

            double value = blockType.Value * UpgradeManager.Instance.SellValueMultiplier * PrestigeUpgradeManager.Instance.MineralValueMultiplier;
            Wallet.Instance.Add(value);
        }

        // GameDesignDoc "falling rocks: mining the terrain under them causes them to fall, damaging
        // the player if they are below it" - telegraph-then-impact is fully owned by the spawned
        // effect; this just places it and lets it run.
        private void SpawnFallingRock(HazardTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            var effect = fallingRockEffectPrefab != null
                ? Instantiate(fallingRockEffectPrefab)
                : new GameObject("FallingRockHazardEffect").AddComponent<FallingRockHazardEffect>();
            effect.transform.position = mapGenerationService.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            effect.Begin(evt.LayerIndex, evt.X, evt.Y);
        }

        // GameDesignDoc "gas pockets: mining releases a damaging/flammable gas cloud" - chain-
        // ignition into Lava/Explosive is deferred to a follow-up pass (real cross-hazard scope);
        // this ships as a standalone expanding/lingering/dissipating damage-over-time cloud.
        private void SpawnGasCloud(HazardTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            var effect = gasCloudEffectPrefab != null
                ? Instantiate(gasCloudEffectPrefab)
                : new GameObject("GasCloudHazardEffect").AddComponent<GasCloudHazardEffect>();
            effect.transform.position = mapGenerationService.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            effect.Begin(evt.LayerIndex, evt.X, evt.Y);
        }

        // GameDesignDoc "Lava: shows up more as you get deeper, damages the player, worth no
        // money" - the mined cell becomes a persistent hazardous surface. Player.HazardDamageHandler
        // applies contact damage-over-time by reading CellData.HazardousSurface directly each
        // frame; no event needed for that part.
        private void ResolveLava(HazardTriggeredEvent evt) => mapGenerationService.MarkHazardousSurface(evt.LayerIndex, evt.X, evt.Y);
    }
}
