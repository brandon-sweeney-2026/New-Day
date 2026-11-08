/// <summary>
/// Small global message bus so scripts can talk to the HUD without referencing it.
/// </summary>
public static class GameEvents
{
    public static event System.Action<string> Toast;

    /// <summary>Shows a short message near the bottom of the screen ("Sold 3 Turnip for 120g").</summary>
    public static void ShowToast(string message)
    {
        if (Toast != null) Toast(message);
    }
}
