using System;
using UnityEngine;

/// <summary>One stop on a route: go to a place, wait, move on.</summary>
[Serializable]
public class ScheduleStop
{
    [Tooltip("The id of an NpcSpot (e.g. StoreCheckout, AptKitchen) or of a PathNode in the scene.")]
    public string place = "";
    [Tooltip("Name of the scene this place is in. Leave empty for the scene the NPC lives in. A different scene = the NPC walks out of this one and is away.")]
    public string scene = "";
    [Tooltip("How long they stay here, in GAME minutes (60 = an hour).")]
    public float waitMinutes = 10f;
    [Tooltip("If this stop is in another scene: game minutes the trip takes (counted before they arrive there).")]
    public float transitMinutes = 10f;
    [Tooltip("Direction they face while waiting. (0,0) = the spot's own direction.")]
    public Vector2 facing = Vector2.zero;
    [Tooltip("What they are doing here. The Inspector label for you, and optionally shown in dialogue.")]
    public string activity = "";
}

public enum RouteEnd { ReturnToIdleSpot, StayAtLastStop }

/// <summary>
/// One daily route. Every morning the NPC looks at all its routes, keeps the ones whose Conditions are true, and (highest Priority first)
/// accepts each route that doesn't overlap one already accepted. Then they simply follow the clock.
/// </summary>
[Serializable]
public class ScheduleRoute
{
    public string routeName = "New route";
    public bool enabled = true;
    [Tooltip("Higher wins when two routes overlap in time.")] public int priority = 0;
    [Tooltip("The hour they set off (24h clock: 13.5 = 1:30 PM).")]
    [Range(0f, 26f)] public float startHour = 8f;
    [Tooltip("Where they are when the route starts. Leave empty = their idle spot (or wherever the last route ended).")]
    public string startPlace = "";
    public string startScene = "";
    public NpcCondition conditions = new NpcCondition();
    public ScheduleStop[] stops = new ScheduleStop[0];
    public RouteEnd whenFinished = RouteEnd.ReturnToIdleSpot;
}

/// <summary>
/// An NPC's whole schedule, shared by every scene they can appear in: right-click in Project > Create > Farm > NPC Schedule.
/// Up to 30 routes. Where they stand when no route is running is the Idle Spot.
/// </summary>
[CreateAssetMenu(fileName = "NpcSchedule", menuName = "Farm/NPC Schedule")]
public class NpcScheduleData : ScriptableObject
{
    public const int MaxRoutes = 30;

    [Tooltip("Unique short id, e.g. dad. Used to remember today's plan.")]
    public string npcId = "npc";
    [Tooltip("Where they stand when no route is running (an NpcSpot id).")]
    public string idleSpot = "";
    [Tooltip("Scene the idle spot is in. Empty = the scene the NPC lives in.")]
    public string idleScene = "";
    [Tooltip("Up to 30 routes.")]
    public ScheduleRoute[] routes = new ScheduleRoute[0];

    private void OnValidate()
    {
        if (routes != null && routes.Length > MaxRoutes) Array.Resize(ref routes, MaxRoutes);
    }
}
