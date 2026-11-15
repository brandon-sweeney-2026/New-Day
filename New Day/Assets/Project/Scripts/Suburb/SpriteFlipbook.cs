using UnityEngine;

/// <summary>
/// Plays a short picture loop on a SpriteRenderer (the slush machines swirl, signs blink...).
/// Put the frames in the list and set the speed. "Only When Player Near" saves a little work far away.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    [Min(0.1f)] public float framesPerSecond = 3f;
    [Tooltip("Start at a random frame so a row of machines don't move in step.")] public bool randomStart = true;
    public bool onlyWhenPlayerNear = false;
    public float nearDistance = 18f;

    private SpriteRenderer sr;
    private float offset;
    private Transform player;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (randomStart) offset = Random.value * 10f;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;
        if (onlyWhenPlayerNear)
        {
            if (player == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            if (player != null && Vector2.Distance(player.position, transform.position) > nearDistance) return;
        }
        sr.sprite = frames[Mathf.FloorToInt((Time.time + offset) * framesPerSecond) % frames.Length];
    }
}
