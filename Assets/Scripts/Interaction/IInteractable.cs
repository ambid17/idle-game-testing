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
    }
    public interface IInteractable
    {
        InteractableType InteractableType { get; }
        string PromptText { get; }
        void Interact();
    }
}
