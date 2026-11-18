using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Interior Decor
///
/// 1. Reads Assets/Project/Art/Interior/decor_manifest.txt (written by Tools/make_interior_art.ps1) and sets every picture up as a
///    pixel-art sprite (16 pixels per tile, Point filter, no compression).
/// 2. Makes one DecorItem per catalogue piece in Assets/Farm/Interiors/Items, the floor / wall tiles in Assets/Farm/Interiors/Tiles,
///    and the catalogue Assets/Farm/Interiors/DecorCatalog.asset. Items that already exist KEEP their name, description, price
///    and "Blocks Player" (only their pictures and sizes are refreshed), so your edits stay.
/// 3. Adds the new floors and wall coverings as options at the farmhouse design table (and Frank's store) if a HouseManager is in the open scene.
/// 4. Furnishes the rooms in the open scene: the farmhouse, the Kims' apartment and the suburb houses. A room that already has a
///    layout asset (Assets/Farm/Interiors/Layouts) is rebuilt from it; others get the starting layout from InteriorDecorLayouts.cs.
///
/// Safe to run again. The scene builders (Suburbs Scene, General Store, Build Farmhouse) call Build() themselves.
/// This file must live in a folder named "Editor".
/// </summary>
public static class InteriorDecorSetupMenu
{
    private const string ItemsFolder = InteriorDecorTools.Root + "/Items";
    private const string TilesFolder = InteriorDecorTools.Root + "/Tiles";
    private const string UpgradeFolder = "Assets/Farm/House/Data/Upgrades";
    private static readonly string[] Faces = { "front", "right", "back", "left" };

    [MenuItem("Farm/Set Up/Interior Decor", false, 125)]
    public static void RunFromMenu()
    {
        string assets = EnsureAssets();
        if (!assets.StartsWith("Made")) { EditorUtility.DisplayDialog("Interior decor", assets, "OK"); return; }

        var rooms = FindRooms();
        string upgrades = AddHouseUpgrades();
        string furnish = "";
        if (rooms.Count > 0 && EditorUtility.DisplayDialog("Furnish the rooms?",
                "Found " + rooms.Count + " room(s) in this scene: " + string.Join(", ", rooms.Select(r => r.Key).ToArray()) + ".\n\n" +
                "Furnish them now? Rooms with a saved layout are rebuilt from it; the others get the starting layout. " +
                "The old plain apartment furniture (Apt_...) in those rooms is removed. Ctrl+Z undoes it.", "Furnish", "Not now"))
            furnish = "\n\n" + FurnishRooms(rooms);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Interior decor", assets + (upgrades.Length > 0 ? "\n\n" + upgrades : "") + furnish +
            "\n\nOpen Farm > Level Editors > Interior Decorator to move, turn and swap things.", "OK");
    }

    /// <summary>Everything without pop-ups: assets, design-table options, furnishing the open scene's rooms. Returns a report.</summary>
    public static string Build()
    {
        string assets = EnsureAssets();
        if (!assets.StartsWith("Made")) return assets;
        return assets + " " + AddHouseUpgrades() + " " + FurnishRooms(FindRooms());
    }

    // ------------------------------------------------------------------ command line

    private static readonly string[] BatchScenes = { "Assets/Project/Scenes/Farm.unity", "Assets/Project/Scenes/Suburbs.unity" };

    /// <summary>
    /// Command line: -batchmode -quit -executeMethod InteriorDecorSetupMenu.BuildBatch (leave out -nographics to get pictures).
    /// Makes the assets, furnishes the rooms of the Farm and Suburbs scenes, saves them, and saves a picture of every room to Logs/interior_&lt;room&gt;.png.
    /// </summary>
    public static void BuildBatch()
    {
        foreach (string path in BatchScenes)
        {
            if (!File.Exists(path)) { Debug.LogWarning("DECOR| no scene " + path); continue; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Debug.Log("DECOR| " + path + ": " + Build());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            SaveRoomPictures();
        }
    }

    /// <summary>Command line: only the pictures (Logs/interior_&lt;room&gt;.png), nothing is changed.</summary>
    public static void PreviewBatch()
    {
        foreach (string path in BatchScenes)
        {
            if (!File.Exists(path)) continue;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            SaveRoomPictures();
        }
    }

    private static void SaveRoomPictures()
    {
        UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
        foreach (DecorRoom room in UnityEngine.Object.FindObjectsByType<DecorRoom>(FindObjectsInactive.Exclude))
        {
            try
            {
                var hi = room.GetComponent<HouseInterior>();
                float W = hi.width + 1f, H = hi.floorRows + hi.wallRows + 1f;
                var camGo = new GameObject("DecorPreviewCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = H * 0.5f; cam.aspect = W / H;
                cam.transform.position = room.transform.position + new Vector3(hi.width * 0.5f, (hi.floorRows + hi.wallRows) * 0.5f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                cam.transparencySortMode = TransparencySortMode.CustomAxis; cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
                var rt = new RenderTexture(Mathf.RoundToInt(W * 32), Mathf.RoundToInt(H * 32), 24);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/interior_" + InteriorDecorTools.Safe(room.roomId) + ".png", tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
                Debug.Log("DECOR| picture Logs/interior_" + room.roomId + ".png (" + room.PiecesRoot.childCount + " pieces)");
            }
            catch (Exception e) { Debug.LogWarning("DECOR| no picture for " + room.roomId + ": " + e.Message); }
        }
    }

    // ------------------------------------------------------------------ rooms in the open scene

    /// <summary>The rooms this menu knows how to furnish: room id -> (interior, starting layout, theme, floor from HouseManager).</summary>
    private static List<KeyValuePair<string, Action>> FindRooms()
    {
        var list = new List<KeyValuePair<string, Action>>();
        foreach (HouseInterior h in UnityEngine.Object.FindObjectsByType<HouseInterior>(FindObjectsInactive.Include))
        {
            HouseInterior hi = h;
            if (hi.name == "HouseInterior")
                list.Add(new KeyValuePair<string, Action>("Farmhouse", () => Report(InteriorDecorTools.Furnish(hi, "Farmhouse", "cottage", InteriorDecorLayouts.Farmhouse, true))));
            else if (hi.name == "ApartmentInterior")
                list.Add(new KeyValuePair<string, Action>("KimApartment", () => Report(InteriorDecorTools.Furnish(hi, "KimApartment", "modern", InteriorDecorLayouts.KimApartment, false))));
            else if (hi.name.StartsWith("SuburbHouseInterior_"))
            {
                int n; if (!int.TryParse(hi.name.Substring("SuburbHouseInterior_".Length), out n)) continue;
                Action<LayoutBuilder> layout = SuburbLayoutFor(hi);
                if (layout == null) continue;
                list.Add(new KeyValuePair<string, Action>("H" + n, () => Report(InteriorDecorTools.Furnish(hi, "H" + n, InteriorDecorLayouts.ThemeForHouse(n), layout, false))));
            }
        }
        return list;
    }

    /// <summary>Works out which suburb layout an existing house uses, from its size (Cosy 14 wide, Family 20, Loft 12).</summary>
    public static Action<LayoutBuilder> SuburbLayoutFor(HouseInterior hi)
    {
        switch (hi.width)
        {
            case 14:
            {
                var style = hi.GetComponent<InteriorStyle>();
                bool mirror = false;
                if (style != null && style.zones != null) foreach (FloorZone z in style.zones) if (z != null && z.label == "Kitchen" && z.area.x > 0) mirror = true;
                return b => InteriorDecorLayouts.Cosy(b, mirror);
            }
            case 20: return InteriorDecorLayouts.Family;
            case 12: return InteriorDecorLayouts.Loft;
        }
        return null;
    }

    private static readonly List<string> reports = new List<string>();
    private static void Report(string s) { reports.Add(s); }

    private static string FurnishRooms(List<KeyValuePair<string, Action>> rooms)
    {
        reports.Clear();
        Physics2D.SyncTransforms();
        foreach (var r in rooms) r.Value();
        AssetDatabase.SaveAssets();
        return string.Join("\n", reports.ToArray());
    }

    // ------------------------------------------------------------------ assets

    /// <summary>True when the catalogue exists or could be made from the art.</summary>
    public static bool Ready()
    {
        DecorCatalog c = InteriorDecorTools.Catalog;
        if (c != null && c.items.Count > 0) return true;
        return EnsureAssets().StartsWith("Made");
    }

    /// <summary>Imports the art and makes / refreshes the DecorItems, tiles and catalogue. Returns a sentence starting with "Made" on success.</summary>
    public static string EnsureAssets()
    {
        string manifestPath = InteriorDecorTools.ArtFolder + "/decor_manifest.txt";
        string full = Path.Combine(Directory.GetCurrentDirectory(), manifestPath);
        if (!File.Exists(full))
            return "Couldn't find " + manifestPath + ". Run Tools/make_interior_art.ps1 first (see the top of that file).";

        AssetDatabase.Refresh();
        string[] lines = File.ReadAllLines(full);
        InteriorDecorTools.EnsureFolder(ItemsFolder);
        InteriorDecorTools.EnsureFolder(TilesFolder);

        // 1. picture import settings
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string line in lines)
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                string[] f = line.Split('\t');
                switch (f[0])
                {
                    case "piece":
                    {
                        bool wall = f[5] == "Wall";
                        foreach (string face in wall ? new[] { "front" } : Faces)
                            ImportPicture(PiecePath(f[3], f[1], face), wall ? -1 : int.Parse(f[10]), wall ? -1 : int.Parse(f[11]), SpriteAlignment.BottomLeft, false);
                        break;
                    }
                    case "floor": ImportPicture(InteriorDecorTools.ArtFolder + "/Flooring/" + f[1] + ".png", -1, -1, SpriteAlignment.Center, false); break;
                    case "wall":
                        foreach (string part in new[] { "_top", "_upper", "_base" })
                            ImportPicture(InteriorDecorTools.ArtFolder + "/WallCovering/" + f[1] + part + ".png", -1, -1, SpriteAlignment.Center, false);
                        break;
                }
            }
            foreach (string room in new[] { "room_cap_h", "room_cap_v", "room_cap_corner", "room_wall_shadow", "room_side_shadow" })
                ImportPicture(InteriorDecorTools.ArtFolder + "/Room/" + room + ".png", -1, -1, SpriteAlignment.BottomLeft, true);
        }
        finally { AssetDatabase.StopAssetEditing(); }

        // 2. items, tiles, catalogue
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(InteriorDecorTools.CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<DecorCatalog>();
            AssetDatabase.CreateAsset(catalog, InteriorDecorTools.CatalogPath);
        }
        int made = 0, refreshed = 0, missingPics = 0;
        foreach (string line in lines)
        {
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;
            string[] f = line.Split('\t');
            DecorItem item = null; bool isNew = false;
            switch (f[0])
            {
                case "piece":
                {
                    DecorCategory cat;
                    if (!Enum.TryParse(f[3], out cat)) { Debug.LogWarning("Interior decor: unknown category " + f[3]); continue; }
                    item = LoadOrCreateItem(cat.ToString(), f[1], out isNew);
                    if (isNew) { item.displayName = f[2]; item.description = f[13]; item.price = int.Parse(f[8]); }
                    item.id = f[1]; item.category = cat; item.theme = f[4];
                    DecorMount mount; Enum.TryParse(f[5], out mount); item.mount = mount;
                    if (isNew) item.blocksPlayer = mount == DecorMount.Floor;
                    item.width = int.Parse(f[6]); item.depth = int.Parse(f[7]); item.surfaceHeight = int.Parse(f[9]);
                    bool wall = mount == DecorMount.Wall;
                    item.front = Spr(PiecePath(f[3], f[1], "front"));
                    item.right = wall ? null : Spr(PiecePath(f[3], f[1], "right"));
                    item.back = wall ? null : Spr(PiecePath(f[3], f[1], "back"));
                    item.left = wall ? null : Spr(PiecePath(f[3], f[1], "left"));
                    if (item.front == null) missingPics++;
                    break;
                }
                case "floor":
                    item = LoadOrCreateItem("Flooring", f[1], out isNew);
                    if (isNew) { item.displayName = f[2]; item.description = f[5]; item.price = int.Parse(f[4]); }
                    item.id = f[1]; item.category = DecorCategory.Flooring; item.theme = f[3]; item.mount = DecorMount.Flat; item.blocksPlayer = false;
                    item.front = Spr(InteriorDecorTools.ArtFolder + "/Flooring/" + f[1] + ".png");
                    item.floorTile = MakeTile(f[1], item.front);
                    if (item.front == null) missingPics++;
                    break;
                case "wall":
                    item = LoadOrCreateItem("WallCovering", f[1], out isNew);
                    if (isNew) { item.displayName = f[2]; item.description = f[5]; item.price = int.Parse(f[4]); }
                    item.id = f[1]; item.category = DecorCategory.WallCovering; item.theme = f[3]; item.mount = DecorMount.Wall; item.blocksPlayer = false;
                    Sprite top = Spr(InteriorDecorTools.ArtFolder + "/WallCovering/" + f[1] + "_top.png");
                    Sprite upper = Spr(InteriorDecorTools.ArtFolder + "/WallCovering/" + f[1] + "_upper.png");
                    Sprite bottom = Spr(InteriorDecorTools.ArtFolder + "/WallCovering/" + f[1] + "_base.png");
                    item.front = upper;
                    item.wallTop = MakeTile(f[1] + "_top", top); item.wallUpper = MakeTile(f[1] + "_upper", upper); item.wallBase = MakeTile(f[1] + "_base", bottom);
                    if (upper == null) missingPics++;
                    break;
                default: continue;
            }
            EditorUtility.SetDirty(item);
            if (isNew) made++; else refreshed++;
            if (!catalog.items.Contains(item)) catalog.items.Add(item);
        }
        catalog.items.RemoveAll(i => i == null);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        return "Made " + made + " new decor items and refreshed " + refreshed + " (catalogue: " + catalog.items.Count + " items in " + InteriorDecorTools.CatalogPath + ")." +
               (missingPics > 0 ? " " + missingPics + " item(s) have no picture: run Tools/make_interior_art.ps1 again." : "");
    }

    private static string PiecePath(string category, string id, string face)
    {
        return InteriorDecorTools.ArtFolder + "/" + category + "/" + id + "_" + face + ".png";
    }

    private static Sprite Spr(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }

    private static DecorItem LoadOrCreateItem(string folder, string id, out bool isNew)
    {
        string dir = ItemsFolder + "/" + folder;
        InteriorDecorTools.EnsureFolder(dir);
        string path = dir + "/" + id + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<DecorItem>(path);
        isNew = item == null;
        if (isNew) { item = ScriptableObject.CreateInstance<DecorItem>(); AssetDatabase.CreateAsset(item, path); }
        return item;
    }

    private static TileBase MakeTile(string name, Sprite sprite)
    {
        string path = TilesFolder + "/decor_" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    /// <summary>
    /// Pixel-art import: Single sprite (its ID never changes), 16 pixels per unit, Point, no compression, no mipmaps.
    /// pivotX / pivotY in pixels from the bottom-left (or -1 to use 'align'). Only reimports when something differs.
    /// </summary>
    private static void ImportPicture(string path, int pivotX, int pivotY, SpriteAlignment align, bool fullRect)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);

        Vector2 pivot = s.spritePivot; int alignment = (int)align;
        if (pivotX >= 0)
        {
            Vector2Int size = PngSize(path);
            if (size.x > 0) { pivot = new Vector2(pivotX / (float)size.x, pivotY / (float)size.y); alignment = (int)SpriteAlignment.Custom; }
        }
        SpriteMeshType mesh = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;

        bool changed = imp.textureType != TextureImporterType.Sprite || s.spriteMode != (int)SpriteImportMode.Single ||
                       !Mathf.Approximately(s.spritePixelsPerUnit, 16f) || s.filterMode != FilterMode.Point || s.mipmapEnabled ||
                       imp.textureCompression != TextureImporterCompression.Uncompressed || s.spriteAlignment != alignment ||
                       (alignment == (int)SpriteAlignment.Custom && (s.spritePivot - pivot).sqrMagnitude > 1e-8f) || s.spriteMeshType != mesh ||
                       !s.alphaIsTransparency;
        if (!changed) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        s.textureType = TextureImporterType.Sprite;
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = 16f;
        s.filterMode = FilterMode.Point;
        s.mipmapEnabled = false;
        s.alphaIsTransparency = true;
        s.spriteAlignment = alignment;
        s.spritePivot = pivot;
        s.spriteMeshType = mesh;
        s.wrapMode = TextureWrapMode.Clamp;
        imp.SetTextureSettings(s);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    /// <summary>Reads a PNG's width and height straight from its header (so we know them before Unity imports it).</summary>
    private static Vector2Int PngSize(string assetPath)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(full)) return Vector2Int.zero;
        var b = new byte[24];
        using (FileStream fs = File.OpenRead(full)) { if (fs.Read(b, 0, 24) < 24) return Vector2Int.zero; }
        int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return new Vector2Int(w, h);
    }

    // ------------------------------------------------------------------ design-table options

    /// <summary>
    /// Makes a HouseUpgradeData for every decor floor and wall covering (Floor_Decor_... / Wall_Decor_..., KEPT if they exist)
    /// and adds them to every HouseManager in the open scene, so they can be bought for the farmhouse.
    /// </summary>
    public static string AddHouseUpgrades()
    {
        var managers = UnityEngine.Object.FindObjectsByType<HouseManager>(FindObjectsInactive.Include);
        if (managers.Length == 0) return "";
        DecorCatalog catalog = InteriorDecorTools.Catalog;
        if (catalog == null) return "";
        InteriorDecorTools.EnsureFolder(UpgradeFolder);

        var options = new List<HouseUpgradeData>();
        foreach (DecorItem item in catalog.items)
        {
            if (item == null || !item.IsSurface) continue;
            bool floor = item.category == DecorCategory.Flooring;
            string path = UpgradeFolder + "/" + (floor ? "Floor" : "Wall") + "_Decor_" + item.id + ".asset";
            var o = AssetDatabase.LoadAssetAtPath<HouseUpgradeData>(path);
            if (o == null)
            {
                o = ScriptableObject.CreateInstance<HouseUpgradeData>();
                o.displayName = item.displayName; o.description = item.description; o.goldCost = item.price; o.isDefault = false;
                AssetDatabase.CreateAsset(o, path);
            }
            o.category = floor ? HouseCategory.Floor : HouseCategory.Wall;
            o.tile = floor ? item.floorTile : item.wallUpper;
            o.tileBase = floor ? null : item.wallBase;
            o.tileTop = floor ? null : item.wallTop;
            EditorUtility.SetDirty(o);
            options.Add(o);
        }

        int added = 0;
        foreach (HouseManager m in managers)
        {
            var so = new SerializedObject(m);
            SerializedProperty list = so.FindProperty("catalog");
            var have = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < list.arraySize; i++) have.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (HouseUpgradeData o in options)
            {
                if (have.Contains(o)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = o;
                added++;
            }
            so.ApplyModifiedProperties();
        }
        AssetDatabase.SaveAssets();
        return added > 0 ? "Added " + added + " floor / wall options to the house design table." : "";
    }
}
