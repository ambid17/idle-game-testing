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
# Art Style
- World (tiles, buildings, props, backdrops)
	- vibrant pixel art of glowing ores, crystalline minerals and ancient tech in a neon-lit fantasy mine
	- the buildings are the style and detail target
- UI (icons, buttons, panels, HUD)
	- flat, smooth pixel art: bold simple shapes, large flat colour areas with clean edges, 2 to 3 tones per colour, dark outline, small white highlight
	- no texture, noise, dithering, gradients or glow
	- the lock icon (SellLockClosed) and health icon (HudIcon_Health) are the style target
	- color palette: cyan, gold, dark gray and white, with neon purple accents
		- gold is the main body colour, cyan the secondary/interactive colour
		- dark gray for panels, backing plates and disabled states
		- white for highlights and text
		- neon purple only as a small accent
	- text uses the Orbitron font

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
		- sight potion: temporarily give full sight beyond the vision radius
		
	- hazardous:
		- explosive: destroys blocks in a radius, but damages the player if they are close. You get to collect the minerals destroyed by the explosion
		- falling rocks: mining the terrain under them causes them to fall, damaging the player if they are below it
		- low-vis areas: higher chance for better minerals, but lowers visibility
		- water pocket: mining into a water block floods the area below it, slowing movement until the water drains out
		- gas pockets: mining releases a damaging/flammable gas cloud; if it touches lava or an explosive power-up block it chain-ignites, spreading beyond a normal explosive's radius
	
## Artifacts:
	- at least 1 artifact is guaranteed per depth layer, with a separate low change of bonus artifacts beyond the guaranteed one
	- artifact spawn rate increases as you go to deeper layers
	- artifacts are secretly the Seals holding back what's buried at the bottom of the mine - see "# Story & Endgame: The Seals
A real ending to dig toward, with no combat: resonating means "getting strong enough to reach the bottom", and the player's own choices on the way down decide what the ending costs. Names below are working titles. Implementation plan: storyImplementation.md.

## Premise
- The Museum is your eager patron. The curator (Prof. Dustworth) pays well for every artifact you bring up and is the reason you resonate.
- The curator is NOT the villain. They're an honest collector who doesn't understand what they're buying.
- The twist: artifacts aren't relics, they're Seals. The Makers buried something (working name: "the Bound") beneath the mine and locked it away with thousands of wards spread through the layers. Every artifact you dig up weakens the prison.
- The player is responsible. Their core loop (dig artifacts, spend them at the Museum) is what frees it.
- The Resonator: a Maker machine in the Museum. The curator feeds it your artifacts and it retunes your rig with Maker tech (Attunements). The side effect is a quake that collapses and reshapes the mine, which he waves off as "geological enthusiasm". It is really the prison's lock: every Resonance spends Seals, and the quake is the Bound stirring.
- The Bound is ambiguous on purpose. The curator comes to believe the Makers imprisoned a devourer. The Bound's own voice says the Makers stole its light and called it ore. Neither is ever confirmed, so the final choice is a real one.
- No combat. The wall between biomes is the drill tier (Mining_DrillTier gates layers 3, 6 and 9), not a boss.

## Three voices
- The curator: argues for resealing once he understands. Comic early, uneasy in the middle, frightened and honest late.
- The Bound: whispers to the player as they set depth records. Never threatens; asks. Argues for release.
- The critter keeper (Grizzle Mossbeard): takes no side, just reports what the critters are doing.

## Choices: Keystones and the last Seal
- Seal Chambers: one brick room on each of layers 3, 6 and 9 (indices 2, 5, 8 - the last layer before each drill-tier gate), regenerated every Dig. Each holds a Keystone.
	- Take it: a large one-time artifact payout (15 / 30 / 60). Permanent - the socket stays empty in every later Dig.
	- Leave it: nothing happens, and the player can come back in any later Dig and change their mind.
- The Vault: a larger room on layer 11 (index 10) holding the last Seal and the final choice.
	- Release: break it. Always available.
	- Reseal: return what was taken. Costs a base 20 artifacts plus 2x the payout of every Keystone pried loose (20 if none were taken, 230 if all three were). A greedy player pays heavily, a careful one pays little. This also plays on the Museum Dividends spend-vs-hoard tension.
- Earlier choices set the price of Reseal; they never lock an ending out.

## Endings
- Both play a short sequence (quake, white flash, epilogue with the Keystone count reflected in it), unlock an achievement, and leave the game playable.
- Release: "The Bound is free." The light leaves the ore and goes up the shaft. Post-game: +25% to all ore and goods sale value. Tremors stop.
- Reseal: "The Seal holds." Taken Keystones are back in their sockets. Post-game: -25% to all damage taken. Tremors stop.
- The curator, the critter keeper and the Resonance prompt all have post-ending lines for each.

## Dropping hints by depth
Each beat fires the first time the player sets a depth record inside its layer range. Drill tier Attunements gate those layers, so depth reached and Resonances completed both drive the story. Story progress is permanent and survives Resonance.

| Stage | Layers (1-based) | Drill tier | What the player sees |
|---|---|---|---|
| The Commission | 1-3 | 0 | Joke rune translations, a giddy curator. The first chamber's mural shows Makers carrying tablets *down*. First Resonance is played as a triumph. |
| The Warnings | 4-6 | 1 | Whispers begin, one per new layer reached. Curator notices the trap rooms face *down* the shaft. Critter keeper notes critters moving up-shaft. Ambient tremors start and grow with each Resonance. |
| The Retranslation | 7-9 | 2 | On the next Museum visit the curator rechecks his work: the tablets are Seals, the Resonator is the lock, the vault inscription reads KEEP IT BURIED. The Collection tab shows each rune's true reading over the struck-through joke. Whispers address the player directly. |
| The Vault | 10-11 | 3 | No more jokes. The Resonance prompt counts the Seals still holding. Curator and voice argue openly. The last Seal. |

Delivery channels:
- Whispers: the first time each layer from 4 on is reached, the Bound speaks in the dialog box (like chatting with the Professor) with its faceless portrait and an eerie murmuring voice. It waits until no panel or cinematic has the screen.
- Curator: stage-keyed greetings and Talk lines, the Retranslation conversation, true rune readings.
- Seal Chambers: a short mural description the first time each one is examined.
- Resonance prompt: its text escalates with the stage.
- Tremors: ambient camera shake, more frequent with stage and Resonance count, silent after either ending.
- Critter keeper: stage-keyed Talk lines.

## Open questions
- the sky relic (hidden Seal at y=200): the one Seal the Makers hid up instead of down - give it a role in the Reseal ending?
- credits: the endings close on a "thank you for playing" page, not a real credits roll
- chamber and Vault art: the Keystone and Seal use placeholder sprites; murals are text only
- post-game: is a flat bonus enough, or should each ending change the world (sky, ore glow, music)?
- how many Resonances should reaching the Vault take? Target an 8-15 hour total playtime
