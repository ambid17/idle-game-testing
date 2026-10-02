Art asset prompts

# UI (icons, buttons, panels, HUD):

Flat, smooth pixel art. Style targets: Assets/Textures/UI/SellLockClosed.png (lock icon) and
Assets/Textures/UI/HudIcon_Health.png (health icon).

Palette: cyan, gold, dark gray, white, with neon purple accents only.
- gold is the main body colour (base #F5B529, shadow #D08515, highlight #F5D560)
- cyan (#22F0F0) for secondary/interactive details
- dark gray for backing plates and disabled states, dark navy (#201040) outline
- white for highlights, glints and text
- neon purple as a small accent, never the main fill

A flat, smooth pixel art icon of <subject>: one bold simple shape, front-facing and centred,
64x64. Large flat colour areas with clean smooth edges, 2 to 3 flat tones per colour, a dark
navy outline and a small white highlight. Golden-yellow body with cyan details, dark gray
secondary parts, white highlights and small neon purple accents only. No texture, no noise,
no dithering, no gradients, no glow, no background.


# Ores:

A vibrant pixel-art style of glowing ores, crystalline minerals, and ancient tech in a
neon-lit fantasy mine, each block detailed with sci-fi textures and magical luminescence,
rendered in clean anime-inspired lines with soft depth and dynamic lighting.

Create a sprite sheet: a clean 2x2 grid of 4 separate FLAT top-down ore texture tiles, one
per cell. Each tile is a completely flat, straight-on orthographic texture swatch (like a
Minecraft texture pack tile) — NOT an isometric cube, NOT a 3D block render, NO perspective
or depth, NO visible cube edges or side faces. Each tile must completely fill its square
cell edge-to-edge with no border, margin, or transparent padding inside the cell — the
texture pattern should look like a seamlessly tileable stone surface photographed straight-on.

- Top-left: coal ore texture (dark stone surface with embedded glowing dark charcoal/
  ember-orange coal flecks)
- Top-right: iron ore texture (grey-brown stone surface with warm rust-orange glowing
  metallic veins)
- Bottom-left: gold ore texture (stone surface with bright glowing golden-yellow veins
  and nuggets)
- Bottom-right: diamond ore texture (stone surface with glowing cyan-white crystalline
  diamond clusters)

All four textures must share identical stone base pattern, identical flat even lighting
(no directional shadow, no highlight sheen suggesting 3D form), and identical scale/grain,
differing only in the embedded mineral color and shape. Leave only a thin transparent
gutter of a few pixels between the four cells (not inside each tile) so they can be cropped
apart cleanly. No text, no labels, no grid lines, no borders, no watermark, no isometric cubes.