I want to make an incremental game similar to motherload

# Inspiration
- Keep On mining
- Clicker Heroes
- Adventure Capitalist
- Scratch Inc.
- Nodebuster
- A Game about feeding a black hole
- Motherload

# Game Overview
- 2d side scroller (similar to motherload)
	- little bit of horizontal scrolling, maybe 2 screens of width
	- vertical depth scales in difficulty. Takes longer to mine, more hazards, but more valuable minerals
- Mechanics
	- WASD to move
	- holding A, S, or D will mine in the direction you are holding if:
		- you are on the ground
	- holding W activates your jetpack, using fuel to fly
		- if you drop down a mine shaft, and dont slow your velocity with the jetpack, you will take fall damage
		- movement speed while flying is faster than while grounded
	- approaching a building will pop up an interaction text
		- pressing E will interact with the building, opening its UI
- Death
	- Death happens when the player runs out of fuel, or their health reaches zero. 
	- a death screen shows ("you have died" and a respawn button)
	- When you die, you lose your inventory and respawn at the start.
- Inventory
	- each block type has a weight and you can only hold so many before you have to drop them off at the ore depot
	- hit tab to view your inventory.
		- shows you a count of each ore type
		- shows you a weight meter
		- show you a list of artifacts. they cannot be stored in the depot, only turned in to the museum
	- once your inventory is full, you can mine dirt blocks, but not minerals
- Idle
	- automated miners will make progress while you are gone
	- they will dig holes in the map that will show up when you return
	- all of their ores will be stored in the depot
	- when the game loads, a screen will populate with the ores dug, and their value
	- same goes for the processing center, add a tab to the UI for this, if unlocked and show the value generated
- Currency
	- Dollars
		- selling minerals or processed goods will yield dollars
	- Artifacts
		- mining an artifact banks it directly as the museum's currency - no separate conversion step
- upgrades
	- all regular upgrades are purchased at the market using Dollars
	- all prestige upgrades at the museum are purchased with artifacts, but only queue - see "# Prestige"
# Map Layout 
	- you start out at 0 meters in depth. 
	- Buildings are on the top of the digging zone on the ground
	- buildings:
		- storage depot: where you store and sell your accrued minerals
			- when interacted with, a UI pops up that shows you your accrued minerals, and allows you to sell any percentage of a certain type, or all of them.
			- if you sell all "gold" for example, the processing center will not run as it has no gold to process
		- market: where you purchase upgrades
			- the UI is a skill tree?
			- has a tab for repairs
				- fill up gas
				- re-fill health
		- control center: where you can control settings for your automatons, view dashboard of their work, and refuel yourself
		- processing center: where you can turn your minerals into finished goods for higher prices
			- this will combine various ore to make a product
				- for example 5 wood and 1 iron will make a chest every 10 seconds.
				- the results are shown in the storage depot where you can sell them
		- museum: prestige center, spend artifacts on permanent perks (queued until you prestige)
	- the mine:
		- the grid will be randomly generated with weights for minerals at certain depth ranges
		- if no ore spawns, the gaps are filled with dirt
		- every 100 blocks in depth, the layer changes. 
			- The dirt changes color, becoming darker, and mining becomes slower. 
			- a new random generation table for minerals is chosen with weights towards more valuable minerals

# Block types
The value and weight scales as you go down the tiers. Value scales faster than weight.
- Wood
- Stone 
- Iron 
- Silver 
- Gold 
- Platinum 
- Diamond 
- Obsidian 
- Mithril 
- Meteorite 
- Lava
	- shows up more as you get deeper, damages the player, worth no money

## Randomness blocks: the exist to make digging a bit more lively
	- positive:
		- treasure chest: contains a treasure trove of materials in the next layer
		- sight potion: temporarily give full sight through the fog of war
		
	- hazardous:
		- explosive: destroys blocks in a radius, but damages the player if they are close. You get to collect the minerals destroyed by the explosion
		- falling rocks: mining the terrain under them causes them to fall, damaging the player if they are below it
		- low-vis areas: higher chance for better minerals, but lowers visibility
		- water pocket: mining into a water block floods the area below it, slowing movement until the water drains out
		- gas pockets: mining releases a damaging/flammable gas cloud; if it touches lava or an explosive power-up block it chain-ignites, spreading beyond a normal explosive's radius
	
## Artifacts:
	- at least 1 artifact is guaranteed per depth layer, with a separate low change of bonus artifacts beyond the guaranteed one
	- artifact spawn rate increases as you go to deeper layers
	- artifacts are secretly the Seals holding back what's buried at the bottom of the mine - see "# Story & Endgame: The Seals"


# Passive upgrades
	- mining over 50% of a layer increases the value of minerals (or processed goods made from minerals) in that layer by 2x. 
		- mining 75%, and 95% double it again
# Market Upgrades
Upgrades will be a skill tree that fans out and requires the player to unlock the previous tier.
- Mining
	- Increase mining size (vein mining): mining an ore block chains into adjacent ore blocks for free.
		- only triggers when the block you mined is an ore (mining dirt/stone never chains).
		- each level lets the chain reach 1 more ore block, spreading outward through connected ore (not a fixed direction) - so a level-3 upgrade clears up to 3 extra ore blocks if there's an ore vein to chain through, fewer if the vein is smaller.
	- Increase mining speed: this will increase the rate at which the player mines blocks
		- each tier adds 10% mining speed. 
		- the final upgrade makes dirt/stone an instant mine
	- Insta-mine chance
	- Lantern:
		- you start out only being able to see the blocks adjacent to your mine shaft
		- the lantern reveals the "fog of war" and enables you to see deeper into the dirt to find minerals and plan a route
		- capstones: 
			- zoom, enhance: zooms the camera out to reveal more of the map
			- hazard sense: highlights hazard blocks
	- Enable digging while flying
- Economy
	- Inventory: increase the player's max carrying weight
	- Marketing: increase sales value of minerals
	- Overflow: once inventory is full, you can continue to mine and ores will auto-sell at a reduced value
- Automation
	- Auto miner: Add an automated miner that will mine a random tunnel
		- auto miner upgrades 
			- miner count
			- miner speed
			- miner radius
			- targeting : more intelligently target higher value blocks, and favor depth
		- capstones:
			- foreman: Miners gain a portion of your upgrades to mining speed/radius
	- Processing center: processes a certain amount of minerals per minute to turn them into a higher value
		- first upgrade unlocks the processing center
		- one side of the upgrade tree unlocks new recipes
			- capstone: 
				- shard of possibility: uses one of every mineral to produce a high value shard
		- other side of the tree improves the processing center production
			- overtime: increase production speed
			- quality: increase value of produced goods
	- Drone delivery: drones will come pick up minerals from you so you don't have to return to the depot. They will fly the fastest route to get to you and follow that route back to the depot
		- upgrade drone carrying capacity
		- upgrade drone speed
		- increase drone count
		- capstones:
			- Market Sense: drones will auto sell their inventory above a certain threshold market value when they reach the depot 
- Survival
	- Increase fuel cap
	- Increase fly speed
	- Increase fly acceleration for changing speed
	- Decrease fall damage
	- Increase fall speed
	
# Prestige
At a certain point the game will become too difficult. You will have to use a new currency when resetting to work towards a more "meta" skill tree that will make your next run faster. Artifacts are that currency - mining one banks it directly, no separate conversion step.

Prestige is manually triggered at the museum. This is a hard reset of all your world upgrades, dollars, and materials in the depot (both minerals and processed goods)

Prestige upgrades can be purchased (spending artifacts) at any time, but only queue - none of them take effect until you actually trigger a prestige. This is what lets map-generation perks (grid size, layer size, etc.) apply cleanly to the freshly-regenerated map instead of retroactively to the one you're standing in, and it keeps every prestige perk's timing consistent with each other.

I would aim for the first prestige to take around 2 hours, with future prestiges being faster due to the upgrades accelerating the player's progress.

The map will regenerate, all of your dug tunnels will be gone. All of your money will be gone. The only thing that will remain is the prestige perks you've purchased (including anything you had queued).

- Mining:
	- view: zooms out the camera a certain percentage to view more of the mineable area
	- Increase grid size: this will add width to the horizontal grid generation
		- true sight: reveals all fog of war
	- keep "digging while flying" upgrade between prestige runs
	- adjust layer sizes: smaller layers let you get deeper faster
		- need to balance with processing recipes
- Economy
	- mineral value multiplier
		- capstone: passive layer bonus - clearing 50%/75%/95% of a layer each multiply its ore value by 1.5x (1.5^3x at 95%)
	- processing
		- processed good production multiplier
- idle
	- auto miner
		- keep 1 idle miner (3 upgrades)
			- these effectively increase the max upgrade tier. If you purchase this, then purchase the idle miner with normal currency, you get another.
		- keep 1 tier of miner speed (3 upgrades)
		- keep 1 tier of miner dig speed (x3)
		- keep 1 tier of miner move speed
- Prestige
	- increase artifact spawn rate
	- increase how many artifacts you get per artifact-ore mined
	- add passive artifact gain over time
		- grant funding: start each run with a % of the dollars earned during the previous run
			- legacy: each prestige ever completed adds a permanent, stacking % to all sale value
	- museum dividends: each unspent artifact held adds a % to all sale value (spend vs. hoard tension)
- Progression
	- Increase spawn odds of next tier of blocks in upper layers
	- increase the spawn odds of all ores
	- power up blocks 
		- increase effectiveness of power up blocks
		- increase spawn rate
- Survival
	- one time shield charges that regenerate over time, preventing damage
	- increased move speed
	- reduced fall damage
	- gas resistance

# Story & Endgame: The Seals
Motherload's structure (friendly employer, stranger signs the deeper you go, a twist, a fight at the bottom) without copying its content. The goal is a real ending to dig toward, so prestige means "getting strong enough to reach the bottom" rather than just "numbers go faster". Names below are working titles.

## Premise
- The Museum is your eager patron. The curator pays well for every artifact you bring up and is the reason you prestige.
- The curator is NOT the villain (this is the main break from Mr. Natas). They're an honest collector who doesn't understand what they're buying.
- The twist: artifacts aren't relics, they're Seals. An ancient civilization buried something (working name: "the Bound") beneath the mine and locked it away with thousands of wards spread through the layers. Every artifact you dig up weakens the prison.
- The player is responsible. Their core loop (dig artifacts, spend them at the Museum) is what frees it.

## Delivering the story
- Artifact lore: lore fragments unlock as you find artifacts in each layer, readable in a Museum lore/collection tab. The tone gets darker with depth:
	- layers 1-3: museum placards ("ceremonial disc, fired clay, purpose unknown")
	- layers 4-7: translated inscriptions that start to warn ("...so that it may not rise...", "do not lift")
	- layers 8-11: the full account of what was sealed and why, and the realization of what the player has been doing
- Curator dialogue: short lines when the Museum opens, keyed to story progress. Excited early, uneasy in the middle, horrified after the twist. After the twist the curator becomes your ally, translating inscriptions to reveal guardian and boss weaknesses.
- Hazards as symptoms: the existing depth scaling of gas, lava, and falling rocks is explained as the Bound stirring. Optional screen-shake "tremors" that grow more frequent as total artifacts collected rises.
- Prestige as a story beat: the map regenerating is diegetic. Each prestige the Bound stirs and the mine collapses and reshapes itself. The prestige confirmation and post-prestige text should say so.
- Story progress (lore unlocked, guardians defeated, twist seen) is permanent and survives prestige, like lifetime stats.

## Guardians (mini-bosses)
Constructs the old civilization left to stop anyone digging toward the prison. Each one guards a layer boundary and teaches one way to turn hazards into weapons, as preparation for the final fight.
- Guardians are the "too difficult" wall that motivates prestige: a guardian blocks further descent until it's defeated in the current run. The first run should hit the Stone Warden at about the 2-hour mark, matching the first-prestige target.
- The Stone Warden: bottom of layer 4
	- a slow construct of rock and wards
	- can't be damaged directly. The arena ceiling is lined with falling rocks and scattered with explosive blocks
	- teaches: mine a rock's support to drop it on the guardian, and lure it into explosives
- The Censer: bottom of layer 8
	- a construct that vents gas clouds into the arena as it moves
	- teaches: ignite its gas while it's standing in it (requires the deferred gas chain-ignition mechanic)
	- its gas also damages the player, so fuel/HP management and Survival perks matter

## The final fight: the Bound
Below layer 11 is the Vault, a hand-authored arena rather than a generated layer. As with the guardians, the player never gets a direct attack. Every hazard they've learned to fear becomes their arsenal.
- Phase 1 (chained): the Bound is held by the last remaining seals. It swipes and triggers cave-ins. The player drops falling rocks and sets off explosives on it.
- Phase 2 (loose): it burrows through the arena terrain, leaving tunnels and gas pockets. The player ignites the gas while it's inside, using what the Censer taught.
- Phase 3 (rising): lava floods the Vault from below and the Bound climbs toward the surface. The player has to climb with the jetpack while hitting it with the rocks and explosives around the shaft. This is a fuel-management test, the tension the whole game has been training.
- Optional: owned automatons and drones join the fight (draw fire, ferry fuel), so idle investment pays off in the finale.

## Ending
- After phase 3 the player chooses:
	- Reseal: sacrifice a large number of artifacts to rebuild the prison. This plays on the Museum Dividends spend-vs-hoard tension, since you're giving up your hoard to fix what you caused.
	- Destroy: an extra, harder phase that ends it for good.
- Both roll credits, unlock a unique achievement, and leave the game playable afterward (post-game / NG+: "the mine stirs again", with the ending reflected in curator dialogue and lore).

## Systems impact
What this adds to the current build:
- persistent story progress in the save file (lore unlocked, guardians beaten, ending chosen), not reset by prestige
- lore fragment data per layer and a Museum lore/collection tab
- curator dialogue lines keyed to story progress
- guardian/boss arenas at the layer 4 and 8 boundaries and below layer 11, plus a descent gate while a guardian is alive
- boss entities with health, AI, and phases
- hazards (falling rocks, explosives, gas, lava) able to damage non-player entities, not just the player
- gas chain-ignition, currently deferred (needed for the Censer and phase 2)
- new achievements: each guardian, each ending

## Open questions
- once a guardian is defeated, does it stay dead across prestiges, or return every run (tougher each time)? Leaning toward returning every run for the first few runs so it stays the prestige wall, then permanent after enough prestige perks
- are artifacts still an anonymous count, or do they become unique named items (sets, rarities)? Lore per layer works with either
- does the twist hit at a fixed depth (e.g. defeating the Stone Warden) or at a lifetime-artifacts threshold?
- how many prestiges should reaching the Vault take? Target an 8-15 hour total playtime
