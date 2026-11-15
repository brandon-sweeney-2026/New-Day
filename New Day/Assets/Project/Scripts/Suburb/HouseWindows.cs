using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the windows on the back wall of the house (Pack C). Put it on the "HouseInterior" object next to HouseInterior.
/// The HouseManager tells it which picture to use (the Window Style you own and picked) and where to put the windows
/// (the Window Layout you picked). Windows are plain child objects made at runtime, so you never need to place them by hand.
/// </summary>
[RequireComponent(typeof(HouseInterior))]
public class HouseWindows : MonoBehaviour
{
    [Tooltip("Height of the window's centre above the floor line, in tiles. The back wall is 3 tiles tall, so 1.6 is about the middle.")]
    public float heightAboveWall = 1.6f;
    [Tooltip("Draw order. The wall is 1; furniture is 5.")]
    public int sortingOrder = 2;
    [Tooltip("Keep windows this many tiles away from the room's left and right edges.")]
    public float edgeMargin = 1.2f;

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    public void Show(Sprite style, WindowSpot[] spots)
    {
        var interior = GetComponent<HouseInterior>();
        int n = (style != null && spots != null) ? spots.Length : 0;

        while (pool.Count < n)
        {
            var go = new GameObject("Window_" + pool.Count);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            pool.Add(sr);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            SpriteRenderer sr = pool[i];
            if (i >= n || spots[i] == null) { sr.enabled = false; continue; }
            WindowSpot s = spots[i];
            float x = Mathf.Lerp(edgeMargin, interior.width - edgeMargin, s.along);
            float y = interior.floorRows + heightAboveWall + s.lift;
            sr.transform.localPosition = new Vector3(x, y, 0f);
            sr.sprite = style;
            sr.flipX = s.flip;
            sr.sortingOrder = sortingOrder;
            sr.enabled = true;
        }
    }
}
