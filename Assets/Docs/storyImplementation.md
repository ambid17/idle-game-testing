Context

The game had no long-term goal beyond earning money. We adopted "The Seals" story (GameDesignDoc # Story & Endgame: The Seals): artifacts are wards holding back the Bound, and every Resonance spends them. An earlier version of this plan had guardian boss fights and crafted weapons; that was dropped before any of it was built. The settled design:
- No combat. Drill tiers are the wall between biomes.
- Two endings, Release and Reseal, chosen at the Vault. Earlier Keystone choices set the price of Reseal and never lock an ending out.
- The Bound is ambiguous: the hints are written so neither the curator nor the voice is confirmed right.
- Story progress is permanent and survives Resonance.

Where things live

- Assets/Scripts/Story/
  - StoryManager.cs: the story state (GameManager.StoryManager, a child of the GameManager object). Stage from the deepest layer reached, Keystones taken, chambers examined, whispers heard, Retranslation seen, ending. Fires whispers and tremors, runs the ending sequence, exposes the post-game multipliers.
  - StoryContent.cs: every line of story text as one ScriptableObject (Assets/ScriptableObjects/Story/StoryContent.asset). Field defaults are the shipped script.
  - SealChamber.cs: the Keystone / last Seal world object. One per chamber in the scene; each finds its room in the generated chunk and stands in it.
- Assets/Scripts/UI/Panels/StoryChoiceUI.cs: the take/leave and Release/Reseal modal.
- Persistence: GameSaveData.Story (StorySaveData). PrestigeManager.ExecutePrestige never touches it.
- Map generation: Structure_SealChamber + Feature_SealChamber on LayerConfigs 2, 5 and 8; Structure_Vault + Feature_Vault on LayerConfig 10 (MaxLayer 10, so the repeated layers below don't get one). Ordinary StructureStampFeatures, first in each layer's Features list so they place before the other set-pieces. Generation stays a pure function of the seed: the room is always there, only the Keystone's state changes.

Stages

StoryStage is derived, not stored: deepest layer index >= 3 Warnings, >= 6 Retranslation, >= 9 Vault. The curator's Retranslation conversation plays on the first Museum visit at that stage and sets RetranslationSeen, which is what the rune cards and post-twist curator lines key off.

Hooks into existing systems

- Museum.MuseumCuratorController: stage greetings and Talk lines, the Retranslation conversation, true rune readings on turn-in.
- UI.RuneSlotUI: joke reading struck through over the true one once RetranslationSeen.
- UI.MuseumPrestigeConfirmUI: prompt text from StoryManager.ResonancePrompt.
- Critters.CritterShopController: stage Talk lines.
- Economy.PrestigeUpgradeManager.Prestige_IncomeMultiplier: x StoryManager.SaleValueMultiplier (Release).
- Player.PlayerHealth.TakeDamage: x StoryManager.DamageTakenMultiplier (Reseal).
- Platform.AchievementManager: EndingRelease / EndingReseal (ACH_ENDING_RELEASE / ACH_ENDING_RESEAL - need Steamworks dashboard entries).
- Interaction.InteractableType.SealChamber: one prompt row for Keystones and the Vault.

Tuning (all on the StoryManager component)

- keystoneRewards: 15 / 30 / 60
- resealBaseCost 20, resealCostPerTakenArtifact 2
- releaseSaleValueMultiplier 1.25, resealDamageTakenMultiplier 0.75
- tremor interval and force

Testing

StoryManager has ContextMenu entries (Dev: Advance Stage, Dev: Reset Story) on its component. Per memory: Play Mode tests write to save.json, so back it up first and restore it afterward.

Not built

- A real credits roll (the endings close on a thank-you page).
- Chamber murals as art (text only), final Keystone / Seal sprites (placeholders).
- Dev Panel buttons (ContextMenu only).
- Any role for the sky relic.
