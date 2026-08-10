# Processing Implementation
the processing building is currently already added to the scene, but it doesn't do anything.

Processing is the term used for taking the ores, and combining them, to make an output that is worth more money

# Mechanics
- the player queues up the processing center to churn out a recipe. The player chooses how many of the item to make, the
- the processor will pull the items from the Depot upon starting its work.
	- if the process is cancelled, the items will be refunded back to the depot
- each recipe will have:
	- a preset sale value
	- duration to process
	- materials required
- each recipe is an individual upgrade that has to be purchased

# UI
- the main panel is split into each of the unlocked processing queues
	- click the "select recipe" button to select a recipe
		- a model pops up showing all unlocked recipes 
	- select the amount to create with a slider (up to how many materials are available in the depot)
	- click "start"


# Upgrades
- there will be a purchaseable upgrade per recipe, each requiring the last to be purchased
	- wood: Chairs
	- stone: Pillars
	- coal: none by itself
	- iron: swords
	- gold: bracelets
	- emerald: earrings
	- diamond: wedding rings
- other upgrades
	- processing time
		- each recipe has a preset duration. This upgrade multiplicatively reduces the duration of all recipe crafting
	- processing queue
		- allows multiple recipes to be running at once
	- processed good sale value
		- increases the sale value of processed goods
		
# Prestige upgrades
- unique recipes with more diverse material requirements
	- steel: coal + iron
	- tiara: iron + gold + emerald
	- crown: diamond + gold + emerald
	- shard of possibility: 1 of each ore
- recipe mastery
	- completing a recipe will count towards how many of a recipe you have crafted. At 5,10,20,40, etc crafts, the value of the recipe will increase by 20%

