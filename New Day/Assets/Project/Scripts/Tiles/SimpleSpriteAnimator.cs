using UnityEngine;

/// <summary>
/// Cycles through a few sprites on a SpriteRenderer (used for the scarecrow, which has 2 frames).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SimpleSpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    [Min(0.1f)] public float framesPerSecond = 2.4f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        int index = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
        spriteRenderer.sprite = frames[index];
    }
}
