using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Makes the camera follow the player smoothly. Put this on the Main Camera.
///
///  - Drag the Player into 'target' (or leave empty: it finds the PlayerController).
///  - 'orthographicSize' is the zoom: half the screen height in world units. 5 = 10 tiles tall.
///  - Turn on 'limitToMap' and assign the Ground tilemap so the camera never shows outside the map.
///  - 'pixelSnap' keeps pixel art crisp while the camera moves (set the pixels-per-unit to match your art, 16).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("Feel")]
    [Tooltip("Higher = the camera catches up more slowly (0 = locked to the player).")]
    [SerializeField] [Min(0f)] private float smoothTime = 0.12f;
    [Tooltip("The camera drifts ahead of the direction you are facing by this many tiles.")]
    [SerializeField] private float lookAhead = 0.5f;

    [Header("Zoom")]
    [SerializeField] private float orthographicSize = 5f;

    [Header("Map limits")]
    [SerializeField] private bool limitToMap = false;
    [Tooltip("The camera stays inside this tilemap's painted area.")]
    [SerializeField] private Tilemap boundsTilemap;
    [Tooltip("Extra room allowed past the painted edge, in tiles.")]
    [SerializeField] private float boundsPadding = 0f;

    [Header("Pixel art")]
    [SerializeField] private bool pixelSnap = true;
    [SerializeField] private float pixelsPerUnit = 16f;

    private Camera cam;
    private Vector3 velocity;
    private PlayerController player;
    private Vector2 currentLook;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;
    }

    private void Start()
    {
        if (target == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        else
        {
            player = target.GetComponent<PlayerController>();
        }

        if (target != null) transform.position = Clamp(Desired());   // start on the player, no swoop
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = Clamp(Desired());
        Vector3 next = smoothTime <= 0f
            ? goal
            : Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);

        if (pixelSnap && pixelsPerUnit > 0f)
        {
            next.x = Mathf.Round(next.x * pixelsPerUnit) / pixelsPerUnit;
            next.y = Mathf.Round(next.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    /// <summary>Instantly snap to the target (after a teleport).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = Clamp(Desired());
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        player = newTarget != null ? newTarget.GetComponent<PlayerController>() : null;
    }

    private Vector3 Desired()
    {
        Vector2 wantLook = player != null ? player.FacingDirection * lookAhead : Vector2.zero;
        currentLook = Vector2.Lerp(currentLook, wantLook, 1f - Mathf.Exp(-4f * Time.deltaTime));

        Vector2 p = (Vector2)target.position + offset + currentLook;
        return new Vector3(p.x, p.y, transform.position.z);
    }

    private Vector3 Clamp(Vector3 pos)
    {
        if (!limitToMap || boundsTilemap == null) return pos;

        boundsTilemap.CompressBounds();
        BoundsInt cells = boundsTilemap.cellBounds;
        if (cells.size.x == 0 || cells.size.y == 0) return pos;

        Vector3 min = boundsTilemap.CellToWorld(cells.min) - new Vector3(boundsPadding, boundsPadding, 0f);
        Vector3 max = boundsTilemap.CellToWorld(cells.max) + new Vector3(boundsPadding, boundsPadding, 0f);

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // If the map is smaller than the screen, centre on it instead of clamping.
        pos.x = (max.x - min.x) <= halfW * 2f ? (min.x + max.x) * 0.5f : Mathf.Clamp(pos.x, min.x + halfW, max.x - halfW);
        pos.y = (max.y - min.y) <= halfH * 2f ? (min.y + max.y) * 0.5f : Mathf.Clamp(pos.y, min.y + halfH, max.y - halfH);
        return pos;
    }
}
