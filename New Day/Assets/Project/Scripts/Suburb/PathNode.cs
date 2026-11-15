using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A waypoint on the walking network. NPCs only ever walk from node to node along the links, so you decide exactly where they go.
///
/// - Put nodes along sidewalks, doorways, shop aisles... and LINK neighbours (the Farm > Level Editors > Path Nodes items do it for you: select nodes, "Link Selected In Order").
///   Links go both ways. A link means "a straight walk between these two is clear".
/// - "Portal To": a door or stairs. An NPC reaching this node steps straight out of the other node (use it for house doors and stairs).
/// - "Leads To Scene": a node at the edge of the map that leads to another scene (e.g. the road to the city). NPCs heading there walk to it and
///   vanish; NPCs arriving from that scene appear here.
/// Everything is drawn in the Scene view (yellow = node, cyan lines = links, magenta = portal, orange = scene exit).
/// </summary>
public class PathNode : MonoBehaviour
{
    [Tooltip("Unique name. Schedules can use it as a place. Leave empty to use the object's name.")]
    public string nodeId = "";
    [Tooltip("Links to neighbouring nodes. (The other node doesn't need to link back; it is automatic.)")]
    public PathNode[] links = new PathNode[0];
    [Tooltip("A door or stairs: the node on the other side.")]
    public PathNode portalTo;
    [Tooltip("An edge-of-map node: the name of the scene it leads to (e.g. City). Must be the same name the schedules use in 'Scene'.")]
    public string leadsToScene = "";

    public string Id { get { return string.IsNullOrEmpty(nodeId) ? name : nodeId; } }

    private static readonly List<PathNode> all = new List<PathNode>();
    public static IReadOnlyList<PathNode> All { get { return all; } }

    private readonly List<PathNode> neighbours = new List<PathNode>();
    private static bool dirty = true;

    private void Awake()
    {
        // the little marker picture used while designing is hidden when the game runs
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    private void OnEnable() { all.Add(this); dirty = true; }
    private void OnDisable() { all.Remove(this); dirty = true; }

    // ------------------------------------------------------------------ graph
    private static void Rebuild()
    {
        foreach (PathNode n in all) n.neighbours.Clear();
        foreach (PathNode n in all)
        {
            if (n.links != null)
                foreach (PathNode o in n.links)
                    if (o != null && o != n)
                    {
                        if (!n.neighbours.Contains(o)) n.neighbours.Add(o);
                        if (!o.neighbours.Contains(n)) o.neighbours.Add(n);
                    }
            if (n.portalTo != null)
            {
                if (!n.neighbours.Contains(n.portalTo)) n.neighbours.Add(n.portalTo);
                if (!n.portalTo.neighbours.Contains(n)) n.portalTo.neighbours.Add(n);
            }
        }
        dirty = false;
    }

    public static void MarkDirty() { dirty = true; }

    public static PathNode Find(string id)
    {
        foreach (PathNode n in all) if (n != null && n.Id == id) return n;
        return null;
    }

    public static PathNode FindExitTo(string scene)
    {
        foreach (PathNode n in all) if (n != null && !string.IsNullOrEmpty(n.leadsToScene) && n.leadsToScene == scene) return n;
        return null;
    }

    public static PathNode Nearest(Vector2 p)
    {
        PathNode best = null; float bd = float.MaxValue;
        foreach (PathNode n in all)
        {
            if (n == null) continue;
            float d = ((Vector2)n.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    /// <summary>One piece of a walk: a straight line, or a jump through a door / stairs.</summary>
    public struct Leg
    {
        public Vector2 from, to;
        public bool portal;
        public float Length { get { return portal ? 3f : Vector2.Distance(from, to); } }   // a door counts as a few tiles of walking
    }

    /// <summary>The legs of the shortest walk from 'a' to 'b'. Returns an empty list if there is no way (the caller should just teleport).</summary>
    public static List<Leg> Route(Vector2 a, Vector2 b, out bool found)
    {
        if (dirty) Rebuild();
        var legs = new List<Leg>();
        found = false;
        PathNode s = Nearest(a), e = Nearest(b);
        if (s == null || e == null) return legs;

        // Dijkstra over the nodes (they are few, so a simple list is fine)
        var dist = new Dictionary<PathNode, float>();
        var prev = new Dictionary<PathNode, PathNode>();
        var open = new List<PathNode>();
        dist[s] = 0f; open.Add(s);
        while (open.Count > 0)
        {
            PathNode cur = open[0]; float cd = dist[cur];
            foreach (PathNode o in open) if (dist[o] < cd) { cur = o; cd = dist[o]; }
            open.Remove(cur);
            if (cur == e) break;
            foreach (PathNode nb in cur.neighbours)
            {
                bool portal = cur.portalTo == nb || nb.portalTo == cur;
                float step = portal ? 3f : Vector2.Distance(cur.transform.position, nb.transform.position);
                float nd = cd + step;
                float old;
                if (!dist.TryGetValue(nb, out old) || nd < old)
                {
                    bool isNew = !dist.ContainsKey(nb);
                    dist[nb] = nd; prev[nb] = cur;
                    if (isNew || !open.Contains(nb)) open.Add(nb);
                }
            }
        }
        if (!dist.ContainsKey(e)) return legs;

        var nodes = new List<PathNode>();
        for (PathNode n = e; n != null; n = prev.ContainsKey(n) ? prev[n] : null) nodes.Add(n);
        nodes.Reverse();

        legs.Add(new Leg { from = a, to = nodes[0].transform.position });
        for (int i = 0; i + 1 < nodes.Count; i++)
        {
            PathNode x = nodes[i], y = nodes[i + 1];
            bool portal = x.portalTo == y || y.portalTo == x;
            legs.Add(new Leg { from = x.transform.position, to = y.transform.position, portal = portal });
        }
        legs.Add(new Leg { from = nodes[nodes.Count - 1].transform.position, to = b });
        found = true;
        return legs;
    }

    // ------------------------------------------------------------------ gizmos
    private void OnDrawGizmos()
    {
        bool exit = !string.IsNullOrEmpty(leadsToScene);
        Gizmos.color = exit ? new Color(1f, 0.55f, 0.1f) : portalTo != null ? new Color(1f, 0.3f, 1f) : new Color(1f, 0.9f, 0.2f);
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        if (links != null) foreach (PathNode o in links) if (o != null) Gizmos.DrawLine(transform.position, o.transform.position);
        if (portalTo != null) { Gizmos.color = new Color(1f, 0.3f, 1f, 0.8f); Gizmos.DrawLine(transform.position, portalTo.transform.position + Vector3.up * 0.05f); }
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, Id + (exitLabel()));
#endif
    }

    private string exitLabel() { return string.IsNullOrEmpty(leadsToScene) ? "" : "  -> " + leadsToScene; }
}
