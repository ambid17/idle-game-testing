using System;
using System.Collections.Generic;
using Events;
using Settings;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Generic button <-> content-root tab switcher, used by ControlCenterUI's four dashboards.
    // Chosen over MarketUI's filter-by-UpgradeBranch trick because those tabs have structurally
    // different content (graph, targeting control, slider), not a homogeneous list of one prefab.
    public class TabGroupUI : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button Button;
            public GameObject ContentRoot;
        }

        [SerializeField] private List<Tab> tabs = new();
        [SerializeField] private int defaultTabIndex;

        // Opt-in (defaults false so existing consumers like ControlCenterUI are unaffected) -
        // UI.DevPanelUI is the first consumer to set this, per CLAUDE.md's tab-coloring rule.
        [SerializeField] private bool colorTabs = false;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = new(0.7f, 0.7f, 0.7f);

        // Set on a group that lives inside another group's tab (Control Center drone dashboards):
        // it cycles on LT/RT so one LB/RB press doesn't switch both groups at once.
        [SerializeField] private bool isSubGroup = false;

        // Button-prompt icons at either end of the tab bar (LB/RB, or LT/RT for a sub group),
        // shown only while the player is on a controller.
        [SerializeField] private GameObject previousTabHint;
        [SerializeField] private GameObject nextTabHint;

        private int currentIndex;

        private void Start()
        {
            if (previousTabHint == null) Debug.LogError($"TabGroupUI.previousTabHint is not assigned on {name}.");
            if (nextTabHint == null) Debug.LogError($"TabGroupUI.nextTabHint is not assigned on {name}.");

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture for the closure
                if (tabs[i].Button == null) continue;
                tabs[i].Button.onClick.AddListener(() => SelectTab(index));

                // Tabs are switched with the bumpers on a controller, never by moving the
                // selection onto them: Navigation.None takes them out of d-pad/stick navigation
                // (and GamepadFocus won't pick them as a default), mouse clicks still work.
                var navigation = tabs[i].Button.navigation;
                navigation.mode = Navigation.Mode.None;
                tabs[i].Button.navigation = navigation;
            }

            SelectTab(defaultTabIndex);

            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
            SetHintsVisible(GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad);
        }

        private void OnDestroy()
        {
            GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);
        }

        // Controller LB/RB cycle tabs, but only in the frontmost panel (see GamepadFocus), so a
        // panel underneath an open modal doesn't switch tabs behind it.
        private void Update()
        {
            if (tabs.Count < 2 || !GamepadFocus.IsInTopmost(transform)) return;

            var keybinds = GameManager.KeybindService;
            bool next = isSubGroup ? keybinds.WasSubTabNextPressedThisFrame() : keybinds.WasTabNextPressedThisFrame();
            bool previous = isSubGroup ? keybinds.WasSubTabPreviousPressedThisFrame() : keybinds.WasTabPreviousPressedThisFrame();
            if (next) CycleTab(1);
            else if (previous) CycleTab(-1);
        }

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt)
        {
            SetHintsVisible(evt.Scheme == InputScheme.Gamepad);
        }

        private void SetHintsVisible(bool visible)
        {
            visible &= tabs.Count >= 2;
            previousTabHint.SetActive(visible);
            nextTabHint.SetActive(visible);
        }

        // Skips tabs whose button is hidden or non-interactable (e.g. locked dashboards).
        private void CycleTab(int direction)
        {
            for (int step = 1; step < tabs.Count; step++)
            {
                int index = ((currentIndex + direction * step) % tabs.Count + tabs.Count) % tabs.Count;
                var button = tabs[index].Button;
                if (button != null && (!button.isActiveAndEnabled || !button.interactable)) continue;

                SelectTab(index);
                return;
            }
        }

        public void SelectTab(int index)
        {
            currentIndex = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].ContentRoot != null) tabs[i].ContentRoot.SetActive(i == index);

                if (colorTabs && tabs[i].Button != null && tabs[i].Button.targetGraphic != null)
                {
                    tabs[i].Button.targetGraphic.color = i == index ? activeTabColor : inactiveTabColor;
                }
            }
        }
    }
}
