Here is the current density of each group, at its in-scene scale. "Texels per unit" is stored texture pixels per world unit; "art pixels per unit" is how many visible chunky pixels fit in a unit, which is what the eye compares. Art-pixel figures are estimates from run-length measurements on one or two representative files per group.

┌───────────────────────────────────────────────────────────────┬───────┬───────────────────┬─────────────────────┐
│                          Asset group                          │ Count │  Texels per unit  │ Art pixels per unit │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Buildings (market, depot, museum, control center, processing) │ 5     │ 341               │ ~85–115             │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Backdrop mid planes                                           │ 7     │ 102               │ ~26                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Backdrop near planes                                          │ 7     │ 102               │ ~13                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Backdrop cave far planes                                      │ 4     │ 102               │ ~7                  │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Backdrop sky far planes                                       │ 4     │ 26                │ smooth gradient     │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Horizon and surface soil backdrops                            │ 3     │ 102               │ ~26                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Base tiles (dirt, grassy dirt, hardpan, ancient brick)        │ 4     │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Ore foregrounds and power-up tiles                            │ 23    │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Hazard tiles                                                  │ 4     │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ EdgeBleed tiles                                               │ 47    │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Crack overlays                                                │ 8     │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Chest, drones, teleport portal                                │ 6     │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Player accessories                                            │ 5     │ 128               │ ~128                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Player sheets                                                 │ 2     │ 100               │ ~100                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Automaton sheets                                              │ 2     │ 100               │ ~100                │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Critters and hats (drawn at 0.5–0.8 units)                    │ 21    │ 155–245           │ ~155–245            │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Gas cloud                                                     │ 1     │ 256 at base scale │ not measured        │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Sky relic                                                     │ 1     │ 71                │ ~71                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Critter shop                                                  │ 1     │ 52                │ ~52                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Cloud platform                                                │ 1     │ 50                │ ~5–25               │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Ore nugget (drawn at 0.4 units)                               │ 1     │ 40                │ ~40                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Sleep Z                                                       │ 1     │ 64 at full size   │ ~32                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Debris chips                                                  │ 1     │ 16                │ ~16                 │
├───────────────────────────────────────────────────────────────┼───────┼───────────────────┼─────────────────────┤
│ Fog tile (flat colour)                                        │ 1     │ 16                │ n/a                 │
└───────────────────────────────────────────────────────────────┴───────┴───────────────────┴─────────────────────┘

- Screen reference: the camera shows about 108 screen pixels per unit at 1080p and 144 at 1440p. Anything above that in the texel column is being shrunk on screen; anything well below is being enlarged.
- Runtime-scaled sprites: the gas cloud is scaled to its hazard radius, and the Sleep Z animates between 0.5× and 1× size, so their on-screen density varies.
- Estimated rows: the horizon/surface-soil row is based on the surface soil file only, and the accessories and hazard tiles r 128 px sprites rather than measured individually.
- UI icons: the 64 px icons (upgrades, prestige, currency, hazard, power-up, recipes) have no world density because they are only drawn in the UI.


# TPU vs APPU

Texels per unit is how many pixels of the stored image file cover one world unit. Art pixels per unit is how many of the visible "drawn" pixels cover one world unit. They differ whenever one drawn pixel is stored as a block of several file pixels.

The market building shows the gap:

- File: it is 1024 px wide and displayed 3 units wide, so 1024 ÷ 3 ≈ 341 texels per unit.
- Art: each chunky pixel you can see in it is a block about 4 file pixels wide, so it only has about 341 ÷ 4 ≈ 85 art pixels per unit.