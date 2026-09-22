using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Reuseable
{
    // Generic hover-to-show tooltip: shows tooltipRoot while the pointer is over this
    // GameObject, hides it on exit. A Button's own Image keeps receiving pointer events even
    // while Button.interactable is false, so callers that only want the tooltip available
    // conditionally (e.g. explaining why a feature is locked) toggle Active rather than this
    // component/GameObject, which would also suppress the pointer events entirely.
    public class HoverTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject tooltipRoot;

        public bool Active { get; set; } = true;

        private void Start()
        {
            if (tooltipRoot == null) Debug.LogError($"{nameof(HoverTooltipTrigger)} on {name} is missing its tooltipRoot reference.");
            tooltipRoot.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Active) tooltipRoot.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) => tooltipRoot.SetActive(false);
    }
}
