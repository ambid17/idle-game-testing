using UnityEngine;

namespace Player
{
    // Overlay sprite that PlayerMining positions over the block currently being mined,
    // swapping through crackStages (low->high progress) to show how close it is to breaking.
    public class MiningCrackIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] crackStages;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                Debug.LogError($"{nameof(MiningCrackIndicator)} on {name} is missing its spriteRenderer reference.");
                return;
            }

            Hide();
        }

        public void Show(Vector3 worldPosition, float progress01)
        {
            if (spriteRenderer == null || crackStages == null || crackStages.Length == 0) return;

            transform.position = worldPosition;
            spriteRenderer.enabled = true;

            int stageIndex = Mathf.Clamp(Mathf.FloorToInt(progress01 * crackStages.Length), 0, crackStages.Length - 1);
            spriteRenderer.sprite = crackStages[stageIndex];
        }

        public void Hide()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.enabled = false;
        }
    }
}
