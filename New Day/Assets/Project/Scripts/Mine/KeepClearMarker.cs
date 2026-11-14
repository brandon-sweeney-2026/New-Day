using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paint one of these (Level Editor > Objects > Marker_KeepClear) where rocks and enemies must NOT appear:
/// the mine entrance, the player's start, the foot of a stairway, a narrow part of the trail.
/// Rock and enemy zones notice every marker in the scene automatically. The picture is only visible while you edit.
/// </summary>
public class KeepClearMarker : MonoBehaviour
{
    public static readonly List<KeepClearMarker> All = new List<KeepClearMarker>();

    [Tooltip("Nothing appears closer than this (world units).")]
    public float radius = 3f;

    private void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;        // editor-only picture
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.45f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
