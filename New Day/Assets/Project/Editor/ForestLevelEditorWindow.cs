using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Level Editors > Forest Level Editor
/// A simple paint tool for the forest scene, so you can build anything OUTSIDE (or between) the maze rooms: ground, thorn walls, trees, signs, ponds.
///   Ground   paints floor tiles (forest floors, grove floors, and any tile labelled FarmLayer_Ground)
///   Walls    paints thorn walls (they block movement)
///   Objects  places prefabs (trees, bushes, signs, stumps, fairy bits, shadow animals... anything labelled FarmLayer_Objects)
/// No rules about where you can paint. Tools: Paint, Erase, Rectangle. Shift = erase. [ and ] change brush size. Pick the Move/Rect tool (not the Hand) in the Scene view.
/// Press 'Set up this scene' once per scene. Must live in a folder named "Editor".
/// </summary>
public class ForestLevelEditorWindow : EditorWindow
{
    private enum Layer { Ground, Walls, Objects }
    private enum Brush { Paint, Erase, Rectangle }

    [SerializeField] private Tilemap ground, walls;
    [SerializeField] private Transform objectsParent;
    [SerializeField] private Layer layer = Layer.Ground;
    [SerializeField] private Brush tool = Brush.Paint;
    [SerializeField] private int brushSize = 1;
    [SerializeField] private int objectAngle = 0;          // how new objects are turned when placed: 0, 90, 180 or 270
    [SerializeField] private bool active = true;
    [SerializeField] private int[] selected = new int[3];

    private readonly List<Object>[] palettes = { new List<Object>(), new List<Object>(), new List<Object>() };
    private Vector2 scroll;
    private bool dragging;
    private Vector3Int rectStart;
    private readonly HashSet<Vector3Int> placedThisStroke = new HashSet<Vector3Int>();
    private bool undoRegistered;

    [MenuItem("Farm/Level Editors/Forest Level Editor", false, 2)]
    public static void Open()
    {
        var w = GetWindow<ForestLevelEditorWindow>("Forest Level Editor");
        w.minSize = new Vector2(300f, 420f);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FindLayers();
        BuildPalettes();
    }

    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; dragging = false; }
    private void OnHierarchyChange() { if (ground == null) { FindLayers(); Repaint(); } }
    private void OnFocus() { if (ground == null) FindLayers(); }

    // ------------------------------------------------------------------ scene layers
    private void FindLayers()
    {
        GameObject grid = GameObject.Find("ForestEditGrid");
        if (grid != null)
        {
            Transform g = grid.transform.Find("Ground"), w = grid.transform.Find("Walls");
            if (w == null) w = grid.transform.Find("Fences");   // made by Farm > Level Editors > Make Forest Editor Layers
            ground = g != null ? g.GetComponent<Tilemap>() : null;
            walls = w != null ? w.GetComponent<Tilemap>() : null;
        }
        else { ground = null; walls = null; }
        GameObject o = GameObject.Find("Objects");
        objectsParent = o != null ? o.transform : null;
    }

    private void SetUpScene()
    {
        GameObject grid = GameObject.Find("ForestEditGrid");
        if (grid == null)
        {
            grid = new GameObject("ForestEditGrid");
            Undo.RegisterCreatedObjectUndo(grid, "Forest edit layers");
            grid.AddComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);
        }
        if (grid.transform.Find("Ground") == null) NewMap(grid, "Ground", -10, false);
        if (grid.transform.Find("Walls") == null) NewMap(grid, "Walls", 5, true);
        if (GameObject.Find("Objects") == null) Undo.RegisterCreatedObjectUndo(new GameObject("Objects"), "Objects parent");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        FindLayers();
    }

    private static void NewMap(GameObject grid, string name, int order, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        if (collider) go.AddComponent<TilemapCollider2D>();
    }

    // ------------------------------------------------------------------ palettes
    private void BuildPalettes()
    {
        MakeForestTiles();
        try { ForestToolsMenu.EnsureSignPrefab(); } catch (System.Exception) { }
        try { BarrierPrefabMenu.EnsureBarriers(); } catch (System.Exception) { }

        Fill(Layer.Ground, "l:" + FarmEditorLabels.Ground + " t:TileBase");
        Fill(Layer.Walls, "l:" + FarmEditorLabels.Fences + " t:TileBase");
        Fill(Layer.Objects, "l:" + FarmEditorLabels.Objects + " t:Prefab");
        for (int i = 0; i < 3; i++) if (selected[i] >= palettes[i].Count) selected[i] = 0;
    }

    private void Fill(Layer l, string query)
    {
        List<Object> list = palettes[(int)l];
        list.Clear();
        foreach (string guid in AssetDatabase.FindAssets(query))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object a = l == Layer.Objects ? (Object)AssetDatabase.LoadAssetAtPath<GameObject>(path) : AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (a != null) list.Add(a);
        }
        list.Sort((x, y) => string.Compare(x.name, y.name, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Makes Tile assets out of the forest floor and wall pictures (once), labelled so they appear in the palettes.</summary>
    private static void MakeForestTiles()
    {
        string dir = ForestSetupMenu.DataFolder + "/Tiles";
        foreach (string name in new[] { "floor_dark_0", "floor_dark_1", "floor_dark_2", "floor_grove_0", "floor_grove_1", "floor_grove_2", "wall_thorn" })
        {
            string tilePath = dir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null) continue;
            Sprite sprite = null;
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == name) { sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p); break; }
            }
            if (sprite == null) continue;
            EnsureFolder(dir);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = name == "wall_thorn" ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SetLabels(tile, new[] { name == "wall_thorn" ? FarmEditorLabels.Fences : FarmEditorLabels.Ground });
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ------------------------------------------------------------------ window
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (Application.isPlaying) EditorGUILayout.HelpBox("Painting is off in Play mode.", MessageType.Info);

        if (ground == null || walls == null || objectsParent == null)
        {
            EditorGUILayout.HelpBox("This scene doesn't have the forest edit layers yet (a Ground tilemap, a Walls tilemap and an 'Objects' parent).", MessageType.Warning);
            if (GUILayout.Button("Set up this scene", GUILayout.Height(30))) SetUpScene();
            EditorGUILayout.EndScrollView();
            return;
        }

        active = EditorGUILayout.ToggleLeft("Painting active (turn off to select things normally)", active);
        layer = (Layer)GUILayout.SelectionGrid((int)layer, new[] { "Ground", "Walls", "Objects" }, 3);
        tool = (Brush)GUILayout.SelectionGrid((int)tool, new[] { "Paint", "Erase", "Rectangle" }, 3);
        brushSize = EditorGUILayout.IntSlider("Brush size", brushSize, 1, 8);
        if (layer == Layer.Objects)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Placed turned " + objectAngle + "\u00B0", GUILayout.Width(120));
                if (GUILayout.Button("Turn left (Shift+R)")) { objectAngle = (objectAngle + 270) % 360; Repaint(); }
                if (GUILayout.Button("Turn right (R)")) { objectAngle = (objectAngle + 90) % 360; Repaint(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rotate selected left")) ObjectRotation.RotateSelected(90f);
                if (GUILayout.Button("Rotate selected right")) ObjectRotation.RotateSelected(-90f);
            }
            EditorGUILayout.HelpBox("R turns what you place. T turns the object under the mouse (Shift+T the other way).", MessageType.None);
        }
        EditorGUILayout.HelpBox("Shift = erase. [ and ] = brush size. Use the Move tool (W) in the Scene view, not the Hand. Alt + mouse moves the camera.", MessageType.None);

        List<Object> items = palettes[(int)layer];
        if (items.Count == 0)
            EditorGUILayout.HelpBox("This palette is empty. Press 'Reload palettes'. If it is still empty, the project has no assets with the label for this layer (Ground: FarmLayer_Ground, Walls: FarmLayer_Fences, Objects: FarmLayer_Objects). Run Farm > Forest > Set Up Forest first.", MessageType.Warning);
        else DrawPalette(items);

        EditorGUILayout.Space();
        if (GUILayout.Button("Reload palettes")) BuildPalettes();
        EditorGUILayout.EndScrollView();
    }

    private void DrawPalette(List<Object> items)
    {
        int sel = selected[(int)layer];
        if (PaletteBrowser.Draw(items, ref sel, "forest_" + layer, position.width)) selected[(int)layer] = sel;
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    // ------------------------------------------------------------------ painting
    private void OnSceneGUI(SceneView sv)
    {
        if (!active || Application.isPlaying || ground == null) return;
        if (Tools.current == UnityEditor.Tool.View) return;
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
        if (e.alt) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftBracket) { brushSize = Mathf.Max(1, brushSize - 1); Repaint(); e.Use(); }
            else if (e.keyCode == KeyCode.RightBracket) { brushSize = Mathf.Min(8, brushSize + 1); Repaint(); e.Use(); }
        }

        Grid grid = ground.layoutGrid;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var plane = new Plane(Vector3.forward, grid.transform.position);
        float dist;
        if (!plane.Raycast(ray, out dist)) return;
        Vector3Int cell = grid.WorldToCell(ray.GetPoint(dist)); cell.z = 0;
        bool erase = tool == Brush.Erase || e.shift;

        if (e.type == EventType.KeyDown && layer == Layer.Objects)
        {
            if (e.keyCode == KeyCode.R) { objectAngle = (objectAngle + (e.shift ? 270 : 90)) % 360; Repaint(); sv.Repaint(); e.Use(); }
            else if (e.keyCode == KeyCode.T)
            {
                foreach (Transform t in ObjectRotation.At(objectsParent, grid, cell, true))
                    ObjectRotation.Rotate(t.gameObject, e.shift ? -90f : 90f);
                sv.Repaint(); e.Use();
            }
        }

        Vector3Int min, max;
        Bounds(cell, out min, out max);
        if (e.type == EventType.Repaint)
        {
            Vector3 a = grid.CellToWorld(min), b = grid.CellToWorld(new Vector3Int(max.x + 1, max.y + 1, 0));
            Handles.DrawSolidRectangleWithOutline(new[] { a, new Vector3(a.x, b.y, a.z), b, new Vector3(b.x, a.y, a.z) },
                erase ? new Color(1f, 0.25f, 0.25f, 0.25f) : new Color(0.3f, 1f, 0.4f, 0.25f), Color.white);
        }
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();
        if (e.button != 0) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Forest paint");
                dragging = true; rectStart = cell; placedThisStroke.Clear(); undoRegistered = false;
                if (tool != Brush.Rectangle) Apply(min, max, erase);
                e.Use(); break;
            case EventType.MouseDrag:
                if (dragging) { if (tool != Brush.Rectangle) Apply(min, max, erase); e.Use(); }
                break;
            case EventType.MouseUp:
                if (dragging)
                {
                    if (tool == Brush.Rectangle) Apply(min, max, erase);
                    dragging = false;
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);
                    e.Use();
                }
                break;
        }
    }

    private void Bounds(Vector3Int cell, out Vector3Int min, out Vector3Int max)
    {
        if (tool == Brush.Rectangle && dragging)
        {
            min = new Vector3Int(Mathf.Min(rectStart.x, cell.x), Mathf.Min(rectStart.y, cell.y), 0);
            max = new Vector3Int(Mathf.Max(rectStart.x, cell.x), Mathf.Max(rectStart.y, cell.y), 0);
            return;
        }
        int n = tool == Brush.Rectangle ? 1 : brushSize;
        int low = -(n - 1) / 2;
        min = new Vector3Int(cell.x + low, cell.y + low, 0);
        max = new Vector3Int(min.x + n - 1, min.y + n - 1, 0);
    }

    private void Apply(Vector3Int min, Vector3Int max, bool erase)
    {
        Object asset = selected[(int)layer] < palettes[(int)layer].Count ? palettes[(int)layer][selected[(int)layer]] : null;
        Tilemap map = layer == Layer.Walls ? walls : ground;
        if (layer != Layer.Objects && !undoRegistered && map != null) { Undo.RegisterCompleteObjectUndo(map, "Forest paint"); undoRegistered = true; }

        for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                var c = new Vector3Int(x, y, 0);
                if (layer == Layer.Objects)
                {
                    if (erase) EraseObjectsAt(c);
                    else if (asset is GameObject && placedThisStroke.Add(c)) PlaceObject((GameObject)asset, c);
                }
                else if (map != null)
                {
                    if (erase) map.SetTile(c, null);
                    else if (asset is TileBase) map.SetTile(c, (TileBase)asset);
                }
            }
    }

    private void PlaceObject(GameObject prefab, Vector3Int cell)
    {
        if (objectsParent == null) return;
        Vector3 p = ground.layoutGrid.GetCellCenterWorld(cell);
        p.y -= ground.layoutGrid.cellSize.y * 0.5f;           // objects stand on the bottom edge of the cell
        p.z = 0f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Place object");
        go.transform.SetParent(objectsParent, true);
        go.transform.position = p;
        ObjectRotation.RotateNew(go, objectAngle);
    }

    private void EraseObjectsAt(Vector3Int cell)
    {
        if (objectsParent == null) return;
        // the object standing in the cell AND any bigger object (a boulder, a turned prop) whose picture covers it
        foreach (Transform t in ObjectRotation.At(objectsParent, ground.layoutGrid, cell, true))
            Undo.DestroyObjectImmediate(t.gameObject);
    }
}
