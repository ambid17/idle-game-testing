using System.Collections.Generic;
using UnityEngine;

namespace MapGeneration
{
    [CreateAssetMenu(fileName = "BlockTypeDatabase", menuName = "Map Generation/Block Type Database")]
    public class BlockTypeDatabase : ScriptableObject
    {
        public List<BlockType> BlockTypes = new();

        private Dictionary<byte, BlockType> lookup;

        public BlockType Get(byte id)
        {
            if (lookup == null) BuildLookup();
            lookup.TryGetValue(id, out var blockType);
            return blockType;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<byte, BlockType>();
            foreach (var blockType in BlockTypes)
            {
                if (blockType != null) lookup[(byte)blockType.Id] = blockType;
            }
        }

        private void OnEnable() => lookup = null;

        public void Validate()
        {
            if (BlockTypes == null)
            {
                Debug.LogError("BlockTypeDatabase is not assigned in GameManager.");
                return;
            }
            foreach (var blockType in BlockTypes)
            {
                if (blockType == null)
                {
                    Debug.LogError("BlockTypeDatabase contains a null BlockType.");
                    continue;
                }
                if (string.IsNullOrEmpty(blockType.DisplayName))
                {
                    Debug.LogError($"BlockType '{blockType.name}' has an invalid display name.");
                }
                if (blockType.Health <= 0)
                {
                    Debug.LogError($"BlockType '{blockType.name}' has <= 0 health value.");
                }
                if (blockType.Icon == null)
                {
                    Debug.LogError($"BlockType '{blockType.name}' has no icon assigned.");
                }
                if (blockType.Tile == null)
                {
                    Debug.LogError($"BlockType '{blockType.name}' has no tile assigned.");
                }
                if (blockType.Value <= 0 && blockType.Category == BlockCategory.Ore)
                {
                    Debug.LogError($"BlockType '{blockType.name}' has a negative value.");
                }
            }
        }
    }
}
