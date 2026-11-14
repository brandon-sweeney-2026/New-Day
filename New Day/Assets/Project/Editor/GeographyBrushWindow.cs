using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The GEOGRAPHY BRUSH: one stroke paints a whole landscape feature.
/// Menu: Farm > Level Editors > Geography Brush.   This file must live in a folder named "Editor".
///
/// Pick a geography (Mountain Pathway, Road, Sidewalk, Forest Pathway...), pick a width from 1 to 10 tiles, and drag in the Scene view
/// (or click points for straight lines). The stroke is painted as ONE shape:
///   - the outline gets proper edge and corner tiles, the inside gets a random mix of ground variants
///   - crossings join up with paths of the same kind that are already there
///   - the width can wobble for natural paths, roads get a dashed centre line
///   - extras are scattered by chance: pebbles, cracks, snow, roots, lamp posts, boulders, trees, flowers...
/// Hold SHIFT while you paint to rub the path out again. Ctrl+Z undoes a whole stroke.
/// Keys:  [ and ]  change the width.   Enter / double-click / right-click  finishes a straight line.   Esc cancels.
/// </summary>
public class GeographyBrushWindow : EditorWindow
{
    private enum DrawMode { Freehand, StraightLines, Rectangle }

    [MenuItem("Farm/Level Editors/Geography Brush (Paths, Roads, Sidewalks)", false, 5)]
    public static void Open()
    {
        var w = GetWindow<GeographyBrushWindow>("Geography Brush");
        w.minSize = new Vector2(320f, 460f);
    }

    // ------------------------------------------------------------------ state
    [SerializeField] private GeographyPreset preset;
    [SerializeField] private int width = 3;
    [SerializeField] private DrawMode mode = DrawMode.Freehand;
    [SerializeField] private bool active = true;
    [SerializeField] private bool placeObjectsAtCellBottom = true;
    [SerializeField] private bool onlyOverGround = false;
    [SerializeField] private Tilemap groundMap, pathMap, decorMap, cliffMap;
    [SerializeField] private Transform objectsParent;

    private GeographyPreset[] allPresets = new GeographyPreset[0];
    private string[] presetNames = new string[0];
    private Vector2 scroll;
    private bool foldLayers, foldPreset;
    private Editor presetEditor;

    // stroke
    private bool drawing, strokeErase;
    private Vector3Int rectStart, rectEnd;                                   // rectangle mode corners
    private readonly List<Vector3Int> centre = new List<Vector3Int>();    // freehand: the traced cells
    private readonly List<Vector3Int> points = new List<Vector3Int>();    // straight lines: the clicked corners
    private int seed;

    private static readonly Vector3Int[] Dir4 = { new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0) };

    /// <summary>The tilemap the surface is painted on: Paths normally, Ground for geographies like the dock.</summary>
    private Tilemap SurfaceMap
    {
        get
        {
            if (preset != null && preset.style == GeographyPreset.Style.Cliff && cliffMap != null) return cliffMap;
            return preset != null && preset.paintOnGroundLayer && groundMap != null ? groundMap : pathMap;
        }
    }

    private bool IsCliff { get { return preset != null && preset.style == GeographyPreset.Style.Cliff; } }

    private Grid CurrentGrid
    {
        get
        {
            if (groundMap != null) return groundMap.layoutGrid;
            if (pathMap != null) return pathMap.layoutGrid;
            return null;
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        RefreshPresets();
        if (groundMap == null) FindLayers();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        drawing = false;
        if (presetEditor != null) DestroyImmediate(presetEditor);
    }

    private void OnFocus() { RefreshPresets(); if (groundMap == null) FindLayers(); }
    private void OnProjectChange() { RefreshPresets(); Repaint(); }

    private void RefreshPresets()
    {
        string[] guids = AssetDatabase.FindAssets("t:GeographyPreset");
        var list = new List<GeographyPreset>();
        foreach (string g in guids)
        {
            var p = AssetDatabase.LoadAssetAtPath<GeographyPreset>(AssetDatabase.GUIDToAssetPath(g));
            if (p != null) list.Add(p);
        }
        list.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
        allPresets = list.ToArray();
        presetNames = new string[allPresets.Length];
        for (int i = 0; i < allPresets.Length; i++) presetNames[i] = allPresets[i].title;
        if (preset == null && allPresets.Length > 0) SelectPreset(allPresets[0]);
    }

    private void SelectPreset(GeographyPreset p)
    {
        preset = p;
        if (p != null) width = Mathf.Clamp(p.defaultWidth, 1, 10);
    }

    // ------------------------------------------------------------------ scene layers
    private void FindLayers()
    {
        groundMap = null; pathMap = null; decorMap = null; cliffMap = null;
        foreach (Tilemap map in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            string n = map.name.ToLowerInvariant();
            if (n == "pathdecor") { if (decorMap == null) decorMap = map; }
            else if (n == "cliffs") { if (cliffMap == null) cliffMap = map; }
            else if (groundMap == null && n.Contains("ground") && !n.Contains("pollution")) groundMap = map;
            else if (pathMap == null && (n.Contains("path") || n.Contains("overlay"))) pathMap = map;
        }
        if (objectsParent == null)
        {
            GameObject o = GameObject.Find("Objects");
            if (o != null) objectsParent = o.transform;
        }
    }

    /// <summary>Makes the Paths and PathDecor layers if the scene does not have them yet. Returns false if there is no ground to build on.</summary>
    private bool EnsureLayers()
    {
        if (groundMap == null && pathMap == null) FindLayers();
        Grid grid = CurrentGrid;
        if (grid == null)
        {
            EditorUtility.DisplayDialog("Geography Brush", "No ground tilemap was found in this scene. Open the scene's level editor first (Farm > Level Editors > Open Level Editor), or for the forest run Make Forest Editor Layers.", "OK");
            return false;
        }
        int baseOrder = 0; int layerId = 0;
        if (groundMap != null)
        {
            var r = groundMap.GetComponent<TilemapRenderer>();
            if (r != null) { baseOrder = r.sortingOrder; layerId = r.sortingLayerID; }
        }
        if (pathMap == null) pathMap = NewMap(grid, "Paths", baseOrder + 1, layerId);
        if (IsCliff && cliffMap == null)
        {
            // solid rock: its own layer above the ground and paths, with a collider the player cannot walk through
            cliffMap = NewMap(grid, "Cliffs", Mathf.Max(baseOrder + 2, GetOrder(pathMap) + 1), layerId);
            cliffMap.gameObject.AddComponent<TilemapCollider2D>();
        }
        if (decorMap == null) decorMap = NewMap(grid, "PathDecor", Mathf.Max(baseOrder + 2, GetOrder(pathMap) + 1, GetOrder(cliffMap) + 1), layerId);
        else if (cliffMap != null && GetOrder(decorMap) <= GetOrder(cliffMap))
        {
            var dr = decorMap.GetComponent<TilemapRenderer>();
            if (dr != null) dr.sortingOrder = GetOrder(cliffMap) + 1;      // decor (moss, snow, rubble) must draw on top of the rock
        }
        return true;
    }

    private static int GetOrder(Tilemap m)
    {
        var r = m != null ? m.GetComponent<TilemapRenderer>() : null;
        return r != null ? r.sortingOrder : 0;
    }

    private static Tilemap NewMap(Grid grid, string name, int order, int layerId)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(grid.transform, false);
        var map = go.AddComponent<Tilemap>();
        var rend = go.AddComponent<TilemapRenderer>();
        rend.sortingOrder = order;
        rend.sortingLayerID = layerId;
        return map;
    }

    // ------------------------------------------------------------------ window UI
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Pick a geography and a width, then drag in the Scene view. SHIFT = rub out. [ ] = width.", MessageType.None);

        active = EditorGUILayout.ToggleLeft("Painting is ON (turn off to select things normally)", active);

        if (allPresets.Length == 0)
        {
            EditorGUILayout.HelpBox("No geography presets yet.", MessageType.Warning);
            if (GUILayout.Button("Make the 4 default geographies")) { GeographyPresetMaker.EnsureAll(); RefreshPresets(); }
            EditorGUILayout.EndScrollView();
            return;
        }

        int index = Mathf.Max(0, Array.IndexOf(allPresets, preset));
        int picked = EditorGUILayout.Popup("Geography", index, presetNames);
        if (picked != index || preset == null) SelectPreset(allPresets[picked]);

        width = EditorGUILayout.IntSlider("Width (tiles)", width, 1, 10);
        if (mode == DrawMode.Rectangle) EditorGUILayout.HelpBox("Rectangle mode ignores the width: drag a box of any size. Good for filling a whole mountain side.", MessageType.None);
        mode = (DrawMode)GUILayout.Toolbar((int)mode, new[] { "Freehand (drag)", "Straight lines (click)", "Rectangle (drag)" });

        if (preset != null)
        {
            int tiles = CountTiles(preset);
            if (tiles == 0)
            {
                EditorGUILayout.HelpBox("This geography has no tiles. Run Farm > Biomes > Build Path Palettes, then press Reset below.", MessageType.Warning);
            }
            else if (preset.fillTiles == null || preset.fillTiles.Length == 0)
            {
                EditorGUILayout.HelpBox("No ground variants set, the inside will use the Middle tile.", MessageType.Info);
            }
            if (IsCliff) EditorGUILayout.HelpBox("Cliff style: paints SOLID rock on a Cliffs layer (made automatically with a collider). Use Rectangle mode for big areas. The bottom 2 rows of each shape become the wall.", MessageType.Info);
            EditorGUILayout.LabelField("Variable features", preset.scatter != null ? preset.scatter.Length + " rules" : "none");
        }

        EditorGUILayout.Space(4);
        placeObjectsAtCellBottom = EditorGUILayout.ToggleLeft("Place prefabs at the bottom of their cell (like the Level Editor)", placeObjectsAtCellBottom);
        onlyOverGround = EditorGUILayout.ToggleLeft("Only paint where the Ground layer has a tile", onlyOverGround);

        foldLayers = EditorGUILayout.Foldout(foldLayers, "Scene layers", true);
        if (foldLayers)
        {
            groundMap = (Tilemap)EditorGUILayout.ObjectField("Ground", groundMap, typeof(Tilemap), true);
            pathMap = (Tilemap)EditorGUILayout.ObjectField("Paths", pathMap, typeof(Tilemap), true);
            decorMap = (Tilemap)EditorGUILayout.ObjectField("PathDecor", decorMap, typeof(Tilemap), true);
            cliffMap = (Tilemap)EditorGUILayout.ObjectField("Cliffs (solid)", cliffMap, typeof(Tilemap), true);
            objectsParent = (Transform)EditorGUILayout.ObjectField("Objects", objectsParent, typeof(Transform), true);
            if (GUILayout.Button("Find layers in the open scene")) FindLayers();
        }
        else if (CurrentGrid == null)
        {
            EditorGUILayout.HelpBox("No ground tilemap found in this scene yet. Open 'Scene layers' and press Find, or open this scene's level editor first.", MessageType.Warning);
        }

        foldPreset = EditorGUILayout.Foldout(foldPreset, "Edit this geography (tiles, chances, objects)", true);
        if (foldPreset && preset != null)
        {
            Editor.CreateCachedEditor(preset, null, ref presetEditor);
            if (presetEditor != null) presetEditor.OnInspectorGUI();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = GeographyPresetMaker.HasDef(preset.paletteGroup);
                if (GUILayout.Button("Reset this geography to defaults"))
                {
                    if (EditorUtility.DisplayDialog("Reset", "Put '" + preset.title + "' back to its default tiles and chances? Your changes to it are lost.", "Reset", "Cancel"))
                    { GeographyPresetMaker.ResetPreset(preset); Repaint(); }
                }
                GUI.enabled = true;
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static int CountTiles(GeographyPreset p)
    {
        int n = 0;
        if (p.fillTiles != null) foreach (TileBase t in p.fillTiles) if (t != null) n++;
        if (p.wideTiles != null) foreach (TileBase t in p.wideTiles) if (t != null) n++;
        if (p.autoTile != null) n++;
        if (p.style == GeographyPreset.Style.Cliff)
        {
            n += NonNull(p.cliffFaceUpper).Count + NonNull(p.cliffFaceLower).Count + NonNull(p.cliffLip).Count;
        }
        return n;
    }

    // ------------------------------------------------------------------ scene view
    private bool TryGetCell(Vector2 guiPosition, out Vector3Int cell)
    {
        cell = Vector3Int.zero;
        Grid grid = CurrentGrid;
        if (grid == null) return false;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var plane = new Plane(grid.transform.forward, grid.transform.position);
        float dist;
        if (!plane.Raycast(ray, out dist)) return false;
        cell = grid.WorldToCell(ray.GetPoint(dist));
        cell.z = 0;
        return true;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!active || Application.isPlaying || preset == null || CurrentGrid == null) return;
        if (Tools.current == UnityEditor.Tool.View) return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);
        if (e.alt) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftBracket) { width = Mathf.Max(1, width - 1); Repaint(); sceneView.Repaint(); e.Use(); }
            else if (e.keyCode == KeyCode.RightBracket) { width = Mathf.Min(10, width + 1); Repaint(); sceneView.Repaint(); e.Use(); }
            else if (e.keyCode == KeyCode.Escape && (points.Count > 0 || drawing)) { CancelStroke(); sceneView.Repaint(); e.Use(); }
            else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && mode == DrawMode.StraightLines && points.Count > 0)
            { FinishStraight(); sceneView.Repaint(); e.Use(); }
        }

        Vector3Int cell;
        if (!TryGetCell(e.mousePosition, out cell)) return;

        if (e.type == EventType.Repaint) DrawPreview(cell);
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sceneView.Repaint();

        // right-click finishes a straight line
        if (e.type == EventType.MouseDown && e.button == 1 && mode == DrawMode.StraightLines && points.Count > 0)
        {
            FinishStraight(); e.Use(); sceneView.Repaint(); return;
        }
        if (e.button != 0) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (mode == DrawMode.Rectangle)
                {
                    rectStart = cell; rectEnd = cell;
                    seed = UnityEngine.Random.Range(1, int.MaxValue);
                    strokeErase = e.shift;
                    drawing = true;
                }
                else if (mode == DrawMode.Freehand)
                {
                    centre.Clear();
                    centre.Add(cell);
                    seed = UnityEngine.Random.Range(1, int.MaxValue);
                    strokeErase = e.shift;
                    drawing = true;
                }
                else
                {
                    if (points.Count == 0) { seed = UnityEngine.Random.Range(1, int.MaxValue); strokeErase = e.shift; }
                    if (points.Count == 0 || points[points.Count - 1] != cell) points.Add(cell);
                    else if (e.clickCount >= 2) { FinishStraight(); sceneView.Repaint(); e.Use(); break; }
                }
                sceneView.Repaint();
                e.Use();
                break;

            case EventType.MouseDrag:
                if (drawing && mode == DrawMode.Rectangle)
                {
                    rectEnd = cell; sceneView.Repaint(); e.Use();
                }
                else if (drawing && mode == DrawMode.Freehand)
                {
                    AppendLine(centre, centre[centre.Count - 1], cell);
                    sceneView.Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (drawing && mode == DrawMode.Rectangle)
                {
                    drawing = false;
                    Commit(new List<Vector3Int> { rectStart }, strokeErase, seed, RectCells(rectStart, rectEnd));
                    sceneView.Repaint();
                    e.Use();
                }
                else if (drawing && mode == DrawMode.Freehand)
                {
                    drawing = false;
                    Commit(new List<Vector3Int>(centre), strokeErase, seed);
                    centre.Clear();
                    sceneView.Repaint();
                    e.Use();
                }
                break;
        }
    }

    private static HashSet<Vector3Int> RectCells(Vector3Int a, Vector3Int b)
    {
        var set = new HashSet<Vector3Int>();
        for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                set.Add(new Vector3Int(x, y, 0));
        return set;
    }

    private void CancelStroke() { drawing = false; centre.Clear(); points.Clear(); }

    private void FinishStraight()
    {
        List<Vector3Int> line = BuildStraightCentre(null);
        Commit(line, strokeErase, seed);
        points.Clear();
    }

    private List<Vector3Int> BuildStraightCentre(Vector3Int? hover)
    {
        var line = new List<Vector3Int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0) line.Add(points[0]);
            else AppendLine(line, points[i - 1], points[i]);
        }
        if (hover.HasValue && points.Count > 0) AppendLine(line, points[points.Count - 1], hover.Value);
        return line;
    }

    /// <summary>Adds every cell on the straight line from 'a' to 'b' (not including 'a') so fast mouse moves leave no gaps.</summary>
    private static void AppendLine(List<Vector3Int> list, Vector3Int a, Vector3Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (x0 != x1 || y0 != y1)
        {
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
            list.Add(new Vector3Int(x0, y0, 0));
        }
    }

    private void DrawPreview(Vector3Int hover)
    {
        List<Vector3Int> trace;
        int useSeed = seed;
        bool erase = strokeErase || Event.current.shift;
        if (mode == DrawMode.Freehand && drawing) trace = centre;
        else if (mode == DrawMode.StraightLines && points.Count > 0) trace = BuildStraightCentre(hover);
        else { trace = new List<Vector3Int> { hover }; useSeed = 1; }

        HashSet<Vector3Int> shape;
        if (mode == DrawMode.Rectangle) shape = drawing ? RectCells(rectStart, rectEnd) : new HashSet<Vector3Int> { hover };
        else shape = BuildShape(trace, width, useSeed);
        Color fill = erase ? new Color(1f, 0.25f, 0.2f, 0.28f) : new Color(0.25f, 0.8f, 1f, 0.28f);
        Color line = erase ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(0.3f, 0.9f, 1f, 0.9f);
        Grid grid = CurrentGrid;
        Vector3 half = new Vector3(grid.cellSize.x, grid.cellSize.y, 0f) * 0.5f;
        foreach (Vector3Int c in shape)
        {
            Vector3 p = grid.GetCellCenterWorld(c);
            Handles.DrawSolidRectangleWithOutline(new[] {
                p + new Vector3(-half.x, -half.y), p + new Vector3(-half.x, half.y), p + new Vector3(half.x, half.y), p + new Vector3(half.x, -half.y) },
                fill, line);
        }
        Handles.Label(grid.GetCellCenterWorld(hover) + new Vector3(0.6f, 0.9f, 0f), (erase ? "Erase " : "") + preset.title + "  width " + width);
    }

    // ------------------------------------------------------------------ shape
    private static readonly Dictionary<long, List<Vector2Int>> stampCache = new Dictionary<long, List<Vector2Int>>();

    private static List<Vector2Int> Stamp(int w, GeographyPreset.BrushShape shape)
    {
        long key = w * 10 + (int)shape;
        List<Vector2Int> list;
        if (stampCache.TryGetValue(key, out list)) return list;
        list = new List<Vector2Int>();
        int lo = -((w - 1) / 2), hi = w / 2;
        float c = (lo + hi) * 0.5f;
        float r = w * 0.5f;
        float lim = r * r + 0.25f;
        for (int x = lo; x <= hi; x++)
            for (int y = lo; y <= hi; y++)
            {
                if (shape == GeographyPreset.BrushShape.Square) { list.Add(new Vector2Int(x, y)); continue; }
                float dx = x - c, dy = y - c;
                if (dx * dx + dy * dy <= lim) list.Add(new Vector2Int(x, y));
            }
        stampCache[key] = list;
        return list;
    }

    private int WidthAt(int i, int baseWidth, int rngSeed)
    {
        int wobble = preset.widthWobble;
        if (wobble <= 0) return baseWidth;
        float n = Mathf.PerlinNoise(i * 0.07f + (rngSeed % 1000) * 0.37f, 3.1f);
        int d = Mathf.RoundToInt((n - 0.5f) * 2f * wobble * 1.5f);
        return Mathf.Clamp(baseWidth + d, Mathf.Max(1, baseWidth - wobble), Mathf.Min(10, baseWidth + wobble));
    }

    private HashSet<Vector3Int> BuildShape(List<Vector3Int> trace, int baseWidth, int rngSeed)
    {
        var set = new HashSet<Vector3Int>();
        for (int i = 0; i < trace.Count; i++)
        {
            List<Vector2Int> stamp = Stamp(WidthAt(i, baseWidth, rngSeed), preset.shape);
            Vector3Int c = trace[i];
            foreach (Vector2Int o in stamp) set.Add(new Vector3Int(c.x + o.x, c.y + o.y, 0));
        }
        return set;
    }

    // ------------------------------------------------------------------ painting
    private HashSet<TileBase> ownTiles;
    private HashSet<Tilemap> undoRegistered;

    private HashSet<TileBase> BuildOwnTiles()
    {
        var set = new HashSet<TileBase>();
        if (preset.fillTiles != null) foreach (TileBase t in preset.fillTiles) if (t != null) set.Add(t);
        if (preset.wideTiles != null) foreach (TileBase t in preset.wideTiles) if (t != null) set.Add(t);
        if (preset.innerTiles != null) foreach (TileBase t in preset.innerTiles) if (t != null) set.Add(t);
        if (preset.autoTile != null) set.Add(preset.autoTile);
        if (preset.style == GeographyPreset.Style.Cliff)
        {
            foreach (TileBase[] arr in new[] { preset.cliffFaceUpper, preset.cliffFaceLower, preset.cliffLip, preset.cliffInner, preset.cliffFaceEdges })
                if (arr != null) foreach (TileBase t in arr) if (t != null) set.Add(t);
        }
        return set;
    }

    private bool Owns(Vector3Int c)
    {
        TileBase t = SurfaceMap.GetTile(c);
        return t != null && ownTiles.Contains(t);
    }

    private void SetTile(Tilemap map, Vector3Int c, TileBase tile)
    {
        if (map == null || map.GetTile(c) == tile) return;
        if (undoRegistered.Add(map)) Undo.RegisterCompleteObjectUndo(map, "Geography Brush");
        map.SetTile(c, tile);
        EditorUtility.SetDirty(map);
        // neighbouring auto-tiles (water!) must redraw when a ground tile changes next to them
        if (map == groundMap)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) map.RefreshTile(new Vector3Int(c.x + dx, c.y + dy, 0));
    }

    private void Commit(List<Vector3Int> trace, bool erase, int rngSeed, HashSet<Vector3Int> shapeOverride = null)
    {
        if (trace == null || trace.Count == 0 || preset == null) return;
        if (!EnsureLayers()) return;
        if (!erase && CountTiles(preset) == 0)
        {
            EditorUtility.DisplayDialog("Geography Brush", "This geography has no tiles yet. Run Farm > Biomes > Build Path Palettes, then press 'Reset this geography to defaults' in the Geography Brush window.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Geography Brush");
        int undoGroup = Undo.GetCurrentGroup();
        undoRegistered = new HashSet<Tilemap>();
        ownTiles = BuildOwnTiles();
        if (preset.paintOnGroundLayer && groundMap == null)
        {
            EditorUtility.DisplayDialog("Geography Brush", "This geography paints on the Ground layer, but no Ground tilemap was found in this scene.", "OK");
            return;
        }
        var rng = new System.Random(rngSeed);

        HashSet<Vector3Int> S = shapeOverride != null ? shapeOverride : BuildShape(trace, width, rngSeed);
        if (onlyOverGround && groundMap != null) S.RemoveWhere(c => groundMap.GetTile(c) == null);

        if (erase) DoErase(S, rng);
        else DoPaint(S, trace, rng);

        Undo.CollapseUndoOperations(undoGroup);
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // ---- paint
    private void DoPaint(HashSet<Vector3Int> S, List<Vector3Int> trace, System.Random rng)
    {
        bool merge = preset.mergeWithExisting;
        if (IsCliff && preset.roughEdges > 0f) Chew(S, rng);
        Func<Vector3Int, bool> member = c => S.Contains(c) || (merge && Owns(c));
        var fills = NonNull(preset.fillTiles);

        var core = new List<Vector3Int>();
        var edge = new List<Vector3Int>();
        foreach (Vector3Int c in S)
        {
            TileBase tile = TileFor(c, member, fills, rng);
            SetTile(SurfaceMap, c, tile);
            if (IsEdge(c, member)) edge.Add(c); else core.Add(c);
        }

        // existing cells touching the new shape may have to change their edge pieces (crossings)
        if (merge) RetileRing(S, member, fills, rng);

        // the shoulder: cells just outside the path
        var shoulder = new List<Vector3Int>();
        var seen = new HashSet<Vector3Int>();
        var frontier = new List<Vector3Int>(S);
        for (int step = 0; step < preset.shoulderWidth; step++)
        {
            var next = new List<Vector3Int>();
            foreach (Vector3Int c in frontier)
                foreach (Vector3Int d in Dir4)
                {
                    Vector3Int n = c + d;
                    if (S.Contains(n) || seen.Contains(n) || member(n)) continue;
                    seen.Add(n); shoulder.Add(n); next.Add(n);
                }
            frontier = next;
        }
        if (onlyOverGround && groundMap != null) shoulder.RemoveAll(c => groundMap.GetTile(c) == null);

        // variable features
        HashSet<Vector2Int> occupied = null;
        if (preset.scatter != null)
            foreach (GeographyPreset.ScatterRule rule in preset.scatter)
                Scatter(rule, core, edge, shoulder, rng, ref occupied);

        // centre line
        if (width >= preset.centerMinWidth && preset.centerHorizontal != null && preset.centerVertical != null)
            CentreLine(trace, S);
    }

    /// <summary>Cliffs: randomly bites single cells off the top and sides of the shape (never the bottom) so the rock is not a perfect block.</summary>
    private void Chew(HashSet<Vector3Int> S, System.Random rng)
    {
        if (S.Count < 12) return;
        var bite = new List<Vector3Int>();
        foreach (Vector3Int c in S)
        {
            bool top = !S.Contains(c + Dir4[0]), left = !S.Contains(c + Dir4[3]), right = !S.Contains(c + Dir4[2]);
            if (!(top || left || right)) continue;
            // keep it chunky: never bite the bottom two rows (the wall)
            if (!S.Contains(c + new Vector3Int(0, -3, 0)) && !S.Contains(c + new Vector3Int(0, -2, 0)) ) continue;
            if (rng.NextDouble() < preset.roughEdges * 0.5) bite.Add(c);
        }
        foreach (Vector3Int c in bite) S.Remove(c);
    }

    private static List<TileBase> NonNull(TileBase[] arr)
    {
        var list = new List<TileBase>();
        if (arr != null) foreach (TileBase t in arr) if (t != null) list.Add(t);
        return list;
    }

    private static bool IsEdge(Vector3Int c, Func<Vector3Int, bool> member)
    {
        foreach (Vector3Int d in Dir4) if (!member(c + d)) return true;
        return false;
    }

    private TileBase Wide(int i) { return preset.wideTiles != null && i < preset.wideTiles.Length ? preset.wideTiles[i] : null; }
    private TileBase Inner(int i) { return preset.innerTiles != null && i < preset.innerTiles.Length ? preset.innerTiles[i] : null; }

    /// <summary>Which tile belongs on a path cell, from which of its neighbours are also path.</summary>
    private TileBase TileFor(Vector3Int c, Func<Vector3Int, bool> member, List<TileBase> fills, System.Random rng)
    {
        bool n = member(c + Dir4[0]), s = member(c + Dir4[1]), e = member(c + Dir4[2]), w = member(c + Dir4[3]);
        TileBase middle = Wide(4);
        TileBase t;

        if (IsCliff) return CliffTileFor(c, member, fills, rng, n, s, e, w);

        // some geographies (docks) are plain ground variants everywhere, no edge pieces
        if (!preset.useEdgeTiles)
        {
            t = RandomFill(fills, rng);
            return t != null ? t : middle;
        }

        // thin bits (one cell wide): the self-connecting tile
        if ((!n && !s) || (!e && !w))
            return preset.autoTile != null ? preset.autoTile : (middle != null ? middle : RandomFill(fills, rng));

        if (!n) t = !w ? Wide(0) : (!e ? Wide(2) : Wide(1));
        else if (!s) t = !w ? Wide(6) : (!e ? Wide(8) : Wide(7));
        else if (!w) t = Wide(3);
        else if (!e) t = Wide(5);
        else
        {
            // surrounded on four sides: maybe an inner corner where a diagonal neighbour is missing
            bool ne = member(c + new Vector3Int(1, 1, 0)), nw = member(c + new Vector3Int(-1, 1, 0));
            bool se = member(c + new Vector3Int(1, -1, 0)), sw = member(c + new Vector3Int(-1, -1, 0));
            int corner = -1;
            if (!nw) corner = 0; else if (!ne) corner = 1; else if (!sw) corner = 2; else if (!se) corner = 3;
            if (corner >= 0 && preset.swapInnerCorners) corner = 3 - corner;
            t = corner >= 0 ? Inner(corner) : null;
            if (t == null) t = RandomFill(fills, rng);
            if (t == null) t = middle;
            return t;
        }
        if (t == null) t = RandomFill(fills, rng);
        if (t == null) t = middle;
        return t;
    }

    private static TileBase Pick(TileBase[] arr, int i)
    {
        return arr != null && i >= 0 && i < arr.Length ? arr[i] : null;
    }

    /// <summary>
    /// Cliff pieces: the bottom two rows of the shape are the rock WALL (lower = foot, upper = wall), the rest is the plateau top:
    /// a lit rim on the top and sides, inner corners in notches, plain rock inside.
    /// </summary>
    private TileBase CliffTileFor(Vector3Int c, Func<Vector3Int, bool> member, List<TileBase> fills, System.Random rng, bool n, bool s, bool e, bool w)
    {
        TileBase t;
        bool lower = !s;
        bool upper = s && !member(c + new Vector3Int(0, -2, 0));
        if (lower || upper)
        {
            TileBase[] variants = lower ? preset.cliffFaceLower : preset.cliffFaceUpper;
            int edgeBase = lower ? 2 : 0;
            if (!w && e) t = Pick(preset.cliffFaceEdges, edgeBase);
            else if (!e && w) t = Pick(preset.cliffFaceEdges, edgeBase + 1);
            else t = null;
            if (t == null && variants != null && variants.Length > 0) t = variants[rng.Next(variants.Length)];
            if (t == null) t = RandomFill(fills, rng);
            return t;
        }
        if (!n)
        {
            if (!w && e) t = Pick(preset.cliffLip, 3);          // top-left
            else if (!e && w) t = Pick(preset.cliffLip, 4);     // top-right
            else t = Pick(preset.cliffLip, 0);                   // top
        }
        else if (!w && e) t = Pick(preset.cliffLip, 1);
        else if (!e && w) t = Pick(preset.cliffLip, 2);
        else
        {
            t = null;
            if (!member(c + new Vector3Int(-1, 1, 0))) t = Pick(preset.cliffInner, 0);
            else if (!member(c + new Vector3Int(1, 1, 0))) t = Pick(preset.cliffInner, 1);
        }
        if (t == null) t = RandomFill(fills, rng);
        return t;
    }

    private static TileBase RandomFill(List<TileBase> fills, System.Random rng)
    {
        return fills.Count == 0 ? null : fills[rng.Next(fills.Count)];
    }

    /// <summary>After painting or erasing, fixes the edge pieces of already-painted path cells right next to the changed area.</summary>
    private void RetileRing(HashSet<Vector3Int> changed, Func<Vector3Int, bool> member, List<TileBase> fills, System.Random rng)
    {
        var ring = new HashSet<Vector3Int>();
        foreach (Vector3Int c in changed)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector3Int(c.x + dx, c.y + dy, 0);
                    if (!changed.Contains(n) && member(n) && Owns(n)) ring.Add(n);
                }
        foreach (Vector3Int c in ring)
        {
            bool inside = !IsEdge(c, member);
            TileBase current = SurfaceMap.GetTile(c);
            TileBase wanted = TileFor(c, member, fills, rng);
            if (inside && current != null && fills.Contains(current) && fills.Contains(wanted)) continue;   // keep the random ground variant it already has
            SetTile(SurfaceMap, c, wanted);
        }
    }

    // ---- erase
    private void DoErase(HashSet<Vector3Int> S, System.Random rng)
    {
        var removedObjects = new List<GameObject>();
        HashSet<Vector2Int> none = null;
        foreach (Vector3Int c in S)
        {
            if (Owns(c)) SetTile(SurfaceMap, c, preset.paintOnGroundLayer ? GroundBehind(c, S) : null);
            if (decorMap != null && decorMap.GetTile(c) != null) SetTile(decorMap, c, null);
        }
        if (objectsParent != null && CurrentGrid != null)
        {
            foreach (Transform child in objectsParent)
            {
                Vector3 p = child.position;
                if (placeObjectsAtCellBottom) p.y += CurrentGrid.cellSize.y * 0.5f;
                Vector3Int cell = CurrentGrid.WorldToCell(p); cell.z = 0;
                if (S.Contains(cell)) removedObjects.Add(child.gameObject);
            }
            foreach (GameObject go in removedObjects) Undo.DestroyObjectImmediate(go);
        }
        Func<Vector3Int, bool> member = c => !S.Contains(c) && Owns(c);
        RetileRing(S, member, NonNull(preset.fillTiles), rng);
        if (none != null) none.Clear();
    }

    /// <summary>When a ground-layer surface (dock) is rubbed out, fill the hole with the commonest ground around it (water, grass...).</summary>
    private TileBase GroundBehind(Vector3Int c, HashSet<Vector3Int> erased)
    {
        var counts = new Dictionary<TileBase, int>();
        TileBase best = null; int bestCount = 0;
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                var n = new Vector3Int(c.x + dx, c.y + dy, 0);
                if (erased.Contains(n)) continue;
                TileBase t = groundMap.GetTile(n);
                if (t == null || ownTiles.Contains(t)) continue;
                int k; counts.TryGetValue(t, out k); counts[t] = ++k;
                if (k > bestCount) { bestCount = k; best = t; }
            }
        return best;
    }

    // ---- scatter
    private void Scatter(GeographyPreset.ScatterRule rule, List<Vector3Int> core, List<Vector3Int> edge, List<Vector3Int> shoulder, System.Random rng, ref HashSet<Vector2Int> occupied)
    {
        if (rule == null || rule.assets == null || rule.assets.Length == 0 || rule.chance <= 0f || width < rule.minBrushWidth) return;
        var assets = new List<UnityEngine.Object>();
        foreach (UnityEngine.Object a in rule.assets) if (a is TileBase || a is GameObject) assets.Add(a);
        if (assets.Count == 0) return;

        var cells = new List<Vector3Int>();
        switch (rule.zone)
        {
            case GeographyPreset.Zone.PathCore: cells.AddRange(core); break;
            case GeographyPreset.Zone.PathEdge: cells.AddRange(edge); break;
            case GeographyPreset.Zone.Shoulder: cells.AddRange(shoulder); break;
            default: cells.AddRange(core); cells.AddRange(edge); cells.AddRange(shoulder); break;
        }
        // shuffle so spacing rules do not favour one corner
        for (int i = cells.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); Vector3Int t = cells[i]; cells[i] = cells[j]; cells[j] = t; }

        var placed = new List<Vector3Int>();
        foreach (Vector3Int c in cells)
        {
            if (rng.NextDouble() > rule.chance) continue;
            if (rule.minSpacing > 0 && TooClose(c, placed, rule.minSpacing)) continue;

            UnityEngine.Object pick = assets[rng.Next(assets.Count)];
            if (pick is TileBase)
            {
                if (decorMap.GetTile(c) != null) continue;
                SetTile(decorMap, c, (TileBase)pick);
                placed.Add(c);
            }
            else
            {
                if (occupied == null) occupied = OccupiedCells();
                var key = new Vector2Int(c.x, c.y);
                if (occupied.Contains(key)) continue;
                PlacePrefab((GameObject)pick, c);
                occupied.Add(key);
                placed.Add(c);
            }
        }
    }

    private static bool TooClose(Vector3Int c, List<Vector3Int> placed, int spacing)
    {
        foreach (Vector3Int p in placed)
            if (Mathf.Abs(p.x - c.x) <= spacing && Mathf.Abs(p.y - c.y) <= spacing) return true;
        return false;
    }

    private HashSet<Vector2Int> OccupiedCells()
    {
        var set = new HashSet<Vector2Int>();
        if (objectsParent == null || CurrentGrid == null) return set;
        foreach (Transform child in objectsParent)
        {
            Vector3 p = child.position;
            if (placeObjectsAtCellBottom) p.y += CurrentGrid.cellSize.y * 0.5f;
            Vector3Int cell = CurrentGrid.WorldToCell(p);
            set.Add(new Vector2Int(cell.x, cell.y));
        }
        return set;
    }

    private void PlacePrefab(GameObject prefab, Vector3Int cell)
    {
        if (objectsParent == null)
        {
            var go = new GameObject("Objects");
            Undo.RegisterCreatedObjectUndo(go, "Create Objects Parent");
            objectsParent = go.transform;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, objectsParent);
        Vector3 p = CurrentGrid.GetCellCenterWorld(cell);
        if (placeObjectsAtCellBottom) p.y -= CurrentGrid.cellSize.y * 0.5f;
        instance.transform.position = p;
        Undo.RegisterCreatedObjectUndo(instance, "Geography Brush");
    }

    // ---- centre line
    private void CentreLine(List<Vector3Int> trace, HashSet<Vector3Int> S)
    {
        int on = Mathf.Max(1, preset.dashOn), off = Mathf.Max(0, preset.dashOff);
        int cycle = on + off;
        for (int i = 0; i < trace.Count; i++)
        {
            if (cycle > 0 && (i % cycle) >= on) continue;
            Vector3Int c = trace[i];
            if (!S.Contains(c)) continue;
            Vector3Int a = trace[Mathf.Max(0, i - 1)], b = trace[Mathf.Min(trace.Count - 1, i + 1)];
            bool horizontal = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y);
            SetTile(decorMap, c, horizontal ? preset.centerHorizontal : preset.centerVertical);
        }
    }
}
