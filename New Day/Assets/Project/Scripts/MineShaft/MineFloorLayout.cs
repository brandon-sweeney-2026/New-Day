using UnityEngine;

/// <summary>The different shapes a mine floor can have.</summary>
public enum MineLayoutStyle
{
    Caves,      // one big natural cave with lumpy walls
    Rooms,      // square rooms joined by straight corridors
    Tunnels,    // long winding tunnels
    Chambers    // a few large round halls joined by wide passages
}

/// <summary>
/// The plan of one mine floor, made by MineFloorGenerator. It is only numbers: MineShaftManager turns it into tiles and objects.
/// Cell (0,0) is the bottom-left corner.
/// </summary>
public class MineFloorLayout
{
    public int width, height;
    public MineLayoutStyle style;
    /// <summary>True where the player can walk.</summary>
    public bool[,] floor;
    /// <summary>True where there is a pool (ice, water, lava...). Pools are never floor.</summary>
    public bool[,] pool;
    /// <summary>Where the player arrives (the ladder up).</summary>
    public Vector2Int start;
    /// <summary>Where the stairs down are.</summary>
    public Vector2Int stairs;
    /// <summary>How many floor cells there are.</summary>
    public int floorCells;
    /// <summary>Steps from the start to the stairs, walking around walls.</summary>
    public int stairsDistance;

    public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < width && y < height; }
    public bool IsFloor(int x, int y) { return InBounds(x, y) && floor[x, y]; }

    /// <summary>How many of the 8 cells around this one are floor.</summary>
    public int FloorNeighbours(int x, int y)
    {
        int n = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if ((dx != 0 || dy != 0) && IsFloor(x + dx, y + dy)) n++;
        return n;
    }
}
