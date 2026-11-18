using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which background tune plays in which scene, and how loud. There is one of these, at
/// Assets/Resources/MusicSettings.asset, made by Farm > Set Up > Music. Click it to change the numbers or swap a tune.
///
/// Two scenes that use the SAME clip (Farm and Suburbs, Mine and MineShaft) keep the music playing when you walk
/// between them instead of starting it again.
///
/// The tunes themselves are made by Tools/make_music.ps1 into Assets/Project/Audio/Music.
/// </summary>
[CreateAssetMenu(fileName = "MusicSettings", menuName = "Farm/Music Settings")]
public class MusicSettings : ScriptableObject
{
    /// <summary>One line of the "which tune where" list.</summary>
    [System.Serializable]
    public class SceneTrack
    {
        [Tooltip("The scene's name exactly as in Assets/Project/Scenes, for example Farm, Suburbs, City, Forest, Mine (the mountain trail) or MineShaft.")]
        public string sceneName;

        [Tooltip("The tune for that scene. Leave empty for silence there.")]
        public AudioClip music;
    }

    [Header("Volume")]
    [Tooltip("How loud the music is (before the player's own Sound volume setting).")]
    [Range(0f, 1f)] public float volume = 0.6f;

    [Tooltip("The music is multiplied by this while the player is inside a house (1 = no change, 0.5 = half as loud).")]
    [Range(0f, 1f)] public float indoorVolume = 0.6f;

    [Tooltip("Seconds to fade from one tune to the next when you change scenes.")]
    [Min(0f)] public float fadeSeconds = 1.5f;

    [Header("Which tune plays where")]
    public List<SceneTrack> tracks = new List<SceneTrack>();

    [Tooltip("Played in any scene that is not in the list above. Leave empty for silence.")]
    public AudioClip fallback;

    /// <summary>The tune for a scene, or the fallback (which may be null = silence).</summary>
    public AudioClip ClipFor(string sceneName)
    {
        foreach (SceneTrack t in tracks)
            if (t != null && t.sceneName == sceneName) return t.music;
        return fallback;
    }

    /// <summary>Loads Assets/Resources/MusicSettings.asset, or makes an empty one (no music) if it does not exist yet.</summary>
    public static MusicSettings Load()
    {
        MusicSettings s = Resources.Load<MusicSettings>("MusicSettings");
        if (s != null) return s;

        Debug.Log("MusicSettings: no Assets/Resources/MusicSettings.asset yet, so there is no music. Run Farm > Set Up > Music once.");
        return CreateInstance<MusicSettings>();
    }
}
