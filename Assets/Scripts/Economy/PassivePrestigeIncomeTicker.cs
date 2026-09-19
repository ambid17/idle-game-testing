using UnityEngine;

namespace Economy
{
    // GameDesignDoc "Prestige > Prestige > add passive prestige point gain over time": accrues
    // fractional points every frame at PrestigeUpgradeManager.PassivePrestigePointRate (points per
    // minute), crediting whole points to PrestigePoints once the fraction passes 1. No existing
    // ticking component to extend - IdleEarningsTracker and PrestigeManager are both purely
    // event-driven with no Update() - so this is a new one. Singleton so it needs no scene wiring.
    public class PassivePrestigeIncomeTicker : Singleton<PassivePrestigeIncomeTicker>
    {
        private double accumulatedFraction;

        private void Update()
        {
            var prestige = PrestigeUpgradeManager.Instance;
            if (prestige == null) return;

            float ratePerMinute = prestige.PassivePrestigePointRate;
            if (ratePerMinute <= 0f) return;

            accumulatedFraction += ratePerMinute / 60.0 * Time.deltaTime;
            if (accumulatedFraction < 1.0) return;

            double wholePoints = System.Math.Floor(accumulatedFraction);
            accumulatedFraction -= wholePoints;
            PrestigePoints.Instance.Add(wholePoints);
        }
    }
}
