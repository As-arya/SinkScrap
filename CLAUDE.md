# Claude Code Project Guide

## Start here

Before changing the game, read these project documents in this order:

1. `Docs/PRD.md` — complete product vision, gameplay rules, network and save contracts, milestone order, and acceptance criteria.
2. `README.md` — how to open and play the current Unity project, controls, and project map.
3. `Docs/Verification.md` — what has actually been built and verified, and what remains unverified.
4. `Docs/Assets.md` — asset provenance and visual-art notes when changing content or presentation.
5. `Docs/BlenderAssets.md` — when making models in Blender (`blender` MCP server in `.mcp.json`): the prioritised build list, sizes, export rules and integration steps. Reference images are in `Docs/References/`.

Then inspect the relevant existing code, Unity scene, prefab, and package versions before designing a change. The repository already contains a Unity implementation; do not treat the project as an empty prototype or replace its architecture without evidence.

## Product intent

Build SinkScrapt as a gloomy stylized first-person cooperative salvage game for one to four Windows players. Players use a small boat as their persistent hub and protected cargo hold, board a derelict cargo ship that descends over a 10-minute expedition, search a regenerated three-floor interior, carry scrap back, and sell it at an island to meet an escalating quota.

The team's main decisions concern value, time, oxygen, carrying capacity, routes, and survival. The sinking wreck should visibly lower beside the small boat. The interior flood, access rope, hazards, warnings, and automatic departure must agree on the same shared timeline.

## Non-negotiable gameplay rules

- **Boat cargo needs a survivor (changed by the product owner, 2026-10-15).** Anything a player puts down aboard the small boat (dropped on its deck or placed on the tool rack) is boat cargo, saved with its position. If at least one crew member is alive and aboard at departure, all of it is kept. **If every player dies or is left off the boat, everything on the boat (deck and tool rack) is lost** (`CampaignState.LoseBoatCargo`). Paid computer orders still in flight still arrive.
- A cargo-ship run lasts 600 seconds unless the host initiates early extraction. The wreck holds steady for four minutes and sinks over the last six: its deck reaches the small-boat deck at minute 9 and the full wreck is submerged at minute 10 (milestones in `ShipProfile.Cargo`, read through `ShipTimeline`). The arrival cutscene (`BalanceConfig.arrivalCutscene`) plays before minute 0: the host sets `Started` that far ahead.
- **Two wrecks (product owner, 2026-10-16).** The helm also offers the abandoned cruise ship (PRD 6.8): 720 seconds (`BalanceConfig.cruiseDuration`), steady for five minutes and sinking for seven, five decks with a fixed two-storey lobby behind main door A and emergency exits B and C. Per-ship values (grid size, decks, room counts, timeline, loot, stalkers) live in `ShipProfile`; `ShipGrid`/`ShipTimeline` read the active profile, which follows `NetSession.Ship` and the blueprint's `ship` field. Do not reintroduce per-ship constants elsewhere.
- A quota cycle contains three expeditions ("days"). The crew stays at sea between them; **the island is only reached after the third expedition**, to sell scrap to the diver merchant and evaluate the quota. Tools are **ordered on the boat computer** at any time and arrive by parachute on the boat (`BalanceConfig.deliverySeconds`, 30 s). Evaluate the quota after the final sale, even if the target was reached earlier.
- The host owns contested gameplay and economy outcomes. Validate and deduplicate pickups, deposits, purchases, sales, departure, and quota changes.
- A procedural layout is seeded and validated. All active clients build from the host's same serialized blueprint. Physical access must match logical reachability.
- Keep player-facing interface and onboarding in Indonesian. Technical code and documentation may be English.

## Current Unity project

- Editor version: Unity `6000.6.5f1`, URP, Windows x64.
- Runtime code: `Assets/SinkScrapt/Runtime`.
- Editor asset/scene builder: `Assets/SinkScrapt/Editor/AlphaBuilder.cs`.
- Network prefabs, materials, and balance data: `Assets/SinkScrapt/Resources/SinkScrapt`.
- Scenes: `Assets/SinkScrapt/Scenes` (`Bootstrap`, `SmallBoat`, `Island`, `Exterior`, `Interior`).
- Wrecks: cargo ship generator in `InteriorLayout.cs` (`InteriorGenerator`), cruise ship generator in `CruiseLayout.cs`; cruise interior look and furniture in `WorldCruise.cs`, its exterior in `WorldCruiseExterior.cs`. Both wrecks share the walls, doors, navigation, stairs and network code.
- Core Editor tests: `Assets/SinkScrapt/Tests/Editor` (`CruiseTests.cs` covers the cruise ship).
- Editor and runtime QA helpers: `Tools`.
- Characters: four crew bodies, the island's diver merchant and both monsters are rigged Blender models (`Resources/SinkScrapt/Models/Characters`, sources in `ArtSource/Blender/characters.blend` and `ArtSource/Blender/scripts`, bodies built with `charbody.py`) played through the legacy `Animation` component (`ClipSet` in `CharacterFx.cs`). `CharacterFx.cs` also holds `CrewAnimator`, `MerchantNpc` and the arrival `Cinematic`.
- Look and feel: the PS1-style post filter is `Assets/SinkScrapt/Art/RetroPost.shader`, wired into `PC_Renderer` by the menu items **SinkScrapt / Build Retro Look** and **Build Atmosphere** (both also run from Build Alpha Assets; Build Atmosphere also makes the transparent `Glass.mat` and `Decal.mat`). Horror dressing (decals, painted Indonesian warnings, debris, hanging cables) is `WorldDecor.cs`. The torch is a narrow bright hotspot plus a wide dim spill (two spot lights, `NetPlayer`) and a bounce light, with no drawn light shaft. Do not give it a light cookie: URP then shaded some surfaces outside the cone solid cyan.
- Audio: recorded CC0 clips processed by `ArtSource/Audio/build_audio.py` (run in Blender's Python) into `Resources/SinkScrapt/Audio/<Snd>_<n>.ogg`; `SoundBank` prefers them and synthesises everything else (UI, alarms, fallbacks). Sources and licences are listed in `Docs/Assets.md`. Do not add sound files without recording their source there. `AudioDirector` mixes the location beds (sea, gulls by day, wind, a near-silent interior room tone with hull creaks) and `PlayerFeel` picks footsteps from the floor collider's procedural material.
- Monsters are `NetEnemy` (server brain) plus `EnemyRig` (client visuals and audio); a monster that first sights a diver enters `EnemyState.Scream` (`screamSeconds`) before the chase. UI is `UiKit` plus `GameHUD`, in Indonesian; item icons are rendered from the models (`ArtSource/Blender/scripts/icons.py` → `Resources/SinkScrapt/Icons`). See the README section "Tampilan, suara, dan monster".
- Runtime QA (`QaRunner`) also covers monster behaviour and records frame times; it runs in the Editor via `Tools/StartQa.cs` (cargo ship) or `Tools/StartQaCruise.cs` (cruise ship) and in a Windows build with `--qa-host [--qa-players N] [--qa-ship cruise]` (clients: `--local-client`). Do not edit scripts while Play mode is running a session: the recompile ends it.
- The package lockfile is the resolved dependency record. Preserve compatible installed versions; do not update packages casually or create a parallel networking/voice stack.
- The Unity CLI and connected Editor are part of the existing workflow. Inspect their current state and use the CLI/Editor API for scene, prefab, and package operations when appropriate.

## Engineering guidance

- Read existing code before changing it. Reuse the current components, data models, generated assets, and registered NGO prefabs wherever they already cover the requirement.
- Keep changes focused and complete. Put new balance values in their shared configuration/data source rather than copying constants into multiple systems.
- Keep the sinking clock server-based and shared. Do not add local timers for water, warnings, ship position, or rope endpoints.
- Keep persistence transactional: version data, save atomically, retain a backup and safe checkpoint, and do not silently overwrite both an invalid save and its recovery copy.
- Keep procedural generation deterministic and validate both graph rules and physical module connections. Log failing seed and generator version.
- Create or edit Unity-generated assets through Unity Editor APIs or the project's builder. Avoid hand-editing serialized scene/prefab YAML when an Editor API operation can do it safely.
- Do not expose private room codes, credentials, auth tokens, or service secrets in logs, documentation, or user-facing responses.
- Avoid adding new packages or broad refactors unless they are needed to meet a concrete PRD requirement or fix a demonstrated failure.

## Working and reporting

- For gameplay, save, generator, or network changes, run the most relevant existing Unity test or targeted QA workflow when available. Do not describe a requirement as complete based only on compilation or a unit test if the PRD calls for runtime, multiplayer, voice, or target-hardware validation.
- Update `Docs/Verification.md` only with results actually observed. State the tested Unity version, player count, hardware, and important limitations when relevant.
- Preserve player-facing Indonesian text when editing UI. Document technical decisions in English so future coding agents can follow them.
- Before finishing, summarize changed files, the behavior change, verification performed, and any acceptance criteria still outstanding. Link to source or documentation paths directly.

