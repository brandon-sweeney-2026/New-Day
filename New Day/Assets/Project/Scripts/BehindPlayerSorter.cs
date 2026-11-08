using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes the player walk BEHIND tall things (the house roof, trees) when standing above them on the map,
/// and IN FRONT when standing below them. It works by itself, with no project sorting settings needed.
///
/// Put it on an object with a SortingGroup (the house) or a SpriteRenderer (a tree).
///  - Threshold Offset: the height (in tiles, from this object's pivot) where "in front" switches to "behind".
///    The house uses 2 (the middle of its 4-tile footprint). Trees use 0 (their base).
/// </summary>
public class BehindPlayerSorter : MonoBehaviour
{
    [Tooltip("When the player's feet are ABOVE this height (pivot + offset) the player is drawn behind this object.")]
    public float thresholdOffset = 2f;

    private SortingGroup group;
    private SpriteRenderer sr;
    private Transform player;
    private SpriteRenderer playerSr;

    private void Awake()
    {
        group = GetComponent<SortingGroup>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
            playerSr = pc.GetComponent<SpriteRenderer>();
        }

        int playerOrder = playerSr != null ? playerSr.sortingOrder : 10;
        bool playerBehind = player.position.y > transform.position.y + thresholdOffset;
        int order = playerBehind ? playerOrder + 5 : playerOrder - 5;   // +-5 so the held tool (player +-1) stays with the player

        if (group != null) { if (group.sortingOrder != order) group.sortingOrder = order; }
        else if (sr != null && sr.sortingOrder != order) sr.sortingOrder = order;
    }
}
