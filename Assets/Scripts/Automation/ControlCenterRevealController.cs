using Buildings;
using Economy;
using Events;
using Tutorial;
using UnityEngine;

namespace Automation
{
    // Keeps the Control Center building hidden and non-interactable until the player unlocks
    // their first Mining Automaton, then routes through
    // Tutorial.TutorialManager.TryShow(TutorialId.ControlCenterReveal) for the persisted once-only
    // guarantee, and hands off to Buildings.BuildingRevealCinematicPlayer for the shared reveal
    // cinematic when that ShowTutorialEvent comes back around. Mirrors
    // Processing.ProcessingCenterRevealController / Economy.MuseumRevealController's shape - each
    // owns only its own unlock trigger and building reference; the description text lives in
    // GameManager.TutorialDatabase like every other tutorial. Scene-placed singleton since it needs
    // an Inspector-wired reference to the Control Center building.
    public class ControlCenterRevealController : Singleton<ControlCenterRevealController>
    {
        [SerializeField] private GameObject controlCenterBuilding;

        protected override void Initialize()
        {
            base.Initialize();
            if (controlCenterBuilding == null)
            {
                Debug.LogError("ControlCenterRevealController.controlCenterBuilding is not assigned.");
            }
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<ShowTutorialEvent>(OnShowTutorial);
        }

        private void OnLoadCompleted()
        {
            bool alreadyUnlocked = UpgradeManager.Instance.Automation_AutomatonCount > 0;
            // Self-heals saves from before the Control Center was gated, so it doesn't stay stuck
            // hidden for players who already own an automaton.
            if (alreadyUnlocked) TutorialManager.Instance.TryShow(TutorialId.ControlCenterReveal);
            controlCenterBuilding.SetActive(TutorialManager.Instance.HasShown(TutorialId.ControlCenterReveal));
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            if (evt.Definition.Effect != UpgradeEffect.Automation_AutomatonCount) return;
            TutorialManager.Instance.TryShow(TutorialId.ControlCenterReveal);
        }

        private void OnShowTutorial(ShowTutorialEvent evt)
        {
            if (evt.Entry.Id != TutorialId.ControlCenterReveal) return;
            BuildingRevealCinematicPlayer.Instance.Reveal(controlCenterBuilding, evt.Entry.Body);
        }
    }
}
