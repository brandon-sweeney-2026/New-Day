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

- `Assets/Project/Scripts/` - game scripts (subfolders: `Mine`, `Forest`, `Fishing`, `Dog`, `Mitzi`, `Lighting`, `Tiles`, `Debug Tools`)
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
- Places are `NpcSpot` ids (for example `StoreCheckout`, `H3Kitchen`, `ParkBenchA`, `FieldMid`, `PlaySwings`, `PlazaBench1`). NPCs walk only along `PathNode` links; a door or stairs is a portal pair; the node `CityExit` on the right edge has Leads To Scene = `City`. A stop with scene `City` makes the NPC walk off that edge and be away. **There is no City scene yet.**
- The street nodes and the outdoor spots are laid by `SuburbSceneBuilder.LayStreetPaths`; hand tools for other scenes are `Farm > Level Editors > Path Nodes - ...`.
- Who lives where: the `Homes` table in `SuburbSceneBuilder.cs` (ten houses, `H1` to `H10`). Who they are, their chat lines and routes: the table in `SuburbResidentsMenu.cs` (18 residents). The Kims (General Store, apartment above it) are in `NpcFamilySetupMenu.cs`; Frank is in `FrankStoreSetupMenu.cs`.
- **Schedule assets that already exist are kept on a rebuild** (`Assets/Farm/Suburb/Family/Schedules`, `Assets/Farm/Suburb/Residents/Schedules`, `Assets/Farm/Suburb/Hardware/Data/Schedules`). To get the starter routes from the code again, delete the asset. Chat lines live on the NPC in the scene and are rewritten every build.
- NPC art: walk sheets are 64 x 84 (4 frames x 3 rows of 16 x 28: down, up, side), portrait sheets 256 x 64 (idle, talk, happy, blink). Kims in `Art/Family`, Frank in `Art/Hardware`, residents in `Art/Residents`. The residents and `sub_house_9` / `sub_house_10` are recolours made by `Tools/make_resident_art.ps1` (edit the colour table there and run it again with `-Project <project folder>`).
- Frank's displays (`HardwareDisplay`) sell house upgrades through `HouseCustomizer.OpenCategories`; his Tools rack is a `ShopCounter` with the tool upgrades. The Suburbs scene has its own `HouseManager` holding only the catalog; what you own is kept in static fields of `HouseManager` for the play session, so a purchase at Frank's shows on the farmhouse.
- `WeatherManager` creates itself at run time and decides the day's weather.
- The old front-only `House_01` to `House_10` prefabs are still in the Objects palette as decoration, but the builder no longer places them.

## Conventions seen in the code

- One public class per file, file name equal to the class name. Exceptions that hold several small types: `Damage.cs`, `ForestData.cs`.
- Small private nested enums named `State`, `Mode` and so on are reused inside different classes. That is fine because they are nested; do not make a top-level type with one of those names.
- Setup menus are written to be safe to run again. Keep that true: re-running a setup must not change existing sprite IDs or break references.
- Enemies derive from `EnemyBase` (`Assets/Project/Scripts/Mine/EnemyBase.cs`) and animate by swapping `Sprite[]` frame lists, not with Animator clips.
- Sprite sheet cells are named `<sheet>_<column>_<row>`, with row 0 at the top.

## Checking work without Unity

- Unity writes its log to `Logs/Editor.log` inside the project. Compile errors appear there as `error CS....`.
- Sprite references can be checked by hand: a reference like `{fileID: N, guid: G}` in a prefab must match an `internalID: N` in the `.png.meta` whose `guid` is `G`.
