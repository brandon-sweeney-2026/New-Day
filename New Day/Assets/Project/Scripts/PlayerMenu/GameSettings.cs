using System;
using UnityEngine;

/// <summary>
/// The player's own settings: UI size, sound volume, screen brightness and fullscreen.
/// They are saved on the computer (PlayerPrefs), so they are remembered the next time the game starts.
///
///   GameSettings.UiScale = 1.2f;        // 1 = normal size
///   GameSettings.MasterVolume = 0.5f;   // 0 = silent, 1 = full
///   GameSettings.Brightness = 1.1f;     // 1 = normal
///
/// This is a plain static class, so there is nothing to put in the scene.
/// ScreenSettingsApplier is the script that actually resizes the UI and darkens or brightens the screen.
/// </summary>
public static class GameSettings
{
    // The limits of each slider. Change these if you want a wider or narrower range.
    public const float MinUiScale = 0.75f, MaxUiScale = 1.5f;
    public const float MinBrightness = 0.5f, MaxBrightness = 1.5f;

    private static bool loaded;
    private static float uiScale = 1f;
    private static float masterVolume = 1f;
    private static float brightness = 1f;

    /// <summary>Raised whenever any setting changes.</summary>
    public static event Action OnChanged;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        OnChanged = null;
        loaded = false;
    }

    // Read the saved settings before the first scene starts, so the volume is right from the first frame.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStart()
    {
        Load();
    }

    /// <summary>Size of all on-screen menus and the HUD. 1 = normal.</summary>
    public static float UiScale
    {
        get { Load(); return uiScale; }
        set { Load(); uiScale = Mathf.Clamp(value, MinUiScale, MaxUiScale); Save(); }
    }

    /// <summary>Loudness of everything. 0 = silent, 1 = full.</summary>
    public static float MasterVolume
    {
        get { Load(); return masterVolume; }
        set { Load(); masterVolume = Mathf.Clamp01(value); Save(); }
    }

    /// <summary>Screen brightness. 1 = normal, lower = darker, higher = brighter.</summary>
    public static float Brightness
    {
        get { Load(); return brightness; }
        set { Load(); brightness = Mathf.Clamp(value, MinBrightness, MaxBrightness); Save(); }
    }

    /// <summary>Fullscreen on or off. (Does nothing inside the Unity editor; it works in a built game.)</summary>
    public static bool Fullscreen
    {
        get { return Screen.fullScreen; }
        set
        {
            Screen.fullScreen = value;
            if (OnChanged != null) OnChanged();
        }
    }

    /// <summary>Puts every setting back to normal.</summary>
    public static void ResetToDefaults()
    {
        Load();
        uiScale = 1f;
        masterVolume = 1f;
        brightness = 1f;
        Save();
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("settings_ui_scale", 1f), MinUiScale, MaxUiScale);
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("settings_volume", 1f));
        brightness = Mathf.Clamp(PlayerPrefs.GetFloat("settings_brightness", 1f), MinBrightness, MaxBrightness);
        AudioListener.volume = masterVolume;
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat("settings_ui_scale", uiScale);
        PlayerPrefs.SetFloat("settings_volume", masterVolume);
        PlayerPrefs.SetFloat("settings_brightness", brightness);
        PlayerPrefs.Save();

        // AudioListener.volume is Unity's master volume: it scales every sound in the game.
        AudioListener.volume = masterVolume;

        if (OnChanged != null) OnChanged();
    }
}
