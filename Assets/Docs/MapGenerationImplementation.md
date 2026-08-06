# Map Generation

## Design goals
- Deterministic per-run generation from a seed, so the same seed always produces the same mine (useful for testing, and lets us regenerate lazily instead of storing the whole descent up front)
- Effectively unbounded depth — the player and idle miners can descend indefinitely, so the grid can't be generated all at once
- Cheap to persist: only what's been changed (mined blocks, revealed fog) needs to be saved; everything else is regenerated from the seed on demand
- Full regeneration on prestige (new seed, all tunnels wiped), except the grid-width prestige upgrade level, which carries into the next generation

## Grid & layer structure
- The mine is a 2D grid: fixed width in blocks (upgradeable via the prestige "Increase grid size" upgrade) x unbounded depth. The fixed default width is 30 blocks.
- Depth is divided into **layers**, 100 blocks tall (matches the existing Map Layout rule). Each layer has its own weighted mineral spawn table, dirt tint, and mining-speed modifier
- A layer is also the natural unit of generation/streaming — treat it as a **chunk**: width x 100 cells, generated and loaded/unloaded together
- Layer-completion tracking (needed for the 50/75/95% passive value-multiplier upgrade) is chunk-level bookkeeping: mined-cell count vs. total cells in that chunk

## Cell data
- Each cell stores: block type (ore/dirt/hazard/artifact id), a mined flag, and a revealed (fog-of-war) flag
- Packs into one byte or a small struct per cell — a chunk (e.g. 40 wide x 100 tall = 4,000 cells) is only a few KB, so keeping many chunks resident is cheap
- Only chunks that have actually been visited need to exist in memory or save data at all

## Generation algorithm
1. When a chunk (layer) is first needed — player or an idle miner reaches its depth range — generate it:
	- Look up that layer's weighted spawn table (ore weights, hazard/power-up weights, dirt fallback)
	- Roll each cell independently using a seeded RNG derived from (world seed, layer index, cell coordinates), so regeneration is deterministic and untouched cells never need to be stored
	- Second pass places artifacts: guarantee at least 1 per layer (reserve a random valid cell if none rolled organically), plus a bonus chance that increases with depth
	- Hazard/power-up blocks (treasure chest, explosive, falling rocks, gas pocket, etc.) are their own weighted pass layered over the dirt/ore result, not competing slots in the ore table
2. Mining a cell sets its mined flag — the only thing that needs to be written to save data for that cell
3. Fog-of-war reveal is a flood-fill outward from mined cells by a radius (base radius at Lantern tier 0, extended by the Lantern upgrade tree; "true sight" capstone reveals the whole loaded chunk)

## Idle/offline simulation
- Automated miners must run the *same* generation + mining logic when the game is closed, not a separate approximation — the generation function needs to be callable headless (no rendering) so miner progress can be fast-forwarded across many layers between sessions

## Persistence & prestige
- Save data per chunk: chunk index, mined cell coords (or a dense bit array if mostly mined), discovered artifact locations
- On prestige: discard all chunk save data, generate a new seed, keep only the grid-width upgrade level (and any "keep passive layer bonus between prestiges" capstone state, which lives outside the grid)

## Unity technical approach
- **Rendering**: one `Tilemap` per loaded chunk (terrain + a fog overlay Tilemap), pooled and repositioned as the player/camera descends rather than instantiated fresh per chunk
- **Tile data**: a `TileBase`/`RuleTile` per block type, referenced from a `BlockType` ScriptableObject (value, weight, mining time/tier, tint) — mirrors the existing "Block types" list below
- **Layer config**: a `LayerConfig` ScriptableObject per 100-block layer (or a formula-driven table beyond an authored range) holding the weighted ore table, hazard table, dirt tint, and mining-speed modifier — tunable in the Editor without code changes
- **Chunk loading**: keep a small window of chunks resident (current layer ± 1) as live Tilemaps for rendering/collision; chunks outside the window keep only their sparse mined-cell data
- **Fog of war**: a second Tilemap per chunk using an opaque "unrevealed" tile, cleared (`SetTile(null)`) as cells are revealed — avoids a GameObject per cell
- **Collision**: `TilemapCollider2D` + `CompositeCollider2D` merges solid tiles into one static collider per chunk for player-vs-terrain collision. Batch mining/generation writes (e.g. per frame) rather than calling `SetTile` one cell at a time — refreshing the composite collider on every single edit can hitch
- **Hazard/artifact blocks**: Tilemap tiles have no physics or per-cell events of their own, so these are represented two ways at once: a special tile for rendering plus a lightweight data entry (position + type) in a chunk-local list, checked on mine. Anything that needs actual physics (falling rocks once undermined, explosion debris) is *promoted*: clear the tile, spawn a pooled prefab with a `Rigidbody2D` at that position, let it fall/fly, then despawn or resolve it — physics only runs on the handful of cells mid-event, never on the static grid itself

## Open items to confirm before implementation
- Exact fog-of-war base reveal radius and how each Lantern tier scales it
