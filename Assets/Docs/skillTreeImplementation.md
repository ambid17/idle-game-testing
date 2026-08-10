# Backwards compatability
- currently our "skills trees" are defined in the UI by a tabbed interface. 
	- I want to keep that code available in case this new process doesn't work. 
	
# New Implementation
- make a new "skill tree" panel. It needs to be re-useable by both the market and the musuem so that we can buy regular upgrades and prestige upgrades.
	- this new panel should have a 2d moveable set of upgrades. You can click and drag to pan the panel. you can scroll in/out to zoom in/out.
	- upgrades should fan out based on their prerequisites. there will be several branches, and i want each branch to fan out from the center, radially based on category. For example, mining upgrades would go to the top left, economy upgrades would go straight up, automaton upgrades go to the right, etc.
	- upgrades will have an image, and a border
		- the border has a few options for colors:
			- gray: when locked
			- while: unlocked and not purchased
			- green: unlocked, and at least 1 upgrade tier purchased
			- gold: unlocked, and maxed out 
	- when you click on an upgrade, a separate modal will pop up showing the details about the upgrade such as:
		- display name
		- description
		- current/max level
		- cost
		- buy button
- there is an exhaustive list of the upgrades and what they do in "Assets/Docs/UpgradeIdeas.pdf"
	- this is not finalized in the proper relationship of the skill tree, but it gets each of the possible effects per category