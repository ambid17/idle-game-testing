using System;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialId
    {
        CoreGoal,
        FirstArtifact,
        Building_Depot,
        Building_Market,
        Building_Museum,
        Building_Processing,
        Building_ControlCenter,
    }

    [Serializable]
    public class TutorialEntry
    {
        public TutorialId Id;
        public string Title;
        [TextArea] public string Body;
    }
}
