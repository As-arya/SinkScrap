# Blender Asset Brief

Handoff for the session that builds SinkScrapt's models in Blender (through the `blender` MCP server in `.mcp.json`). Read this together with `CLAUDE.md`, `Docs/PRD.md` and `Docs/Verification.md`.

## Current state

Everything visible in the game is still generated in code: primitives and textured boxes from `WorldFactory` (`Assets/SinkScrapt/Runtime/WorldZone.cs`, `WorldInterior.cs`, `WorldExterior.cs`), procedural textures from `Surfaces.cs`, monsters from `EnemyRig.cs`, and items from `WorldFactory.ItemVisual`. The goal is to replace the most-seen pieces with proper low-poly models, one category at a time, without changing gameplay.

The user may add reference images to `Docs/References/<category>/<asset-id>.png` (for example `Docs/References/items/scrap_valve.png`) or paste them in chat. Check that folder before modelling an asset. If an image disagrees with the sizes below, the sizes win (gameplay depends on them) and the image guides shape and style.

## Art direction

- Gloomy, stylised PS1-era industrial horror: corroded steel, worn paint, chunky readable silhouettes, low polygon counts, no fine bevels, no realistic sculpt detail. Seen at a 0.55 render scale through the retro filter (`Art/RetroPost.shader`), so small detail disappears; shape and colour blocks carry the look.
- Palette already used in code: hull blue-grey, steel `(.24,.31,.32)`, rust `(.48,.23,.13)`, teal `(.19,.49,.47)`, cream `(.74,.71,.56)`, off-white paint, antifouling red, hazard yellow/black, brass for portholes and compasses.
- Player-facing text on models (signs, labels) is Indonesian.

## Technical rules

- **Units and scale:** 1 Blender unit = 1 m. Model at real size. Apply all transforms before export.
- **Polygon budgets (triangles):** small item 100–400; two-handed item 300–800; furniture and kit pieces 50–600; large exterior pieces (bridge house, crane) 1,000–4,000; whole small boat ≤ 4,000; monsters ≤ 2,000 each. Target hardware is a GTX 1650 with up to four players.
- **Materials:** two routes, pick per asset.
  - *Large surfaces and kit pieces:* UV unwrap in **metres** (cube projection, 1 UV unit = 1 m) and name material slots after the `Surface` enum in `Surfaces.cs` (`Plate`, `RustPlate`, `Diamond`, `Grating`, `Bulkhead`, `DeckPaint`, `Wood`, `Planks`, `Corrugated`, `Rock`, `Concrete`, `Hazard`, `Hull`, `Sand`, `Tile`). Unity then applies the existing procedural materials, which keeps everything consistent and needs no texture files.
  - *Small hero items:* one material, a baked or hand-painted texture of 128 px (max 256), point-filtered in Unity. Keep texture files next to the model.
- **Pivots:** floor objects at bottom centre; wall-mounted objects at the bottom centre of the face that touches the wall; items at their centre (the game positions and spins them around the origin).
- **Facing:** the front of an object faces Blender **−Y**. Export FBX with *Forward −Z, Up Y, Apply Transform, Apply Scalings: FBX All*. Verify the orientation on the first import in Unity and write down the confirmed settings here.
- **No colliders in the models.** Gameplay keeps its box colliders from code; monster navigation and the sealed-hull QA test depend on them. Models must fit inside those boxes (sizes below).
- **Files:** Blender sources go in `ArtSource/Blender/<category>.blend` (outside `Assets`, so Unity does not try to import `.blend`). Exports go in `Assets/SinkScrapt/Resources/SinkScrapt/Models/<Category>/<asset-id>.fbx`, where `ModelLibrary` (`Runtime/ModelLibrary.cs`) loads them by path at runtime, strips any colliders and swaps the material slots for the game's procedural materials. No prefab step is needed. Asset ids are lower_snake_case and match this list.
- **Axes (confirmed on import, 2026-10-15):** with these export settings Unity x = −Blender x, Unity y = Blender z and Unity z = −Blender y. That is a 180° turn, not a mirror. Pieces modelled before 2026-10-15 assumed Unity x = Blender x. That only matters for the asymmetric ones (`ext_lifeboat_davit`, `boat_fenders`, `isl_fuel_tank`'s ladder); the 2026-10-15 boat (`boat_*`) is modelled with the correct sign.
- **Provenance:** if anything is downloaded (Poly Haven, Sketchfab, and so on), ask the user first, prefer CC0, and record source and licence in `Docs/Assets.md`.

## Interior grid facts (from `InteriorLayout.cs` / `WorldInterior.cs`)

- Cell 4 x 4 m. Deck floors at y = 2, 5, 8 (interior coordinates); ceiling height 2.76 m (ceiling panel), wall skins 0.1 m thick, wall top 2.98 m above the floor.
- Door opening **1.6 m wide x 2.25 m high**, centred on a cell edge. Stair and airlock openings **3.0 m wide x 2.5 m high**.
- Stair shaft: 2 cells long (8 m), 3.5 m wide flight; 1 m bottom landing, 16 steps over 6 m run and 3 m rise (0.375 x 0.1875 m), 1 m top landing.
- Airlock: 4 m wide, 10 m long beyond the hull end; 11 steps over 4 m rising 2 m, then a 6 m platform at the cargo-deck height, end wall with the "Kembali ke dek" hatch.
- **Furniture rule (monster paths):** furniture stands only against closed walls, at most **1.0 m deep** and **3.6 m long**, or in cell corners (at most **0.9 m** along a wall that has a door). The middle 1.4 m band through every cell and doorway must stay empty. Keep heights ≤ 2.2 m.

## Build list, in priority order

### 1. Scrap items and tools (`ItemCatalog` in `GameData.cs`; visuals in `WorldFactory.ItemVisual`)

One-handed items must fit a **0.4 m cube** (their pickup box); two-handed items must fit **0.8 x 0.6 x 0.6 m**. They are also shown in the player's hand at 65–70% scale, so read them at a glance.

| id | asset-id | In game (Indonesian) | Notes |
|---:|---|---|---|
| 0 | scrap_bolt_box | Kotak baut | small metal box of bolts, 20 cr |
| 1 | scrap_steel_plate | Pelat besi | rusty plate offcut, 25 cr |
| 2 | scrap_valve | Katup | gate valve with handwheel, 30 cr |
| 3 | scrap_pressure_gauge | Pengukur tekanan | round dial with pipe stub, 35 cr |
| 4 | scrap_ship_lamp | Lampu kapal | caged bulkhead lamp, 40 cr |
| 5 | scrap_old_battery | Baterai bekas | heavy lead battery, 45 cr |
| 6 | scrap_broken_radio | Radio rusak | old marine radio set, 60 cr |
| 7 | scrap_circuit_board | Papan sirkuit | green board, chips, 75 cr (valuable: faint glint) |
| 8 | scrap_brass_compass | Kompas kuningan | brass binnacle compass, 90 cr (valuable) |
| 9 | scrap_electric_motor | Motor listrik | **two-handed**, finned motor, 110 cr |
| 10 | scrap_industrial_pump | Pompa industri | **two-handed**, pump with flanges, 130 cr |
| 11 | scrap_generator | Generator | **two-handed**, small genset, 150 cr |
| 12 | tool_flashlight | Senter | lens on local +Z (the held torch shines forward) |
| 13 | tool_radio | Radio | handheld radio with antenna |
| 14 | tool_oxygen_tank | Tabung oksigen | blue tank with valve |
| 15 | tool_flare | Flare | red flare stick |

**Status (2026-10-15):** all 16 modelled in Blender 5.2.2 (`ArtSource/Blender/items.blend`, collection `Items`) and exported to `Assets/SinkScrapt/Resources/SinkScrapt/Models/Items/<asset-id>.fbx`. `WorldFactory.ItemVisual` now loads them through `ModelLibrary` and falls back to the old primitives when a model is missing. Not yet checked in a running game, so the orientation and export settings are still unconfirmed in Unity.
- Each item is one mesh with its origin at its bounding-box centre; 124–512 triangles; every one fits its pickup box (one-handed ≤ 0.4 m, two-handed ≤ 0.8 x 0.6 x 0.6 m).
- One shared material `SS_Items` uses a palette atlas: `items_palette.png` (128 px, 8 x 8 swatches of 16 px; the item swatches use the `ItemCatalog` colours) plus `items_palette_emission.png` (only the glowing swatches: lamp glass, torch lens, flare tip, circuit LED, radio screen). Import both point-filtered, without mipmaps. In Blender, the swatch for each face comes from the integer face attribute `ss_mat`.
- Flashlight and flare point their lens or striker toward Blender −Y, which should become Unity +Z.

### 2. Interior structure kit (`WorldInterior.cs`: `WallSide`, `Porthole`, `CeilingLamp`, `CorridorCell`, `Stair`, `Airlock`)

| asset-id | Size / fit | Replaces |
|---|---|---|
| kit_door_frame | hatch frame around a 1.6 x 2.25 m opening, raised sill ≤ 0.08 m, ≤ 0.15 m proud of the wall | jambs, lintel, sill boxes |
| kit_door_leaf | open watertight door with dogs/handles, lies flat against the wall beside the opening (no collision) | new |
| kit_wide_opening_frame | frame for the 3.0 x 2.5 m stair and airlock openings | header trim |
| kit_frame_rib | vertical I-beam rib, 2.98 m tall, ≤ 0.22 m wide, ≤ 0.12 m proud | "Frame rib" box |
| kit_deck_beam | ceiling beam across a 4 m corridor, ~0.2 m deep, bottom ≥ 2.55 m above the floor | "Deck beam" box |
| kit_porthole | round porthole (glass ~0.46 m), brass ring and bolts; the glass gets a faint emissive in Unity | porthole cylinders |
| kit_ceiling_lamp | caged bulkhead lamp, ~0.5 x 0.28 m, plus a broken variant | lamp housing, glass, cage |
| kit_pipe_straight / kit_pipe_elbow / kit_pipe_flange | 4 m straight runs (0.1 m and 0.18 m diameters), elbow, flange | overhead pipes |
| kit_cable_tray | 4 m tray, 0.5 m wide | "Cable tray" box |
| kit_valve_wheel, kit_fuse_box, kit_extinguisher | wall fittings, ≤ 0.2 m proud | corridor fittings |
| kit_stair_flight | the full 16-step flight with nosing and both handrails, fitting the shaft above | step boxes and rails |
| kit_airlock_flight | the 11-step airlock flight with rails | airlock steps |
| kit_hatch_exit | "Kembali ke dek" hatch with wheel, 1.8 x 2.4 m | teal box plus wheel |

**Status (2026-10-15):** 21 meshes modelled in Blender 5.2.2 (`ArtSource/Blender/interior_kit.blend`, collection `Kit`) and exported to `Assets/SinkScrapt/Resources/SinkScrapt/Models/Kit/<asset-id>.fbx`. Wired into `WorldInterior` on 2026-10-15 (see "Wiring" below). Triangle counts are 68–648. The stair flight (648) is slightly over the 600 kit budget, but it replaces 32 step and nosing boxes plus rails.
- **Material slots** use metre UVs (cube projection). A slot named `<Surface>` (for example `Plate`, `Diamond`, `Grating`, `Hazard`) means the code supplies the tint, as it does now for pipes and fuse boxes. A slot named `<Surface>__<Colour>` carries a fixed tint named after the colour constants in `WorldZone`/`WorldInterior` (`DarkSteel`, `Steel`, `Teal`, `Brass`, `RedPaint`, `Cream`), plus new ones: `DoorFrame` (.36,.40,.38), `Black` (≈.05), `White` (≈.85) and `DeadGlass` (.15,.15,.14). A slot named `Glow__<Colour>` is emissive: `Glow__Lamp` takes the code's LampCold/LampWarm, and `Glow__PortholeGlass` is (.02,.09,.10).
- **Pivots and extents** (Blender axes: −Y is the room/corridor side, the wall plane is y = 0):
  - Door frame, wide opening frame: bottom centre of the opening on the wall's centre plane. Flanges sit on both wall faces (±0.145 m). Door frame opening 1.6 x 2.25 with a 0.062 m sill; wide opening frame 3.0 x 2.5, no sill, hazard edging.
  - Door leaf: 1.5 x 2.15, back face on y = 0, hinge knuckles on +X (put that edge next to the jamb). It reaches 0.14 m proud.
  - Frame rib: bottom centre on the wall, 2.98 tall, 0.22 wide, 0.12 proud.
  - Porthole: bottom of the ring on the wall. The glass centre is 0.31 m above the pivot, so offset it from the code's centre position.
  - Ceiling lamps (`kit_ceiling_lamp`, `kit_ceiling_lamp_broken`), deck beam and cable tray: pivot at the centre of the face that touches the ceiling, with the geometry below z = 0. The beam is 4 m along X and 0.2 m deep. The tray is 4 m along Y.
  - Pipes: ids `kit_pipe_straight_d10/_d18` (4 m long), `kit_pipe_elbow_d10/_d18` and `kit_pipe_flange_d10/_d18`. The axis is Blender Y and the pivot is on the axis. The elbow pivot is where the two centre lines cross: one leg points −Y, the other +Z.
  - Valve wheel, fuse box, extinguisher: bottom centre on the wall, at most 0.2 m proud, with their heights in the model (valve 0.6 m, fuse box 0.6 m plus a 0.3 m conduit, extinguisher 0.65 m). The fuse box's status LEDs stay in code.
  - Stair flight: pivot at the back edge of the low cell on the floor, centred on the 3.5 m width, climbing toward −Y. It has 16 steps from s = 1 to 7 m, a top landing to s = 8 m at 3 m height, and both handrails at x = ±1.7. That is the same origin that `WorldInterior.Stair` uses. The airlock flight is 11 steps over 4 m rising 2 m, 3.6 m wide, pivot at its foot, rails at ±1.75.
  - Exit hatch: 1.8 x 2.4, bottom centre on the end wall, 0.22 m proud including the wheel (wheel centre 1.25 m up). A blank sign plate at 2.5–2.8 m sits behind the code's "KELUAR" text.
- Colliders stay in code: the solid step blocks and landing keep their current boxes.

### 3. Room furniture (`WorldInterior.Furnish`, one set per room type)

All within the furniture rule above (≤ 1.0 m deep, ≤ 3.6 m long; corner pieces ≤ 0.9 m wide).

| Room type (`type`) | asset-ids |
|---|---|
| 0 PALKA (cargo hold) | furn_crate_large, furn_crate_small, furn_barrel, furn_pallet_sacks |
| 1 GUDANG (storage) | furn_shelf_unit (3.2 m, 4 shelves), furn_storage_box |
| 2 KABIN KRU (crew cabin) | furn_bunk_bed (2.1 x 0.9 m, two tiers), furn_locker_row, furn_footlocker |
| 3 DAPUR (galley) | furn_galley_counter (3.4 m), furn_stove, furn_fridge |
| 4 RUANG MESIN (engine room) | furn_engine_block (3.2 x 1.0 x 1.8 m), furn_gauge_panel |
| 5 POMPA (pump room) | furn_pump_unit, furn_riser_pipe |
| 6 LISTRIK (electrical) | furn_electrical_cabinet (1.0 x 2.0 x 0.6 m; status LEDs stay in code) |

**Status (2026-10-15):** all 17 modelled in Blender 5.2.2 (`ArtSource/Blender/furniture.blend`, collection `Furniture`) and exported to `Assets/SinkScrapt/Resources/SinkScrapt/Models/Furniture/<asset-id>.fbx`. Wired into `WorldInterior.Furnish` on 2026-10-15. Triangle counts are 84–540.
- **Pivot and orientation:** bottom centre of the footprint, front toward −Y, back against the wall on +Y. This matches the code's `At(depth, …)` placement, which puts the centre `depth/2` off the wall. Material slots follow the kit convention above. New fixed tints: `Sand__Sacks` (.70,.65,.50), `Sand__Mattress` (.55,.60,.58), `Sand__Blanket` (.36,.40,.30), `Corrugated__Locker` (.45,.55,.52), `Plate__Galley` (.70,.72,.72), `Plate__Fridge` (.85,.86,.84), `Plate__Cabinet` (.50,.58,.52) and `Wood__BoxCard` (.75,.70,.62).
- **Code tint kept:** `furn_barrel` (random Rust/Teal/Steel), `furn_riser_pipe` and the engine crankcase (`RustPlate`) keep the code's tint, through slots without a colour suffix.
- **Sizes match the current code boxes:**
  - Crates 1.0 x 0.9 x 0.9 and 0.8³; barrel ⌀0.8 x 0.9.
  - Pallet and sacks 3.2 x 1.0 x 0.87; shelf unit 3.2 x 0.6 x 2.2 (boxes stay random in code); storage box 1.4 x 0.7 x 0.7 (the code shrinks it in corners).
  - Bunk 2.1 x 0.9 x 1.9 (the ladder adds 0.04 m at the front); locker row 1.95 x 0.55 x 1.9; footlocker 0.8 x 0.5 x 0.5.
  - Galley counter 3.5 x 0.74 x 0.94, with sink and tap (burners stay in code); stove 0.9 x 0.7 x 0.9 plus a 0.3 m backsplash; fridge 0.9 x 0.7 x 2.0.
  - Engine block 3.2 x 1.0 x 1.8; pump unit 2.95 x 0.8 x 1.1; electrical cabinet 1.0 x 0.6 x 2.0 plus conduit to 2.2.
- **Differences from the code:**
  - `furn_gauge_panel` stands on the floor (0.8 x 0.3 x 1.8). The code's panel floats at 0.6–1.8 m, so place the model on the floor.
  - `furn_riser_pipe` is the only piece above 2.2 m: it runs to the deckhead (2.98 m) like the code's 3 m riser. Its pivot is on the pipe axis at the floor, with the wall 0.25 m behind it.
- Status LEDs, text and colliders stay in code.

### 4. Exterior: wreck, boat and island (`WorldExterior.cs`)

Gameplay anchors must stay where they are: stations, rope ends, rescue ladder, access doors, deck heights. Fit models around them.

- **Wreck (deck top at y = 10; hull 29.8 m wide, 70 m long plus the bow taper to z = +40, keel at y = −6):** ext_bridge_house (three tiers at the stern, front face at z = −29.5 with access door A centred at x = 0), ext_funnel, ext_mast, ext_deck_crane (pedestal ≤ 1.4 m diameter), ext_container_40ft (6 x 2.8 x 10 m, door end detail), ext_lifeboat_davit, ext_forecastle_cabin (6 x 3 x 3 m, door B on its −z face), ext_windlass, ext_bollard, ext_floodlight_pole, ext_rail_section (1 m tall).
- **Small boat (deck top y = 1.0, deck 8 x 16 m centred at x = 25, z = −5):** boat_hull, boat_wheelhouse (open front at z = −8; the KEMUDI station stays at (25, 1.6, −9)), boat_radar, boat_fenders, boat_life_ring, boat_cargo_hold_frame (around the MUATAN AMAN station at (25, 1.45, −3)).
- **Island (harbour ground top y ≈ 0.9, around x 35–61, z −27 to 3):** isl_workshop (15 x 12 m, open front facing +z; stations JUAL SCRAP (44, 1.6, −10), TOKO ALAT (50, 1.6, −10), SELESAIKAN KUOTA (48, 1.6, −16)), isl_dock (12 x 4 m at x 28–40, z −7 to −3, deck top y = 1.0), isl_lighthouse (at (58, 0, 0), beacon at y ≈ 9.8), isl_fuel_tank, isl_power_pole, isl_boulders (a set of 4–6 rock variants), isl_shrubs.

**Status (2026-10-15):** 34 meshes modelled in Blender 5.2.2 (`ArtSource/Blender/exterior.blend`, collection `Exterior`) and exported to `Assets/SinkScrapt/Resources/SinkScrapt/Models/Exterior/{Wreck,Boat,Island}/<asset-id>.fbx`. Wired into `WorldExterior` on 2026-10-15. Material slots follow the kit convention. New fixed tints: `OffWhite`, `Antifouling`, `Yellow`, `Orange` (.90,.42,.12), `BoatTeal` (.20,.42,.44), `CabinPaint`, `Workshop` (.55,.62,.60), `Roof` (.62,.38,.26), `DockWood` (.62,.55,.46), `Piling` (.32,.26,.20), `Pole` (.55,.45,.38), `Scrub` (.35,.50,.30), `Chain` (.20,.16,.13) and `Rope` (= Cream). Glow slots: `Glow__WindowLit`, `Glow__Flood` (1,.88,.70).
- **Frame:** every exterior piece is modelled in its zone or ship frame and is placed with **no rotation** (unless noted) at the pivot listed. Blender x = Unity x, Blender −y = Unity +z and Blender z = Unity y, all relative to the pivot. Check the X sign on the first import, because the lifeboat and the fender lean to one side.
- **Wreck** (ship-local coordinates):
  - `ext_bridge_house`: pivot (0,10,−29.5), the tier-1 front face. It has three tiers, a wing walkway with rails, the code's lit/dark window pattern, a frame around door A and a sign plate. Door A, the "PALKA A" text, the lights, lifeboats, mast and funnel stay separate. 2,090 tris.
  - `ext_funnel`: pivot (0,18.1,−33.3). `ext_mast`: pivot (0,18,−31); the blinking bulb stays in code at +6.2.
  - `ext_deck_crane_pedestal`: pivot (x,10,z). `ext_deck_crane_house`: pivot on the slew ring (x,13,z), jib along local +Z like the code, so the code's yaw still applies.
  - `ext_container_40ft` (6 x 2.8 x 10) and `ext_container_8m` (for the stacked ones): bottom centre, door end on +Z.
  - `ext_lifeboat_davit`: pivot at the lifeboat centre (±7.3,14.4,−32.2), modelled for the +X side. Use scale X = −1 on the port side.
  - `ext_forecastle_cabin`: pivot (0,10,29.5), the door-B face, which looks to Unity −Z. `ext_windlass`: pivot (0,10,34), with both gypsies and the chain to the hawse pipes.
  - `ext_bollard`: double bitts, bottom centre. `ext_floodlight_pole`: bottom centre, lamps at +4.45–4.7.
  - `ext_rail_section`: 2 m long along X with its post at local −1 m; tile it and close each run with `ext_rail_post`. Keep the rope gap at z ±0.5.
- **Boat (rebuilt 2026-10-15, `exterior.blend` collection `Boat2`, wired into `WorldFactory.SmallBoat`):** a 10 x 20 m deck (Unity x 20..30, z −15..5, top y 1; `BoatLayout` in `GameData.cs`). It is modelled with Blender x = 25 − Unity x and Blender y = −(Unity z + 5). About 4,085 tris in all, slightly over the 4,000 boat budget.
  - `boat_hull`, `boat_cabin`, `boat_helm`, `boat_terminal`, `boat_tool_rack`: all pivot at (25,0,−5).
    - The hull carries one continuous rail (bulwark, stanchions, mid rail, cap rail 2.1) round the deck. Its only opening is the starboard gate at z −6..−4.
    - It also carries the cargo-area marking, the rope bitt (port, z 0), the rescue-ladder rungs (starboard, z 1), bollards, tyres and the life ring.
    - The cabin (x 21.5..28.5, z −14..−8) has a 1.6 m door, windows, a roof lamp, the deck floodlights, the radar mast and an exhaust stack.
    - The helm is at x 23.2, the computer (three CRTs, screens `Glow__Screen`) at x 26.8, and the tool rack (six shelves) on the back wall.
  - `boat_gate`: pivot on the hinge (29.96,1,−6), leaf toward +Z. Code swings it −90° while the island is loaded.
  - `boat_radar`: pivot at its spin centre (25,5.65,−11).
  - The old `boat_wheelhouse`, `boat_cargo_hold_frame`, `boat_radar_mast`, `boat_fenders` and `boat_life_ring` exports were removed.
  - 2026-10-15 fix: `boat_hull` had no deck over the bow (Unity z 5..10.3), so from aboard the bow looked missing (its inner faces are culled). It now has a `Planks` foredeck at y 1.0 following the sheer and an inner `Plate__OffWhite` bulwark skin up to the gunwale. The bow stays behind the forward cross rail; code adds stepped bow colliders. `boat_terminal` lost a stray 0.4 m `Plate__Black` bar that floated above the monitors. Both re-exported with the settings above (Forward −Z, Up Y, Apply Transform, FBX All).
- **Island:**
  - `isl_workshop`: pivot (48,1.1,−13), the slab top, with the same walls, columns and roof footprint as the code.
  - `isl_dock`: pivot (34,1,−5), the deck top.
  - `isl_lighthouse`: pivot (58,0,0). The door faces the harbour; the bulb stays in code.
  - `isl_fuel_tank`: pivot (58.5,0.9,z). `isl_power_pole`: pivot (x,0.9,z); wire ends at ±0.6, +4.95.
  - `isl_boulder_a..e`: unit 1 x 1.1 x 0.8 (scale by the code's `s`). `isl_shrub_a..c`: about 0.9 m across. Both pivot at the centre. The code rotates scrub randomly on every axis; use yaw only with the models.
- **Not modelled:** the cargo hull itself (`HullMesh` stays procedural and keeps its collider) and the bow mast.

### 5. Characters (rigged, done 2026-10-15)

Built by script in Blender 5.2.2: `ArtSource/Blender/scripts/charlib.py` (toolkit), `mon_hull_stalker.py`, `mon_drowned_hunter.py`, `crew.py`, `props.py`; source file `ArtSource/Blender/characters.blend`. Exports: `Resources/SinkScrapt/Models/Characters/<id>.fbx`, plus `Models/Props/prop_supply_crate.fbx`, `prop_parachute.fbx` and a new `Models/Items/tool_flashlight.fbx`.

- **Rigging:** one mesh per character, every primitive weighted 100% to one bone (PS1-style rigid skinning). Crew share `charlib.CREW_BONES` (Hips, Spine, Chest, Neck, Head, UpperArm/LowerArm/Hand, UpperLeg/LowerLeg/Foot .L/.R; front −Y, left +X). Clips are NLA strips; the FBX exporter writes each strip as a take named after the clip.
- **Export settings (characters):** Forward/Up as in `charlib.export`, Apply Scalings FBX All, **no** Apply Transform (it breaks armature animation), `add_leaf_bones=False`, NLA strips baked, scene at **30 fps** (the keys are placed at 30 fps; at Blender's default 24 fps every clip imported 25% too long). Unity imports them as **Legacy** with `bakeAxisConversion` (`ModelImportRules`), facing −Z; `ModelLibrary.Spawn` turns `Characters/*` by 180°, which also puts the right hand on +X.
- **Materials:** slot names `Col__RRGGBB` (flat) and `Glow__RRGGBB` (emissive); `ModelLibrary` reads the hex.
- **Assets and clips:**
  - `mon_hull_stalker` (1,956 tris): hulking grey hairless humanoid, ~2.3 m, pustules on the shoulders, dim eyes. idle 3.0, walk 1.2, run 0.75, attack 2.1 (wind-up 0.65 s, matches `attackWindUp`), grab 2.8 (lunge 0.45, lift, shake, throw at ~1.9 s, matches `NetEnemy.GrabLunge/GrabHold`).
  - `mon_drowned_hunter` (1,772 tris): pale anglerfish, ~3.3 m, origin at the body centre, lure bone `Lure2` (the glow light hangs there). idle, swim, chase, bite.
  - `char_crew_parka`, `char_crew_medic`, `char_crew_engineer`, `char_crew_fisher` (1,400–1,560 tris): idle, walk, run, crouch, swim, climb.
  - `npc_diver_merchant` (1,632 tris): brass-helmet diver with twin tanks; idle, wave.
- **Static re-exports for z-fighting (2026-10-15):** `scripts/fix_zfight.py` pushes trim pieces that were flush with a surface of another material 6 mm proud (grows the smaller island, or lifts a face off a connected mesh) and re-exports with the kit settings (`export_static`, which also un-hides a viewport-hidden collection; `exterior.blend` hides "Exterior", and an export from a hidden collection writes an empty 4 KB file). Applied to `ext_container_40ft`, `ext_container_8m`, `ext_funnel`, `ext_windlass`, `boat_cabin`, `boat_hull`, `isl_lighthouse`, `isl_workshop`, `isl_dock` and `kit_door_leaf`.

### 6. Feedback batch 3 (2026-10-16): doors, characters, monsters, items, debris, icons

Built in Blender 5.2.2 with two new toolkits: `scripts/kitlib.py` (static pieces with metre UVs, slabs with holes, `export_static`) and `scripts/charbody.py` (continuous skinned bodies). Reference images for the doors were pasted in chat by the product owner (a grey watertight door with a round porthole and a dog linkage on its back; a varnished plank cabin door with a chrome porthole).

- **Room doors (`scripts/doors.py`, `interior_kit.blend` collection `Doors`, `Models/Kit`).** The doorway is now **1.1 m** wide (`ShipGrid.DoorWidth`) with **one** 1.04 x 2.13 m leaf, replacing the squeezed double leaf (`kit_door_leaf` was deleted). Four styles, mixed per door by `WorldFactory.DoorStyleOf` (room type plus a seeded roll):
  - `kit_door_steel` (1,556 tris): grey `Plate__DoorGrey`, rounded corners, a stiffening rim, a chrome porthole with bolts and see-through `Glass__Porthole`, a long lever handle, two hinges in front and a six-dog linkage on the back.
  - `kit_door_wood` (1,176): `Wood__DoorWood` with vertical grain (UVs rotated, 2.4 per metre), board grooves, top and kick rails, a chrome porthole, brass plates, three butt hinges.
  - `kit_door_rusted` (808): `RustPlate__DoorRust`, no window, stiffener ribs, a red handwheel, dogs front and back.
  - `kit_door_cabin` (1,328): `DeckPaint__DoorCream`, a small square wired-glass window (`Glass__Wired`), a louvred vent and a kick plate.
  - Leaf frame: hinge axis at the origin, the leaf runs along Blender −X (Unity +X) to x = −1.04, bottom 0.07 m above the floor (clears the sill), thickness centred on y = 0, handle face toward Blender −Y (Unity +Z). Code hangs it 3 cm off the hinge jamb.
  - `kit_door_frame` (232) was rebuilt for the 1.1 x 2.25 m opening on a 0.1 m wall: a lining 6 mm inside the opening (no z-fighting with the wall block ends), rounded U-shaped trim flanges proud of both wall faces, and a hazard-striped sill. Where two skins meet the code still scales it x2 in depth.
  - The doors are over the 600-triangle kit budget; there are about 30 per wreck (≈ 40k triangles in all).
- **Glass:** slots named `Glass__<Tint>` map to `WorldFactory.Glass` (URP Lit, alpha-blended, glossy) cloned from `Resources/SinkScrapt/Glass.mat`, which keeps the transparent variant in builds. Tint alpha is the opacity (`Porthole` 0.42, `Wired` 0.55). `_BlendModePreserveSpecular` must be 0: with it on and SrcAlpha blending the glass rendered solid cyan.
- **Crew and merchant (`scripts/crew.py`, `characters.blend`).** Rebuilt on `charbody.py`: a stick skeleton of nodes with radii goes through the Skin modifier plus one subdivision level, so each body is one closed mesh without seams at the joints. Faces are coloured by region (nearest bone plus height rules: coat hem, boot tops, collar, a painted-on apron). The body is skinned with Blender's automatic weights (`parent_set ARMATURE_AUTO`). The sculpted heads (flattened face, tapered jaw, brow), noses, brows, eyes, ears, mitten gloves with cuffs, boots with shafts, hats and kit are rigid parts weighted 100% to one bone and joined into the same mesh. Same skeleton (`CREW_BONES`), same clips (idle, walk, run, crouch, swim, climb; merchant idle, wave), 2,900–3,200 tris each. Watch out: `Mesh.materials.clear()` resets every face's material index in Blender 5, so slots are added before indices are set.
- **Hull stalker (`scripts/mon_hull_stalker.py`).** A gaunt 2.35 m figure replacing the bulky, bumpy one: sunken belly, ribs hugging the skin (placed by ray casts from the spine axis against a BVH of the body), very long arms, four long clawed fingers, clawed toes, and a long skull with deep dark sockets and pinprick glows (no nose). New **`Jaw`** bone (child of Head) carries the lower jaw with its own teeth. Clips: idle, walk, run, attack, grab (timings unchanged) plus **`scream`** (1.3 s, `screamSeconds`: rears back, arms thrown wide, jaw dropped to about 60°, body shuddering). 4,564 tris (over the 2,000 budget: there is one, two from cycle 4).
- **Drowned hunter (`scripts/mon_drowned_hunter.py`).** The body is now one lofted, subdivided tube (`charbody.loft_mesh`: elliptical rings along +Y; the Skin modifier squared off a body this fat) with a sagging belly, an underslung jaw packed with needle teeth, cloudy dead eyes, a glowing lure, and torn fins. Same bones. New clip **`scream`** (jaw gaping, head thrashing). 2,596 tris.
- **Items (`scripts/items_extra.py`, `items.blend`).** `tool_battery` (id 16, a chunky 6 V lantern battery, 196 tris) and `item_key` (id 17, a brass key on a red tag, 172) replace the last code primitives; `ModelLibrary.ItemModels` now covers ids 0–17. `items.blend`'s palette images were re-pointed to `Resources/SinkScrapt/Models/Items`.
- **Debris (`scripts/props_horror.py`, `Models/Props/prop_debris_*`).** A tipped chair, a bucket, a pair of abandoned sea boots, loose papers, broken planks and a life jacket (48–212 tris). Bottom-centre pivot, no colliders. `WorldFactory.Dress` scatters them in room corners and against corridor walls, outside the 1.4 m navigation band.
- **Item icons (`scripts/icons.py`).** Every item (ids 0–18; 18 is the supply crate) is rendered with EEVEE from its game model (orthographic three-quarter view, key/fill/rim suns, transparent film), outlined, given a soft drop shadow and box-filtered from 256 to 128 px into `Resources/SinkScrapt/Icons/item_<id>.png`. `Editor/MediaImportRules.cs` imports them as sprites. Re-run the script after changing an item model.

## Wiring (2026-10-15)

Every builder places a model through `WorldFactory.Model(...)`, which returns false when the FBX is missing so the old primitive is built instead. Blocks that carry gameplay colliders stay in place with their renderers turned off (`WorldFactory.Hide`). Random draws are the same with or without models, so peers furnish rooms identically. `Editor/ModelImportRules.cs` sets the import rules for everything under `Resources/SinkScrapt/Models`:
- Meshes are readable, because the interior is static-batched at runtime.
- No colliders, animation, cameras or lights are imported.
- The palettes are point-filtered, uncompressed and have no mipmaps.

- **Interior (`WorldInterior`):**
  - Door and wide-opening frames: one per opening, from one side, stretched over both 10 cm skins when two cells share the wall.
  - Frame ribs, deck beams (turned 90° for x-runs), cable trays, pipes (`d10`/`d18` scaled to the code's diameter and length, flange at the centre) and portholes.
  - Ceiling lamps: the broken variant for dead lamps; the glass takes the warm or cold lamp colour.
  - Extinguisher, valve wheel (with the code's vertical pipe), fuse box (status LEDs moved to its door face).
  - Stair flight and airlock flight (the step blocks stay as colliders) and the exit hatch (the interactable box stays as the collider).
- **Furniture (`Furnish`):**
  - Every piece listed in batch 3. The galley's corner spot now gets `furn_stove` instead of a barrel; it still consumes the same random draw.
  - `furn_pump_unit` is turned 180° so its pump and motor sit over the code's pump and motor colliders. The riser pipe is scaled 1.25 for the 0.3 m corner riser.
  - Engine and gauge-panel LEDs moved onto their front faces.
- **Wreck (`Exterior`):**
  - Containers (tinted per container), bridge house, both lifeboats (the starboard one mirrored with scale x −1), mast, funnel, forecastle cabin, windlass, bollards, crane pedestals and slewing houses, and floodlight poles.
  - All white railings (`Railing`) are tiled from `ext_rail_section` (2 m, stretched to fit) plus one `ext_rail_post`.
- **Island (`Island`):** boulders (variant `i % 5`, same rotation, uniform scale `s`), shrubs (yaw only), the dock (turned 180° so its piles match the code piles), workshop, fuel tanks, lighthouse and power poles.
- **Doors (`InteriorDoor.cs`, since 2026-10-16):** every room doorway is 1.1 m wide with one `kit_door_*` leaf hung on one jamb (the side is a seeded roll). It swings 90° into the room and then stands across the wall 0.55 m off the door centre and about 1.07 m deep. That leaves the 0.42 m-radius monster about 0.1 m on the navigation line through the door centre, clears the corner furniture (from 0.95 m off the centre) and clears the loot (at least 1.45 m from any wall). Locked doors add emissive lock lamps above the handle, a padlocked hasp and the TERKUNCI label in code; door sounds follow the style (wood or steel).
- **Not used:** the island's containers and drums and the boat's tyres stay primitives. The battery and key items have no models yet (code primitives in `WorldFactory.ItemVisual`).

## Integration steps for each batch (Unity side)

1. Export the FBX to `Assets/SinkScrapt/Resources/SinkScrapt/Models/<Category>/<asset-id>.fbx`. Import with Read/Write off and no generated colliders.
2. Load it with `ModelLibrary.Spawn("<Category>/<asset-id>", parent, position)`, which returns null when the model is missing. Keep a code fallback to the current primitive in that case, so missing art never breaks a layout.
3. Swap one call site at a time (for example `ItemVisual` for items, `Furnish` for furniture). Do not change collider sizes or positions.
4. Run the Editor tests and the runtime QA (`Tools/StartQa.cs`). The interior wall-push and monster-clipping checks must still pass. Then check it visually with Game-view captures.
5. Record what changed in `Docs/Verification.md`, any third-party sources in `Docs/Assets.md`, and the confirmed export settings in this file.
