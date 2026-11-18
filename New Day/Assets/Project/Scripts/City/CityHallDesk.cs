using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The clerk's counter at City Hall. Two lists:
///   - City improvements: pay gold and the city changes (street trees, flower beds, a boom across the river...). Buying one sets the
///     flag city_imp_<id>; objects in the scene with a CityImprovement component appear when their flag is set.
///   - Notice board: small jobs for gold (bring items, or reach a number of something). They show up in the Quests tab of the player menu.
/// Both lists are filled in the Inspector (the city builder writes the starting ones). Needs a Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CityHallDesk : MonoBehaviour, IInteractable
{
    /// <summary>Something the city can build if the player pays for it.</summary>
    [Serializable]
    public class Improvement
    {
        [Tooltip("Short unique name with no spaces. Remembered as the flag city_imp_<id>.")]
        public string id = "trees";
        public string title = "Street trees";
        [TextArea] public string description = "";
        [Min(0)] public int goldCost = 500;
        [Tooltip("The id of an improvement that must be bought first. Empty = none.")]
        public string requires = "";
        [Tooltip("Tick for improvements that clean the river. Each one counts towards the counter 'river_cleanups'.")]
        public bool cleansRiver = false;
    }

    /// <summary>A job on the notice board.</summary>
    [Serializable]
    public class Job
    {
        [Tooltip("Short unique name with no spaces. Progress is kept in the counter cityq_<id> (0 = not taken, 1 = taken, 2 = finished).")]
        public string id = "job";
        public string title = "New job";
        [TextArea] public string description = "";
        [Header("Either: bring items")]
        public ItemData item;
        [Min(1)] public int count = 5;
        [Header("Or: reach a number (leave Item empty)")]
        [Tooltip("A GameState counter, for example trash_treated, crops_sold, research_done.")]
        public string counterKey = "";
        [Tooltip("Only count what happens AFTER the job is taken.")]
        public bool countFromAccept = true;
        [Header("Reward")]
        [Min(0)] public int rewardGold = 200;
        [Tooltip("Optional: a flag set when the job is finished (other things can react to it).")]
        public string rewardFlag = "";
    }

    [SerializeField] private string title = "City Hall";
    [SerializeField] private string prompt = "City services";
    public List<Improvement> improvements = new List<Improvement>();
    public List<Job> jobs = new List<Job>();

    private PlayerActions player;

    public string InteractPrompt { get { return prompt; } }

    public static string ImprovementFlag(string id) { return "city_imp_" + id; }
    private static string JobKey(Job j) { return "cityq_" + j.id; }

    private void Start()
    {
        // Jobs taken earlier (in this play session) go back into the quest journal's progress display after a scene change.
        foreach (Job j in jobs) if (j != null && GameState.GetCounter(JobKey(j)) == 1) AddToJournal(j);
    }

    public void Interact(PlayerActions p)
    {
        player = p;
        MenuUI.Open(title, BuildTop, "Up/Down: choose     Space: open     Esc: close");
    }

    private List<MenuRow> BuildTop()
    {
        return new List<MenuRow>
        {
            new MenuRow { label = "City improvements", detail = "Pay for something and the city builds it. Some of them help the river.",
                          onSelect = () => MenuUI.Open(title + " - improvements", BuildImprovementRows, "Up/Down: choose     Space: pay for it     Esc: back") },
            new MenuRow { label = "Notice board", detail = "Jobs the city needs doing. Take one, do it, come back for the reward.",
                          onSelect = () => MenuUI.Open(title + " - notice board", BuildJobRows, "Up/Down: choose     Space: take / hand in     Esc: back") },
            new MenuRow { label = "Close", onSelect = MenuUI.CloseAll }
        };
    }

    // ------------------------------------------------------------------ improvements
    private List<MenuRow> BuildImprovementRows()
    {
        var rows = new List<MenuRow>();
        foreach (Improvement improvement in improvements)
        {
            if (improvement == null) continue;
            Improvement imp = improvement;
            bool done = GameState.HasFlag(ImprovementFlag(imp.id));
            bool locked = !done && !string.IsNullOrEmpty(imp.requires) && !GameState.HasFlag(ImprovementFlag(imp.requires));
            rows.Add(new MenuRow
            {
                label = imp.title,
                right = done ? "Built" : (locked ? "Locked" : imp.goldCost + "g"),
                enabled = !done && !locked && GameState.Gold >= imp.goldCost,
                color = done ? (Color?)new Color(0.7f, 1f, 0.7f) : null,
                detail = imp.description + (locked ? "\nNeeds another improvement first." : "") + "\nGold: " + GameState.Gold + "g",
                onSelect = () => BuyImprovement(imp)
            });
        }
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing planned)", enabled = false });
        return rows;
    }

    private void BuyImprovement(Improvement imp)
    {
        if (GameState.HasFlag(ImprovementFlag(imp.id))) { GameEvents.ShowToast("The city already built that."); return; }
        if (!string.IsNullOrEmpty(imp.requires) && !GameState.HasFlag(ImprovementFlag(imp.requires))) { GameEvents.ShowToast("Something else has to be built first."); return; }
        if (!GameState.TrySpendGold(imp.goldCost)) { GameEvents.ShowToast("Not enough gold."); return; }

        GameState.SetFlag(ImprovementFlag(imp.id));
        GameState.AddCounter("city_improvements");
        if (imp.cleansRiver) GameState.AddCounter("river_cleanups");
        GameEvents.ShowToast("The city thanks you: " + imp.title + "!");
    }

    // ------------------------------------------------------------------ jobs
    private int Progress(Job j, Inventory inv)
    {
        if (j.item != null) return inv != null ? inv.CountOf(j.item) : 0;
        int v = GameState.GetCounter(j.counterKey);
        if (j.countFromAccept) v -= GameState.GetCounter("cityq_base_" + j.id);
        return Mathf.Max(0, v);
    }

    private string ProgressText(Job j)
    {
        Inventory inv = Inventory.Instance;
        return (j.item != null ? j.item.itemName : "Progress") + ": " + Mathf.Min(Progress(j, inv), j.count) + " / " + j.count;
    }

    private void AddToJournal(Job j)
    {
        QuestLog.Add("city_" + j.id, j.title, "City Hall", j.description, () => ProgressText(j));
    }

    private List<MenuRow> BuildJobRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        var rows = new List<MenuRow>();
        foreach (Job job in jobs)
        {
            if (job == null) continue;
            Job j = job;
            int state = GameState.GetCounter(JobKey(j));
            bool ready = state == 1 && Progress(j, inv) >= j.count;
            rows.Add(new MenuRow
            {
                label = j.title,
                right = state == 2 ? "Finished" : (state == 0 ? "Take it" : (ready ? "Hand in" : "Working on it")),
                enabled = state == 0 || ready,
                color = state == 2 ? (Color?)new Color(0.7f, 1f, 0.7f) : (ready ? (Color?)new Color(1f, 0.9f, 0.5f) : null),
                detail = j.description + "\n" + (state == 1 ? ProgressText(j) + "     " : "") + "Reward: " + j.rewardGold + "g",
                onSelect = () => PickJob(j, inv)
            });
        }
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(the board is empty)", enabled = false });
        return rows;
    }

    private void PickJob(Job j, Inventory inv)
    {
        int state = GameState.GetCounter(JobKey(j));
        if (state == 2) { GameEvents.ShowToast("That job is finished."); return; }
        if (state == 0)
        {
            GameState.SetCounter(JobKey(j), 1);
            if (j.item == null && j.countFromAccept) GameState.SetCounter("cityq_base_" + j.id, GameState.GetCounter(j.counterKey));
            AddToJournal(j);
            GameEvents.ShowToast("Job taken: " + j.title);
            return;
        }
        if (Progress(j, inv) < j.count) { GameEvents.ShowToast("Not finished yet. " + ProgressText(j)); return; }

        if (j.item != null && inv != null) inv.Remove(j.item, j.count);
        GameState.SetCounter(JobKey(j), 2);
        GameState.AddGold(j.rewardGold);
        GameState.AddCounter("city_jobs_done");
        if (!string.IsNullOrEmpty(j.rewardFlag)) GameState.SetFlag(j.rewardFlag);
        QuestLog.Complete("city_" + j.id);
        GameEvents.ShowToast("Job done! +" + j.rewardGold + "g");
    }
}
