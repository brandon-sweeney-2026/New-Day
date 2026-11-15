using UnityEngine;

/// <summary>
/// A switch that says "the player is underground right now" (in the mine shaft).
/// MineShaftManager turns it on when the mine shaft scene opens and off when it closes.
///
/// Two other scripts read it:
///  - WeatherEffects: no rain, snow, fog or wind underground.
///  - LightingManager: no sun underground. It is always dark, lamps are always lit, and the
///    faint light there is comes from Ambient Color and Ambient Brightness below.
/// </summary>
public static class Underground
{
    /// <summary>True while the player is in the mine shaft.</summary>
    public static bool Active;

    /// <summary>The colour of the faint light everywhere underground (each mine theme sets its own).</summary>
    public static Color AmbientColor = new Color(0.6f, 0.65f, 0.8f);

    /// <summary>How bright it is away from lamps: 0 = pitch black, 1 = daylight.</summary>
    public static float AmbientBrightness = 0.35f;

    // Makes sure the switch is off when the game starts (matters when Unity's "Enter Play Mode Options" skip the usual reset).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnStart() { Active = false; }
}
