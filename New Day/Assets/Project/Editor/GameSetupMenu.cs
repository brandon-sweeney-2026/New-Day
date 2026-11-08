using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click setup for the new game systems. Menu: Farm > Game > Set Up Game Systems In Scene.
///
/// It is safe to run more than once: anything that already exists is reused, not duplicated.
/// It adds to your open scene: TimeManager, PollutionMap (+ a Pollution tilemap), CropManager, GameHUD,
/// Camera follow, and on the Player: Inventory, PlayerStats and PlayerActions.
/// It also creates the Pollution Settings asset, 5 pollution tiles, a sword, two spells, and a bed + shipping bin to start with.
///
/// This file must live in a folder named "Editor".
/// </summary>
public static class GameSetupMenu
{
    private const string DataFolder = "Assets/Farm/Data";
    private const string ItemsFolder = "Assets/Farm/Data/Items";
    private const string PollutionFolder = "Assets/Farm/Pollution";

    [MenuItem("Farm/Game/Set Up Game Systems In Scene")]
    public static void SetUp()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("No player found",
                "Add your Player (with a PlayerController) to the scene first.\n\nTip: Farm > Kiln Harvest > 2. Create Farm Scene Layout builds a whole scene.", "OK");
            return;
        }

        var log = new List<string>();
        var farm = Object.FindAnyObjectByType<FarmManager>();
        if (farm == null) log.Add("WARNING: no FarmManager in the scene - crops and time cannot work without it.");

        EnsureFolder(DataFolder);
        EnsureFolder(ItemsFolder);
        EnsureFolder(PollutionFolder);

        PollutionSettings settings = EnsurePollutionSettings();
        TileBase[] pollutionTiles = EnsurePollutionTiles(settings);
        List<ItemData> items = EnsureItems(log);

        SetUpPlayer(player.gameObject, items, log);
        SetUpTime(farm, log);
        SetUpPollution(farm, settings, pollutionTiles, log);
        SetUpCrops(farm, log);
        SetUpHud(log);
        SetUpCamera(player.transform, farm, log);
        SetUpWorldObjects(player.transform, log);

        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Game systems set up:\n - " + string.Join("\n - ", log));
        EditorUtility.DisplayDialog("Done", "Game systems are set up. Press Play!\n\nSee the Console for the details of what was added.", "OK");
    }

    // ------------------------------------------------------------------
    // Player
    // ------------------------------------------------------------------

    private static void SetUpPlayer(GameObject player, List<ItemData> items, List<string> log)
    {
        Inventory inv = AddOnce<Inventory>(player, log);
        AddOnce<PlayerStats>(player, log);
        AddOnce<PlayerActions>(player, log);
        AddOnce<HeldItemVisual>(player, log);

        // The old controllers are replaced by PlayerActions. Disabled, not deleted, so nothing is lost.
        foreach (MonoBehaviour b in player.GetComponents<MonoBehaviour>())
        {
            if (b == null) continue;
            string n = b.GetType().Name;
            if ((n == "FarmToolController" || n == "PlayerAttack") && b.enabled)
            {
                Undo.RecordObject(b, "Disable old controller");
                b.enabled = false;
                log.Add("Disabled old " + n + " (PlayerActions replaces it). You can remove it.");
            }
        }

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null && rb.interpolation != RigidbodyInterpolation2D.Interpolate)
        {
            Undo.RecordObject(rb, "Interpolate");
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;   // smooth camera follow
        }

        // Starting inventory
        var so = new SerializedObject(inv);
        SerializedProperty list = so.FindProperty("startingItems");
        if (list.arraySize == 0)
        {
            var wanted = new List<ItemData>();
            ItemData.ToolAction[] order =
            {
                ItemData.ToolAction.Hoe, ItemData.ToolAction.WateringCan, ItemData.ToolAction.Sickle,
                ItemData.ToolAction.Axe, ItemData.ToolAction.Pickaxe, ItemData.ToolAction.Shovel
            };
            foreach (var action in order)
                foreach (ItemData it in items)
                    if (it.type == ItemData.ItemType.Tool && it.toolAction == action) { wanted.Add(it); break; }

            foreach (ItemData it in items) if (it.type == ItemData.ItemType.Weapon) { wanted.Add(it); break; }
            foreach (ItemData it in items) if (it.type == ItemData.ItemType.Spell && it.spellEffect == ItemData.SpellEffect.Projectile) { wanted.Add(it); break; }
            foreach (ItemData it in items) if (it.type == ItemData.ItemType.Spell && it.spellEffect == ItemData.SpellEffect.Heal) { wanted.Add(it); break; }

            int seeds = 0;
            foreach (ItemData it in items)
            {
                if (it.type != ItemData.ItemType.Seed || seeds >= 3) continue;
                wanted.Add(it);
                seeds++;
            }

            list.arraySize = wanted.Count;
            for (int i = 0; i < wanted.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("item").objectReferenceValue = wanted[i];
                e.FindPropertyRelative("count").intValue = wanted[i].type == ItemData.ItemType.Seed ? 10 : 1;
            }
            so.ApplyModifiedProperties();
            log.Add("Gave the player " + wanted.Count + " starting items (edit them on the Inventory component).");
        }
    }

    // ------------------------------------------------------------------
    // Scene systems
    // ------------------------------------------------------------------

    private static void SetUpTime(FarmManager farm, List<string> log)
    {
        TimeManager time = GetOrCreate<TimeManager>("TimeManager", log);
        if (farm != null) SetRef(time, "farm", farm);
    }

    private static void SetUpPollution(FarmManager farm, PollutionSettings settings, TileBase[] tiles, List<string> log)
    {
        PollutionMap map = GetOrCreate<PollutionMap>("PollutionMap", log);

        Tilemap pollutionTilemap = null;
        foreach (Tilemap t in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
            if (t.name.ToLowerInvariant().Contains("pollution")) { pollutionTilemap = t; break; }

        if (pollutionTilemap == null)
        {
            Transform grid = null;
            foreach (Tilemap t in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
                if (t.name.ToLowerInvariant().Contains("ground")) { grid = t.transform.parent; break; }

            var go = new GameObject("Pollution", typeof(Tilemap), typeof(TilemapRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Create Pollution tilemap");
            if (grid != null) go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = 3;
            pollutionTilemap = go.GetComponent<Tilemap>();
            log.Add("Created a 'Pollution' tilemap (paint it with the Pollution layer of the Level Editor).");
        }

        var so = new SerializedObject(map);
        so.FindProperty("pollutionTilemap").objectReferenceValue = pollutionTilemap;
        so.FindProperty("settings").objectReferenceValue = settings;
        SerializedProperty arr = so.FindProperty("levelTiles");
        arr.arraySize = PollutionSettings.MaxLevel;
        for (int i = 0; i < PollutionSettings.MaxLevel; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
        so.ApplyModifiedProperties();
    }

    private static void SetUpCrops(FarmManager farm, List<string> log)
    {
        CropManager cm = GetOrCreate<CropManager>("CropManager", log);
        if (farm != null) SetRef(cm, "farm", farm);

        var all = new List<CropData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
            all.Add(AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid)));

        var so = new SerializedObject(cm);
        SerializedProperty arr = so.FindProperty("crops");
        arr.arraySize = all.Count;
        for (int i = 0; i < all.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        so.ApplyModifiedProperties();
        log.Add("CropManager knows " + all.Count + " crops.");
    }

    private static void SetUpHud(List<string> log)
    {
        GetOrCreate<GameHUD>("GameHUD", log);
    }

    private static void SetUpCamera(Transform player, FarmManager farm, List<string> log)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            log.Add("WARNING: no camera tagged MainCamera, so camera follow was not added.");
            return;
        }

        var follow = cam.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = Undo.AddComponent<CameraFollow2D>(cam.gameObject);
            log.Add("Added CameraFollow2D to the camera.");
        }

        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;

        foreach (Tilemap t in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (t.name.ToLowerInvariant().Contains("ground"))
            {
                so.FindProperty("boundsTilemap").objectReferenceValue = t;
                so.FindProperty("limitToMap").boolValue = true;
                break;
            }
        }
        so.ApplyModifiedProperties();
    }

    private static void SetUpWorldObjects(Transform player, List<string> log)
    {
        if (Object.FindAnyObjectByType<Bed>() == null)
        {
            var go = new GameObject("Bed");
            Undo.RegisterCreatedObjectUndo(go, "Create Bed");
            go.transform.position = player.position + new Vector3(-3f, 1f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Square;
            sr.color = new Color(0.75f, 0.35f, 0.4f);
            sr.sortingOrder = 8;
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            go.AddComponent<Bed>();

            var wake = new GameObject("WakePoint");
            wake.transform.SetParent(go.transform, false);
            wake.transform.localPosition = new Vector3(1.2f, -0.3f, 0f);
            wake.transform.localScale = new Vector3(1f, 0.5f, 1f);
            SetRef(go.GetComponent<Bed>(), "wakePoint", wake.transform);
            log.Add("Added a placeholder Bed (pink rectangle). Give it your own sprite.");
        }

        if (Object.FindAnyObjectByType<ShippingBin>() == null)
        {
            var go = new GameObject("ShippingBin");
            Undo.RegisterCreatedObjectUndo(go, "Create Shipping Bin");
            go.transform.position = player.position + new Vector3(3f, 1f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Square;
            sr.color = new Color(0.55f, 0.38f, 0.2f);
            sr.sortingOrder = 8;
            go.transform.localScale = new Vector3(1.5f, 1f, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            go.AddComponent<ShippingBin>();
            log.Add("Added a placeholder ShippingBin (brown rectangle). Give it your own sprite.");
        }
    }

    // ------------------------------------------------------------------
    // Assets
    // ------------------------------------------------------------------

    private static PollutionSettings EnsurePollutionSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PollutionSettings"))
            return AssetDatabase.LoadAssetAtPath<PollutionSettings>(AssetDatabase.GUIDToAssetPath(guid));

        var asset = ScriptableObject.CreateInstance<PollutionSettings>();
        asset.levels = PollutionSettings.DefaultLevels();
        AssetDatabase.CreateAsset(asset, DataFolder + "/PollutionSettings.asset");
        return asset;
    }

    /// <summary>Makes 5 translucent overlay tiles (a speckled wash that gets denser with each level).</summary>
    private static TileBase[] EnsurePollutionTiles(PollutionSettings settings)
    {
        var tiles = new TileBase[PollutionSettings.MaxLevel];

        for (int level = 1; level <= PollutionSettings.MaxLevel; level++)
        {
            string tilePath = PollutionFolder + "/Pollution" + level + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null) { tiles[level - 1] = existing; continue; }

            string pngPath = PollutionFolder + "/pollution_" + level + ".png";
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), pngPath);

            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color baseColor = Color.Lerp(new Color(0.55f, 0.65f, 0.2f), new Color(0.5f, 0.1f, 0.6f), (level - 1) / 4f);
            float coverage = 0.12f + 0.14f * level;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float h = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f + level * 37.719f) * 43758.5453f) % 1f;
                    float wash = 0.05f + 0.04f * level;
                    float a = h < coverage ? 0.55f : wash;
                    tex.SetPixel(x, y, new Color(baseColor.r, baseColor.g, baseColor.b, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SetLabels(tile, new[] { FarmEditorLabels.Pollution });
            tiles[level - 1] = tile;
        }

        return tiles;
    }

    private static List<ItemData> EnsureItems(List<string> log)
    {
        // A sword and two spells, so there is something to equip right away. Edit them freely.
        CreateItemIfMissing("WoodenSword", it =>
        {
            it.itemName = "Wooden Sword"; it.type = ItemData.ItemType.Weapon; it.maxStack = 1;
            it.damage = 5; it.attackRange = 0.8f; it.attackRadius = 0.6f; it.knockbackForce = 6f;
            it.swingDuration = 13f / 12f; it.hitDelay = 0.5f; it.attackCooldown = 0.1f;
        }, log);
        CreateItemIfMissing("Spark", it =>
        {
            it.itemName = "Spark"; it.type = ItemData.ItemType.Spell; it.maxStack = 1;
            it.spellEffect = ItemData.SpellEffect.Projectile; it.magicCost = 8f; it.spellDamage = 8;
            it.projectileSpeed = 8f; it.projectileLifetime = 1.2f; it.knockbackForce = 4f; it.castTime = 0.25f;
        }, log);
        CreateItemIfMissing("HealingLight", it =>
        {
            it.itemName = "Healing Light"; it.type = ItemData.ItemType.Spell; it.maxStack = 1;
            it.spellEffect = ItemData.SpellEffect.Heal; it.magicCost = 15f; it.healAmount = 25; it.castTime = 0.4f;
            it.projectileColor = new Color(0.5f, 1f, 0.6f);
        }, log);

        var items = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it == null) continue;

            // Tools imported before ToolAction existed get it guessed from their name.
            if (it.type == ItemData.ItemType.Tool && it.toolAction == ItemData.ToolAction.None)
            {
                it.toolAction = GuessTool(it.itemName);
                if (it.toolAction != ItemData.ToolAction.None) EditorUtility.SetDirty(it);
            }
            items.Add(it);
        }

        AssignIcons(items, log);

        items.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return items;
    }

    /// <summary>
    /// Gives items without a picture one from any "icon_*.png" in the project (icon_wooden_sword.png fits an item
    /// asset called WoodenSword). Icon PNGs are turned into 16-pixel sprites automatically.
    /// </summary>
    private static void AssignIcons(List<ItemData> items, List<string> log)
    {
        var sprites = new Dictionary<string, Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("icon_ t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith("icon_") || file.EndsWith("_big")) continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites[file.Substring(5).Replace("_", "")] = sprite;
        }

        int assigned = 0;
        foreach (ItemData it in items)
        {
            if (it.icon != null) continue;
            string key = it.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            Sprite s;
            if (sprites.TryGetValue(key, out s))
            {
                it.icon = s;
                EditorUtility.SetDirty(it);
                assigned++;
            }
        }
        if (assigned > 0) log.Add("Assigned " + assigned + " item icons from icon_*.png files.");
    }

    private static void CreateItemIfMissing(string fileName, System.Action<ItemData> setup, List<string> log)
    {
        string path = ItemsFolder + "/" + fileName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null) return;

        var it = ScriptableObject.CreateInstance<ItemData>();
        setup(it);
        AssetDatabase.CreateAsset(it, path);
        log.Add("Created item " + it.itemName + " (no icon yet - assign one in the Inspector).");
    }

    private static ItemData.ToolAction GuessTool(string itemName)
    {
        string n = itemName.ToLowerInvariant();
        if (n.Contains("hoe")) return ItemData.ToolAction.Hoe;
        if (n.Contains("water")) return ItemData.ToolAction.WateringCan;
        if (n.Contains("sickle") || n.Contains("scythe")) return ItemData.ToolAction.Sickle;
        if (n.Contains("pickaxe") || n.Contains("pick")) return ItemData.ToolAction.Pickaxe;
        if (n.Contains("axe")) return ItemData.ToolAction.Axe;
        if (n.Contains("shovel") || n.Contains("spade")) return ItemData.ToolAction.Shovel;
        if (n.Contains("rod") || n.Contains("fish")) return ItemData.ToolAction.FishingRod;
        return ItemData.ToolAction.None;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static T AddOnce<T>(GameObject go, List<string> log) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) return c;
        c = Undo.AddComponent<T>(go);
        log.Add("Added " + typeof(T).Name + " to " + go.name + ".");
        return c;
    }

    private static T GetOrCreate<T>(string name, List<string> log) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>();
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        log.Add("Created " + name + ".");
        return go.AddComponent<T>();
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedProperties(); }
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
