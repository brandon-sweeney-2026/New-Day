using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Level Editors > Interior Decorator
///
/// The catalogue of furniture, windows, pictures, floors and wall coverings, and the tools to redecorate a room:
///   1. Select anything inside a room in the Hierarchy (or click it in the Scene view). The window shows that room.
///   2. Pick a category and click a picture to start placing it. Move the mouse over the room in the Scene view and click to put it down.
///      R turns it (Shift+R the other way). Esc or a right-click stops placing.
///   3. Click a placed piece to select it: R / Shift+R turn it (you see its side or back), "Swap for" changes it to another piece
///      of the same kind in the same spot, Delete removes it. Drag it with the Move tool; it snaps to the tiles when you let go.
///   4. Change the floor, the floor of each patch (kitchen, bathroom...) and the wall covering at the top of the window.
///   5. "Restyle the whole room" swaps every piece, the floor and the walls to one theme (Cottage, Modern, Rustic, Retro, Coastal).
/// Every change is saved to the room's layout asset straight away (Assets/Farm/Interiors/Layouts), so rebuilding the Suburbs scene keeps it.
/// Ctrl+Z undoes. This file must live in a folder named "Editor".
/// </summary>
public class InteriorDecoratorWindow : EditorWindow
{
    private DecorRoom room;
    private HouseInterior bareInterior;          // a selected room that is not decoratable yet
    private DecorCategory category = DecorCategory.Couch;
    private int themeFilter;                     // 0 = all, then the themes, then "any"
    private Vector2 scroll;
    private DecorItem placing;
    private DecorFacing placeFacing = DecorFacing.Front;
    private GameObject ghost;
    private bool layoutDirty;
    private bool snapDirty;

    private static readonly string[] ThemeTabs = { "All", "Cottage", "Modern", "Rustic", "Retro", "Coastal", "Other" };
    private const float Thumb = 72f;

    [MenuItem("Farm/Level Editors/Interior Decorator", false, 20)]
    public static void Open()
    {
        var w = GetWindow<InteriorDecoratorWindow>("Interior Decorator");
        w.minSize = new Vector2(340f, 420f);
        w.PickRoomFromSelection();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        Undo.postprocessModifications += OnModifications;
        PickRoomFromSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        Undo.postprocessModifications -= OnModifications;
        StopPlacing();
    }

    private void OnSelectionChanged() { PickRoomFromSelection(); Repaint(); }
    private void OnUndoRedo() { layoutDirty = true; Repaint(); }
    private void OnHierarchyChanged() { if (room != null) layoutDirty = true; }

    private void Update()
    {
        // save after pieces were deleted / duplicated / undone, once the mouse is up
        if (layoutDirty && room != null && GUIUtility.hotControl == 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            layoutDirty = false;
            if (snapDirty) { snapDirty = false; SnapMovedPieces(); }
            InteriorDecorTools.SaveLayout(room);
        }
    }

    private void PickRoomFromSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return;
        HouseInterior hi = go.GetComponentInParent<HouseInterior>();
        if (hi == null) return;
        room = hi.GetComponent<DecorRoom>();
        bareInterior = room == null ? hi : null;
    }

    // ------------------------------------------------------------------ window

    private void OnGUI()
    {
        DecorCatalog catalog = InteriorDecorTools.Catalog;
        if (catalog == null || catalog.items.Count == 0)
        {
            EditorGUILayout.HelpBox("There is no decor catalogue yet. Draw the art with Tools/make_interior_art.ps1, then run Farm > Set Up > Interior Decor.", MessageType.Info);
            if (GUILayout.Button("Run Farm > Set Up > Interior Decor")) InteriorDecorSetupMenu.RunFromMenu();
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        RoomSection(catalog);
        if (room != null)
        {
            EditorGUILayout.Space(6);
            SelectedSection(catalog);
            EditorGUILayout.Space(6);
            CatalogueSection(catalog);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RoomSection(DecorCatalog catalog)
    {
        EditorGUILayout.LabelField("Room", EditorStyles.boldLabel);
        if (room == null)
        {
            if (bareInterior != null)
            {
                EditorGUILayout.HelpBox("'" + bareInterior.name + "' is not set up for decorating yet.", MessageType.Info);
                if (GUILayout.Button("Make '" + bareInterior.name + "' decoratable"))
                {
                    room = InteriorDecorTools.EnsureRoom(bareInterior, bareInterior.name);
                    InteriorDecorTools.LayoutFor(room, true);
                    InteriorDecorTools.SaveLayout(room);
                    InteriorDecorTools.BuildFrame(room);
                    bareInterior = null;
                }
            }
            else EditorGUILayout.HelpBox("Select something inside a room (the farmhouse, a suburb house, the Kims' apartment...) in the Hierarchy.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(room.name + "   (layout: " + room.roomId + ")");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Show room")) { Selection.activeGameObject = room.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
            if (GUILayout.Button("Rebuild frame")) InteriorDecorTools.BuildFrame(room);
            if (GUILayout.Button("Reload layout") && EditorUtility.DisplayDialog("Reload layout", "Throw away the pieces in the scene and build them again from the layout asset?", "Reload", "Cancel"))
                InteriorDecorTools.ApplyLayout(room, true);
        }

        InteriorLayout layout = InteriorDecorTools.LayoutFor(room, true);
        if (room.surfacesFromHouseManager)
            EditorGUILayout.HelpBox("This room's floor and walls are chosen at the design table in the game (the new floors and wall coverings are options there).", MessageType.None);
        else
        {
            DecorItem f = SurfacePopup("Floor", layout.floor, catalog.InCategory(DecorCategory.Flooring));
            DecorItem w = SurfacePopup("Walls", layout.wallCovering, catalog.InCategory(DecorCategory.WallCovering));
            if (f != layout.floor || w != layout.wallCovering)
            {
                Undo.RecordObject(layout, "Change room surfaces"); layout.floor = f; layout.wallCovering = w; EditorUtility.SetDirty(layout);
                InteriorDecorTools.PaintSurfaces(room);
            }
            var style = room.GetComponent<InteriorStyle>();
            if (style != null && style.zones != null)
                foreach (FloorZone z in style.zones)
                {
                    if (z == null) continue;
                    InteriorLayout.ZoneFloor zf = layout.zoneFloors.FirstOrDefault(x => x != null && x.zoneLabel == z.label);
                    DecorItem now = zf != null ? zf.floor : null;
                    DecorItem pick = SurfacePopup("   " + z.label, now, catalog.InCategory(DecorCategory.Flooring));
                    if (pick != now)
                    {
                        Undo.RecordObject(layout, "Change zone floor");
                        if (zf == null) { zf = new InteriorLayout.ZoneFloor { zoneLabel = z.label }; layout.zoneFloors.Add(zf); }
                        zf.floor = pick; EditorUtility.SetDirty(layout);
                        InteriorDecorTools.PaintSurfaces(room);
                    }
                }
        }

        EditorGUILayout.LabelField("Restyle the whole room", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
            for (int i = 0; i < DecorCatalog.Themes.Length; i++)
                if (GUILayout.Button(ThemeTabs[i + 1], EditorStyles.miniButton)) Restyle(catalog, DecorCatalog.Themes[i]);
    }

    private static DecorItem SurfacePopup(string label, DecorItem current, List<DecorItem> options)
    {
        var names = new List<string> { "(keep as it is)" };
        names.AddRange(options.Select(o => o.displayName));
        int index = current != null ? options.IndexOf(current) + 1 : 0;
        int pick = EditorGUILayout.Popup(label, index, names.ToArray());
        return pick <= 0 ? null : options[pick - 1];
    }

    private void SelectedSection(DecorCatalog catalog)
    {
        var pieces = Selection.gameObjects.Select(g => g.GetComponent<DecorPiece>()).Where(p => p != null).ToList();
        EditorGUILayout.LabelField("Selected piece", EditorStyles.boldLabel);
        if (pieces.Count == 0) { EditorGUILayout.LabelField("Click a piece in the Scene view.", EditorStyles.miniLabel); return; }

        DecorPiece first = pieces[0];
        EditorGUILayout.LabelField(pieces.Count == 1 && first.item != null ? first.item.displayName + "  -  facing " + first.facing : pieces.Count + " pieces");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Turn left (Shift+R)")) { foreach (var p in pieces) InteriorDecorTools.Turn(p, false); InteriorDecorTools.SaveLayout(room); }
            if (GUILayout.Button("Turn right (R)")) { foreach (var p in pieces) InteriorDecorTools.Turn(p, true); InteriorDecorTools.SaveLayout(room); }
            if (GUILayout.Button("Delete"))
            {
                foreach (var p in pieces) Undo.DestroyObjectImmediate(p.gameObject);
                InteriorDecorTools.SaveLayout(room);
                GUIUtility.ExitGUI();
            }
        }
        if (pieces.Count == 1 && first.item != null)
        {
            EditorGUILayout.LabelField("Swap for:", EditorStyles.miniBoldLabel);
            DecorItem chosen = Grid(catalog.InCategory(first.item.category), first.item);
            if (chosen != null && chosen != first.item) { InteriorDecorTools.Swap(first, chosen); InteriorDecorTools.SaveLayout(room); }
        }
    }

    private void CatalogueSection(DecorCatalog catalog)
    {
        EditorGUILayout.LabelField("Catalogue", EditorStyles.boldLabel);
        category = (DecorCategory)EditorGUILayout.EnumPopup("Category", category);
        themeFilter = GUILayout.Toolbar(themeFilter, ThemeTabs, EditorStyles.miniButton);

        if (placing != null)
            EditorGUILayout.HelpBox("Placing " + placing.displayName + " (facing " + placeFacing + "). Click in the room in the Scene view. R turns it, Esc stops.", MessageType.None);

        var items = catalog.InCategory(category).Where(PassesFilter).ToList();
        if (items.Count == 0) { EditorGUILayout.LabelField("(nothing here)", EditorStyles.miniLabel); return; }
        DecorItem clicked = Grid(items, placing);
        if (clicked == null) return;

        InteriorLayout layout = InteriorDecorTools.LayoutFor(room, true);
        if (clicked.category == DecorCategory.Flooring || clicked.category == DecorCategory.WallCovering)
        {
            if (room.surfacesFromHouseManager) { ShowNotification(new GUIContent("Floors and walls here come from the design table")); return; }
            Undo.RecordObject(layout, "Change room surfaces");
            if (clicked.category == DecorCategory.Flooring) layout.floor = clicked; else layout.wallCovering = clicked;
            EditorUtility.SetDirty(layout);
            InteriorDecorTools.PaintSurfaces(room);
        }
        else StartPlacing(clicked);
    }

    private bool PassesFilter(DecorItem i)
    {
        if (themeFilter == 0) return true;
        if (themeFilter == ThemeTabs.Length - 1) return !DecorCatalog.Themes.Contains(i.theme);
        return i.theme == DecorCatalog.Themes[themeFilter - 1];
    }

    /// <summary>A grid of pictures with names. Returns the one clicked, or null.</summary>
    private DecorItem Grid(List<DecorItem> items, DecorItem highlight)
    {
        int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / (Thumb + 6f)));
        DecorItem clicked = null;
        for (int i = 0; i < items.Count; i += cols)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int j = i; j < Mathf.Min(items.Count, i + cols); j++)
                {
                    DecorItem it = items[j];
                    Rect r = GUILayoutUtility.GetRect(Thumb, Thumb + 14f, GUILayout.Width(Thumb));
                    Rect pic = new Rect(r.x, r.y, r.width, Thumb);
                    EditorGUI.DrawRect(pic, it == highlight ? new Color(0.95f, 0.8f, 0.35f, 0.5f) : new Color(0f, 0f, 0f, 0.15f));
                    DrawSprite(new Rect(pic.x + 4, pic.y + 4, pic.width - 8, pic.height - 8), it.front);
                    GUI.Label(new Rect(r.x, r.y + Thumb, r.width, 14f), it.displayName, EditorStyles.centeredGreyMiniLabel);
                    if (GUI.Button(r, new GUIContent("", it.displayName + "\n" + it.description), GUIStyle.none)) clicked = it;
                }
        }
        return clicked;
    }

    private static void DrawSprite(Rect r, Sprite s)
    {
        if (s == null || s.texture == null) return;
        Texture2D t = s.texture; Rect tr = s.textureRect;
        float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
        if (k >= 1f) k = Mathf.Floor(k);
        var dr = new Rect(r.center.x - tr.width * k * 0.5f, r.center.y - tr.height * k * 0.5f, tr.width * k, tr.height * k);
        GUI.DrawTextureWithTexCoords(dr, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
    }

    private void Restyle(DecorCatalog catalog, string theme)
    {
        if (!EditorUtility.DisplayDialog("Restyle room", "Swap every themed piece in '" + room.name + "', its floor and its walls to the " + theme + " theme?", "Restyle", "Cancel")) return;
        Undo.SetCurrentGroupName("Restyle room");
        int group = Undo.GetCurrentGroup();
        foreach (DecorPiece p in room.PiecesRoot.GetComponentsInChildren<DecorPiece>())
        {
            if (p.item == null || !DecorCatalog.Themes.Contains(p.item.theme)) continue;      // bathroom pieces and other "any" pieces stay
            DecorItem other = catalog.Pick(p.item.category, theme);
            if (other != null && other != p.item && DecorCatalog.Themes.Contains(other.theme)) InteriorDecorTools.Swap(p, other);
        }
        InteriorLayout layout = InteriorDecorTools.LayoutFor(room, true);
        Undo.RecordObject(layout, "Restyle room");
        var b = new LayoutBuilder(layout, catalog, theme, 0, 0);
        if (!room.surfacesFromHouseManager)
        {
            layout.floor = catalog.ById(b.ThemeFloor);
            layout.wallCovering = catalog.ById(b.ThemeWall);
            foreach (InteriorLayout.ZoneFloor zf in layout.zoneFloors)
                if (zf != null && zf.zoneLabel.Contains("Kitchen")) zf.floor = catalog.ById(b.ThemeKitchenFloor);
        }
        EditorUtility.SetDirty(layout);
        InteriorDecorTools.SaveLayout(room);
        InteriorDecorTools.PaintSurfaces(room);
        Undo.CollapseUndoOperations(group);
    }

    // ------------------------------------------------------------------ placing in the Scene view

    private void StartPlacing(DecorItem item)
    {
        placing = item;
        if (!item.CanRotate) placeFacing = DecorFacing.Front;
        if (ghost == null)
        {
            ghost = new GameObject("Decor placing preview") { hideFlags = HideFlags.HideAndDontSave };
            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 200; sr.color = new Color(1f, 1f, 1f, 0.75f);
        }
        UpdateGhostSprite();
        SceneView.lastActiveSceneView?.Focus();
    }

    private void StopPlacing()
    {
        placing = null;
        if (ghost != null) DestroyImmediate(ghost);
        ghost = null;
        Repaint();
    }

    private void UpdateGhostSprite()
    {
        if (ghost == null || placing == null) return;
        ghost.GetComponent<SpriteRenderer>().sprite = placing.SpriteFor(placeFacing);
    }

    private void OnScene(SceneView view)
    {
        if (room == null) return;
        Event e = Event.current;

        if (placing != null)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Vector3 world = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            Vector3 local = room.transform.InverseTransformPoint(world);
            Vector2Int size = placing.SizeFor(placeFacing);
            bool half = placing.mount == DecorMount.Wall;
            Vector2 corner = InteriorDecorTools.Snap(new Vector2(local.x - size.x * 0.5f, local.y - size.y * 0.5f), half);
            Vector3 cornerWorld = room.transform.TransformPoint(new Vector3(corner.x, corner.y, 0f));

            if (ghost != null) ghost.transform.position = cornerWorld;
            Handles.DrawSolidRectangleWithOutline(new Rect(cornerWorld.x, cornerWorld.y, size.x, size.y), new Color(1f, 0.85f, 0.3f, 0.12f), new Color(1f, 0.85f, 0.3f, 0.9f));

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                DecorPiece p = InteriorDecorTools.Place(room, placing, corner, placeFacing, true);
                InteriorDecorTools.SaveLayout(room);
                if (p != null && !e.shift) Selection.activeGameObject = p.gameObject;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 1) { StopPlacing(); e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && !e.control && !e.command)
            {
                if (placing.CanRotate) { placeFacing = DecorItem.Turn(placeFacing, !e.shift); UpdateGhostSprite(); Repaint(); }
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopPlacing(); e.Use(); }
            if (e.type == EventType.MouseMove) view.Repaint();
            return;
        }

        // turning the selected pieces with R / Shift+R
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && !e.control && !e.command && !e.alt)
        {
            var pieces = Selection.gameObjects.Select(g => g.GetComponent<DecorPiece>()).Where(p => p != null).ToList();
            if (pieces.Count > 0)
            {
                foreach (var p in pieces) InteriorDecorTools.Turn(p, !e.shift);
                InteriorDecorTools.SaveLayout(room);
                Repaint();
                e.Use();
            }
        }

    }

    /// <summary>After pieces were dragged with the Move tool: snap them to the tiles (surface pieces back onto their counter).</summary>
    private void SnapMovedPieces()
    {
        foreach (DecorPiece p in room.PiecesRoot.GetComponentsInChildren<DecorPiece>())
        {
            if (p.item == null) continue;
            Vector2 lp = p.transform.localPosition;
            bool surface = p.item.mount == DecorMount.Surface;
            Vector2 snapped = InteriorDecorTools.Snap(lp, p.item.mount == DecorMount.Wall);
            if (surface && Mathf.Abs(lp.y - (Mathf.Round(lp.y) - 0.01f)) < 0.001f && Mathf.Abs(lp.x - snapped.x) < 0.001f) continue;   // already sitting right
            if (!surface && (snapped - lp).sqrMagnitude < 1e-6f) continue;
            Undo.RecordObject(p.transform, "Snap decor"); Undo.RecordObject(p, "Snap decor");
            p.transform.localPosition = new Vector3(snapped.x, snapped.y, 0f);
            InteriorDecorTools.SettleOnHost(room, p);
            p.Refresh();
        }
    }

    private UndoPropertyModification[] OnModifications(UndoPropertyModification[] mods)
    {
        foreach (UndoPropertyModification m in mods)
        {
            var t = m.currentValue != null ? m.currentValue.target as Transform : null;
            if (t != null && t.GetComponent<DecorPiece>() != null) { snapDirty = true; layoutDirty = true; }
        }
        return mods;
    }
}
