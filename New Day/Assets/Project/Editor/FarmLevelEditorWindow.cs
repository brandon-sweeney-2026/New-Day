using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Asset labels that tell the level editor which palette an asset belongs to.
/// The Kiln Harvest importer sets these. To put your own tiles or prefabs in a palette, select the asset in the
/// Project window and add the label at the bottom of the Inspector (e.g. FarmLayer_Ground).
/// </summary>
public static class FarmEditorLabels
{
    public const string Ground = "FarmLayer_Ground";
    public const string Paths = "FarmLayer_Paths";
    public const string Soil = "FarmLayer_Soil";
    public const string Wet = "FarmLayer_Wet";
    public const string Fences = "FarmLayer_Fences";
    public const string Objects = "FarmLayer_Objects";
    public const string Pollution = "FarmLayer_Pollution";
}

/// <summary>
/// A visual level editor for your farm. Open it from the menu: Farm > Level Editor.
///
/// IMPORTANT: this file must live inside a folder named "Editor" (e.g. Assets/_Project/Scripts/Editor/).
///
/// How it works: you pick a layer (Ground / Paths / Soil / Wet / Fences / Objects / Pollution), pick something from the palette,
/// and paint in the Scene view. It edits the real Tilemaps in your scene, so each Scene is a level.
/// All edits support Ctrl+Z / Ctrl+Y.
///
/// Fastest way to get a correct scene: Farm > Kiln Harvest > 2. Create Farm Scene Layout.
/// </summary>
public class FarmLevelEditorWindow : EditorWindow
{
    public enum FarmLayer { Ground = 0, Paths = 1, Soil = 2, Wet = 3, Fences = 4, Objects = 5, Pollution = 6 }
    public enum BrushTool { Paint, Erase, Rectangle }

    private const int LayerCount = 7;
    private static readonly string[] LayerNames = { "Ground", "Paths", "Soil", "Wet", "Fences", "Objects", "Pollution" };
    private static readonly string[] LayerLabels =
    {
        FarmEditorLabels.Ground, FarmEditorLabels.Paths, FarmEditorLabels.Soil,
        FarmEditorLabels.Wet, FarmEditorLabels.Fences, FarmEditorLabels.Objects, FarmEditorLabels.Pollution
    };

    [System.Serializable]
    public class PaletteList
    {
        public List<Object> items = new List<Object>();
    }

    [MenuItem("Farm/Level Editor")]
    public static void Open()
    {
        var window = GetWindow<FarmLevelEditorWindow>("Farm Level Editor");
        window.minSize = new Vector2(300f, 460f);
    }

    // ------------------------------------------------------------------
    // Scene references and palettes (indexed by FarmLayer)
    // ------------------------------------------------------------------
    [SerializeField] private Tilemap[] tilemaps = new Tilemap[LayerCount];
    [SerializeField] private Transform objectsParent;
    [SerializeField] private PaletteList[] palettes = new PaletteList[LayerCount];

    // ------------------------------------------------------------------
    // Editor state
    // ------------------------------------------------------------------
    [SerializeField] private FarmLayer layer = FarmLayer.Ground;
    [SerializeField] private BrushTool tool = BrushTool.Paint;
    [SerializeField] private int[] selectedByLayer = new int[LayerCount];
    [SerializeField] private int brushSize = 1;
    [SerializeField] private bool restrictToValidCells = true;
    [SerializeField] private bool placeObjectsAtCellBottom = true;
    [SerializeField] private bool editingActive = true;
    [SerializeField] private int fillWidth = 30;
    [SerializeField] private int fillHeight = 20;

    private SerializedObject so;
    private Vector2 scroll;
    private bool paletteFoldout;
    private bool previewsPending;

    private bool dragging;
    private Vector3Int rectStart;
    private int strokeUndoGroup;
    private readonly HashSet<Object> registeredThisStroke = new HashSet<Object>();

    private Tilemap GroundMap { get { return tilemaps != null && tilemaps.Length > 0 ? tilemaps[0] : null; } }
    private Grid CurrentGrid { get { return GroundMap != null ? GroundMap.layoutGrid : null; } }

    private Tilemap Map(FarmLayer l)
    {
        int i = (int)l;
        return (tilemaps != null && i >= 0 && i < tilemaps.Length) ? tilemaps[i] : null;
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void OnEnable()
    {
        EnsureData();
        so = new SerializedObject(this);
        SceneView.duringSceneGui += OnSceneGUI;

        if (GroundMap == null) FindSceneReferences();

        bool allEmpty = true;
        for (int i = 0; i < LayerCount; i++)
            if (palettes[i].items.Count > 0) allEmpty = false;
        if (allEmpty) AutoFillAllPalettes();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        dragging = false;
    }

    // Called ~10 times per second while the window is open. Picks up palette previews as they finish loading.
    private void OnInspectorUpdate()
    {
        if (previewsPending) Repaint();
    }

    private void EnsureData()
    {
        if (tilemaps == null || tilemaps.Length != LayerCount) System.Array.Resize(ref tilemaps, LayerCount);
        if (palettes == null || palettes.Length != LayerCount) System.Array.Resize(ref palettes, LayerCount);
        for (int i = 0; i < LayerCount; i++)
        {
            if (palettes[i] == null) palettes[i] = new PaletteList();
            if (palettes[i].items == null) palettes[i].items = new List<Object>();
        }
        if (selectedByLayer == null || selectedByLayer.Length != LayerCount) selectedByLayer = new int[LayerCount];
    }

    // ------------------------------------------------------------------
    // Window UI
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        EnsureData();
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Painting is disabled in Play mode. Exit Play mode to edit the level.", MessageType.Info);
        }

        // ---- Scene setup ----
        EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
        DrawMapField(FarmLayer.Ground, "Ground");
        DrawMapField(FarmLayer.Paths, "Paths");
        DrawMapField(FarmLayer.Soil, "Soil (tilled)");
        DrawMapField(FarmLayer.Wet, "Wet soil");
        DrawMapField(FarmLayer.Fences, "Fences");
        DrawMapField(FarmLayer.Pollution, "Pollution (levels 1-5)");
        objectsParent = (Transform)EditorGUILayout.ObjectField("Objects Parent", objectsParent, typeof(Transform), true);
        if (GUILayout.Button("Auto-find in scene (by name)")) FindSceneReferences();

        if (GroundMap == null)
        {
            EditorGUILayout.HelpBox(
                "Assign at least the Ground tilemap to start.\n" +
                "Quick way: Farm > Kiln Harvest > 2. Create Farm Scene Layout builds everything for you.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        editingActive = EditorGUILayout.ToggleLeft("Painting active (turn off to select things normally)", editingActive);

        // ---- Layer + tool ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);
        layer = (FarmLayer)GUILayout.SelectionGrid((int)layer, LayerNames, 3);

        if (layer != FarmLayer.Objects && Map(layer) == null)
            EditorGUILayout.HelpBox("The " + LayerNames[(int)layer] + " tilemap is not assigned above, so nothing can be painted on this layer.", MessageType.Warning);

        EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);
        tool = (BrushTool)GUILayout.Toolbar((int)tool, new[] { "Paint", "Erase", "Rectangle" });
        brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 8);
        restrictToValidCells = EditorGUILayout.ToggleLeft("Soil, paths and fences only on ground; wet only on soil", restrictToValidCells);
        placeObjectsAtCellBottom = EditorGUILayout.ToggleLeft("Place objects with their base at the bottom of the cell", placeObjectsAtCellBottom);
        EditorGUILayout.HelpBox("Hold Shift to erase with any tool.  [ and ] change brush size.  Hold Alt to orbit/pan the Scene view.", MessageType.None);

        // ---- Palette ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette: " + LayerNames[(int)layer], EditorStyles.boldLabel);
        DrawPalette();

        paletteFoldout = EditorGUILayout.Foldout(paletteFoldout, "Edit palette list", true);
        if (paletteFoldout)
        {
            SerializedProperty prop = so.FindProperty("palettes").GetArrayElementAtIndex((int)layer).FindPropertyRelative("items");
            EditorGUILayout.PropertyField(prop, true);

            if (GUILayout.Button("Reload this palette from labelled assets"))
            {
                so.ApplyModifiedProperties();
                AutoFillPalette(layer);
                so.Update();
            }
            if (GUILayout.Button("Reload ALL palettes"))
            {
                so.ApplyModifiedProperties();
                AutoFillAllPalettes();
                so.Update();
            }
            EditorGUILayout.HelpBox("Palettes load assets that carry the label " + LayerLabels[(int)layer] +
                                    ". Add that label to your own tiles/prefabs to show them here.", MessageType.None);
        }

        // ---- Extras ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Extras", EditorStyles.boldLabel);

        if (layer == FarmLayer.Ground)
        {
            EditorGUILayout.BeginHorizontal();
            fillWidth = Mathf.Max(1, EditorGUILayout.IntField("Fill W x H", fillWidth));
            fillHeight = Mathf.Max(1, EditorGUILayout.IntField(fillHeight));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Fill area around origin with selected ground tile")) FillGround();
        }

        if (GUILayout.Button("Clear this layer"))
        {
            if (EditorUtility.DisplayDialog("Clear layer", "Remove everything on the " + LayerNames[(int)layer] + " layer? (You can undo this.)", "Clear", "Cancel"))
                ClearLayer();
        }

        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }

    private void DrawMapField(FarmLayer l, string label)
    {
        tilemaps[(int)l] = (Tilemap)EditorGUILayout.ObjectField(label, tilemaps[(int)l], typeof(Tilemap), true);
    }

    private void DrawPalette()
    {
        List<Object> items = palettes[(int)layer].items;
        int count = items.Count;
        if (count == 0)
        {
            EditorGUILayout.HelpBox(
                "This palette is empty.\nRun Farm > Kiln Harvest > 1. Import Pack Assets, or open 'Edit palette list' below to drag in your own tiles/prefabs.",
                MessageType.Info);
            return;
        }

        previewsPending = false;
        const float cell = 56f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / (cell + 6f)));
        int i = 0;
        while (i < count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns && i < count; c++, i++)
            {
                Object asset = items[i];
                Rect r = GUILayoutUtility.GetRect(cell, cell, GUILayout.Width(cell), GUILayout.Height(cell));

                bool selected = selectedByLayer[(int)layer] == i;
                string tip = asset != null ? asset.name : "(empty slot)";
                bool pressed = GUI.Toggle(r, selected, new GUIContent("", tip), "Button");
                if (pressed && !selected) selectedByLayer[(int)layer] = i;

                if (Event.current.type == EventType.Repaint && asset != null) DrawAssetIcon(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), asset);
            }
            EditorGUILayout.EndHorizontal();
        }

        Object current = SelectedAsset();
        EditorGUILayout.LabelField("Selected: " + (current != null ? current.name : "none"), EditorStyles.miniLabel);
    }

    /// <summary>The sprite that best represents a palette entry (tile or prefab), or null.</summary>
    private static Sprite SpriteOf(Object asset)
    {
        var autoTile = asset as AutoTile;
        if (autoTile != null) return autoTile.previewSprite;

        var randomTile = asset as RandomGroundTile;
        if (randomTile != null) return randomTile.previewSprite;

        var tile = asset as Tile;
        if (tile != null) return tile.sprite;

        var go = asset as GameObject;
        if (go != null)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) return sr.sprite;
        }
        return null;
    }

    private void DrawAssetIcon(Rect area, Object asset)
    {
        Sprite sprite = SpriteOf(asset);
        if (sprite != null && sprite.texture != null)
        {
            Texture2D tex = sprite.texture;
            Rect tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

            // Fit inside the square without stretching.
            float scale = Mathf.Min(area.width / tr.width, area.height / tr.height);
            float w = tr.width * scale;
            float h = tr.height * scale;
            var fitted = new Rect(area.x + (area.width - w) * 0.5f, area.y + (area.height - h) * 0.5f, w, h);
            GUI.DrawTextureWithTexCoords(fitted, tex, uv);
            return;
        }

        Texture preview = AssetPreview.GetAssetPreview(asset);
        if (preview == null)
        {
            preview = AssetPreview.GetMiniThumbnail(asset);
            previewsPending = true;
        }
        if (preview != null) GUI.DrawTexture(area, preview, ScaleMode.ScaleToFit);
    }

    // ------------------------------------------------------------------
    // Scene view painting
    // ------------------------------------------------------------------

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!editingActive || Application.isPlaying || CurrentGrid == null) return;
        if (Tools.current == UnityEditor.Tool.View) return; // hand tool = camera navigation only

        Event e = Event.current;

        // Stops Unity from selecting objects when we click to paint.
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

        if (e.alt) return; // Alt + mouse = camera controls

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftBracket) { brushSize = Mathf.Max(1, brushSize - 1); Repaint(); e.Use(); }
            else if (e.keyCode == KeyCode.RightBracket) { brushSize = Mathf.Min(8, brushSize + 1); Repaint(); e.Use(); }
        }

        Vector3Int cell;
        if (!TryGetCell(e.mousePosition, out cell)) return;
        bool erase = tool == BrushTool.Erase || e.shift;

        if (e.type == EventType.Repaint) DrawPreview(cell, erase);
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sceneView.Repaint();

        if (e.button != 0) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                BeginStroke();
                dragging = true;
                rectStart = cell;
                if (tool != BrushTool.Rectangle) ApplyAtHover(cell, erase);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (dragging)
                {
                    if (tool != BrushTool.Rectangle) ApplyAtHover(cell, erase);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (dragging)
                {
                    if (tool == BrushTool.Rectangle) ApplyAtHover(cell, erase);
                    dragging = false;
                    EndStroke();
                    e.Use();
                }
                break;
        }
    }

    private bool TryGetCell(Vector2 guiPosition, out Vector3Int cell)
    {
        Grid grid = CurrentGrid;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(grid.transform.forward, grid.transform.position);
        float distance;
        if (plane.Raycast(ray, out distance))
        {
            cell = grid.WorldToCell(ray.GetPoint(distance));
            cell.z = 0;
            return true;
        }
        cell = default(Vector3Int);
        return false;
    }

    /// <summary>The block of cells the brush (or the dragged rectangle) currently covers.</summary>
    private void GetHoverBounds(Vector3Int cell, out Vector3Int min, out Vector3Int max)
    {
        if (tool == BrushTool.Rectangle && dragging)
        {
            min = new Vector3Int(Mathf.Min(rectStart.x, cell.x), Mathf.Min(rectStart.y, cell.y), 0);
            max = new Vector3Int(Mathf.Max(rectStart.x, cell.x), Mathf.Max(rectStart.y, cell.y), 0);
            return;
        }

        int n = tool == BrushTool.Rectangle ? 1 : brushSize;
        int low = -(n - 1) / 2;
        min = new Vector3Int(cell.x + low, cell.y + low, 0);
        max = new Vector3Int(min.x + n - 1, min.y + n - 1, 0);
    }

    private void DrawPreview(Vector3Int cell, bool erase)
    {
        Vector3Int min, max;
        GetHoverBounds(cell, out min, out max);

        Grid grid = CurrentGrid;
        Vector3 a = grid.CellToWorld(min);
        Vector3 b = grid.CellToWorld(new Vector3Int(max.x + 1, max.y + 1, 0));
        Vector3[] corners =
        {
            a,
            new Vector3(a.x, b.y, a.z),
            b,
            new Vector3(b.x, a.y, a.z)
        };

        Color fill = erase ? new Color(1f, 0.25f, 0.25f, 0.25f) : new Color(0.3f, 1f, 0.4f, 0.25f);
        Handles.DrawSolidRectangleWithOutline(corners, fill, Color.white);
    }

    // ------------------------------------------------------------------
    // Applying edits
    // ------------------------------------------------------------------

    private void BeginStroke()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Farm Level Edit");
        strokeUndoGroup = Undo.GetCurrentGroup();
        registeredThisStroke.Clear();
    }

    private void EndStroke()
    {
        Undo.CollapseUndoOperations(strokeUndoGroup);
        if (GroundMap != null) EditorSceneManager.MarkSceneDirty(GroundMap.gameObject.scene);
    }

    private void ApplyAtHover(Vector3Int cell, bool erase)
    {
        Vector3Int min, max;
        GetHoverBounds(cell, out min, out max);
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                ApplyCell(new Vector3Int(x, y, 0), erase);
    }

    private void ApplyCell(Vector3Int cell, bool erase)
    {
        if (layer == FarmLayer.Objects)
        {
            if (erase) EraseObjectAt(cell);
            else PlaceObjectAt(cell);
        }
        else
        {
            ApplyTile(cell, erase);
        }
    }

    private void ApplyTile(Vector3Int cell, bool erase)
    {
        Tilemap map = Map(layer);
        if (map == null) return;

        if (erase)
        {
            EraseTileAt(layer, cell);
            return;
        }

        var tile = SelectedAsset() as TileBase;
        if (tile == null) return;

        if (restrictToValidCells)
        {
            bool needsGround = layer == FarmLayer.Paths || layer == FarmLayer.Soil || layer == FarmLayer.Fences;
            if (needsGround && (GroundMap == null || GroundMap.GetTile(cell) == null)) return;

            if (layer == FarmLayer.Wet)
            {
                Tilemap soil = Map(FarmLayer.Soil);
                if (soil == null || soil.GetTile(cell) == null) return;
            }
        }

        SetTile(map, cell, tile);
    }

    /// <summary>Erases a tile and keeps the layers consistent (no soil without ground, no wet without soil).</summary>
    private void EraseTileAt(FarmLayer l, Vector3Int cell)
    {
        SetTile(Map(l), cell, null);

        if (l == FarmLayer.Ground)
        {
            SetTile(Map(FarmLayer.Paths), cell, null);
            SetTile(Map(FarmLayer.Soil), cell, null);
            SetTile(Map(FarmLayer.Wet), cell, null);
            SetTile(Map(FarmLayer.Fences), cell, null);
        }
        else if (l == FarmLayer.Soil)
        {
            SetTile(Map(FarmLayer.Wet), cell, null);
        }
    }

    private void SetTile(Tilemap map, Vector3Int cell, TileBase tile)
    {
        if (map == null || map.GetTile(cell) == tile) return;

        if (registeredThisStroke.Add(map))
            Undo.RegisterCompleteObjectUndo(map, "Farm Level Edit");

        map.SetTile(cell, tile);

        // Neighbouring auto-tiles must redraw their edges (this matters most after an erase).
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    map.RefreshTile(new Vector3Int(cell.x + dx, cell.y + dy, cell.z));

        EditorUtility.SetDirty(map);
    }

    // ---- Objects (trees, rocks, spawn points, etc.) ----

    private Transform GetOrCreateObjectsParent()
    {
        if (objectsParent != null) return objectsParent;

        var go = new GameObject("Objects");
        Undo.RegisterCreatedObjectUndo(go, "Create Objects Parent");
        objectsParent = go.transform;
        return objectsParent;
    }

    /// <summary>Where an object for this cell goes: the cell centre, or the middle of its bottom edge.</summary>
    private Vector3 ObjectPositionFor(Vector3Int cell)
    {
        Vector3 p = CurrentGrid.GetCellCenterWorld(cell);
        if (placeObjectsAtCellBottom) p.y -= CurrentGrid.cellSize.y * 0.5f;
        return p;
    }

    private List<Transform> ObjectsAt(Vector3Int cell)
    {
        var found = new List<Transform>();
        if (objectsParent == null || CurrentGrid == null) return found;

        foreach (Transform child in objectsParent)
        {
            Vector3 p = child.position;
            if (placeObjectsAtCellBottom) p.y += CurrentGrid.cellSize.y * 0.5f;   // back to the cell centre
            Vector3Int childCell = CurrentGrid.WorldToCell(p);
            if (childCell.x == cell.x && childCell.y == cell.y) found.Add(child);
        }
        return found;
    }

    private void PlaceObjectAt(Vector3Int cell)
    {
        var prefab = SelectedAsset() as GameObject;
        if (prefab == null) return;
        if (ObjectsAt(cell).Count > 0) return; // one object per cell

        Transform parent = GetOrCreateObjectsParent();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = ObjectPositionFor(cell);
        Undo.RegisterCreatedObjectUndo(instance, "Place Object");
    }

    private void EraseObjectAt(Vector3Int cell)
    {
        foreach (Transform t in ObjectsAt(cell))
            Undo.DestroyObjectImmediate(t.gameObject);
    }

    // ---- Extras ----

    private void FillGround()
    {
        var tile = SelectedAsset() as TileBase;
        if (tile == null || GroundMap == null)
        {
            EditorUtility.DisplayDialog("Fill ground", "Select a ground tile in the palette first.", "OK");
            return;
        }

        BeginStroke();
        int startX = -fillWidth / 2;
        int startY = -fillHeight / 2;
        for (int x = 0; x < fillWidth; x++)
            for (int y = 0; y < fillHeight; y++)
                SetTile(GroundMap, new Vector3Int(startX + x, startY + y, 0), tile);
        EndStroke();
    }

    private void ClearLayer()
    {
        BeginStroke();

        if (layer == FarmLayer.Objects)
        {
            if (objectsParent != null)
            {
                var children = new List<Transform>();
                foreach (Transform child in objectsParent) children.Add(child);
                foreach (Transform child in children) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
        else
        {
            ClearMap(Map(layer));
            if (layer == FarmLayer.Ground)
            {
                ClearMap(Map(FarmLayer.Paths));
                ClearMap(Map(FarmLayer.Soil));
                ClearMap(Map(FarmLayer.Wet));
                ClearMap(Map(FarmLayer.Fences));
            }
            else if (layer == FarmLayer.Soil)
            {
                ClearMap(Map(FarmLayer.Wet));
            }
        }

        EndStroke();
    }

    private void ClearMap(Tilemap map)
    {
        if (map == null) return;
        if (registeredThisStroke.Add(map)) Undo.RegisterCompleteObjectUndo(map, "Clear Layer");
        map.ClearAllTiles();
        EditorUtility.SetDirty(map);
    }

    // ------------------------------------------------------------------
    // Helpers: scene lookup, palettes
    // ------------------------------------------------------------------

    private void FindSceneReferences()
    {
        foreach (Tilemap map in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            string n = map.name.ToLowerInvariant();

            if (tilemaps[(int)FarmLayer.Ground] == null && n.Contains("ground")) tilemaps[(int)FarmLayer.Ground] = map;
            else if (tilemaps[(int)FarmLayer.Paths] == null && (n.Contains("path") || n.Contains("overlay"))) tilemaps[(int)FarmLayer.Paths] = map;
            else if (tilemaps[(int)FarmLayer.Wet] == null && (n.Contains("wet") || n.Contains("water"))) tilemaps[(int)FarmLayer.Wet] = map;
            else if (tilemaps[(int)FarmLayer.Soil] == null && n.Contains("soil")) tilemaps[(int)FarmLayer.Soil] = map;
            else if (tilemaps[(int)FarmLayer.Fences] == null && n.Contains("fence")) tilemaps[(int)FarmLayer.Fences] = map;
            else if (tilemaps[(int)FarmLayer.Pollution] == null && n.Contains("pollution")) tilemaps[(int)FarmLayer.Pollution] = map;
        }

        if (objectsParent == null)
        {
            GameObject found = GameObject.Find("Objects");
            if (found != null) objectsParent = found.transform;
        }
    }

    private Object SelectedAsset()
    {
        List<Object> items = palettes[(int)layer].items;
        int index = selectedByLayer[(int)layer];
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    private void AutoFillAllPalettes()
    {
        for (int i = 0; i < LayerCount; i++) AutoFillPalette((FarmLayer)i);
    }

    /// <summary>Fills a palette with every project asset that carries this layer's label.</summary>
    private void AutoFillPalette(FarmLayer l)
    {
        List<Object> list = palettes[(int)l].items;
        list.Clear();

        string label = LayerLabels[(int)l];
        string type = l == FarmLayer.Objects ? "t:Prefab" : "t:TileBase";

        foreach (string guid in AssetDatabase.FindAssets("l:" + label + " " + type))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = l == FarmLayer.Objects
                ? (Object)AssetDatabase.LoadAssetAtPath<GameObject>(path)
                : (Object)AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (asset != null) list.Add(asset);
        }

        list.Sort((x, y) => string.Compare(x.name, y.name, System.StringComparison.OrdinalIgnoreCase));
        selectedByLayer[(int)l] = 0;
    }
}
