---
name: upgrade-authoring
description: Create and wire up a new Market Upgrade or Prestige Upgrade in the idle-game-testing Unity project - the UpgradeDefinition/PrestigeUpgradeDefinition ScriptableObject, its UpgradeEffect/PrestigeUpgradeEffect enum entry, its entry in UpgradeDatabase/PrestigeUpgradeDatabase, its Prerequisite chain, and the actual gameplay code that reads or reacts to it. Use this whenever the user wants to add a new upgrade, perk, or skill-tree node to the Market or the Museum/Prestige tree, asks to "hook up" or "wire" an upgrade's effect, wants to add a prerequisite/capstone relationship between upgrades, or is extending UpgradeManager/PrestigeUpgradeManager with a new property. Don't skip this even for "just add one small upgrade" - the enum-ordering and database-registration steps are easy to get subtly wrong and silently break saved games or the skill tree UI.
---

# Upgrade authoring (Market + Prestige)

This project has two parallel but structurally identical upgrade families. Figure out which one applies before touching anything:

| | Market Upgrade | Prestige Upgrade |
|---|---|---|
| Class | `Economy.UpgradeDefinition` | `Economy.PrestigeUpgradeDefinition` |
| Base class | `Economy.UpgradeDefinitionBase` (shared) | same |
| Branch enum | `UpgradeBranch` (Mining/Economy/Automation/Movement/Processing) | `PrestigeUpgradeBranch` (Mining/Economy/Idle/Prestige/Progression/Survival) |
| Effect enum | `UpgradeEffect` | `PrestigeUpgradeEffect` |
| Database asset | `Assets/ScriptableObjects/Upgrades/UpgradeDatabase.asset` (`Economy.UpgradeDatabase`) | `Assets/ScriptableObjects/PrestigeUpgrades/PrestigeUpgradeDatabase.asset` (`Economy.PrestigeUpgradeDatabase`) |
| Manager singleton | `Economy.UpgradeManager` | `Economy.PrestigeUpgradeManager` |
| Currency | Dollars (`Wallet`) | Prestige Points (`PrestigePoints`) |
| Purchase events | `UpgradePurchasedEvent` / `UpgradeLoadedEvent` | `PrestigeUpgradePurchasedEvent` (loaded reuses the same event) |
| Cleared on prestige? | Yes - `UpgradeManager.ResetAllLevels()` | No - this is the point of Prestige upgrades |
| Reset() defaults | `BaseCost 100`, `CostGrowth 1.15` | `BaseCost 10`, `CostGrowth 1.5` |

Both managers derive from `UpgradeManagerBase<TSelf, TDefinition, TEffect>` (`Assets/Scripts/Economy/UpgradeManagerBase.cs`), which already implements `CanPurchase`, `TryPurchase`, `IsMaxed`, `IsUnlocked`, `GetNextCost`, `GetPurchaseBlockedReason`, and save/load (`SetLevel`). You never touch or reimplement that logic - everything below is about the *new* effect and its enum/database/property, not the purchase machinery itself.

If the user's request doesn't make clear which family they mean, ask - "does this survive a prestige reset?" is usually the deciding question.

## The five things a new upgrade needs

Skipping any one of these produces a specific, recognizable failure - use this table to sanity-check finished work:

| Step | If skipped... |
|---|---|
| 1. Effect enum entry | Nothing to assign on the asset; no gameplay hook possible |
| 2. ScriptableObject asset | Nothing to add to the database; upgrade doesn't exist in-game |
| 3. Registered in the database | Upgrade is invisible - doesn't appear in the skill tree UI, can't be purchased |
| 4. Prerequisite wired (if any) | Upgrade is purchasable immediately with no gating, or (if forgotten on a *later* tier that depends on this one) breaks that tier's gating |
| 5. Gameplay wiring | Upgrade is purchasable and shows progress in the UI, but does nothing - the classic "silent no-op upgrade" bug |

### Step 1 - Add the effect enum entry

Open `Assets/Scripts/Economy/UpgradeDefinition.cs` (Market) or `Assets/Scripts/Economy/PrestigeUpgradeDefinition.cs` (Prestige) and add a new member to `UpgradeEffect` / `PrestigeUpgradeEffect`.

**Every member has an explicit `= N` value, and that number is what's serialized into the `.asset` files.** Members are grouped by prefix in ranges of 100 (e.g. `UpgradeEffect`: Automation 100s, Economy 200s, Mining 300s, Movement 400s, Processing 500s; `PrestigeUpgradeEffect`: Mining 100s, Economy 200s, Idle 300s, Prestige 400s, Progression 500s, Survival 600s). Rules:
- Give the new member the **next free number in its prefix's range** (check the highest existing value - members aren't listed in numeric order). It can go anywhere in the source, e.g. alphabetically.
- Reordering or deleting members is safe. **Never change an existing member's number, and never reuse a retired member's number** - either silently re-points existing assets. C# also allows duplicate values without complaint; `Validate()` catches that at startup.
- Renaming a member is safe for the asset data, but the asset filename must be renamed to match (see Step 2).

Add a branch, if this is a new category, to `UpgradeBranch` / `PrestigeUpgradeBranch` - those enums are still positional and append-only, since the skill tree UI uses `Enum.GetValues(...).Length` as the branch count and groups nodes by `BranchIndex`.

Give the new member a one-line comment naming the doc section it implements (the existing entries all cite `Assets/Docs/GameDesignDoc.md` or `Assets/Docs/UpgradeIdeas.pdf` sections) - future edits rely on that trail to know what an effect is *supposed* to do.

### Step 2 - Create the ScriptableObject asset

If UnityMCP is connected, use `manage_scriptable_object` rather than hand-authoring YAML (per this project's rule that `.asset` files are technically editable but risky to get right by hand):

```
manage_scriptable_object(
  action="create",
  type_name="Economy.UpgradeDefinition",       # or Economy.PrestigeUpgradeDefinition
  asset_name="Mining_YourNewEffect",               # MUST equal the enum member name exactly
  folder_path="Assets/ScriptableObjects/Upgrades"   # or .../PrestigeUpgrades
)
```

Then set fields with `action="modify"` and `patches` against the created asset (`target={"path": "Assets/ScriptableObjects/Upgrades/YourNewUpgradeName.asset"}`), matching the fields on `UpgradeDefinitionBase` plus the subclass's `Branch`/`Effect`:

- `DisplayName`, `Description` - shown in the skill tree detail modal, and `DisplayName` **doubles as the save-file key** (`UpgradeManagerBase.KeyOf`) - keep it unique within its family and be cautious about renaming later (renaming orphans existing save data; `UpgradeManagerBase.SetLevel` logs an error and discards the level rather than crashing, but the player still loses that upgrade's progress).
- `Icon` - a `Sprite`; flat icon style, 64x64, per this project's asset style guide, if you're generating one.
- `EffectValuePerLevel` - the per-level magnitude; what it means is entirely up to the property you write in Step 5, so pick a value that makes that formula read sensibly (e.g. 0.1 for "+10% per level" read as `1f + level * EffectValuePerLevel`).
- `MaxLevel` - use `1` for a one-time unlock or capstone, higher for a scaling stat.
- `BaseCost` / `CostGrowth` - leave at the type's `Reset()` defaults unless the design doc says otherwise.
- `Branch`, `Effect` - the enum entries from Step 1. The asset's filename must equal `Effect`'s member name - `UpgradeDatabase.Validate()` / `PrestigeUpgradeDatabase.Validate()` log an error on any mismatch, which is how a mis-pointed Effect gets caught.
- `RequirePrerequisiteMaxed` / `Prerequisite` - see Step 4.

No UnityMCP available? Use `Assets > Create > Economy > Upgrade Definition` (or `Prestige Upgrade Definition`) in the Editor and fill the Inspector fields by hand - same fields, same rules.

### Step 3 - Register it in the database

Add the new asset to the `Upgrades` list on `Assets/ScriptableObjects/Upgrades/UpgradeDatabase.asset` or `.../PrestigeUpgrades/PrestigeUpgradeDatabase.asset`. This is the step people forget because the asset "looks done" after Step 2 - but `UpgradeDatabase.Find()` only searches this list, so an unregistered upgrade is completely invisible to the manager, the save system, and the skill tree UI.

Use `manage_scriptable_object` (`modify`, appending to the `Upgrades` array patch) if available, otherwise drag the asset into the list slot in the Inspector.

Each `UpgradeEffect`/`PrestigeUpgradeEffect` is expected to appear on **at most one** definition in the list - `BuildLookup()` logs a warning and lets the later entry win on a duplicate. If you're tempted to reuse an existing effect for a second upgrade, that's a sign you're missing an enum entry from Step 1 instead.

### Step 4 - Wire up the prerequisite (if this upgrade should be gated)

Set the new asset's `Prerequisite` field to another `UpgradeDefinition`/`PrestigeUpgradeDefinition` **of the same family** (a Market upgrade can't gate on a Prestige perk or vice versa - `UpgradeManager.PrerequisiteOf` does an `as` cast that would silently return null across families).

Two gating modes, both handled for you by `UpgradeManagerBase.IsUnlocked`:
- **Normal prerequisite** (default): unlocked once the prerequisite has *any* purchased level.
- **Capstone gate**: set `RequirePrerequisiteMaxed = true` to require the prerequisite fully maxed first - use this for the "tree branch" capstone nodes (e.g. `Mining_CameraZoom`, gated on a maxed Lantern).

Leave `Prerequisite` empty for a branch's first tier. Nothing else needs to change for prerequisites - `MarketSkillTreeSource`/`MuseumSkillTreeSource` (`Assets/Scripts/UI/SkillTree/`) link the visual tree edges automatically off this field, and `CanPurchase`/`GetPurchaseBlockedReason` already enforce it.

### Step 5 - Wire the effect into actual gameplay

This is the step that's easy to think is "just UI work" but isn't - **the skill tree UI is already fully generic** (`MarketSkillTreeSource`/`MuseumSkillTreeSource` iterate `database.Upgrades` and read `DisplayName`/`Icon`/`Level`/`IsUnlocked`/etc. off whatever's registered). Nothing UI-side needs to change for a new upgrade to show up, display cost, and be purchasable. What's missing after Step 3 is purely the *gameplay* system that should notice this effect exists.

There are two patterns in this codebase for that, and picking the right one matters:

**Pattern A - Pull (read on demand). Use this by default.**

Add a computed property to `UpgradeManager.cs` (in its `#region Utils`) or `PrestigeUpgradeManager.cs`, following the existing shape exactly:

```csharp
// GameDesignDoc "<section this implements>": <one line of what it does in-game>.
public float YourNewBonus => LevelOf(UpgradeEffect.YourNewEffect) * EffectValuePerLevelOf(UpgradeEffect.YourNewEffect);
```

Common shapes already established, reuse whichever matches your design:
- Flat scaling bonus: `LevelOf(effect) * EffectValuePerLevelOf(effect)`
- Multiplier (+X% per level): `1f + LevelOf(effect) * EffectValuePerLevelOf(effect)`
- Reduction multiplier (-X% per level, floored at 0): `Mathf.Max(0f, 1f - LevelOf(effect) * EffectValuePerLevelOf(effect))`
- One-time unlock/capstone (`MaxLevel = 1`): `IsMaxedEffect(UpgradeEffect.YourEffect)` returning `bool`

Then have the consuming gameplay script (`PlayerMining`, `PlayerController`, `PlayerInventory`, `Depot`, `MapGenerationService`, `ProcessingManager`, etc.) read `UpgradeManager.Instance.YourNewBonus` (or `PrestigeUpgradeManager.Instance.YourNewBonus`) wherever it currently uses the un-upgraded base value - typically inline in the formula, not cached, so purchase order/timing never matters. This is why pull is the default: no event wiring, no staleness risk.

If the effect should also combine with the *other* family's related bonus (e.g. camera zoom, grid width both have a Market and a Prestige contributor), add both instance reads together at the call site the way `CameraZoomController.ApplyZoom()` does - don't collapse them into one manager.

**Pattern B - Push (react to purchase/load immediately). Use only when a live value needs to change the instant the upgrade is bought or a save is restored** - spawning/despawning entities, resizing the live world, adjusting an already-running Cinemachine lens, etc. Something that a "read on next frame" pull wouldn't visibly apply until the next time that system happens to run.

```csharp
private void OnEnable()
{
    GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradeChanged);
    GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);   // also handle save restore
}

private void OnDisable()
{
    GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradeChanged);
    GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
}

private void OnUpgradeChanged(UpgradePurchasedEvent evt) => ApplyIfRelevant(evt.Definition);
private void OnUpgradeLoaded(UpgradeLoadedEvent evt) => ApplyIfRelevant(evt.Definition);

private void ApplyIfRelevant(UpgradeDefinition def)
{
    if (def == null || def.Effect != UpgradeEffect.YourNewEffect) return;
    // re-derive and apply the live value here
}
```

Use `PrestigeUpgradePurchasedEvent` for Prestige effects (there's no separate "loaded" event for that family - `PrestigeUpgradeManager` deliberately reuses the purchased event for both, since every listener should react the same way either way).

Reference implementations of this pattern, in increasing complexity:
- `Assets/Scripts/Camera/CameraZoomController.cs` - listens to all three events, recomputes a single derived value, and combines a Market + a Prestige contributor.
- `Assets/Scripts/MapGeneration/MapGenerationService.cs` (`ApplyGridWidthIfRelevant`) - filters by effect, then calls back into world-mutation logic.
- `Assets/Scripts/Automation/AutomationSpawner.cs` (`ReconcileAll`) - reconciles spawned entity counts to match `UpgradeManager.Instance.AutomatonCount` etc.; also listens for `PrestigeCompletedEvent` since a "kept tier" baseline can restore automatons with nothing else firing a purchase event.

Follow this project's [CLAUDE.md](../../CLAUDE.md) rules while doing this: dispatch/listen via `GameManager.EventService`, not raw C# events or actions; use `GameManager.<X>`/`UpgradeManager.Instance` singleton access rather than `FindAnyObjectByType`; and don't null-check `UpgradeManager.Instance`/`GameManager` at every call site - the singleton pattern guarantees it.

### Step 6 - Verify

There's no CLI test runner in this project. Let Unity recompile, check the Console for errors (`read_console` if using UnityMCP), then confirm in Play Mode:
1. The upgrade appears in the Market or Museum skill tree, in the right branch, gated behind its prerequisite if one was set.
2. Purchasing it deducts the right currency and advances its level/cost curve.
3. The actual gameplay change is observable - not just "the UI says level 1 now," but the effect itself (faster mining, a spawned automaton, a wider grid, etc.). This is the step that catches a forgotten Step 5.

Don't report the upgrade as "done" without a Play Mode check that specifically exercises the new effect - a correctly-registered upgrade with no gameplay wiring compiles cleanly and looks completely normal in the UI right up until the player notices it does nothing.

## Quick reference: file map

| File | Role |
|---|---|
| `Assets/Scripts/Economy/UpgradeDefinitionBase.cs` | Shared fields (DisplayName, cost curve, Prerequisite) - abstract, don't instantiate directly |
| `Assets/Scripts/Economy/UpgradeDefinition.cs` | `UpgradeBranch`, `UpgradeEffect` enums + Market `UpgradeDefinition` |
| `Assets/Scripts/Economy/PrestigeUpgradeDefinition.cs` | `PrestigeUpgradeBranch`, `PrestigeUpgradeEffect` enums + `PrestigeUpgradeDefinition` |
| `Assets/Scripts/Economy/UpgradeManagerBase.cs` | Shared purchase/level/unlock logic - don't modify unless changing behavior for *both* families |
| `Assets/Scripts/Economy/UpgradeManager.cs` | Market manager + every Market effect's computed property (`#region Utils`) |
| `Assets/Scripts/Economy/PrestigeUpgradeManager.cs` | Prestige manager + every Prestige effect's computed property |
| `Assets/Scripts/Economy/UpgradeDatabase.cs` / `PrestigeUpgradeDatabase.cs` | List + lookup by effect/name |
| `Assets/ScriptableObjects/Upgrades/UpgradeDatabase.asset` | The actual registered Market upgrade list - edit this, not the .cs |
| `Assets/ScriptableObjects/PrestigeUpgrades/PrestigeUpgradeDatabase.asset` | Same, Prestige |
| `Assets/Scripts/Utils/Events/Events.cs` | `UpgradePurchasedEvent`, `UpgradeLoadedEvent`, `PrestigeUpgradePurchasedEvent` |
| `Assets/Scripts/UI/SkillTree/MarketSkillTreeSource.cs` / `MuseumSkillTreeSource.cs` | Generic skill tree UI adapters - shouldn't need edits for a new upgrade |
| `Assets/Docs/GameDesignDoc.md`, `Assets/Docs/UpgradeIdeas.pdf` | What each effect is supposed to do - cite the relevant section in new enum/property comments |
