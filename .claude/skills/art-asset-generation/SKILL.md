---
name: art-asset-generation
description: Generate pixel-art game assets (UI/upgrade/currency icons, ore tiles, buildings) for idle-game-testing via OpenRouter, matching the project's neon fantasy-mine style, Orbitron font, and per-category size/style specs. Use when creating or regenerating game art.
---

# Art asset generation
- A vibrant pixel-art style of glowing ores, crystalline minerals, and ancient tech in a neon-lit fantasy mine, each block detailed with sci-fi textures and magical luminescence, rendered in clean anime-inspired lines with soft depth and dynamic lighting.

## Building-style prompt (use for world tiles and props)
The buildings in `Assets/Textures/Buildings` are the style target. Generate image-to-image with a building (e.g. `market.png`) attached as the style reference, and open the prompt with:

> Use the attached image ONLY as an art-style reference (do not draw the building). Match its pixel-art style exactly: chunky, clean, hand-placed pixel clusters with the same pixel size; dark near-black navy/brown outlines around every shape; large readable forms; 3 to 4 flat cel-shading tones per material with hue-shifted shadows and a lighter top-left rim highlight; limited palette; no anti-aliasing, no blur, no gradients, no dithering, no single-pixel noise or speckle.

Then describe the subject. For tiles add:

> a flat, front-facing, perfectly square texture filled edge-to-edge (no border, no frame, no bevel, no perspective, no drop shadow), designed to tile seamlessly with itself, drawn as bold shapes rather than fine grain.

- Describe terrain as organic shapes ("irregular polygonal clods, like dried cracked mud"). Words like "plates", "strata", "layers" come back as bricks or planks, and saying "no brick look" makes it worse.
- Do not rely on the prompt for colour: hex codes are ignored when a reference image is attached (results stay orange) and asking for "muted, low contrast" softens the pixels. Generate for shape and crispness, then gradient-map onto the game palette in post.
- Once one sheet has the right look, attach that sheet (not the building) as the reference for further variations; it holds pixel size and style better.
- Background materials (dirt) must be calm: no large stones, thin cracks, so ores drawn on top stand out.
- Batch as a 2x2 sheet on flat pure magenta (#FF00FF) with a thick magenta gutter, one variation per quadrant.
- The model still draws a dark frame around each tile and the edges do not tile; trim ~8px per side and fix the seams afterwards.
- Snap the result to the art-pixel grid with `Tools/Tiles/pixelize.py` (96x96 art pixels, stored x4 = 384px, 384 PPU).

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
