# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

"New Day" is a 2D cozy-survival farming game.

- **Engine:** Unity 6.6 (`6000.6.7f1`), installed at `C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor`
- **Rendering:** URP 2D (`com.unity.render-pipelines.universal`), Tilemaps
- **Input:** the new Input System (`com.unity.inputsystem`). Do not use the old `UnityEngine.Input` class.
- **Art:** pixel art at **16 pixels per unit**, **Point** filtering, no compression, no mipmaps
- **The developer is a beginner.** Explain things in plain language.

## Rules (always follow these)

1. **Write beginner-friendly code.** Comment what each script and each non-obvious block does. Use `/// <summary>` on classes.
2. **Expose settings in the Inspector.** Use public or `[SerializeField]` fields with `[Header]`, `[Tooltip]`, `[Range]`/`[Min]` instead of hard-coded numbers.
3. **Editor scripts go in an `Editor` folder** (currently `Assets/Project/Editor/`). Anything that uses `using UnityEditor;` must live there, or the game will not build.
4. **Before adding or replacing any script, search the whole project for duplicates** of both the file name and the class name (all of `Assets/`, not just the folder being edited). Two classes with the same name break compilation; two files with the same name confuse Unity's script lookup.
5. **Never regenerate sprite sheets with random sprite IDs. Always use stable IDs.** When code slices a sheet, set each sprite's ID with `rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);` (`Assets/Project/Editor/StableSpriteId.cs`): it keeps the ID a sprite of that name already has, and otherwise derives one from the names. Never call `GUID.Generate()` for a sprite. The name/file-ID pairs must be set too. (`EnemyArtRepair.StableId` is the older version used only for the bat and bug sheets; leave it as it is.) Random IDs make every prefab, animation clip and tile that pointed at the old sprites lose its art.
6. **Do not compile or launch Unity unless the developer says Unity is closed.** Unity locks the project while it is open.
7. **Diagnose before changing.** When something is broken, report what is wrong first and wait for a go-ahead before editing.

## Layout

- `Assets/Project/Scripts/` - game scripts (subfolders: `Mine`, `Forest`, `Fishing`, `Dog`, `Mitzi`, `Lighting`, `Tiles`, `Debug Tools`, `Decor`)
- `Assets/Project/Editor/` - editor-only tools. Most add menu items under **Farm >** (see "The Farm menu" below).
- `Assets/Project/Art/` - art, with generated prefabs in a `Prefabs` folder next to each art set
- `Assets/Project/Scenes/` - `Farm`, `Forest`, `Mine`, `Suburbs`
- `Assets/Farm/Data/` - ScriptableObject data (items and so on)
- `Assets/Resources/Lighting/` - `SpriteLit.mat` and other lighting materials used by generated prefabs
- `Assets/Zones/` - Level Editor zone and marker art and prefabs
- There are no assembly definition files for the game code: runtime scripts compile into `Assembly-CSharp`, editor scripts into `Assembly-CSharp-Editor`.

## The Farm menu

Editor tools are grouped by **purpose**, not by game area. Put every new `[MenuItem]` in one of these five submenus and give it a priority in that group's range (a gap of 11 or more between two priorities draws a separator line):

| Submenu | Priority range | What belongs there |
|---|---|---|
| `Farm/Level Editors/` | 0-99 | Tools used every day while building levels |
| `Farm/Set Up/` | 100-199 | Run-once setup for a system or area (for example `Farm > Set Up > Mountain Trail`) |
| `Farm/Build Art/` | 200-299 | Builders for palettes, prefabs and presets, plus the `Kiln Harvest` importer |
| `Farm/Test and Debug/` | 300-399 | Cheats and test helpers (give items, reset progress, drop test objects) |
| `Farm/Fix and Diagnose/` | 400-499 | Checks and repairs |

- Do not add new top-level submenus or loose items directly under `Farm`.
- Do not use `&`, `%`, `#` or `_` in menu names: Unity reads them as shortcut keys.
- When a menu path changes, update every comment, tooltip and dialog that mentions it (search for `Farm > `).
- The right-click `Create > Farm > ...` entries come from `[CreateAssetMenu]` and are a separate menu.

## Tools and upgrades

- The upgradable tools are Hoe, Watering Can, Scythe, Axe and Pickaxe, each in five tiers: Stone, Copper, Iron, Gold, Platinum (the same tiers as weapons and ores; there is no Silver).
- `Farm > Set Up > Tools` (`ToolSetupMenu.cs`) makes the 25 items in `Assets/Farm/Data/Tools`. Default numbers live in the tables at the top of that file.
- What a tool does comes from two Inspector numbers on its `ItemData`: **Tool Reach** (tiles in a row, for Hoe / Watering Can / Scythe) and **Tool Power** (hits per swing, for Axe / Pickaxe). `tier` is only a label.
- Tool pictures are 24x24, one PNG per tool and tier, in `Assets/Project/Art/Tools/` named `<tool>_<tier>.png` (for example `hoe_stone.png`). The handle end is the pivot so the tool turns in the hand.
- Tool movement is done in code, not with animation clips: `HeldItemVisual.cs` picks a motion per Tool Action, and `ToolFx.cs` draws the dirt, drops and chips.
- Things a tool can hit implement `IToolTarget.OnToolUsed(tool, power, user)` and must subtract `power` hits, not 1.

## Fishing

- Scripts are in `Assets/Project/Scripts/Fishing/`. `FishingManager.Run` is the whole trip: bait goes on by itself (last bait used, else the first in the backpack) -> `FishingRig.Cast` (rod swing + bobber arc, drawn in the world, no UI) -> wait (action button or Esc reels in) -> bite: `FishingSfx.Ding`, bobber sinks, "!" over the player -> press the action button within `hookWindowSeconds` -> junk is pulled out at once (no fight), a fish starts the `FishingUI.Minigame` rhythm fight -> `FishingUI.Result` card for a caught fish.
- Junk chance = `junkChance` + `RiverPollution.Level` x `junkChancePerRiverLevel`, times `junkWithBaitMultiplier` with bait, capped at `maxJunkChance`. The `junk` list on the FishingManager (filled by `Farm > Set Up > Fishing` only when empty); if it is empty at run time the built-in waste items are looked up through `SaveCatalog`. Counter `junk_fished`.
- All fishing pictures (bobber, "!" bubble, the underwater fight scene, frames, notes, stars) are drawn in code by `FishingArt`; the sounds (ding, plop, splash) are synthesized by `FishingSfx` unless clips are set on the FishingManager. The rod in the world is the rod item's icon; `HeldItemVisual` hides its carried copy while `FishingManager.IsFishing`.
- `FishingRig` adds itself to the FishingManager object; add one by hand there to tune the cast in the Inspector.

## Crops, seasons and weather

- **Seasons live on the seed item**, not the crop: `ItemData.plantInSpring / plantInSummer / plantInAutumn / plantInWinter`. `CropData.CanGrowIn`, planting (`PlayerActions`), growing (`CropManager`) and the shops (`ShopCounter`) all read those boxes. Winter is off for every crop.
- A crop still in the ground when its season ends **withers** (brown, drooping); the scythe or shovel clears it and it gives nothing. Switch: `CropManager > Wither Out Of Season`.
- `Farm > Set Up > Crop Seasons` (`CropLoopSetupMenu.cs`) applies the default season table at the top of that file to seeds that are still untouched, and removes the old `GeneralStore` stall from the open scene. **The farm has no shop on purpose**: seeds are bought at the General Store in the suburbs (seed rack and checkout), and shops hide seeds that are out of season.
- `WeatherManager` (from suburb pack B, in `Assets/Project/Scripts/Suburb/`) only DECIDES the day's weather. `Assets/Project/Scripts/Weather/WeatherEffects.cs` draws it and applies it: rain and storm water all tilled soil, set `WindSway.WindStrength`, and show rain; snow and fog have their own look. It creates itself in every scene and draws nothing indoors (`HouseInterior.PlayerIsInside`).
- Crops sway through a `WindSway` that `CropManager` adds to each plant (taller = more sway).

## Suburb (general store and apartment)

- Runtime scripts are in `Assets/Project/Scripts/Suburb/` (`InteriorStyle`, `InteriorBlocker`, `NpcSpot`, `SpriteFlipbook`, `StoreFlavor`); art is in `Assets/Project/Art/Store/`.
- `Farm > Set Up > General Store` (`StoreSetupMenu.cs`) builds the store, its interior and the apartment in the open scene, and writes generated assets to `Assets/Farm/Suburb/`.
- `Farm > Set Up > Rebuild Selected Interior` (`HouseRoomRebuildMenu.cs`) repaints the interior selected in the Hierarchy, or the farmhouse when nothing is selected.
- **The Suburbs scene is generated.** `Farm > Set Up > Suburbs Scene` (`SuburbSceneBuilder.cs`) rebuilds `Assets/Project/Scenes/Suburbs.unity` from scratch: it copies the Mine scene (to get the Player, camera, HUD, TimeManager and lighting), empties it, then paints streets and places everything. Running it again REPLACES the scene, so layout changes belong in the constants at the top of that script, not in hand edits to the scene. It also adds `ExitToSuburbs` / `SuburbFarmEntry` to the Farm scene.
- Suburb art is in `Assets/Project/Art/Suburb/` (one PNG per sprite, so no sheet slicing). Prop prefabs are in `Assets/Farm/Suburb/Prefabs/`, labelled for the Level Editor's Objects palette.
- The suburb trees are decoration: a trunk plus a swaying `Canopy` child, with no `Breakable`, so they cannot be chopped and drop nothing.
- Ambient life scripts are in `Assets/Project/Scripts/Ambient/`: `WindSway` (plants rock in gusts), `FlowerSpot` (marks a flower insects may visit), `PollinatorSpawner` + `Pollinator` (butterflies and bees, daytime only).
- To check the generated scene without opening Unity: run Unity with `-batchmode -quit -executeMethod SuburbSceneBuilder.BuildBatch` (rebuilds) or `SuburbSceneBuilder.PreviewBatch` (only takes the picture). Both save an overhead picture to `Logs/suburb_preview.png`. Leave out `-nographics` or no picture is taken.
- Unity gotcha found here: `AddComponent` returns null for a script marked `[RequireComponent(typeof(Collider2D))]` if the object has no collider yet. Add the collider first.

## Suburb people, shops and houses (packs B, C, D)

- **One menu rebuilds it all.** `Farm > Set Up > Suburbs Scene` runs, in order: General Store, Store Paths (`PathToolsMenu`), Frank's Home and Hardware (`FrankStoreSetupMenu`), Suburb Houses (`SuburbHousesMenu`), Kim Family (`NpcFamilySetupMenu`), Suburb Residents (`SuburbResidentsMenu`), then Farmhouse Hardware Upgrades (`HouseHardwareUpgradesMenu`) in the Farm scene. Each also has its own item under `Farm > Set Up` and a no-pop-up `Build()` / `Run()` method that returns a sentence (starting with "Built", "Placed", "Made" or "Added" on success).
- **NPCs** are `ScheduledNpc` (`Assets/Project/Scripts/Suburb/`). Where one is, is worked out from the game clock and today's routes, so nothing needs saving. A schedule is an `NpcScheduleData` asset (`Create > Farm > NPC Schedule`): routes with a start hour, a priority, conditions (`NpcCondition`: weekdays, seasons, weather, flags, counters) and stops. **Two routes that overlap in time cannot both run: the higher priority wins and the other is dropped for that day**, so leave time for walking (1.5 tiles per game minute).
- `Farm > Fix and Diagnose > Check NPC Schedules` (`NpcScheduleCheckMenu.cs`) works out everybody's week without pressing Play and writes it to the Console as `SCHEDULE|` lines (problems are marked `!!`). The scene builder runs it after every build, so it is in the batch log too.
- Places are `NpcSpot` ids (for example `StoreCheckout`, `H3Kitchen`, `ParkBenchA`, `FieldMid`, `PlaySwings`, `PlazaBench1`). NPCs walk only along `PathNode` links; a door or stairs is a portal pair; the node `CityExit` on the right edge has Leads To Scene = `City`. A stop with scene `City` makes the NPC walk off that edge and be away (and turn up at that place in the City scene, see below).
- The street nodes and the outdoor spots are laid by `SuburbSceneBuilder.LayStreetPaths`; hand tools for other scenes are `Farm > Level Editors > Path Nodes - ...`.
- Who lives where: the `Homes` table in `SuburbSceneBuilder.cs` (ten houses, `H1` to `H10`). Who they are, their chat lines and routes: the table in `SuburbResidentsMenu.cs` (18 residents). The Kims (General Store, apartment above it) are in `NpcFamilySetupMenu.cs`; Frank is in `FrankStoreSetupMenu.cs`.
- **Schedule assets that already exist are kept on a rebuild** (`Assets/Farm/Suburb/Family/Schedules`, `Assets/Farm/Suburb/Residents/Schedules`, `Assets/Farm/Suburb/Hardware/Data/Schedules`). To get the starter routes from the code again, delete the asset. Chat lines live on the NPC in the scene and are rewritten every build.
- NPC art: walk sheets are 64 x 84 (4 frames x 3 rows of 16 x 28: down, up, side), portrait sheets 256 x 64 (idle, talk, happy, blink). Kims in `Art/Family`, Frank in `Art/Hardware`, residents in `Art/Residents`. The residents and `sub_house_9` / `sub_house_10` are recolours made by `Tools/make_resident_art.ps1` (edit the colour table there and run it again with `-Project <project folder>`).
- Frank's displays (`HardwareDisplay`) sell house upgrades through `HouseCustomizer.OpenCategories`; his Tools rack is a `ShopCounter` with the tool upgrades. The Suburbs scene has its own `HouseManager` holding only the catalog; what you own is kept in static fields of `HouseManager` for the play session, so a purchase at Frank's shows on the farmhouse.
- `WeatherManager` creates itself at run time and decides the day's weather.
- The old front-only `House_01` to `House_10` prefabs are still in the Objects palette as decoration, but the builder no longer places them.

## Interior decor (catalogue, turning furniture, redecorating)

- **Art:** `Tools/make_interior_art.ps1` draws everything into `Assets/Project/Art/Interior/<Category>/` plus `decor_manifest.txt`, and a contact sheet to `Logs/interior_catalogue.png`. Furniture is modelled as small 3D blocks (C# inside the script) and drawn from the front, right, back and left, so turned pieces show their side / back. Every category has one piece per theme (cottage, modern, rustic, retro, coastal; ids `<category>_<theme>`, e.g. `couch_retro`) plus some `any` pieces (bathroom, clock, mirror...). Change a shape or colour there and run it again; it overwrites the PNGs (single sprites, IDs stay the same).
- **Picture pivots:** furniture PNGs have their pivot at the bottom-left corner of the floor space (2 px in, 3 px up, `PadL` / `PadB` in the script); wall pieces at the bottom-left; floor / wall tiles at the centre.
- **Runtime scripts** (`Assets/Project/Scripts/Decor/`): `DecorItem` (one catalogue entry: 4 sprites, footprint in tiles, mount Floor / Flat / Surface / Wall, `surfaceHeight`, or floor / wall tiles), `DecorPiece` (a placed piece: item + `facing`; its position is the bottom-left of its floor space; adds a SortingGroup, an "Art" child and a collider; sorts against the player like `BehindPlayerSorter`), `DecorRoom` (on a `HouseInterior`: room id + layout), `InteriorLayout` (saved floor, wall covering, zone floors and pieces), `DecorCatalog`.
- `Farm > Set Up > Interior Decor` (`InteriorDecorSetupMenu.cs`) imports the art, makes `DecorItem`s in `Assets/Farm/Interiors/Items`, tiles in `Assets/Farm/Interiors/Tiles`, the catalogue `Assets/Farm/Interiors/DecorCatalog.asset` (existing items KEEP name / description / price / Blocks Player), adds the new floors and walls to any HouseManager's catalog (`Floor_Decor_*` / `Wall_Decor_*` upgrades), then furnishes the rooms in the open scene: `HouseInterior` = Farmhouse, `ApartmentInterior` = KimApartment, `SuburbHouseInterior_n` = H<n>.
- **Layouts are assets and are KEPT:** `Assets/Farm/Interiors/Layouts/<roomId>.asset`. A room with a layout is rebuilt from it; without one it gets the starting layout from `InteriorDecorLayouts.cs` (themes per house: `ThemeForHouse`). Delete the asset to get the starting layout back. The Suburbs scene builder, `Farm > Set Up > General Store` and `Build Farmhouse` furnish through `InteriorDecorTools.Furnish` when the catalogue exists, and fall back to the old `Apt_*` props when it doesn't.
- **Redecorating:** `Farm > Level Editors > Interior Decorator` (`InteriorDecoratorWindow.cs`). Select something in a room; pick a category, click a picture, click in the room (R / Shift+R turn, Esc stops). Selected pieces: R / Shift+R turn, Swap for, Delete; dragged pieces snap to tiles. Floor, zone floors, wall covering and "Restyle the whole room" are at the top. Every change is saved to the layout asset at once. `Ctrl+Shift+R` (ObjectRotation) also turns decor pieces.
- The farmhouse (`DecorRoom.surfacesFromHouseManager`) keeps its bed, stove, slots and design table; its floor and walls still come from the design table. Starting pieces that would overlap existing colliders or inside walls are left out.
- **Room frame:** `InteriorDecorTools.BuildFrame` adds a `RoomFrame` child: thick wall tops round the room (gaps at front doors, order 40) and floor shadows under the back and side walls (order 1). Wall coverings have a third tile with crown moulding (`HouseInterior.PaintWall(upper, base, top)`, `InteriorStyle.wallTop`, `HouseUpgradeData.tileTop`).
- Checking without Unity: the scripts can be compiled with Unity's Roslyn (`Editor/Data/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll` run by `Editor/Data/NetCoreRuntime/dotnet.exe`) using the `-r:` lines of `Library/Bee/artifacts/*/Assembly-CSharp.rsp`.

## Building doors (open / close animation)

- `HouseDoor` and `SuburbHouseDoor` walk the player through with `DoorTransition` (`Assets/Project/Scripts/Doors/`): the door swings open, the screen fades (`fadeSeconds` on each door), the player is moved, and the front door swings shut behind you when you come out. Input is locked meanwhile.
- `DoorSwing` (added automatically by both door scripts) cuts the door out of the building picture and every layer on top of it (so the farmhouse's painted door upgrade is included), and squeezes it toward its hinge over a dark doorway. No new art is needed.
- Where each door is in its picture is the `KnownDoors` table at the top of `DoorSwing.cs` (pixels from the bottom-left, single or double). A new building needs a line there, or a `DoorSwing` added by hand with `Door Pixels` filled in; without one it only fades.

## NPC story arcs (10 parts each, with a choice)

- The plan for all 23 suburb NPCs is `Design/Suburb Story Arcs.md`. All 23 are built (the Kims' ids are `dad`, `mom`, `son`, `daughter`; everyone else uses their first name in lower case). Stories link to each other through `story_<id>_<n>` flags; the links are listed in comments above each household in `StoryArcSetupMenu.cs`.
- A story is an `NpcStoryArc` asset (`Create > Farm > NPC Story Arc`) in `Assets/Resources/Stories/`, found by npc id (no scene wiring). `StoryArcs` (static, `Scripts/Suburb/`) plays it: `ScheduledNpc.Interact` calls `StoryArcs.TryTalk` before picking a chat line.
- Part N normally needs N hearts, the previous part finished, its `NpcCondition`, and no other part of that story finished today. A part can ask for items (quest in the journal, handed over on a later talk, optional min quality) or offer the choice (sets path A or B for good). Parts marked Only A / Only B share a number; usually 9 and 10 exist twice.
- Progress is GameState, so it is saved: counter `story_<id>` (last part), `story_path_<id>` (0/1/2), `story_day_<id>`; flags `story_<id>_<n>`, `story_<id>_asked`, `story_<id>_done`, `story_<id>_chose_a/_b`. Use `story_rosa_7` in another NPC's conditions to link stories.
- `Farm > Set Up > Story Arcs` (`StoryArcSetupMenu.cs`, also run by the Suburbs scene builder) writes the stories from the table in that file; existing assets are KEPT. `Farm > Fix and Diagnose > Check Story Arcs` finds gaps; `Farm > Test and Debug > Story Arcs - ...` adds hearts / allows another part today / resets (Play mode).
- The People tab shows each story's progress and what the next part is waiting for (`StoryArcs.MenuText`).

## City Block (scene `City`, to the right of the suburbs)

- **The City scene is generated.** `Farm > Set Up > City Scene` (`CitySceneBuilder.cs`) rebuilds `Assets/Project/Scenes/City.unity` from a copy of the Mine scene; layout changes belong in the constants at the top of that script. It also turns the Suburbs scene's `CityRoad` into a `SceneExit` (arrive at `FromSuburbs` / `FromCity`). Command line: `-executeMethod CitySceneBuilder.BuildBatch` / `PreviewBatch` (picture in `Logs/city_preview.png`, no `-nographics`). Build the Suburbs scene first: the city reuses its tree prefabs and the neighbours' schedules and art.
- Art is drawn by `Tools/make_city_art.ps1` into `Assets/Project/Art/City` (people are 64 x 84 walk sheets + 256 x 64 faces like the suburb NPCs; Victor Sterling has `portraits_ceo` with 8 faces in 2 rows and `ceo_acts.png`, 4 acts x 4 frames). Sheets are cut with `HardwareEditorUtil.SliceSheet` (stable IDs).
- `Farm > Set Up > City Research and Items` (`CityAssetsMenu.cs`, also run by the scene builder) makes items in `Assets/Farm/Data/City`, `Assets/Resources/SoilCareSettings.asset` and `Assets/Farm/City/ResearchCatalog.asset`. All three are KEPT on a rebuild; starting numbers are the tables at the top of that file.
- Runtime scripts are in `Assets/Project/Scripts/City/`:
  - `ResearchDesk` (Dr. Hale at the university): pay once per `ResearchProject` (flag `research_<id>`), then buy its items.
  - `SoilCare` creates itself in any scene with a `FarmManager`. Soil items (sprinklers, moisture gel 1-4 days, Silver/Gold/Platinum fertilizer, purifier, growth tonic) are ordinary Resource items listed on `SoilCareSettings`; `PlayerActions` calls `SoilCare.TryUse` for them.
  - **Crop quality**: `ItemStack.quality` 0 Bronze, 1 Silver, 2 Gold, 3 Platinum. Set by fertilizer at harvest (`CropManager`), multiplies the sale price, drawn as a coloured band around the icon (`CropQuality.Band`, used by `GameHUD` and `ShippingUI`). Stacks of different quality never merge.
  - The Sterling tower sells at 95%: `ShopCounter`, `HardwareDisplay` and `Smelter` have a `priceMultiplier`; house upgrades use the static `HouseUpgradeData.PriceMultiplier` (reset on every scene load; use `GoldPrice`, not `goldCost`, when charging).
  - `CeoNpc` is Victor Sterling (the antagonist): paces the lobby, plays acts, mood depends on flags `saw_tower_dumping` (set by the `StoryTrigger` in the back room) and `ceo_confronted`, and counters `river_dumped` / `trash_treated`.
  - `CityHallDesk` (improvements = flag `city_imp_<id>`, shown by `CityImprovement` parents; jobs = counter `cityq_<id>`, in the Quests tab). `WasteDesk` is both the treatment plant (fee 20% of sell price, mixed loads at the cheapest rate) and the river bank (`intoRiver`, free). `ItemData.isWaste` items can't be shipped.
  - `RiverPollution.Level` (0-5) comes from `river_dumped`, the tower's daily dumping, `trash_treated` and `river_cleanups`; it paints pollution on the Forest and the top rows of the Farm when they load. Numbers are on `SoilCareSettings`.
  - `CityPerson` = a standing NPC with lines and an optional service; `oneLineOnly` for the tower staff. `CityTraffic` + `CityMover` = random strangers (walkers in groups of 1-4, cyclists 1-3, cars incl. van and bus, scooters) on routes, busier at rush hour; they leave the map and are destroyed.
- **Visitors**: neighbours who work in the city have a second `ScheduledNpc` in the City scene with the same schedule and `homeScene = Suburbs`; it only shows for stops whose scene is `City`. Who works where is the `Visitors()` table in `CitySceneBuilder.cs` (it rewrites the City stops of those schedule assets to point at the right `NpcSpot`).
## Healing Forest (the Forest scene)

- One open forest split into zones you heal. It replaced an older maze of rooms and gates; the maze code (`ForestRoom`, `ForestDoor`, `ForestStyle`, the Maze Generator) and its backup scene were deleted, so don't look for them.
- `Farm > Set Up > Forest` (`ForestSetupMenu.cs`) is still needed once: it makes the pieces the builder copies or places (shadow animal prefabs, grove decorations, Fairy, Shadow Essence, `ForestProgression.asset`) in `Assets/Project/Art/Forest/Prefabs`. `Farm > Set Up > Forest Sick Fairy and Fairy Door` makes the SickFairy and FairyDoor prefabs there.
- **The Forest scene is generated.** `Farm > Set Up > Forest Scene` (`ForestSceneBuilder.cs`) rebuilds `Forest.unity` from a copy of the Mine scene. The layout is the text `Map` at the top of that file (one letter = 4 x 4 tiles: `1`-`6` zones, `~` creek, `G` grove, `#` old woods), plus the `Trail` points and the `Zones` table (stump, sign, blocker gap, animals, scenery mix). Walls of trees grow wherever two areas meet, except at the gaps. Command line: `-executeMethod ForestSceneBuilder.BuildBatch` / `PreviewBatch` (picture in `Logs/forest_preview.png`, no `-nographics`); each zone's request is logged as a `FORESTZONE|` line.
- What each heart stump asks for comes from `Assets/Farm/Data/Forest/ForestProgression.asset` (stump 1 = the Trailhead). `ForestProgression` lives in its own file: a ScriptableObject class must be in a file of the same name, or its assets lose their script link (`m_Script: {fileID: 0}`).
- Flat things in the forest use sorting order 5 (above the tilemaps, which use 0-4) and standing things 8.
- Test helpers (Play mode, `ForestHealTestMenu.cs`): `Farm > Test and Debug > Healing Forest - Heal Next Zone / Heal All Zones / Give Next Stump's Items / Go To Next Heart Stump / Reset All Zones`. Achievements: one per zone (`forest_trailhead` ... `forest_mire`), `forest_whole` (counter `forest_zones_healed` = 6) and `gentle_hands` (10 pets).
- Play check without opening Unity: `-batchmode -executeMethod ForestHealPlayCheck.RunBatch` (no `-quit`, no `-nographics`; it quits itself). It heals the Trailhead in Play mode, logs `FORESTCHECK|` lines (tiles, scenery, blocker, animals, fog) and saves `Logs/forest_heal_before.png` / `_after.png`. `-checkScene <path>` plays another scene for comparison.
- Batch-mode gotcha: press Play only after Unity has finished compiling/importing (`EditorApplication.isCompiling` / `isUpdating` both false). A script reload that lands during Play empties every script's unsaved fields (statics like the backpack, private lists), which shows up as duplicate fog and NullReferenceExceptions in `Inventory` / `MusicManager` that are not real bugs.
- Art: `Tools/make_forest_heal_art.ps1` draws everything into `Assets/Project/Art/ForestHeal` as `<name>_sick` / `<name>_healthy` pairs (one PNG each), plus animal sheets `critter_<animal>` and `critter_<animal>_shadow` (144 x 72: 6 frames x 3 rows of 24 x 24 = idle, move, special; facing right). Running it again gives identical pictures (fixed seeds); `-OutDir` writes elsewhere for trying colours.
- Scripts (`Scripts/Forest/`): `ForestZone` (a box; flag `forest_zone_<id>`, counter `forest_zones_healed`; heals with a green wave that swaps floor tiles, `HealSwap` pictures and opens `ZoneBlocker`s, turns `ShadowCreature`s into `ForestCritter`s, fades fog and cleans `PollutionMap` in the box). `ForestHealSettings` (`Assets/Resources/ForestHealSettings.asset`: sick/healthy tile pairs, wave look, critter frames). `HealSpark` = the sparkles.
- A `ForestStump` with its **Zone** set is that zone's heart stump. `ForestStumpUI` works on `IForestOffering` (`ForestZone` implements it); offerings are counted under `forest_give_zone_<id>_<n>`.
- `ForestGuide` (no settings) describes the next sick zone (lowest `order`). `SickFairyNpc` recovers a little per healed zone (`recoveryPerZone`). Petting an animal adds to counter `forest_critters_petted` (once per animal per day).
- `Reset All Zones` also resets the fairies (`fairy_met`, `sick_fairy_met`, `sick_fairy_promised`, the Fairy's quest counters) and the old-save flags below.
- **Old maze saves:** the first time a zone loads, `ForestZone.UpgradeOldSave` (in `OnEnable`, before anything reads `IsHealed`) checks the save once (flag `forest_old_save_checked`). If `forest_grove_reached` is set but `forest_zones_healed` is 0, it sets `forest_old_save_grove`, and every zone counts as healed from then on, quietly, with no wave. Saves that were partway through the maze start the zones from the beginning.

## Farm debris (random clutter on the farm)

- Scripts are in `Assets/Project/Scripts/FarmDebris/`. `FarmDebrisSpawner` (a `SpawnAreaBase`) creates itself in the `Farm` scene, scatters a starting set (weeds, stones and fallen trees cover `startCoverage` = 50% of the free ground tiles, split by each group's Start Count; litter is an exact count) and adds more each morning (catching up nights away, up to `maxCatchUpDays`). What lies where is remembered between scenes (static) and saved (`SaveFile.debris`, by Kind id and position, via `FarmDebrisSpawner.CaptureForSave / LoadFromSave`). Renaming a Kind id drops those pieces from old saves; a save from before this gets a fresh scatter.
- Groups: Pollution (cans, debris, black tar lumps, purple goo lumps: `LitterPickup`, picked up with the action button, gives waste items; lumps pollute their tile until picked up), Weed (`Breakable` needing the Sickle, cut by the scythe along its reach, drops Fiber), Stone (the mountain `MineableRock` prefabs, rare coal / copper), FallenTree (`Breakable` + Axe, drops Logs). The hoe won't till a cluttered tile.
- All numbers are on `Assets/Resources/FarmDebrisSettings.asset` (kinds with weights, a rule per group). `Farm > Set Up > Farm Debris` (`FarmDebrisSetupMenu.cs`) makes it, the items in `Assets/Farm/Data/FarmDebris` and the prefabs in `Assets/Farm/FarmDebris/Prefabs`; all are KEPT on a rerun. Art: `Tools/make_farm_debris_art.ps1` into `Assets/Project/Art/FarmDebris`.
- **Sterling raises litter**: `SterlingInfluence` counts `sterling_dealings` (a purchase in a discounted shop = the Sterling tower, a smelt, a house upgrade bought there, talking to Victor once a day). Litter per day = base + dealings x `pollutionPerDayPerDealing` + river level x `pollutionPerDayPerRiverLevel`, capped at `pollutionMaxPerDay`.

## Mine shaft (hub and 100 generated floors)

- **Naming trap:** the scene `Mine` is the OUTDOOR mountain trail. The underground mine is the scene `MineShaft`.
- **One scene, regenerated in place.** `MineShaftManager` (`Assets/Project/Scripts/MineShaft/`) shows the hand-built `Hub` object for floor 0 and, for floors 1-100, switches the hub off and paints a new floor into the `Ground` / `MineWalls` / `MineFill` tilemaps from a `MineFloorLayout` made by `MineFloorGenerator` (four styles: Caves, Rooms, Tunnels, Chambers). Go to a floor with `MineShaftManager.Instance.GoToFloor(n)`.
- **The MineShaft scene is generated.** `Farm > Set Up > Mine Shaft` (`MineShaftSetupMenu.cs`) rebuilds `Assets/Project/Scenes/MineShaft.unity` from a copy of the Mine scene, so hub changes belong in the constants at the top of that script. It also edits the Mine scene: sets `MineEntrance.sceneName`, and adds the object `MineShaftLinks` (spawn point `FromMineShaft` and the trail's fairy door `MineFairyDoor`).
- **All tuning is in `Assets/Farm/Mine/MineShaftConfig.asset`** (`MineShaftConfig`): Ore Chance (0.05 = each rock has a 5% chance of being ore), the per-depth ore table, rocks and enemies per 100 tiles, floor sizes, cart interval, and the five themes (tiles, light, plain rocks, enemy set, stat growth). The asset and the enemy prefabs in `Assets/Farm/Mine/Prefabs/` are KEPT on a rebuild; delete one to get the starting version back.
- Themes: 1-20 icy, 21-40 rocky, 41-60 flooded cave, 61-80 desert, 81-100 lava. Enemy sets: Bug / Bat / Frost Slime; Mud Slime / Rock Spitter / Mole; Cave Bat / Bubble Spitter / Stone Golem; Scarab / Sand Worm / Cactus; Magma Slime (splits into Ember Slimes) / Fire Bat / Fire Spitter / Magma Golem.
- New enemy scripts (all on `EnemyBase`, in `Scripts/Mine/`): `SlimeEnemy` (hops), `ShooterEnemy` + `EnemyProjectile` (keeps its distance and spits; shots and spread are Inspector numbers), `BurrowerEnemy` (underground and unhittable until it surfaces), `GolemEnemy` (slow, telegraphed area slam).
- Progress is in `GameState`: counter `mine_deepest_floor`, flag `mine_fairy_door`. The fairy doors are `ForestShortcutDoor` with `travelToScene` set (a door to another scene) and, in the hub, `wakeOnInteract`.
- `Underground.Active` (static) is true in the mine shaft: `WeatherEffects` draws nothing and `LightingManager` uses the theme's ambient light with lamps always lit.
- Art is drawn by `Tools/make_mine_art.ps1` into `Assets/Project/Art/MineShaft/` (one PNG per picture, except three recoloured bat / bug sheets cut with `StableSpriteId.For`). Palette keys in that script must not differ only by upper / lower case (PowerShell treats them as the same key).
- Checking without playing: `-executeMethod MineShaftSetupMenu.BuildBatch` (build), `PreviewBatch` (pictures in `Logs/mine_*.png`; add `-mineFloors 1,50,100` to choose floors; no `-nographics`), `VerifyBatch` (generates all 100 floors and logs a `MINEFLOOR|` line each). In the editor: `Farm > Test and Debug > Mine Floor Previews (one per theme)`, `Unlock All Mine Cart Floors`, `Reset Mine Progress`.

## Player menu

- Scripts are in `Assets/Project/Scripts/PlayerMenu/`. **Tab** (or Esc) opens it; tabs are Character, Skills, Crafting, Quests, Achievements, People, Settings.
- `PlayerMenu` draws the window and handles keys; it creates itself at run time, so it is in no scene. What each tab shows is in `PlayerMenuPages.cs` as lists of `PlayerMenuRow` (a `MenuRow` with an icon and a bar).
- Tunable numbers and lists (open key, colours, XP rules, perks, achievements, crafting recipes, friendship points) are on `Assets/Resources/PlayerMenuSettings.asset` (`PlayerMenuSettings`). `Farm > Set Up > Player Menu` (`PlayerMenuSetupMenu.cs`) makes it, plus starter equipment in `Assets/Farm/Data/Equipment` and recipes in `Assets/Farm/Data/Crafting`. Without the asset the built-in defaults are used.
- `GameSettings` (UI size, volume, brightness; saved in PlayerPrefs) is applied by `ScreenSettingsApplier`, which rescales every `CanvasScaler` and lays a brightness sheet over the screen.
- `PlayerSkills` (Farming, Mining, Foraging, Combat) earns XP by **listening to GameState counters** (the Xp Rules list), so gameplay code never calls it. Fishing still uses `FishingTalents`. Progress is stored in counters `xp_<skill>` and `perk_<id>`.
- Equipment: an `ItemData` with Type = Equipment and an Equip Slot; `PlayerEquipment` holds what is worn (static, survives scene changes).
- **`PlayerBonuses` is the one place gameplay reads bonuses** from perks and equipment (`PlayerStats`, `PlayerController`, `EnemyBase`, `PlayerActions`, `CropManager` each read it once). Add new bonus kinds there.
- `Achievements` watches counters, flags and gold; done = flag `ach_done_<id>`. `QuestLog` is the journal: quest givers call `QuestLog.Add` / `QuestLog.Complete` (only `FairyNpc` does so far). `Friendship` stores points in `friend_<npcId>`; `ScheduledNpc` calls it for the daily chat and the "Give <item>" gift choice, using the NPC's `NpcProfile` for tastes.
- A `Recipe` with **Crafted** ticked counts as `items_crafted` instead of `meals_cooked`.

## Book UI (how the windows look)

- Every window is drawn as a storybook by `BookSkin` (`Assets/Project/Scripts/UI/BookSkin.cs`): a leather cover with brass corners, cream pages (one page, or an open two-page book), bookmark ribbon tabs, a honey highlighter on the chosen row, a red ribbon banner for titles, and ink-coloured writing. Used by `MenuUI`, `PlayerMenu`, `TitleScreen` (the front cover), `DayRecapScreen` (a two-page journal), `ShippingUI`, `DialogueBox` and `ForestStumpUI`. The HUD and fishing screens keep their own look.
- The art is one PNG per piece in `Assets/Project/Art/UI/Book` (`book_cover`, `page_single`, `page_left`, `page_right`, `gutter`, `tab`, `tab_selected`, `highlight`, `rule`, `banner`, `divider`, `slot`, `slot_selected`, `label_box`, `bar_back`, `portrait_frame`). **The developer repaints these by hand**; keep the file names. Defaults are drawn by `Tools/make_book_ui_art.ps1`, and running it again OVERWRITES hand-painted files, so don't.
- `Assets/Resources/BookSkin.asset` holds the sprites, ink colours, pixel scale (2 screen px per art px), cover margins and page padding. `Farm > Set Up > Book UI` (`BookSkinSetupMenu.cs`) imports the PNGs (50 PPU, Point, borders from its `Pictures` table) and fills only EMPTY sprite slots; `BookArtImporter` does this by itself on first import. Borders are only set when a sprite has none, so Sprite Editor changes stay.
- New windows: build them with `skin.BuildBook(...)` and put everything inside `leftContent` / `rightContent`; size the cover with `skin.CoverSizeFor(...)` from the content so it always fits. Use `skin.NewText` (wraps and shrinks to its box), `skin.Ink(color)` for colours chosen for dark backgrounds, `skin.PaintRow` for row highlights and `skin.NewCoverHint` for key help on the leather.

## Wall and cliff shadows

- The sun is a Global `Light2D`, and URP global lights cannot cast shadows. Sun shadows are painted instead: `ShadowCaster` (sprites, trees) and `WallSunShadows` (walls and cliffs). Real Unity shadows (`ShadowCaster2D`) come only from point lights (`LightSource`: Cast Shadows / Shadow Strength / Shadow Softness).
- `WallShadowBlock` (`Scripts/Lighting/`) marks a wall: a non-trigger `BoxCollider2D` or a tilemap with a `TilemapCollider2D` (only tiles with a collider count). Its `height` sets the sun shadow length. In `Start` it makes sure a `ShadowCaster2D` shaped like the collider exists (`LampShadowHelper.CastFromCollider`, which sets Unity's hidden "Casting Source = Collider 2D" by reflection, because a tiled sprite's own outline is only one tile).
- `WallSunShadows` creates itself when a scene has a `WallShadowBlock` and paints all wall shadows into one point-filtered texture, redrawn only when the sun moves. Master switches on `LightingManager`: `Lamp Shadows`, `Wall Sun Shadows`.
- `Farm > Set Up > Wall Shadows` (`WallShadowSetupMenu.cs`) adds them to the Forest and Mine scenes (tilemaps named `Cliffs` / `Walls` / `Fences` with a collider) and saves them; walls that already have a `WallShadowBlock` are kept. The Forest Level Editor's `Walls` map and the Geography Brush's `Cliffs` map add them to new walls.

## Music

- The tunes are composed in code by `Tools/make_music.ps1` (a small C# synthesizer inside the script; no samples). It writes seamless 32-bar loops to `Assets/Project/Audio/Music`: `music_farm` (Farm + Suburbs, upbeat D major 112 BPM), `music_city` (jazzier F major 124 BPM), `music_forest` (solemn A minor waltz 72 BPM), `music_mountain` (Mine trail + MineShaft, grand D minor 88 BPM). Notes and chords are the strings in its `NdSongs` class; run it again with `-Project <project folder>` (add `-Only farm` for one track).
- `MusicManager` (`Assets/Project/Scripts/Audio/`) creates itself, survives scene changes, cross-fades between tunes, keeps playing when two scenes share a clip, is quieter indoors, and adds an `AudioListener` to the main camera if the scene has none (the scenes had none). `PlayOverride` / `ClearOverride` for special moments.
- Which tune plays where and the volumes are on `Assets/Resources/MusicSettings.asset`, made by `Farm > Set Up > Music` (`MusicSetupMenu.cs`, KEPT on a rerun). `MusicImportSettings` gives new files in the music folder Streaming + Vorbis.

## Saving, title screen and day recap

- Scripts are in `Assets/Project/Scripts/SaveLoad/`. `SaveSystem` (static) writes 3 slots as JSON (`save_slot1.json`...) to `Application.persistentDataPath`; `SaveFile.cs` holds the plain data classes (several small types in one file, like `Damage.cs`).
- **Auto save only**: `TimeManager.StartNewDay` ends with `DayRecap.FinishDay`, which saves and then shows `DayRecapScreen` (XP earned per skill + fishing, items shipped, total). Starting a New Game also saves once. There is no manual save.
- `TitleScreen` creates itself when the game starts (Continue / New Game / Load Game / Quit). `Farm > Test and Debug > Skip Title Screen When Testing` skips it in the Editor (PlayerPrefs key); a build always shows it. Without the title screen there is no slot, so nothing is saved.
- Saved: GameState (gold, flags, counters, so skills, perks, friendship points, achievements, mine progress), backpack, mailbox, equipment, house upgrades, soil + crops per scene, SoilCare marks, quests, people met, the clock, farm debris. Not saved: trees, farm pollution, dropped items, player position (a load wakes you in bed in the `Farm` scene).
- Items are saved by asset name and found through `Assets/Resources/SaveCatalog.asset`, refreshed automatically on Play and before builds (`SaveSetupMenu.cs`, also `Farm > Set Up > Save System`). Two ItemData assets with the same file name would clash; the refresh warns.
- Crops are saved by CropData asset name, so renaming a crop or item asset breaks old saves for that thing (it is skipped, not crashed).
- The clock and each scene's soil and crops are also kept between scene changes (`SaveSystem.Clock`, `RememberSoil/RememberCrops/RestoreFarm`); nights spent away from the farm are caught up when you return (up to 28).
- After loading, `PlayerSkills.SyncAfterLoad()` must run, or loaded counters would be counted as new XP.

## Conventions seen in the code

- One public class per file, file name equal to the class name. Exceptions that hold several small types: `Damage.cs`, `ForestData.cs`.
- Small private nested enums named `State`, `Mode` and so on are reused inside different classes. That is fine because they are nested; do not make a top-level type with one of those names.
- Setup menus are written to be safe to run again. Keep that true: re-running a setup must not change existing sprite IDs or break references.
- Enemies derive from `EnemyBase` (`Assets/Project/Scripts/Mine/EnemyBase.cs`) and animate by swapping `Sprite[]` frame lists, not with Animator clips.
- Sprite sheet cells are named `<sheet>_<column>_<row>`, with row 0 at the top.

## Checking work without Unity

- Unity writes its log to `Logs/Editor.log` inside the project. Compile errors appear there as `error CS....`.
- Sprite references can be checked by hand: a reference like `{fileID: N, guid: G}` in a prefab must match an `internalID: N` in the `.png.meta` whose `guid` is `G`.
