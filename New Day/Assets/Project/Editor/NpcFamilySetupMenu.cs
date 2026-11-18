using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Kim Family
///
/// The Kims run the General Store and live in the apartment above it: Jae-hoon (dad), Mi-sook (mom) and their two grown-up
/// children Ji-ho (son) and Soo-ah (daughter), who help out from time to time.
/// This menu imports the family art, makes four NpcSchedule assets (in Assets/Farm/Suburb/Family/Schedules) with starter routes,
/// and places the four people in the open scene at their beds. Every route and chat line is plain data - edit it in the Inspector.
///
/// Run Farm > Set Up > General Store and Farm > Set Up > Store Paths first. (Farm > Set Up > Suburbs Scene runs all of them for you.)
/// The park, plaza and city stops in the schedules use places that only the generated Suburbs scene has; in any other scene those
/// stops are skipped with a warning in the Console.
///
/// Schedule assets that already exist are KEPT, so your own edits survive a rebuild. Delete an asset to get the starter routes back.
/// Must live in a folder named "Editor".
/// </summary>
public static class NpcFamilySetupMenu
{
    private const string Root = "Assets/Farm/Suburb/Family";

    private class Person
    {
        public string id, name, art, portraits, idle; public Color color; public Vector2 offset;
    }

    // 'offset' keeps family members apart when they share a spot (the parents' bed, the dinner table, the till).
    private static readonly Person[] People =
    {
        new Person { id="dad",      name="Jae-hoon", art="npc_dad",      portraits="portraits_dad",      idle="AptBed1", color=new Color(0.25f,0.35f,0.55f), offset=new Vector2(0f, 0f) },
        new Person { id="mom",      name="Mi-sook",  art="npc_mom",      portraits="portraits_mom",      idle="AptBed1", color=new Color(0.65f,0.3f,0.4f),   offset=new Vector2(0.9f, 0f) },
        new Person { id="son",      name="Ji-ho",    art="npc_son",      portraits="portraits_son",      idle="AptBed2", color=new Color(0.3f,0.5f,0.35f),   offset=new Vector2(-0.8f, -0.2f) },
        new Person { id="daughter", name="Soo-ah",   art="npc_daughter", portraits="portraits_daughter", idle="AptBed3", color=new Color(0.55f,0.4f,0.6f),   offset=new Vector2(0.3f, -0.5f) },
    };

    [MenuItem("Farm/Set Up/Kim Family", false, 148)]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog(result.StartsWith("Placed") ? "Kim family placed" : "Kim family", result, "OK");
    }

    /// <summary>Places the family in the open scene. Returns a sentence saying what happened (it starts with "Placed" when it worked). No pop-ups.</summary>
    public static string Build()
    {
        if (Object.FindObjectsByType<NpcSpot>().Length == 0)
            return "No NpcSpots found. Run Farm > Set Up > General Store first (in the same scene).";
        if (GameObject.Find("Npc_Jae-hoon") != null)
            return "The family is already in this scene. Delete the Npc_* objects to place them again (your schedule assets are kept).";
        string art = HardwareEditorUtil.FindFolder("npc_dad");
        if (art == null) return "The family art (npc_dad.png and friends) was not found. It belongs in Assets/Project/Art/Family.";

        HardwareEditorUtil.EnsureFolder(Root + "/Schedules");
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { art }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.StartsWith("npc_")) HardwareEditorUtil.SliceSheet(p, 16, 28, SpriteAlignment.BottomCenter);          // (stable sprite IDs: see StableSpriteId)
            else if (n.StartsWith("portraits_")) HardwareEditorUtil.SliceSheet(p, 64, 64, SpriteAlignment.Center);
            else HardwareEditorUtil.ImportSingle(p, SpriteAlignment.Center);
        }
        AssetDatabase.Refresh();

        GameObject parent = GameObject.Find("Kim Family");
        if (parent == null) { parent = new GameObject("Kim Family"); Undo.RegisterCreatedObjectUndo(parent, "Kim family"); }

        ShopCounter checkout = FindCheckout();
        foreach (Person p in People)
        {
            NpcScheduleData sched = MakeSchedule(p);
            var go = new GameObject("Npc_" + p.name);
            Undo.RegisterCreatedObjectUndo(go, "Add NPC");
            go.transform.SetParent(parent.transform, false);
            NpcSpot spot = HardwareEditorUtil.FindSpot(p.idle);
            go.transform.position = spot != null ? spot.transform.position + (Vector3)p.offset : Vector3.zero;
            go.AddComponent<SpriteRenderer>().sortingOrder = 0;
            var npc = go.AddComponent<ScheduledNpc>();
            npc.displayName = p.name;
            npc.schedule = sched;
            npc.standOffset = p.offset;
            HardwareEditorUtil.GiveNpcArt(npc, art, p.art, p.portraits);
            npc.nameColor = p.color;
            npc.firstMeeting = FirstMeeting(p.id);
            npc.chatLines = ChatLines(p.id);
            if (checkout != null) npc.shop = checkout;          // whoever is standing at the till can sell to you
            EditorUtility.SetDirty(npc);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = parent;
        AssetDatabase.SaveAssets();
        return "Placed Jae-hoon, Mi-sook, Ji-ho and Soo-ah in the scene, each with a schedule in " + Root + "/Schedules.\n\n" +
               "Edit a schedule asset to change routes (up to 30 each). Select an NPC and use the component's right-click menu 'Print Today's Plan' while playing to see what they will do.\n" +
               "The city trips use a scene named 'City': the family walks off the right edge of the suburb and is away for a while. Farm > Set Up > City Scene points those stops at the right places in the city.";
    }

    // ------------------------------------------------------------------ schedules
    // How to read a route:  Route(name, start hour, priority, when, stops...)   and   Stop(place, minutes to wait, what they are doing).
    // 'Stay' routes end where their last stop is, so the next route starts from there (no walk home in between).
    // Two routes that overlap in time can't both run: the higher priority one wins and the other is dropped for the day,
    // so the times below leave room for the walk (the store is about 30 game minutes from the bedrooms, the park about 85).
    private static NpcScheduleData MakeSchedule(Person p)
    {
        string path = Root + "/Schedules/Schedule_" + p.id + ".asset";
        var s = AssetDatabase.LoadAssetAtPath<NpcScheduleData>(path);
        if (s != null) return s;
        s = ScriptableObject.CreateInstance<NpcScheduleData>();
        s.npcId = p.id; s.idleSpot = p.idle;
        var r = new List<ScheduleRoute>();
        NpcCondition wet = Weather(WeatherMask.Rain | WeatherMask.Storm);
        NpcCondition dry = Weather(WeatherMask.Clear | WeatherMask.Cloudy | WeatherMask.Fog);
        switch (p.id)
        {
            case "dad":     // runs the till; Sundays he buys stock in the city
                r.Add(Stay("Breakfast, then open the store", 6.5f, 10, null, Stop("AptKitchen", 25, "Having breakfast"), Stop("StoreCheckout", 270, "Running the till")));
                r.Add(Stay("Lunch upstairs", 12.25f, 10, null, Stop("AptDining", 40, "Having lunch")));
                r.Add(Stay("Afternoon till", 13.5f, 10, null, Stop("StoreCheckout", 300, "Running the till")));
                r.Add(Stay("Rainy day: stock check", 13.5f, 30, wet, Stop("StoreAisleBack", 40, "Checking stock"), Stop("StoreSlush", 15, "Fixing the slush machine"), Stop("StoreCheckout", 220, "Running the till")));
                r.Add(Route("Close up", 19.25f, 10, null, Stop("StoreDoorInside", 5, "Locking up"), Stop("AptLiving", 120, "Watching TV")));
                r.Add(Route("Sunday in the city", 8.5f, 50, Days(DayMask.Sun), Stop("MarketInside", 240, "Buying stock in the city", "City", 45)));
                break;
            case "mom":     // works the shop floor, covers the till over lunch, cooks; Saturdays she visits friends in the city
                r.Add(Stay("Morning prep", 6.25f, 10, null, Stop("AptKitchen", 35, "Making breakfast"), Stop("StoreAisleMid", 60, "Stocking shelves"), Stop("StoreSeeds", 30, "Tidying the seed rack"),
                    Stop("StoreSlush", 20, "Cleaning the slush machines"), Stop("StoreAisleBack", 120, "Checking the coolers")));
                r.Add(Stay("Cover the till over lunch", 12f, 10, null, Stop("StoreCheckout", 110, "Running the till")));
                r.Add(Stay("Late lunch upstairs", 14.5f, 10, null, Stop("AptKitchen", 30, "Cooking lunch"), Stop("AptDining", 30, "Having lunch")));
                r.Add(Stay("Afternoon in the plaza", 16.25f, 10, dry, Stop("PlazaBench1", 50, "Catching up with the neighbours"), Stop("StoreFront", 10, "Watering the flower pots")));
                r.Add(Stay("Rainy afternoon: baking", 16.25f, 30, Weather(WeatherMask.Rain | WeatherMask.Storm | WeatherMask.Snow), Stop("AptKitchen", 100, "Baking")));
                r.Add(Route("Evening", 18.5f, 10, null, Stop("AptKitchen", 60, "Cooking dinner"), Stop("AptDining", 50, "Having dinner"), Stop("AptLiving", 60, "Relaxing")));
                r.Add(Route("Saturday in the city", 8.5f, 50, Days(DayMask.Sat), Stop("CafeInside", 210, "Visiting friends in the city", "City", 45)));
                break;
            case "son":     // helps Tuesdays and Fridays; plays football in the park on dry afternoons
                r.Add(Stay("Sleep in, then breakfast", 9f, 5, null, Stop("AptKitchen", 30, "Having breakfast"), Stop("AptLiving", 150, "Gaming")));
                r.Add(Stay("Helping at the store", 9f, 40, Days(DayMask.Tue | DayMask.Fri), Stop("StoreAisleMid", 90, "Stocking shelves"), Stop("StoreSlush", 30, "Refilling the slush machine"), Stop("StoreAisleBack", 60, "Restocking the coolers")));
                r.Add(Stay("Afternoon kickabout", 15f, 20, dry, Stop("FieldLeft", 90, "Kicking a ball around"), Stop("ParkBenchA", 20, "Catching his breath")));
                r.Add(Route("Evening: dinner and games", 19.5f, 10, null, Stop("AptDining", 40, "Having dinner"), Stop("AptLiving", 120, "Gaming")));
                break;
            case "daughter":    // studies at the university in the city Mon/Tue/Thu; helps Wednesdays and Saturdays
                r.Add(Stay("Morning routine", 7.5f, 5, null, Stop("AptBath2", 20, "Getting ready"), Stop("AptKitchen", 25, "Having breakfast"), Stop("AptDining", 90, "Studying")));
                r.Add(Route("University in the city", 7.75f, 40, Days(DayMask.Mon | DayMask.Tue | DayMask.Thu), Stop("UniStudy", 360, "At lectures in the city", "City", 40)));
                r.Add(Stay("Helping at the store", 9.5f, 40, Days(DayMask.Wed | DayMask.Sat), Stop("StoreSeeds", 45, "Sorting seeds"), Stop("StoreAisleMid", 30, "Stocking shelves")));
                r.Add(Stay("Saturday: cover the till", 11.9f, 45, Days(DayMask.Sat), Stop("StoreCheckout", 105, "Running the till")));
                r.Add(Stay("Afternoon in her room", 14.5f, 5, null, Stop("AptBed3", 120, "Listening to music")));
                r.Add(Stay("Reading in the plaza", 15f, 20, new NpcCondition { days = DayMask.Fri | DayMask.Sun, weather = WeatherMask.Clear | WeatherMask.Cloudy }, Stop("PlazaBench2", 75, "Reading on the bench")));
                r.Add(Route("Evening", 19f, 10, null, Stop("AptDining", 60, "Having dinner"), Stop("AptLiving", 90, "Watching TV")));
                break;
        }
        s.routes = r.ToArray();
        AssetDatabase.CreateAsset(s, path);
        return s;
    }

    private static ScheduleStop Stop(string place, float wait, string act, string scene = "", float transit = 10f)
    {
        return new ScheduleStop { place = place, waitMinutes = wait, activity = act, scene = scene, transitMinutes = transit };
    }

    /// <summary>A route that walks home (to the idle spot) when it is finished.</summary>
    private static ScheduleRoute Route(string name, float hour, int prio, NpcCondition cond, params ScheduleStop[] stops)
    {
        return new ScheduleRoute { routeName = name, startHour = hour, priority = prio, conditions = cond ?? new NpcCondition(), stops = stops, whenFinished = RouteEnd.ReturnToIdleSpot };
    }

    /// <summary>A route that ends at its last stop: they stay there until the next route starts.</summary>
    private static ScheduleRoute Stay(string name, float hour, int prio, NpcCondition cond, params ScheduleStop[] stops)
    {
        ScheduleRoute r = Route(name, hour, prio, cond, stops);
        r.whenFinished = RouteEnd.StayAtLastStop;
        return r;
    }

    private static NpcCondition Days(DayMask d) { return new NpcCondition { days = d }; }
    private static NpcCondition Weather(WeatherMask w) { return new NpcCondition { weather = w }; }

    // ------------------------------------------------------------------ dialogue
    private static string[] FirstMeeting(string id)
    {
        switch (id)
        {
            case "dad": return new[] { "Welcome, welcome! I'm Jae-hoon. My wife and I run this shop.", "Seeds, snacks, slush - if we don't have it, tell me and I'll order it." };
            case "mom": return new[] { "Oh, you must be the new farmer! I'm Mi-sook.", "You look hungry. Have you eaten today?" };
            case "son": return new[] { "Hey. I'm Ji-ho. I help out here sometimes... when my parents make me.", "Don't tell them I said that." };
            default: return new[] { "Hi! I'm Soo-ah. I'm studying in the city, but I help at the shop on my days off.", "The seed rack is my project. Take a look!" };
        }
    }

    private static NpcChatLine[] ChatLines(string id)
    {
        var l = new List<NpcChatLine>();
        NpcCondition wet = Weather(WeatherMask.Rain | WeatherMask.Storm);
        switch (id)
        {
            case "dad":     // proud shopkeeper, worries about the big tower in the city
                l.Add(Chat("Default", 0, null, "Good to see you again! Fresh stock came in this week."));
                l.Add(Chat("Default 2", 0, null, "Thirty years behind this till. I know what every house on the street eats for breakfast."));
                l.Add(Chat("Default 3", 0, null, "That tower in the city sells everything cheaper. But they don't know your name, do they?"));
                l.Add(Chat("Rain", 10, wet, "Terrible weather, isn't it? Good for the crops, at least."));
                l.Add(Chat("Sunday", 12, Days(DayMask.Sun), "Sunday is my wholesale day. The city is loud, but the prices are good."));
                break;
            case "mom":     // feeds everyone, knows all the news
                l.Add(Chat("Default", 0, null, "Have you eaten? Take a rice ball. No, no, take it."));
                l.Add(Chat("Default 2", 0, null, "Rosa from down the street swapped me her bread recipe for my kimchi. I think I won."));
                l.Add(Chat("Default 3", 0, null, "My children say I work too much. Then who would stock the shelves, hm?"));
                l.Add(Chat("Rain", 10, wet, "Rainy days are baking days. Come by later, it will smell wonderful upstairs."));
                l.Add(Chat("Saturday", 12, Days(DayMask.Sat), "Saturday! I am off to see my friends in the city. Soo-ah has the till."));
                break;
            case "son":     // easy-going, would rather be playing football or games
                l.Add(Chat("Default", 0, null, "Oh, hey. If Mom asks, I already restocked the coolers."));
                l.Add(Chat("Default 2", 0, null, "Dev and I are getting a five-a-side going in the park. You any good in goal?"));
                l.Add(Chat("Default 3", 0, null, "I'll take over the shop one day. Probably. It needs a games corner first."));
                l.Add(Chat("Rain", 10, wet, "Rain means no football. So, gaming. Tragic."));
                l.Add(Chat("Work days", 12, Days(DayMask.Tue | DayMask.Fri), "Tuesdays and Fridays I'm on shelf duty. Ask me where anything is. Anything."));
                break;
            default:        // studious, organised, a bit of a plant nerd
                l.Add(Chat("Default", 0, null, "I sorted the seed rack by season AND by colour. Nobody noticed. You noticed, right?"));
                l.Add(Chat("Default 2", 0, null, "My course is in the city, three days a week. The walk to the edge of town is the nice part."));
                l.Add(Chat("Default 3", 0, null, "If a seed packet looks faded, tell me. I'll swap it for you."));
                l.Add(Chat("Rain", 10, wet, "Rain is good news for seedlings. Bad news for my notes."));
                l.Add(Chat("Uni days", 12, Days(DayMask.Mon | DayMask.Tue | DayMask.Thu), "Lecture day. I should already be walking, really."));
                break;
        }
        // lines every Kim can say
        l.Add(Chat("Snow", 10, Weather(WeatherMask.Snow), "Snow already! Bundle up out there."));
        l.Add(Chat("Winter", 8, new NpcCondition { seasons = SeasonMask.Winter }, "Winter is slow for the shop. Warm slush sells surprisingly well, though."));
        l.Add(Chat("Forest grove", 20, new NpcCondition { requireFlags = new[] { "forest_grove_reached" } }, "I heard you found something strange in the forest. Be careful in there."));
        return l.ToArray();
    }

    private static NpcChatLine Chat(string label, int prio, NpcCondition c, params string[] pages)
    {
        return new NpcChatLine { label = label, priority = prio, conditions = c ?? new NpcCondition(), pages = pages };
    }

    // ------------------------------------------------------------------ helpers
    private static ShopCounter FindCheckout()
    {
        foreach (ShopCounter s in Object.FindObjectsByType<ShopCounter>())
            if (s.gameObject.name.Contains("Checkout")) return s;
        return null;
    }
}
