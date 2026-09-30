using Economy;
using Events;
using MapGeneration;
using UnityEngine;

namespace RunModifiers
{
    // Scene-side access to the active run modifier (the state itself lives on MineWorld, see
    // RunModifierState): the mining/economy multipliers gameplay code reads, the contract tracker,
    // and player-facing text. Lives under the GameManager, accessed via GameManager.RunModifierService.
    public class RunModifierService : MonoBehaviour
    {
        private RunModifierDatabase database => GameManager.RunModifierDatabase;
        private BlockTypeDatabase blockTypes => GameManager.BlockTypeDatabase;

        public RunModifierState ActiveState => GameManager.MapGenerationService.World.RunModifier;
        public RunModifierDefinition ActiveDefinition => database.Get(ActiveState.ModifierId);

        private void OnEnable()
        {
            GameManager.EventService.Add<DepotOreDepositedEvent>(OnDepotOreDeposited);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DepotOreDepositedEvent>(OnDepotOreDeposited);
        }

        // ---- Gameplay multipliers ----

        public float SellValueMultiplier(BlockTypeId ore) => RunModifierResolver.SellValueMultiplier(ActiveDefinition, ActiveState, ore);

        public float BlockHealthMultiplier
        {
            get
            {
                var def = ActiveDefinition;
                return def != null ? def.BlockHealthMultiplier : 1f;
            }
        }

        public float FogRadiusMultiplier(int layerIndex)
        {
            var def = ActiveDefinition;
            return RunModifierResolver.IsTargetLayer(def, ActiveState, layerIndex) ? def.TargetLayerFogRadiusMultiplier : 1f;
        }

        public int OreYieldMultiplier(int layerIndex)
        {
            var def = ActiveDefinition;
            return RunModifierResolver.IsTargetLayer(def, ActiveState, layerIndex) ? def.TargetLayerOreYieldMultiplier : 1;
        }

        // ---- Contract ----

        private void OnDepotOreDeposited(DepotOreDepositedEvent evt)
        {
            var def = ActiveDefinition;
            var state = ActiveState;
            if (def == null || !def.IsContract || state.ContractCompleted || evt.Ore != state.OreA) return;

            state.ContractProgress = Mathf.Min(state.ContractAmount, state.ContractProgress + evt.Amount);
            if (state.ContractProgress >= state.ContractAmount)
            {
                state.ContractCompleted = true;
                Wallet.Instance.AddArtifacts(def.ContractRewardArtifacts);
                GameManager.EventService.Dispatch(new NotificationEvent($"Contract complete! +{def.ContractRewardArtifacts} <color=purple>Stellar Credits</color>", NotificationUrgency.Queued));
            }
            GameManager.EventService.Dispatch<RunModifierChangedEvent>();
        }

        // ---- Text ----

        public string OreName(BlockTypeId id)
        {
            var block = blockTypes.Get((byte)id);
            return block != null ? block.DisplayName : id.ToString();
        }

        // Description with {oreA} {oreB} {layer} {amount} {reward} filled from the rolled state.
        // Layers are shown 1-based, matching the HUD depth readout.
        public string Describe(RunModifierDefinition def, RunModifierState state)
        {
            if (def == null) return "";
            return (def.Description ?? "")
                .Replace("{oreA}", def.UsesOreA ? OreName(state.OreA) : "")
                .Replace("{oreB}", def.UsesOreB ? OreName(state.OreB) : "")
                .Replace("{layer}", (state.TargetLayer + 1).ToString())
                .Replace("{amount}", state.ContractAmount.ToString())
                .Replace("{reward}", def.ContractRewardArtifacts.ToString());
        }

        // One-line HUD summary of the active modifier, or empty when there is none.
        public string ActiveSummary()
        {
            var def = ActiveDefinition;
            if (def == null) return "";
            var state = ActiveState;
            string summary = def.DisplayName;
            if (def.IsContract)
            {
                summary += state.ContractCompleted
                    ? " - complete"
                    : $" - {OreName(state.OreA)} {state.ContractProgress}/{state.ContractAmount}";
            }
            else if (def.UsesTargetLayer) summary += $" (layer {state.TargetLayer + 1})";
            return summary;
        }
    }
}
