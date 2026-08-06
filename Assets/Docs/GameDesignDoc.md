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
	- Prestige points
		- finding artifacts will yield prestige points when given to the museum
- upgrades
	- all regular upgrades are purchased at the market using Dollars
	- all prestige upgrades at the museum are purchased with prestige points
# Map Layout 
	- you start out at 0 meters in depth. 
	- Buildings are on the top of the digging zone on the ground
	- buildings:
		- storage depot: where you store and sell your accrued minerals
			- when interacted with, a UI pops up that shows you your accrued minerals, and allows you to sell any percentage of a certain type, or all of them.
			- if you sell all "gold" for example, the processing center will not run as it has no gold to process
		- market: where you purchase upgrades
			- the UI is a skill tree?
		- shipping center: where you can sell your own materials for higher prices, based on market fluctuations
			- maybe a casino instead
			- value is time-based sine wave + noise
		- processing center: where you can turn your minerals into finished goods for higher prices
			- this will combine various ore to make a product
				- for example 5 wood and 1 iron will make a chest every 10 seconds.
				- the results are shown in the storage depot where you can sell them
		- museum: prestige center, turn in artifacts to earn prestige points
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


# Passive upgrades
	- mining over 50% of a layer increases the value of minerals (or processed goods made from minerals) in that layer by 2x. 
		- mining 75%, and 95% double it again
# Market Upgrades
Upgrades will be a skill tree that fans out and requires the player to unlock the previous tier.
- Mining
	- Increase mining size: this will add 1 block radius to the player's mining around them.
		- each upgrade mines 1 more block in one direction.
			- the first upgrade mines a block the left on each dig
			- second upgrade mines a block to the right
			- third upgrade mines a block down and to the left
			- fourth upgrade mines a block down and to the right
			- fifth+ upgrade, repeat the cycle, adding another block of distance from the player
	- Increase mining speed: this will increase the rate at which the player mines blocks
		- each tier adds 10% mining speed. 
		- the final upgrade makes dirt/stone an instant mine
	- Insta-mine chance
	- Lantern:
		- you start out only being able to see the blocks adjacent to your mine shaft
		- the lantern reveals the "fog of war" and enables you to see deeper into the dirt to find minerals and plan a route
		- capstones: 
			- true sight: reveals all fog of war
			- zoom, enhance: zooms the camera out to reveal more of the map
			- hazard sense: highlights hazard blocks
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
- Progression
	- Increase spawn odds of next tier of blocks in upper layers
	- increase the spawn odds of all ores
	- power up blocks 
		- increase effectiveness of power up blocks
		- increase spawn rate

	
# Prestige
At a certain point the game will become too difficult. You will have to use a new currency when resetting to work towards a more "meta" skill tree that will make your next run faster. Artifacts are that currency.

Prestige is manually triggered at the museum. This is a hard reset of all your world upgrades, dollars, and materials in the depot (both minerals and processed goods)

I would aim for the first prestige to take around 2 hours, with future prestiges being faster due to the upgrades accelerating the player's progress.

The map will regenerate, all of your dug tunnels will be gone. All of your money will be gone. The only thing that will remain is the prestige perks you've purchased.

- Mining:
	- view: zooms out the camera a certain percentage to view more of the mineable area
	- Increase grid size: this will add width to the horizontal grid generation
- Economy
	- mineral value multiplier
		- capstones: 
			- double the passive layer bonus
			- keep the passive layer bonus between prestiges
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
	- increase how many prestige points you earn per artifact
	- add passive prestige point gain over time
	- capstones: 
		- auto-prestige when it's mathematically worth it
- Survival
	- one time shield charges that regenerate over time, preventing damage
	- increased move speed
	- reduced fall damage
	- gas resistance