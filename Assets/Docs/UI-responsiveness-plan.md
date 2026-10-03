UI responsiveness plan: PC and Steam Deck

What the UI does today

- The main Canvas doesn't scale with the screen. It uses Constant Pixel Size at scale factor 1. All 20+ panels (HUD, Depot, Market, Museum, Processing, Options, Main Menu and the rest) live on this one canvas. The UI was laid out at about 1920×1080 (the project's default resolution), so:
  - 1280×800 (Steam Deck): every element keeps its 1080p pixel size on a screen with 74% of the height. Layouts get squeezed, for example the Depot interior's -160 inset, the 1634-unit skill tree view and the 760-unit run-modifier label.
  - 1440p and 4K: the UI shrinks to tiny.
- Two world-space canvases (the player's InteractionPrompt and the World Tutorial Popup) have scalers set to 1920×1080 and 800×600. Scalers barely matter in world space, so these are low priority.
- A third scaling setup: ReleaseCinematic.cs:492 builds its own Scale With Screen Size canvas at runtime. Assets/UI Toolkit/PanelSettings.asset (1200×800) looks unused.
- Code that will break once scaling is on: HudFlyIconsUI sets sizeDelta = coinSize * Screen.height. That mixes screen pixels with canvas units, so coins would be scaled twice.
- Tooltips aren't kept on screen. Neither the skill-tree tooltip nor the HoverTooltipTrigger tooltip is clamped to the screen edges.
- Small text: about 24 labels use font size 14–16 and many more use 18. 51 TMP labels have auto-size on, and their minimum sizes aren't audited. Once the UI scales down to 1280×800, these drop below Valve's Deck legibility guideline (smallest characters about 9px tall at 1280×800).
- Good news:
  - The panels follow the stretch-root → renderer pattern consistently, and there are many layout groups (about 130 H/V groups and 230 LayoutElements). Most fixes are about setting sizes, not restructuring.
  - Gamepad navigation, keybind glyphs and ScrollToSelected already exist.
  - The only text inputs are in the Developer Panel, so players never need the Deck's on-screen keyboard.

Target model (one rule for every resolution)

- Main Canvas: Scale With Screen Size, reference 1920×1080, Screen Match Mode Expand. Expand guarantees that a 1920×1080 layout always fits: on 16:10 the canvas becomes 1920×1200, on ultrawide it becomes 2560×1080.
- UI Scale setting (80–150%) multiplies on top of that. It defaults to 100% on PC and 125% on Steam Deck, detected with SteamUtils.IsSteamRunningOnSteamDeck().
- Design floor: every panel must fit in 1536×864 canvas units. That is the worst case: 1080p at 125%, which also covers the Deck at 125% (1536×960).
- Text tiers (canvas units): Title 40 / Header 28 / Body 22 / Small 20 minimum, and auto-size minimum ≥ 20. On the Deck at 125%, 20 units is about 17px, which clears the legibility guideline. I still need to check this against Orbitron's x-height on a real screenshot.
- Interactive targets are at least 48 units, for the Deck touchscreen and gamepad highlights.

Phases

Phase 0: baseline audit (no behaviour changes)
1. An Editor audit script that lists:
   - text below the size floor
   - auto-size minimums below the floor
   - rects wider or taller than 1536×864 that use fixed sizes
   - fixed-size children inside stretch parents
   - tooltips and popups that aren't clamped
2. A Play Mode screenshot pass that opens each panel at 1280×800, 1920×1080, 1920×1200, 2560×1440, 3840×2160 and 2560×1080. It has to run in Play Mode because the capture tool misses overlay UI otherwise. Back up save.json first, and check that you aren't play-testing before anything recompiles.

Phase 1: scaling foundation
1. A UIScaleService singleton under GameManager (GameManager.UIScaleService). It owns the main CanvasScaler: it applies Expand with 1920×1080 times the user scale and re-applies when the resolution changes.
2. Add UIScale to SettingsService (PlayerPrefs), with the Steam Deck default on first run. Add a gamepad-friendly stepper on the Options Video tab.
3. Fix HudFlyIconsUI to divide sizes by canvas.scaleFactor, or convert to canvas units.
4. Point ReleaseCinematic's runtime canvas at the same reference resolution and match mode. Delete the unused PanelSettings asset after confirming nothing references it.
5. Add a shared ClampToCanvas helper for tooltips (skill tree, HoverTooltipTrigger, run-modifier tooltip).

Phase 2: written conventions
- Add a "Responsiveness" section to Assets/Scripts/UI/CLAUDE.md covering:
  - the 1536×864 floor
  - the text tiers
  - root content uses anchors plus LayoutElement preferred/flexible sizes, never fixed widths above the floor
  - long lists always go in a ScrollRect with ScrollToSelected
  - tooltips use ClampToCanvas
  - the 48-unit minimum target size
- Turn the Phase 0 audit into an EditMode test so changes that break these rules fail.

Phase 3: per-panel pass, highest risk first. I'd do the layout edits in the Editor, or via MCP with verification, not by hand-editing the scene YAML.
1. HUD: corner clusters, minimap and status bars on 16:10; the analyzer readout and run-modifier label widths.
2. Market and Museum skill trees: fit-to-view initial zoom in SkillTreePanZoomUI, and legend and currency placement.
3. Depot, Processing (both tabs and the recipe modal), Control Center dashboards, Critter Shop.
4. Options and Keybinds, Main Menu, Pause, Death, Offline Earnings.
5. Dialog, Story Choice, Tutorial modal and popup, notifications, building reveal text.
6. The Developer Panel last, or exempt it because it's dev-only.

Phase 4: Steam Deck specifics
- Boot fullscreen at the native 1280×800. defaultIsNativeResolution is already on, so confirm the saved-resolution path doesn't override it on first run.
- Make sure the resolution dropdown lists 16:10 modes.
- Make the Deck show controller glyphs by default.
- Do a touchscreen sanity pass. uGUI handles touch, but hover-only tooltips need a tap or focus alternative.

Phase 5: verification
- Re-run the screenshot pass and compare it with Phase 0.
- Add a Play Mode test that steps through the resolutions and asserts that every open panel's corners are inside the screen and no TMP label is overflowing (isTextOverflowing).
- Test on a real Deck, or at least at 1280×800 at 7" viewing distance, before calling it done.

Decisions for you

1. UI Scale option: I recommend adding it. It's cheap once the service exists, and it's the cleanest way to give the Deck bigger text without making 1080p look oversized.
2. Ultrawide (21:9): I'd treat it as "must not break" (Expand covers this) rather than designing layouts specifically for it.
3. Hardware: do you have a Deck to test on? If not, Phase 5 relies on simulating 1280×800 in the Game view.

I haven't changed anything or run anything in the Editor; everything above comes from reading the scene YAML and scripts. If the plan looks right, I'd start with Phase 0 and Phase 1: they're low-risk, and the audit shows exactly how much Phase 3 work there is.