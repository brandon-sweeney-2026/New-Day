using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Sets up the 4 growing trees. Menu: Farm > Trees > Set Up Trees In Scene.
///
/// Before running it, copy the "Trees" folder (the PNGs) anywhere into your project (e.g. Assets/Farm/Trees).
/// It then:
///   - cuts each tree sheet into 16 pictures (4 growth stages x 4 seasons) with the pivot at the trunk base,
///   - creates the items (Log, Acorn, Apple, Pine Cone, Maple Syrup) and 4 Tree assets (Oak, Apple, Pine, Maple),
///   - adds a TreeManager to your scene and fills in the ground tiles trees may grow on (Grass and DirtFlat),
///     the tilemaps to avoid, and the places to keep clear (the farmhouse and the player).
///
/// Safe to run again: assets are reused and the manager is only refreshed. You can change every setting afterwards.
/// This file must live in a folder named "Editor".
/// </summary>
public static class TreeSetupMenu
{
    private const string DataRoot = "Assets/Farm/Trees/Data";
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    private const int CellW = 32, CellH = 48;
    private const float PixelsPerUnit = 16f;

    // Names of the ground tiles trees may grow on. Change them in the TreeManager Inspector afterwards if you like.
    private static readonly string[] GroundTileNames = { "Grass", "DirtFlat" };
    // Tilemaps that block sprouting (matched by name, case-insensitive, partial match).
    private static readonly string[] BlockedMapHints = { "path", "soil", "wet", "fence", "pollution", "water", "road" };

    private static string artFolder;

    [MenuItem("Farm/Trees/Set Up Trees In Scene")]
    public static void Build()
    {
        string[] found = AssetDatabase.FindAssets("tree_oak t:Texture2D");
        if (found.Length == 0)
        {
            EditorUtility.DisplayDialog("Tree art not found",
                "Copy the 'Trees' folder from the zip into your project (for example Assets/Farm/Trees) first, then run this again.", "OK");
            return;
        }
        artFolder = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(found[0])).Replace('\\', '/');

        EnsureFolder(DataRoot);
        EnsureFolder(ItemsFolder);

        ImportIcons();
        foreach (string name in new[] { "tree_oak", "tree_apple", "tree_pine", "tree_maple" })
            SliceSheet(artFolder + "/" + name + ".png");

        // ---- items ----
        ItemData log = MakeItem("Log", "Log", "Sturdy timber from a felled tree.", ItemData.ItemType.Resource, 0, 8, "icon_log", 99);
        ItemData acorn = MakeItem("Acorn", "Acorn", "A nut from the oak tree.", ItemData.ItemType.Crop, 0, 6, "icon_acorn", 99);
        ItemData apple = MakeItem("Apple", "Apple", "Crisp and sweet. Great in cooking.", ItemData.ItemType.Crop, 0, 14, "icon_apple", 99);
        ItemData cone = MakeItem("PineCone", "Pine Cone", "Smells like a forest.", ItemData.ItemType.Resource, 0, 5, "icon_pine_cone", 99);
        ItemData syrup = MakeItem("MapleSyrup", "Maple Syrup", "Thick, golden and delicious.", ItemData.ItemType.Crop, 0, 30, "icon_maple_syrup", 99);

        ItemData oakSeed = MakeItem("AcornSeed", "Oak Sapling", "Plant it to grow an oak tree.", ItemData.ItemType.Seed, 0, 10, "icon_acorn", 99);
        ItemData appleSeed = MakeItem("AppleSeed", "Apple Seed", "Plant it to grow an apple tree.", ItemData.ItemType.Seed, 0, 10, "icon_apple", 99);
        ItemData pineSeed = MakeItem("PineSeed", "Pine Seed", "Plant it to grow a pine tree.", ItemData.ItemType.Seed, 0, 10, "icon_pine_cone", 99);
        ItemData mapleSeed = MakeItem("MapleSeed", "Maple Seed", "Plant it to grow a maple tree.", ItemData.ItemType.Seed, 0, 10, "icon_maple_syrup", 99);

        // ---- trees ----  (drops are in order: Spring, Summer, Autumn, Winter)
        TreeData oak = MakeTree("Oak", "Oak", "tree_oak", log, 1.2f, new[] { 1, 2, 4, 6 }, new[] { 0, 1, 3, 5 },
            Drops(null, null, D(acorn, 1, 3, 0.4f), null));
        TreeData appleTree = MakeTree("AppleTree", "Apple Tree", "tree_apple", log, 1f, new[] { 1, 2, 3, 5 }, new[] { 0, 1, 2, 3 },
            Drops(null, D(apple, 1, 2, 0.3f), D(apple, 1, 3, 0.45f), null));
        TreeData pine = MakeTree("Pine", "Pine", "tree_pine", log, 1.2f, new[] { 1, 2, 4, 6 }, new[] { 0, 1, 3, 5 },
            Drops(null, null, D(cone, 1, 2, 0.35f), D(cone, 1, 2, 0.25f)));
        TreeData maple = MakeTree("Maple", "Maple", "tree_maple", log, 0.8f, new[] { 1, 2, 4, 6 }, new[] { 0, 1, 3, 5 },
            Drops(D(syrup, 1, 1, 0.25f), null, null, null));

        oak.seedItem = oakSeed; appleTree.seedItem = appleSeed; pine.seedItem = pineSeed; maple.seedItem = mapleSeed;
        foreach (TreeData td in new[] { oak, appleTree, pine, maple }) EditorUtility.SetDirty(td);

        AssetDatabase.SaveAssets();

        // ---- scene ----
        var manager = Object.FindAnyObjectByType<TreeManager>();
        bool created = false;
        if (manager == null)
        {
            manager = new GameObject("TreeManager").AddComponent<TreeManager>();
            Undo.RegisterCreatedObjectUndo(manager.gameObject, "Create TreeManager");
            created = true;
        }

        var species = new List<Object> { oak, appleTree, pine, maple };
        SetArray(manager, "species", species);

        // Ground tiles: look up by exact asset name.
        var ground = new List<Object>();
        var missing = new List<string>();
        foreach (string tileName in GroundTileNames)
        {
            TileBase t = FindTile(tileName);
            if (t != null) ground.Add(t); else missing.Add(tileName);
        }
        SetArray(manager, "allowedGround", ground);

        // Ground tilemap: the one FarmManager uses, else one named like "Ground".
        var farm = Object.FindAnyObjectByType<FarmManager>();
        Tilemap groundMap = null;
        if (farm != null) groundMap = farm.GroundTilemap;
        if (groundMap == null)
            groundMap = Object.FindObjectsByType<Tilemap>()
                .FirstOrDefault(m => m.name.ToLower().Contains("ground"));
        if (groundMap != null) SetRef(manager, "groundTilemap", groundMap);

        // Tilemaps that block sprouting.
        var blocked = new List<Object>();
        foreach (Tilemap m in Object.FindObjectsByType<Tilemap>())
        {
            if (m == groundMap) continue;
            string n = m.name.ToLower();
            if (BlockedMapHints.Any(h => n.Contains(h))) blocked.Add(m);
        }
        SetArray(manager, "blockedTilemaps", blocked);

        // Keep clear: the farmhouse and the player.
        var keep = new List<Object>();
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) keep.Add(player.transform);
        var houseObj = GameObject.Find("Farmhouse");
        if (houseObj != null) keep.Add(houseObj.transform);
        SetArray(manager, "keepClearAround", keep);

        EditorUtility.SetDirty(manager);
        if (!created) { /* refreshed only */ }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(created ? "TreeManager added to the scene." : "TreeManager refreshed.");
        sb.AppendLine("Trees: Oak, Apple Tree, Pine, Maple (in " + DataRoot + ").");
        sb.AppendLine("Can grow on: " + (ground.Count > 0 ? string.Join(", ", ground.Select(g => g.name).ToArray()) : "(nothing yet)"));
        if (missing.Count > 0)
            sb.AppendLine("\nCould not find a tile asset named: " + string.Join(", ", missing.ToArray()) +
                ".\nDrag the tiles you want into 'Allowed Ground' on the TreeManager.");
        if (groundMap == null)
            sb.AppendLine("\nNo ground Tilemap found. Drag it into 'Ground Tilemap' on the TreeManager.");
        sb.AppendLine("\nPress Play: trees appear and grow each new day.");

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Trees ready", sb.ToString(), "OK");
        Selection.activeObject = manager.gameObject;
    }

    // ------------------------------------------------------------------
    // Art
    // ------------------------------------------------------------------

    private static void ImportIcons()
    {
        foreach (string guid in AssetDatabase.FindAssets("icon_ t:Texture2D", new[] { artFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureType = TextureImporterType.Sprite;
            ts.spriteMode = (int)SpriteImportMode.Single;
            ts.spriteAlignment = (int)SpriteAlignment.Center;
            ts.spritePixelsPerUnit = PixelsPerUnit;
            ts.filterMode = FilterMode.Point;
            ts.mipmapEnabled = false;
            imp.SetTextureSettings(ts);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    /// <summary>Cuts a 128x192 sheet into 32x48 cells named name_column_row (row counted from the top).</summary>
    private static void SliceSheet(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width / CellW;
        int rows = tex.height / CellH;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = baseName + "_" + c + "_" + r;
                rect.rect = new Rect(c * CellW, tex.height - (r + 1) * CellH, CellW, CellH);
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = new Vector2(0.5f, 0f);   // bottom centre = the trunk base
                rect.spriteID = GUID.Generate();
                rects.Add(rect);
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        idProvider.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------
    // Assets
    // ------------------------------------------------------------------

    private static ItemData MakeItem(string asset, string name, string desc, ItemData.ItemType type, int buy, int sell, string icon, int stack)
    {
        var it = LoadOrCreate<ItemData>(ItemsFolder + "/" + asset + ".asset");
        it.itemName = name; it.description = desc; it.type = type;
        it.buyPrice = buy; it.sellPrice = sell; it.maxStack = stack;
        it.icon = AssetDatabase.LoadAssetAtPath<Sprite>(artFolder + "/" + icon + ".png");
        EditorUtility.SetDirty(it);
        return it;
    }

    private static TreeData.SeasonDrop D(ItemData item, int min, int max, float chance)
    {
        return new TreeData.SeasonDrop { item = item, minAmount = min, maxAmount = max, chancePerDay = chance };
    }

    private static TreeData.SeasonDrop[] Drops(TreeData.SeasonDrop spring, TreeData.SeasonDrop summer,
        TreeData.SeasonDrop autumn, TreeData.SeasonDrop winter)
    {
        return new[]
        {
            spring ?? new TreeData.SeasonDrop(), summer ?? new TreeData.SeasonDrop(),
            autumn ?? new TreeData.SeasonDrop(), winter ?? new TreeData.SeasonDrop()
        };
    }

    private static TreeData MakeTree(string asset, string name, string sheet, ItemData log, float weight,
        int[] hits, int[] logs, TreeData.SeasonDrop[] drops)
    {
        var t = LoadOrCreate<TreeData>(DataRoot + "/" + asset + ".asset");
        t.treeName = name;
        t.spawnWeight = weight;
        t.logItem = log;
        t.hitsByStage = hits;
        t.logsByStage = logs;
        t.seasonDrops = drops;

        // 16 sprites: season*4 + stage. Sheet columns are stages, rows are seasons.
        var all = AssetDatabase.LoadAllAssetsAtPath(artFolder + "/" + sheet + ".png").OfType<Sprite>()
            .ToDictionary(s => s.name, s => s);
        t.sprites = new Sprite[16];
        for (int season = 0; season < 4; season++)
            for (int stage = 0; stage < 4; stage++)
            {
                Sprite s;
                all.TryGetValue(sheet + "_" + stage + "_" + season, out s);
                t.sprites[season * 4 + stage] = s;
            }

        EditorUtility.SetDirty(t);
        return t;
    }

    private static TileBase FindTile(string exactName)
    {
        foreach (string guid in AssetDatabase.FindAssets(exactName + " t:TileBase"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == exactName)
                return AssetDatabase.LoadAssetAtPath<TileBase>(p);
        }
        return null;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Field not found: " + field + " on " + target.GetType().Name); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetArray(Object target, string field, List<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Field not found: " + field + " on " + target.GetType().Name); return; }
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
