using System.Collections.Generic;
using UnityEngine;

namespace RunModifiers
{
    // Every run modifier that can be offered at prestige.
    [CreateAssetMenu(fileName = "RunModifierDatabase", menuName = "Run Modifiers/Run Modifier Database")]
    public class RunModifierDatabase : ScriptableObject
    {
        public List<RunModifierDefinition> Modifiers = new();

        public RunModifierDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var modifier in Modifiers)
            {
                if (modifier != null && modifier.Id == id) return modifier;
            }
            return null;
        }

        public void Validate()
        {
            if (Modifiers.Count == 0)
            {
                Debug.LogError("RunModifierDatabase has no modifiers.");
                return;
            }

            var seen = new HashSet<string>();
            foreach (var modifier in Modifiers)
            {
                if (modifier == null)
                {
                    Debug.LogError("RunModifierDatabase contains a null modifier.");
                    continue;
                }
                if (string.IsNullOrEmpty(modifier.Id)) Debug.LogError($"RunModifier '{modifier.name}' has no Id.");
                else if (!seen.Add(modifier.Id)) Debug.LogError($"RunModifierDatabase has a duplicate Id '{modifier.Id}'.");
                if (modifier.Features.Contains(null) || modifier.TargetLayerFeatures.Contains(null)) Debug.LogError($"RunModifier '{modifier.Id}' has a null feature.");
            }
        }
    }
}
