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
        // Appended (scene prompt rows serialize this enum as an int).
        Critter,
        Building_CritterShop,
        SkyArtifact,
        // A Keystone or the Vault's last Seal (Story.SealChamber).
        SealChamber,
        // The Bound's cave after Reseal (Story.VoidCave): the way home, and the Bound himself.
        VoidCavePortal,
        TheBound,
        // Portal to the Depot beside the Critter Shop (Critters.CritterShopPortal).
        CritterShopPortal,
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
