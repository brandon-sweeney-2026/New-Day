using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A line an NPC may say, and when. The best match (highest Priority whose Conditions are true) is used; ties are picked at random.</summary>
[Serializable]
public class NpcChatLine
{
    public string label = "Line";
    public int priority = 0;
    public NpcCondition conditions = new NpcCondition();
    [TextArea] public string[] pages = { "Hello!" };
}

/// <summary>
/// A villager who follows a daily schedule: they walk the waypoint network between places, wait a while, then move on, and may leave the scene (to the city) and come back.
///
/// HOW IT WORKS: where the NPC is, is worked out from the game clock and today's plan (picked each morning from the Schedule's routes). Because of that they are in the right
/// place when you walk into a room, after a scene change, after sleeping, and while time is skipped. The player can't push them around, and talking to them
/// pauses the clock, so they stand still while you talk.
///
/// SET UP: give them a Schedule asset (Create > Farm > NPC Schedule), put PathNodes and NpcSpots in the scene, and put this on an object with a SpriteRenderer.
/// The setup menus (Farm > Set Up > Kim Family, Farm > Set Up > Suburb Residents) build all of this for you.
/// </summary>
public class ScheduledNpc : MonoBehaviour, IInteractable
{
    [Header("Who")]
    public string displayName = "Neighbour";
    public NpcScheduleData schedule;
    public NpcProfile profile;
    [Tooltip("Name of the scene this NPC lives in. Empty = the scene they are placed in.")]
    public string homeScene = "";

    [Header("Walking")]
    [Tooltip("How far they walk in one game minute (tiles). 1.5 is a relaxed stroll when a game minute is 0.7 seconds.")]
    public float tilesPerGameMinute = 1.5f;
    public float walkFps = 7f;
    [Tooltip("Where they stand compared with the exact spot, in tiles. Give people who share a spot (a bench, the dinner table, a bed) different offsets so they stand side by side instead of inside each other.")]
    public Vector2 standOffset = Vector2.zero;

    [Header("Pictures (the setup menu fills these in; each is a row of walk frames, first frame = standing)")]
    public Sprite[] down;
    public Sprite[] up;
    public Sprite[] side;           // faces right; flipped for left

    [Header("Talking")]
    public Sprite portraitIdle, portraitTalk, portraitHappy, portraitBlink;
    public Sprite dialoguePanel, portraitFrame;
    public Color nameColor = new Color(0.2f, 0.45f, 0.5f);
    [TextArea] public string[] firstMeeting = { "Hello! Nice to meet you." };
    public NpcChatLine[] chatLines;
    [Tooltip("If set, 'Buy something' appears while this NPC is standing at the place below (a shopkeeper at the checkout).")]
    public ShopCounter shop;
    [Tooltip("Optional: a HardwareDisplay (Frank's counter). Adds a 'Browse the hardware' choice while he stands near the shop spot.")]
    public HardwareDisplay hardware;
    public string shopSpotId = "StoreCheckout";
    public float shopSpotRadius = 3f;

    [Header("Look at what they are doing (read-only)")]
    [SerializeField] private string nowDoing = "";

    public string InteractPrompt { get { return "Talk to " + displayName; } }
    public string CurrentActivity { get { return nowDoing; } }
    public bool IsAway { get; private set; }

    // ------------------------------------------------------------------ plan data
    private enum Kind { Walk, Teleport, Wait, Away }
    private class Seg
    {
        public Kind kind; public float t0, t1;
        public Vector2 a, b; public Vector2 facing; public string label;
    }
    private class Plan
    {
        public ScheduleRoute route; public int index;
        public float start, end;
        public List<Seg> segs = new List<Seg>();
        public Vector2 endPos; public bool endAway;
        public string startPlace, startScene;
    }

    private static readonly Dictionary<string, int> acceptedDay = new Dictionary<string, int>();
    private static readonly Dictionary<string, List<int>> accepted = new Dictionary<string, List<int>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { acceptedDay.Clear(); accepted.Clear(); }

    private readonly List<Plan> plans = new List<Plan>();
    private int planDay = -1;
    private string sceneName;
    private SpriteRenderer sr;
    private Collider2D col;
    private Transform player;
    private SpriteRenderer playerSr;
    private Vector2 lastPos, facingDir = Vector2.down;
    private bool walking, wasPaused;
    private float animTime;
    private DialogueSpeaker speaker;
    private readonly HashSet<string> warned = new HashSet<string>();

    // ------------------------------------------------------------------ setup
    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        if (col == null) { var c = gameObject.AddComponent<CircleCollider2D>(); c.radius = 0.6f; c.offset = new Vector2(0f, 0.5f); c.isTrigger = true; col = c; }
        sceneName = string.IsNullOrEmpty(homeScene) ? SceneManager.GetActiveScene().name : homeScene;
        speaker = new DialogueSpeaker
        {
            speakerName = displayName,
            idle = portraitIdle, talk = portraitTalk, happy = portraitHappy, blink = portraitBlink,
            panel = dialoguePanel, frame = portraitFrame, nameColor = nameColor,
        };
        lastPos = transform.position;
    }

    private void Update()
    {
        var tm = TimeManager.Instance;
        if (tm == null || schedule == null) return;
        if (planDay != tm.TotalDays) BuildDay(tm);

        State st = Evaluate(NpcClock.Minutes);
        nowDoing = st.label;
        ApplyState(st);
    }

    // ------------------------------------------------------------------ placing (where is a "place"?)
    private struct Place { public bool found, inScene; public Vector2 pos, facing; }

    private Place Resolve(string id, string scene)
    {
        var p = new Place();
        if (string.IsNullOrEmpty(id)) return p;
        bool other = !string.IsNullOrEmpty(scene) && scene != sceneName;
        if (other) { p.found = true; p.inScene = false; return p; }
        // (Stand Offset moves them a little to one side, so two people sent to the same spot don't stand inside each other.)
        NpcSpot spot = NpcSpot.Find(id);
        if (spot != null) { p.found = true; p.inScene = true; p.pos = (Vector2)spot.transform.position + standOffset; p.facing = spot.facing; return p; }
        PathNode node = PathNode.Find(id);
        if (node != null) { p.found = true; p.inScene = true; p.pos = (Vector2)node.transform.position + standOffset; p.facing = Vector2.down; return p; }
        if (warned.Add(id)) Debug.LogWarning(displayName + ": can't find a place called '" + id + "' (an NpcSpot id or PathNode id) in this scene.");
        return p;
    }

    // ------------------------------------------------------------------ the day's plan
    private void BuildDay(TimeManager tm)
    {
        planDay = tm.TotalDays;
        plans.Clear();
        ScheduleRoute[] routes = schedule.routes;
        if (routes == null) return;

        List<int> picked;
        int day;
        if (acceptedDay.TryGetValue(schedule.npcId, out day) && day == tm.TotalDays && accepted.TryGetValue(schedule.npcId, out picked)) { }
        else
        {
            // decide today's routes: conditions first, then priority, skipping overlaps
            var cands = new List<int>();
            for (int i = 0; i < routes.Length; i++)
            {
                ScheduleRoute r = routes[i];
                if (r == null || !r.enabled || r.stops == null || r.stops.Length == 0) continue;
                if (r.conditions != null && !r.conditions.Matches()) continue;
                cands.Add(i);
            }
            cands.Sort((a, b) =>
            {
                int c = routes[b].priority.CompareTo(routes[a].priority);
                return c != 0 ? c : routes[a].startHour.CompareTo(routes[b].startHour);
            });
            picked = new List<int>();
            var provisional = new List<Plan>();
            foreach (int i in cands)
            {
                Plan p = BuildPlan(routes[i], i, routes[i].startHour * 60f, null, null);
                bool overlap = false;
                foreach (Plan q in provisional) if (p.start < q.end && q.start < p.end) { overlap = true; break; }
                if (overlap) continue;
                provisional.Add(p); picked.Add(i);
            }
            picked.Sort((a, b) => routes[a].startHour.CompareTo(routes[b].startHour));
            acceptedDay[schedule.npcId] = tm.TotalDays;
            accepted[schedule.npcId] = picked;
        }

        // final plans in time order; a route with no start place begins where the last one ended (if it said "stay")
        string carryPlace = null, carryScene = null;
        foreach (int i in picked)
        {
            if (i >= routes.Length) continue;
            ScheduleRoute r = routes[i];
            Plan p = BuildPlan(r, i, r.startHour * 60f, carryPlace, carryScene);
            plans.Add(p);
            if (r.whenFinished == RouteEnd.StayAtLastStop && r.stops.Length > 0)
            { ScheduleStop last = r.stops[r.stops.Length - 1]; carryPlace = last.place; carryScene = last.scene; }
            else { carryPlace = null; carryScene = null; }
        }
    }

    private Plan BuildPlan(ScheduleRoute r, int index, float start, string carryPlace, string carryScene)
    {
        var plan = new Plan { route = r, index = index, start = start };
        string startPlace = !string.IsNullOrEmpty(r.startPlace) ? r.startPlace : (carryPlace ?? schedule.idleSpot);
        string startScene = !string.IsNullOrEmpty(r.startPlace) ? r.startScene : (carryPlace != null ? carryScene : schedule.idleScene);
        plan.startPlace = startPlace; plan.startScene = startScene;

        Place cur = Resolve(startPlace, startScene);
        bool away = !cur.inScene;
        string curScene = string.IsNullOrEmpty(startScene) ? sceneName : startScene;
        Vector2 pos = cur.pos;
        float t = start;

        foreach (ScheduleStop s in r.stops)
        {
            if (s == null) continue;
            Place next = Resolve(s.place, s.scene);
            if (!next.found) continue;
            string nextScene = string.IsNullOrEmpty(s.scene) ? sceneName : s.scene;

            if (!away && next.inScene)
            {
                t = AddWalk(plan, t, pos, next.pos);
                pos = next.pos;
                t = AddWait(plan, t, pos, next.facing, s);
            }
            else if (!away && !next.inScene)
            {
                PathNode exit = PathNode.FindExitTo(nextScene);
                if (exit != null) { t = AddWalk(plan, t, pos, exit.transform.position); pos = exit.transform.position; }
                float dur = s.transitMinutes + s.waitMinutes;
                plan.segs.Add(new Seg { kind = Kind.Away, t0 = t, t1 = t + dur, a = pos, b = pos, label = Label(s, true) });
                t += dur; away = true;
            }
            else if (away && next.inScene)
            {
                // coming back from elsewhere: appear at the entry node of the scene they came from (or right at the place)
                plan.segs.Add(new Seg { kind = Kind.Away, t0 = t, t1 = t + s.transitMinutes, a = pos, b = pos, label = "On the way" });
                t += s.transitMinutes;
                PathNode entry = PathNode.FindExitTo(curScene);
                pos = entry != null ? (Vector2)entry.transform.position : next.pos;
                away = false;
                t = AddWalk(plan, t, pos, next.pos);
                pos = next.pos;
                t = AddWait(plan, t, pos, next.facing, s);
            }
            else
            {
                float dur = s.transitMinutes + s.waitMinutes;
                plan.segs.Add(new Seg { kind = Kind.Away, t0 = t, t1 = t + dur, a = pos, b = pos, label = Label(s, true) });
                t += dur;
            }
            curScene = nextScene;
        }

        if (r.whenFinished == RouteEnd.ReturnToIdleSpot && !string.IsNullOrEmpty(schedule.idleSpot))
        {
            Place home = Resolve(schedule.idleSpot, schedule.idleScene);
            if (home.found)
            {
                if (!away && home.inScene) { t = AddWalk(plan, t, pos, home.pos); pos = home.pos; }
                else if (!away && !home.inScene)
                {
                    string homeSc = string.IsNullOrEmpty(schedule.idleScene) ? sceneName : schedule.idleScene;
                    PathNode exit = PathNode.FindExitTo(homeSc);
                    if (exit != null) { t = AddWalk(plan, t, pos, exit.transform.position); pos = exit.transform.position; }
                    away = true;
                }
                else if (away && home.inScene)
                {
                    PathNode entry = PathNode.FindExitTo(curScene);
                    pos = entry != null ? (Vector2)entry.transform.position : home.pos;
                    away = false;
                    t = AddWalk(plan, t, pos, home.pos); pos = home.pos;
                }
            }
        }
        plan.end = t; plan.endPos = pos; plan.endAway = away;
        return plan;
    }

    private string Label(ScheduleStop s, bool away)
    {
        if (!string.IsNullOrEmpty(s.activity)) return s.activity;
        return away ? "Out and about" : "Busy";
    }

    private float AddWalk(Plan plan, float t, Vector2 from, Vector2 to)
    {
        if (Vector2.Distance(from, to) < 0.05f) return t;
        bool found;
        List<PathNode.Leg> legs = PathNode.Route(from, to, out found);
        if (!found)
        {
            // no path: they just turn up (a quick jump) so a missing link doesn't break the day
            plan.segs.Add(new Seg { kind = Kind.Teleport, t0 = t, t1 = t + 0.2f, a = from, b = to, label = "Walking" });
            return t + 0.2f;
        }
        foreach (PathNode.Leg leg in legs)
        {
            if (leg.portal)
            {
                plan.segs.Add(new Seg { kind = Kind.Teleport, t0 = t, t1 = t + 0.3f, a = leg.from, b = leg.to, label = "Walking" });
                t += 0.3f;
            }
            else
            {
                float len = Vector2.Distance(leg.from, leg.to);
                if (len < 0.01f) continue;
                float dur = len / Mathf.Max(0.1f, tilesPerGameMinute);
                plan.segs.Add(new Seg { kind = Kind.Walk, t0 = t, t1 = t + dur, a = leg.from, b = leg.to, label = "Walking" });
                t += dur;
            }
        }
        return t;
    }

    private float AddWait(Plan plan, float t, Vector2 pos, Vector2 spotFacing, ScheduleStop s)
    {
        Vector2 f = s.facing != Vector2.zero ? s.facing : spotFacing;
        plan.segs.Add(new Seg { kind = Kind.Wait, t0 = t, t1 = t + Mathf.Max(0f, s.waitMinutes), a = pos, b = pos, facing = f, label = Label(s, false) });
        return t + Mathf.Max(0f, s.waitMinutes);
    }

    // ------------------------------------------------------------------ evaluating the clock
    private struct State { public Vector2 pos; public bool away, walking; public Vector2 dir; public string label; }

    private State Evaluate(float now)
    {
        var st = new State { dir = facingDir, label = "Resting" };
        Plan current = null;
        foreach (Plan p in plans) if (p.start <= now) current = p;

        Place idle = Resolve(schedule.idleSpot, schedule.idleScene);
        if (current == null)
        {
            st.pos = idle.pos; st.away = !idle.inScene; st.dir = idle.facing != Vector2.zero ? idle.facing : facingDir; st.label = "Resting";
            return st;
        }
        if (now >= current.end)
        {
            if (current.route.whenFinished == RouteEnd.StayAtLastStop)
            {
                st.pos = current.endPos; st.away = current.endAway; st.label = LastLabel(current);
                Seg last = current.segs.Count > 0 ? current.segs[current.segs.Count - 1] : null;
                if (last != null && last.kind == Kind.Wait && last.facing != Vector2.zero) st.dir = last.facing;
            }
            else { st.pos = idle.pos; st.away = !idle.inScene; st.dir = idle.facing != Vector2.zero ? idle.facing : facingDir; st.label = "Resting"; }
            return st;
        }

        foreach (Seg s in current.segs)
        {
            if (now < s.t0 || now >= s.t1) continue;
            st.label = s.label;
            switch (s.kind)
            {
                case Kind.Walk:
                    float u = Mathf.InverseLerp(s.t0, s.t1, now);
                    st.pos = Vector2.Lerp(s.a, s.b, u); st.walking = true; st.dir = (s.b - s.a).normalized; return st;
                case Kind.Teleport:
                    st.pos = now < (s.t0 + s.t1) * 0.5f ? s.a : s.b; return st;
                case Kind.Wait:
                    st.pos = s.a; if (s.facing != Vector2.zero) st.dir = s.facing; return st;
                case Kind.Away:
                    st.pos = s.a; st.away = true; return st;
            }
        }
        // before the first segment (or a gap): at the start of the route
        st.pos = current.segs.Count > 0 ? current.segs[0].a : idle.pos;
        return st;
    }

    private string LastLabel(Plan p)
    {
        for (int i = p.segs.Count - 1; i >= 0; i--) if (p.segs[i].kind == Kind.Wait || p.segs[i].kind == Kind.Away) return p.segs[i].label;
        return "Resting";
    }

    // ------------------------------------------------------------------ showing it
    private void ApplyState(State st)
    {
        IsAway = st.away;
        if (sr != null && sr.enabled == st.away) sr.enabled = !st.away;
        if (col != null && col.enabled == st.away) col.enabled = !st.away;
        if (st.away) { lastPos = st.pos; return; }

        Vector2 delta = st.pos - lastPos;
        bool paused = TimeManager.Instance != null && TimeManager.Instance.Paused;
        walking = st.walking && delta.sqrMagnitude > 0.00000001f && !paused;
        if (walking) facingDir = delta.normalized;
        else if (st.dir != Vector2.zero && !DialogueBox.IsOpen) facingDir = st.dir;
        transform.position = new Vector3(st.pos.x, st.pos.y, transform.position.z);
        lastPos = st.pos;
        Draw();
    }

    private void Draw()
    {
        if (sr == null) return;
        Sprite[] set; bool flip = false;
        if (Mathf.Abs(facingDir.x) > Mathf.Abs(facingDir.y)) { set = side; flip = facingDir.x < 0f; }
        else set = facingDir.y > 0f ? up : down;
        if (set != null && set.Length > 0)
        {
            if (walking) animTime += Time.deltaTime;
            else animTime = 0f;
            sr.sprite = walking && set.Length > 1 ? set[1 + Mathf.FloorToInt(animTime * walkFps) % (set.Length - 1)] : set[0];
        }
        sr.flipX = flip;

        // in front of the player when below them on screen, behind when above
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) { player = pc.transform; playerSr = pc.GetComponentInChildren<SpriteRenderer>(); }
        }
        if (playerSr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + (transform.position.y < player.position.y - 0.05f ? 2 : -2);
        }
    }

    // ------------------------------------------------------------------ talking
    public void Interact(PlayerActions p)
    {
        if (DialogueBox.IsOpen || IsAway) return;
        // face the player
        if (player != null) { Vector2 d = player.position - transform.position; if (d.sqrMagnitude > 0.01f) { facingDir = d.normalized; Draw(); } }

        string metFlag = "npc_met_" + (schedule != null ? schedule.npcId : displayName);
        if (!GameState.HasFlag(metFlag))
        {
            GameState.SetFlag(metFlag);
            DialogueBox.Show(speaker, firstMeeting, Options(p), null, true);
            return;
        }
        NpcChatLine line = BestLine();
        string[] pages = line != null && line.pages != null && line.pages.Length > 0 ? line.pages : new[] { "Hello again!" };
        DialogueBox.Show(speaker, pages, Options(p), null);
    }

    /// <summary>The line to say now: the highest Priority whose Conditions are true. If several share that priority, one is picked at random (so they don't repeat themselves).</summary>
    private NpcChatLine BestLine()
    {
        if (chatLines == null) return null;
        var best = new List<NpcChatLine>();
        foreach (NpcChatLine l in chatLines)
        {
            if (l == null) continue;
            if (l.conditions != null && !l.conditions.Matches()) continue;
            if (best.Count > 0 && l.priority < best[0].priority) continue;
            if (best.Count > 0 && l.priority > best[0].priority) best.Clear();
            best.Add(l);
        }
        return best.Count == 0 ? null : best[UnityEngine.Random.Range(0, best.Count)];
    }

    private List<DialogueOption> Options(PlayerActions p)
    {
        var o = new List<DialogueOption>();
        if (shop != null && IsNearShopSpot())
            o.Add(new DialogueOption("Buy something", () => { DialogueBox.Close(); shop.Interact(p); }, true));
        if (hardware != null && IsNearShopSpot())
            o.Add(new DialogueOption("Browse the hardware", () => { DialogueBox.Close(); hardware.Interact(p); }, true));
        o.Add(new DialogueOption("What are you up to?", () =>
        {
            string what = string.IsNullOrEmpty(nowDoing) || nowDoing == "Walking" || nowDoing == "Resting" ? "Just going about my day." : nowDoing + ".";
            DialogueBox.Show(speaker, new[] { what }, Options(p), null);
        }));
        o.Add(new DialogueOption("Goodbye", null, true));
        return o;
    }

    private bool IsNearShopSpot()
    {
        NpcSpot s = NpcSpot.Find(shopSpotId);
        return s != null && Vector2.Distance(s.transform.position, transform.position) <= shopSpotRadius;
    }

    // ------------------------------------------------------------------ debugging
    [ContextMenu("Print Today's Plan")]
    public void PrintPlan()
    {
        if (!Application.isPlaying || schedule == null) { Debug.Log("Press Play first."); return; }
        if (planDay != (TimeManager.Instance != null ? TimeManager.Instance.TotalDays : -1)) BuildDay(TimeManager.Instance);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(displayName + " - today's routes (" + (TimeManager.Instance != null ? TimeManager.Instance.GetDateString() : "") + "), weather " + WeatherManager.Current + ":");
        foreach (Plan p in plans) sb.AppendLine("  " + Clock(p.start) + " - " + Clock(p.end) + "  " + p.route.routeName + (p.route.whenFinished == RouteEnd.StayAtLastStop ? "  (stays at last stop)" : ""));
        if (plans.Count == 0) sb.AppendLine("  (no routes today - stays at the idle spot)");
        Debug.Log(sb.ToString());
    }

    private static string Clock(float minutes)
    {
        int m = Mathf.RoundToInt(minutes); int h = (m / 60) % 24;
        return h.ToString("00") + ":" + (m % 60).ToString("00");
    }
}
