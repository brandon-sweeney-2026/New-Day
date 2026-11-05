using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A visual level editor for your farm. Open it from the menu: Farm > Level Editor.
///
/// IMPORTANT: this file must live inside a folder named "Editor" (e.g. Assets/_Project/Scripts/Editor/).
/// Unity will not compile editor-only code anywhere else.
///
/// How it works: you pick a layer (Ground / Soil / Water / Objects), pick something from the palette,
/// and paint in the Scene view. It edits the real Tilemaps in your scene, so each Scene is a level.
/// All edits support Ctrl+Z / Ctrl+Y.
/// </summary>
public class FarmLevelEditorWindow : EditorWindow
{
    public enum FarmLayer { Ground, Soil, Water, Objects }
    public enum BrushTool { Paint, Erase, Rectangle }

    [MenuItem("Farm/Level Editor")]
    public static void Open()
    {
        var window = GetWindow<FarmLevelEditorWindow>("Farm Level Editor");
        window.minSize = new Vector2(290f, 420f);
    }

    // ------------------------------------------------------------------
    // Scene references
    // ------------------------------------------------------------------
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap soilTilemap;
    [SerializeField] private Tilemap waterTilemap;
    [SerializeField] private Transform objectsParent;

    // ------------------------------------------------------------------
    // Palettes (edit these lists in the window, or use the auto-fill button)
    // ------------------------------------------------------------------
    [SerializeField] private List<TileBase> groundTiles = new List<TileBase>();
    [SerializeField] private List<TileBase> soilTiles = new List<TileBase>();
    [SerializeField] private List<TileBase> waterTiles = new List<TileBase>();
    [SerializeField] private List<GameObject> objectPrefabs = new List<GameObject>();

    // ------------------------------------------------------------------
    // Editor state
    // ------------------------------------------------------------------
    [SerializeField] private FarmLayer layer = FarmLayer.Ground;
    [SerializeField] private BrushTool tool = BrushTool.Paint;
    [SerializeField] private int[] selectedByLayer = new int[4];
    [SerializeField] private int brushSize = 1;
    [SerializeField] private bool restrictToValidCells = true;
    [SerializeField] private bool editingActive = true;
    [SerializeField] private int fillWidth = 30;
    [SerializeField] private int fillHeight = 30;

    private SerializedObject so;
    private Vector2 scroll;
    private bool paletteFoldout;
    private bool previewsPending;

    private bool dragging;
    private Vector3Int rectStart;
    private int strokeUndoGroup;
    private readonly HashSet<Object> registeredThisStroke = new HashSet<Object>();

    private Grid CurrentGrid => groundTilemap != null ? groundTilemap.layoutGrid : null;

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void OnEnable()
    {
        so = new SerializedObject(this);
        SceneView.duringSceneGui += OnSceneGUI;
        if (groundTilemap == null) FindSceneReferences();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        dragging = false;
    }

    // Called ~10 times per second while the window is open. Used to pick up palette previews as they finish loading.
    private void OnInspectorUpdate()
    {
        if (previewsPending) Repaint();
    }

    // ------------------------------------------------------------------
    // Window UI
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Painting is disabled in Play mode. Exit Play mode to edit the level.", MessageType.Info);
        }

        // ---- Scene setup ----
        EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
        groundTilemap = (Tilemap)EditorGUILayout.ObjectField("Ground", groundTilemap, typeof(Tilemap), true);
        soilTilemap = (Tilemap)EditorGUILayout.ObjectField("Soil", soilTilemap, typeof(Tilemap), true);
        waterTilemap = (Tilemap)EditorGUILayout.ObjectField("Water", waterTilemap, typeof(Tilemap), true);
        objectsParent = (Transform)EditorGUILayout.ObjectField("Objects Parent", objectsParent, typeof(Transform), true);
        if (GUILayout.Button("Auto-find in scene (by name)")) FindSceneReferences();

        if (groundTilemap == null)
        {
            EditorGUILayout.HelpBox("Assign at least the Ground tilemap to start. See LEVEL_EDITOR.md for scene setup.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        editingActive = EditorGUILayout.ToggleLeft("Painting active (turn off to select things normally)", editingActive);

        // ---- Layer + tool ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel);
        layer = (FarmLayer)GUILayout.Toolbar((int)layer, System.Enum.GetNames(typeof(FarmLayer)));

        EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);
        tool = (BrushTool)GUILayout.Toolbar((int)tool, new[] { "Paint", "Erase", "Rectangle" });
        brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 8);
        restrictToValidCells = EditorGUILayout.ToggleLeft("Soil only on ground, water only on soil", restrictToValidCells);
        EditorGUILayout.HelpBox("Hold Shift to erase with any tool.  [ and ] change brush size.  Hold Alt to orbit/pan the Scene view.", MessageType.None);

        // ---- Palette ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette: " + layer, EditorStyles.boldLabel);
        DrawPalette();

        paletteFoldout = EditorGUILayout.Foldout(paletteFoldout, "Edit palette list", true);
        if (paletteFoldout)
        {
            var prop = so.FindProperty(ListFieldName(layer));
            EditorGUILayout.PropertyField(prop, true);
            if (GUILayout.Button("Auto-fill palette from project"))
            {
                so.ApplyModifiedProperties();
                AutoFillPalette();
                so.Update();
            }
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
            if (EditorUtility.DisplayDialog("Clear layer", "Remove everything on the " + layer + " layer? (You can undo this.)", "Clear", "Cancel"))
                ClearLayer();
        }

        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }

    private void DrawPalette()
    {
        int count = PaletteCount();
        if (count == 0)
        {
            EditorGUILayout.HelpBox("This palette is empty. Open 'Edit palette list' below and drag tiles/prefabs in, or press Auto-fill.", MessageType.Info);
            return;
        }

        previewsPending = false;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / 62f));
        int i = 0;
        while (i < count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns && i < count; c++, i++)
            {
                Object asset = PaletteAt(i);
                Texture preview = null;
                if (asset != null)
                {
                    preview = AssetPreview.GetAssetPreview(asset);
                    if (preview == null)
                    {
                        // Unity generates previews in the background; show a placeholder and retry shortly.
                        preview = AssetPreview.GetMiniThumbnail(asset);
                        previewsPending = true;
                    }
                }

                bool selected = selectedByLayer[(int)layer] == i;
                var content = new GUIContent(preview, asset != null ? asset.name : "(empty slot)");
                bool pressed = GUILayout.Toggle(selected, content, "Button", GUILayout.Width(56), GUILayout.Height(56));
                if (pressed && !selected) selectedByLayer[(int)layer] = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        Object current = SelectedAsset();
        EditorGUILayout.LabelField("Selected: " + (current != null ? current.name : "none"), EditorStyles.miniLabel);
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

        if (!TryGetCell(e.mousePosition, out Vector3Int cell)) return;
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
        if (plane.Raycast(ray, out float distance))
        {
            cell = grid.WorldToCell(ray.GetPoint(distance));
            cell.z = 0;
            return true;
        }
        cell = default;
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
        GetHoverBounds(cell, out Vector3Int min, out Vector3Int max);

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
        if (groundTilemap != null) EditorSceneManager.MarkSceneDirty(groundTilemap.gameObject.scene);
    }

    private void ApplyAtHover(Vector3Int cell, bool erase)
    {
        GetHoverBounds(cell, out Vector3Int min, out Vector3Int max);
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
        Tilemap map = TargetTilemap(layer);
        if (map == null) return;

        if (erase)
        {
            SetTile(map, cell, null);

            // Keep the layers consistent: no soil without ground, no water without soil.
            if (layer == FarmLayer.Ground)
            {
                SetTile(soilTilemap, cell, null);
                SetTile(waterTilemap, cell, null);
            }
            else if (layer == FarmLayer.Soil)
            {
                SetTile(waterTilemap, cell, null);
            }
            return;
        }

        var tile = SelectedAsset() as TileBase;
        if (tile == null) return;

        if (restrictToValidCells)
        {
            if (layer == FarmLayer.Soil && (groundTilemap == null || groundTilemap.GetTile(cell) == null)) return;
            if (layer == FarmLayer.Water && (soilTilemap == null || soilTilemap.GetTile(cell) == null)) return;
        }

        SetTile(map, cell, tile);
    }

    private void SetTile(Tilemap map, Vector3Int cell, TileBase tile)
    {
        if (map == null || map.GetTile(cell) == tile) return;

        if (registeredThisStroke.Add(map))
            Undo.RegisterCompleteObjectUndo(map, "Farm Level Edit");

        map.SetTile(cell, tile);
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

    private List<Transform> ObjectsAt(Vector3Int cell)
    {
        var found = new List<Transform>();
        if (objectsParent == null || CurrentGrid == null) return found;

        foreach (Transform child in objectsParent)
        {
            Vector3Int childCell = CurrentGrid.WorldToCell(child.position);
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
        instance.transform.position = CurrentGrid.GetCellCenterWorld(cell);
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
        if (tile == null || groundTilemap == null)
        {
            EditorUtility.DisplayDialog("Fill ground", "Select a ground tile in the palette first.", "OK");
            return;
        }

        BeginStroke();
        int startX = -fillWidth / 2;
        int startY = -fillHeight / 2;
        for (int x = 0; x < fillWidth; x++)
            for (int y = 0; y < fillHeight; y++)
                SetTile(groundTilemap, new Vector3Int(startX + x, startY + y, 0), tile);
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
                foreach (var child in children) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
        else
        {
            ClearMap(TargetTilemap(layer));
            if (layer == FarmLayer.Ground)
            {
                ClearMap(soilTilemap);
                ClearMap(waterTilemap);
            }
            else if (layer == FarmLayer.Soil)
            {
                ClearMap(waterTilemap);
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
        foreach (var map in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            string n = map.name.ToLowerInvariant();
            if (groundTilemap == null && n.Contains("ground")) groundTilemap = map;
            else if (soilTilemap == null && n.Contains("soil")) soilTilemap = map;
            else if (waterTilemap == null && n.Contains("water")) waterTilemap = map;
        }

        if (objectsParent == null)
        {
            GameObject found = GameObject.Find("Objects");
            if (found != null) objectsParent = found.transform;
        }
    }

    private Tilemap TargetTilemap(FarmLayer l)
    {
        switch (l)
        {
            case FarmLayer.Ground: return groundTilemap;
            case FarmLayer.Soil: return soilTilemap;
            case FarmLayer.Water: return waterTilemap;
            default: return null;
        }
    }

    private static string ListFieldName(FarmLayer l)
    {
        switch (l)
        {
            case FarmLayer.Ground: return "groundTiles";
            case FarmLayer.Soil: return "soilTiles";
            case FarmLayer.Water: return "waterTiles";
            default: return "objectPrefabs";
        }
    }

    private int PaletteCount()
    {
        switch (layer)
        {
            case FarmLayer.Ground: return groundTiles.Count;
            case FarmLayer.Soil: return soilTiles.Count;
            case FarmLayer.Water: return waterTiles.Count;
            default: return objectPrefabs.Count;
        }
    }

    private Object PaletteAt(int index)
    {
        switch (layer)
        {
            case FarmLayer.Ground: return groundTiles[index];
            case FarmLayer.Soil: return soilTiles[index];
            case FarmLayer.Water: return waterTiles[index];
            default: return objectPrefabs[index];
        }
    }

    private Object SelectedAsset()
    {
        int index = selectedByLayer[(int)layer];
        return index >= 0 && index < PaletteCount() ? PaletteAt(index) : null;
    }

    /// <summary>Fills the current layer's palette with every matching asset found in the project.</summary>
    private void AutoFillPalette()
    {
        if (layer == FarmLayer.Objects)
        {
            objectPrefabs.Clear();
            string[] searchFolders = AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")
                ? new[] { "Assets/_Project/Prefabs" }
                : new[] { "Assets" };

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", searchFolders))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) objectPrefabs.Add(prefab);
            }
            return;
        }

        List<TileBase> target = layer == FarmLayer.Ground ? groundTiles : layer == FarmLayer.Soil ? soilTiles : waterTiles;
        target.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:TileBase"))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null) target.Add(tile);
        }
    }
}
