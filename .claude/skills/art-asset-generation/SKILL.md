---
name: art-asset-generation
description: Generate pixel-art game assets (UI/upgrade/currency icons, ore tiles, buildings) for idle-game-testing via OpenRouter, matching the project's two styles - flat smooth pixel art in a cyan/gold/dark gray/white/neon-purple palette for UI, the neon fantasy-mine building style for world art - plus the Orbitron font and per-category size/style specs. Use when creating or regenerating game art.
---

# Art asset generation
There are two styles. UI art follows "UI style" below; world art (tiles, props, buildings, backdrops) follows the building style.

- World art: a vibrant pixel-art style of glowing ores, crystalline minerals, and ancient tech in a neon-lit fantasy mine, each block detailed with sci-fi textures and magical luminescence, rendered in clean anime-inspired lines with soft depth and dynamic lighting.

## UI style (icons, buttons, panels, HUD - anything drawn on the UI)
Flat, smooth pixel art. `Assets/Textures/UI/SellLockClosed.png` (lock) and `Assets/Textures/UI/HudIcon_Health.png` (heart) are the style target - attach one as the style reference when generating image-to-image, and compare new art against them before importing.

- One bold, simple, front-facing shape that still reads at 64x64. No scene, no background, no perspective.
- Large flat colour areas with smooth, clean edges: 2 to 3 tones per colour (base, one shadow, one highlight), a dark outline, and a small white specular glint. No texture, grain, noise, dithering, gradients, bevel-and-emboss, glow halos or soft blur.
- Palette, and nothing outside it:
	- gold - the main body colour (base `#F5B529`, shadow `#D08515`, highlight `#F5D560`)
	- cyan - the secondary/interactive colour (`#22F0F0`)
	- dark gray - backing plates, panels, disabled states, and the near-black outline (`#201040`-ish dark navy)
	- white - highlights, glints and text
	- neon purple - accents only (a gem, a trim line, a status pip), never the main fill
- Colour is not reliable from the prompt (see below): generate for shape and smoothness, then remap onto the palette in post if it drifts.

Prompt opener for UI art:

> Use the attached image ONLY as an art-style reference (do not draw its subject). Match its style exactly: flat, smooth pixel art icon; one bold simple shape, front-facing, centred; large flat colour areas with clean smooth edges; 2 to 3 flat tones per colour, a dark navy outline and a small white highlight; golden-yellow body with cyan details, dark gray secondary parts, white highlights and small neon purple accents only; no texture, no noise, no dithering, no gradients, no glow, no background.

Workflow that produced the power-up icons and badges (2026-10-02):
- Batch every icon into one request on flat pure magenta (#FF00FF). The model ignores the requested grid and may draw extras, so don't rely on cell positions: `python Tools/UI/slice_icon_sheet.py <sheet.png>` lists the blobs it finds, then `... <sheet.png> 3=Assets/path/Icon.png` writes the chosen ones as 64x64 transparent sprites (keeps purple accents, cuts enclosed handle holes).
- A 2x2 sheet of the lock, heart, fuel and backpack HUD icons as the reference held the style better than one icon. For a redo of a weak icon, attach the first generated sheet (`Tools/UI/icon_sheet_raw_a.png`) as the reference and ask for just one or two large icons.
- Upgrade icons (2026-10-02, sheets in `Tools/UI/upgrade_icon_sheets/`): with the first good sheet as the reference, a 3x3 grid was honoured every time, so `python Tools/UI/slice_icon_grid.py <sheet.png> 3x3 1=… 5=…` cuts by cell and keeps multi-piece icons (robot + "+", drill + speed lines) whole, which the blob slicer splits. Expect ~1 in 8 icons to miss its brief (arrows pointing the wrong way, chevrons drawn as hearts); put the misses on one redo sheet, and fix simple ones (flipping arrows) in post.
- The reference image must be under `Assets/` for `generate_image`; use a temporary `Assets/_Concepts` folder and delete it afterwards.
- Overwrite the existing PNG in place so the `.meta` and every reference survive.

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
	- style notes: flat smooth pixel art in the UI palette - see "UI style" above
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
