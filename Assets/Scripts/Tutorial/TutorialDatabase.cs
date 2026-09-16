using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    // Tutorial copy per TutorialId - see Tutorial.TutorialManager for when each one fires.
    // Follows Economy.UpgradeDatabase's lazy-lookup-dictionary pattern.
    [CreateAssetMenu(fileName = "TutorialDatabase", menuName = "Tutorial/Tutorial Database")]
    public class TutorialDatabase : ScriptableObject
    {
        public List<TutorialEntry> Entries = new();

        private Dictionary<TutorialId, TutorialEntry> entriesById;

        public bool TryGet(TutorialId id, out TutorialEntry entry)
        {
            if (entriesById == null) BuildLookup();
            return entriesById.TryGetValue(id, out entry);
        }

        private void BuildLookup()
        {
            entriesById = new Dictionary<TutorialId, TutorialEntry>();
            foreach (var entry in Entries)
            {
                if (entry == null) continue;
                entriesById[entry.Id] = entry;
            }
        }
    }
}
