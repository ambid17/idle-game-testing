using Economy;
using Events;
using MapGeneration;
using Settings;
using UnityEngine;

namespace Player
{
    // Active ability unlocked by the Museum's Hazard_Analyzer perk: scans the block in front of the
    // player (the one PlayerMining would dig - below while holding down, otherwise the side the
    // player last moved toward) and dispatches a BlockAnalyzedEvent for UI.AnalyzerReadoutUI.
    // Dirt and empty cells (air, or the surface where buildings sit) report nothing and don't start
    // the cooldown. Readout text comes from BlockType.AnalyzerLines; ores also show their current
    // per-unit sale value. Input is handled by PlayerAbilities.
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerMining))]
    public class PlayerAnalyzer : PlayerAbility
    {
        [SerializeField] private float cooldownSeconds = 2f;

        private PlayerController playerController;
        private PlayerMining playerMining;
        private int facingX = 1;

        private MapGenerationService mapGenerationService => GameManager.MapGenerationService;

        public override string DisplayName => "Analyzer";
        // Same icon as the Museum perk that unlocks it.
        public override Sprite Icon => GameManager.PrestigeUpgradeDatabase.Find(PrestigeUpgradeEffect.Hazard_Analyzer).Icon;
        public override bool IsUnlocked => PrestigeUpgradeManager.Instance.Hazard_AnalyzerUnlocked;
        public override float CooldownSeconds => cooldownSeconds;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerMining = GetComponent<PlayerMining>();
        }

        private void Update()
        {
            float inputX = playerController.MovementInput.x;
            if (inputX != 0f) facingX = inputX > 0f ? 1 : -1;
        }

        protected override bool Activate()
        {
            var direction = GameManager.KeybindService.IsPressed(GameAction.MoveDown) ? Vector2Int.down : new Vector2Int(facingX, 0);
            if (!playerMining.TryResolveTargetCell(direction, out int layerIndex, out int x, out int y)) return false;

            var blockType = mapGenerationService.GetBlockTypeAt(layerIndex, x, y);
            if (blockType == null || blockType.Category == BlockCategory.Dirt) return false;

            string body = BuildReadout(blockType);
            if (body == null) return false;

            GameManager.EventService.Dispatch(new BlockAnalyzedEvent(blockType, body));
            return true;
        }

        private static string BuildReadout(BlockType blockType)
        {
            if (blockType.AnalyzerLines == null || blockType.AnalyzerLines.Length == 0)
            {
                Debug.LogError($"{nameof(PlayerAnalyzer)}: BlockType '{blockType.name}' has no AnalyzerLines.");
                return null;
            }

            string line = blockType.AnalyzerLines[Random.Range(0, blockType.AnalyzerLines.Length)];
            if (blockType.Category != BlockCategory.Ore) return line;

            // Same multiplier stack Depot applies when selling.
            double value = blockType.Value * UpgradeManager.Instance.Economy_SellValueMultiplier(blockType) * PrestigeUpgradeManager.Instance.Economy_MineralValueMultiplier * PrestigeUpgradeManager.Instance.Prestige_IncomeMultiplier * GameManager.RunModifierService.SellValueMultiplier(blockType.Id);
            return $"{line}\n<color=#7CFC7C>Value: ${value:0} each</color>";
        }
    }
}
