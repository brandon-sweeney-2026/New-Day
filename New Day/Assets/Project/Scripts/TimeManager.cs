using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The game clock and calendar: time of day, day of the week, season and year.
///
///  - Time advances while the game runs (set how fast below).
///  - At passOutHour the player collapses and a new day starts. Sleeping (see Bed) starts a new day too.
///  - A new day calls FarmManager.AdvanceDay(), which grows crops and dries the soil.
///
/// Put this on an empty GameObject called "TimeManager".
/// </summary>
public class TimeManager : MonoBehaviour
{
    public enum Season { Spring, Summer, Autumn, Winter }

    public static TimeManager Instance { get; private set; }

    public static readonly string[] WeekDayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    [Header("Clock")]
    [Tooltip("Real seconds that pass for each in-game minute. 0.7 = about 42 seconds per in-game hour.")]
    [SerializeField] private float realSecondsPerGameMinute = 0.7f;
    [Tooltip("Hour the day starts at (6 = 6:00 AM).")]
    [SerializeField] private int dayStartHour = 6;
    [Tooltip("Hour the player passes out. 26 = 2:00 AM the next morning.")]
    [SerializeField] private int passOutHour = 26;
    [SerializeField] private bool clockRunning = true;

    [Header("Calendar")]
    [SerializeField] private int daysPerSeason = 28;
    [SerializeField] private Season startSeason = Season.Spring;
    [SerializeField] private int startDay = 1;
    [SerializeField] private int startYear = 1;
    [Tooltip("0 = Monday ... 6 = Sunday")]
    [Range(0, 6)] [SerializeField] private int startWeekDay = 0;

    [Header("Links")]
    [SerializeField] private FarmManager farm;

    [Header("Debug")]
    [Tooltip("N = skip to the next day.  T = add one hour.  Turn off for release.")]
    [SerializeField] private bool debugKeys = true;

    // ---- state ----
    private float minutes;          // minutes since midnight of the current day; can pass 1440 after midnight
    private float timer;
    private int pauseRequests;

    public int Day { get; private set; }
    public int Year { get; private set; }
    public int WeekDay { get; private set; }
    public int TotalDays { get; private set; }
    public Season CurrentSeason { get; private set; }
    public bool LastDayWasPassOut { get; private set; }

    // ---- events ----
    public event System.Action OnMinuteChanged;
    public event System.Action OnNewDay;
    public event System.Action OnSeasonChanged;

    // ---- read-only helpers ----
    public int Hour24 { get { return Mathf.FloorToInt(minutes / 60f); } }
    public int HourOfDay { get { return Hour24 % 24; } }
    public int Minute { get { return Mathf.FloorToInt(minutes % 60f); } }
    /// <summary>Hours since midnight as a decimal, 0 to about 26.</summary>
    public float HoursDecimal { get { return minutes / 60f; } }
    public bool IsNight { get { return HourOfDay >= 20 || HourOfDay < 6; } }
    public bool Paused { get { return pauseRequests > 0; } }
    public string WeekDayName { get { return WeekDayNames[WeekDay % 7]; } }
    public string SeasonName { get { return CurrentSeason.ToString(); } }

    private void Awake()
    {
        Instance = this;
        CurrentSeason = startSeason;
        Day = Mathf.Max(1, startDay);
        Year = Mathf.Max(1, startYear);
        WeekDay = startWeekDay;
        TotalDays = 1;
        minutes = dayStartHour * 60f;

        if (farm == null) farm = FindFirstObjectByType<FarmManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (debugKeys && Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame) StartNewDay(false);
            if (Keyboard.current.tKey.wasPressedThisFrame) AddMinutes(60);
        }

        if (!clockRunning || Paused) return;

        timer += Time.deltaTime;
        while (timer >= realSecondsPerGameMinute)
        {
            timer -= realSecondsPerGameMinute;
            AddMinutes(1);
        }
    }

    // ------------------------------------------------------------------
    // Pausing (dialogue, menus...). Every Pause() needs a matching Resume().
    // ------------------------------------------------------------------

    public void Pause() { pauseRequests++; }
    public void Resume() { pauseRequests = Mathf.Max(0, pauseRequests - 1); }

    // ------------------------------------------------------------------
    // Time and days
    // ------------------------------------------------------------------

    public void AddMinutes(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            minutes += 1f;
            if (OnMinuteChanged != null) OnMinuteChanged();

            if (minutes >= passOutHour * 60f)
            {
                PassOut();
                return;
            }
        }
    }

    /// <summary>Go to sleep: a new day begins at the start hour.</summary>
    public void Sleep()
    {
        StartNewDay(false);
    }

    public void PassOut()
    {
        GameEvents.ShowToast("You passed out...");
        StartNewDay(true);
    }

    public void StartNewDay(bool passedOut)
    {
        LastDayWasPassOut = passedOut;
        TotalDays++;
        WeekDay = (WeekDay + 1) % 7;
        Day++;

        bool seasonChanged = false;
        if (Day > daysPerSeason)
        {
            Day = 1;
            CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
            if (CurrentSeason == Season.Spring) Year++;
            seasonChanged = true;
        }

        minutes = dayStartHour * 60f;
        timer = 0f;

        // Crops grow and soil dries first, then everything else reacts to the new day.
        if (farm != null) farm.AdvanceDay();

        if (seasonChanged && OnSeasonChanged != null) OnSeasonChanged();
        if (OnNewDay != null) OnNewDay();
        if (OnMinuteChanged != null) OnMinuteChanged();
    }

    // ------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------

    /// <summary>"6:40 AM"</summary>
    public string GetClockString()
    {
        int h = HourOfDay;
        string suffix = h >= 12 ? "PM" : "AM";
        int h12 = h % 12;
        if (h12 == 0) h12 = 12;
        return h12 + ":" + Minute.ToString("00") + " " + suffix;
    }

    /// <summary>"Mon, Spring 3"</summary>
    public string GetDateString()
    {
        return WeekDayName + ", " + SeasonName + " " + Day;
    }
}
