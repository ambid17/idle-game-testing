using System;
using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    [Serializable]
    public class SoundEntry
    {
        public SoundId Id;
        [Tooltip("One is picked at random per play - add a few variations for frequent sounds (mining, hits) so they don't get grating.")]
        public AudioClip[] Clips;
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("Random +/- pitch offset per play. Looping sounds ignore this.")]
        [Range(0f, 0.5f)] public float PitchVariance = 0.05f;
        [Tooltip("Plays requested sooner than this after the last one are dropped - stops bursts (vein mining, chain reactions) from stacking into one loud blast.")]
        public float MinInterval = 0.05f;
    }

    // SoundId -> clip(s) for Audio.AudioService, plus the background music track. SoundIds with
    // no entry are allowed and simply play nothing, so audio can be filled in incrementally.
    // Follows Tutorial.TutorialDatabase's lazy-lookup-dictionary pattern.
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Audio/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        public List<SoundEntry> Sounds = new();
        public AudioClip Music;
        [Range(0f, 1f)] public float MusicVolume = 0.5f;

        private Dictionary<SoundId, SoundEntry> entriesById;

        public bool TryGet(SoundId id, out SoundEntry entry)
        {
            if (entriesById == null) BuildLookup();
            return entriesById.TryGetValue(id, out entry);
        }

        private void BuildLookup()
        {
            entriesById = new Dictionary<SoundId, SoundEntry>();
            foreach (var entry in Sounds)
            {
                if (entry == null || entry.Id == SoundId.None || entry.Clips == null || entry.Clips.Length == 0) continue;
                entriesById[entry.Id] = entry;
            }
        }

        public void Validate()
        {
            var seen = new HashSet<SoundId>();
            foreach (var entry in Sounds)
            {
                if (entry == null) continue;
                if (entry.Id == SoundId.None)
                {
                    Debug.LogError("SoundLibrary has an entry with SoundId.None.");
                    continue;
                }
                if (!seen.Add(entry.Id))
                {
                    Debug.LogError($"SoundLibrary has more than one entry for {entry.Id} - only the last is used.");
                }
                if (entry.Clips == null || entry.Clips.Length == 0 || Array.Exists(entry.Clips, clip => clip == null))
                {
                    Debug.LogError($"SoundLibrary entry {entry.Id} has no clips or an empty clip slot.");
                }
            }
        }

#if UNITY_EDITOR
        // Rebuild on inspector edits so clips added during Play Mode are heard immediately.
        private void OnValidate() => entriesById = null;
#endif
    }
}
