using Economy;
using Events;
using UnityEngine;

namespace MapGeneration
{
    // Resolves the world-side effects of GameDesignDoc "Randomness blocks > hazardous" beyond flat
    // proximity damage - Player.HazardDamageHandler still owns all player damage. This is block
    // destruction (Explosive) and spawning a local telegraphed/lingering effect (FallingRock/
    // GasPocket). Lava needs no world-side resolution at all - mining it clears the cell like any
    // other block (MapGenerationService.MineCell), and its damage (both the instant mining hit and
    // the per-frame underfoot tick) is entirely owned by Player.HazardDamageHandler. Singleton,
    // event-only coupling.
    public class HazardEffectResolver : Singleton<HazardEffectResolver>
    {
        [SerializeField] private int explosiveBlastRadius = 2;

        [Tooltip("Optional - if unset, a bare GameObject + component is created at runtime instead.")]
        [SerializeField] private ExplosiveHazardEffect explosiveEffectPrefab;

        [Tooltip("Optional - if unset, a bare GameObject + component is created at runtime instead.")]
        [SerializeField] private FallingRockHazardEffect fallingRockEffectPrefab;

        [Tooltip("Optional - if unset, a bare GameObject + component is created at runtime instead.")]
        [SerializeField] private GasCloudHazardEffect gasCloudEffectPrefab;

        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        private void OnEnable()
        {
            GameManager.EventService.Add<CustomBlockTriggeredEvent>(OnHazardTriggered);
            GameManager.EventService.Add<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<CustomBlockTriggeredEvent>(OnHazardTriggered);
            GameManager.EventService.Remove<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
        }

        private void OnHazardTriggered(CustomBlockTriggeredEvent evt)
        {
            switch (evt.Hazard)
            {
                case CustomBehavior.Explosive: SpawnExplosiveEffect(evt); break;
                case CustomBehavior.FallingRock: SpawnFallingRock(evt); break;
                case CustomBehavior.GasPocket: SpawnGasCloud(evt); break;
            }
        }

        private void OnExplosiveDetonated(ExplosiveDetonatedEvent evt) => ResolveExplosive(evt.LayerIndex, evt.X, evt.Y);

        // GameDesignDoc "explosive: destroys blocks in a radius... you get to collect the minerals
        // destroyed by the explosion" - destroying a cell reuses MapGenerationService.MineCell, so
        // a destroyed Hazard block dispatches its own CustomBlockTriggeredEvent for free (chain
        // reactions between Explosive blocks happen with no extra code, naturally bounded since
        // TryMineCell refuses an already-mined cell). PowerUp blocks survive the blast - they're
        // player-only, so MineCell refuses them here. Destroyed
        // ore is auto-credited straight to Wallet rather than added to inventory - there's no
        // guarantee the player (or the automaton that triggered this) is standing close enough to
        // physically collect it. Runs off ExplosiveDetonatedEvent, not HazardTriggeredEvent - see
        // ExplosiveHazardEffect's jiggle/flash telegraph for why the two are a second apart.
        private void ResolveExplosive(int layerIndex, int originX, int originY)
        {
            if (mapGenerationService == null) return;

            for (int dy = -explosiveBlastRadius; dy <= explosiveBlastRadius; dy++)
            {
                for (int dx = -explosiveBlastRadius; dx <= explosiveBlastRadius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (dx * dx + dy * dy > explosiveBlastRadius * explosiveBlastRadius) continue;

                    int x = originX + dx;
                    int y = originY + dy;
                    var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, x, y);
                    if (blockType == null) continue;

                    if (!mapGenerationService.MineCell(layerIndex, x, y)) continue;
                    if (blockType.Category == BlockCategory.Ore) CreditOreValue(blockType);
                }
            }
        }

        private void SpawnExplosiveEffect(CustomBlockTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            var effect = explosiveEffectPrefab != null
                ? Instantiate(explosiveEffectPrefab)
                : new GameObject("ExplosiveHazardEffect").AddComponent<ExplosiveHazardEffect>();
            effect.transform.position = mapGenerationService.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            effect.Begin(evt.LayerIndex, evt.X, evt.Y);
        }

        private static void CreditOreValue(BlockType blockType)
        {
            if (Wallet.Instance == null || UpgradeManager.Instance == null || PrestigeUpgradeManager.Instance == null) return;

            double value = blockType.Value * UpgradeManager.Instance.Economy_SellValueMultiplier * PrestigeUpgradeManager.Instance.Economy_MineralValueMultiplier * PrestigeUpgradeManager.Instance.Prestige_IncomeMultiplier;
            Wallet.Instance.Add(value);
        }

        // GameDesignDoc "falling rocks: mining the terrain under them causes them to fall, damaging
        // the player if they are below it" - telegraph-then-impact is fully owned by the spawned
        // effect; this just places it and lets it run.
        private void SpawnFallingRock(CustomBlockTriggeredEvent evt)
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
        private void SpawnGasCloud(CustomBlockTriggeredEvent evt)
        {
            if (mapGenerationService == null) return;

            var effect = gasCloudEffectPrefab != null
                ? Instantiate(gasCloudEffectPrefab)
                : new GameObject("GasCloudHazardEffect").AddComponent<GasCloudHazardEffect>();
            effect.transform.position = mapGenerationService.CellToWorldCenter(evt.LayerIndex, evt.X, evt.Y);
            effect.Begin(evt.LayerIndex, evt.X, evt.Y);
        }
    }
}
