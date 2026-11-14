using UnityEngine;

/// <summary>
/// An invisible wall. The player (and anything with physics) can't walk through it. Use it to close off parts of the map you haven't finished yet.
/// In the editors you see it as a red hatched box (and a red outline in the Scene view); when the game runs the picture is hidden.
///
/// Size is in tiles. Place it with any level editor (Objects palette: Barrier_1x1, Barrier_4x1, Barrier_1x4, Barrier_4x4) or drag the prefab into the scene, then change Size here.
/// The box stands on the placement point: its bottom edge is where you click, and it grows up and sideways (centred left-right).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[ExecuteAlways]
public class InvisibleBarrier : MonoBehaviour
{
    [Tooltip("Width and height in tiles.")]
    public Vector2 size = Vector2.one;
    [Tooltip("Keep the red picture visible while playing (to see where your barriers are).")]
    public bool showWhilePlaying = false;

    private void Awake()
    {
        Apply();
        if (Application.isPlaying && !showWhilePlaying)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }

    private void OnValidate()
    {
        ApplyCollider();
#if UNITY_EDITOR
        // changing the picture's size is not allowed while Unity is validating, so do it a moment later
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) ApplyPicture(); };
#endif
    }

    private void Apply() { ApplyCollider(); ApplyPicture(); }

    private void ApplyCollider()
    {
        size = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = false;
            col.size = size;
            col.offset = new Vector2(0f, size.y * 0.5f);
        }
    }

    private void ApplyPicture()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null && sr.drawMode != SpriteDrawMode.Simple && sr.size != size) sr.size = size;
    }

    private void OnDrawGizmos()
    {
        Vector3 c = transform.position + new Vector3(0f, size.y * 0.5f, 0f);
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawWireCube(c, new Vector3(size.x, size.y, 0f));
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.12f);
        Gizmos.DrawCube(c, new Vector3(size.x, size.y, 0f));
    }
}
