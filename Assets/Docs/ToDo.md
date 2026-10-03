todo 

market/museum upgrade ordering
museum collection UI scaling
out of fuel death animation has long waits

dont let automatons target artifacts if the player hasn't mined at least 1
are there more UI animations, VFX, or fluff we could add to make the game more satisfying? 

To test
1. Panels open and close instantly. Every panel just switches on and off (MarketUI.cs:74, and the same pattern elsewhere). One shared component that fades panels in with a small 0.95→1 scale over about 0.12s, using unscaled time so it still runs while paused, would lift every screen in the game. This is the highest value for the least work.
2. Buying an upgrade has no payoff. MarketUI.OnUpgradePurchased just refreshes the list. In the skill tree, the bought node could punch in scale with a flash and a sparkle burst (WorldEffects already has sparkle art), and the connector lines could light up toward the nodes it just unlocked.
3. Pulse what you can afford. Nodes and buttons could glow gently when they first become affordable, with a small badge on the HUD that says "upgrade available". This pulls players back to the shops.
4. Buttons could react to hover and press with a small scale-and-squash, alongside the button sounds. Separately, the bulk UIButtonSound tool still hasn't been run.
5. The money readout could react to income with a quick scale-and-color punch, and big jumps could roll up through AnimatedCounter. I haven't checked whether the HUD money already uses AnimatedCounter.
6. Toasts could slide in with a slight overshoot. Layer-bonus tiers (LayerBonusTracker.cs:34) are milestones but currently show as plain toasts, so they could get their own banner.
7. Biome title card. When you first cross into a biome, show an "Entering <Biome> · 300m" banner with a sting sound. It makes depth feel like progress.
8. Rising pitch on fast mining. Breaking blocks quickly in a row nudges the break sound's pitch up a little each time, like a combo meter but without any UI. It's a small change that makes mining feel noticeably better.
9. Bigger breaks for valuable ore. Rare ore could get its own ring flash, a glint and a distinct sound, scaled by the ore's value. Right now every ore breaks the same way except for its tint.
10. Show the drones earning. When drones deposit, a small ore or coin pop over the Depot would make the passive income visible.
11. Offline earnings count-up. The offline earnings popup could count up to the total with a coin shower when you claim it.


- map
	- Wandering events, starting with the treasure mole and cave-in warning. 
- processing
	- make craftable utility items. maybe the teleporter is a crafted item?
- steam 
	- ensure steam deck compatibility
		claude steps:
			1. A Deck detection service that applies Deck defaults (resolution, UI scale, controller button icons, frame-rate cap).
			2. An audit of every UI panel at 1280×800 that flags text likely to end up under 9px.
			3. A plan to unify the canvas scaling setups.
			4. A review of the offline-progress code for sleep/resume problems.
- ideas 
	- upgrades
		- chance to duplicate ores on mine
	- treasure map that highlights where to go to find something
	- ? move depth and layer to minimap, add biome/structure name to it


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

