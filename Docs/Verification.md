# Alpha Verification Record

This file records observed results, separate from the goals and acceptance criteria in `Docs/PRD.md`. Package presence, compilation, or a passing unit test does not by itself prove a multiplayer, voice, performance, or gameplay requirement.

## Confirmed

- The Unity `6000.6.5f1` Editor connects to the Unity CLI.
- NGO, Multiplayer Services, Authentication, and Vivox are installed. The local Unity Pipeline package was updated to a CLI-compatible release.
- Five gameplay scenes, four NGO network prefabs, generated materials, balance configuration, the retro post pass, the dusk sky, and the particle material are built through the Editor asset builder (`SinkScrapt / Build Alpha Assets`, `Build Retro Look`, `Build Atmosphere`).
- The Unity Cloud project already associated with this project was linked earlier. A private online room was created and joined; the host generated a private code, and the runtime joined the proximity, radio, and spectator Vivox channels. The temporary room was closed. This confirms one online service handshake, not a four-player service test, and it was **not repeated** after the latest changes.

## Cruise ship art and loot (observed 2026-10-16, latest)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 16 GB, Windows 11), Unity `6000.6.5f1`, Blender 5.2.2 via the MCP server. **Editor only, solo** (one listen host, local transport). No Windows build, no clients.

- **Built:** six cruise-only scrap items (ids 19–24) with Blender models and icons, plus eleven cruise furniture models wired into `WorldCruise.cs`. Details are in `Docs/BlenderAssets.md` build list 7 and PRD 6.8.
- **Import:** all 17 FBX files load from `Resources`. Their bounds match the Blender sizes, and the axes match the existing convention. Item icons 19–24 load as sprites.
- **Editor tests: 32/32 passed.** The 31 earlier tests pass. The new `CruiseScrapStaysOnTheCruiseShipAndSaves` test checks five things:
  - Every loot table holds scrap worth 20–150.
  - Cruise ids appear only in the cruise tables.
  - Cruise large loot is two-handed.
  - Machinery rooms use the cargo tables.
  - A deck of the six cruise items saves and loads.
- **Cruise runtime QA (`Tools/StartQaCruise.cs`): passed, 264 checks over 3 cruise expeditions.** Frame rate: 59.8 average / 43.7 1%-low FPS (Game view 502x459). The 1%-low is still below the 45 FPS target.
  - The first run failed at "Deck 1 stands in waist-deep bilge water". QA had walked every room of a large seed on the real clock past 6:20, when the flood overtakes the bilge: 430 s had elapsed, and the water stood at 4.37 m against 2.9 m.
  - This was a QA timing issue, not a game change. `QaRunner` now restarts the expedition clock just before the bilge check. The rerun passed.
- **Loot on a live cruise wreck (first QA expedition, read from the host):** 66 items.
  - 44 cruise scrap: 5 champagne, 10 trays, 6 chips, 8 bells, 6 jewellery boxes, 9 gramophones.
  - 13 industrial scrap from crew and machinery rooms.
  - 6 keys and 3 batteries.
  - During QA the player carried a champagne bottle with its icon in the hand slot.
- **Cargo-ship regression (`Tools/StartQa.cs`): passed, 367 checks over 10 expeditions.** Frame rate: 59.8 average / 49.4 1%-low FPS. No game exceptions were logged during either run; the usual `NetworkVariable is written to…` warning appeared.
- **Visual spot checks:** an offscreen review camera with one point light, on seeds 12345 and 1. Captures are in `Artifacts/CruiseArt/`.
  - Lobby: modelled pillars, the grand piano, the lit hanging chandelier, the fallen chandelier and the sofa groups.
  - Cabin: bed with nightstand.
  - Suite: king bed and single bed.
  - Casino: slot machines with the code's lit screens in their bezels; model chairs round the card tables.
  - Restaurant: clothed tables, chairs upright and toppled.
- **Not verified:**
  - No multiplayer client has seen the new models or items.
  - Nobody has played it by hand.
  - GTX 1650 performance with the extra meshes has not been measured. The furniture adds draw calls over the hidden primitives, and renderer counts were not re-measured.

## Abandoned cruise ship, second level (observed 2026-10-16)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 16 GB, Windows 11), Unity `6000.6.5f1`, **in the Editor only, solo** (one listen host, local transport). No Windows build, no clients, no online services, no Blender (its MCP server did not connect; all cruise geometry is built in code).

- **Editor tests: 31/31 passed** (the 25 existing tests plus `CruiseTests`, 5 tests). New coverage:
  - 1,000 cruise seeds validate with no fallback: 75–90 rooms on five decks, the lobby fixed as room 0 (6 x 7 cells on deck 2), entry A inside it, three open arches, B and C on two different decks among 3–5, 4–7 locked doors, 12 stair shafts, the straight runs QA needs, deck-1 themes (crew and machinery only) and more than 15 passenger cabins or suites. More than 950 distinct corridor networks. Packed blueprints stay under 4,000 bytes (largest seen while measuring 300 seeds: 2,232).
  - 200 seeds: openings are symmetric, the hull is closed except at the three entries, nothing opens into the atrium, and the atrium's 42 cells have no slab.
  - Determinism, pack/unpack round trip, boundary seeds, and the fallback seed generate directly; the same seed on the cargo ship is unchanged and valid.
  - Rejected blueprints: a corridor in the atrium, entry A outside the lobby, both exits on one deck, a locked arch, no atrium, a cargo-only room type, a wrong generator version, a cruise blueprint read as a cargo ship, an arch on the cargo ship.
  - Timeline: depth 0 until 5:00, monotonic to 12:00, water exactly at decks 2–5 at 7:30, 8:40, 10:00 and 10:50, the promenade (y 11) level with the boat deck at 9:45, the funnel under at 12:00, stretching with a longer duration, monotonic interior water.
- **Cruise runtime QA in the Editor (solo, `Tools/StartQaCruise.cs`): passed, 262 checks over 3 cruise expeditions** (`Artifacts/QA-Editor-Cruise/qa-result.json`). Observed:
  - Two hull stalkers spawn; the scripted stalker screams, chases, telegraphs, hits for 30, grabs/lifts (0.90 m) and throws for 22, follows a flare, and stays clear of walls for about 1,250 frames per expedition while hunting through doors. The drowned hunter appears once water reaches deck 2 and hits a swimmer for 35.
  - First expedition: 6 locked doors with one key each; the key-and-door checks pass with a cabin-style door leaf.
  - **The CharacterController walked from a corridor into every one of the 82 rooms on all five decks** (first layout; two rooms per deck on the next two), through the bilge on deck 1. All 12 stair shafts were climbed in each of the three layouts. The hull held at the sampled cells.
  - Entry A is in the lobby on deck 2; B and C were on decks 5 and 4. The physical route to each vestibule and both directions of all three portals work.
  - The rope sags 1.48 m; heavy scrap climbs the rope to the gangway platform and back; scrap dropped on the boat becomes cargo; the boat computer shows the live deck map and shuts a hazard down.
  - At the deck-level milestone the exterior ship sits 10 m down and the interior water is at 10 m (the promenade at 11 m is level with the 1 m boat deck). A full wipe empties the boat; automatic departure keeps cargo; the island sale and checkpoint pass.
  - Frame times (Editor, RTX 5050, Game view about 883x464): **59.6 average / 43.2 1%-low FPS**. This is below the 45 FPS 1%-low target, on a stronger GPU than the target. The cargo regression run on the same machine measured 49.1. The cruise interior has about 11,350 renderers (9,784 static-batched), 60 lights, and 393 draw calls seen from the lobby; the main thread was about 5.5 ms there. The low may come from QA teleports and scene loads rather than steady play; that has not been separated. **GTX 1650 performance on the cruise ship is unverified and is a risk.**
- **Cargo-ship regression (`Tools/StartQa.cs`, after the shared refactor): passed, 363 checks over 10 expeditions** (`Artifacts/QA-Editor/qa-result.json`). The Editor log has no game exceptions; the `NetworkVariable is written to…` warning still appears once per expedition, as before.
- **Visual spot checks** (offscreen review camera with a small light, captures in `Artifacts/Cruise/`):
  - The exterior from the sea: hull, three-tier superstructure, funnel, lifeboats.
  - The gangway platform with the rope down to the boat, and the hull stair to the promenade.
  - The promenade deck with an emergency exit.
  - Inside: the lobby (pillars, fountain, piano, sofa groups, lit chandelier, balustrade of the gallery above) and the atrium seen from the gallery; a passenger corridor with numbered cabin doors; a cabin; a restaurant with laid tables; the casino (card tables, lit slot machines); a suite (king bed, wardrobes); the boutique (glass cases, mannequin); the main-door and emergency-exit vestibules.
  - **Defects found and fixed:**
    - Several walls rolled a "RESEPSIONIS" desk. There is now one, on a fixed wall.
    - The vestibule welcome line and the exit sign text overflowed. Both were resized; the final exit-sign width was compiled but **not re-captured**.
  - **Seen but not changed:** the hull and superstructure paint reads as rust-stained, brownish off-white at midday rather than clean white.
- **Not verified:**
  - Playing the level by hand.
  - Monsters patrolling the cruise ship over a whole 12-minute run.
  - Two or more players on the cruise ship (blueprint and geometry agreement between peers, late joiners, the exterior rebuild when `NetSession.Ship` arrives after the scene loads).
  - A Windows build, the GTX 1650 target, and the online path.
  - The cruise arrival cutscene framing (it was not captured).

## Windows build, two-player local QA with a visible client: feedback batch 2 seen from a client (observed 2026-10-15)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 15.3 GB, Windows 11, 1536x960 desktop at 125% scaling), Unity `6000.6.5f1`. **Two players on one PC over local transport**: a windowed host (1280x720) and one *visible, rendering* client (windowed 1920x1080). No headless clients, no second machine, no online services.

- **Windows x64 build:** `Builds/StandaloneWindows64/SinkScrapt.exe`, five scenes from Build Settings, 154 MB, 0 errors. Warnings: the Pipeline runtime-config notice, stripped debug shaders, and (last build) Unity's "uncompiled code changes" notice, which concerns Editor code only.
- **Editor tests: 25/25 passed** after the changes below.
- **Baseline build QA (unchanged QA code), 2 players: passed, 365 checks** over 10 expeditions (`Artifacts/QA-Build2-Baseline`). Host log: 0 exceptions and the usual 10 `NetworkVariable is written to…` warnings (one per expedition). Client log: no exceptions or warnings.
- **New client-view QA.** The host asks each client what its own screen shows and checks the answer (`NetSession.ProbeView` → `QaRunner.DescribeView`). A client started with `--qa-capture` also saves `client-<tag>.png` next to its `--save-path`. QA moves the client with the existing server teleport so it is looking at the host. Commands:
  - host: `SinkScrapt.exe --qa-host --new-campaign --save-path Artifacts\QA-Build2\campaign.json --qa-players 2`
  - client: `SinkScrapt.exe --local-client --profile p2 --qa-capture --save-path Artifacts\QA-Build2\client\campaign.json -screen-width 1920 -screen-height 1080`
- **Final build QA, 2 players: passed, 383 checks** over 10 expeditions (`Artifacts/QA-Build2/qa-result.json`, captures in `Artifacts/QA-Build2/client/`). Host log: 0 exceptions and the same 10 warnings. Client log: 0 exceptions. Frame times on the host (1280x720 expedition frames): 60.0 average / 59.7 1%-low FPS. These were observed on the client:
  - *Crew characters:* the client shows the host as the parka crew model and itself as the medic (the host builds the client as the medic). No two crew share a body. Capture `client-teammate.png` shows the full parka model on the deck, and `host-teammate.png` shows the medic from the host's side.
  - *Clips driven only by replicated state*, read from the client's `CrewAnimator`: idle while standing; walk at 2 m/s; run at 7 m/s; crouch (`Crouched`); swim (`Motion`=1); climb (`Motion`=2). The walk capture shows a mid-stride pose. QA moves the host directly rather than with the CharacterController, so these clips come from synthetic motion, not from a person playing.
  - *Computer order countdown on the client HUD:* right after the order the client's timer read 29.8 s against the host's 29.7 s, and the HUD line showed `PAKET 30 DTK`. Two seconds later both read 27.7 s, which confirms the client-side countdown in `GameHUD`. At the drop the client shows the package descending (1 falling item) and the order gone from its HUD.
  - *Wreck arrival cutscene:* plays on the client and ends there. The letterbox and wreck establishing shot are captured in `client-arrival.png`.
  - *Island arrival cutscene:* plays on the client. `client-island-arrival.png` shows the letterbox and the island shot.
  - *Stalker grab and throw seen by the teammate:* the client stands one cell behind the host. It sees the stalker in `Grab` playing its `grab` clip with the host lifted 0.71 m. After the throw it sees `Recover`. The watching client was never attacked. `client-grab.png` shows the pale stalker holding the diver up in a dark deck-2 corridor, with the host's torch pool on the floor; `client-thrown.png` was captured just after the throw.
  - *Belt torch on a teammate:* the host's torch light and beam object are active on the client, both on the boat and in the corridor. In the daylight boat capture (`client-torch-beam.png`) the shaft is not visible, which is expected because it fades to a quarter in daylight. In the dark corridor the lit pool is visible, but the shaft itself is not clearly distinguishable in a still frame.
  - *1920x1080 HUD capture* from the client: `client-hud-order.png`.
- **Defect found and fixed:** at 1920x1080 with a package in flight, the campaign line `SIKLUS 1 • HARI ○○○ • PAKET 30 DTK` wrapped, and `DTK` spilled below the wallet panel's frame. The line is now a single line that shrinks to fit (`GameHUD`, 10–15 pt; text unchanged). The final run's capture shows it on one line inside the panel.
- **QA-only issues found while adding the checks (no game change):**
  - A view RPC sent in the same frame as a NetworkVariable change can reach the client first. The island-cutscene check first failed because the client still showed `BERLAYAR`. The checks now re-ask for up to 3 s (`QaRunner.ViewUntil`).
  - The stalker checks started inside the 7 s wreck cutscene, so the first grab capture showed the cutscene camera blending toward the client's interior eyes over empty sea. QA now waits for the cutscene to end on the first expedition.
- **Seen in captures but not changed:**
  - During the island cutscene, a fading `30 DETIK!` alert and the "Mesin menyala. Menuju pulau..." toast draw over the shot. QA jumps to the end of the run just before sailing, so the alert is still fading. In normal play the alert fires about 30 s before departure, but the toast over a cutscene could still happen.
  - On the client, the host's body briefly plays a walk step after a QA teleport of less than 4 m (`CrewAnimator` takes the jump as speed).
- **Not verified:**
  - The parachute canopy as seen from the client: both captures missed the package (it was high above the view, and later the cabin blocked it). Only the replicated state (`Descending`) was checked.
  - More than two players with rendering clients; separate machines; latency or packet loss; the online path (Relay/Vivox).
  - A human playing either window. All motion and actions were driven by QA.
  - The GTX 1650 target.

## Feedback batch 2: rigged characters, computer shop and parachute drops, island-only cycle end, boat lost on a wipe, HUD rework, belt slot, torch beam, cutscenes, z-fighting (observed 2026-10-15)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 16 GB), Unity `6000.6.5f1`, Blender 5.2.2, in the Editor only, solo.

- **Editor tests: 25/25 passed.** New or changed:
  - A computer order is paid from the wallet, rejects too little money, an over-full package (more than 4 tools), non-shop ids and an empty list, and becomes a `Paket pasokan` holding its tools.
  - The belt slot takes a torch or radio, keeps the belt torch active while a two-handed item is in hand, refuses to move into a hand that holds a two-handed item, and an empty belt survives JSON as empty.
  - Ending an expedition keeps boat cargo; `LoseBoatCargo` clears the deck and the rack but leaves paid orders in flight.
- **Runtime QA in the Editor (solo): passed, 354 checks over 10 expeditions** (`Artifacts/QA-Editor/qa-result.json`). New coverage:
  - A new campaign starts at sea (no island). The helm refuses the island before the third expedition. Every crew member gets a different character.
  - Computer order `12,13,14` takes the $120 wallet to $0; a further order is refused; a 5-item package is refused. The package lands under its parachute on the boat deck, opens into three tools, the torch goes onto the belt and the radio/tank into hand slots; [5] swaps belt and hand both ways.
  - The belt torch switches on (F), drains, refills from a battery, and stays lit when the hand slot changes and while carrying two-handed scrap.
  - Hull stalker grab: it lunges, seizes the diver, lifts them 0.6 m+ off the floor, throws them, and the throw costs `grabDamage` (22). Strike damage checks run with the grab forced off.
  - A full-team wipe with deck cargo and a racked tool aboard empties the boat's cargo; the items leave the deck and the following checkpoint holds no cargo.
  - After forcing three expeditions, the helm sails to the island, the arrival cutscene flag is set, and the island sale and checkpoint checks pass.
- **Import:** the 7 rigged characters import as Legacy with their clips (stalker idle/walk/run/attack/grab; hunter idle/swim/chase/bite; crew idle/walk/run/crouch/swim/climb; merchant idle/wave) at the authored lengths after setting the Blender scene to 30 fps. They import facing −Z without Apply Transform; `ModelLibrary` turns `Characters/*` by 180°.
- **Visual spot checks** (Game view 630x633 plus offscreen camera renders): HUD watch, $ wallet, metal-framed vitals, belt slot with battery cells; the computer TOKO tab; a parachute package in the air and landed; opening it; the belt torch and its visible beam; the wreck arrival cutscene (letterbox, title, ends in the player's view); stalker and anglerfish models in the interior; the parka crew model walking; the island with the diver merchant. The beam was softened (nested shells) and made fainter in daylight after these captures; that final look was compiled but **not re-captured**.
- **Z-fighting:** a triangle-level scan (coplanar, overlapping faces of different materials) found flush trim in container corner castings, funnel bands, the boat cabin door jambs, windlass, kit door leaves, lighthouse band, workshop sign/roof/wall seams. Those pieces were pushed 6 mm proud in Blender and re-exported. A re-scan of the boat and island shows only the boat's stern transom under the deck and the dock's far end; the wreck and interior were not re-scanned after the fix.
- Editor frame timing during the QA (502x459): 59.8 average / 49.5 1%-low FPS.
- **Not verified:**
  - No multiplayer run: other players' character models, CrewAnimator clips, the grab as seen by teammates, client-side order countdowns and the cutscene on clients are untested.
  - The HUD was only seen at roughly square Game-view sizes; a 16:9 1080p capture was not taken.
  - No human playtest of the new loop (3 days at sea, island only at the end), 30 s deliveries, grab frequency (35%), or how the wipe rule feels.
  - No Windows build.

## Playtest feedback batch: 10-minute run, generator v3, doors and keys, torch battery, day/night, CRT computer (observed 2026-10-15)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 16 GB), Unity `6000.6.5f1`, in the Editor only, solo.

- **Editor tests: 24/24 passed.** Updated or new:
  - 1,000 generator-v3 seeds validate with no fallback. Every packed blueprint is under 4,000 bytes, entry A is on the top deck, and each layout has 1–4 locked doors.
  - The hull is sealed except the three entry cells. B and C never share a deck.
  - Invalid blueprints are rejected: an entry off the corridor, entry A below the top deck, only two entries, a locked door that is not a dead end, and generator v2.
  - The 10-minute timeline holds steady to 4:00, passes every milestone, never reverses, and stretches with a longer duration. `BalanceConfig.duration` defaults to 600.
  - Pack/unpack of the blueprint is lossless.
  - A battery refills only a non-full torch and is used up. Keys are never Safe Cargo and cannot be bought. A charge above 1 fails validation.
  - A version-2 save migrates with charged flashlights in both cargo and loadouts.
- **Runtime QA in the Editor (solo): passed, 335 checks over 10 expeditions.** New coverage, on the first layout unless noted:
  - A locked door refuses a diver without a key and closes the route into its room. A key is picked up, opens the door and is used up, and the route opens. Both leaves rest open inside the room. A plain door opens and closes. One key spawned per locked door.
  - Physical walks to all three entries. Entry A uses the airlock flight; a lower-deck entry uses the hatch vestibule. All three portals go in and out.
  - A swimmer pulls up onto a ledge 0.6 m above the water and ends standing on it (0.64 m). A 1.2 m ledge is refused.
  - The boarding rope hangs below its chord (0.50 m sag at that moment).
  - The torch drains to empty and switches off. A dead torch without a battery stays dark. A carried battery refills it and is used up.
  - The expedition clock ran from night into day (1.8 h to 7.6 h).
  - The boat computer showed its live deck map.
  - On every layout: all doors were opened for the traversal walk (31–34 per layout). The stalker hunted through doors without its body touching any collider for about 1,260 frames per layout. All earlier checks (rooms on three decks, stairs, sealed hull, bilge, monsters, cargo, wipe, sale, checkpoint) still pass.
  - QA moves the clock to the 7:00 and 9:00 milestones and to the end of the run through `ShipTimeline` instead of fixed seconds.
- **Defects found by QA and fixed during this work:**
  - The stalker could walk through an open door leaf, because its wall sweep ignored every unlocked door. Only a closed unlocked door is now passable to it (it opens that door).
  - A 1.6 m single leaf opened to within 0.4 m of the room-cell centre, which bodies pass through; three random layouts showed overlaps. Doors are now double doors: two 0.8 m leaves that reach 0.8 m into the room.
  - The mantle animation was paused by the dialog QA keeps open. QA now closes it around that check; this was not a game bug.
- **Visual spot checks** (Editor Game view at about 502x459 with the retro filter, plus one offscreen 1920x1080 render of the HUD):
  - *Boat model, rendered in Blender and Unity:* the bow now has a foredeck and an inner bulwark. The stray black bar above the computer monitors is gone.
  - *Interior darkness:* a corridor without a torch is near black. A first capture was still grey because the moon (a directional light) and skybox reflections reached the interior; both are now off while the local diver is inside. With the torch, a lit pool shows ahead.
  - *Torch beam:* the beam originally started about 1 m ahead of the eye and blew out a nearby wall. It now starts at the hand, and held items cast no shadow.
  - *Boat computer:* CRT bezel, scanlines and phosphor text; the map centred on the followed diver; crew panel, water, and hazard rows. The map was zoomed out so a whole deck fits (54 px cells), and a wrapping legend was shortened. The final zoom was not re-captured.
  - *Exterior:* the rope at night was invisible at first; it is now 8.5 cm wide with a faint glow, and reads as a curved hemp rope. Hatch C and its sign are on deck.
  - *Doors:* a locked double door shows red lamps and the "Pintu terkunci / Butuh kunci" prompt; open, both leaves stand in the room with green lamps. The TERKUNCI label overlapped the room sign and was moved onto the leaves; that last move was compiled but not re-captured.
- Editor frame timing during the QA (502x459): 59.8 average / 53.6 1%-low FPS.
- **Console:** no errors at runtime. The usual 10 `NetworkVariable is written to…` warnings appear, one per expedition (pre-existing, from `SpawnEnemy`).
- **Not verified:**
  - Interior z-fighting: the cargo deck plate was coplanar with the hull's top face and now sits 3 cm above it, and interior wall skins no longer overlap their neighbours by 2 cm. Both were found by reading the geometry. Neither was confirmed in motion; a still capture cannot show flicker.
  - Shallow-water swimming without jitter (hysteresis and buoyancy) and the forgiving pickup aim: covered only indirectly (the bilge-wading and pickup checks pass). Nobody has played them.
  - No Windows build, no multiplayer run, no online run. Clients' view of doors, the clock and the computer map is untested.
  - No human playtest of darkness, battery length (150 s), door pacing, the 10-minute timing, or day/night readability.

## Interior kit, furniture, wreck and island models wired in (observed 2026-10-15)

Same machine and Unity `6000.6.5f1`, Editor only.

- **Import:** 88 models and 2 palette textures reimported under the new `ModelImportRules` postprocessor, with readable meshes and no colliders or animation. The console showed no static-batching or import errors.
- **Editor tests: 21/21 passed.**
- **Runtime QA in the Editor (solo): passed, 299 checks over 10 expeditions.** The count varies with how many rooms each random layout has.
  - All interior walks, stair and airlock climbs, portals, the sealed-hull wall pushes and stalker wall-clipping checks pass with the models in place. That confirms the hidden collider blocks still define the walkable space.
  - All boat, cargo, rack, hazard and wipe checks pass.
- **One QA defect found and fixed** (not a gameplay change). On the first run "Tool taken back from the rack" failed: the QA picked "the first loose item", which can be a two-handed item or the heavy scrap lying on the boat deck, and the server correctly refuses a pickup while hands are full. The QA now picks one-handed loot inside the wreck.
- **Console:** 0 errors. The same 10 pre-existing `NetworkVariable is written to…` warnings from `SpawnEnemy`, one per expedition.
- **Visual spot checks** (502x459 Game view with the retro filter, so detail is limited):
  - The island: dock, double bollards, workshop and lighthouse.
  - The wreck: bridge house with lit windows, door A, the PALKA A sign, mast and funnel.
  - An interior corridor: deck beams, ribs and overhead pipes.
  - The engine room: the engine-block model under torchlight.
  - The torch model's lens was checked to point forward (+Z).
  - These are developer spot checks, not a playtest. The lit windows on the bridge house render as near-white blocks, as the old window boxes did.
- Editor frame timing during the QA (502x459): 59.8 average / 51.5 1%-low FPS (previously 59.9 / 52.1).
- **Windows x64 build:** rebuilt from the Editor to `Builds/StandaloneWindows64/SinkScrapt.exe`: 145 MB, 0 errors, 3 warnings (the Pipeline runtime-config notice and two stripped debug shaders, as in earlier builds). The build includes the new boat, all Blender models and the boat computer.
- **Build QA, four players: passed, 311 checks over 10 expeditions.**
  - Setup: one windowed host (1280x720) plus three headless clients (`-batchmode -nographics`) on local transport, with a fresh campaign.
  - Every peer loaded the small boat and island scenes and reported the host's blueprint and the same interior node count on all 10 expeditions.
  - All the new boat checks ran on the host in the 4-player session and passed: drop-on-deck Safe Cargo, deck pickup, the tool rack, hazard shut-off and cooldown, the wipe keeping deck cargo, and sold scrap leaving the deck.
  - No exceptions in the host log or any of the three client logs. The host log has the usual 10 `NetworkVariable is written to…` warnings (one per expedition, pre-existing); the clients have none.
- Build frame times (expedition frames, host window 1280x720): 60.0 average / 59.8 1%-low FPS on this machine. This says nothing about the GTX 1650 target.
- **Limits:**
  - The build QA checks the host's authoritative state and the peers' blueprints. It does **not** check what clients see of the new features: deck cargo positions, rack items, the gate swing, the boat-computer timers and map on a client screen.
  - The headless clients do not quit on their own and were closed after the host exited.
- **Not done:** online (Relay/Vivox) play, the GTX 1650 target, and a human playtest of the new boat, the computer UI and the new art.

## New workboat, drop-on-boat Safe Cargo, tool rack, boat computer, item models (observed 2026-10-15, later)

Same machine (RTX 5050 Laptop GPU, Ryzen 7 250, 16 GB) and Unity `6000.6.5f1`, in the Editor only.

- **Compile:** clean (no errors). The first import of the Blender FBX files from `Resources/SinkScrapt/Models` confirmed the axis mapping. Blender −Y imports as Unity +Z, and Blender +X as Unity −X. The importer adds no root rotation or scale. The new boat's rescue-ladder rungs land at Unity x 30, z 1 and the gate leaf at z −6..−4, as designed.
- **Editor tests: 21/21 passed.** New tests:
  - deck cargo is stored once, and selling stays idempotent;
  - deck cargo can be taken only once;
  - the tool rack takes tools only, one per slot, within six slots, and every slot is aboard;
  - drops and stow positions stay inside the rail;
  - a version-1 save migrates its chest cargo onto the deck.
- **Runtime QA in the Editor (solo): passed, 300 checks over 10 expeditions.** New or changed coverage:
  - Heavy scrap carried back along the rope and dropped on the boat becomes Safe Cargo.
  - Each expedition's scrap dropped on the deck becomes Safe Cargo at an on-deck position. Picking it up takes it out of Safe Cargo, and a second pickup cannot duplicate it.
  - Scrap is refused by the tool rack. A torch goes onto a rack slot and comes back off it.
  - The boat computer shuts hazard 0 down, and an immediate second request is refused by the cooldown (the layout had 3 hazards).
  - A full-team wipe keeps every secured item in the campaign and its object on the deck.
  - Automatic departure keeps the cargo.
  - The island sale removes sold scrap from the deck and cannot be repeated.
  - The saved checkpoint keeps deck positions and rack slots.
  - All previous checks still pass: interior walks, stairs, airlocks, portals, sealed hull, monster clipping, bilge.
- **Console:** 0 errors. 10 `NetworkVariable is written to…` warnings from `NetSession.SpawnEnemy`, one per expedition: the same pre-existing warning as before.
- **Visual spot checks** (Game-view captures at 502x459 with the retro filter, so detail is limited):
  - The boat from the cargo deck: cabin, mast and radar, the closed port rail, and the dock beside the open starboard gate.
  - The cabin interior with the computer desk, three teal monitors and the tower.
  - The boat-computer screen in a live expedition: three-deck map, flood overlay on decks 1–2, three hazard markers and rows with MATIKAN buttons, and the water and crew panel.
  - Two problems seen and fixed: the in-world KEMUDI/KOMPUTER labels were oversized, and the monitor emission washed out to white. After the fix only the Editor tests were re-run; the full QA was not.
- Editor frame timing during the QA (502x459): 59.9 average / 52.1 1%-low FPS.
- **Not done:** a Windows build, a multiplayer run (the boat computer's hazard timers, deck cargo and the gate on clients), online play, and any human playtest of the new boat, the rail and gate, or the computer UI. The kit, furniture, wreck and island models are exported but not yet used by the world builders.

## Interior generator v2, monster clipping, bilge water, transparent water, art pass (observed 2026-10-15)

Same machine and Unity `6000.6.5f1` as below (not the PRD target hardware).

- **Editor tests: 18/18 passed.** The generator tests were rewritten for generator v2: 1,000 seeds validate with no fallback (rooms, doors, stairs, airlocks, reachability, per-floor loop of 8+ cells, blueprint JSON under 4,000 bytes; largest seen 2,833), more than 900 distinct corridor networks in those 1,000 seeds, the fallback seed generates directly, and invalid blueprints are rejected (overlap, no doors, missing stair, airlock off the corridor, old generator version). New tests check that openings are symmetric, that no opening leads into a void, and that the hull edge is closed everywhere except the two airlocks (200 seeds), and that the bilge level and interior water never go backward.
- **Runtime QA in the Editor (solo): passed, 290 checks over 10 random layouts** (generation itself takes about 0.6 ms per layout: 1,000 seeds in 0.6 s). New or changed coverage:
  - CharacterController walks from a corridor into **every** room on all three decks on the first layout and into two rooms per deck on the others; climbs **every** stair shaft (4 per layout); climbs both airlock flights; uses both portals.
  - **Sealed hull:** the diver is pushed into all four walls from 10 (first layout) or 4 sample cells. They never ended up in a void cell, outside the hull, or below their deck.
  - **Bilge:** on deck 1 the interior water stands at 2.9 m (floor + 0.9 m) at the start, and the player is wading (not swimming, not underwater).
  - **Monster clipping:** on every layout the stalker hunts a player three times from the cell farthest from a room (so its route has to turn through doors) for 7 s each. Every frame, its body (0.25 m spheres at 0.6 m and 1.5 m) overlapped no level collider and stayed inside a used cell: about 1,260 frames per layout, 12,600 in total. The earlier sight, chase, hit, flare and drowned-hunter checks still pass on the new layouts.
- **Defect found during this work and fixed:** stair geometry and stair nav nodes were built 2–3 m too high (the deck height was added twice). It was found by looking at the shaft in the Editor. The QA stair walk would also have failed on it.
- **Visual review:** Editor Game-view captures of the boat, island, wreck exterior, an interior corridor, deck 1 in bilge water, a cargo hold and a stair shaft. Transparent water shows the boat hull, the island's sand shelf and the deck-1 floor through it. These are spot checks by a developer, not a playtest. The darker interior (ambient 0.1, denser fog, about 22% dead lamps) has **not** been judged by a human for readability.
- Editor frame timing during the QA (502x459 Game view, so not comparable to 1080p): 59.8 average / 50.8 1%-low FPS.
- **Windows x64 build:** rebuilt to `Builds/StandaloneWindows64/SinkScrapt.exe` (0 errors, 3 warnings).
- **Build QA, four players: passed, 300 checks.** One windowed host (1280x720) plus three headless clients on local transport. On all 10 expeditions, every peer reported the host's blueprint and the same interior node count. Host and client logs contain no exceptions. The host log has 10 `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet` warnings, one per expedition. The previous four-player run had the same 10, so this change did not introduce them (likely `IsWater` set before the enemy spawns).
- Build frame times (expedition frames, `targetFrameRate=60`, retro filter on, 0.55 render scale): 60.0 average / 59.8 1%-low FPS at 1280x720 on this machine. This says nothing about the GTX 1650 target.
- Not done for this change: a solo build QA run, online (Relay/Vivox) play, and any human playtest of the new layouts, the darker lighting or wading.

## Earlier results (observed 2026-10-14)

Machine: AMD Ryzen 7 250 / NVIDIA GeForce RTX 5050 Laptop GPU (plus Radeon 780M iGPU) / 15.3 GB RAM / Windows 11. Unity `6000.6.5f1`. This is **not** the PRD target hardware.

- **Editor tests: 16/16 passed.** The 15 earlier tests (layout generation and determinism, 1,000 seeds, invalid layouts, timeline milestones, purchases, inventory, idempotent deposit and sale, protected cargo on a wipe, quota, oxygen refill, backup recovery) plus a new test that every synthesised sound (all variants) is non-empty, finite, not clipping, and that loops do not click at the seam.
- **Runtime QA in the Editor (solo): passed, 165 checks over 10 random layouts**, then again as part of the build runs below. It now also covers monsters (see "Monster checks").
- **Windows x64 build:** `Builds/StandaloneWindows64/SinkScrapt.exe` (141 MB, Mono, 0 build errors; only Unity-internal shader-stripping notices and a Pipeline-package notice).
- **Build QA, four players: passed, 176 checks.** One windowed host (1280x720) plus three headless (`-batchmode -nographics`) clients, all on local transport (loopback UDP 7777). Every client loaded the SmallBoat and Island scenes, built the host's interior blueprint and geometry identically on all 10 expeditions, and answered the host's probes. Host and client logs contain no exceptions.
- **Build QA, solo (`--qa-players 1`): passed, 165 checks.**
- **Frame times from those build runs** (expedition frames only; the loop runs with `targetFrameRate=60`): 60.0 average / 59.7 1%-low FPS (four-player host) and 59.9 / 59.7 (solo), 1280x720, 0.55 render scale, retro filter on. This only shows the build holds its 60 FPS cap on this much stronger machine. It says nothing about the GTX 1650 target.

### What the runtime QA covers now

Purchases and rejection of an unaffordable purchase; layout validity of the live blueprint; physical traversal of corridors on all three decks for ten layouts; both physical stair routes, both airlock stairs, both portals in and out; heavy-scrap rope climb both ways; pickup, deposit, withdrawal (no duplication); the minute-12 deck alignment with the interior flood line; full-team wipe with stored cargo preserved; automatic extraction; cargo sale at the island and an idempotent repeat; checkpoint round trip.

**Monster checks (new):** the hull stalker spawns with the expedition and the drowned hunter does not appear early; the stalker notices a player in its corridor, gives chase, telegraphs (wind-up) before it hits, and the hit costs exactly the configured damage; chasing raises the threat cue; a flare pulls the stalker away from a visible player without harming them; the drowned hunter appears once the water reaches the middle deck, starts inside the flooded deck, and hits a submerged swimmer for the configured damage. The chase-and-hit check runs on **every** expedition (10 random layouts); the flare and hunter checks run on the first.

### Defects found and fixed in this session (kept for traceability)

- **Stair traversal gate (was open):** the deck-3 stair and airlock slab sat directly above the deck-2 stair and landing, leaving about 1.8 m of headroom. The airlock flight and airlock now sit at `WorldZone.AirlockX` (x=-3.5). The inner bulkheads also ran into the end cross-passages and blocked the corridor-to-corridor end links; they now stop at z=+-18.5.
- **Mirrored 3D signs** on the island, boat, and hatches (TextMeshPro text reads from -Z).
- **HUD** text pinned to the top-left of a 1920x1080 reference clipped on narrower windows; the canvas now scales by width and edge-pins its elements.
- **Monster stood still** when it and its target snapped to the same graph node; it now closes in directly.
- **Monster dithered** in layouts where the middle cross-passage node linked to only one rail node: the "nearest node" start flipped on every repath. The graph now links both neighbours, and the monster compares several start nodes by total route length. Found because the chase check failed on one random layout; it now runs on all ten.
- **Join race:** a client connecting while the host was loading scenes got NGO's "Server Scene Handle already exist" exception. The host now refuses joins while loading with a clear Indonesian reason, shown to the joiner; the headless test clients retry.
- **NGO warning** "NetworkVariable ... before the NetworkObject is spawned" (once per loot item) is gone: the item data is published from `OnNetworkSpawn`.

### Visual and audio review (limits)

Reviewed with the Editor's Game-view capture and, for the menu, a screenshot of the running Windows player: PS1-style filter (low-res point upscaling, dither, banding, grain, vignette), dusk sky and water, harbor and ship lighting, interior corridor with flashlight, monsters, all HUD elements, the menu, shop, sale, settings and first-run guide dialogs. These are single-frame spot checks of a developer; **no one has listened to the sound design or played the game by hand yet**. The audio tests prove the clips are valid, not that they sound good or are mixed well, and monster difficulty and pacing are untested by humans. The UI was reviewed at roughly 16:9 and a narrower Game-view window; ultrawide was not.

## Still required

- Measure 1080p Medium average FPS and 1% low on the stated GTX 1650 / i5-8400 / 16 GB class with four players, the largest interior, active monsters, and flood effects. Nothing here can stand in for it. If it misses, the first levers are the retro render scale (`RetroLook.Level`), shadow distance, and the number of point lights (about 40 inside the wreck).
- Run the four-player scenario across separate machines, with the 150 ms round-trip / 2% packet-loss simulation (`Tools/udp-lag.py`). Late join, reconnect, and host-disconnect behaviour on the build, and the new join refusal during loading, still need hands-on validation.
- Verify proximity and radio voice across zones and underwater with real clients, microphones, and the online services (Relay, Vivox). The online path was not re-run after the latest changes.
- Validate interrupted-save recovery, corrupt-main-save backup behaviour, and the both-invalid case against the current campaign format in a build. Editor tests cover backup recovery and the build QA covers the checkpoint round trip, but no corrupted file was fed to the build.
- Human playtest: sound mix, monster tuning, and onboarding clarity. Also: whether the darker interior is readable without and with a torch, whether waist-deep wading on deck 1 feels right (`wadeSpeedFactor`), and whether generator v2 layouts are fun to navigate.
- Performance on target hardware now also has to cover the generator v2 interior (more renderers, statically batched at runtime) and the transparent depth-sampling water.

## Performance target

At 1080p on Medium, the PRD target is an average of at least 60 FPS and a 1% low of at least 45 FPS on a GTX 1650, i5-8400-class CPU, and 16 GB RAM. Test with four players, the largest interior, active monsters, and flood effects. No target-hardware result is recorded yet.
