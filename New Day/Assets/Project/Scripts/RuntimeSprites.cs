using UnityEngine;

/// <summary>
/// Tiny generated sprites (a white square and a soft circle) so the game never needs extra art
/// for highlights, projectiles and drops. Tint them with SpriteRenderer.color.
/// </summary>
public static class RuntimeSprites
{
    private static Sprite square;
    private static Sprite circle;

    /// <summary>A 1x1 unit white square.</summary>
    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                square.name = "RuntimeSquare";
            }
            return square;
        }
    }

    /// <summary>A white circle, 1 unit wide.</summary>
    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int size = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
                    }
                }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                circle.name = "RuntimeCircle";
            }
            return circle;
        }
    }
}
