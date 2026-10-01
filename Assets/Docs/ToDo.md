todo 
look for any unused assets and remove them

- player 
	- stop adding upward force after a certain speed
- market
	- new prestige upgrade: 
		- chain vein mine: enables vein mining to branch to any ore
	- upgrade:
		- vein miner: remove allowing it to mine any ore. only vein mine ores of the type you originally mined
		- zoom out camera
			- add mouse wheel to zoom between the original and max unlocked zoom
			- handle edges. Currently the new zoom doesn't respect the grid boundary
			- lower the scaling between camera zoom upgrades by half
- depot
	- add lock icons to prevent selling certain items you want to process. this setting should be saved
- Dev Experience
	- 
- skill tree
	- 
- control center UI rework
	- 
- tutorial
	- 
- map
	- set piece rooms: They add landmarks and exploration goals, and slot into the existing chunk generator.
	-  Wandering events, starting with the treasure mole and cave-in warning. These break up the mining routine and reuse systems you already have.
	- lower layers need more hazard structures
- dev tools menu
	- 
- processing
	- make craftable utility items. maybe the teleporter is a crafted item?
- drone
	- test miner radius upgrade
- notifications
	- 
- prestige
	- needs to unblock some sort of barrier. right now there's neat upgrades but it doesn't do anything meaningful
		- lock certain levels of upgrades behind prestiging
		- i.e.: mining speed tier 6 requires prestiging with 10 artifacts
- steam 
	- ensure steam deck compatibility
		claude steps:
			1. A Deck detection service that applies Deck defaults (resolution, UI scale, controller button icons, frame-rate cap).
			2. An audit of every UI panel at 1280×800 that flags text likely to end up under 9px.
			3. A plan to unify the canvas scaling setups.
			4. A review of the offline-progress code for sleep/resume problems.
- ideas 
	- XXXXXX missing an end goal
	- find lost drones that become automatons
	- stock-market style selling of processed goods
	- encumberance lets you overfill inventory, but uses more fuel
	- shift to dash
	- add a vibrating animation when mining artifacts
	- hide an artifact high above the ground
	- inventory
		- ? maybe add a goal input to let you know when you should go get an upgrade
			- maybe add a goal button in the market
		- ? if youre almost full, and you mine a material that overfills your inventory, it allows you to grab it
			- allow this, but take a hit to fuel?
	- add spectate automaton view?
	- combat
		- little caves you enter to get loot?
	- QOL
		- scanner/analyzer that pops up info about the block: value, hazard description, name, etc 
	- upgrades
		- hull strength (speed needed to damage) vs hull quiality (max hp)
		- chance to duplicate ores on mine
	- automatons
		- ? show state above head as an icon
		- ? dont let automatons target artifacts
	- processing
		- ? processing slots +1, +10, Max instead of slider
	- treasure map that highlights where to go to find something


sfx changes
- ore collected too harsh
- jetpack fan noise too loud
 - powerup collected too harsh
- explosive fuse just needs to be a sizzle sound
- gas release can just be a short woosh
- procesing completed is the right vibe but too harsh
- need a portal sound

reference games:
Aground
steamworld dig 2: art reference
dome keeper


I think the biggest visual issue with the game currently is the drastic difference in pixels per unit across the various assets. I like the level of detail in the buildings and parallax backgrounds. Could you put together a checklist of everything that would need to be regenerated to match?



I want to create more hazards. can you give me a list of ideas?
	- arrow trap room with treasure at the end
	

