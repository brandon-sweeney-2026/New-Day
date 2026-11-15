using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools for building the walking network NPCs follow.
/// Farm > Level Editors:
///   Path Nodes - Add Node Here            a new PathNode at the middle of the Scene view (under a 'PathNetwork' object)
///   Path Nodes - Link Selected In Order   links the selected nodes in the order you clicked them (Ctrl+click each one)
///   Path Nodes - Unlink Selected          removes the links between the selected nodes
///   Path Nodes - Auto-Link Within Range   links nearby nodes that have a clear straight line between them (selected nodes, or all nodes if none selected)
///   Path Nodes - Make Portal Pair         select two nodes (a door's two sides): they become a door/stairs connection
/// Farm > Set Up:
///   Store Paths                           makes the nodes for the general store and the apartment (needs the General Store in the scene)
/// The Suburbs scene builder (Farm > Set Up > Suburbs Scene) runs Store Paths by itself and lays the street nodes.
/// Must live in a folder named "Editor".
/// </summary>
public static class PathToolsMenu
{
    private static Transform NetworkRoot(string name = "PathNetwork")
    {
        GameObject g = GameObject.Find(name);
        if (g == null) { g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Create " + name); }
        return g.transform;
    }

    public static PathNode MakeNode(string id, Vector2 pos, Transform parent)
    {
        var go = new GameObject(id);
        Undo.RegisterCreatedObjectUndo(go, "Add path node");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var n = go.AddComponent<PathNode>();
        n.nodeId = id;
        return n;
    }

    [MenuItem("Farm/Level Editors/Path Nodes - Add Node Here", false, 60)]
    public static void AddNode()
    {
        Vector3 p = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        p = new Vector3(Mathf.Round(p.x * 2f) / 2f, Mathf.Round(p.y * 2f) / 2f, 0f);
        Transform root = NetworkRoot();
        PathNode n = MakeNode("Node_" + root.childCount, p, root);
        Selection.activeGameObject = n.gameObject;
    }

    private static List<PathNode> Selected()
    {
        var list = new List<PathNode>();
        foreach (GameObject g in Selection.gameObjects)
        {
            var n = g.GetComponent<PathNode>();
            if (n != null) list.Add(n);
        }
        return list;
    }

    public static void Link(PathNode a, PathNode b)
    {
        if (a == null || b == null || a == b) return;
        foreach (PathNode x in a.links) if (x == b) return;
        foreach (PathNode x in b.links) if (x == a) return;
        Undo.RecordObject(a, "Link nodes");
        var l = new List<PathNode>(a.links) { b };
        a.links = l.ToArray();
        EditorUtility.SetDirty(a);
    }

    [MenuItem("Farm/Level Editors/Path Nodes - Link Selected In Order", false, 61)]
    public static void LinkChain()
    {
        List<PathNode> sel = Selected();
        if (sel.Count < 2) { EditorUtility.DisplayDialog("Select nodes", "Ctrl+click two or more PathNodes (in the order you want them linked), then run this.", "OK"); return; }
        for (int i = 0; i + 1 < sel.Count; i++) Link(sel[i], sel[i + 1]);
        PathNode.MarkDirty();
    }

    [MenuItem("Farm/Level Editors/Path Nodes - Unlink Selected", false, 62)]
    public static void Unlink()
    {
        List<PathNode> sel = Selected();
        foreach (PathNode a in sel)
        {
            Undo.RecordObject(a, "Unlink nodes");
            var keep = new List<PathNode>();
            foreach (PathNode x in a.links) if (x != null && !sel.Contains(x)) keep.Add(x);
            a.links = keep.ToArray();
            EditorUtility.SetDirty(a);
        }
        PathNode.MarkDirty();
    }

    [MenuItem("Farm/Level Editors/Path Nodes - Auto-Link Within Range", false, 63)]
    public static void AutoLink()
    {
        List<PathNode> nodes = Selected();
        if (nodes.Count < 2) nodes = new List<PathNode>(Object.FindObjectsByType<PathNode>());
        const float range = 6f;
        int made = 0;
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 a = nodes[i].transform.position, b = nodes[j].transform.position;
                if (Vector2.Distance(a, b) > range) continue;
                if (Blocked(a, b)) continue;
                int before = nodes[i].links.Length;
                Link(nodes[i], nodes[j]);
                if (nodes[i].links.Length > before) made++;
            }
        PathNode.MarkDirty();
        Debug.Log("Path nodes: auto-link made " + made + " new links (range " + range + " tiles, blocked by solid colliders).");
    }

    private static bool Blocked(Vector2 a, Vector2 b)
    {
        foreach (RaycastHit2D h in Physics2D.LinecastAll(a, b))
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (h.collider.GetComponentInParent<ScheduledNpc>() != null) continue;
            return true;
        }
        return false;
    }

    [MenuItem("Farm/Level Editors/Path Nodes - Make Portal Pair", false, 64)]
    public static void MakePortal()
    {
        List<PathNode> sel = Selected();
        if (sel.Count != 2) { EditorUtility.DisplayDialog("Select two nodes", "Select exactly two PathNodes: one on each side of a door or stairs.", "OK"); return; }
        Undo.RecordObjects(new Object[] { sel[0], sel[1] }, "Portal pair");
        sel[0].portalTo = sel[1]; sel[1].portalTo = sel[0];
        EditorUtility.SetDirty(sel[0]); EditorUtility.SetDirty(sel[1]);
        PathNode.MarkDirty();
    }

    // ------------------------------------------------------------------ the store and apartment network
    [MenuItem("Farm/Set Up/Store Paths", false, 147)]
    public static void BuildStorePathsFromMenu()
    {
        string result = BuildStorePaths();
        EditorUtility.DisplayDialog(result.StartsWith("Made") ? "Store paths built" : "Store paths", result, "OK");
    }

    /// <summary>
    /// Makes the path nodes for the general store and the apartment. Returns a sentence saying what happened
    /// (it starts with "Made" when it worked). No pop-ups, so the Suburbs scene builder can call it too.
    /// </summary>
    public static string BuildStorePaths()
    {
        GameObject ext = GameObject.Find("SuburbStore"), store = GameObject.Find("StoreInterior"), apt = GameObject.Find("ApartmentInterior");
        if (ext == null || store == null || apt == null)
            return "Needs 'SuburbStore', 'StoreInterior' and 'ApartmentInterior' in the open scene (Farm > Set Up > General Store).";
        if (GameObject.Find("PathNetwork_Store") != null)
            return "'PathNetwork_Store' already exists. Delete it to rebuild.";
        Transform root = NetworkRoot("PathNetwork_Store");
        Vector2 S = store.transform.position, A = apt.transform.position;
        Vector2 E = ext.transform.position;

        PathNode street = MakeNode("Street_StoreFront", E + new Vector2(0f, -1.4f), root);
        PathNode sDoor = MakeNode("Store_Door", S + new Vector2(10.5f, 1.5f), root);
        PathNode sFront = MakeNode("Store_Front", S + new Vector2(10.5f, 4.6f), root);
        PathNode sCheck = MakeNode("Store_Checkout", S + new Vector2(16.5f, 4.6f), root);
        PathNode sMid = MakeNode("Store_Mid", S + new Vector2(11f, 10.5f), root);
        PathNode sBack = MakeNode("Store_Back", S + new Vector2(11f, 15f), root);
        PathNode sSlush = MakeNode("Store_Slush", S + new Vector2(13.5f, 15.4f), root);
        PathNode sSeeds = MakeNode("Store_Seeds", S + new Vector2(16f, 11f), root);
        PathNode sLand = MakeNode("Store_StairsLanding", S + new Vector2(18.5f, 15.2f), root);
        Chain(sDoor, sFront, sCheck); Link(sFront, sMid); Link(sMid, sBack); Link(sBack, sSlush); Link(sBack, sLand); Link(sMid, sSeeds);
        Portal(street, sDoor);

        // where a customer stands: on the shopper's side of the checkout, facing the till (used by the neighbours' schedules)
        PathNode sCustomer = MakeNode("Store_Customer", S + new Vector2(16.5f, 1.3f), root);
        Link(sDoor, sCustomer);
        if (store.transform.Find("Spot_Customer") == null)
        {
            var spotGo = new GameObject("Spot_Customer");
            spotGo.transform.SetParent(store.transform, false);
            spotGo.transform.localPosition = new Vector3(16.5f, 1.3f, 0f);
            var spot = spotGo.AddComponent<NpcSpot>();
            spot.spotId = "StoreCustomer"; spot.facing = Vector2.up;
        }

        PathNode aEntry = MakeNode("Apt_Entry", A + new Vector2(10.5f, 1.4f), root);
        PathNode aMid = MakeNode("Apt_Mid", A + new Vector2(10f, 4.5f), root);
        PathNode aFrontL = MakeNode("Apt_LivingFront", A + new Vector2(6.5f, 4.5f), root);
        PathNode aLivCorner = MakeNode("Apt_LivingCorner", A + new Vector2(6.5f, 2.4f), root);
        PathNode aKit = MakeNode("Apt_Kitchen", A + new Vector2(14.5f, 5f), root);
        PathNode aDin = MakeNode("Apt_Dining", A + new Vector2(14.5f, 2f), root);
        PathNode aGap = MakeNode("Apt_HallGap", A + new Vector2(10f, 8.6f), root);
        PathNode aHall = MakeNode("Apt_Hall", A + new Vector2(10f, 10.5f), root);
        PathNode aHallW = MakeNode("Apt_HallWest", A + new Vector2(7f, 11.4f), root);
        PathNode aHallE = MakeNode("Apt_HallEast", A + new Vector2(13.5f, 11.2f), root);
        PathNode aBath1 = MakeNode("Apt_Bath1Door", A + new Vector2(4.5f, 11.8f), root);
        PathNode aBath2 = MakeNode("Apt_Bath2Door", A + new Vector2(15.5f, 11.8f), root);
        PathNode aB1h = MakeNode("Apt_Bed1Hall", A + new Vector2(7f, 13.4f), root);
        PathNode aB1d = MakeNode("Apt_Bed1Door", A + new Vector2(6f, 14.6f), root);
        PathNode aB2h = MakeNode("Apt_Bed2Hall", A + new Vector2(11f, 13f), root);
        PathNode aB2d = MakeNode("Apt_Bed2Door", A + new Vector2(11f, 14.6f), root);
        PathNode aB3in = MakeNode("Apt_Bath2In", A + new Vector2(17f, 13f), root);
        PathNode aB3d = MakeNode("Apt_Bed3Door", A + new Vector2(17f, 14.6f), root);
        Chain(aEntry, aMid, aKit); Link(aKit, aDin); Link(aMid, aFrontL); Link(aFrontL, aLivCorner); Link(aMid, aGap); Link(aGap, aHall);
        Link(aHall, aHallW); Link(aHall, aHallE); Link(aHallW, aBath1); Link(aHallE, aBath2);
        Chain(aHallW, aB1h, aB1d); Chain(aHall, aB2h, aB2d); Chain(aBath2, aB3in, aB3d);
        Portal(sLand, aEntry);

        // the apartment's doorways: make sure Bedroom 1's door is above the hall (x 5-6), not above the bathroom
        var style = apt.GetComponent<InteriorStyle>();
        if (style != null)
        {
            style.partitions = new[]
            {
                P("Living wall (left)", 0, 8, 9, 1), P("Living wall (right)", 11, 8, 9, 1),
                P("Bedroom wall A", 0, 14, 5, 1), P("Bedroom wall B", 7, 14, 3, 1), P("Bedroom wall C", 12, 14, 4, 1), P("Bedroom wall D", 18, 14, 2, 1),
                P("Bath 1 wall (low)", 4, 9, 1, 2), P("Bath 1 wall (high)", 4, 13, 1, 1),
                P("Bath 2 wall (low)", 15, 9, 1, 2), P("Bath 2 wall (high)", 15, 13, 1, 1),
                P("Bedroom 1|2", 7, 15, 1, 5), P("Bedroom 2|3", 14, 15, 1, 5),
            };
            style.Apply();
            EditorUtility.SetDirty(style);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Made 'PathNetwork_Store': the street in front of the store, the store aisles, the stairs and the whole apartment, with the front door and stairs as portals.\n\n" +
               "To let NPCs walk the suburb, make nodes along the street and link 'Street_StoreFront' to them (Farm > Level Editors > Path Nodes - Add Node Here, then Link Selected In Order). " +
               "The Suburbs scene builder does this for you.\n" +
               "The apartment's inside walls were also corrected so Bedroom 1's door opens to the hall.";
    }

    private static PartitionRect P(string l, int x, int y, int w, int h) { return new PartitionRect { label = l, area = new RectInt(x, y, w, h) }; }

    private static void Chain(params PathNode[] n) { for (int i = 0; i + 1 < n.Length; i++) Link(n[i], n[i + 1]); }

    private static void Portal(PathNode a, PathNode b)
    {
        a.portalTo = b; b.portalTo = a;
        EditorUtility.SetDirty(a); EditorUtility.SetDirty(b);
    }
}
