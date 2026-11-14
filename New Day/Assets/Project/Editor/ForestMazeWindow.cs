using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Level Editors > Forest Maze Generator
/// Builds the whole forest in the open scene: a maze of 15 x 15 tile rooms (default 8 x 8 rooms = 64 grid cells), a winding main path from the entrance to the
/// grove (it tries many layouts until the path is long enough), a locked gate + stump + item request in every room on the main path, shadow animals in
/// every room, dead-end side rooms, and the enchanted grove with the Fairy at the very end. Everything it makes is ordinary scene objects,
/// so you can move, delete or rebuild any room by hand afterwards (select a room > "Rebuild Room").
/// Set up first with Farm > Forest > Set Up Forest.
/// </summary>
public class ForestMazeWindow : EditorWindow
{
    private int width = 8, height = 8, seed = 1, minPath = 30, tries = 3000, roomSize = 15;
    private Vector2 origin = Vector2.zero;
    private ForestProgression progression;
    private Vector2 groveSize = new Vector2(36f, 24f);
    private string entranceSpawnId = "FromFarm";
    private enum EntranceSide { East, West }
    private EntranceSide entrance = EntranceSide.East;
    private bool tutorialRoom = true;
    private bool makeEditLayers = true;

    [MenuItem("Farm/Level Editors/Forest Maze Generator", false, 3)]
    public static void Open() { GetWindow<ForestMazeWindow>("Forest Maze"); }

    private void OnEnable()
    {
        if (progression == null) progression = AssetDatabase.LoadAssetAtPath<ForestProgression>(ForestSetupMenu.DataFolder + "/ForestProgression.asset");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Builds the maze + grove in the OPEN scene. Run 'Set Up Forest' first. Each room is 'Room size' x 'Room size' tiles (default 15); the maze is width x height rooms.", MessageType.Info);
        roomSize = Mathf.Clamp(EditorGUILayout.IntField("Room size (tiles)", roomSize), 8, 30);
        width = Mathf.Clamp(EditorGUILayout.IntField("Rooms across", width), 2, 10);
        height = Mathf.Clamp(EditorGUILayout.IntField("Rooms up", height), 2, 10);
        seed = EditorGUILayout.IntField("Random seed (change for a new maze)", seed);
        minPath = Mathf.Clamp(EditorGUILayout.IntField("Wanted rooms on the main path", minPath), 2, 100);
        tries = Mathf.Clamp(EditorGUILayout.IntField("Layouts to try", tries), 1, 20000);
        origin = EditorGUILayout.Vector2Field("Bottom-left room centre", origin);
        groveSize = EditorGUILayout.Vector2Field("Grove size", groveSize);
        entranceSpawnId = EditorGUILayout.TextField("Entrance spawn id", entranceSpawnId);
        entrance = (EntranceSide)EditorGUILayout.EnumPopup("Entrance side", entrance);
        tutorialRoom = EditorGUILayout.Toggle("Add a tutorial room at the entrance", tutorialRoom);
        makeEditLayers = EditorGUILayout.Toggle("Make Level Editor layers", makeEditLayers);
        progression = (ForestProgression)EditorGUILayout.ObjectField("Item progression", progression, typeof(ForestProgression), false);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Maze + Grove", GUILayout.Height(32))) Generate();
        if (GUILayout.Button("Delete the generated 'ForestMaze'")) DeleteOld();
    }

    private static void DeleteOld()
    {
        GameObject old = GameObject.Find("ForestMaze");
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    private static readonly Vector2Int[] Dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };   // N E S W

    private void Generate()
    {
        GameObject roomPrefab = FindPrefab("ForestRoom"), stumpPrefab = FindPrefab("ForestStump"), fairyPrefab = FindPrefab("Fairy"), zonePrefab = FindPrefab("Grove_Zone");
        if (roomPrefab == null || stumpPrefab == null || fairyPrefab == null || zonePrefab == null)
        {
            EditorUtility.DisplayDialog("Not set up", "Run Farm > Forest > Set Up Forest first.", "OK");
            return;
        }
        var style = AssetDatabase.LoadAssetAtPath<ForestStyle>(ForestSetupMenu.DataFolder + "/ForestStyle_Polluted.asset");
        if (progression == null) Debug.LogWarning("Forest: no ForestProgression chosen - doors will ask for nothing.");
        if (GameObject.Find("ForestMaze") != null && !EditorUtility.DisplayDialog("Replace the old maze?", "There is already a 'ForestMaze' in this scene. Delete it and make a new one?", "Replace", "Cancel")) return;
        DeleteOld();
        ForestGateAnimMenu.EnsureGreenGates(false);      // gives the gates their green pictures (quietly)

        // ---------------- 1. the maze (random depth-first), keep the layout with the longest path ----------------
        int W = width, H = height, N = W * H;
        // the grove sits on the same (east) side as the entrance, level with a row far enough above the staging room
        int minRow = Mathf.Min(H - 1, tutorialRoom ? 2 : 1);
        bool[,] bestLink = null; List<int> bestPath = null;
        for (int attempt = 0; attempt < tries; attempt++)
        {
            bool[,] link = new bool[N, 4];
            MakeMaze(link, W, H, new System.Random(seed * 7919 + attempt));
            List<int> path = LongestToBorder(link, W, H, minRow);
            if (bestPath == null || path.Count > bestPath.Count) { bestPath = path; bestLink = link; }
            if (bestPath.Count >= minPath) break;
        }
        // entrance on the east side: mirror the whole maze left-to-right (the entrance cell becomes bottom-right)
        int entranceDir = entrance == EntranceSide.East ? 1 : 3;
        if (entrance == EntranceSide.East)
        {
            var mirrored = new bool[N, 4];
            for (int c = 0; c < N; c++)
            {
                int cx = c % W, cy = c / W, mc = cy * W + (W - 1 - cx);
                for (int d = 0; d < 4; d++) mirrored[mc, d == 1 ? 3 : d == 3 ? 1 : d] = bestLink[c, d];
            }
            bestLink = mirrored;
            var mp = new List<int>();
            foreach (int c in bestPath) mp.Add((c / W) * W + (W - 1 - c % W));
            bestPath = mp;
        }
        int L = bestPath.Count;
        var pathIdx = new int[N];
        for (int i = 0; i < N; i++) pathIdx[i] = -1;
        for (int i = 0; i < L; i++) pathIdx[bestPath[i]] = i;

        int endCell = bestPath[L - 1], startCell = bestPath[0];
        int ex = endCell % W, ey = endCell / W;
        // the last room's gate leads out to the grove on the same side as the entrance (east by default)
        int exitDir = entranceDir;

        // ---------------- 2. rooms ----------------
        var root = new GameObject("ForestMaze");
        Undo.RegisterCreatedObjectUndo(root, "Generate forest maze");
        // an odd room size puts the room edges half a tile off the grid, so shift the whole maze by half a tile to keep walls on the tile grid
        float S = roomSize;
        root.transform.position = origin + (roomSize % 2 == 1 ? new Vector2(0.5f, 0.5f) : Vector2.zero);
        var rooms = new ForestRoom[N];
        var rnd = new System.Random(seed);

        for (int c = 0; c < N; c++)
        {
            int x = c % W, y = c / W;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
            go.name = "Room_" + x + "_" + y + (pathIdx[c] >= 0 ? "" : "_DeadEnd");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(x * S, y * S, 0f);
            var fr = go.GetComponent<ForestRoom>();
            fr.style = style;
            fr.size = roomSize;
            var sides = new ForestRoom.Side[4];
            for (int d = 0; d < 4; d++)
            {
                sides[d] = ForestRoom.Side.Wall;
                if (bestLink[c, d])
                {
                    int nb = (y + Dirs[d].y) * W + (x + Dirs[d].x);
                    sides[d] = (pathIdx[c] >= 0 && pathIdx[nb] == pathIdx[c] + 1) ? ForestRoom.Side.Door : ForestRoom.Side.Open;
                }
            }
            if (c == startCell) sides[entranceDir] = ForestRoom.Side.Open;   // the way in
            if (c == endCell) sides[exitDir] = ForestRoom.Side.Door;         // the last gate, to the grove
            fr.north = sides[0]; fr.east = sides[1]; fr.south = sides[2]; fr.west = sides[3];

            if (pathIdx[c] < 0) { fr.minEnemies = 2; fr.maxEnemies = 3; }
            else if (c == startCell) { fr.minEnemies = 1; fr.maxEnemies = 1; }
            fr.Rebuild();
            rooms[c] = fr;
        }

        // ---------------- 3. doors + stumps along the main path ----------------
        var stepList = new List<ForestGuide.Step>();
        for (int i = 0; i < L; i++)
        {
            int c = bestPath[i];
            ForestRoom fr = rooms[c];
            int d = (i == L - 1) ? exitDir : DirToNext(c, bestPath[i + 1], W);
            ForestDoor door = FindGate(fr, d);
            if (door == null) continue;
            door.id = "r" + (c % W) + "_" + (c / W);
            door.requirements = progression != null ? progression.For(i, L).ToArray() : new ForestRequirement[0];

            var st = (GameObject)PrefabUtility.InstantiatePrefab(stumpPrefab);
            st.transform.SetParent(fr.transform, false);
            // not in the middle of the walkway: a bit off to one side, different in each room
            float sx = rnd.Next(2) == 0 ? -2f : 2f, sy = rnd.Next(2) == 0 ? -1.5f : 1.5f;
            st.transform.localPosition = new Vector3(sx, sy, 0f);
            st.name = "Stump";
            st.GetComponent<ForestStump>().door = door;
            stepList.Add(new ForestGuide.Step { room = fr, door = door, stump = st.GetComponent<ForestStump>() });
        }

        // ---------------- 4. entrance marker ----------------
        var start = new GameObject("ForestStart");
        start.transform.SetParent(root.transform, false);
        float inDir = entranceDir == 1 ? 1f : -1f;                      // +1 = east, -1 = west
        start.transform.position = rooms[startCell].transform.position + new Vector3(inDir * 3f, 0f, 0f);
        var sp = start.AddComponent<SceneSpawnPoint>();
        var so = new SerializedObject(sp);
        so.FindProperty("spawnId").stringValue = entranceSpawnId;
        so.FindProperty("isDefault").boolValue = true;
        so.FindProperty("faceDirection").vector2Value = new Vector2(-inDir, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- 4b. tutorial room (outside the maze, in front of the entrance) ----------------
        GameObject tut = null;
        if (tutorialRoom)
        {
            tut = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
            tut.name = "Room_Tutorial";
            tut.transform.SetParent(root.transform, false);
            tut.transform.position = rooms[startCell].transform.position + new Vector3(inDir * S, 0f, 0f);
            var tr = tut.GetComponent<ForestRoom>();
            tr.style = style;
            tr.size = roomSize;
            tr.minEnemies = 0; tr.maxEnemies = 0;
            if (entranceDir == 1) { tr.west = ForestRoom.Side.Open; tr.east = ForestRoom.Side.Open; }
            else { tr.east = ForestRoom.Side.Open; tr.west = ForestRoom.Side.Open; }
            tr.Rebuild();
            Transform scen = tut.transform.Find("Built/Scenery");           // keep the staging room tidy: no trees on the signs or the fairy
            if (scen != null) Object.DestroyImmediate(scen.gameObject);
            // the way in is now the far edge of the tutorial room
            start.transform.position = tut.transform.position + new Vector3(inDir * 3f, 0f, 0f);
            PlaceTutorialSigns(tut.transform, inDir);
        }
        if (makeEditLayers) ForestToolsMenu.EnsureEditLayers();

        // ---------------- 5. the grove ----------------
        GameObject sickPrefab = null, doorPrefab = null;
        bool haveSick = ForestSickFairyMenu.Ensure(out sickPrefab, out doorPrefab);      // shows its own message if the new art is missing
        ForestShortcutDoor groveDoor = BuildGrove(root.transform, rooms[endCell].transform.position, exitDir, zonePrefab, fairyPrefab, haveSick ? doorPrefab : null, rnd);

        // ---------------- 6. the sick fairy, the fairy doors, and the guide she reads from ----------------
        Transform staging = tut != null ? tut.transform : rooms[startCell].transform;
        if (haveSick)
        {
            // the sick fairy: just before the maze proper
            var sick = (GameObject)PrefabUtility.InstantiatePrefab(sickPrefab);
            sick.name = "SickFairy";
            sick.transform.SetParent(staging, false);
            sick.transform.localPosition = tut != null ? new Vector3(-inDir * 2.2f, -1.0f, 0f) : new Vector3(inDir * 2.8f, 3.0f, 0f);

            // the fairy door in the staging room (north wall; south wall if there is a gap in the north)
            bool north = tut != null || rooms[startCell].north == ForestRoom.Side.Wall;
            var doorGo = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            doorGo.name = "FairyDoor_Shortcut";
            doorGo.transform.SetParent(staging, false);
            doorGo.transform.localPosition = north ? new Vector3(inDir * 0.7f, S * 0.5f - 1f, 0f) : new Vector3(inDir * 0.7f, -(S * 0.5f - 0.5f), 0f);
            var sd = doorGo.GetComponent<ForestShortcutDoor>();
            sd.requireFlag = "forest_grove_reached";
            sd.arriveOffset = north ? new Vector2(0f, -1.3f) : new Vector2(0f, 2.3f);
            sd.arriveFacing = north ? Vector2.down : Vector2.up;
            if (groveDoor != null) { sd.partner = groveDoor; groveDoor.partner = sd; groveDoor.requireFlag = ""; EditorUtility.SetDirty(groveDoor); }
            EditorUtility.SetDirty(sd);
        }

        var guideGo = new GameObject("ForestGuide");
        guideGo.transform.SetParent(root.transform, false);
        var guide = guideGo.AddComponent<ForestGuide>();
        guide.steps = stepList.ToArray();
        var everyRoom = new List<ForestRoom>(rooms);
        if (tut != null) everyRoom.Add(tut.GetComponent<ForestRoom>());
        guide.allRooms = everyRoom.ToArray();
        EditorUtility.SetDirty(guide);

        Selection.activeGameObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        string msg = "Maze built: " + N + " rooms, " + L + " of them on the main path (each with a gate and a stump), " + (N - L) + " dead ends, and the grove after the last gate." +
                     (L < minPath ? "\n\nThe path is shorter than you wanted (" + L + "). Try a different seed or a bigger grid." : "") +
                     "\n\nThe entrance is on the " + (entranceDir == 1 ? "east side of the bottom-right" : "west side of the bottom-left") + (tutorialRoom ? " room, through the tutorial room (read the signs)" : " room") + " (marker 'ForestStart', spawn id '" + entranceSpawnId + "'). Connect it to your farm with a SceneExit." +
                     (haveSick ? "\n\nThe sick fairy waits " + (tut != null ? "in the tutorial room" : "in the first room") + ", and a fairy door there (sealed until the maze is finished) leads straight to the grove." : "\n\nThe sick fairy and fairy door were NOT added: copy the four new sick-fairy pictures into your forest art folder and generate again.") +
                     (makeEditLayers ? "\n\nTo edit the scene by hand: Farm > Level Editors > Forest Level Editor." : "");
        Debug.Log("Forest: " + msg);
        EditorUtility.DisplayDialog("Forest generated", msg, "OK");
    }

    // ------------------------------------------------------------------ tutorial signs
    private static void PlaceTutorialSigns(Transform room, float inDir)
    {
        GameObject prefab = ForestToolsMenu.EnsureSignPrefab();
        if (prefab == null) return;
        // x runs from the farm side (outer) towards the maze (inner)
        Sign(prefab, room, inDir, -2.5f, 2.5f, "Welcome",
            "This is the Shadow Forest. Pollution has twisted the trees and turned the animals into shadows.",
            "Deeper in, the forest is a maze of rooms. At the very end is a hidden grove where the pollution has not reached. Someone there needs your help.",
            "Read the other signs here before you go in. You can come back any time.");
        Sign(prefab, room, inDir, -0.5f, -2.5f, "The Shadows",
            "Shadow animals wander the rooms. Each one follows a rhythm: it RESTS, then WARNS (it shakes or flashes), then GOES in one fast burst.",
            "Watch for the warning and step sideways out of its path, then hit it while it rests. Do not stand and trade blows!",
            "Defeated shadows drop Shadow Essence and fade away. They return if you leave a room for a while.");
        Sign(prefab, room, inDir, 1.5f, 2.5f, "Stumps and Gates",
            "Thorny gates block the way to the next room. Each gate has a glowing STUMP in the same room.",
            "Walk up to the stump and press the action button. It lists what it wants, like '5 Carrots', and how many you have already given.",
            "Press Space to offer items. You can give a few at a time and come back: the stump remembers. When it has everything, the gate withers and stays open for good.");
        Sign(prefab, room, inDir, 3.5f, -2.5f, "The Way Through",
            "Bring crops from your farm and stone, coal or ore from the mines. Early rooms ask for little; later rooms ask for more.",
            "Keep going from room to room. Dead-end rooms have extra shadows to fight. The last gate leads to the grove.",
            "Good luck. Look after yourself in there.");
    }

    private static void Sign(GameObject prefab, Transform room, float inDir, float x, float y, string title, params string[] pages)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(room, false);
        go.transform.localPosition = new Vector3(x * inDir * -1f, y, 0f);
        go.name = "Sign_" + title.Replace(" ", "");
        var s = go.GetComponent<ForestSign>();
        s.signTitle = title; s.pages = pages;
        EditorUtility.SetDirty(s);
    }

    // ------------------------------------------------------------------ maze maths
    private static void MakeMaze(bool[,] link, int W, int H, System.Random r)
    {
        var visited = new bool[W * H];
        var stack = new Stack<int>();
        int cur = 0; visited[0] = true; stack.Push(0);
        while (stack.Count > 0)
        {
            cur = stack.Peek();
            int x = cur % W, y = cur / W;
            var options = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Dirs[d].x, ny = y + Dirs[d].y;
                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                if (!visited[ny * W + nx]) options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            int pick = options[r.Next(options.Count)];
            int nb = (y + Dirs[pick].y) * W + (x + Dirs[pick].x);
            link[cur, pick] = true; link[nb, (pick + 2) % 4] = true;
            visited[nb] = true; stack.Push(nb);
        }
    }

    /// <summary>Path from cell 0 (bottom-left) to the farthest cell on the left edge of the grid that is at least 'minRow' rows up.</summary>
    private static List<int> LongestToBorder(bool[,] link, int W, int H, int minRow)
    {
        int N = W * H;
        var dist = new int[N]; var parent = new int[N];
        for (int i = 0; i < N; i++) { dist[i] = -1; parent[i] = -1; }
        var q = new Queue<int>(); q.Enqueue(0); dist[0] = 0;
        while (q.Count > 0)
        {
            int c = q.Dequeue(); int x = c % W, y = c / W;
            for (int d = 0; d < 4; d++)
            {
                if (!link[c, d]) continue;
                int nb = (y + Dirs[d].y) * W + (x + Dirs[d].x);
                if (dist[nb] >= 0) continue;
                dist[nb] = dist[c] + 1; parent[nb] = c; q.Enqueue(nb);
            }
        }
        int best = -1;
        for (int c = 1; c < N; c++)
        {
            int x = c % W, y = c / W;
            bool border = x == 0 && y >= minRow;              // the entrance-side edge (west here; the whole maze is mirrored for an east entrance), a few rows up
            if (!border) continue;
            if (best < 0 || dist[c] > dist[best]) best = c;
        }
        var path = new List<int>();
        for (int c = best; c >= 0; c = parent[c]) path.Add(c);
        path.Reverse();
        return path;
    }

    private static int DirToNext(int a, int b, int W)
    {
        int dx = (b % W) - (a % W), dy = (b / W) - (a / W);
        for (int d = 0; d < 4; d++) if (Dirs[d].x == dx && Dirs[d].y == dy) return d;
        return 0;
    }

    private static ForestDoor FindGate(ForestRoom room, int dir)
    {
        string[] names = { "Wall_N", "Wall_E", "Wall_S", "Wall_W" };
        Transform t = room.transform.Find("Built/" + names[dir] + "/Gate");
        return t != null ? t.GetComponent<ForestDoor>() : null;
    }

    // ------------------------------------------------------------------ grove
    private struct Spot { public Vector2 p; public float r; }

    private ForestShortcutDoor BuildGrove(Transform root, Vector3 endRoomPos, int exitDir, GameObject zonePrefab, GameObject fairyPrefab, GameObject doorPrefab, System.Random rnd)
    {
        Vector2 dir = Dirs[exitDir];
        Vector2 half = groveSize * 0.5f;
        // grove box touches the outer edge of the last room
        Vector2 centre = (Vector2)endRoomPos + dir * (roomSize * 0.5f + (Mathf.Abs(dir.x) > 0 ? half.x : half.y));
        centre = new Vector2(Mathf.Round(centre.x), Mathf.Round(centre.y));      // keep the grove on the tile grid
        var grove = new GameObject("Grove");
        grove.transform.SetParent(root, false);
        grove.transform.position = centre;

        var zone = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab);
        zone.name = "GroveZone"; zone.transform.SetParent(grove.transform, false); zone.transform.localPosition = Vector3.zero;
        zone.GetComponent<ForestGrove>().boxSize = groveSize;
        var zsr = zone.GetComponent<SpriteRenderer>(); if (zsr != null) zsr.enabled = false;

        // floor: patchwork of bright grass
        var floor = new GameObject("GroveFloor"); floor.transform.SetParent(grove.transform, false);
        var tiles = new List<Sprite>();
        for (int i = 0; i < 3; i++)
        {
            string p = ForestSetupMenu.ArtFolder != null ? ForestSetupMenu.ArtFolder + "/floor_grove_" + i + ".png" : null;
            var sp = p != null ? AssetDatabase.LoadAssetAtPath<Sprite>(p) : null;
            if (sp == null) sp = FindSprite("floor_grove_" + i);
            if (sp != null) tiles.Add(sp);
        }
        int gw = Mathf.RoundToInt(groveSize.x), gh = Mathf.RoundToInt(groveSize.y);
        if (tiles.Count > 0)
            for (int y = 0; y < gh; y += 2)
                for (int x = 0; x < gw; x += 2)
                {
                    var t = new GameObject("G"); t.transform.SetParent(floor.transform, false);
                    t.transform.localPosition = new Vector3(-gw * 0.5f + x + 1f, -gh * 0.5f + y + 1f, 0f);
                    var sr = t.AddComponent<SpriteRenderer>();
                    sr.sprite = tiles[rnd.Next(tiles.Count)];
                    sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(2f, 2f); sr.sortingOrder = -20;
                    t.AddComponent<LightingExempt>();
                }

        // invisible walls on the three sides that don't touch the last room
        var walls = new GameObject("GroveWalls"); walls.transform.SetParent(grove.transform, false);
        for (int d = 0; d < 4; d++)
        {
            if (d == (exitDir + 2) % 4) continue;                 // the side facing the maze
            var w = new GameObject("Wall"); w.transform.SetParent(walls.transform, false);
            bool horiz = Dirs[d].y != 0;
            w.transform.localPosition = new Vector3(Dirs[d].x * (half.x + 1f), Dirs[d].y * (half.y + 1f), 0f);
            var bc = w.AddComponent<BoxCollider2D>();
            bc.size = horiz ? new Vector2(groveSize.x + 4f, 2f) : new Vector2(2f, groveSize.y + 4f);
        }

        // scatter pieces
        var placed = new List<Spot>();
        var entry = centre - dir * (Mathf.Abs(dir.x) > 0 ? half.x : half.y);     // middle of the edge touching the maze
        placed.Add(new Spot { p = entry, r = 4.5f });                              // keep the entrance clear
        placed.Add(new Spot { p = centre, r = 3.4f });                             // fairy ring + fairy

        PlaceOne(grove.transform, FindPrefab("Grove_FairyRing"), centre, 0f, placed, rnd, true);
        var fairy = (GameObject)PrefabUtility.InstantiatePrefab(fairyPrefab);
        fairy.transform.SetParent(grove.transform, false);
        fairy.transform.position = centre + new Vector2(0f, 0.2f);

        // ponds on the two far corners
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 far = centre + dir * (half.x * 0.5f * Mathf.Abs(dir.x) + half.y * 0.5f * Mathf.Abs(dir.y));
        PlaceOne(grove.transform, FindPrefab("Grove_PondBig"), far + perp * (Mathf.Abs(perp.x) > 0 ? half.x * 0.55f : half.y * 0.55f), 4.2f, placed, rnd, true);
        PlaceOne(grove.transform, FindPrefab("Grove_PondSmall"), far - perp * (Mathf.Abs(perp.x) > 0 ? half.x * 0.5f : half.y * 0.5f), 2.6f, placed, rnd, true);

        // the fairy door (leads back to the first room): by the entrance, towards one corner
        ForestShortcutDoor groveDoor = null;
        if (doorPrefab != null)
        {
            Vector2 doorPos = entry + dir * 3.2f + perp * ((Mathf.Abs(perp.x) > 0 ? half.x : half.y) - 3.4f);
            var dg = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            dg.name = "FairyDoor_Grove";
            dg.transform.SetParent(grove.transform, false);
            dg.transform.position = doorPos;
            groveDoor = dg.GetComponent<ForestShortcutDoor>();
            placed.Add(new Spot { p = doorPos, r = 2.8f });
        }

        // trees and bushes: around the edges and a few inside
        PlaceMany(grove.transform, "Grove_Tree", 9, centre, half, placed, rnd, 2.4f, true);
        PlaceMany(grove.transform, "Grove_Bush", 12, centre, half, placed, rnd, 1.0f, true);
        // flowers, mushrooms
        PlaceMany(grove.transform, "Grove_FlowersPink", 14, centre, half, placed, rnd, 0.9f, false);
        PlaceMany(grove.transform, "Grove_FlowersBlue", 14, centre, half, placed, rnd, 0.9f, false);
        PlaceMany(grove.transform, "Grove_FlowersYellow", 14, centre, half, placed, rnd, 0.9f, false);
        PlaceMany(grove.transform, "Grove_MushroomRed", 8, centre, half, placed, rnd, 0.9f, false);
        PlaceMany(grove.transform, "Grove_MushroomBlue", 7, centre, half, placed, rnd, 1.2f, false);
        PlaceMany(grove.transform, "Grove_MushroomGlow", 7, centre, half, placed, rnd, 1.2f, false);
        return groveDoor;
    }

    private void PlaceMany(Transform parent, string prefabName, int count, Vector2 centre, Vector2 half, List<Spot> placed, System.Random rnd, float radius, bool edgeBias)
    {
        GameObject prefab = FindPrefab(prefabName);
        if (prefab == null) return;
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float rx = (float)(rnd.NextDouble() * 2 - 1), ry = (float)(rnd.NextDouble() * 2 - 1);
                if (edgeBias && rnd.NextDouble() < 0.65)       // push toward the edges
                {
                    if (rnd.Next(2) == 0) rx = Mathf.Sign(rx) * Mathf.Lerp(0.82f, 0.96f, (float)rnd.NextDouble()); else ry = Mathf.Sign(ry) * Mathf.Lerp(0.82f, 0.96f, (float)rnd.NextDouble());
                }
                Vector2 p = centre + new Vector2(rx * (half.x - 1.2f), ry * (half.y - 1.2f));
                p = new Vector2(Mathf.Round(p.x * 2f) / 2f, Mathf.Round(p.y * 2f) / 2f);
                if (PlaceOne(parent, prefab, p, radius, placed, rnd, false)) break;
            }
        }
    }

    private bool PlaceOne(Transform parent, GameObject prefab, Vector2 p, float radius, List<Spot> placed, System.Random rnd, bool forced)
    {
        if (prefab == null) return false;
        if (!forced)
            foreach (Spot s in placed)
                if (Vector2.Distance(s.p, p) < s.r + radius) return false;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = p;
        placed.Add(new Spot { p = p, r = radius });
        return true;
    }

    private static GameObject FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    private static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        return null;
    }
}
