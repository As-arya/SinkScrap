# SinkScrapt — Alpha Product Requirements and Development Plan

**Document purpose:** This is the product and implementation source of truth for the SinkScrapt alpha. Read it before making gameplay, architecture, content, balance, or networking changes. The project is inspired by the cooperative salvage tension of *Lethal Company*, while using its own setting, systems, art, characters, and interface.

**Product status:** The Unity project already contains an in-progress implementation. This document describes the intended alpha, not a claim that every feature or acceptance test is complete. Use `Docs/Verification.md` for evidence about the current build and `README.md` for how to open and play the current project.

## 1. Product vision

SinkScrapt is a first-person cooperative horror salvage game for one to four players on Windows PC. A crew uses a small workboat as its lobby, supply depot, and cargo hold. The crew travels to a derelict cargo ship, boards it, searches a changing interior for valuable scrap, and carries that scrap back to the workboat. The ship steadily sinks throughout the expedition. Tools are ordered on the boat computer and dropped onto the boat by parachute. After three expeditions ("days") the crew sails to a nearby island, sells its cargo to an old diver merchant, and uses the proceeds to meet an escalating campaign quota.

> **Product-owner changes, 2026-10-15.** These replace the earlier rules where they conflict: (1) a full-crew wipe now loses everything on the boat; (2) the island is only visited at the end of a three-expedition cycle; (3) tools are bought on the boat computer at any time and arrive by parachute; (4) the HUD shows a 24-hour watch, dollars, a vertical oxygen gauge only while air changes, and a belt slot; the sinking progress is on the boat computer only; (5) the torch is switched with F and stays lit whatever is in hand; (6) four rigged crew characters, a diver merchant, an anglerfish water monster and a humanoid stalker that can grab and throw; (7) arrival cutscenes.

> **Product-owner changes, 2026-10-16.** (1) Room doors are single hinged doors in 1.1 m doorways (no more double leaves), in four looks mixed per door: grey watertight with a porthole, varnished planks with a porthole, rusted heavy steel, painted cabin door with a small window. (2) The torch draws no visible light shaft; it lights the space itself (a bright hotspot inside a wide dim spill, plus a bounce light). (3) A monster screams when it first sights a diver, then chases. (4) The soundscape is horror rather than retro: recorded CC0 sounds are allowed (sources in `Docs/Assets.md`): footsteps per floor material (wood, iron, grating, concrete, gravel, wading), sea, birds by day and wind outside, a near-silent wreck interior with hull creaks, groans and drips, and no music inside. (5) The wreck is dressed for dread with light gore: blood smears, handprints, drag marks, claw gouges, rust, painted Indonesian warnings, debris, hanging cables; no corpses. (6) Crew, merchant and monsters are continuous skinned low-poly models; the hull stalker is gaunt rather than bulky. (7) Inventory slots, the belt slot, the computer shop and the sale list show item pictures.

Every expedition asks the crew to weigh value against time and danger: search another room, descend into water for better loot, help a teammate, or return to the boat while the route is still usable. The sinking ship is both a deadline and a changing level. At the end of a long run, the derelict should sit low beside the workboat; by the end of the expedition it should disappear beneath the surface.

The visual direction is gloomy stylized industrial horror: corroded steel, worn paint, low practical light, a dark open sea, fog and water, readable silhouettes, and increasingly flooded ship interiors. The small workboat and island form a familiar social and economic hub between excursions. The fiction should stay clear and economical; no extra story, lore, or progression system should dilute the salvage loop.

## 2. Design pillars

1. **Risk is chosen by the crew.** Valuable routes and deeper rooms should demand time, coordination, equipment, or exposure to water and monsters. Players should understand the risk before choosing to continue.
2. **The sinking is visible and spatial.** Players should feel that the same ship is descending. The exterior, interior water, access routes, rope, warning audio, and timer must agree on one synchronized sinking progression.
3. **Teamwork creates practical tradeoffs.** Four shared inventory slots, heavy two-handed objects, oxygen, and tools should make players coordinate what to carry and who returns to the boat.
4. **Someone has to make it home.** Cargo put down on the small boat is kept as long as at least one crew member is alive and aboard when the boat departs. If every player dies or is left behind, the boat drifts home empty: everything on its deck and tool rack is lost, along with unsecured objects on the wreck and the carried inventories of dead players.
5. **Repeat visits stay legible but varied.** Procedural room layouts and placements change routes, loot, and threats; every generated layout still has guaranteed valid access, connected rooms, and usable escape routes.
6. **Co-op outcomes are trustworthy.** The host decides contested gameplay and economic results. An interaction, save, pickup, sale, or purchase must not be applied twice when requests overlap or retry.

## 3. Alpha scope and boundaries

### In scope

- Windows PC, first-person, solo and online/private co-op for one to four players.
- One persistent small workboat, one explorable island hub, and one sinking cargo-ship exterior with a regenerated three-floor interior.
- A complete repeatable campaign loop: preparation, travel, 10-minute expedition, return, sale and resupply, then a quota evaluation after three expeditions.
- Deterministic procedural interior generation with hinged doors (some locked, opened by keys found in the wreck); 12 scrap types; five purchasable tools (flashlight, flashlight battery, radio, oxygen tank, flare); two non-lethal monster types; steam and submerged-electrical hazards.
- A day/night cycle: each expedition starts by day and ends at night or starts at night and ends at dawn; the island gets a random day or night on each visit.
- A host-side test mode without monsters.
- Host-authoritative game rules, late-join spectators, safe-phase checkpoints, private room codes, positional voice, and radio voice.
- An Indonesian in-game interface and tutorial. Source code and technical documentation may remain English.

### Out of scope for alpha

- Combat that kills monsters, weapon progression, character classes, skill trees, cosmetics, public matchmaking, PvP, more than one wreck or island, and console releases.
- A battery-management loop for the radio (the flashlight has one; see 6.4a).
- Replacing the Unity transport, persistence, or scene architecture without a demonstrated failure in the current design.

## 4. Player, mode, and platform

- **Audience:** Players who enjoy short cooperative expeditions, environmental tension, voice coordination, and escalating team goals.
- **Session size:** 1–4 players. The campaign belongs to the host and is shared by the team.
- **Platform:** Windows PC, built with Unity `6000.6.5f1` and URP.
- **Session type:** Solo local play, private online session joined by code, and local network testing.
- **Language:** HUD, menus, item interactions, warnings, and tutorial use Indonesian. Keep identifiers and serialization keys stable and language-independent.
- **Accessibility and readability:** Important dangers use more than color alone. Warning text and audio are complementary. Settings include microphone selection, voice volume, self-mute, and per-player mute.

## 5. Main game loop

1. The host creates a campaign or loads its last safe checkpoint. Friends join the private lobby on the small boat using its session code.
2. Between expeditions the crew stays on the workboat at sea. At any time (at sea, during an expedition, or at the island) a player at the boat computer can order tools; the package lands on the boat by parachute after 30 seconds and is opened with E. Oxygen tanks are refilled at the computer.
3. The crew uses the workboat helm to travel to the cargo wreck. Travel is automatic after destination selection. Starting a new campaign begins on the workboat at sea with $120 shared money. Arriving at the wreck or the island plays a short arrival cutscene for every client; the expedition clock starts when it ends.
4. The wreck exterior and host-generated interior blueprint load for all active players. The 600-second expedition timer starts only after the active party and world are ready.
5. Players board from the workboat by rope, cross the deck, then enter through exterior access A, B or C. A always arrives on the top deck; B and C arrive on two different, randomly chosen decks and hull sides, so their interior positions change every expedition.
6. Players explore, coordinate tools and routes, retrieve scrap, evade hazards and monsters, and decide when to carry loot back.
7. A player aboard the workboat puts carried scrap down on its deck (drop, G). Anything put down aboard is immediately **boat cargo**: saved with the campaign where it lies, and sellable at the island, but lost if no crew member makes it back aboard. Tools can also be placed on the cabin's tool rack.
8. The crew may use the helm to leave early. Departure has a five-second countdown. Otherwise, the boat automatically leaves when the 10-minute timer ends.
9. Players alive and on the workboat at departure keep their carried inventory. Dead or left-behind players become spectators; their carried items drop, and any such items still on the wreck are lost when it departs.
10. During the safe phase, the team can return to the island to sell and resupply. After the third expedition, the team must return to the island, receive the final sale opportunity, and evaluate its quota.
11. On quota success, the next cycle begins with retained money, protected cargo, and remaining equipment, plus a higher quota. On failure, the campaign resets to its starting economy and empty cargo.

### Campaign and expedition state transitions

| State | Entry | Allowed activity | Exit |
|---|---|---|---|
| Safe hub / preparation | Campaign start, or return from an expedition | Move around the boat at sea (or the island after day 3), order tools, refill oxygen, sell selected cargo (island only), invite/join players | Host selects wreck (days 1–3) or island (after day 3) |
| Travel / loading | Host selects wreck | Load exterior and interior additively; synchronize the blueprint; wait for active players to be ready | Host starts the expedition clock once the world is ready |
| Active expedition | Shared server start time is set | Board, explore, loot, secure cargo on the boat, watch the boat computer, return early, die, or disconnect | Manual departure countdown, 600 seconds elapsed, or all active players dead |
| Departure / results | Extraction begins | Stop expedition interactions, resolve carried items and dropped gear, update used expedition count | Return to safe hub |
| Quota evaluation | Third expedition has ended and final sale at island is available | Sell eligible cargo, view target and progress | Pass: increment campaign cycle and quota. Fail: reset campaign |

The host is the only authority that changes campaign or expedition state. A client UI may predict an animation, but it must display the host-confirmed result.

## 6. Detailed gameplay requirements

### 6.1 The sinking cargo ship and timer

- Expedition duration is 600 seconds (`BalanceConfig.duration`; the milestones below scale with it). The wreck holds steady for the first four minutes and sinks during the last six. The host stores one network start time and duration. Clients derive the visible progress from synchronized server time rather than running independent countdowns.
- The cargo ship descends vertically. The exterior ocean surface remains at a fixed world height. Interior water rises relative to the ship using the same sinking progress and calibrated reference elevations.
- Use one smoothed sinking curve to drive ship height, interior water, access-rope endpoints, flood thresholds, warning cues, and automatic departure. Do not implement separate timers for those systems.
- Calibrate heights against the actual maximum bounds of the ship model. By minute 10 the top of the wreck must be under the exterior surface. At minute 9 the main wreck deck and the workboat deck are level.

| Elapsed time | Required world state |
|---:|---|
| 0:00 | Main routes usable; the lowest interior floor already stands in waist-deep (0.9 m) bilge water that slows walking but does not require swimming. The wreck does not sink yet. |
| 4:00 | The wreck starts to sink (warning: six minutes left). |
| about 6:00 | The rising flood overtakes the bilge level on the lowest floor; from here the interior water follows the sinking curve. |
| 7:00 | Water reaches the middle floor; water monster becomes active. |
| 8:20 | Water begins covering the upper interior floor. |
| 9:00 | Main cargo-ship deck is level with the small-boat deck. |
| 10:00 | Wreck is completely submerged and the workboat departs automatically. |

- Post clear expedition-time warnings when the wreck starts to sink (six minutes remaining), at two minutes, and at 30 seconds remaining. Pair the HUD countdown with hull groans, alarm changes, water sounds, and visible flooding. Audio must reinforce, not replace, readable UI warnings.
- If all active players die, end the expedition promptly, consume one expedition in the quota cycle, and clear the boat's cargo (deck and rack).
- Do not let the visual sinking curve go backward. The boat and water must match for clients joining or loading late.

### 6.2 Boarding, movement, and transitions

- Standard controls: WASD move, mouse look, Shift sprint, Space jump or swim up, Ctrl crouch or swim down, E interact, G drop selected item, keys 1–4 select a slot, V proximity push-to-talk, and R radio push-to-talk.
- Players can walk, sprint, crouch, jump, swim, and dive. Movement, jump, and climb should remain predictable in tight interior modules and on moving ship decks.
- The rope connects the boat to the cargo ship and updates its endpoints as the ship sinks. The player climbs up and down manually. Carrying a large two-handed item reduces climbing speed to 50% of normal while still permitting the climb.
- Provide a rescue ladder from the water to the small boat so the crew can return even when the deck and rope are submerged.
- When on the cargo ship deck, players must follow the deck's vertical motion smoothly without shaking, drifting away, or receiving unintended velocity.
- The three exterior entrances transition to separate interior locations with a brief fade. Entries on the top deck climb an airlock flight to the cargo deck; entries on lower decks arrive in a flat hatch vestibule with a ladder (the teleport stands in for the climb down). The timer, inventory, health, oxygen, and voice/session state continue; the transition must not heal, refill oxygen, reset a carried item, or pause the expedition.
- Water below chest height is waded (slower movement, no breath use); deeper water is swum. Swimming starts above 1.25 m of water and ends below 1.0 m (hysteresis), and a swimmer floats with the head just out of the water, so a depth near the threshold never flips between swimming and walking.
- A swimmer pushing toward a low ledge (up to 2 m above the feet, about 0.65 m above the waterline) pulls up onto it; on land, Space in front of an obstacle up to 1 m high climbs it. The workboat rail (1.15 m) stays higher than that.
- Baseline health is 100. Baseline breath capacity is 35 seconds. Once breath is exhausted, the player loses 10 health per second. Breathing restores when the player's head is above water. A purchased oxygen tank adds 90 seconds of underwater capacity and is refilled at the boat computer for $20.

### 6.3 Procedural interior and level layout

- Each expedition uses a deterministic seed and generator version to produce 24–30 rooms across three floors, with 8–10 rooms per floor. Corridors, landings, stairs, connectors, and the two external access modules do not count toward the room total.
- Room themes include cargo holds, storage rooms, crew cabins, galley, engine room, pump room, and electrical room. Layout randomization covers module arrangement and connections, loot, monsters, and environmental hazards.
- On a given seed and generator version, the host and every client must use the same blueprint. Send or synchronize a canonical blueprint; clients must not independently choose a different seed or regenerate divergent room connections.
- The generator must guarantee all three access points (A on the top deck, B and C on two different decks), reachability of all generated rooms at expedition start, two independent inter-floor connections, at least one loop route on every floor, non-overlapping modules, connected physical doors and stair landings, reachable loot placement, and compliance with the ship bounds.
- Attempt generation no more than 20 times. If every attempt fails validation, load a known-valid fallback and record the failed seed and generator version for diagnosis.
- Validate graph reachability and physical placement before use. Treat navigation connections as gameplay data: monsters may move only over connected space and must not pass through walls.
- The current alpha target is one coherent layout generator and reusable modular room set. Do not increase room count or add new ship variants before layout validation and traversal work reliably.
- The interior layout does not have to mirror the exterior hull shape. Generator v3 places modules on a sealed 9 x 13 grid of 4 m cells per deck: unused cells are closed voids, and every edge between different modules is a wall unless the blueprint declares a door, stair or airlock opening there. Players must never be able to leave the modules into voids or outside the hull.

### 6.4 Scrap, inventory, equipment, and cargo safety

- Each player has four shared slots for both tools and scrap. Item weight slows movement progressively, with a maximum initial penalty of 40%.
- A large scrap item occupies one slot but requires both hands. While carrying it, the player cannot use other tools. It can still be climbed with at half normal rope speed.
- Alpha contains 12 scrap definitions: six common items, three high-value items, and three large items. Initial sale values are between 20 and 150 shared credits. An expedition spawns 18–25 items, plus one high-value or large item behind each locked door, one key per locked door and two or three spare flashlight batteries. Lower floors improve the chance of higher-value scrap.
- Room doorways are 1.1 m wide with one hinged door that the host opens and closes (state synchronized to every peer); open, it stands against the room side clear of the navigation line through the doorway. Two to four dead-end rooms (exactly one door) are locked; a key opens one locked door and is used up. Keys are never boat cargo and stay behind with the wreck. Monsters push unlocked doors open and treat locked ones as walls.
- Players must carry scrap back to the small boat. An item becomes boat cargo when a player puts it down aboard (dropped on the deck or placed on the tool rack); the host validates that the player is aboard, clamps the drop inside the rail, and records the item's position (or rack slot) once. Items only carried, or lying anywhere else, are not secured; standing near the boat or starting departure does not secure them.
- Picking an item up from the boat takes it out of boat cargo until it is put down aboard again. Sold scrap leaves the deck. Deck cargo is saved with its position and is rebuilt on the boat when a session starts.
- The workboat has one continuous rail (higher than a jump) so cargo and crew stay aboard at sea; its only opening is the boarding gate, which opens only while moored at the island dock. The rope and rescue ladder are climbed over the rail.
- A living player aboard the workboat at departure retains their carried inventory, and the boat's cargo is kept.
- On player death, carried equipment and scrap fall in the wreck and can be recovered until departure. **If all players die (or none is aboard at departure), everything on the boat is lost as well** (`CampaignState.LoseBoatCargo`); paid orders still in flight still arrive.
- Each player has four hand slots plus one **belt slot** that holds a torch or a radio (key 5 swaps it with the hand). A torch or radio picked up with an empty belt goes there. A radio on the belt can be keyed with both hands full. The torch is switched with F and stays lit whatever is in hand; held, it shines from the hand, otherwise from a shoulder clip. It is a wide spot with a bright hotspot and a dim spill (no drawn light shaft), strong enough to light a room; the holder's own torch adds a faint bounce light where the beam lands.

### 6.4a Boat computer

- The workboat cabin holds the helm, the tool rack (six shelves, tools only) and the boat computer. Its screen is presented as a CRT monitor. During an expedition it shows a live map that follows one crew member (switchable; solo, the player): the deck they are on, centred on them, with walls, doors (locked ones marked), stairs, entries, hazards and other crew on that deck (monsters are not shown); that diver's location, health, breath and torch; the water level and remaining time from the shared clock; and hazard control.
- Hazard control lets a player at the computer shut one steam or electrical hazard off for 30 seconds, followed by a 60-second cooldown for that hazard (values in `BalanceConfig`). The host validates the player's position and the cooldown, and the off-until/ready-at times are synchronized to every client.
- Purchase every tool; do not grant the crew a free default kit. The shared starting budget is $120.
- **Shop (TOKO tab of the boat computer):** a package holds up to four tools. The host validates the player at the computer, the price and the package size, charges the wallet, and records a `SupplyOrder` (saved with the campaign). After `BalanceConfig.deliverySeconds` (30 s) it drops onto a free spot of the cargo deck under a parachute with a strobe; it lands as boat cargo, and E opens it into its tools on the deck.

| Tool | Starting price | Alpha behavior |
|---|---:|---|
| Flashlight | 15 | Improves visibility; sold with a full battery that lasts about 150 s of light (`flashlightBattery`). |
| Flashlight battery | 10 | Refills a flashlight when used (left click while held, or automatically when switching on a dead torch). Also found in the wreck. |
| Radio | 25 | Enables cross-location radio voice for its carrier; no battery loop. |
| Oxygen tank | 80 | Adds 90 seconds of breath capacity; refill at the boat computer costs 20. |
| Flare | 20 | Single use; distracts either monster for 20 seconds. |

### 6.5 Monsters and environmental hazards

- **Dry-space monster:** Patrols accessible interior routes that are not submerged, investigates footsteps and dropped-item sounds, and pursues visible or detected players. As routes flood it searches connected dry paths that remain available.
- **Water monster:** Activates as the water reaches the middle floor. It navigates connected flooded rooms and hunts players in water. It must not move through walls or use dry-only routes while its movement rules prohibit them.
- **Flare:** A single-use distraction that diverts either monster for 20 seconds, giving the crew time to escape. Players do not kill monsters in alpha.
- **Scream:** When a monster first sights a diver it stops, faces them and screams (`screamSeconds`, 1.3 s), then chases; it screams at most once per `screamCooldown` (15 s). The scream is both a warning and a head start.
- **Steam leak:** A seeded environmental danger in selected rooms or connectors.
- **Submerged electrical hazard:** A seeded electrical area that becomes dangerous when flooded. Its activation must follow the shared water height.
- Starting with campaign cycle four, raise the threat population as available content allows, capped at two dry-space monsters and two water monsters. The expedition duration remains 10 minutes.
- For testing, the host can start a session without monsters (menu toggle or `--no-monsters`).
- Threats should produce legible clues: sound, silhouette, environmental cue, or a clear hazard state. Do not make unavoidable damage the primary source of difficulty.

### 6.6 Death, spectating, and party failure

- A dead or left-behind player becomes a spectator for the remainder of the active expedition. Do not respawn players inside the wreck during that expedition.
- Dead players' carried inventory drops at their death location. Any teammate may recover dropped tools and scrap before the wreck leaves.
- When the active party dies entirely, end the expedition, spend one expedition from the current three-expedition quota cycle, and lose everything on the boat. This must have runtime and persistence coverage.
- At the next safe phase, eligible players return at full health. Lost tools remain lost; the game does not silently replace purchased equipment.
- A player left outside the workboat at departure is treated as dead/left behind and becomes a spectator. Resolve their carried items according to the dropped-loot rules.
- During an active expedition, a late joiner or reconnecting player joins as a spectator until a safe phase. A player disconnecting mid-expedition loses control of their character; host policy drops their carried inventory at the last valid position when possible.

### 6.7 Island economy, quota, and campaign persistence

- The island is a small walkable hub with a dock, the scrap buyer (an old diver in a brass helmet behind the workshop counter, who turns to and waves at approaching crew), and the campaign quota terminal. It is only reachable after the third expedition of a cycle; there is no shop on it.
- Scrap sales use a review list, individual item selection, and explicit transaction confirmation. Money and quota progress are team-owned.
- The first quota is 600 credits of sales over three expeditions. Purchases do not reduce sales progress. Selling above the target still adds the full money value to the team's wallet, but only the target amount counts toward the current quota; excess does not carry into the next quota.
- Always evaluate quota after the third expedition and the return-to-island sale opportunity, even when the target was reached earlier. Do not start a new quota cycle immediately after reaching the number mid-cycle.
- On success, preserve wallet, unsold boat cargo, and remaining equipment. The next target is the previous target multiplied by 1.35, rounded upward to the nearest 10 credits.
- On failure, reset the campaign to the initial 120 credits, first target of 600, empty cargo, and starting cycle. Keep this reset explicit to the user in the quota result UI.
- The campaign repeats until the team fails a quota.
- Save campaign state on the host at safe checkpoints, after safe-phase returns, after sales/purchases, and before departure as specified by the persistence design. Keep a last-known-safe checkpoint before an expedition so interrupted or host-disconnected sessions can recover without inventing expedition rewards.

## 7. Multiplayer, authority, voice, and saves

### 7.1 Session and world authority

- Use Netcode for GameObjects with the Unity Multiplayer Services package for private sessions and Relay-managed internet connectivity. Use Authentication for player identity and Vivox for voice. Follow the package versions resolved in `Packages/packages-lock.json`; do not add a second networking stack.
- One player is host and simulation authority. The host decides the clock, phase, sinking progression, monster state, player health/death, pickup ownership, safe deposit, equipment purchase, sale, wallet, quota, and expedition outcome.
- Local player movement should feel responsive. The host validates gameplay interactions by player state, distance, item state, and transaction state before committing the result.
- Process a pickup, deposit, purchase, sale, departure, quota result, and save operation at most once. Use stable item/session identifiers or equivalent idempotency guards for retries and simultaneous requests.
- Host generates the procedural blueprint and shares it with clients. All clients build their static geometry from that same blueprint. Spawn networked loot, doors with changing state, players, and monsters through the host.
- Keep Bootstrap and the small-boat scene alive for the campaign. Load Island, Exterior, and Interior additively according to the destination; load the exterior and interior together before the expedition timer starts. A door transition moves a player between exterior and interior spaces without a scene reload or timer reset.
- Start the timer only after required additive scenes, blueprint, network objects, and active players are ready. Use NGO scene events and explicit readiness state. Late players follow the spectator rule.
- If the host disconnects, end the current session cleanly and return the party to the menu with a clear explanation. Resume from the last safe host checkpoint; do not award an interrupted expedition.

### 7.2 Voice behavior

- Use positional proximity voice with an initial 15-meter range. Interior and exterior are separated acoustic spaces: a portal transition must not make proximity voice carry through a closed location boundary.
- A player carrying a radio can use radio push-to-talk across locations and underwater. While radio PTT is held, transmit through the radio channel instead of also sending proximity voice, preventing doubled audio.
- Spectators can communicate only with other spectators. Keep spectator channels isolated from living players.
- Provide microphone selection, voice volume, self-mute, per-player mute, and a speaking indicator. Voice failure must not block the salvage game from starting; surface the voice state clearly.

### 7.3 Data contracts and persistence

Use stable, explicit data models. Field names may evolve, but migrations must preserve campaign meaning.

| Model | Minimum information |
|---|---|
| `CampaignState` | Cycle number, shared money, quota target, current-cycle sale value, expeditions used, secured cargo, owned/carried equipment, and safe checkpoint metadata. |
| `ExpeditionState` | Expedition/session ID, seed, generator version, synchronized start time, duration, phase, and per-player alive/ready/spectator state. |
| `InteriorLayout` | Canonical room/module IDs, positions, rotations, door/stair links, floor, loot/monster/hazard placement, access-A/B mapping, seed, and generator version. |
| `ItemDefinition` | Stable item ID, category, base value, weight, large/two-hand flag, and equipment behavior. |
| `BalanceConfig` | Sink curve and milestone heights/times, oxygen, health, movement, prices, quota growth, spawn counts, and threat parameters. |

- Persist host campaign data in a versioned format under `Application.persistentDataPath`. Write atomically and retain one valid backup. Avoid partially overwriting the prior safe file when a write fails.
- On load, validate the main save and then its backup. If both are invalid, show a clear recovery error and do not overwrite either file until the player chooses a new campaign.
- Save after transactions and safe-state transitions; keep a pre-departure checkpoint. Do not save an uncommitted sale, speculative pickup, or failed expedition reward.
- Store currency and sale progress as integer credits to avoid floating-point quota comparisons.

## 8. Unity implementation architecture

- **Engine:** Unity `6000.6.5f1`, URP, Windows x64.
- **Core packages:** Input System, AI Navigation, uGUI, Unity Test Framework, Netcode for GameObjects 2.x, Unity Multiplayer Services 2.x, Unity Authentication, Unity Transport via NGO, and Vivox 16.4+ when compatible with the Editor. The committed package lock is the resolved source of truth.
- **Scenes:** `Bootstrap`, `SmallBoat`, `Island`, `Exterior`, and `Interior`. Bootstrap owns application/session setup. SmallBoat remains the campaign staging area and cargo-protection boundary. Location scenes are loaded additively.
- **Runtime code:** `Assets/SinkScrapt/Runtime`. Keep responsibilities cohesive: campaign data and balance, world generation/rendering, interactions and hazards, network session and player/items/enemies, HUD, audio, and voice.
- **Generated project assets:** `Assets/SinkScrapt/Editor/AlphaBuilder.cs` is the reproducible Editor API builder for scenes, materials, prefabs, and other generated assets. Prefer using or extending this builder for generated Unity objects so the project can recreate them. Do not hand-edit Unity scene or prefab serialization when the Editor API can create the asset safely.
- **Network prefabs:** Keep the registered network prefab set in `Assets/SinkScrapt/Resources/SinkScrapt` and verify changes in the Unity Editor. Avoid runtime-only prefabs that are not registered with NGO.
- **Tests:** `Assets/SinkScrapt/Tests/Editor` contains deterministic core and generator tests. `Tools` contains deliberate editor/runtime QA entry points. Record actual results and test limitations in `Docs/Verification.md`.
- **Balance:** Keep playtest values in data/config where practical. Avoid scattering duplicate timers, prices, and milestone heights across scripts.

### Shared sinking calculation

The conceptual source of truth is one normalized server-time progress:

```text
progress = clamp((serverTime - expeditionStartTime) / 600 seconds, 0, 1)
shipDrop = sinkingCurve(progress)
interiorWaterHeight = calibratedInteriorReference - shipDrop
```

The implementation may choose an equivalent coordinate convention, but it must preserve these invariants: the outside sea stays fixed; the wreck descends; interior flooding is consistent with that descent; the workboat does not sink with the wreck; rope endpoints follow both boats; every client computes the same visual state; and automatic departure occurs at the shared 600-second end.

## 9. Initial balancing values

All numbers below are concrete first-playtest values, not immutable final balance. Change them only alongside a clear gameplay reason and update the relevant docs and tests.

| Setting | Initial value |
|---|---:|
| Maximum party size | 4 players |
| Expedition time | 10 minutes / 600 seconds (sinking from minute 4) |
| Manual departure countdown | 5 seconds |
| Warning thresholds | Sinking starts (6 minutes remaining), 2 minutes, 30 seconds remaining |
| Ship-deck alignment with small boat | Minute 9 |
| Breath capacity | 35 seconds |
| Suffocation damage | 10 HP per second |
| Base health | 100 HP |
| Oxygen tank extension / refill price | 90 seconds / 20 credits |
| Starting shared money | 120 credits |
| First quota / quota cycles | 600 credits / 3 expeditions |
| Quota multiplier and rounding | 1.35x, round upward to next 10 credits |
| Proximity voice range | 15 meters |
| Procedural rooms | 24–30 total; 8–10 per floor; 3 floors; 3 entries; 2–4 locked rooms |
| Generator attempt limit | 20 before validated fallback |
| Scrap per expedition | 18–25 items plus one per locked room |
| Flashlight battery | about 150 seconds of light |
| Scrap values | 20–150 credits initially |
| Weight movement penalty | Progressive, capped at 40% |
| Large-item rope speed | 50% of standard climb speed |
| Flare distraction | 20 seconds, one use |

## 10. User experience requirements

- Keep the current objective, expedition time, safe-cargo amount, carried item, selected slot, health, and oxygen readable during play.
- Present explicit feedback when depositing loot, picking up or dropping an item, running out of oxygen, entering a hazard, beginning extraction, and completing a sale.
- Before a risky transaction, show what the team selected, the sale value, the new shared wallet, and updated quota progress. Require confirmation before the sale commits.
- Explain that cargo on the small boat is lost if nobody makes it back aboard. Teach this in onboarding before the first expedition, and repeat it at the helm and in the results.
- **HUD (minimal):** a 24-hour wristwatch with the in-game time of day (top left) and the objective under it; the team's dollars, quota and day dots (top right); health and stamina bars with icons and the belt slot with its battery meter in riveted frames (bottom left); four hand slots (bottom centre), each showing the item's picture, name and state; and a vertical oxygen gauge on the right edge that appears only while the air is falling or refilling. The sinking progress and flood level are shown only on the boat computer, the alarms and the water itself. Money is shown as `$`.
- **Characters:** each player wears one of four crew characters (green parka, medic, engineer, fisherman), handed out by the host so no two players match; teammates see the animated body (idle, walk, run, crouch, swim, climb, held). The hull stalker sometimes grabs a diver, lifts and throws them (`grabChance`, `grabDamage`, `throwSpeed`); an ordinary strike shoves (`hitKnockback`).
- The player must understand that winning the current quota requires selling over the three-expedition cycle and that quota evaluation waits until that cycle finishes.
- Menus and hub interactions should remain usable with one player and with a full party. A failure to connect voice must provide feedback without blocking local UI or game flow.

## 11. Implementation plan and acceptance gates

Implement in this order. Each gate produces a playable, reviewable slice. A later content milestone must not hide a failing core gate.

### Milestone 1 — Project, session, and co-op foundation

**Deliver:** Bootstrap flow, private host/join code, solo mode, NGO session/player spawning, additive scene lifecycle, ready/loading flow, host leave handling, and basic network HUD.

**Acceptance:** One, two, and four players can join the same private session; the host can move the party between the small boat, island, and wreck scenes; late joiners during an expedition become spectators; host departure returns everyone to menu cleanly.

### Milestone 2 — Movement, boarding, and synchronized sinking

**Deliver:** First-person controller, exterior workboat and wreck, two rope/access points, swim/dive/breath, water surfaces, interior/exterior transition, rescue ladder, shared 15-minute clock and sinking curve.

**Acceptance:** Players can board, enter, exit, and return to the workboat at dry and flooded stages. The rope follows the changing gap. Heavy loot can be climbed in either direction. Timers, oxygen, and inventory survive portals. The wreck deck aligns at minute 9 and the complete model submerges at minute 10.

### Milestone 3 — Complete salvage and quota loop

**Deliver:** Four-slot inventory, representative scrap set, tools, safe cargo storage, helm departure, island shop and buyer, transaction confirmation, shared wallet, quota-cycle state machine, death results, and campaign checkpoints.

**Acceptance:** A team can complete a three-expedition quota cycle, pass it, and fail it. Simultaneous pickup cannot duplicate an item. Deposited cargo survives when all players die. A living player aboard the workboat retains carried items after extraction. Sale and purchase requests cannot commit twice.

### Milestone 4 — Procedural ship interior

**Deliver:** Three floors of modular rooms/corridors, two independent vertical connections, loops, two entrance mappings, deterministic blueprint generation and transmission, seeded loot/monster/hazard placement, and a known-valid fallback.

**Acceptance:** Run at least 1,000 seeds through graph, fit, access, stair, collision, and loot-reachability validation. Traverse ten generated maps in the actual CharacterController. Clients construct identical layouts from the host blueprint. Failed seeds and generator version can be identified from logs.

### Milestone 5 — Horror systems, hazards, and communication

**Deliver:** Dry monster, water monster, steam and electrical hazards, flare distraction, threat cues, spectator behavior, positional voice, radio voice, mute/settings UI.

**Acceptance:** Monsters navigate valid connected routes in dry and flooded spaces without clipping through walls. Hazards follow flood rules. Flare affects both threat types. Proximity respects the 15-meter range and space boundaries; radio works across locations for a carrier; spectator voice is isolated; mute controls are respected.

### Milestone 6 — Recovery, balance, and alpha build

**Deliver:** Campaign migration and backup recovery, disconnect policies, late join flow, clear Indonesian onboarding and warnings, sound and final visual pass, Windows x64 build, performance review.

**Acceptance:** Restart, safe checkpoint recovery, corrupt-main-save backup recovery, and interrupted-expedition behavior preserve committed campaign data without duplication. The complete alpha loop works for solo, two, and four players. On the target GTX 1650 / i5-8400 / 16 GB class at 1080p Medium, average frame rate reaches 60 FPS with 1% low of at least 45 FPS. Record hardware when reporting the result.

## 12. Verification matrix

| Area | Required verification |
|---|---|
| Generator | 1,000 seeds for room count, connectivity, overlap, floors, two entrances, two inter-floor links, per-floor loops, ship bounds, and reachable loot; physically traverse ten layouts. |
| Sinking | Sample all timing milestones; check monotonic descent, fixed sea height, interior flood alignment, rope positions, deck alignment at minute 9, and full submersion at minute 10. |
| Portals and traversal | Enter/exit when dry and submerged. Confirm timer, oxygen, health, and inventory are continuous. Climb both directions with normal and large cargo. Use rescue ladder after rope access is submerged. |
| Transactions | Simultaneous pickup, duplicate deposit attempt, sale retry, insufficient-funds purchase, quota rounding, and departure while loot is carried or deposited. |
| Death and extraction | Dead-player drops, teammate recovery, left-behind spectator, full-party wipe clears the boat's cargo, cargo kept when someone returns, and surviving carried inventory at departure. |
| Quota | Purchases do not subtract sales; early quota completion waits through expedition three; excess sale value does not roll forward; success and failure apply the right campaign state. |
| Multiplayer | Solo, two, and four clients; matching blueprint; late join; reconnect; host disconnect; and network simulation at 150 ms round-trip latency with 2% packet loss. |
| Voice | Proximity radius and walls/zones, radio PTT across locations and underwater, no doubled transmission, spectator-only voice, microphone choice, speaking indicator, and individual mute. |
| Persistence | App restart; successful and failed transactions; pre-expedition checkpoint; atomic write failure behavior; main-save corruption and backup recovery; both saves invalid without silent overwrite. |
| Performance | 1080p Medium, four players, largest interior, monsters active, and flood effects on GTX 1650 / i5-8400 / 16 GB target hardware. Record average and 1% low. |

Use automated tests for deterministic rules and randomized seed coverage. Use actual play-mode traversal and multiplayer runs for physics, synchronization, voice, and player experience. A passing unit test does not prove a multiplayer, audio-device, or target-GPU requirement.

## 13. Main technical risks and mitigations

- **Rope and moving-deck stability:** Exercise ladder and rope at multiple sink heights with ordinary and heavy items. Keep player attachment behavior explicit and test for jitter and clipping.
- **Flood agreement between spaces:** Drive exterior descent, interior water, trigger states, and hazards from one shared timeline and calibrated reference elevations. Check transition behavior at the exact flood thresholds.
- **Procedural graph versus physical geometry:** Validate generated connectivity and then traverse representative layouts physically. A graph-only pass is insufficient when a stair, landing, door, or collider is unusable.
- **Network ownership and duplicate actions:** Keep host-side validations and idempotency keys for contested interactions. Test simultaneous requests rather than relying on UI button disabling.
- **Save integrity:** Keep writes atomic with a backup and a safe checkpoint. Do not overwrite a corrupt campaign automatically.
- **Voice and service configuration:** Package installation alone does not prove cloud services, Relay, microphone permissions, or Vivox works. Verify the actual environment and report service-dependent limitations distinctly.
- **Performance evidence:** An unrelated or more powerful developer GPU does not prove the minimum spec. Use the stated target class or mark the target test outstanding.

## 14. Decision rules for future work

1. Preserve the hard product rules in the design pillars. In particular, a total-party wipe loses the boat's cargo (product-owner decision, 2026-10-15).
2. Prefer the smallest implementation change that completely satisfies an acceptance gate. Extend existing code and packages before adding a new framework.
3. Preserve seed determinism, serialized campaign compatibility, and authority boundaries whenever changing world or economy code.
4. Treat balance values as playtest defaults; tune them deliberately and document meaningful changes.
5. Record real implementation and verification status in `Docs/Verification.md`. Label unverified goals as targets, not completed results.
6. Keep all player-facing text in Indonesian unless the product owner requests another language. This English document exists so English-oriented coding agents can understand the product intent precisely.

## 15. External Unity references

- [Unity additive scene loading](https://docs.unity3d.com/Manual/scenes-working-with.html)
- [Unity Multiplayer Services: Relay connections and join codes](https://docs.unity.com/en-us/mps-sdk/networking/relay-servers)
- [Netcode for GameObjects scene events](https://docs-multiplayer.unity3d.com/netcode/current/basics/scenemanagement/scene-events/)
- [Vivox channel types](https://docs.unity.com/en-us/vivox-unity/developer-guide/channels/channel-types)
- [Unity services pricing and billing](https://docs.unity.com/en-us/services/pricing-and-billing)
