using System;
using UnityEngine;

public enum Weather { Clear, Cloudy, Rain, Storm, Snow, Fog }

/// <summary>For conditions: tick any number of weathers. Matches the Weather enum above in the same order.</summary>
[Flags]
public enum WeatherMask { Clear = 1, Cloudy = 2, Rain = 4, Storm = 8, Snow = 16, Fog = 32 }

/// <summary>
/// Today's weather. Picked once each morning from the chances below for the current season (the same day always gives the same weather, so nothing needs saving).
/// NPC schedules read it ("when it rains, Dad stays in the store"). Other systems can read WeatherManager.Current or listen to OnWeatherChanged.
/// This script only DECIDES the weather - it doesn't draw rain yet. It adds itself when the game starts; add it to a scene to change the numbers.
/// Debug: set "Force Weather" in the Inspector to try a schedule.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    [Serializable]
    public class SeasonChances
    {
        public TimeManager.Season season;
        [Tooltip("Chance (any positive numbers) of each weather: Clear, Cloudy, Rain, Storm, Snow, Fog")]
        public float[] chances = { 5f, 2f, 2f, 0f, 0f, 1f };
    }

    public static WeatherManager Instance { get; private set; }
    public static Weather Current { get { return Instance != null ? Instance.today : Weather.Clear; } }
    public static WeatherMask CurrentMask { get { return (WeatherMask)(1 << (int)Current); } }
    public static event Action<Weather> OnWeatherChanged;

    [Tooltip("Tick to always use the 'Forced' weather (testing).")] public bool forceWeather = false;
    public Weather forced = Weather.Rain;
    [Tooltip("Shows 'Today's weather: Rain' each morning.")] public bool announceEachMorning = false;
    public SeasonChances[] seasons =
    {
        new SeasonChances { season = TimeManager.Season.Spring, chances = new[] { 4f, 2f, 4f, 1f, 0f, 1f } },
        new SeasonChances { season = TimeManager.Season.Summer, chances = new[] { 7f, 2f, 1f, 2f, 0f, 0f } },
        new SeasonChances { season = TimeManager.Season.Autumn, chances = new[] { 3f, 4f, 3f, 0f, 0f, 3f } },
        new SeasonChances { season = TimeManager.Season.Winter, chances = new[] { 3f, 3f, 0f, 0f, 5f, 1f } },
    };

    private Weather today = Weather.Clear;
    private int lastDay = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<WeatherManager>() != null) return;
        new GameObject("WeatherManager").AddComponent<WeatherManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        var tm = TimeManager.Instance;
        if (tm == null) return;
        if (forceWeather && today != forced) { Set(forced); return; }
        if (tm.TotalDays != lastDay) { lastDay = tm.TotalDays; if (!forceWeather) Roll(tm); }
    }

    private void Roll(TimeManager tm)
    {
        float[] ch = null;
        foreach (SeasonChances s in seasons) if (s != null && s.season == tm.CurrentSeason) { ch = s.chances; break; }
        Weather w = Weather.Clear;
        if (ch != null)
        {
            float total = 0f;
            for (int i = 0; i < ch.Length && i < 6; i++) total += Mathf.Max(0f, ch[i]);
            if (total > 0f)
            {
                var rng = new System.Random(tm.TotalDays * 7919 + tm.Year * 31 + (int)tm.CurrentSeason * 101);
                float roll = (float)rng.NextDouble() * total;
                for (int i = 0; i < ch.Length && i < 6; i++)
                {
                    roll -= Mathf.Max(0f, ch[i]);
                    if (roll <= 0f) { w = (Weather)i; break; }
                }
            }
        }
        Set(w);
        if (announceEachMorning) GameEvents.ShowToast("Today's weather: " + w);
    }

    private void Set(Weather w)
    {
        if (today == w) return;
        today = w;
        if (OnWeatherChanged != null) OnWeatherChanged(w);
    }
}
