using System.Collections.Generic;
using Events;
using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    // Controller focus for one panel or modal. Put it on the GameObject that's toggled active when
    // the panel opens (a panel's "renderer", a modal's root). While it's the most recently opened
    // one and the player is on a gamepad, it keeps the EventSystem selection inside itself: it
    // selects firstSelected (or, if unset/unavailable, the first interactable Selectable found)
    // on open, re-selects if the selection disappears (e.g. a list rebuilt its rows), and hands
    // focus back to the panel underneath - at the button it last had selected - when it closes.
    // On keyboard/mouse it clears the selection instead, so mouse users never see a stuck
    // highlight. The HUD deliberately has none: the left stick moves the player, so it must
    // never also be navigating HUD buttons.
    public class GamepadFocus : MonoBehaviour
    {
        [SerializeField] private Selectable firstSelected;

        // Open GamepadFocus instances in opening order - the last one owns the selection.
        private static readonly List<GamepadFocus> openStack = new();

        private GameObject lastSelected;

        private static bool IsGamepad => GameManager.KeybindService.CurrentScheme == InputScheme.Gamepad;
        private bool IsTopmost => openStack.Count > 0 && openStack[^1] == this;

        // Whether t belongs to the panel that currently owns controller focus - inside it, or an
        // ancestor of it (TabGroupUI often sits on the panel root above its "renderer"). Used by
        // TabGroupUI so only the frontmost panel's tabs react to LB/RB.
        public static bool IsInTopmost(Transform t)
        {
            if (openStack.Count == 0) return false;
            var top = openStack[^1].transform;
            return t.IsChildOf(top) || top.IsChildOf(t);
        }

        // A TMP_Dropdown's option list is spawned as a child of the dropdown, so a selection below
        // a dropdown (rather than the dropdown itself) means its list is open. Cancel then belongs
        // to the dropdown (closing the list), not to PlayerController's close-the-panel handling.
        public static bool IsDropdownListOpen()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var dropdown = selected.GetComponentInParent<TMP_Dropdown>();
            return dropdown != null && dropdown.gameObject != selected;
        }

        private void OnEnable()
        {
            openStack.Remove(this);
            openStack.Add(this);
            lastSelected = null;
            GameManager.EventService.Add<InputSchemeChangedEvent>(OnInputSchemeChanged);
            if (IsGamepad) SelectRemembered();
        }

        private void OnDisable()
        {
            bool wasTopmost = IsTopmost;
            openStack.Remove(this);
            GameManager.EventService.Remove<InputSchemeChangedEvent>(OnInputSchemeChanged);
            if (!wasTopmost || EventSystem.current == null) return;

            if (openStack.Count > 0 && IsGamepad)
            {
                openStack[^1].SelectRemembered();
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void Update()
        {
            if (!IsTopmost || !IsGamepad || EventSystem.current == null) return;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (IsValidSelection(selected))
            {
                lastSelected = selected;
                return;
            }

            SelectRemembered();
        }

        private void OnInputSchemeChanged(InputSchemeChangedEvent evt)
        {
            if (!IsTopmost || EventSystem.current == null) return;

            if (evt.Scheme == InputScheme.Gamepad)
            {
                SelectRemembered();
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void SelectRemembered()
        {
            if (EventSystem.current == null) return;
            var target = IsValidSelection(lastSelected) ? lastSelected : FindDefault();
            EventSystem.current.SetSelectedGameObject(target);
            lastSelected = target;
        }

        private GameObject FindDefault()
        {
            if (firstSelected != null && firstSelected.isActiveAndEnabled && firstSelected.IsInteractable())
            {
                return firstSelected.gameObject;
            }

            // Scrollbars last - landing on a list's scrollbar rather than its first row is useless.
            GameObject scrollbarFallback = null;
            foreach (var selectable in GetComponentsInChildren<Selectable>())
            {
                if (!selectable.isActiveAndEnabled || !selectable.IsInteractable()) continue;
                if (selectable is not Scrollbar) return selectable.gameObject;
                if (scrollbarFallback == null) scrollbarFallback = selectable.gameObject;
            }
            return scrollbarFallback;
        }

        private bool IsValidSelection(GameObject selected)
        {
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform)) return false;
            var selectable = selected.GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }
    }
}
