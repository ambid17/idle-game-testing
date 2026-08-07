namespace Events
{
    public class CurrencyUpdatedEvent { }
    public class PlayerDiedEvent { }
    public class PlayerRevivedEvent { }
    public class PlayerHpUpdatedEvent { }

    public class DidCraftUpgradeEvent { }

    public class ClosedCraftingUiEvent { }
    

    public class CurrencyRewardEvent: IEvent
    {
        public float MinutesAway;
        public float Award;

        public CurrencyRewardEvent(float minutesAway, float award)
        {
            MinutesAway = minutesAway;
            Award = award;
        }
    }

    public class SceneIsReadyEvent : IEvent
    {
        public SceneIsReadyEvent()
        {
        }
    }
}
