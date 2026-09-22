---
name: art-asset-generation
description: Generate pixel-art game assets (UI/upgrade/currency icons, ore tiles, buildings) for idle-game-testing via OpenRouter, matching the project's neon fantasy-mine style, Orbitron font, and per-category size/style specs. Use when creating or regenerating game art.
---

# Art asset generation
- A vibrant pixel-art style of glowing ores, crystalline minerals, and ancient tech in a neon-lit fantasy mine, each block detailed with sci-fi textures and magical luminescence, rendered in clean anime-inspired lines with soft depth and dynamic lighting.

## Fonts
- for any text use the Orbitron font
	- the file is "Orbitron-Regular SD TMP"


## Asset requirements
- UI/Upgrade/Currency Icons
	- size: 64x64
	- style notes: flat icon style
- Ore tiles
	- size 128x128
	- style notes: 
		- alpha transparency
		- 128 pixels per unit
		- flat, top-down square textures meant to tile edge-to-edge on a Tilemap — like a Minecraft texture pack
- Buildings 
	- size 256x256
	- style notes: should resemble the purpose of the building, largely mechanical with ancient/futuristic blend
## Asset Generation
- Use OpenRouter to generate art assets.
- Ask question relevant to saving on credits/tokens. Check if there's ways the user can give you input to save costs.
- when generating similar assets, attempt batching them into sprite sheet requests
