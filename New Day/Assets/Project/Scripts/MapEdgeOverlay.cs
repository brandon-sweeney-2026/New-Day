using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Hides the edge of the map with a strip of artwork (tree canopy, city rooftops, mountain cliff) drawn ABOVE the player.
///
/// Use: drag a Border prefab into your scene, then
///   1. pick the Side of the map it covers (Top, Bottom, Left or Right),
///   2. drag your ground Tilemap into 'Map To Fit',
///   3. click the three dots on this component and choose "Place Along Map Edge" (it also does this by itself when the game starts).
/// Everything is adjustable: height shown, how far it reaches past the map, how far it overlaps into the map, draw order.
/// For a map with 4 sides, use 4 copies (the corners overlap neatly).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MapEdgeOverlay : MonoBehaviour
{
    public enum Side { Top, Bottom, Left, Right }

    [Header("Where")]
    [SerializeField] private Side side = Side.Top;
    [Tooltip("The tilemap whose outer edge gets covered (usually your Ground tilemap).")]
    [SerializeField] private Tilemap mapToFit;
    [Tooltip("Shift the strip toward the map's inside (+) or outside (-), in tiles. Raise it to cover more.")]
    [SerializeField] private float pushIn = 0f;
    [Tooltip("Extra length added at both ends so corners are covered, in tiles.")]
    [SerializeField] private float extraLength = 6f;
    [Tooltip("Place automatically when the scene starts.")]
    [SerializeField] private bool placeOnStart = true;

    [Header("Look")]
    [Tooltip("How tall the strip is, in tiles (the picture is 5 tiles tall).")]
    [SerializeField] private float thickness = 5f;
    [Tooltip("Above the player (10) and trees so it overlaps them.")]
    [SerializeField] private int sortingOrder = 50;

    private void Start()
    {
        if (placeOnStart && mapToFit != null) Place();
    }

    [ContextMenu("Place Along Map Edge")]
    public void Place()
    {
        if (mapToFit == null) { Debug.LogWarning("MapEdgeOverlay: drag your ground Tilemap into 'Map To Fit'.", this); return; }

        var sr = GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.sortingOrder = sortingOrder;

        mapToFit.CompressBounds();
        BoundsInt cb = mapToFit.cellBounds;
        Vector3 min = mapToFit.CellToWorld(cb.min);
        Vector3 max = mapToFit.CellToWorld(cb.max);
        float cx = (min.x + max.x) * 0.5f, cy = (min.y + max.y) * 0.5f;
        float width = Mathf.Abs(max.x - min.x), height = Mathf.Abs(max.y - min.y);

        // The picture hangs DOWN from its top edge. Rotation turns it to hang inward from each side.
        switch (side)
        {
            case Side.Top:
                transform.position = new Vector3(cx, max.y - pushIn, 0f);
                transform.rotation = Quaternion.identity;
                sr.size = new Vector2(width + extraLength * 2f, thickness);
                break;
            case Side.Bottom:
                transform.position = new Vector3(cx, min.y + pushIn, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                sr.size = new Vector2(width + extraLength * 2f, thickness);
                break;
            case Side.Left:
                transform.position = new Vector3(min.x + pushIn, cy, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                sr.size = new Vector2(height + extraLength * 2f, thickness);
                break;
            case Side.Right:
                transform.position = new Vector3(max.x - pushIn, cy, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                sr.size = new Vector2(height + extraLength * 2f, thickness);
                break;
        }
    }
}
