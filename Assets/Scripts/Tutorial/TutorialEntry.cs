using System;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialId
    {
        CoreGoal,
        Building_Depot,
        Building_Market,
        Building_Museum,
        Building_Processing,
        Building_ControlCenter,
        MuseumReveal,
        ProcessingReveal,
    }

    // Which UI component should render this tutorial when TutorialManager dispatches it - each of
    // UI.TutorialModalUI / UI.WorldTutorialPopupUI / Economy.MuseumRevealController /
    // Processing.ProcessingCenterRevealController filters ShowTutorialEvent by this field to decide
    // "is this mine?".
    public enum TutorialDisplayType
    {
        Modal,
        WorldPopup,
        BuildingReveal,
    }

    [Serializable]
    public class TutorialEntry
    {
        public TutorialId Id;
        public TutorialDisplayType DisplayType = TutorialDisplayType.Modal;
        public string Title;
        [TextArea] public string Body;
    }
}
