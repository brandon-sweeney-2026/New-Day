using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The forest's "map" that the sick fairy reads from when you ask her for help. The maze generator makes one called "ForestGuide" and fills it in.
/// "Steps" is the main path in order: room, the gate that leads on from it, and its stump. "All Rooms" is every room (for finding the way).
/// If you build or change the forest by hand, fill these lists yourself. The fairy still works without it, but can't give directions.
/// </summary>
public class ForestGuide : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public ForestRoom room;
        public ForestDoor door;
        public ForestStump stump;
    }

    [Tooltip("The main path, first room first. Each step's gate leads to the next step's room (the last gate leads to the grove).")]
    public Step[] steps = new Step[0];
    [Tooltip("Every room of the forest, including the staging room and dead ends.")]
    public ForestRoom[] allRooms = new ForestRoom[0];

    private static readonly Vector2[] Dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private static readonly string[] Names = { "north (up)", "east (right)", "south (down)", "west (left)" };

    // ------------------------------------------------------------------ what she says
    /// <summary>The pages of dialogue for "Ask for help", based on where the player stands and which gates are still shut.</summary>
    public string[] Describe(Vector2 playerPos)
    {
        var pages = new List<string>();

        ForestGrove grove = FindAnyObjectByType<ForestGrove>();
        if (grove != null && grove.Contains(playerPos))
        {
            pages.Add("You're already in the grove! My sister should be near the fairy ring in the middle. Go and talk to her.");
            pages.Add("When you want to come back to me, the fairy door here will take you straight back to the first room.");
            return pages.ToArray();
        }

        int k = FirstLockedStep();
        ForestRoom here = RoomAt(playerPos);

        if (steps == null || steps.Length == 0)
        {
            pages.Add("I can feel the paths, but I can't tell which way is right. Keep going deeper, through the gates.");
            return pages.ToArray();
        }

        if (here == null)
        {
            ForestRoom near = Nearest(playerPos);
            pages.Add(near != null ? "You're outside the maze. Go " + Name(Compass(((Vector2)near.transform.position) - playerPos)) + " to find the rooms." : "I can't feel you in the maze. Find the rooms and I'll guide you.");
            return pages.ToArray();
        }

        if (k < 0)
        {
            // every gate is open
            Step last = steps[steps.Length - 1];
            if (here == last.room)
            {
                pages.Add("The last gate is open. Go through the " + Name(GateDir(last)) + " opening and you'll come out in the grove.");
            }
            else
            {
                AddRoute(pages, here, last.room, "The last gate is open. Make your way to the room at the end of the main path");
                pages.Add("From there, go " + Name(GateDir(last)) + " through the last gate to reach the grove, where my sister waits.");
            }
        }
        else
        {
            Step s = steps[k];
            if (here == s.room)
            {
                pages.Add("There's a thorny gate on the " + Name(GateDir(s)) + " side of this room, and the stump in here holds its key.");
            }
            else
            {
                AddRoute(pages, here, s.room, "The next stump is in another room");
            }
            pages.Add("The stump wants:\n" + RequestText(s.door));
            pages.Add("Bring it to the stump and press the action button. You can give a few at a time; it remembers.");
        }

        if (GameState.HasFlag("forest_grove_reached"))
            pages.Add("You've already been to the grove. The fairy door in the first room will take you straight there.");
        return pages.ToArray();
    }

    /// <summary>Lines like "Carrot x5 (2 given). You carry 4."</summary>
    public static string RequestText(ForestDoor door)
    {
        if (door == null) return "(nothing I can tell)";
        if (door.requirements == null || door.requirements.Length == 0) return "Nothing at all. Just touch it and the gate will open.";
        Inventory inv = Inventory.Instance;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < door.requirements.Length; i++)
        {
            ForestRequirement r = door.requirements[i];
            if (r == null || r.item == null) continue;
            int given = ForestProgress.Given(door.id, i);
            int have = inv != null ? ForestItemMatch.Count(inv, r.item) : 0;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(r.item.itemName).Append(" x").Append(r.count);
            if (given >= r.count) sb.Append("  (done!)");
            else sb.Append("  (").Append(given).Append(" given, you carry ").Append(have).Append(")");
        }
        return sb.Length > 0 ? sb.ToString() : "Nothing at all. Just touch it and the gate will open.";
    }

    // ------------------------------------------------------------------ the maths
    private int FirstLockedStep()
    {
        for (int i = 0; i < steps.Length; i++)
            if (steps[i] != null && steps[i].door != null && !steps[i].door.IsOpen && !ForestProgress.IsUnlocked(steps[i].door.id)) return i;
        return -1;
    }

    private ForestRoom RoomAt(Vector2 p)
    {
        if (allRooms == null) return null;
        foreach (ForestRoom r in allRooms)
        {
            if (r == null) continue;
            Vector2 d = p - (Vector2)r.transform.position;
            float h = r.size * 0.5f;
            if (Mathf.Abs(d.x) <= h && Mathf.Abs(d.y) <= h) return r;
        }
        return null;
    }

    private ForestRoom Nearest(Vector2 p)
    {
        ForestRoom best = null; float bd = float.MaxValue;
        foreach (ForestRoom r in allRooms)
        {
            if (r == null) continue;
            float d = Vector2.Distance(p, r.transform.position);
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }

    private static int Compass(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0f ? 1 : 3;
        return v.y > 0f ? 0 : 2;
    }

    private static string Name(int dir) { return Names[Mathf.Clamp(dir, 0, 3)]; }

    private static int GateDir(Step s)
    {
        if (s == null || s.door == null || s.room == null) return 1;
        return Compass(s.door.transform.position - s.room.transform.position);
    }

    private static ForestRoom.Side SideOf(ForestRoom r, int d)
    {
        return d == 0 ? r.north : d == 1 ? r.east : d == 2 ? r.south : r.west;
    }

    private ForestRoom Neighbour(ForestRoom r, int d)
    {
        if (SideOf(r, d) == ForestRoom.Side.Wall) return null;
        Vector2 target = (Vector2)r.transform.position + Dirs[d] * r.size;
        foreach (ForestRoom o in allRooms)
        {
            if (o == null || o == r) continue;
            if (Vector2.Distance(o.transform.position, target) > 1f) continue;
            if (SideOf(o, (d + 2) % 4) == ForestRoom.Side.Wall) continue;
            return o;
        }
        return null;
    }

    /// <summary>Shortest way between two rooms through the openings (gates count as openings). Gives the first direction to take and how many rooms away.</summary>
    private bool Route(ForestRoom from, ForestRoom to, out int firstDir, out int rooms)
    {
        firstDir = 0; rooms = 0;
        var firstOf = new Dictionary<ForestRoom, int>();
        var dist = new Dictionary<ForestRoom, int>();
        var q = new Queue<ForestRoom>();
        q.Enqueue(from); dist[from] = 0;
        while (q.Count > 0)
        {
            ForestRoom cur = q.Dequeue();
            if (cur == to) { firstDir = firstOf.ContainsKey(cur) ? firstOf[cur] : 0; rooms = dist[cur]; return true; }
            for (int d = 0; d < 4; d++)
            {
                ForestRoom nb = Neighbour(cur, d);
                if (nb == null || dist.ContainsKey(nb)) continue;
                dist[nb] = dist[cur] + 1;
                firstOf[nb] = cur == from ? d : firstOf[cur];
                q.Enqueue(nb);
            }
        }
        return false;
    }

    private void AddRoute(List<string> pages, ForestRoom from, ForestRoom to, string intro)
    {
        int dir, n;
        if (Route(from, to, out dir, out n))
            pages.Add(intro + ". Leave this room through the " + Name(dir) + " opening, and keep following the path - it is " + n + (n == 1 ? " room" : " rooms") + " away.");
        else
            pages.Add(intro + ", but I can't find a way from here. Try going back to the first room and starting again.");
    }
}
