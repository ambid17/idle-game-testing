Context

The game had no long-term goal beyond earning money. We adopted "The Seals" story (GameDesignDoc # Story & Endgame: The Seals): artifacts are wards holding back the Bound, and every Resonance spends them. An earlier version of this plan had guardian boss fights and crafted weapons; that was dropped before any of it was built. The settled design:
- No combat. Drill tiers are the wall between biomes.
- Two endings, Release and Reseal, chosen at the Vault. Earlier Keystone choices set the price of Reseal and never lock an ending out.
- Release destroys the world and ends the game, but is never saved: Continue returns to the Vault with the choice un-made. Reseal is permanent.
- The Bound is ambiguous: the hints are written so neither the curator nor the voice is confirmed right.
- Story progress is permanent and survives Resonance.

Where things live

- Assets/Scripts/Story/
  - StoryManager.cs: the story state (GameManager.StoryManager, a child of the GameManager object). Stage from the deepest layer reached, Keystones taken, chambers examined, whispers heard, Retranslation seen, ending. Opens the Bound's whisper dialogs (queued until no panel or cinematic has the screen) and fires tremors, runs the ending sequence, exposes the post-game multipliers.
  - StoryContent.cs: every line of story text as one ScriptableObject (Assets/ScriptableObjects/Story/StoryContent.asset). Field defaults are the shipped script.
  - SealChamber.cs: the Keystone / last Seal world object. One per chamber in the scene; each finds its room in the generated chunk and stands in it. Hidden for good once Ending == Reseal.
  - ReleaseCinematic.cs: the Release ending (scene object StoryEndings/ReleaseCinematic). Seal shatters, Bound's dialog, portal to the surface, sky grading Volume, the Bound rising, bolts, buildings blown away, end card built in code, scene reload.
  - VoidCave.cs: the Reseal ending and the cave itself (scene object StoryEndings/VoidCave at (15, 400), far above the map where every map system reads open sky). Toggles its Contents child; the portal home and the Bound are BuildingInteractables (InteractableType.VoidCavePortal / TheBound) with their own prompt rows.
- Player.PlayerPortalTravel.TryTravelTo: the Depot Recall portal trip to any point, with a callback at the moment of the teleport (camera cut + flash). All three story teleports use it.
- Art: Assets/Textures/Story/TheBound.png and VoidCave.png, cut from the raw AI sheets in Tools/Story/source by Tools/Story/make_ending_art.py.
- Assets/Scripts/UI/Panels/StoryChoiceUI.cs: the take/leave and Release/Reseal modal.
- Persistence: GameSaveData.Story (StorySaveData). PrestigeManager.ExecutePrestige never touches it.
  - Release: StoryManager.BeginRelease sets ReleaseWitnessed, saves, then sets SaveService.SavingSuspended so nothing of the destruction reaches the disk. The cinematic sets StoryManager.RewoundThisSession before reloading the scene, which queues the Bound's "you saw" lines after the load. StoryEnding.Release is never stored (old saves that have it load as witnessed + un-made).
  - Reseal: Ending = Reseal, then VoidCaveSpeechHeard and VoidCaveLeft. A save made inside the cave reloads inside it; leaving it any other way (respawn, Depot Recall) counts as leaving.
- Map generation: Structure_SealChamber + Feature_SealChamber on LayerConfigs 2, 5 and 8; Structure_Vault + Feature_Vault on LayerConfig 10 (MaxLayer 10, so the repeated layers below don't get one). Ordinary StructureStampFeatures, first in each layer's Features list so they place before the other set-pieces. Generation stays a pure function of the seed: the room is always there, only the Keystone's state changes.

Stages

StoryStage is derived, not stored: deepest layer index >= 3 Warnings, >= 6 Retranslation, >= 9 Vault. The curator's Retranslation conversation plays on the first Museum visit at that stage and sets RetranslationSeen, which is what the rune cards and post-twist curator lines key off.

Hooks into existing systems

- Museum.MuseumCuratorController: stage greetings and Talk lines, the Retranslation conversation, true rune readings on turn-in.
- UI.RuneSlotUI: joke reading struck through over the true one once RetranslationSeen.
- UI.MuseumPrestigeConfirmUI: prompt text from StoryManager.ResonancePrompt.
- Critters.CritterShopController: stage Talk lines.
- Player.PlayerHealth.TakeDamage: x StoryManager.DamageTakenMultiplier (Reseal).
- Platform.AchievementManager: EndingRelease / EndingReseal (ACH_ENDING_RELEASE / ACH_ENDING_RESEAL - need Steamworks dashboard entries).
- Interaction.InteractableType.SealChamber: one prompt row for Keystones and the Vault.
- Interaction.PlayerInteractionDetector: no prompts while StoryManager.IsEndingPlaying.

Tuning (all on the StoryManager component)

- keystoneRewards: 15 / 30 / 60
- resealBaseCost 20, resealCostPerTakenArtifact 2
- resealDamageTakenMultiplier 0.75
- cinematic timings, bolt rate and colours are on ReleaseCinematic; cave glints and the mend timing on VoidCave
- tremor interval and force

Testing

StoryManager has ContextMenu entries (Dev: Advance Stage, Dev: Reset Story) on its component. Per memory: Play Mode tests write to save.json, so back it up first and restore it afterward.

Not built

- A real credits roll (the endings close on a thank-you page).
- Chamber murals as art (text only), final Keystone / Seal sprites (placeholders).
- Sounds of their own for the endings (they reuse RockRumble, Explosion, Prestige and the portal sounds).
- A proper HUD state for the void cave (depth and biome banner read nonsense there).
- Dev Panel buttons (ContextMenu only).
- Any role for the sky relic.
