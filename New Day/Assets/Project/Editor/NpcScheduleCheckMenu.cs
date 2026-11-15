using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Fix and Diagnose > Check NPC Schedules
///
/// Works out, without pressing Play, what every ScheduledNpc in the open scene will do on each day of the week, and writes
/// it to the Console. It uses the same rules as the game (ScheduledNpc): keep the routes whose conditions are true, take the
/// highest priority first, drop any route that overlaps one already taken, and walk the PathNode network at the NPC's speed.
///
/// It warns about:
///   - a stop whose place does not exist in the scene (a misspelt NpcSpot id),
///   - a walk with no path between two places (the NPC would jump there),
///   - a route that is DROPPED because it overlaps another one (usually: not enough time left for the walk),
///   - and it lists who is standing near whom, so you can see that neighbours really do meet.
/// It checks an ordinary dry day and a rainy day in spring. Routes that need game flags or counters are left out.
/// Lines in the Console start with "SCHEDULE|" so they are easy to find in Logs/Editor.log.
/// Must live in a folder named "Editor".
/// </summary>
public static class NpcScheduleCheckMenu
{
    [MenuItem("Farm/Fix and Diagnose/Check NPC Schedules", false, 430)]
    public static void CheckFromMenu()
    {
        int problems;
        string report = Check(out problems);
        Debug.Log(report);
        EditorUtility.DisplayDialog("NPC schedules", problems == 0
            ? "No problems found. Everybody's week is in the Console."
            : problems + " problem(s) found. Look in the Console for the lines marked '!!'.", "OK");
    }

    // ------------------------------------------------------------------ the scene, read once
    private static PathNode[] nodes;
    private static NpcSpot[] spots;
    private static Dictionary<PathNode, List<PathNode>> neighbours;

    private class Wait { public string who, place, doing; public float t0, t1; public Vector2 pos; }
    private class Plan { public ScheduleRoute route; public float start, end; public Vector2 endPos; public bool endAway; public List<Wait> waits = new List<Wait>(); }
    private struct Place { public bool found, inScene; public Vector2 pos; }

    private static readonly HashSet<string> problemsSeen = new HashSet<string>();

    /// <summary>Returns the whole report as text. 'problems' is how many different problems were found.</summary>
    public static string Check(out int problems)
    {
        nodes = Object.FindObjectsByType<PathNode>();
        spots = Object.FindObjectsByType<NpcSpot>();
        BuildGraph();
        problemsSeen.Clear();

        var npcs = new List<ScheduledNpc>(Object.FindObjectsByType<ScheduledNpc>());
        npcs.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));

        var sb = new StringBuilder();
        sb.AppendLine("SCHEDULE| " + npcs.Count + " NPCs, " + nodes.Length + " path nodes, " + spots.Length + " spots.  (a dry spring day; then a rainy one)");
        var meetings = new StringBuilder();
        int meetingCount = 0;

        for (int weather = 0; weather < 2; weather++)
        {
            WeatherMask mask = weather == 0 ? WeatherMask.Clear : WeatherMask.Rain;
            for (int day = 0; day < 7; day++)
            {
                string dayName = TimeManager.WeekDayNames[day];
                var allWaits = new List<Wait>();
                foreach (ScheduledNpc npc in npcs)
                {
                    if (npc.schedule == null) { Problem(sb, npc.displayName + " has no schedule."); continue; }
                    List<Plan> plans = PlanDay(npc, day, mask, sb, dayName + (weather == 0 ? "" : " (rain)"));
                    if (weather == 0)
                    {
                        var line = new StringBuilder("SCHEDULE| " + dayName + " " + npc.displayName.PadRight(9) + ": ");
                        foreach (Plan p in plans) line.Append(Clock(p.start) + "-" + Clock(p.end) + " " + p.route.routeName + (p.endAway ? " (away)" : "") + " | ");
                        if (plans.Count == 0) line.Append("(stays at the idle spot)");
                        sb.AppendLine(line.ToString());
                    }
                    foreach (Plan p in plans) allWaits.AddRange(p.waits);
                }
                if (weather != 0) continue;

                // who is standing near whom today (outdoors or in a shop, for at least 10 game minutes)
                for (int i = 0; i < allWaits.Count; i++)
                    for (int j = i + 1; j < allWaits.Count; j++)
                    {
                        Wait a = allWaits[i], b = allWaits[j];
                        if (a.who == b.who || !Public(a) || !Public(b)) continue;
                        float from = Mathf.Max(a.t0, b.t0), to = Mathf.Min(a.t1, b.t1);
                        if (to - from < 10f || Vector2.Distance(a.pos, b.pos) > 3.5f) continue;
                        meetings.AppendLine("SCHEDULE| " + dayName + " " + Clock(from) + "-" + Clock(to) + "  " + a.who + " (" + a.doing + ") + " + b.who + " (" + b.doing + ") at " + a.place);
                        meetingCount++;
                    }
            }
        }
        sb.AppendLine("SCHEDULE| ---- " + meetingCount + " times in the week two people are together outdoors or in a shop (dry weather) ----");
        sb.Append(meetings);
        problems = problemsSeen.Count;
        sb.AppendLine("SCHEDULE| ---- " + (problems == 0 ? "no problems found" : problems + " problem(s), marked !! above") + " ----");
        return sb.ToString();
    }

    private static bool Public(Wait w)
    {
        return w.pos.x < 300f || w.place.StartsWith("Store") || w.place.StartsWith("Hw");      // the map itself, or inside one of the two shops
    }

    private static void Problem(StringBuilder sb, string text)
    {
        if (problemsSeen.Add(text)) sb.AppendLine("SCHEDULE| !! " + text);
    }

    // ------------------------------------------------------------------ one NPC, one day (the same steps as ScheduledNpc.BuildDay)
    private static List<Plan> PlanDay(ScheduledNpc npc, int day, WeatherMask weather, StringBuilder sb, string dayName)
    {
        NpcScheduleData sched = npc.schedule;
        ScheduleRoute[] routes = sched.routes ?? new ScheduleRoute[0];
        var cands = new List<int>();
        for (int i = 0; i < routes.Length; i++)
        {
            ScheduleRoute r = routes[i];
            if (r == null || !r.enabled || r.stops == null || r.stops.Length == 0) continue;
            if (!Matches(r.conditions, day, weather)) continue;
            cands.Add(i);
        }
        cands.Sort((a, b) =>
        {
            int c = routes[b].priority.CompareTo(routes[a].priority);
            return c != 0 ? c : routes[a].startHour.CompareTo(routes[b].startHour);
        });
        var picked = new List<int>();
        var provisional = new List<Plan>();
        foreach (int i in cands)
        {
            Plan p = BuildPlan(npc, routes[i], null, null, sb);
            Plan clash = null;
            foreach (Plan q in provisional) if (p.start < q.end && q.start < p.end) { clash = q; break; }
            if (clash != null)
            {
                // a lower-priority route losing to a higher one at the same time of day is how "rainy day" and "work day" routes are meant to work;
                // it is only a problem when both have the same priority (nobody meant one of them to lose)
                if (routes[i].priority == clash.route.priority)
                    Problem(sb, npc.displayName + ", " + dayName + ": '" + routes[i].routeName + "' (" + Clock(p.start) + "-" + Clock(p.end) + ") is DROPPED, it overlaps '" + clash.route.routeName + "' (" + Clock(clash.start) + "-" + Clock(clash.end) + ").");
                continue;
            }
            provisional.Add(p); picked.Add(i);
        }
        picked.Sort((a, b) => routes[a].startHour.CompareTo(routes[b].startHour));

        var plans = new List<Plan>();
        string carryPlace = null, carryScene = null;
        foreach (int i in picked)
        {
            ScheduleRoute r = routes[i];
            Plan p = BuildPlan(npc, r, carryPlace, carryScene, sb);
            if (plans.Count > 0 && plans[plans.Count - 1].end > p.start + 0.5f)
                Problem(sb, npc.displayName + ", " + dayName + ": '" + plans[plans.Count - 1].route.routeName + "' ends at " + Clock(plans[plans.Count - 1].end) + ", after '" + r.routeName + "' starts (" + Clock(p.start) + "). The first one is cut short.");
            plans.Add(p);
            if (r.whenFinished == RouteEnd.StayAtLastStop) { ScheduleStop last = r.stops[r.stops.Length - 1]; carryPlace = last.place; carryScene = last.scene; }
            else { carryPlace = null; carryScene = null; }
        }
        return plans;
    }

    private static bool Matches(NpcCondition c, int day, WeatherMask weather)
    {
        if (c == null) return true;
        if (c.days != 0 && (c.days & (DayMask)(1 << day)) == 0) return false;
        if (c.seasons != 0 && (c.seasons & SeasonMask.Spring) == 0) return false;
        if (c.weather != 0 && (c.weather & weather) == 0) return false;
        if (c.requireFlags != null) foreach (string f in c.requireFlags) if (!string.IsNullOrEmpty(f)) return false;      // no flags are set in this check
        if (c.anyFlags != null) foreach (string f in c.anyFlags) if (!string.IsNullOrEmpty(f)) return false;
        if (c.counters != null) foreach (CounterCheck k in c.counters) if (k != null && !string.IsNullOrEmpty(k.key) && k.atLeast > 0) return false;
        return true;
    }

    private static Plan BuildPlan(ScheduledNpc npc, ScheduleRoute r, string carryPlace, string carryScene, StringBuilder sb)
    {
        NpcScheduleData sched = npc.schedule;
        var plan = new Plan { route = r, start = r.startHour * 60f };
        string startPlace = !string.IsNullOrEmpty(r.startPlace) ? r.startPlace : (carryPlace ?? sched.idleSpot);
        string startScene = !string.IsNullOrEmpty(r.startPlace) ? r.startScene : (carryPlace != null ? carryScene : sched.idleScene);

        Place cur = Resolve(npc, startPlace, startScene, sb);
        bool away = !cur.inScene;
        Vector2 pos = cur.pos;
        float t = plan.start;
        string curScene = startScene;

        foreach (ScheduleStop s in r.stops)
        {
            if (s == null) continue;
            Place next = Resolve(npc, s.place, s.scene, sb);
            if (!next.found) continue;
            if (!away && next.inScene)
            {
                t = Walk(npc, t, pos, next.pos, r, sb); pos = next.pos;
                plan.waits.Add(new Wait { who = npc.displayName, place = s.place, doing = s.activity, t0 = t, t1 = t + Mathf.Max(0f, s.waitMinutes), pos = pos });
                t += Mathf.Max(0f, s.waitMinutes);
            }
            else if (!away && !next.inScene)
            {
                PathNode exit = ExitTo(s.scene);
                if (exit == null) Problem(sb, npc.displayName + ": no PathNode leads to the scene '" + s.scene + "' (set Leads To Scene on a node at the edge of the map).");
                else { t = Walk(npc, t, pos, exit.transform.position, r, sb); pos = exit.transform.position; }
                t += s.transitMinutes + s.waitMinutes; away = true;
            }
            else if (away && next.inScene)
            {
                t += s.transitMinutes;
                PathNode entry = ExitTo(curScene);
                pos = entry != null ? (Vector2)entry.transform.position : next.pos;
                away = false;
                t = Walk(npc, t, pos, next.pos, r, sb); pos = next.pos;
                plan.waits.Add(new Wait { who = npc.displayName, place = s.place, doing = s.activity, t0 = t, t1 = t + Mathf.Max(0f, s.waitMinutes), pos = pos });
                t += Mathf.Max(0f, s.waitMinutes);
            }
            else t += s.transitMinutes + s.waitMinutes;
            curScene = s.scene;
        }

        if (r.whenFinished == RouteEnd.ReturnToIdleSpot && !string.IsNullOrEmpty(sched.idleSpot))
        {
            Place home = Resolve(npc, sched.idleSpot, sched.idleScene, sb);
            if (home.found)
            {
                if (!away && home.inScene) { t = Walk(npc, t, pos, home.pos, r, sb); pos = home.pos; }
                else if (away && home.inScene)
                {
                    PathNode entry = ExitTo(curScene);
                    pos = entry != null ? (Vector2)entry.transform.position : home.pos;
                    away = false;
                    t = Walk(npc, t, pos, home.pos, r, sb); pos = home.pos;
                }
            }
        }
        plan.end = t; plan.endPos = pos; plan.endAway = away;
        return plan;
    }

    private static Place Resolve(ScheduledNpc npc, string id, string scene, StringBuilder sb)
    {
        var p = new Place();
        if (string.IsNullOrEmpty(id)) return p;
        if (!string.IsNullOrEmpty(scene) && scene != npc.gameObject.scene.name && scene != npc.homeScene) { p.found = true; return p; }
        foreach (NpcSpot s in spots) if (s.spotId == id) { p.found = true; p.inScene = true; p.pos = (Vector2)s.transform.position + npc.standOffset; return p; }
        foreach (PathNode n in nodes) if (n.Id == id) { p.found = true; p.inScene = true; p.pos = (Vector2)n.transform.position + npc.standOffset; return p; }
        Problem(sb, npc.displayName + ": there is no place called '" + id + "' in this scene (an NpcSpot id or a PathNode id).");
        return p;
    }

    private static PathNode ExitTo(string scene)
    {
        foreach (PathNode n in nodes) if (!string.IsNullOrEmpty(n.leadsToScene) && n.leadsToScene == scene) return n;
        return null;
    }

    // ------------------------------------------------------------------ walking (the same shortest-path rules as PathNode.Route)
    private static void BuildGraph()
    {
        neighbours = new Dictionary<PathNode, List<PathNode>>();
        foreach (PathNode n in nodes) neighbours[n] = new List<PathNode>();
        foreach (PathNode n in nodes)
        {
            if (n.links != null) foreach (PathNode o in n.links) if (o != null && o != n) Join(n, o);
            if (n.portalTo != null) Join(n, n.portalTo);
        }
    }

    private static void Join(PathNode a, PathNode b)
    {
        if (!neighbours.ContainsKey(a) || !neighbours.ContainsKey(b)) return;
        if (!neighbours[a].Contains(b)) neighbours[a].Add(b);
        if (!neighbours[b].Contains(a)) neighbours[b].Add(a);
    }

    private static PathNode Nearest(Vector2 p)
    {
        PathNode best = null; float bd = float.MaxValue;
        foreach (PathNode n in nodes) { float d = ((Vector2)n.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = n; } }
        return best;
    }

    /// <summary>The time after walking from 'from' to 'to', starting at 't' (game minutes).</summary>
    private static float Walk(ScheduledNpc npc, float t, Vector2 from, Vector2 to, ScheduleRoute r, StringBuilder sb)
    {
        if (Vector2.Distance(from, to) < 0.05f) return t;
        float speed = Mathf.Max(0.1f, npc.tilesPerGameMinute);
        PathNode s = Nearest(from), e = Nearest(to);
        if (s == null || e == null) return t + 0.2f;

        var dist = new Dictionary<PathNode, float>();
        var open = new List<PathNode>();
        dist[s] = 0f; open.Add(s);
        while (open.Count > 0)
        {
            PathNode cur = open[0];
            foreach (PathNode o in open) if (dist[o] < dist[cur]) cur = o;
            open.Remove(cur);
            if (cur == e) break;
            foreach (PathNode nb in neighbours[cur])
            {
                bool portal = cur.portalTo == nb || nb.portalTo == cur;
                // a door costs 3 tiles when choosing the way, but only 0.3 minutes to step through (as in the game)
                float nd = dist[cur] + (portal ? 3f : Vector2.Distance(cur.transform.position, nb.transform.position));
                float old;
                if (!dist.TryGetValue(nb, out old)) { dist[nb] = nd; open.Add(nb); }
                else if (nd < old) { dist[nb] = nd; if (!open.Contains(nb)) open.Add(nb); }
            }
        }
        if (!dist.ContainsKey(e))
        {
            Problem(sb, npc.displayName + ", route '" + r.routeName + "': no path from near '" + s.Id + "' to near '" + e.Id + "' (the NPC would jump). Link the nodes.");
            return t + 0.2f;
        }
        // walking time: the two ends, plus the network distance. Count the doors on the way to turn their 3 "tiles" back into 0.3 minutes.
        float minutes = (Vector2.Distance(from, s.transform.position) + Vector2.Distance(e.transform.position, to)) / speed;
        PathNode at = e; float network = dist[e]; int doors = 0;
        while (at != s)
        {
            PathNode prev = null;
            foreach (PathNode nb in neighbours[at])
            {
                if (!dist.ContainsKey(nb)) continue;
                bool portal = at.portalTo == nb || nb.portalTo == at;
                float step = portal ? 3f : Vector2.Distance(at.transform.position, nb.transform.position);
                if (Mathf.Abs(dist[nb] + step - dist[at]) < 0.001f) { prev = nb; if (portal) doors++; break; }
            }
            if (prev == null) break;
            at = prev;
        }
        minutes += (network - doors * 3f) / speed + doors * 0.3f;
        return t + minutes;
    }

    private static string Clock(float minutes)
    {
        int m = Mathf.RoundToInt(minutes);
        return ((m / 60) % 24).ToString("00") + ":" + (m % 60).ToString("00");
    }
}
