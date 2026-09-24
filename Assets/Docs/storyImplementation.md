 Context

 The game has no long-term goal beyond earning money. We adopted "The Seals" story (GameDesignDoc # Story & Endgame: The Seals): artifacts are wards holding back the Bound; guardians at layers 4 and 8; a final fight below layer 11. Since then, the design discussion has settled these points:
 - Guardians are permanent once beaten (a story flag that survives prestige). The arena and gate generate only while the guardian is undefeated.
 - Artifacts stay a plain count; lore unlocks per layer (later milestone).
 - Boss weapons are crafted at the Processing Center from ore, unlocked through Market upgrades, and used by dropping them from above (hotkey while flying). They are arena-only; normal mine hazards are unchanged.
 - The mix of crafted weapons vs. hazards varies per boss: the Stone Warden is fought mostly with direct weapons; later bosses lean on setting off hazards.
 - No water/obsidian entomb; Reseal stays the artifact sacrifice.

 This also answers the original problem: ore now has a use besides being sold (you can craft with it instead).

 Roadmap

 1. M1: Stone Warden slice (this plan): story-progress save, weapon crafting, drop-weapon, arena + ward gate at the bottom of layer 4, the Stone Warden boss.
 2. M2: Story delivery: per-layer lore from lifetime artifacts found per layer, a Museum lore tab, curator lines, and prestige flavor text.
 3. M3: Censer (bottom of layer 8): gas chain-ignition (deferred in GasCloudHazardEffect), an Igniter Flare weapon that lights gas.
 4. M4: The Bound + Vault below layer 11: a three-phase fight using deep-ore weapons plus hazards.
 5. M5: Endings: Reseal (artifact sacrifice) or Destroy, credits, post-game.

 M1 implementation

 0. Design doc

 Update Assets/Docs/GameDesignDoc.md # Story & Endgame with the decisions above: crafted drop-weapons, arena-only, permanent guardians, and Market unlocks. Rewrite the guardian/fight bullets that currently say "weapons are hazards".

 1. Story progress (persistent, prestige-proof)

 - New Assets/Scripts/Story/StoryManager.cs: a Singleton<StoryManager> holding HashSet<GuardianId> defeatedGuardians, with IsDefeated(id), MarkDefeated(id) (dispatches a new GuardianDefeatedEvent), and RestoreFromSaveData.
 - enum GuardianId { StoneWarden, Censer }, append-only.
 - Persistence/SaveData.cs: add a StoryProgressSaveData StoryProgress sibling to LifetimeStats. PrestigeManager.ExecutePrestige must not touch it.
 - Persistence/SaveService.cs: save it, and restore it first in ApplyLoadedData. Verify that ApplyMapData and chunk generation can't run before the restore (the arena decision is made at generation time). If needed, restore story progress before map data.

 2. Weapons as a recipe output

 - Processing/ProcessingRecipeDefinition.cs: add RecipeOutputKind Kind (Good | Weapon, default Good so existing assets are unaffected) and WeaponId Weapon.
 - New enum WeaponId : byte { BlastingCharge }, append-only.
 - New Player/PlayerWeapons.cs (weapon counts, Add/TryConsume, save/restore). Add it to GameManager per the singleton rule. Weapons are kept on death and cleared in PrestigeManager.ExecutePrestige, like Depot goods.
 - Processing/ProcessingManager.cs: CompleteJob and the offline branch of RestoreFromSaveData route Weapon recipes to PlayerWeapons.Add instead of Depot.DepositGood.
 - Processing UI (UI/Processing/ProcessingRecipeRowUI.cs, ProcessingQueueSlotUI.cs): hide the sale value for weapons and show "Weapon" instead.
 - New asset ScriptableObjects/Processing/Recipes/Recipe_BlastingCharge.asset (Coal ×4 + IronOre ×1; Coal has no recipe today), registered in ProcessingRecipeDatabase.asset. Append ProcessingRecipeId.BlastingCharge.
 - New Market upgrade Processing_BlastingChargeRecipeUnlock, set as the recipe's RequiredUpgrade. Use the upgrade-authoring skill (handles enum order, database registration, and prerequisites).

 3. Dropping weapons

 - PlayerWeapons polls Keyboard.current (same as PlayerController/PlayerMining) for a drop key, only while the player is inside an active arena (GuardianArenaController.IsPlayerInArena).
 - New Weapons/DroppedChargeEffect.cs: falls cell by cell like FallingRockHazardEffect.Fall until it lands on something solid or reaches the boss, briefly flashes like ExplosiveHazardEffect.Telegraph, then dispatches a new WeaponDetonatedEvent(position, radius, damage). It doesn't damage terrain or the player in M1.
 - HUD: a weapon count and drop-key hint while in the arena (a small addition to UI/Panels/HUDUI.cs).

 4. Arena and ward gate (bottom of layer index 3)

 - New BlockTypeId.WardStone = 28 (appended) plus a BlockType asset registered in BlockTypeDatabase, with placeholder art via the art-asset-generation skill.
 - ChunkGenerator.Generate: add a trailing optional GuardianArenaSpec arena parameter (keeps it pure/headless). When set, a final pass:
   - carves the bottom N rows (~8) across the full width by setting Mined = true, same approach as CarveEmptyPockets
   - lines the row below with WardStone
   - optionally seeds some ceiling FallingRock blocks as boss-slam threats
 - MineWorld.GetOrGenerateChunk passes the spec for layer 3 only when !StoryManager.IsDefeated(StoneWarden).
 - MineWorld.TryMineCell refuses WardStone (same shape as the FallingRock refusal). AutomatonReachability.IsUnmineableByAutomaton also treats it as unmineable.
 - Persistence works because ChunkSaveData stores only mined/revealed bits over seed regeneration. On defeat, ForceClearCell the ward row, so the saved bits keep the gate open. After a reload or prestige, the arena isn't regenerated at all.
 - Existing saves whose layer-3 chunk is already generated get the arena on their next prestige. That's acceptable; note it in the dev panel.

 5. Stone Warden

 - New Story/GuardianArenaController.cs (scene object, editor reference to the boss prefab). Once the layer-3 chunk exists and the guardian is undefeated, it spawns the boss on the arena floor. It exposes IsPlayerInArena from the player's layer and row. On GuardianDefeatedEvent it clears the ward row through MapGenerationService and refreshes those cells' visuals (RefreshCellVisual).
 - New Story/StoneWardenBoss.cs: HP, and a simple state loop: patrol the floor → when the player is overhead/near, telegraph → slam, which sets off ceiling FallingRocks through the existing hazard path (they damage the player via HazardDamageHandler). Subscribes to WeaponDetonatedEvent for damage using a radius check (mirroring HazardDamageHandler.TryApplyRadiusDamage), plus a small bonus from FallingRockImpactEvent if a rock lands on it (a slight nod to the per-boss mix). At 0 HP it calls StoryManager.MarkDefeated.
 - Placeholder art (boss sprite, charge sprite) via art-asset-generation; the boss prefab is built in the Editor via UnityMCP.
 - Platform/AchievementManager.cs: add GuardianStoneWarden (append the enum and API name). A Steamworks dashboard entry is needed later; flag this to the user.

 6. Dev tooling

 Add to UI/Developer/DevPanelProgressionTab.cs: "Reset guardians", "Give 10 Blasting Charges", and "Teleport to layer 4 arena", so the fight can be tested without a 2-hour run.

 Conventions to follow

 Singletons are accessed via GameManager.<Name> with no null checks at call sites. Never use Find*. Log null checks once in Start. Enums are append-only. Move .meta files with their assets. Build prefabs and scene wiring in the Editor (UnityMCP), not by hand-editing YAML.

 Verification

 1. After each step, refresh Unity and check read_console for compile errors.
 2. Play Mode (UnityMCP), using the dev panel:
    - Buy the unlock upgrade, craft a Blasting Charge, and confirm it goes to PlayerWeapons, not the Depot.
    - Teleport to the layer 4 arena. The boss spawns, and the WardStone can't be mined by the player or by automatons.
    - Drop charges from above. Boss HP drops; slams bring down ceiling rocks that hurt the player.
    - Kill the boss. The ward row opens, the achievement unlocks, and you can descend.
    - Save and reload: the gate stays open and the boss doesn't respawn.
    - Prestige: the new map has no layer 4 arena, and weapons are cleared.
    - Use the dev "Reset guardians" and prestige: the arena returns.
 3. Per memory: Play Mode tests write to save.json, so back it up first and restore it afterward.
 4. Report what compiled and what was verified in Play Mode; don't claim gameplay "works" without the Play Mode pass.