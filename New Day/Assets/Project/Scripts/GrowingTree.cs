using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One tree in the world. Created by TreeManager (you don't add this yourself).
/// It grows a stage every few days, changes look with the season, drops fruit when mature, and can be chopped with an axe.
/// </summary>
public class GrowingTree : MonoBehaviour, IToolTarget
{
    public TreeData Data { get; private set; }
    public Vector3Int Cell { get; private set; }
    /// <summary>0 = sapling ... 3 = fully grown.</summary>
    public int Stage { get; private set; }
    public bool IsMature { get { return Stage >= 3; } }

    private int daysInStage;
    private int hitsLeft;
    private float shakeUntil;
    private Vector3 basePos;

    private SpriteRenderer sr;
    private BoxCollider2D trunk;
    private TreeManager manager;
    private readonly List<ItemPickup> fallen = new List<ItemPickup>();

    public void Init(TreeManager owner, TreeData data, Vector3Int cell, int stage, int sortingOrder)
    {
        manager = owner;
        Data = data;
        Cell = cell;
        Stage = Mathf.Clamp(stage, 0, 3);
        daysInStage = 0;
        hitsLeft = data.Hits(Stage);
        basePos = transform.position;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        var sorter = gameObject.AddComponent<BehindPlayerSorter>();   // player walks behind the tree when standing above it
        sorter.thresholdOffset = 0f;

        trunk = gameObject.AddComponent<BoxCollider2D>();
        Refresh(TimeManager.Instance != null ? (int)TimeManager.Instance.CurrentSeason : 0);
    }

    // ------------------------------------------------------------------
    // Growing
    // ------------------------------------------------------------------

    /// <summary>Called by TreeManager each new day.</summary>
    public void AdvanceDay(int season)
    {
        if (Stage < 3)
        {
            bool grows = true;
            if (Data.slowedByPollution && PollutionMap.Instance != null)
                grows = Random.value <= PollutionSettings.Current.GrowthChance(PollutionMap.Instance.GetLevel(Cell));

            if (grows)
            {
                daysInStage++;
                if (daysInStage >= Data.DaysInStage(Stage))
                {
                    Stage++;
                    daysInStage = 0;
                    hitsLeft = Data.Hits(Stage);
                }
            }
        }
        else
        {
            DropFruit(season);
        }

        Refresh(season);
    }

    /// <summary>Called when the season changes.</summary>
    public void Refresh(int season)
    {
        if (sr == null) return;
        sr.sprite = Data.GetSprite(season, Stage);

        // Saplings can be walked through; bigger trees have a solid trunk.
        trunk.enabled = Stage >= 1;
        trunk.size = Data.trunkSize * (Stage >= 3 ? 1f : 0.7f);
        trunk.offset = new Vector2(0f, trunk.size.y * 0.5f);
    }

    private void DropFruit(int season)
    {
        fallen.RemoveAll(p => p == null);
        if (Data.seedItem != null && fallen.Count < 4 && Random.value <= Data.seedDropChancePerDay)
        {
            ItemPickup s = ItemPickup.Spawn(Data.seedItem, 1, transform.position + new Vector3(0f, 0.2f, 0f), 0, 0.9f);
            if (s != null) fallen.Add(s);
        }

        if (Data.seasonDrops == null || season >= Data.seasonDrops.Length) return;
        TreeData.SeasonDrop drop = Data.seasonDrops[season];
        if (drop == null || drop.item == null) return;

        fallen.RemoveAll(p => p == null);            // picked up already
        if (fallen.Count >= 4) return;                // don't let drops pile up forever
        if (Random.value > drop.chancePerDay) return;

        int amount = Random.Range(drop.minAmount, Mathf.Max(drop.minAmount, drop.maxAmount) + 1);
        ItemPickup p = ItemPickup.Spawn(drop.item, amount, transform.position + new Vector3(0f, 0.2f, 0f), 0, 0.9f);
        if (p != null) fallen.Add(p);
    }

    // ------------------------------------------------------------------
    // Chopping
    // ------------------------------------------------------------------

    public bool OnToolUsed(ItemData.ToolAction tool, PlayerActions user)
    {
        if (tool != ItemData.ToolAction.Axe)
        {
            GameEvents.ShowToast("Chop trees with an axe.");
            return false;
        }

        hitsLeft--;
        shakeUntil = Time.time + 0.15f;

        if (hitsLeft <= 0) FellTree();
        return true;
    }

    private void FellTree()
    {
        int logs = Data.Logs(Stage);
        if (Data.logItem != null && logs > 0) ItemPickup.Spawn(Data.logItem, logs, transform.position + new Vector3(0f, 0.4f, 0f), 0, 0.7f);

        if (IsMature && Data.seedItem != null && Random.value <= Data.seedDropChanceOnChop)
            ItemPickup.Spawn(Data.seedItem, 1, transform.position + new Vector3(0f, 0.3f, 0f), 0, 0.7f);

        GameState.AddCounter("trees_chopped");
        if (manager != null) manager.Unregister(this);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (Time.time < shakeUntil)
            transform.position = basePos + new Vector3(Mathf.Sin(Time.time * 70f) * 0.04f, 0f, 0f);
        else if (transform.position != basePos)
            transform.position = basePos;
    }
}
