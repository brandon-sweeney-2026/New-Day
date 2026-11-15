using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Works out the plan of a mine floor: where the floor is, where the pools are, where the player arrives and where the stairs down are.
/// It only makes a MineFloorLayout (numbers). MineShaftManager paints the tiles and places rocks and enemies from it.
///
/// There are four styles (see MineLayoutStyle). Whatever the style, the result always follows these rules:
///  - a rim of solid rock runs round the outside;
///  - every bit of floor can be reached from the start (cut-off pockets are filled in);
///  - the stairs down are far from the start.
/// The same 'rng' seed always gives the same floor.
/// </summary>
public static class MineFloorGenerator
{
    private const int Border = 3;          // solid rock round the edge, in tiles
    private const int SmallestMap = 32;
    private const int BiggestMap = 100;

    public static MineFloorLayout Generate(int width, int height, MineLayoutStyle style, System.Random rng)
    {
        int w = Mathf.Clamp(width, SmallestMap, BiggestMap), h = Mathf.Clamp(height, SmallestMap, BiggestMap);
        var layout = new MineFloorLayout { width = w, height = h, style = style, floor = new bool[w, h], pool = new bool[w, h] };
        bool[,] f = layout.floor;

        switch (style)
        {
            case MineLayoutStyle.Rooms: CarveRooms(f, w, h, rng); break;
            case MineLayoutStyle.Tunnels: CarveTunnels(f, w, h, rng); break;
            case MineLayoutStyle.Chambers: CarveChambers(f, w, h, rng); break;
            default: CarveCaves(f, w, h, rng); break;
        }

        ClearBorder(f, w, h);
        // a floor that came out tiny (very unlucky dice) gets a plain hall in the middle so it is always playable
        if (KeepLargestRegion(f, w, h) < 250)
        {
            CarveRect(f, w, h, w / 2 - 12, h / 2 - 12, 24, 24);
            KeepLargestRegion(f, w, h);
        }

        MakePools(layout, rng);
        layout.floorCells = Count(f, w, h);
        PickStartAndStairs(layout, rng);
        return layout;
    }

    // ------------------------------------------------------------------ the four styles

    /// <summary>Caves: fill the map with random noise, then smooth it a few times so it clumps into natural caves.</summary>
    private static void CarveCaves(bool[,] f, int w, int h, System.Random rng)
    {
        for (int y = Border; y < h - Border; y++)
            for (int x = Border; x < w - Border; x++)
                f[x, y] = rng.NextDouble() < 0.55;

        var next = new bool[w, h];
        for (int pass = 0; pass < 5; pass++)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // count the rock in the 3 x 3 block around this cell (off the map counts as rock)
                    int rock = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || !f[nx, ny]) rock++;
                        }
                    next[x, y] = rock < 5;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    f[x, y] = next[x, y];
        }
    }

    /// <summary>Rooms: scatter rectangles that don't touch, then join each to its nearest neighbour with a corridor 3 tiles wide.</summary>
    private static void CarveRooms(bool[,] f, int w, int h, System.Random rng)
    {
        var rooms = new List<RectInt>();
        int want = 9 + rng.Next(6);
        for (int attempt = 0; attempt < 400 && rooms.Count < want; attempt++)
        {
            int rw = 7 + rng.Next(10), rh = 6 + rng.Next(8);
            int roomX = w - 2 * Border - rw, roomY = h - 2 * Border - rh;
            if (roomX <= 0 || roomY <= 0) continue;
            var room = new RectInt(Border + rng.Next(roomX), Border + rng.Next(roomY), rw, rh);

            bool clash = false;
            foreach (RectInt other in rooms)
                if (room.xMin - 2 < other.xMax && room.xMax + 2 > other.xMin && room.yMin - 2 < other.yMax && room.yMax + 2 > other.yMin) { clash = true; break; }
            if (clash) continue;

            rooms.Add(room);
            CarveRect(f, w, h, room.xMin, room.yMin, room.width, room.height);
        }

        // join every room to the nearest room made before it (so they all end up connected)...
        for (int i = 1; i < rooms.Count; i++)
        {
            int best = 0; float bestDist = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Vector2.Distance(rooms[i].center, rooms[j].center);
                if (d < bestDist) { bestDist = d; best = j; }
            }
            CarveCorridor(f, w, h, Centre(rooms[i]), Centre(rooms[best]), 1, rng);
        }
        // ...and add a couple of extra corridors so there are loops, not only dead ends
        for (int extra = 0; extra < 2 && rooms.Count > 3; extra++)
            CarveCorridor(f, w, h, Centre(rooms[rng.Next(rooms.Count)]), Centre(rooms[rng.Next(rooms.Count)]), 1, rng);
    }

    /// <summary>Tunnels: a digger wanders about carving a path 3 tiles wide, now and then opening a small cave or starting a side branch.</summary>
    private static void CarveTunnels(bool[,] f, int w, int h, System.Random rng)
    {
        int target = (int)((w - 2 * Border) * (h - 2 * Border) * 0.3f);
        int carved = 0;
        int x = w / 2, y = h / 2;
        int[] stepX = { 1, 0, -1, 0 }, stepY = { 0, 1, 0, -1 };
        int dir = rng.Next(4);
        var dug = new List<Vector2Int>();

        for (int step = 0; step < 200000 && carved < target; step++)
        {
            carved += CarveBlob(f, w, h, x, y, rng.NextDouble() < 0.02 ? 3 : 1);
            dug.Add(new Vector2Int(x, y));

            if (rng.NextDouble() < 0.12) dir = rng.Next(4);                       // turn
            if (rng.NextDouble() < 0.004) { Vector2Int p = dug[rng.Next(dug.Count)]; x = p.x; y = p.y; }   // start a side branch

            int nx = x + stepX[dir], ny = y + stepY[dir];
            if (nx < Border + 2 || ny < Border + 2 || nx > w - Border - 3 || ny > h - Border - 3) { dir = rng.Next(4); continue; }   // hit the edge: turn
            x = nx; y = ny;
        }
    }

    /// <summary>Chambers: a few big round halls with rock pillars in them, joined by wide passages.</summary>
    private static void CarveChambers(bool[,] f, int w, int h, System.Random rng)
    {
        var centres = new List<Vector2Int>();
        var radii = new List<Vector2Int>();
        int want = 4 + rng.Next(3);
        for (int attempt = 0; attempt < 200 && centres.Count < want; attempt++)
        {
            int rx = 7 + rng.Next(7), ry = 6 + rng.Next(6);
            int roomX = w - 2 * (Border + rx) - 2, roomY = h - 2 * (Border + ry) - 2;
            if (roomX <= 0 || roomY <= 0) continue;
            var c = new Vector2Int(Border + rx + 1 + rng.Next(roomX), Border + ry + 1 + rng.Next(roomY));

            bool clash = false;
            for (int i = 0; i < centres.Count; i++)
                if (Mathf.Abs(c.x - centres[i].x) < rx + radii[i].x + 2 && Mathf.Abs(c.y - centres[i].y) < ry + radii[i].y + 2) { clash = true; break; }
            if (clash) continue;

            centres.Add(c); radii.Add(new Vector2Int(rx, ry));
            for (int dy = -ry; dy <= ry; dy++)
                for (int dx = -rx; dx <= rx; dx++)
                    if ((float)(dx * dx) / (rx * rx) + (float)(dy * dy) / (ry * ry) <= 1f) Set(f, w, h, c.x + dx, c.y + dy, true);
        }

        for (int i = 1; i < centres.Count; i++)
        {
            int best = 0; float bestDist = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Vector2.Distance(centres[i], centres[j]);
                if (d < bestDist) { bestDist = d; best = j; }
            }
            CarveCorridor(f, w, h, centres[i], centres[best], 2, rng);
        }

        // pillars: small lumps of rock left standing inside each hall (MakePools turns them into pools)
        for (int i = 0; i < centres.Count; i++)
        {
            int pillars = 1 + rng.Next(3);
            for (int p = 0; p < pillars; p++)
            {
                int px = centres[i].x + rng.Next(-radii[i].x / 2, radii[i].x / 2 + 1);
                int py = centres[i].y + rng.Next(-radii[i].y / 2, radii[i].y / 2 + 1);
                if (Mathf.Abs(px - centres[i].x) < 2 && Mathf.Abs(py - centres[i].y) < 2) continue;   // keep the middle open
                int r = 1 + rng.Next(2);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r + 1) Set(f, w, h, px + dx, py + dy, false);
            }
        }
    }

    // ------------------------------------------------------------------ carving helpers

    private static Vector2Int Centre(RectInt r) { return new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2); }

    private static void Set(bool[,] f, int w, int h, int x, int y, bool value)
    {
        if (x >= Border && y >= Border && x < w - Border && y < h - Border) f[x, y] = value;
    }

    private static void CarveRect(bool[,] f, int w, int h, int x0, int y0, int rw, int rh)
    {
        for (int y = y0; y < y0 + rh; y++)
            for (int x = x0; x < x0 + rw; x++)
                Set(f, w, h, x, y, true);
    }

    /// <summary>Carves a square of floor around a cell and returns how many cells were rock before.</summary>
    private static int CarveBlob(bool[,] f, int w, int h, int cx, int cy, int radius)
    {
        int made = 0;
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < Border || y < Border || x >= w - Border || y >= h - Border || f[x, y]) continue;
                f[x, y] = true; made++;
            }
        return made;
    }

    /// <summary>An L-shaped corridor from a to b. 'halfWidth' 1 = 3 tiles wide, 2 = 5 tiles wide.</summary>
    private static void CarveCorridor(bool[,] f, int w, int h, Vector2Int a, Vector2Int b, int halfWidth, System.Random rng)
    {
        // go sideways first or up/down first, at random
        Vector2Int corner = rng.Next(2) == 0 ? new Vector2Int(b.x, a.y) : new Vector2Int(a.x, b.y);
        CarveLine(f, w, h, a, corner, halfWidth);
        CarveLine(f, w, h, corner, b, halfWidth);
    }

    private static void CarveLine(bool[,] f, int w, int h, Vector2Int a, Vector2Int b, int halfWidth)
    {
        int dx = System.Math.Sign(b.x - a.x), dy = System.Math.Sign(b.y - a.y);
        int x = a.x, y = a.y;
        for (int guard = 0; guard < 400; guard++)
        {
            CarveBlob(f, w, h, x, y, halfWidth);
            if (x == b.x && y == b.y) break;
            if (x != b.x) x += dx;
            if (y != b.y) y += dy;
        }
    }

    private static void ClearBorder(bool[,] f, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x < Border || y < Border || x >= w - Border || y >= h - Border) f[x, y] = false;
    }

    private static int Count(bool[,] f, int w, int h)
    {
        int n = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (f[x, y]) n++;
        return n;
    }

    // ------------------------------------------------------------------ making it one connected floor

    /// <summary>
    /// Spreads out from a cell like spilled water, over cells where 'open' matches 'wanted', writing 'label' into 'marks'.
    /// Returns how many cells it reached. (This is called a flood fill.)
    /// </summary>
    private static int Flood(bool[,] open, bool wanted, int[,] marks, int w, int h, int startX, int startY, int label, int[] queue)
    {
        int head = 0, tail = 0;
        queue[tail++] = startX + startY * w;
        marks[startX, startY] = label;
        while (head < tail)
        {
            int cell = queue[head++];
            int x = cell % w, y = cell / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h || marks[nx, ny] != 0 || open[nx, ny] != wanted) continue;
                marks[nx, ny] = label;
                queue[tail++] = nx + ny * w;
            }
        }
        return tail;
    }

    /// <summary>Fills in every pocket of floor except the biggest one, so the whole floor is connected. Returns the size of what is left.</summary>
    private static int KeepLargestRegion(bool[,] f, int w, int h)
    {
        var marks = new int[w, h];
        var queue = new int[w * h];
        int label = 0, bestLabel = 0, bestSize = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!f[x, y] || marks[x, y] != 0) continue;
                label++;
                int size = Flood(f, true, marks, w, h, x, y, label, queue);
                if (size > bestSize) { bestSize = size; bestLabel = label; }
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (f[x, y] && marks[x, y] != bestLabel) f[x, y] = false;
        return bestSize;
    }

    // ------------------------------------------------------------------ pools

    /// <summary>
    /// Pools are places the player cannot walk, drawn as ice / pits / water / quicksand / lava.
    /// 1. Small islands of rock standing inside the floor become pools.
    /// 2. A few round pools are added in wide open areas. Each one keeps a ring of floor all round it, so it can never block the way.
    /// </summary>
    private static void MakePools(MineFloorLayout layout, System.Random rng)
    {
        int w = layout.width, h = layout.height;
        bool[,] f = layout.floor;

        // 1. rock that is not joined to the outside rock, in lumps of 80 tiles or fewer
        var marks = new int[w, h];
        var queue = new int[w * h];
        Flood(f, false, marks, w, h, 0, 0, 1, queue);              // label 1 = the outside rock
        int label = 1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (f[x, y] || marks[x, y] != 0) continue;
                label++;
                int size = Flood(f, false, marks, w, h, x, y, label, queue);
                if (size > 80) continue;
                for (int i = 0; i < size; i++) layout.pool[queue[i] % w, queue[i] / w] = true;
            }

        // 2. extra round pools in the open
        int tries = w * h / 250;
        for (int t = 0; t < tries; t++)
        {
            int r = 1 + rng.Next(2);
            int cx = Border + r + 2 + rng.Next(Mathf.Max(1, w - 2 * (Border + r + 2)));
            int cy = Border + r + 2 + rng.Next(Mathf.Max(1, h - 2 * (Border + r + 2)));

            bool open = true;
            for (int dy = -r - 2; dy <= r + 2 && open; dy++)
                for (int dx = -r - 2; dx <= r + 2; dx++)
                    if (!layout.IsFloor(cx + dx, cy + dy)) { open = false; break; }
            if (!open) continue;

            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r + 1) { f[cx + dx, cy + dy] = false; layout.pool[cx + dx, cy + dy] = true; }
        }
    }

    // ------------------------------------------------------------------ start and stairs

    /// <summary>Steps from a cell to every floor cell, walking around walls (-1 = cannot be reached).</summary>
    private static int[,] Distances(MineFloorLayout layout, Vector2Int from)
    {
        int w = layout.width, h = layout.height;
        var dist = new int[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dist[x, y] = -1;

        var queue = new int[w * h];
        int head = 0, tail = 0;
        queue[tail++] = from.x + from.y * w;
        dist[from.x, from.y] = 0;
        while (head < tail)
        {
            int cell = queue[head++];
            int x = cell % w, y = cell / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (!layout.IsFloor(nx, ny) || dist[nx, ny] >= 0) continue;
                dist[nx, ny] = dist[x, y] + 1;
                queue[tail++] = nx + ny * w;
            }
        }
        return dist;
    }

    private static void PickStartAndStairs(MineFloorLayout layout, System.Random rng)
    {
        int w = layout.width, h = layout.height;

        // "roomy" cells have floor on all 8 sides, so a ladder or stairs there never blocks a corridor
        var roomy = new List<Vector2Int>();
        var any = new List<Vector2Int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!layout.floor[x, y]) continue;
                any.Add(new Vector2Int(x, y));
                if (layout.FloorNeighbours(x, y) == 8) roomy.Add(new Vector2Int(x, y));
            }
        if (roomy.Count < 2) roomy = any;
        if (roomy.Count == 0) { layout.start = layout.stairs = new Vector2Int(w / 2, h / 2); return; }

        // the start: the roomy cell farthest from a random one (this puts it near an end of the floor, not in the middle)
        int[,] dist = Distances(layout, roomy[rng.Next(roomy.Count)]);
        Vector2Int start = roomy[0]; int far = -1;
        foreach (Vector2Int c in roomy)
            if (dist[c.x, c.y] > far) { far = dist[c.x, c.y]; start = c; }
        layout.start = start;

        // the stairs: a random roomy cell in the farthest quarter from the start
        dist = Distances(layout, start);
        far = 0;
        foreach (Vector2Int c in roomy) far = Mathf.Max(far, dist[c.x, c.y]);
        var farCells = new List<Vector2Int>();
        foreach (Vector2Int c in roomy)
            if (dist[c.x, c.y] >= far * 0.75f && c != start) farCells.Add(c);
        layout.stairs = farCells.Count > 0 ? farCells[rng.Next(farCells.Count)] : roomy[roomy.Count - 1];
        layout.stairsDistance = dist[layout.stairs.x, layout.stairs.y];
    }
}
