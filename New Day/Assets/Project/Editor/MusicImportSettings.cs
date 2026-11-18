using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets good import settings for background music the first time a file lands in Assets/Project/Audio/Music:
/// Streaming (read from disk while it plays, so a long tune does not sit in memory) and Vorbis compression
/// (makes the game much smaller than the raw WAV files).
///
/// It only does this for a NEW file, so if you change a clip's settings by hand in the Inspector, they are kept.
/// Farm > Set Up > Music applies the same settings again to every file in the folder.
/// </summary>
public class MusicImportSettings : AssetPostprocessor
{
    /// <summary>The folder the music files live in (Tools/make_music.ps1 writes there).</summary>
    public const string MusicFolder = "Assets/Project/Audio/Music";

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(MusicFolder + "/")) return;
        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null || !importer.importSettingsMissing) return;   // not new: leave the settings alone
        Apply(importer);
    }

    /// <summary>Writes the music settings onto an audio importer (does not reimport by itself).</summary>
    public static void Apply(AudioImporter importer)
    {
        importer.forceToMono = false;
        importer.loadInBackground = true;

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;   // 0..1; 0.6 sounds the same as the WAV for this music and is about a tenth of the size
        importer.defaultSampleSettings = s;
    }
}
