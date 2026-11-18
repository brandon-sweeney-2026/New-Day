using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Music. Links the background music to the scenes:
///   1. gives every file in Assets/Project/Audio/Music the music import settings (Streaming, Vorbis);
///   2. makes Assets/Resources/MusicSettings.asset with the starting "which tune where" table below.
///
/// Safe to run again: scenes already in the list keep the tune you chose; only missing scenes (or empty slots) are filled.
/// The tunes are made by Tools/make_music.ps1.
/// </summary>
public static class MusicSetupMenu
{
    private const string SettingsPath = "Assets/Resources/MusicSettings.asset";

    // Scene name -> music file name (without .wav). Farm and Suburbs share a tune, so do the mountain trail and the mine.
    private static readonly string[,] DefaultTracks =
    {
        { "Farm",      "music_farm" },
        { "Suburbs",   "music_farm" },
        { "City",      "music_city" },
        { "Forest",    "music_forest" },
        { "Mine",      "music_mountain" },   // "Mine" is the OUTDOOR mountain trail
        { "MineShaft", "music_mountain" },   // the underground mine
    };

    [MenuItem("Farm/Set Up/Music", false, 110)]
    private static void Menu()
    {
        EditorUtility.DisplayDialog("Music", Run(), "OK");
    }

    /// <summary>Does the setup without a pop-up and returns a sentence saying what happened.</summary>
    public static string Run()
    {
        // 1. import settings
        int fixedFiles = 0;
        if (AssetDatabase.IsValidFolder(MusicImportSettings.MusicFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicImportSettings.MusicFolder }))
            {
                AudioImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
                if (importer == null) continue;
                MusicImportSettings.Apply(importer);
                importer.SaveAndReimport();
                fixedFiles++;
            }
        }

        // 2. the settings asset
        MusicSettings settings = AssetDatabase.LoadAssetAtPath<MusicSettings>(SettingsPath);
        bool made = settings == null;
        if (made)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<MusicSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        int linked = 0;
        string missing = "";
        for (int i = 0; i < DefaultTracks.GetLength(0); i++)
        {
            string scene = DefaultTracks[i, 0];
            string clipPath = MusicImportSettings.MusicFolder + "/" + DefaultTracks[i, 1] + ".wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null) { missing += "\n  " + clipPath; continue; }

            MusicSettings.SceneTrack line = settings.tracks.Find(t => t != null && t.sceneName == scene);
            if (line == null) { line = new MusicSettings.SceneTrack { sceneName = scene }; settings.tracks.Add(line); }
            if (line.music != null) continue;       // keep a tune that was already chosen
            line.music = clip;
            linked++;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        string msg = (made ? "Made " + SettingsPath : "Added to " + SettingsPath) + ": linked " + linked + " scene(s) to their music, "
                   + "and set import settings on " + fixedFiles + " music file(s).";
        if (missing.Length > 0) msg += "\n\nThese music files were not found (run Tools/make_music.ps1 first):" + missing;
        return msg;
    }
}
