namespace Interaction
{
    public enum InteractableType
    {
        Building_Depot,
        Building_Market,
        Building_Museum,
        Building_Processing,
        Building_ControlCenter,
        Chest,
        // Not a world object - PlayerInteractionDetector shows this row itself while the player
        // is stranded out of fuel waiting on a Fuel Drone (see PlayerController.IsStrandedWithoutFuel).
        OutOfFuel,
    }

    public enum InteractionType
    {
        None,
        Primary,
        Secondary,
        Tertiary
    }

    public interface IInteractable
    {
        InteractableType InteractableType { get; }
    }
}
