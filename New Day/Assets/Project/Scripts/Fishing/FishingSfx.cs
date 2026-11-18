using UnityEngine;

/// <summary>
/// The fishing sound effects, made in code the first time they play (so no sound files are needed):
///   Ding   - a bright bell when something bites
///   Plop   - the bobber landing in the water
///   Splash - pulling a fish or a piece of junk out of the water
///
/// To use your own sounds instead, drag them into the slots under "Sounds" on the FishingManager.
/// The game's master volume (Settings tab) still applies, because Unity's AudioListener scales every sound.
/// </summary>
public static class FishingSfx
{
    private const int Rate = 44100;     // samples per second

    private static AudioSource source;
    private static AudioClip ding, plop, splash;

    /// <summary>Plays 'clip' once. 'volume' is 0 to 1.</summary>
    public static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (source == null)
        {
            var go = new GameObject("FishingSfx");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;           // 2D: same loudness wherever the camera is
        }
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>A bell: a high note plus a few quieter overtones, each dying away at its own speed.</summary>
    public static AudioClip Ding
    {
        get
        {
            if (ding != null) return ding;
            const float seconds = 0.9f, f = 1318.5f;           // E6
            var data = new float[(int)(Rate * seconds)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float attack = Mathf.Clamp01(t / 0.004f);       // a tiny fade-in so it doesn't click
                float s = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 4.5f)
                        + 0.45f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t * 7f)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 11f)
                        + 0.12f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 18f);
                data[i] = s * attack * 0.42f;
            }
            ding = MakeClip("FishingDing", data);
            return ding;
        }
    }

    /// <summary>A short drop in pitch with a bit of fizz: the sound of something small landing in water.</summary>
    public static AudioClip Plop
    {
        get
        {
            if (plop != null) return plop;
            const float seconds = 0.22f;
            var data = new float[(int)(Rate * seconds)];
            var rng = new System.Random(3);
            float phase = 0f, noise = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float freq = Mathf.Lerp(720f, 170f, Mathf.Clamp01(t / 0.09f));
                phase += 2f * Mathf.PI * freq / Rate;
                noise = noise * 0.8f + ((float)rng.NextDouble() * 2f - 1f) * 0.2f;      // softened noise
                float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 26f);
                data[i] = (Mathf.Sin(phase) * 0.8f + noise * 0.5f * Mathf.Exp(-t * 40f)) * env * 0.6f;
            }
            plop = MakeClip("FishingPlop", data);
            return plop;
        }
    }

    /// <summary>A burst of softened noise that dies away: water splashing.</summary>
    public static AudioClip Splash
    {
        get
        {
            if (splash != null) return splash;
            const float seconds = 0.45f;
            var data = new float[(int)(Rate * seconds)];
            var rng = new System.Random(11);
            float low = 0f, prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * 0.35f;                          // takes the harsh hiss off
                float band = low - prev * 0.6f;                        // and a little of the rumble
                prev = low;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 8f) * (0.75f + 0.25f * Mathf.Sin(t * 90f));
                data[i] = band * env * 0.9f;
            }
            splash = MakeClip("FishingSplash", data);
            return splash;
        }
    }

    private static AudioClip MakeClip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
