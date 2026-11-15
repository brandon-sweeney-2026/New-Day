using UnityEngine;

/// <summary>
/// A smooth game clock for NPCs. TimeManager moves in whole minutes (a step every ~0.7 s); walking needs smaller steps, so this adds the
/// fraction in between. It freezes while the game is paused (dialogue, menus) and learns the clock speed by itself.
/// </summary>
public static class NpcClock
{
    private static int lastWhole = -1;
    private static int lastFrame = -1;
    private static float fracSeconds;
    private static float secondsPerMinute = 0.7f;

    /// <summary>Minutes since midnight (can pass 1440 after midnight, like TimeManager).</summary>
    public static float Minutes
    {
        get
        {
            var tm = TimeManager.Instance;
            if (tm == null) return 0f;
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                if (!tm.Paused) fracSeconds += Time.unscaledDeltaTime;
            }
            int w = Mathf.FloorToInt(tm.HoursDecimal * 60f + 0.01f);
            if (w != lastWhole)
            {
                // learn the clock speed from normal one-minute steps
                if (lastWhole >= 0 && w == lastWhole + 1 && fracSeconds > 0.05f && fracSeconds < 10f)
                    secondsPerMinute = Mathf.Lerp(secondsPerMinute, fracSeconds, 0.2f);
                lastWhole = w;
                fracSeconds = 0f;
            }
            return w + Mathf.Clamp01(fracSeconds / Mathf.Max(0.05f, secondsPerMinute)) * 0.999f;
        }
    }
}
