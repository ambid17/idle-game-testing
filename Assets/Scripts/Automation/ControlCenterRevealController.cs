using System.Collections;
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
        [SerializeField] private AutomationSpawner automationSpawner;
        [SerializeField] private ControlCenterEntrance entrance;

        protected override void Initialize()
        {
            base.Initialize();
            if (controlCenterBuilding == null)
            {
                Debug.LogError("ControlCenterRevealController.controlCenterBuilding is not assigned.");
            }
            if (automationSpawner == null)
            {
                Debug.LogError("ControlCenterRevealController.automationSpawner is not assigned.");
            }
            if (entrance == null)
            {
                Debug.LogError("ControlCenterRevealController.entrance is not assigned.");
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
            // Held from here, not from when the cinematic starts: the reveal waits out the Market
            // panel and the Automatons tutorial first, and the automaton shouldn't be out mining
            // from a building that hasn't appeared yet.
            automationSpawner.HoldAutomatonsInside();
            BuildingRevealCinematicPlayer.Instance.Reveal(controlCenterBuilding, evt.Entry.Body, DeployFirstAutomaton);
        }

        // Plays once the building has materialized: doors open, the first automaton walks out.
        private IEnumerator DeployFirstAutomaton()
        {
            var automaton = automationSpawner.FirstAutomaton;
            if (automaton == null)
            {
                Debug.LogError("ControlCenterRevealController: no automaton to walk out of the Control Center.");
            }
            else
            {
                yield return entrance.DeployThroughDoors(automaton);
            }

            automationSpawner.ReleaseAutomatons();
        }
    }
}
