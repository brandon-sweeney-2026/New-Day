using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Farmhouse Hardware Upgrades      (run it with the FARM scene open)
/// Farm > Set Up > Suburbs Scene runs it for you when it visits the Farm scene.
/// Adds to your existing farmhouse:
///   - Front door colours and Shutters (pictures drawn over the outside of the house, tinted),
///   - Kitchen cabinets (a wall slot above the stove),
///   - Window styles and Window layouts (windows drawn on the back wall),
///   - four more stoves (Gas, Retro, Double Oven, Chef's range) with better cooking bonuses.
/// It makes the upgrade assets, joins them to the HouseManager's catalog, and sets up the slots / overlay pictures.
/// Safe to run again: existing assets are reused and nothing is added twice.
/// If you ever use Farm > Set Up > Rebuild Farmhouse (replace existing), run this again afterwards.
/// Must live in a folder named "Editor".
/// </summary>
public static class HouseHardwareUpgradesMenu
{
    private static string art;
    private static string dataFolder;

    [MenuItem("Farm/Set Up/Farmhouse Hardware Upgrades", false, 149)]
    public static void RunFromMenu()
    {
        string result = Run();
        EditorUtility.DisplayDialog(result.StartsWith("Added") ? "Hardware upgrades added" : "Hardware upgrades", result, "OK");
    }

    /// <summary>
    /// Makes the upgrade assets and joins them to the farmhouse in the open scene. Returns a sentence saying what happened
    /// (it starts with "Added" when it worked). No pop-ups, so the Suburbs scene builder can call it too.
    /// </summary>
    public static string Run()
    {
        List<HouseUpgradeData> made = MakeAssets();
        if (made == null) return "Couldn't find house_door_overlay.png. The 'Hardware' art folder belongs in Assets/Project/Art/Hardware.";
        HouseManager mgr = Object.FindAnyObjectByType<HouseManager>();
        if (mgr == null) return "No HouseManager in the open scene. Build the farmhouse first (Farm > Set Up > Build Farmhouse) and open that scene.";

        WireScene(mgr, made);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Added " + made.Count + " upgrade options to the HouseManager: front doors, shutters, kitchen cabinets, window styles and layouts, and four stoves.\n\n" +
               "They show up at the design table (Floor / Walls / ... / Front door / Shutters / Kitchen cabinets / Window style / Window layout) and at the displays in Frank's store.\n\n" +
               "Tip: the HouseManager has 'Buy Only At Hardware Store'. Tick it when you want upgrades to be bought at Frank's only.";
    }

    /// <summary>
    /// Only the assets (pictures imported, one HouseUpgradeData per option). Needs no scene, so it can run before Frank's store is built.
    /// Returns null if the art is missing.
    /// </summary>
    public static List<HouseUpgradeData> MakeAssets()
    {
        art = HardwareEditorUtil.FindFolder("house_door_overlay");
        if (art == null) return null;
        ImportArt();
        dataFolder = FindUpgradeFolder();
        HardwareEditorUtil.EnsureFolder(dataFolder);
        List<HouseUpgradeData> made = MakeUpgrades();
        AssetDatabase.SaveAssets();
        return made;
    }

    // ------------------------------------------------------------------ art
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("house_")) HardwareEditorUtil.ImportSingle(p, SpriteAlignment.BottomCenter);
            else if (n.StartsWith("win_")) HardwareEditorUtil.ImportSingle(p, SpriteAlignment.Center);
            else if (n.StartsWith("cab_") || n.StartsWith("fur_stove_")) HardwareEditorUtil.ImportSingle(p, SpriteAlignment.BottomLeft);
        }
        AssetDatabase.Refresh();
    }

    private static string FindUpgradeFolder()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:HouseUpgradeData"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return "Assets/Farm/Data/Upgrades";
    }

    // ------------------------------------------------------------------ the options
    private struct Cost { public string item; public int n; public Cost(string i, int c) { item = i; n = c; } }

    private static List<HouseUpgradeData> MakeUpgrades()
    {
        var list = new List<HouseUpgradeData>();
        Cost Wood(int n) { return new Cost("WoodPlank", n); }
        Cost Paint(int n) { return new Cost("PaintBucket", n); }
        Cost Fabric(int n) { return new Cost("FabricRoll", n); }
        Cost Stone(int n) { return new Cost("StoneBrick", n); }
        Cost Glass(int n) { return new Cost("GlassPane", n); }
        Cost[] None = new Cost[0];

        HouseUpgradeData Add(HouseCategory cat, string id, string name, string desc, bool isDefault, int gold, Cost[] costs,
                             string sprite = null, Color? tint = null, float food = 0f, WindowSpot[] spots = null)
        {
            var o = HardwareEditorUtil.LoadOrCreate<HouseUpgradeData>(dataFolder + "/" + cat + "_" + id + ".asset");
            o.category = cat; o.displayName = name; o.description = desc; o.isDefault = isDefault; o.goldCost = gold;
            o.materials = new List<MaterialCost>();
            foreach (Cost c in costs)
            {
                ItemData it = HardwareEditorUtil.FindItem(c.item);
                if (it != null) o.materials.Add(new MaterialCost { item = it, count = c.n });
                else Debug.LogWarning("Hardware upgrades: material '" + c.item + "' not found, left out of " + name);
            }
            o.sprite = sprite != null ? HardwareEditorUtil.Spr(art, sprite) : null;
            o.tint = tint ?? Color.white;
            o.foodBonus = food;
            o.windowSpots = spots ?? new WindowSpot[0];
            EditorUtility.SetDirty(o);
            list.Add(o);
            return o;
        }
        Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

        // ---- front door: no picture = the original brown door that is baked into the house art
        Add(HouseCategory.Door, "Original", "Original Oak Door", "The warm wooden door the house came with.", true, 0, None);
        Add(HouseCategory.Door, "Red", "Red Door", "A cheerful barn-red front door.", false, 90, new[] { Paint(2) }, "house_door_overlay", Hex("#c0463a"));
        Add(HouseCategory.Door, "Navy", "Navy Door", "Classic and calm.", false, 90, new[] { Paint(2) }, "house_door_overlay", Hex("#3a5a8c"));
        Add(HouseCategory.Door, "Forest", "Forest Green Door", "Blends in with the farm.", false, 90, new[] { Paint(2) }, "house_door_overlay", Hex("#3f7a55"));
        Add(HouseCategory.Door, "Yellow", "Sunny Yellow Door", "You can see it from the road.", false, 100, new[] { Paint(2) }, "house_door_overlay", Hex("#e8b830"));
        Add(HouseCategory.Door, "Teal", "Teal Door", "Fresh and a little bit fancy.", false, 100, new[] { Paint(2) }, "house_door_overlay", Hex("#3a9a9a"));
        Add(HouseCategory.Door, "Black", "Black Door", "Sleek and modern.", false, 120, new[] { Paint(2) }, "house_door_overlay", Hex("#3a3a42"));

        // ---- shutters
        Add(HouseCategory.Shutters, "None", "No Shutters", "Bare windows.", true, 0, None);
        Add(HouseCategory.Shutters, "White", "White Shutters", "Crisp and country.", false, 110, new[] { Wood(4), Paint(2) }, "house_shutters_overlay", Hex("#f2f0e8"));
        Add(HouseCategory.Shutters, "Green", "Green Shutters", "Leafy green louvres.", false, 110, new[] { Wood(4), Paint(2) }, "house_shutters_overlay", Hex("#4a8a5a"));
        Add(HouseCategory.Shutters, "Navy", "Navy Shutters", "Deep blue shutters.", false, 110, new[] { Wood(4), Paint(2) }, "house_shutters_overlay", Hex("#3a5a8c"));
        Add(HouseCategory.Shutters, "Red", "Red Shutters", "Bold barn red.", false, 110, new[] { Wood(4), Paint(2) }, "house_shutters_overlay", Hex("#b8483c"));
        Add(HouseCategory.Shutters, "Black", "Black Shutters", "Smart and dramatic.", false, 120, new[] { Wood(4), Paint(2) }, "house_shutters_overlay", Hex("#35353c"));

        // ---- kitchen cabinets
        Add(HouseCategory.Cabinets, "None", "No Cabinets", "Just a bare wall above the stove.", true, 0, None);
        Add(HouseCategory.Cabinets, "White", "White Shaker Cabinets", "Simple white cupboards.", false, 150, new[] { Wood(6), Paint(2) }, "cab_white");
        Add(HouseCategory.Cabinets, "Oak", "Oak Cabinets", "Warm natural wood.", false, 200, new[] { Wood(8) }, "cab_oak");
        Add(HouseCategory.Cabinets, "Sage", "Sage Cabinets", "Soft green paint.", false, 180, new[] { Wood(6), Paint(3) }, "cab_sage");
        Add(HouseCategory.Cabinets, "Navy", "Navy Cabinets", "Bold blue with brass handles.", false, 220, new[] { Wood(6), Paint(3) }, "cab_navy");
        Add(HouseCategory.Cabinets, "Glass", "Glass-Front Cabinets", "Show off your nicest plates.", false, 280, new[] { Wood(6), Glass(4) }, "cab_glass");
        Add(HouseCategory.Cabinets, "Modern", "Modern Grey Cabinets", "Flat-front with yellow pulls.", false, 300, new[] { Wood(8), Paint(2), Stone(2) }, "cab_modern");

        // ---- stoves (the existing Cast Iron Stove and Modern Range are made by the farmhouse setup)
        Add(HouseCategory.Kitchen, "Gas", "Gas Range", "A 35% chance of an extra serving every time you cook.", false, 650, new[] { Stone(8), Glass(2) }, "fur_stove_gas", null, 0.35f);
        Add(HouseCategory.Kitchen, "Retro", "Retro Mint Range", "Pretty as a picture. 40% chance of an extra serving.", false, 750, new[] { Stone(8), Glass(3), Paint(2) }, "fur_stove_retro", null, 0.40f);
        Add(HouseCategory.Kitchen, "Double", "Double Oven Range", "Two ovens! 50% chance of an extra serving.", false, 1100, new[] { Stone(12), Glass(4) }, "fur_stove_double", null, 0.50f);
        Add(HouseCategory.Kitchen, "Chef", "Chef's Range", "Professional steel with red knobs. 60% chance of an extra serving.", false, 1600, new[] { Stone(16), Glass(6), Wood(4) }, "fur_stove_chef", null, 0.60f);

        // ---- window styles (the picture). Layouts below say where they go.
        Add(HouseCategory.WindowStyle, "Plain", "Plain Windows", "Simple four-pane windows.", true, 0, None, "win_plain");
        Add(HouseCategory.WindowStyle, "Cottage", "Cottage Windows", "Curtains and a little flower box.", false, 120, new[] { Wood(2), Fabric(3), Glass(2) }, "win_cottage");
        Add(HouseCategory.WindowStyle, "Arched", "Arched Windows", "A graceful curved top.", false, 180, new[] { Wood(3), Glass(4) }, "win_arched");
        Add(HouseCategory.WindowStyle, "Round", "Round Windows", "Ship-style portholes.", false, 160, new[] { Wood(3), Glass(3) }, "win_round");
        Add(HouseCategory.WindowStyle, "Wide", "Wide Picture Windows", "Big, bright and modern.", false, 240, new[] { Wood(4), Glass(6) }, "win_wide");

        // ---- window layouts (along the back wall: 0 = left edge, 1 = right edge)
        Add(HouseCategory.WindowLayout, "Two", "Two Windows", "One on each side of the room.", true, 0, None, null, null, 0f,
            Spots(0.18f, 0.62f));
        Add(HouseCategory.WindowLayout, "One", "One Window", "A single window. Lots of wall for art.", false, 60, new[] { Wood(1) }, null, null, 0f,
            Spots(0.4f));
        Add(HouseCategory.WindowLayout, "Three", "Three Windows", "A bright, sunny room.", false, 160, new[] { Wood(3), Glass(2) }, null, null, 0f,
            Spots(0.08f, 0.32f, 0.62f));
        Add(HouseCategory.WindowLayout, "Wall", "Window Wall", "Five windows in a row.", false, 300, new[] { Wood(5), Glass(4) }, null, null, 0f,
            Spots(0.06f, 0.2f, 0.34f, 0.48f, 0.62f));
        Add(HouseCategory.WindowLayout, "Corners", "Corner Windows", "Windows at both ends of the room.", false, 120, new[] { Wood(2) }, null, null, 0f,
            Spots(0.04f, 0.96f));
        return list;
    }

    private static WindowSpot[] Spots(params float[] along)
    {
        var r = new WindowSpot[along.Length];
        for (int i = 0; i < along.Length; i++) r[i] = new WindowSpot { along = along[i] };
        return r;
    }

    // ------------------------------------------------------------------ the scene
    private static void WireScene(HouseManager mgr, List<HouseUpgradeData> made)
    {
        var mso = new SerializedObject(mgr);

        // ---- catalog
        SerializedProperty cat = mso.FindProperty("catalog");
        var have = new HashSet<Object>();
        for (int i = 0; i < cat.arraySize; i++) have.Add(cat.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (HouseUpgradeData o in made)
        {
            if (have.Contains(o)) continue;
            cat.arraySize++;
            cat.GetArrayElementAtIndex(cat.arraySize - 1).objectReferenceValue = o;
        }

        // ---- outside overlays (children of the Farmhouse, drawn on top of the Details layer)
        GameObject house = GameObject.Find("Farmhouse");
        SerializedProperty doorProp = mso.FindProperty("doorRenderer");
        SerializedProperty shutProp = mso.FindProperty("shuttersRenderer");
        if (house != null)
        {
            if (doorProp.objectReferenceValue == null) doorProp.objectReferenceValue = Overlay(house, "DoorOverlay", 3);
            if (shutProp.objectReferenceValue == null) shutProp.objectReferenceValue = Overlay(house, "ShuttersOverlay", 4);
        }
        else Debug.LogWarning("Hardware upgrades: no 'Farmhouse' object found, so front doors and shutters have nothing to draw on. Assign Door Renderer / Shutters Renderer on the HouseManager yourself.");

        // ---- inside
        HouseInterior interior = mgr.Interior;
        if (interior != null)
        {
            HouseWindows hw = interior.GetComponent<HouseWindows>();
            if (hw == null) hw = Undo.AddComponent<HouseWindows>(interior.gameObject);
            mso.FindProperty("windows").objectReferenceValue = hw;
            EnsureCabinetSlot(interior);
        }
        else Debug.LogWarning("Hardware upgrades: the HouseManager has no Interior assigned, so cabinets and windows can't be added.");
        mso.ApplyModifiedProperties();
        EditorUtility.SetDirty(mgr);
    }

    private static SpriteRenderer Overlay(GameObject house, string name, int order)
    {
        Transform t = house.transform.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Overlay");
            go.transform.SetParent(house.transform, false);
        }
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    private static void EnsureCabinetSlot(HouseInterior interior)
    {
        var iso = new SerializedObject(interior);
        SerializedProperty slots = iso.FindProperty("slots");
        Vector3 pos = new Vector3(14f, interior.floorRows + 0.5f, 0f);
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty e = slots.GetArrayElementAtIndex(i);
            int c = e.FindPropertyRelative("category").enumValueIndex;
            var r = e.FindPropertyRelative("renderer").objectReferenceValue as SpriteRenderer;
            if (c == (int)HouseCategory.Cabinets) return;                          // already there
            if (c == (int)HouseCategory.Kitchen && r != null) pos = new Vector3(r.transform.localPosition.x, interior.floorRows + 0.5f, 0f);
        }
        var go = new GameObject("Slot_Cabinets");
        Undo.RegisterCreatedObjectUndo(go, "Cabinets slot");
        go.transform.SetParent(interior.transform, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 4;
        sr.enabled = false;
        slots.arraySize++;
        SerializedProperty ne = slots.GetArrayElementAtIndex(slots.arraySize - 1);
        ne.FindPropertyRelative("category").enumValueIndex = (int)HouseCategory.Cabinets;
        ne.FindPropertyRelative("renderer").objectReferenceValue = sr;
        iso.ApplyModifiedProperties();
    }
}
