using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the background music. It creates itself when the game starts and stays alive through every scene change,
/// so there is nothing to put in a scene.
///
/// Each time a scene loads it asks MusicSettings which tune belongs there:
///   - the same tune as now (Farm to Suburbs, Mine to MineShaft): it just keeps playing, no restart;
///   - a different tune: the old one fades out while the new one fades in;
///   - no tune: the music fades to silence.
/// Inside a house the music gets quieter (MusicSettings > Indoor Volume).
///
/// Other scripts can change the music for a moment (a boss fight, a festival) with
///   MusicManager.Instance.PlayOverride(myClip);   and back to normal with   MusicManager.Instance.ClearOverride();
/// </summary>
public class MusicManager : MonoBehaviour
{
    /// <summary>The one music player (null before the game starts).</summary>
    public static MusicManager Instance { get; private set; }

    private MusicSettings settings;
    private readonly AudioSource[] sources = new AudioSource[2];   // two players, so one can fade out while the other fades in
    private int active;                                            // which of the two is the "new" one
    private AudioClip current;                                     // the tune playing now
    private AudioClip sceneClip;                                   // the tune the current scene wants
    private AudioClip overrideClip;                                // set by PlayOverride
    private float fade = 1f;                                       // 0 = cross-fade just started, 1 = finished
    private float outgoingStart;                                   // volume of the old player when the fade began
    private float indoorLevel = 1f;                                // eases between 1 and Indoor Volume

    // "Enter Play Mode Options" can keep static values between Play sessions, so clear them first.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("MusicManager");
        DontDestroyOnLoad(go);
        go.AddComponent<MusicManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        settings = MusicSettings.Load();

        for (int i = 0; i < 2; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;      // 2D: the same loudness wherever the camera is
            src.priority = 0;           // music is never cut off to make room for sound effects
            src.volume = 0f;
            sources[i] = src;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;    // an extra scene loaded on top does not change the music
        EnsureListener();
        sceneClip = settings.ClipFor(scene.name);
        if (overrideClip == null) SwitchTo(sceneClip);
    }

    /// <summary>Plays this tune instead of the scene's own until ClearOverride is called (or another override).</summary>
    public void PlayOverride(AudioClip clip)
    {
        overrideClip = clip;
        SwitchTo(clip != null ? clip : sceneClip);
    }

    /// <summary>Goes back to the scene's own tune.</summary>
    public void ClearOverride()
    {
        overrideClip = null;
        SwitchTo(sceneClip);
    }

    // Starts a cross-fade to "clip" (null = fade to silence). Does nothing if that tune is already playing.
    private void SwitchTo(AudioClip clip)
    {
        if (clip == current) return;
        current = clip;

        outgoingStart = sources[active].volume;
        active = 1 - active;
        AudioSource src = sources[active];
        src.Stop();
        src.clip = clip;
        src.volume = 0f;
        if (clip != null) src.Play();
        fade = 0f;
    }

    private void Update()
    {
        // unscaled time, so the fades still work while the game is paused
        float dt = Time.unscaledDeltaTime;

        float indoorTarget = HouseInterior.PlayerIsInside ? settings.indoorVolume : 1f;
        indoorLevel = Mathf.MoveTowards(indoorLevel, indoorTarget, dt * 2f);   // about half a second to change
        float target = settings.volume * indoorLevel;

        fade = settings.fadeSeconds <= 0f ? 1f : Mathf.MoveTowards(fade, 1f, dt / settings.fadeSeconds);

        AudioSource incoming = sources[active], outgoing = sources[1 - active];
        incoming.volume = target * fade;
        outgoing.volume = outgoingStart * (1f - fade);
        if (fade >= 1f && outgoing.isPlaying) outgoing.Stop();
    }

    // Without an AudioListener nothing is heard at all. The scenes do not have one yet, so add one to the main camera.
    private void EnsureListener()
    {
        AudioListener mine = GetComponent<AudioListener>();
        AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);

        // A listener from an earlier scene, kept on this object, is not needed once the new scene has its own.
        if (mine != null && all.Length > 1) { Destroy(mine); return; }
        if (all.Length > 0) return;

        Camera cam = Camera.main;
        if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        else gameObject.AddComponent<AudioListener>();
    }
}
