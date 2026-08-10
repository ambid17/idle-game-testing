using UnityEngine;

namespace UI.SkillTree
{
    // A prerequisite edge, drawn as a thin Image stretched and rotated between two node
    // positions - the standard uGUI "line via rotated Image" trick, since the project has no
    // line-renderer/graph package. Purely cosmetic; safe to omit without affecting layout/logic.
    [RequireComponent(typeof(RectTransform))]
    public class SkillTreeConnectorUI : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private float thickness = 4f;

        public void SetEndpoints(Vector2 from, Vector2 to)
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;

            Vector2 diff = to - from;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            rectTransform.anchoredPosition = from + diff / 2f;
            rectTransform.sizeDelta = new Vector2(length, thickness);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
