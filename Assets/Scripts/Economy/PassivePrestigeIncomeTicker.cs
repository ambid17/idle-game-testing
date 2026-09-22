using UnityEngine;

namespace Economy
{
    // GameDesignDoc "Prestige > Prestige > add passive artifact gain over time": accrues fractional
    // artifacts every frame at PrestigeUpgradeManager.PassiveArtifactRate (artifacts per minute),
    // crediting whole artifacts to the Wallet once the fraction passes 1. No existing ticking
    // component to extend - IdleEarningsTracker and PrestigeManager are both purely event-driven
    // with no Update() - so this is a new one. Singleton so it needs no scene wiring.
    public class PassivePrestigeIncomeTicker : Singleton<PassivePrestigeIncomeTicker>
    {
        private double accumulatedFraction;

        private void Update()
        {
            var prestige = PrestigeUpgradeManager.Instance;
            if (prestige == null) return;

            float ratePerMinute = prestige.PassiveArtifactRate;
            if (ratePerMinute <= 0f) return;

            accumulatedFraction += ratePerMinute / 60.0 * Time.deltaTime;
            if (accumulatedFraction < 1.0) return;

            int wholeArtifacts = (int)System.Math.Floor(accumulatedFraction);
            accumulatedFraction -= wholeArtifacts;
            Wallet.Instance.AddArtifacts(wholeArtifacts);
        }
    }
}
