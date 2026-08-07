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
    }
}
