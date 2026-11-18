using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Set Up > Story Arcs
///
/// Makes the starter NPC storylines (NpcStoryArc assets) in Assets/Resources/Stories. Each story has ten parts that unlock
/// one heart at a time, a choice at Part 8, and two versions of Parts 9 and 10 (one per answer).
/// The full plan for every neighbour is in Design/Suburb Story Arcs.md; the stories written here so far are:
///
///   rosa    The Soup on the Step      A: the Saturday market     B: bread for the tower
///   sam     The Book and the Barrels  A: publish the expose     B: finish the novel
///   marcus  The Man in the Tower      A: blow the whistle        B: take the promotion
///   frank   The Cot in the Office     A: the co-op               B: sell to Sterling
///   dad      Jae-hoon: The First Dollar     A: the local supplier     B: the franchise
///   mom      Mi-sook: Have You Eaten?       A: the street dinner      B: the competition
///   son      Ji-ho: The Reluctant Heir      A: the business course    B: stay and change the store
///   daughter Soo-ah: The Seed Tin           A: the seed library       B: the internship
///   emeka   The Dead Letter Box        A: save the route          B: take the tower job
///   grace   What the River Remembers   A: publish the history     B: keep the files safe
///   nia     Gregory's River            A: the science fair        B: the cup final
///   walt    Delgado & Petrakis         A: build it with Gus       B: finish it alone
///   gus     Eleni's Song               A: say it to his face      B: fix his fence instead
///   kenji   The Crow and the Board     A: go home to Japan        B: tell Yuki first
///   priya   Shoes That Don't Hurt      A: stay at the hospital    B: take the clinic job
///   dev     The Best Left Foot         A: stay with the team      B: join the academy
///   bea     Forty Questions            A: publish the results     B: keep the grant
///   tariq   Maple Lane                 A: keep the song           B: sell the song
///   mateo   Eleven Kinds of Grass      A: save the park           B: take the deal
///   lucia   Shouting and Oranges       A: run for council         B: lose the paperwork
///   yuki    The Wetland Room           A: the honest exhibition   B: the pop-up museum
///   lena    Every House on the Street  A: print fifty copies      B: sign with the big publisher
///   ada     Never Tell a Rose          A: reopen the stall        B: pass it to Bea
///
/// That is all 23 suburb NPCs.
///
/// A story asset that already exists is KEPT, so your edits in the Inspector survive. Delete an asset to get the version below back.
/// Nothing in any scene is changed: NPCs find their story by npc id when you talk to them.
///
/// Also here:
///   Farm > Test and Debug > Story Arcs - ...      (while playing) add hearts, allow another part today, reset the stories
///   Farm > Fix and Diagnose > Check Story Arcs    looks for missing parts, missing items and paths that can't be reached
/// Must live in a folder named "Editor".
/// </summary>
public static class StoryArcSetupMenu
{
    private const string Root = "Assets/Resources/Stories";

    // ------------------------------------------------------------------ menu items
    [MenuItem("Farm/Set Up/Story Arcs", false, 156)]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog("Story arcs", result, "OK");
    }

    /// <summary>Makes any missing story assets. Returns a sentence (starting with "Made" when it worked). No pop-ups.</summary>
    public static string Build()
    {
        EnsureFolder(Root);
        var made = new List<string>();
        var kept = new List<string>();
        foreach (NpcStoryArc arc in Stories())
        {
            string path = Root + "/Story_" + arc.npcId + ".asset";
            if (AssetDatabase.LoadAssetAtPath<NpcStoryArc>(path) != null) { kept.Add(arc.npcId); Object.DestroyImmediate(arc); continue; }
            arc.name = "Story_" + arc.npcId;
            AssetDatabase.CreateAsset(arc, path);
            made.Add(arc.npcId);
        }
        AssetDatabase.SaveAssets();
        string text = "Made " + made.Count + " story arc(s)" + (made.Count > 0 ? " (" + string.Join(", ", made) + ")" : "") + " in " + Root + ".";
        if (kept.Count > 0) text += "\nKept the ones that already existed: " + string.Join(", ", kept) + ". Delete an asset to get its starting version back.";
        text += "\n\nTalk to someone once a day and give gifts: each new heart unlocks the next part. The People tab (Tab) shows how far each story is.";
        return text;
    }

    /// <summary>
    /// For the command line (Unity closed): makes the stories, checks them, and writes the report to the log.
    ///   Unity.exe -batchmode -nographics -quit -projectPath PROJECT -executeMethod StoryArcSetupMenu.BuildBatch -logFile LOG
    /// Lines start with STORY| (one per story) and !! (a problem). Exits with code 1 if there are problems.
    /// </summary>
    public static void BuildBatch()
    {
        Debug.Log("STORYBUILD| " + Build().Replace("\n", " "));
        int problems;
        Debug.Log(Check(out problems));
        if (Application.isBatchMode) EditorApplication.Exit(problems == 0 ? 0 : 1);
    }

    [MenuItem("Farm/Fix and Diagnose/Check Story Arcs", false, 431)]
    public static void CheckFromMenu()
    {
        int problems;
        string report = Check(out problems);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Check Story Arcs", problems == 0 ? "No problems found. The full report is in the Console." : problems + " problem(s). Look for lines marked '!!' in the Console.", "OK");
    }

    [MenuItem("Farm/Test and Debug/Story Arcs - Add 1 Heart With Everyone", false, 332)]
    private static void AddHeart()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Story arcs", "Press Play first.", "OK"); return; }
        int per = PlayerMenuSettings.Current != null ? PlayerMenuSettings.Current.pointsPerHeart : 100;
        foreach (NpcStoryArc arc in AllArcs()) Friendship.AddPoints(arc.npcId, per);
        Debug.Log("Story arcs: added one heart with everyone who has a story.");
    }

    [MenuItem("Farm/Test and Debug/Story Arcs - Allow Another Part Today", false, 333)]
    private static void AllowToday()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Story arcs", "Press Play first.", "OK"); return; }
        foreach (NpcStoryArc arc in AllArcs()) GameState.SetCounter("story_day_" + arc.npcId, 0);
        Debug.Log("Story arcs: everyone may tell another part today.");
    }

    [MenuItem("Farm/Test and Debug/Story Arcs - Reset All Stories", false, 334)]
    private static void ResetAll()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Story arcs", "Press Play first. (Story progress lives in the running game and the save file.)", "OK"); return; }
        foreach (NpcStoryArc arc in AllArcs())
        {
            string id = arc.npcId;
            GameState.SetCounter("story_" + id, 0);
            GameState.SetCounter("story_path_" + id, 0);
            GameState.SetCounter("story_day_" + id, 0);
            GameState.SetFlag("story_" + id + "_asked", false);
            GameState.SetFlag("story_" + id + "_done", false);
            GameState.SetFlag("story_" + id + "_chose_a", false);
            GameState.SetFlag("story_" + id + "_chose_b", false);
            for (int n = 1; n <= 20; n++) GameState.SetFlag("story_" + id + "_" + n, false);
        }
        Debug.Log("Story arcs: every story is back at the start (hearts are unchanged; flags set by story choices are not undone).");
    }

    // ------------------------------------------------------------------ checking
    /// <summary>Looks at every story asset and writes a report. Lines with problems start with "!!".</summary>
    public static string Check(out int problems)
    {
        problems = 0;
        var sb = new StringBuilder("STORY ARCS\n");
        var seen = new HashSet<string>();
        foreach (NpcStoryArc arc in AllArcs())
        {
            sb.AppendLine("STORY| " + arc.npcId + ": " + arc.title + " (" + (arc.parts != null ? arc.parts.Length : 0) + " entries)");
            if (string.IsNullOrEmpty(arc.npcId)) { sb.AppendLine("!! " + arc.name + " has no Npc Id."); problems++; continue; }
            if (!seen.Add(arc.npcId)) { sb.AppendLine("!! Two stories are for '" + arc.npcId + "'."); problems++; }
            if (arc.parts == null || arc.parts.Length == 0) { sb.AppendLine("!! " + arc.npcId + " has no parts."); problems++; continue; }

            int last = arc.LastNumber, choiceAt = 0, lastHearts = 0;
            for (int n = 1; n <= last; n++)
            {
                bool both = false, a = false, b = false;
                foreach (NpcStoryArc.Part p in arc.parts)
                {
                    if (p == null || p.number != n) continue;
                    if (p.path == NpcStoryArc.Path.Both) both = true;
                    if (p.path == NpcStoryArc.Path.OnlyA) a = true;
                    if (p.path == NpcStoryArc.Path.OnlyB) b = true;
                    if (p.choice.enabled)
                    {
                        if (choiceAt > 0) { sb.AppendLine("!! " + arc.npcId + " has more than one choice (Parts " + choiceAt + " and " + n + "). Only the first decides the path."); problems++; }
                        else choiceAt = n;
                        if (p.ask.Active) { sb.AppendLine("!! " + arc.npcId + " Part " + n + " both asks for items and offers a choice. Use one or the other."); problems++; }
                    }
                    if (p.ask.amount > 0 && p.ask.item == null) { sb.AppendLine("!! " + arc.npcId + " Part " + n + " asks for " + p.ask.amount + " of an item that is not set."); problems++; }
                    if (p.heartsNeeded < lastHearts) sb.AppendLine("   " + arc.npcId + " Part " + n + " needs fewer hearts than the part before it (fine, but unusual).");
                    lastHearts = Mathf.Max(lastHearts, p.heartsNeeded);
                    if ((p.path != NpcStoryArc.Path.Both) && (choiceAt == 0 || n <= choiceAt)) { sb.AppendLine("!! " + arc.npcId + " Part " + n + " is 'Only A/B' but comes before the choice, so it can never play."); problems++; }
                }
                if (!both && !(a && b))
                {
                    if (!both && !a && !b) sb.AppendLine("!! " + arc.npcId + " has no Part " + n + ", so the story stops at Part " + (n - 1) + ".");
                    else sb.AppendLine("!! " + arc.npcId + " Part " + n + " exists only for path " + (a ? "A" : "B") + ", so path " + (a ? "B" : "A") + " stops at Part " + (n - 1) + ".");
                    problems++;
                }
            }
        }
        if (seen.Count == 0) { sb.AppendLine("!! No story arcs in " + Root + ". Run Farm > Set Up > Story Arcs."); problems++; }
        sb.AppendLine(problems == 0 ? "No problems found." : problems + " problem(s).");
        return sb.ToString();
    }

    private static List<NpcStoryArc> AllArcs()
    {
        var list = new List<NpcStoryArc>();
        foreach (string guid in AssetDatabase.FindAssets("t:NpcStoryArc"))
        {
            var a = AssetDatabase.LoadAssetAtPath<NpcStoryArc>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) list.Add(a);
        }
        return list;
    }

    // ------------------------------------------------------------------ short helpers used in the stories below
    // P(number, title, pages...)  a part everyone sees, unlocked at 'number' hearts
    // A(...) / B(...)             the same, but only after choice A / choice B
    // Ask(part, item, amount, quality, quest title, quest text, reminder, thanks)   the part asks for items
    // Fork(part, answer A, reply A, flags A, answer B, reply B, flags B)            the part offers the choice
    // When(part, condition)   Flags(part, flags...)   Gift(part, gold, item, amount)
    private static NpcStoryArc.Part P(int n, string title, params string[] pages)
    {
        return new NpcStoryArc.Part { number = n, heartsNeeded = n, title = title, pages = pages, conditions = new NpcCondition() };
    }
    private static NpcStoryArc.Part A(int n, string title, params string[] pages) { NpcStoryArc.Part p = P(n, title, pages); p.path = NpcStoryArc.Path.OnlyA; return p; }
    private static NpcStoryArc.Part B(int n, string title, params string[] pages) { NpcStoryArc.Part p = P(n, title, pages); p.path = NpcStoryArc.Path.OnlyB; return p; }
    private static string[] L(params string[] lines) { return lines; }

    private static NpcStoryArc.Part Ask(NpcStoryArc.Part p, string itemAsset, int amount, int quality, string questTitle, string questText, string[] reminder, string[] thanks)
    {
        ItemData item = FindItem(itemAsset);
        if (item == null) { Debug.LogWarning("Story arcs: no item asset called '" + itemAsset + "', so '" + p.title + "' asks for nothing. Set it in the Inspector."); return p; }
        p.ask = new NpcStoryArc.Ask { item = item, amount = amount, minQuality = quality, questTitle = questTitle, questText = questText, reminder = reminder, thanks = thanks };
        return p;
    }

    private static NpcStoryArc.Part Fork(NpcStoryArc.Part p, string optionA, string[] replyA, string[] flagsA, string optionB, string[] replyB, string[] flagsB)
    {
        p.choice = new NpcStoryArc.Choice { enabled = true, optionA = optionA, replyA = replyA, flagsA = flagsA, optionB = optionB, replyB = replyB, flagsB = flagsB };
        return p;
    }

    private static NpcStoryArc.Part When(NpcStoryArc.Part p, NpcCondition c) { p.conditions = c; return p; }
    private static NpcStoryArc.Part Flags(NpcStoryArc.Part p, params string[] flags) { p.setFlags = flags; return p; }
    private static NpcStoryArc.Part Happy(NpcStoryArc.Part p) { p.happy = true; return p; }
    private static NpcStoryArc.Part Gift(NpcStoryArc.Part p, int gold, string itemAsset = null, int amount = 0)
    {
        p.reward = new NpcStoryArc.Reward { gold = gold, item = string.IsNullOrEmpty(itemAsset) ? null : FindItem(itemAsset), amount = amount };
        return p;
    }

    private static NpcCondition AfterFlag(string flag) { return new NpcCondition { requireFlags = new[] { flag } }; }
    private static NpcCondition Counter(string key, int atLeast) { return new NpcCondition { counters = new[] { new CounterCheck { key = key, atLeast = atLeast } } }; }

    private static NpcStoryArc Story(string id, string title, string pathA, string pathB, string summary, params NpcStoryArc.Part[] parts)
    {
        var arc = ScriptableObject.CreateInstance<NpcStoryArc>();
        arc.npcId = id; arc.title = title; arc.pathAName = pathA; arc.pathBName = pathB; arc.summary = summary; arc.parts = parts;
        return arc;
    }

    // ------------------------------------------------------------------ the stories
    private static List<NpcStoryArc> Stories()
    {
        var all = new List<NpcStoryArc>();

        // ============================================================ ROSA DELGADO (House 2)
        all.Add(Story("rosa", "The Soup on the Step", "The Saturday market", "Bread for the tower",
            "Rosa leaves soup on Sam's step because Sam reminds her of her son Tomas, who works in the tower and never visits. " +
            "The tower's bakery counter hurts the Kims' bread sales. Fork: start a plaza market, or sell her bread to the tower.",
            Happy(P(1, "A loaf for the farmer",
                "There you are, dear! I baked an extra one this morning, and an extra one always has somebody's name on it.",
                "Today it's yours. No, don't argue. Walt argues. You're not Walt.")),
            P(2, "The great recipe swap",
                "Has Mi-sook told you about our swap? My bread recipe for her kimchi.",
                "She thinks she won. Let her think so. I've had that kimchi on toast every morning for a week, and Walt says it's the best thing I've ever made.",
                "I haven't told him it isn't mine."),
            P(3, "Caught on the doorstep",
                "You were out early this morning, weren't you? Near Maple Lane?",
                "Then you saw me. With the soup. On Sam's step.",
                "Oh, don't look at me like that. Somebody has to make sure that young writer eats something that isn't noodles."),
            P(4, "A secret between us",
                "Promise me you won't tell Sam it's me. Please, dear.",
                "If Sam knew, Sam would feel they owed me something. And then the bowl would stop coming back. I like it when the bowl comes back washed.",
                "It's the only conversation we have, and it's a lovely one."),
            P(5, "Tomas",
                "I'll tell you why, since you've kept my secret.",
                "We have a son. Tomas. He's in the city, in that great glass tower, on the thirty-second floor. He's very important there, he says.",
                "He used to bring his washing home every Sunday and complain about my soup. I'd give anything to hear him complain about my soup.",
                "Sam sits at that window the way Tomas used to sit at his desk. That's all. An old woman's silliness."),
            Gift(Ask(P(6, "Bread for the whole street",
                "I've had an idea, and I need a farmer for it.",
                "Every house on the street, one loaf each, all baked on the same morning. The way we used to do it when the street was new.",
                "But I want real flour. Flour from wheat I can see growing. Would you bring me some?"),
                "Wheat", 10, 0, "Bread for the whole street", "Rosa wants wheat from your farm to bake a loaf for every house on the street.",
                L("Any luck with the wheat, dear? Ten will do it. The oven's been looking at me."),
                L("Look at that! Proper wheat, with dirt still on it.", "Come back tomorrow and you'll smell it from the farm. Here, take something warm for now.")),
                0, "Food_honey_pastry", 3),
            P(7, "The bakery counter",
                "Have you seen? The tower's opened a bakery counter. Bread in plastic, three for the price of one.",
                "Jae-hoon says nobody bought my loaves at the store yesterday. Not one. They've all gone to the tower for the plastic ones.",
                "And you know... you can tell Sam about the soup now, if you like. I think I'm too tired to keep secrets."),
            Fork(P(8, "What would you do?",
                "A man from the tower came to the door this morning. In a suit, on Walt's porch!",
                "He wants to buy my recipe and sell my bread at the counter. My name on the bag. More money than I've ever seen.",
                "Or... Ada says we should do what they used to do in the city. A Saturday market in the plaza. Everyone bringing what they make.",
                "You're a sensible one. What would you do?"),
                "Start a Saturday market",
                L("A market! Yes. Ada with her flowers, Soo-ah with her seeds, Mi-sook with her kimchi. And you, dear, with everything you grow.", "I'll tell Walt to build the stalls. He'll grumble for a week and then make them perfect."),
                L("suburb_market_planned"),
                "Sell the recipe to the tower",
                L("You're right. I'm old, and it's a great deal of money, and Walt's knee isn't getting better.", "My name on a bag. Fancy that. I'll ring the man in the morning."),
                L("rosa_sold_recipe")),
            Happy(A(9, "Market day",
                "Did you see it? Did you SEE it? Six stalls! Walt built every one of them and complained the whole time.",
                "Ada sold out of tulips by ten. Mi-sook gave away more kimchi than she sold. And my bread was gone before the pigeons were awake.",
                "Bring your best crops next Saturday. I've saved you the stall by the fountain.")),
            B(9, "My name on a bag",
                "I saw my bread at the tower yesterday. In plastic. With my name on it, spelled wrong.",
                "It doesn't taste like mine. They changed the flour to make it cheaper. I asked and they said it's 'the same recipe, optimised'.",
                "I've stopped baking at home. There doesn't seem much point now, does there?"),
            Gift(Flags(Happy(A(10, "Tomas comes home",
                "You'll never guess who walked through the market on Saturday. In a suit, looking for the bakery counter.",
                "Tomas. He'd heard about the market at the tower and came to see what was stealing their customers.",
                "He ate a whole loaf standing at my stall. And then he complained about my soup. Oh, it was wonderful.",
                "He's coming for Sunday dinner. And Sam is coming too. I asked them myself. Thank you, dear. For all of it.")),
                "suburb_market_open"), 500),
            Gift(Flags(Happy(B(10, "The oven's warm again",
                "I tore up the contract. Walt says I'll be sued. I said let them sue an old woman with a rolling pin.",
                "And look who came round when he heard: Tomas. He said the tower bread tastes like the bag it comes in.",
                "So I'm baking again, dear. One loaf for every house, the old way. And one for Sam, with soup, every Sunday, on the step.",
                "Some things you shouldn't sell. I'm only sorry it took me so long to remember.")),
                "rosa_baking_again"), 300)));

        // ============================================================ SAM WHITAKER (House 7)
        all.Add(Story("sam", "The Book and the Barrels", "Publish the expose", "Finish the novel",
            "Sam is a shy writer stuck on an autumn novel, who works three mornings a week at the treatment plant and sees what the tower throws away. " +
            "Linked to Rosa's soup: Part 5 needs Rosa's Part 7. Fork: publish an expose about the plant, or finish the novel and pass the notes to Marcus.",
            P(1, "The talking part",
                "Oh. Hello again. You came back. People don't usually come back.",
                "Sorry. That sounded sad. I meant it as a compliment. I'm better at writing things than saying them."),
            P(2, "The mystery soup",
                "The soup was there again this morning. Leek and potato. Still warm.",
                "I've tried staying up to catch whoever it is. I fell asleep at the window with my face on the keyboard. Page forty-one is now just the letter J."),
            P(3, "One sentence",
                "Can I read you something? Just one sentence. Don't say anything after. Just nod.",
                "\"The leaves came down all at once that year, as if the trees had agreed on it.\"",
                "...",
                "I'm going to delete it now. Thank you for nodding."),
            P(4, "Barrels at the plant",
                "You know I sort waste at the plant in the city, three mornings a week?",
                "There are barrels that come in at night. Black ones. They don't go through the treatment line. They go round the back, to the river pipe.",
                "They've all got the same logo on the side. The tower's logo. I've been writing it all down. I don't know why. Habit."),
            When(P(5, "So it was Rosa",
                "Rosa? ROSA? The soup is Rosa?",
                "Every week for a whole year. And I've walked past her house and said 'morning' like a stranger.",
                "I don't know what to do with this. I'm going to have to write about it. That's what I do with things."),
                AfterFlag("story_rosa_7")),
            P(6, "Two notebooks",
                "I've got two notebooks now. Look.",
                "This one is the novel. Autumn, the leaves, a town a bit like this one. This one is the barrels. Dates, times, the colour of the river after.",
                "Lena wants me to write the words for her picture book about the street as well. That's three. I can't even finish one."),
            Ask(P(7, "Proof",
                "A reporter at the city paper said the barrel notes aren't enough. She wants something she can put in a jar and test.",
                "I can't take anything from the plant. I'd lose the job. But you fish, don't you? In the river by your farm, downstream of the pipe?",
                "If the fish there are sick... that's proof nobody can argue with."),
                "Fish_carp", 1, 0, "A fish from downstream", "Sam needs a fish from the river by your farm, downstream of the treatment plant's pipe, as proof of the pollution.",
                L("Any fish from downstream yet? A carp would do it. They live near the bottom, where it all settles."),
                L("Look at its gills. That colour isn't right, is it?", "I'll take it to the reporter tomorrow. Thank you. I think. I'm a bit scared, honestly.")),
            Fork(P(8, "Which book?",
                "The reporter says she'll run it. My name on the front page. \"What the Tower Pours Away\".",
                "But if I do, the plant will know it was me. And I've got about three chapters of the novel left. The real one. The one I came here to write.",
                "I can't do both. I'm not brave enough to do both. Which one should I do?"),
                "Publish the expose",
                L("Okay. Okay. I'm going to do it.", "If my hands stop shaking long enough to type, it'll be in the paper next week."),
                L("sam_expose_published", "tower_file_barrels"),
                "Finish the novel",
                L("The novel. Yes. I think that's what I'm for.", "I'll give the barrel notes to Marcus Bell. He works in the tower. He'll know what to do with them, better than me."),
                L("sam_novel_finished", "tower_file_barrels")),
            A(9, "Front page",
                "It's in the paper. Front page. People keep stopping me on the road to the city.",
                "The plant manager called me into the office. I thought I was finished. He shook my hand and closed the door and said 'about time'.",
                "Victor Sterling's lawyers have written to me twice. I've filed the letters under 'research'."),
            Happy(B(9, "The last chapter",
                "I finished it. The novel. Last night, at about four in the morning.",
                "The last chapter is a street dinner. Everybody's there. There's a farmer in it, who brings too much food.",
                "Don't look at me like that. Lots of books have farmers in them.")),
            Happy(A(10, "Soup for Rosa",
                "I did something this morning. I made soup. It was terrible. Lentils. Too much salt.",
                "And I put it on Rosa's step and knocked and stood there like an idiot until she opened the door.",
                "She ate it all. She said it was the worst soup she'd ever had and asked me in for lunch.",
                "I think I'm going to stay here a long time. I hope that's all right with you.")),
            Happy(B(10, "Soup for Rosa",
                "My book's being printed. Just a small press. Fifty copies first, then we'll see.",
                "I gave the first one to Rosa this morning. With soup. Terrible soup, lentils, far too much salt. On her step. And then I knocked.",
                "The book's dedicated to her. And there's a farmer in chapter twelve, and a dedication to them too. You'll have to read it to find out who."))));

        // ============================================================ MARCUS BELL (House 6)
        all.Add(Story("marcus", "The Man in the Tower", "Blow the whistle", "Take the promotion",
            "Marcus works at Sterling's tower, feels guilty about it, and finds the dumping paperwork. Part 7 needs the flag saw_tower_dumping " +
            "(the StoryTrigger in the tower's back room). Fork: take the proof to city hall, or accept a promotion to 'change it from inside'.",
            P(1, "A guilt thing",
                "Hey! The farmer. You know, I bought all my vegetables from the Kims this week. All of them. Even the ones I don't know how to cook.",
                "Lena says I'm trying to buy forgiveness one turnip at a time. She's not wrong."),
            P(2, "Second place",
                "I've been getting up earlier to beat Kenji to the football field. Five forty-five this morning.",
                "He was already there. Him and the crow. The crow looked at me like I'd let it down."),
            Gift(Ask(P(3, "Straight from the farm",
                "Can I ask you something weird? Can I buy straight from you? Not the store, not the tower. You.",
                "Five tomatoes. I'll pay properly. I just want to eat one thing this week that I know where it came from."),
                "Tomato", 5, 0, "Tomatoes for Marcus", "Marcus wants to buy five tomatoes straight from your farm.",
                L("Still on for those tomatoes? Five. I've already told Lena I'm making sauce. I have never made sauce."),
                L("These are perfect. They smell like a garden. The ones at the tower smell like a fridge.", "Here. Don't argue, that's the price. Fair's fair.")),
                150),
            P(4, "The franchise letter",
                "They sent me to the Kims this week. With a letter. 'Become a Sterling Express franchise.'",
                "Jae-hoon read the whole thing, very politely, and then he gave me a rice ball and asked after Lena.",
                "I've never felt so small in my life. I didn't even take the rice ball. Well. I took it. But I felt bad."),
            P(5, "River disposal",
                "I saw something at work. I shouldn't tell you. I'm going to tell you.",
                "Shipping sheets. Every night, a line that just says 'river disposal'. Barrels, hundreds of them, going out the back of the tower.",
                "My job is to make the numbers look nice. I've been making that line look nice for a year."),
            P(6, "Lena's drawings",
                "Lena showed me her sketchbook last night. All the drawings of me from this year.",
                "In the spring ones I'm laughing. By summer I'm not. The last few, I'm just sitting at the kitchen table staring at nothing.",
                "She didn't say anything. She didn't have to. She's a very good artist."),
            When(P(7, "You saw it too",
                "You've been in the back room. Haven't you? Don't lie, you've got the same face I had.",
                "So it's not just paper. It's real. Somebody else has seen it.",
                "I've got copies of the shipping sheets. On a memory stick, in my sock drawer, which is the least secret place on Earth."),
                AfterFlag("saw_tower_dumping")),
            Fork(P(8, "The window office",
                "Victor called me up to the fortieth floor today. Victor Sterling himself.",
                "He offered me a promotion. A window office. Double the pay. 'A man like you could change how things are done here, Marcus.'",
                "Or I take the memory stick to Lucia at city hall tomorrow, and I never work in this city again.",
                "Lena says it's my decision. I'm asking you, because I can't trust myself to answer."),
                "Take it to city hall",
                L("Right. Tomorrow, nine o'clock, city hall. Lucia said she'll be at her desk with a sandwich and a book.", "I'm terrified. I feel better than I have in a year."),
                L("marcus_whistleblower", "tower_file_manifests"),
                "Take the promotion",
                L("From the inside. Yes. That's the smart way. You can't change anything from outside, can you?", "I'll keep the memory stick. Just in case. Just... in case."),
                L("marcus_promoted")),
            A(9, "Out of work",
                "They walked me out of the building with a cardboard box. Like in the films. There was a plant in it. It died on the way home.",
                "But the street... Frank left a toolbox on the porch with a note: 'Fix something.' Rosa sent bread. Gus told me I'd done the right thing and then told me my porch was crooked.",
                "City hall is looking at the shipping sheets. Lucia says it'll take months. I've got nothing but time."),
            B(9, "The fortieth floor",
                "The window office has a view of the river. I didn't think about that when I said yes.",
                "I can see the pipe from my desk. At night the water goes a different colour.",
                "I'm changing things from the inside. I've changed the coffee machine. It's a start. It's a start, isn't it?"),
            Gift(Happy(A(10, "First at the field",
                "Guess who was first at the football field this morning. Five thirty.",
                "Kenji turned up, saw me, and bowed. The crow said something. I think it was 'well done'.",
                "Lucia's got me a job at city hall, helping with the river case. Less money. A window, though. A real one, that opens.",
                "Thank you. I mean it. You were the only one who asked me the question out loud.")),
                0),
            Gift(Flags(Happy(B(10, "The box under the arm",
                "I quit. This morning. I walked out of the fortieth floor with a box of my own.",
                "Not a dead plant this time. Every shipping sheet from the last two years. I made copies of all of them while I was 'changing things from the inside'.",
                "Turns out that's what I was doing in there after all. Lucia has them now.",
                "I'm going jogging. I bet I can beat Kenji tomorrow. I've had a year of practice at getting up early and feeling awful.")),
                "tower_file_manifests"), 0)));

        // ============================================================ FRANK (Frank's Home and Hardware)
        all.Add(Story("frank", "The Cot in the Office", "The co-op", "Sell to Sterling",
            "Frank sold his house to keep the store open and sleeps on the cot in his back office. Sterling's flat-pack doors undercut him. " +
            "Part 4 needs 3 house upgrades. Fork: turn the store into a neighbourhood co-op, or sell it to Sterling for a fair price.",
            P(1, "Measure twice",
                "You know what my dad used to say? Measure twice, cut once.",
                "He also said 'never trust a man who doesn't own a level', which is why I've got eleven of them. Eleven! You want one? Take one."),
            P(2, "Every item has a story",
                "See that tin of screws? Bought it the day I opened. Thirty-one years ago. Never sold one. Not one!",
                "I keep it on the shelf because it reminds me: you don't open a hardware store to get rich. You open it so that when somebody's door won't shut at nine at night, there's somebody to call."),
            P(3, "The cot",
                "You saw the cot, didn't you. In the back office. With the toothbrush.",
                "Ha! It's nothing. Late nights, stock counts, you know how it is. Sometimes it's easier to just sleep here.",
                "Every night, for two years, it's been easier to sleep here. Let's not make a thing of it."),
            When(P(4, "Doors in a box",
                "Have you seen the tower's new catalogue? Doors in a box! Half my price.",
                "You know what's in that box? A door made of cardboard and hope. Rain gets in, it swells up like a sponge.",
                "But people see the price, and they don't see the rain. Three customers this week went to the tower. Three!"),
                Counter("house_upgrades", 3)),
            P(5, "Gus finds out",
                "Gus knows. About the house. He came round early with coffee and found me shaving in the stock-room mirror.",
                "Forty years we've been friends, and he shouted at me for the first time. 'Why didn't you SAY something, you stubborn old fool?'",
                "I told him a man doesn't say something. A man fixes it. He said that's the stupidest thing he's ever heard. He might be right."),
            Ask(P(6, "A display to beat the tower",
                "Here's my plan. The tower sells pictures of doors. I'm going to build a real one right in the window. Oak, brass hinges, the works.",
                "People can open it, close it, knock on it. Let's see a box do that!",
                "I need good timber, though. Would you bring me some wood?"),
                "Wood", 20, 0, "Timber for Frank's window", "Frank wants 20 Wood to build a real door in his shop window, to beat the tower's flat-pack doors.",
                L("How's the wood coming? Twenty pieces and I can start tonight. I don't sleep much anyway. Ha."),
                L("Now THAT is timber. Smell that!", "Come back in a few days. That door is going to be the best thing on Shop Street.")),
            P(7, "The bank letter",
                "Emeka brought a letter this morning. He looked like he was delivering it to a funeral.",
                "The bank. The store's loan. Three months to pay, or they take the building.",
                "And a man from Sterling turned up an hour later, like he'd been waiting round the corner. He made me an offer. A fair one, too. That's the worst part."),
            Fork(P(8, "Frank's choice",
                "Gus has an idea. A co-op, he calls it. Everybody on the street buys a share of the store. Walt, the Kims, Marcus, Gus. You.",
                "It'd still be Frank's. Just... Frank's and everybody's. I'd have to let people tell me what to do. Walt would tell me what to do. Every day.",
                "Or I take Sterling's money, pay the bank, buy a little house, and sleep in a real bed. You tell me. I'm too close to see it straight."),
                "Make it a co-op",
                L("A co-op. Frank's and everybody's.", "Walt's going to make me put the screws in alphabetical order. I can feel it. All right. All right! Let's do it."),
                L("frank_coop"),
                "Sell to Sterling",
                L("Sell. Yeah. Yeah, that's the sensible thing.", "Thirty-one years. I'll sign on Monday. Don't tell Gus yet. Let me tell Gus."),
                L("frank_sold_store")),
            Happy(A(9, "Frank's and everybody's",
                "Fifteen shares sold in a week! Rosa bought one with her egg money. Nia bought one with her pocket money, so now I've got a nine-year-old on the board.",
                "Walt reorganised the screws. Gus stands at the door and tells everybody my prices are robbery, then sends them in.",
                "Saturday mornings we're running workshops. Fix your own tap. Hang your own door. Thirty people last week!")),
            B(9, "Feet up",
                "I'm a man of leisure now. A house! With a bed! I've got nothing to fix.",
                "Every morning I walk past the store. They've painted it grey. The sign says 'Sterling Home Essentials'. The tin of screws is gone.",
                "Somebody's door wouldn't shut last night, on Maple Lane. They rang the store. It's shut at six now. Nobody came."),
            Gift(Happy(A(10, "A house for Frank",
                "I want to show you something. Come round the side of the store. No, the side!",
                "Gus and Walt built it. Walt did the frame, Gus did the roof, and they only stopped speaking twice.",
                "A little house. Right next to the store. With a door that shuts properly and a real bed in it.",
                "The cot's going in the museum, Yuki says. 'An important historical object.' Ha! Thank you, farmer. Truly. Here, take a share of the first profits.")),
                800),
            Gift(Happy(B(10, "Frank's Fix-It",
                "Couldn't stand it. Four weeks of feet up and I nearly went round the bend.",
                "So I've got a stall now, in the plaza, by the fountain. 'Frank's Fix-It'. Doors, taps, hinges, bikes. Cash or cake.",
                "Didn't need a building after all. Just a toolbox and people who know my name.",
                "And there's a tin of screws on the counter. Brand new. I'll never sell one.")),
                300)));

        // ============================================================ THE KIMS (General Store, apartment upstairs)
        // Their stories lean on each other: Jae-hoon's Part 6 needs Ji-ho's Part 5, Mi-sook's Part 6 needs Soo-ah's Part 4,
        // and Jae-hoon's Part 7 (the franchise offer) needs Marcus's Part 4 (the day Marcus brought the letter).

        // ------------------------------------------------------------ JAE-HOON (dad)
        all.Add(Story("dad", "The First Dollar", "The local supplier", "The franchise",
            "Thirty years behind the till. Jae-hoon wants the store to outlive him, but the tower's prices and a Sterling-owned wholesaler squeeze him. " +
            "Fork: become the town's local shop, stocked from your farm, or take Sterling's franchise offer.",
            P(1, "The frame under the till",
                "You see that little frame under the till? The first dollar this store ever took. Thirty years ago, from Rosa Delgado, for a loaf of bread she'd baked herself.",
                "My wife says I should spend it. I say some things are worth more than a dollar."),
            P(2, "What do you grow?",
                "Tell me, what are you growing out there? Properly, I mean. Not the polite answer.",
                "I've been thinking. The tower sells vegetables from six countries away. What if a shop sold vegetables from six minutes away?",
                "Ha. Mi-sook says I'm dreaming again. She says it like it's a bad thing."),
            Gift(Ask(P(3, "From the farm",
                "Let's try it. Bring me ten of something you grow, and I'll put it by the till with a sign. 'From the farm.' Hand-written, so people know it's real.",
                "Turnips, maybe. Nia Abara says nobody likes turnips. I want to prove her wrong."),
                "Turnip", 10, 0, "From the farm", "Jae-hoon wants 10 Turnips to sell by the till under a hand-written 'From the farm' sign.",
                L("Still bringing me those turnips? Ten. I've already written the sign. It's very good. Soo-ah fixed the spelling."),
                L("Look at these! Dirt and all. Perfect.", "Here, I'll pay you what the tower would, and a little more for the walk.")),
                220),
            P(4, "The wholesaler",
                "Sunday I went to my wholesaler in the city, same as every Sunday for twenty years.",
                "New sign over the door. 'A Sterling Company.' And new prices. Everything up by a third, but only for small shops. The tower pays the old price.",
                "The man at the desk was very sorry. He said it three times. Sorry doesn't stock shelves."),
            P(5, "Sums at midnight",
                "You're up late too? I saw your lamp from the window.",
                "I've been doing the sums at the kitchen table. Again. They come out the same every time, no matter how hard I press the pencil.",
                "Six more months like this and the store is losing money. Don't tell the children. Please."),
            When(P(6, "I never asked him",
                "Ji-ho told me about the business course. He thought I would shout.",
                "I didn't shout. I just sat there. Thirty years I've been saying 'one day this will all be yours', and I never once asked if he wanted it.",
                "What kind of father plans a whole life for his son and forgets to ask the son?"),
                AfterFlag("story_son_5")),
            When(P(7, "The franchise letter",
                "Marcus Bell came in with a letter from the tower. Poor boy. He looked like he wanted the floor to swallow him.",
                "'Become a Sterling Express franchise.' They would pay off what I owe. New fridges, new lights, new uniforms. Their name over the door, and mine nowhere.",
                "I gave Marcus a rice ball. It isn't his fault. But I haven't stopped reading the letter since."),
                AfterFlag("story_marcus_4")),
            Fork(P(8, "Two signs",
                "I've made two signs. Look. This one says 'Sterling Express'. They sent it, in the post, before I even said yes.",
                "This one I painted myself last night. 'Kim's General Store. Local food, local people.' The paint isn't dry yet.",
                "If I go local, you would have to bring me crops every week, and we would still be poor for a while. If I sign, we're safe, and it isn't ours any more.",
                "You've been honest with me since the turnips. Be honest now. Which sign goes up?"),
                "Go local",
                L("The wet one. Yes. The wet one.", "Ji-ho, come down! Bring a ladder! And don't drop it on the slush machine this time!"),
                L("kims_local_supplier"),
                "Take the franchise",
                L("Safe. Yes. Safe is good. My children won't have to do sums at midnight.", "I'll ring them in the morning. You'll still come in, won't you? Even when it's... their shop?"),
                L("kims_franchise")),
            Happy(A(9, "Sold out on Saturday",
                "Saturday the whole 'From the farm' shelf was empty by eleven o'clock. Empty! I had to put up a sign saying 'More on Tuesday'.",
                "People are coming back. Not everyone. But Grace Abara came in and said 'I'd rather pay a little more and know your name'.",
                "Keep the crops coming, farmer. This shop has a future again, and half of it grows on your land.")),
            B(9, "The bright cold shop",
                "New lights. Very bright. New fridges. Very cold. The till talks to the tower all by itself.",
                "The regulars still come. But they don't stay at the counter any more. There's nothing to talk about. The prices are the same as everywhere.",
                "Rosa bought bread yesterday. Not hers. Theirs. She didn't say anything and neither did I."),
            Gift(Happy(A(10, "The second dollar",
                "Look under the till. Two frames now.",
                "The first dollar, from Rosa, thirty years ago. And the second, from the first week the farm shelf made more than the tower took from us.",
                "Tuesdays and Fridays, Ji-ho runs the till now. He's better at it than me, which is very annoying.",
                "Thank you, farmer. You didn't just sell me turnips. You sold me the next thirty years.")),
                500),
            Gift(Flags(Happy(B(10, "Starting again",
                "I took their sign down this morning. Myself. Walt Delgado held the ladder and told me I was doing it wrong the whole time.",
                "I'll pay them back. It will take years. Ji-ho has a plan with a spreadsheet, and Soo-ah has a plan with seeds, and Mi-sook has a plan with food.",
                "Thirty-one years, and starting again. Bring me turnips, farmer. Nia Abara is still wrong about turnips.")),
                "kims_local_supplier"), 300)));

        // ------------------------------------------------------------ MI-SOOK (mom)
        all.Add(Story("mom", "Have You Eaten?", "The street dinner", "The competition",
            "Mi-sook feeds the whole street and never herself. Her 'Saturday friends in the city' are really a cooking class she's too shy to mention. " +
            "Fork: enter a Sterling-sponsored cooking competition, or cook one big dinner for the whole street.",
            Gift(Happy(P(1, "Take the rice ball",
                "Have you eaten today? Don't lie. I can always tell. You have a 'no breakfast' face.",
                "Here. Take it. No, no, take it. If you don't take it, I'll put it in your pocket and it will be squashed. Better to take it.")),
                0, "Food_rice_triangle", 2),
            P(2, "Who won the swap",
                "Did Rosa tell you about our recipe swap? Her bread for my kimchi.",
                "She thinks she won. I know she has been eating my kimchi on toast every morning. Walt told Jae-hoon, and Jae-hoon told me. Husbands are terrible at secrets.",
                "Don't tell her I know. It's more fun this way."),
            Ask(P(3, "Something for something",
                "Could you bring me some strawberries? Five. Good ones.",
                "What for? Oh... for something. Just something. Never mind what for."),
                "Strawberry", 5, 0, "Strawberries for something", "Mi-sook wants five strawberries. She won't say what for.",
                L("The strawberries? Five, please. It's for... something. Stop smiling."),
                L("Beautiful. So red.", "Now go, go. I have something to practise. I mean, to cook. To cook.")),
            P(4, "The apron",
                "You saw me. In the city, at the café. In the apron with my name stitched on it.",
                "It's a cooking class. Every Saturday, for a year. My 'friends in the city' are a French chef who shouts and eleven people half my age.",
                "Please don't tell the children. They would make a fuss. I don't want a fuss. I just wanted something that was only for me."),
            P(5, "No, no, no",
                "My teacher says I should enter the big cooking competition in the city. On television!",
                "I told her no. Who would stock the shelves? Who would cook dinner? Who would make sure Ji-ho eats a vegetable?",
                "...She said 'Ji-ho is twenty-two'. She doesn't understand. Twenty-two is very young for vegetables."),
            When(P(6, "Soo-ah knows",
                "Soo-ah found the apron in my bag. She cried. She said 'Why didn't you tell me, Umma? I tell you everything.'",
                "I didn't know what to say. Thirty years I've been feeding everyone. I never learned how to ask for anything.",
                "She's not angry any more. Now she wants me to enter the competition. Everybody wants me to enter the competition."),
                AfterFlag("story_daughter_4")),
            Ask(P(7, "The best ingredients",
                "All right. All right! I'm going to cook something properly. My mother's recipe, but my way.",
                "I need the best pumpkins you can grow. Not ordinary ones. Silver quality, at least. The kind that make you close your eyes.",
                "Whatever I decide to do with the dish, it has to start with the best."),
                "Pumpkin", 3, 1, "Mi-sook's best dish", "Mi-sook needs three Silver-quality (or better) pumpkins for her mother's recipe. Fertilizer from the city improves crop quality.",
                L("Pumpkins, Silver quality or better. Three. I know it's a lot to ask. I'm asking anyway. See? I'm learning."),
                L("Oh. Oh, these are perfect.", "My mother would have held one up to the light like this. Thank you.")),
            Fork(P(8, "Where should I cook it?",
                "The competition is next month. Sterling Foods is paying for it. The prize is a lot of money, and they put your dish in their shops.",
                "Or... I had another idea. In my village, once a year, everybody brought a table into the street and we ate together. All of us.",
                "One big dinner, here, in the plaza. Everybody brings one thing. And for once, I cook something for me and they all eat it.",
                "Which one? Don't think. Just say."),
                "Cook a street dinner",
                L("A street dinner. Yes. Yes!", "I'm telling Rosa first. She'll bring bread. Then Frank will bring terrible sandwiches, and Gus will complain about them, and it will be perfect."),
                L("street_dinner_planned"),
                "Enter the competition",
                L("The competition. On television! Ji-ho will laugh at me.", "Good. Let him laugh. I'll be the one on television."),
                L("misook_competition")),
            Happy(A(9, "Long tables",
                "Did you see the tables? From the fountain all the way to the slush machine!",
                "Gus said Frank's sandwiches were the worst food he'd ever tasted, and then he ate four. Kenji brought tea. Tariq played until the street lamps came on.",
                "And my pumpkin dish was gone before I even sat down. Everyone asked for the recipe. I said maybe. Maybe means yes, but slowly.")),
            B(9, "Second place",
                "Second! Second place, on television! The judge said my dish 'tasted like somebody's home'.",
                "Then this week I saw it in the tower. In a plastic box. 'Sterling Kitchen Pumpkin Bowl'. With my mother's recipe, changed to be cheaper. No name.",
                "They said it's in the rules. Page eleven. Nobody reads page eleven."),
            Gift(Happy(A(10, "Somebody cooks for her",
                "Sit down. No, sit. Tonight somebody else is cooking. Soo-ah, Ji-ho and Jae-hoon have locked me out of my own kitchen.",
                "Thirty years I've fed everybody. Tonight they feed me. It smells... it smells burnt, actually. But it's the best thing I've ever smelled.",
                "Take these for the road. And next Saturday, come to class with me. My chef shouts, but he shouts kindly.")),
                0, "Food_kimchi_jar", 2),
            Gift(Flags(Happy(B(10, "My name on it",
                "I made a recipe book. Not for the tower. For the street. Everybody's recipes: Rosa's bread, Kenji's tea, Gus's terrible stew.",
                "And on page one, my mother's pumpkin dish. The real one. With her name on it, and mine.",
                "The tower can sell the box. They can't sell the page. Here. The first copy is for the farmer whose pumpkins started it.")),
                "street_cookbook"), 0, "Food_kimchi_jar", 2)));

        // ------------------------------------------------------------ JI-HO (son)
        all.Add(Story("son", "The Reluctant Heir", "The business course", "Stay and change the store",
            "Everybody thinks Ji-ho is lazy. He's actually great with numbers and people; he just doesn't want his parents' life. " +
            "Fork: take a business course in the city, or stay and modernise the store his way (the games corner).",
            P(1, "Already restocked",
                "Oh, hey. If Mom asks, I already restocked the coolers.",
                "I haven't. But I'm going to. In my head I basically have. That counts for about seventy percent."),
            P(2, "The floor plan",
                "Don't look at this. Okay, you can look at this. It's a floor plan.",
                "If you move the cleaning stuff to the back and turn the corner by the window round, there's room for two arcade machines and a bench.",
                "Kids would hang out here instead of at the tower. They'd buy slush. I've done the maths. Dad would hate it."),
            P(3, "Terrible in goal",
                "Dev says I'm the worst keeper he's ever seen. He's right. I dive the wrong way on purpose so it looks like I meant it.",
                "The thing is, nobody else will go in goal. So it's me, or no team. I'd rather be bad at something than have no team."),
            P(4, "The slush trick",
                "Promise you won't tell anyone? I changed the slush prices. Small one cheaper, big one a bit dearer, and a 'medium' that nobody needs.",
                "Everybody buys the medium now. Sales are up a third. Nobody noticed. Not even Dad.",
                "I kind of want somebody to notice. But if they notice, they'll expect me to do it again."),
            P(5, "The letter from the city",
                "I got into a business course. In the city. Two years. I applied in the winter and didn't tell anyone, in case I didn't get in.",
                "Now I did get in, and I still haven't told anyone. Except you. Right now. Oh no.",
                "If I go, who helps Mom on Tuesdays? If I stay, I'm stocking coolers until I'm fifty."),
            When(P(6, "The napkin",
                "I worked something out on a napkin last night. Look.",
                "This is what the store took last year. This is what the wholesaler's new prices cost us. This is what the tower's taking from us, every week.",
                "Dad's been doing these sums on paper for months. On his own. He thinks we don't know. I've known since the first week."),
                AfterFlag("story_dad_5")),
            P(7, "Diving the right way",
                "We played the city team on Sunday. The ones in the Sterling shirts.",
                "Last minute, penalty. And I dived the RIGHT way. For the first time in my life. Dev picked me up off the grass and screamed in my face. It was great.",
                "I think I'm only good at things when they matter. Is that a thing? That might be a thing."),
            Fork(P(8, "Go or stay",
                "I have to tell the course by Friday.",
                "If I go, I learn how big companies work. How the tower works. Then maybe one day I come back and beat them properly.",
                "If I stay, I do it now, my way. The games corner, the slush trick, everything. Small, but real.",
                "Mom and Dad say it's my choice. Which is worse, honestly. What would you do?"),
                "Take the course",
                L("The course. Okay. Okay, wow.", "I'm going to tell Dad tonight. If you hear shouting, it's me, from happiness. Probably."),
                L("jiho_business_course"),
                "Stay and change the store",
                L("Stay. Yeah. I think I knew that already. I just needed someone else to say it.", "Right. I'm going to need a screwdriver, two arcade machines and my dad in a very good mood."),
                L("jiho_games_corner")),
            A(9, "Weekend ideas",
                "First month of the course. I come home every weekend with a notebook full of ideas, and Dad pretends he isn't reading them.",
                "Do you know the tower loses money on half of what it sells? On purpose. To kill small shops. Then it puts the prices back up. There's a name for it in the textbook.",
                "I'm going to find every trick they use, and then I'm going to find the trick that beats it."),
            Happy(B(9, "The games corner",
                "It's open! Two machines, one bench, and a 'no shouting after eight' sign that nobody reads.",
                "Dev and half the football team are in here every afternoon. They buy slush. Medium slush. Nobody needs a medium slush.",
                "Dad came down at closing time, looked at it for a long time, and said 'hm'. That's the best 'hm' I've ever had.")),
            Happy(A(10, "Two at the till",
                "Look at this. Me and Dad, at the till together, on a Saturday.",
                "I brought back one idea from the course: a loyalty card, but on paper, stamped by hand. Ten stamps, one free slush. People love it. People love stamps.",
                "Dad still pretends he isn't proud. He's terrible at pretending. Thanks for telling me to go. I'll be back for good. I always was going to be.")),
            Happy(B(10, "Two at the till",
                "Dad gave me the keys for Tuesdays and Fridays. The real keys. Not the spare that doesn't fit the back door.",
                "I stocked the coolers this morning. Actually stocked them. One hundred percent. Not seventy.",
                "He's standing behind me right now pretending to check the shelves. He's checking me. Hi, Dad. Thanks for telling me to stay, farmer. I'm glad someone did."))));

        // ------------------------------------------------------------ SOO-AH (daughter)
        all.Add(Story("daughter", "The Seed Tin", "The seed library", "The internship",
            "A plant nerd who studies in the city. Soo-ah finds old local seeds in the store's basement and wants to save them before Sterling's patented seeds replace everything. " +
            "Fork: start a free seed library in the store, or take an internship at Sterling AgriSeed to learn their secrets.",
            P(1, "You noticed, right?",
                "I sorted the seed rack by season AND by colour. Spring at the top, winter at the bottom, rainbow from left to right.",
                "Nobody noticed. You noticed, right? Please say you noticed."),
            P(2, "Faded packets",
                "If a seed packet looks faded, tell me and I'll swap it. Old seeds still grow, mostly. But faded packets make people sad.",
                "That's my real job, I think. Not selling seeds. Stopping people being sad about seeds."),
            Gift(P(3, "The tin in the basement",
                "I found something in the basement behind the old freezer. A biscuit tin. Inside, paper envelopes, all hand-written.",
                "Seeds. Really old ones. The writing on this one says 'Halmeoni's pumpkin, the good one'. That's my grandmother. She died before I was born.",
                "Would you try to grow some? I don't have a garden. You have a whole farm. Here, take a handful."),
                0, "SeedsPumpkin", 5),
            Ask(P(4, "Grandmother's pumpkin",
                "Did any of Halmeoni's seeds come up? Even one?",
                "If you can grow some, bring them to me. I want to save the seeds from inside them. That's how a plant stays alive for a hundred years: someone keeps saving the seeds."),
                "Pumpkin", 2, 0, "Halmeoni's pumpkins", "Soo-ah wants two pumpkins grown from her grandmother's old seeds, so she can save their seeds.",
                L("Any pumpkins yet? Two is enough. I just need what's inside."),
                L("Oh. Oh, they're real. They're actually real.", "Mom cried when she saw them. She said they look exactly like the ones in the village. Thank you.")),
            P(5, "Three hours at the seed rack",
                "Bea Lindqvist came in on Tuesday to buy one packet of seeds. One packet.",
                "We talked for three hours. About soil, and roots, and which flowers bees like best. Dad had to turn the lights off to make us leave.",
                "She's coming back Thursday. I've sorted the rack again. Don't tell her that's why."),
            P(6, "The internship offer",
                "My professor offered me an internship. A paid one. At Sterling AgriSeed, in the city.",
                "They make the seeds the tower sells. Huge labs, every machine you can imagine. It's the kind of thing people in my course would fight for.",
                "I should be happy. Why aren't I happy?"),
            P(7, "Seeds that don't come back",
                "I found out why I wasn't happy.",
                "AgriSeed seeds are bred so that if you save the seeds from your crop, they don't grow properly. You have to buy new ones. Every year. From them.",
                "Halmeoni's pumpkin has grown for a hundred years because people saved it. Theirs is made so nobody ever can."),
            Fork(P(8, "The library or the lab",
                "I've got an idea. A seed library. In the store, in the corner by the rack. Free.",
                "You borrow seeds, grow them, and bring some back at the end of the season. Everybody's seeds, from everybody's gardens. Halmeoni's pumpkin first.",
                "Or I take the internship. Learn exactly how AgriSeed does it, from the inside. Find something we can use against them. I'd be in that building every day.",
                "You helped grow the pumpkins. You choose with me."),
                "Start the seed library",
                L("The library! I'm going to need tiny envelopes. So many tiny envelopes.", "And a sign. Lena will draw it. And Bea will help. Bea will definitely help."),
                L("seed_library_planned"),
                "Take the internship",
                L("The internship. Okay. I'll be careful.", "I'll write everything down. Every test, every number. If they're hiding something, I'll find it."),
                L("sooah_internship")),
            Happy(A(9, "Borrowed seeds",
                "Forty envelopes borrowed in the first week! Ada took the sweet peas. Mateo took a tree I've never heard of. Nia took 'whatever bugs like'.",
                "And Bea brought back seeds from her university garden that nobody's grown here for fifty years.",
                "At the end of the season, they'll all come back. More than went out. That's the thing about seeds: they come back with interest.")),
            B(9, "What the lab knows",
                "I've been at AgriSeed for a month. The labs are amazing. I hate how amazing they are.",
                "I found the soil tests. Their seeds grow badly in polluted ground. They know the river is poisoned. They sell 'river-resistant' seeds for double the price to the farms downstream.",
                "They're making money from the pollution their own tower causes. I copied the report. Bea has it now. I'm scared and I'm angry."),
            Gift(Flags(Happy(A(10, "Grown by the farm",
                "Look at the rack. The top shelf is new.",
                "'Local heirloom seeds. Grown on the farm.' Halmeoni's pumpkin, the good one. With your farm's name under it, as the grower.",
                "These are the first packets. They're for you. Keep some back each harvest, and they'll keep going long after both of us.")),
                "seed_library_open"), 0, "SeedsPumpkin", 10),
            Gift(Flags(Happy(B(10, "Grown by the farm",
                "I quit the internship. Bea and I took the soil report to city hall together. Lucia read the whole thing with her sandwich going cold.",
                "And then I came home and opened a seed library anyway. You can't keep Halmeoni's pumpkin in a lab.",
                "Top shelf, look: 'Local heirloom seeds. Grown on the farm.' Your farm's name, as the grower. The first packets are yours.")),
                "seed_library_open", "tower_file_seeds"), 0, "SeedsPumpkin", 10)));

        // ============================================================ THE ABARAS (House 1)
        // Linked: Emeka's Part 5 needs Nia's Part 4 (the sick beetle), and Grace's Part 6 needs Sam's Part 4 (the barrels at the plant).

        // ------------------------------------------------------------ EMEKA (mail carrier)
        all.Add(Story("emeka", "The Dead Letter Box", "Save the route", "Take the tower job",
            "Emeka knows everyone's business and tells nobody. Sterling wants to replace his round with parcel lockers. " +
            "He keeps a box of letters nobody could deliver, and one is for whoever farms your land. Fork: petition city hall to keep the round, or take Sterling's logistics job.",
            Gift(Happy(P(1, "Your first postcard",
                "Told you everybody gets a postcard in their first weeks. Here. Hand-delivered, because I couldn't wait for tomorrow's round.",
                "It's a picture of the plaza. On the back it says 'Welcome to the street. From the street.' Everybody signed it. Gus signed it twice, once to complain about the pen.")),
                0, "Letter", 1),
            P(2, "Eleven doorsteps",
                "You know what you learn, eleven doorsteps every morning? Who's up early, who's up late, who's waiting for a letter that never comes.",
                "Gus gets a seed catalogue every month and has never planted a seed. Kenji writes to his brother in Japan every Friday. Ada gets nothing at all, and checks the box every day anyway.",
                "I don't tell people what I know. I just make sure I knock a bit louder on the quiet houses."),
            P(3, "The dead letter box",
                "Come here. Look. Under the counter at the sorting office, I keep a shoebox. Well. Three shoeboxes.",
                "Dead letters. Wrong addresses, people who moved, names nobody remembers. Twenty years of them. The rules say I should send them to be shredded.",
                "I've never been very good at that rule."),
            P(4, "The lockers",
                "Have you seen the metal boxes in the plaza? Yellow, with the tower's logo?",
                "Sterling Parcel Lockers. You get a code on your phone, you open a little door, you take your parcel. No knock. No 'morning, Emeka'.",
                "The post office says my round is going down to three days a week. 'To reflect changing demand.' I asked who's demanding. They didn't know."),
            When(P(5, "Nia asks",
                "Nia asked me last night why I keep staring at my boots. I said I'm checking the laces.",
                "She said, 'Dad, you've been checking them for a week.' She notices everything, that girl. Bugs, beetles, her father being sad.",
                "She found a sick beetle by the river this week. Grey, she said, like it had been dipped in ash. She's more worried about that beetle than about my job. Good. So am I."),
                AfterFlag("story_nia_4")),
            P(6, "Letters nobody read",
                "I read you one of the dead letters. Just one. I've never opened one before, but this one wasn't sealed.",
                "'Dear Ada, the stall will be fine without you for a week. Come to the coast. Bring the cat. Yours, always, J.' Postmarked thirty-one years ago. Wrong house number.",
                "I took it to her this morning. She read it on the step, and then she sat down on the step, and we sat there for an hour. My round was very late."),
            P(7, "For whoever farms here next",
                "There's one in the box for your farm. It's older than me. The envelope just says 'To whoever farms here next'.",
                "I think it's yours. Here. Read it at home.",
                "...You read it already? What did it say? No. Don't tell me. I'm the mail carrier, not the reader. Just tell me it was a good one."),
            Fork(P(8, "The offer",
                "A woman from the tower stopped me on the round today. Very polite. Very good shoes.",
                "'Logistics supervisor. Sterling Parcel Network.' A desk, a chair, three times the pay, and no more wet socks. I'd run the lockers. All of them.",
                "Or Lucia says if the whole street signs a petition, city hall has to look at the round again. Every door. Every name.",
                "Grace says I should decide with my heart. My heart says my feet hurt. What do you say?"),
                "Start the petition",
                L("A petition it is. I'll start with Gus, because if Gus signs, everybody signs, just to disagree with him.", "And I know every door on this street. That's the one advantage I've got over a yellow box."),
                L("mail_petition"),
                "Take the tower job",
                L("A desk. Dry socks. Three times the pay. Nia can have football boots that fit.", "I'll tell the post office tomorrow. Somebody else will do the round. Somebody will."),
                L("emeka_tower_job")),
            Happy(A(9, "Every door",
                "Two hundred and six names! The whole street, the store, Frank's, and half the city hall staff. Kenji signed in calligraphy. It took him an hour.",
                "Lucia carried it into the council meeting herself. The lockers are staying, but so am I. Six days a week, like before.",
                "And look: nobody's using the lockers. The yellow doors just sit there, shut. People would rather wait for a knock.")),
            B(9, "The quiet street",
                "Third week at the desk. I can see a map of every locker in the city. Little green lights. No faces.",
                "The new carrier only does the street twice a week. Gus didn't get his catalogue. Kenji's letter to Japan sat in a box for nine days.",
                "And I don't know any of the news any more. Rosa had a cold, and I only found out when it was better. That's never happened."),
            Gift(Happy(A(10, "A letter from everyone",
                "This morning I had a letter in my own bag, addressed to me. In Nia's handwriting.",
                "Inside: one page from every house on the street. Rosa drew a loaf. Walt wrote 'adequate service'. Ada wrote 'thank you for J.'",
                "And there's one for you, too. Here. I'm not telling you what it says. I'm the mail carrier, not the reader.")),
                0, "Letter", 1),
            Gift(Flags(Happy(B(10, "One more round",
                "I quit. Handed in my badge and walked out with my old bag over my shoulder. The woman in the good shoes didn't understand.",
                "Then I walked the old round. All eleven doorsteps, with nothing to deliver. Just knocking. 'Morning, Rosa. Morning, Gus. Morning, Kenji.'",
                "The post office took me back the next day. Six days a week. Here: the first letter I've delivered since. It's from me. It says 'thanks'.")),
                "mail_petition"), 0, "Letter", 1)));

        // ------------------------------------------------------------ GRACE (historian)
        all.Add(Story("grace", "What the River Remembers", "Publish the history", "Keep the files safe",
            "Grace is writing the history of the valley and finds that the Sterling family's first business poisoned the river long before the tower. " +
            "Her university is funded by Sterling. Fork: publish and risk her job, or hide the files until the time is right.",
            P(1, "Rent",
                "Remember what I told you? 'Farmer' comes from an old word for rent. You pay the land, one way or another.",
                "Here is another one, free of charge: 'neighbour' means 'the farmer who lives nearby'. So you are everybody's neighbour, by definition. Congratulations."),
            P(2, "Anything old",
                "I am writing a history of the valley. Before the suburb, before the city, when it was all fields and one long, clean river.",
                "Nobody has written it properly. The city library's version is twelve pages long, and eleven of them are about the tower.",
                "If you dig, fish or find anything old, bring it to me. Even rubbish. Rubbish is the most honest thing a century leaves behind."),
            Ask(P(3, "Rubbish from the river",
                "I mean it about rubbish. Old bottles, buttons, boots. Especially boots: people wrote their names in them, and the makers stamped theirs inside.",
                "Fish in the river near your farm long enough and it will give you a boot. It always does. Bring me one."),
                "OldBoot", 1, 0, "A boot from the river", "Grace wants an old boot fished out of the river. She says the maker's stamp could tell her something.",
                L("No boot yet? Keep fishing. The river is generous with boots. It is the fish it keeps to itself."),
                L("Perfect. Disgusting, and perfect.", "Look, inside the heel. A stamp: 'Sterling & Sons Tannery'. A tannery. Do you know what tanneries poured into rivers? I am going to find out exactly.")),
            P(4, "Sterling & Sons",
                "I found them. Sterling & Sons Tannery, upstream of your farm, ninety years ago. Victor Sterling's great-grandfather.",
                "Tanneries soak leather in lime and chrome and things I won't say in front of Nia. All of it went into the river. For forty years.",
                "So the tower didn't start this. It is only the newest chapter of a very old family habit."),
            P(5, "The Sterling Wing",
                "The university opened its new building today. The Sterling Wing. Very modern. Lots of glass. My office is in it.",
                "Victor Sterling cut the ribbon himself. He shook my hand and said he'd heard I was writing a history of the valley. 'I'd love to read it,' he said.",
                "I have never been so frightened by a compliment."),
            When(P(6, "The old maps",
                "The archive had maps from before the tannery. There was a wetland, right where your farm's bottom fields meet the river. Reeds, birds, clean water.",
                "The Sterlings drained it to build the tannery yard. Without the wetland, the river has nothing to clean it. Everything that goes in stays in.",
                "And now Sam Whitaker tells me about black barrels going into the river pipe at night. Ninety years, the same river, the same family."),
                AfterFlag("story_sam_4")),
            P(7, "The dean's request",
                "The dean called me in today. He was very kind. He said the university 'values its partnership with Sterling Holdings'.",
                "He said a history of the valley would be wonderful, as long as it was 'balanced'. He used that word nine times. I counted.",
                "I marked forty essays tonight out of pure fury. Some of those students got very good grades they did not deserve."),
            Fork(P(8, "Publish or wait",
                "The book is finished. Every map, every tannery record, every boot. It is the best thing I have ever written.",
                "If I publish, I will lose my job in the Sterling Wing. Probably my job anywhere. Emeka says we will manage. Nia says I can be a goalkeeper.",
                "Or I keep the files somewhere safe and wait until someone can use them properly. A court. City hall. Someone braver than a history lecturer.",
                "You found the boot. You have a vote."),
                "Publish it",
                L("Then I will publish it. And I will give a lecture, in the plaza, free, to anybody who wants to come.", "History is not for the Sterling Wing. It is for the people who live where it happened."),
                L("grace_published", "tower_file_maps"),
                "Keep the files safe",
                L("Wait, then. A history that nobody reads can't be silenced, at least.", "Here. Copies of everything, for you. Keep them somewhere damp-proof. Not in a barn. Please, not in a barn."),
                L("grace_files_hidden", "tower_file_maps")),
            Happy(A(9, "The plaza lecture",
                "Did you see how many came? I counted sixty chairs, and then people sat on the fountain.",
                "Walt asked the only rude question and it was a very good one. Nia sat in the front row holding the boot like a trophy.",
                "The university has 'paused my contract'. The city paper has asked me to write a column. I think I have just traded forty students for forty thousand readers.")),
            B(9, "In a drawer",
                "The files are in your keeping now, and the book is in a drawer. I teach my classes. I smile at the dean.",
                "Victor Sterling sent me flowers yesterday, with a card: 'For your balanced work.' I don't know how he knew there was any work to balance.",
                "I took the flowers to Ada. She said they were grown in a greenhouse with no soil, and put them in the bin."),
            Gift(Flags(Happy(A(10, "The plaque",
                "Come to the plaza tomorrow morning. Lucia got permission from city hall. A plaque, by the fountain.",
                "'Here the valley was fields, and the river ran clean. Remember it, so it can be again.'",
                "Yuki wants the boot for the museum. I said only if it has its own glass case. She said that was the plan all along.")),
                "valley_plaque"), 400),
            Gift(Flags(Happy(B(10, "The right time",
                "Lucia came to see me. City hall is building a case about the river. She asked if anybody had anything old. Anything at all.",
                "I told her the farmer has a box of files that is not in a barn. I hope it is not in a barn.",
                "So the history gets told after all. Not by me, in a book, but in a courtroom, by the river itself. That might be better. It is certainly louder.")),
                "river_case_evidence"), 400)));

        // ------------------------------------------------------------ NIA (age 9)
        all.Add(Story("nia", "Gregory's River", "The science fair", "The cup final",
            "Nia wants to be a bug scientist, or a goalkeeper, or both. She is the first to notice the river is making the bugs sick. " +
            "Fork: the science fair and the football cup final are on the same day. Neither is wrong; the other ending finds its way back.",
            P(1, "How many legs?",
                "Did you bring a bug? You didn't bring a bug. That's okay. Next time.",
                "Do you know beetles are the most kinds of animal there are? More kinds than anything! If you picked an animal at random, it would probably be a beetle. I want to know all of them."),
            P(2, "Gregory",
                "This is Gregory. In the jar. Only for one minute, then he goes back. Mr. Reyes says one minute is the rule.",
                "Gregory has green wings and a bad attitude. He's my best friend that isn't a person.",
                "...Okay, one minute. Bye, Gregory. Be good."),
            Gift(Ask(P(3, "Bug shells",
                "You go in the mine, right? Dad says there are bugs down there as big as a dog. Is that TRUE?",
                "When they get beaten, they leave bits of shell behind. Chitin! That's what beetle shells are made of. Can you bring me some? Three bits?",
                "It's for science. And for my shelf."),
                "Chitin", 3, 0, "Chitin for Nia", "Nia wants three Chitin Shards from the bugs in the mine, for science (and her shelf).",
                L("Did you get the chitin? Three bits! From the BIG bugs! Is it gross? Tell me it's gross."),
                L("WOW. It's so gross. It's so cool.", "This one's got a scratch. Did it fight you? I'm going to label them. Here, I made you something.")),
                0, "Food_ice_cream_bar", 1),
            P(4, "The grey beetle",
                "I found a beetle by the river. Near your farm. It wasn't moving right.",
                "It was grey all over, like ash. Beetles from there are supposed to be green and shiny, like Gregory.",
                "I put it on a leaf in the shade. I don't think it's going to be okay. Is something wrong with the river?"),
            P(5, "A real project",
                "I'm doing a science fair project! A REAL one. Bea from the university is helping.",
                "We're counting bugs at three places: the park, the football field, and the river by your farm. Then we compare.",
                "Bea says a good scientist writes down what she sees, even when she doesn't like it. I don't like what I'm seeing."),
            P(6, "Fewer every week",
                "Look at my chart. The park: lots of bugs. The football field: lots. The river: fewer every week. And the ones there are grey.",
                "Bea says that's called a 'trend'. Dad says it's called 'bad news'. Mum says it's called 'evidence' and gave me a hug that was too long.",
                "I'm nine. I don't want to have evidence. I want to have bugs."),
            P(7, "Same day",
                "There's a problem. A HUGE problem. The science fair is on a Saturday. The football cup final is on the same Saturday.",
                "Coach Lucia says I'm the best keeper the team has, even better than Ji-ho. Everybody's better than Ji-ho, but still.",
                "Mum says pick the science fair. Dad says pick the final. Then they both said 'it's your choice, sweetheart', which is the worst thing a grown-up can say."),
            Fork(P(8, "What would you do?",
                "You're a grown-up but you don't count, because you're a farmer. So you can tell me.",
                "The fair: Bea says my chart could make the city newspaper. People might finally look at the river.",
                "The final: the team needs a keeper, and it's against the city team in the Sterling shirts, and I really, REALLY want to beat them.",
                "Which one?"),
                "The science fair",
                L("The fair. Yeah. For the beetles. For Gregory's cousins.", "I'm going to make the BEST poster. With glitter. Science can have glitter."),
                L("nia_science_fair"),
                "The cup final",
                L("The final! YES! I'm going to save EVERYTHING.", "Bea says she'll take my chart to the fair for me anyway. So I get both! Kind of! Half of both!"),
                L("nia_cup_final")),
            Happy(A(9, "First prize",
                "I WON. First prize! A gold ribbon and a book about beetles that weighs more than Gregory's whole house.",
                "And a man from the city newspaper took my picture. Tomorrow it says: '9-year-old finds poison in river.'",
                "Dad bought ten copies. Mum read it out loud three times. And the team won the final without me, five-four, so nobody's even sad.")),
            Happy(B(9, "The penalty",
                "Did you SEE? Last minute, penalty, the big city kid in the Sterling shirt, and I SAVED IT.",
                "Dev picked me up and ran round the whole field. Dad cried. He says he didn't. He did.",
                "And Bea took my chart to the science fair, and it got a special mention, and the newspaper wrote about the river anyway. Half of both was the best of both!")),
            Gift(Flags(Happy(A(10, "A beetle with your name",
                "Bea showed my chart to her professor, and her professor found a beetle at the river that nobody has ever written down before. A new kind!",
                "When you find a new kind, you get to name it. The professor said I could choose.",
                "So it's named after you. Because you were the first one I told about the grey beetle, and you didn't laugh. Here. It's a picture. You can put it on your fridge.")),
                "nia_beetle_named"), 0, "Letter", 1),
            Gift(Flags(Happy(B(10, "A beetle with your name",
                "Coach Lucia gave me a medal. And Bea gave me a jar. Inside was a beetle from the river. Green. Shiny. Not grey.",
                "She says the professor found a new kind there, because of my chart, and I get to name it. So I named it after you, because you helped me choose and you were right.",
                "Gregory's met it. They don't get along. That's okay. Here: a drawing of both of them, being rude to each other.")),
                "nia_beetle_named"), 0, "Letter", 1)));

        // ============================================================ THE OLD PARTNERSHIP: Walt (House 2), Gus (House 10), Kenji (House 5)
        // Walt and Gus were "Delgado & Petrakis Builders" until Gus took a Sterling contract thirty years ago. Kenji is the quiet referee.
        // Linked: Walt's Part 7 needs Gus's Part 6, Gus's Part 7 needs Walt's Part 6, and Gus's Part 5 needs Frank's Part 5.

        // ------------------------------------------------------------ WALT DELGADO
        all.Add(Story("walt", "Delgado & Petrakis", "Build it with Gus", "Finish it alone",
            "Walt is proud, grumpy and his knee is failing. He and Gus were partners until Gus built the first tower for Sterling. " +
            "Fork: get the two of them to build the plaza bandstand together, or help Walt finish his secret project alone.",
            P(1, "A strategy",
                "Kenji has beaten me at chess ten days running now. Ten.",
                "I'm lulling him into a false sense of security. Any day now, he'll get careless. Any day. Probably next year."),
            P(2, "Your fence",
                "I walked past your farm. Your fence posts are a disgrace. Leaning like drunks at a wedding.",
                "...I straightened three of them while you weren't looking. Don't thank me. It was offending my eyes, that's all."),
            Gift(Ask(P(3, "Something in the shed",
                "I'm building something. In the shed. Don't ask what.",
                "I need good wood, and Frank's timber is too wet, whatever he says. You've got trees out there. Fifteen pieces. Straight ones, mind."),
                "Wood", 15, 0, "Wood for Walt's shed", "Walt wants 15 pieces of good Wood for something he's building in his shed. He won't say what.",
                L("The wood? Fifteen. Straight. If it's bent, take it to Frank, he'll sell it to somebody."),
                L("Hm. Not bad. Not bad at all.", "Here. Fair price. Now don't go telling Rosa there's anything in the shed.")),
                180),
            P(4, "The knee",
                "Don't fuss. I'm sitting in the flowerbed because I want to. It's a perfectly good flowerbed.",
                "...All right. The knee went. Halfway through the weeding. Just folded, like a cheap ladder.",
                "Priya Nair came by and poked at it and said words I didn't like. 'Specialist.' 'Rest.' 'At your age.' I'm not at my age. I've never been at my age."),
            P(5, "Rosa's worried",
                "Rosa's been talking to you about the doctor, hasn't she. I can tell. You've got her face on.",
                "I'll go when I'm ready. A man spends forty years climbing ladders, he knows what his knees can take.",
                "Mine can take about one more ladder. I've been saving it for something."),
            P(6, "The photo",
                "Here. Look at this. Don't get fingerprints on it.",
                "Two young idiots in front of a truck. 'Delgado & Petrakis, Builders.' That's me on the left, with hair. That's Gus on the right, with more hair.",
                "We built half the porches on this street together. Then we didn't. That's all you need to know about that."),
            When(P(7, "The first tower",
                "Gus told you his side. I can see it. So you might as well have mine.",
                "Thirty years ago a man named Sterling wanted a building in the city. Big contract. More money than our whole company was worth. I said no: I'd heard what his family did to the river.",
                "Gus said yes. He went and built it without me. The first Sterling tower. Thirty years I've been looking at it from my porch.",
                "...He never told me why. And I never asked. Stubborn, the pair of us."),
                AfterFlag("story_gus_6")),
            Fork(P(8, "The last ladder",
                "Lucia's got permission for a bandstand in the plaza. For Tariq, the market, the street dinners. City hall wants a builder.",
                "I could build it. One last job. One last ladder. But I can't do the roof. Not with this knee. I'd need somebody who knows roofs.",
                "Gus knows roofs. Gus is the best roofer I ever met.",
                "Or I finish what's in the shed, quiet, on my own, and leave the bandstand to the young ones. Well? Don't just stand there."),
                "Build it with Gus",
                L("Hm. Fine. FINE. I'll go round there. I'll knock. If he slams the door, it's your fault.", "...If he doesn't slam the door, it's also your fault. Somehow."),
                L("bandstand_planned"),
                "Finish it alone",
                L("Alone, then. Like always. It's what I'm good at.", "Come round on Sunday. You can see what's in the shed. You've earned that much."),
                L("walt_shed_project")),
            Happy(A(9, "Two old idiots on a roof",
                "Three weeks. Three weeks of that man telling me my joints were wrong while I told him his tiles were crooked.",
                "Kenji sat on a bench the whole time and said nothing, which is the most annoying thing anyone has ever done to me.",
                "It's finished. Best thing on the street. Gus did the roof. I did everything else. Mostly. Don't tell him I said 'mostly'.")),
            Happy(B(9, "What's in the shed",
                "Well. There it is. Go on, look.",
                "A cradle. Oak. No nails, just joints. My father made one like this for me.",
                "Tomas, our boy, doesn't come home. He's busy. Too busy for grandchildren, maybe. But if there's ever one, there's a cradle waiting. And if there isn't... it's a good cradle anyway.")),
            Gift(Happy(A(10, "Checkmate",
                "Checkmate. CHECKMATE. Did you hear me, Kenji? Checkmate!",
                "Eleven years. Eleven years, and I've finally done it. I'm getting it carved on the bench.",
                "Kenji says he was distracted by the bandstand roof shining. I say he's a sore loser. Gus says we're both senile. Best day I've had in thirty years.")),
                0, "Wood", 10),
            Gift(Happy(B(10, "A good move",
                "Kenji had me beat. Third move from the end, I could see it coming. Then he moved his bishop to the wrong square.",
                "He's never made a wrong move in eleven years. He looked at the cradle in my shed last week, and now he's making wrong moves.",
                "I won. I'm not getting it carved on the bench. Some wins you keep quiet. ...And I've been round to see Gus. Don't make a face. We had tea. That's all.")),
                0, "Wood", 10)));

        // ------------------------------------------------------------ GUS PETRAKIS
        all.Add(Story("gus", "Eleni's Song", "Say it to his face", "Fix his fence instead",
            "Gus heckles everyone because he's lonely. He's a widower; Tariq's songs remind him of his late wife Eleni. " +
            "He built the first Sterling tower to pay her hospital bills, which ended his partnership with Walt. Fork: how he makes it up to Walt.",
            P(1, "How I would have built it",
                "You want advice? Free advice? Your barn door. It sticks, I can hear it from here.",
                "Plane a millimetre off the bottom, and put the hinge pins in with a bit of candle wax. I'd do it myself. I'm retired. I just like telling people."),
            P(2, "Your house",
                "I had a look at your farmhouse from the road. Not bad. Somebody built it properly, once.",
                "The roof's the important thing. A roof is a promise. You make it once, and then it has to keep the rain off for fifty years without anybody saying thank you."),
            P(3, "Too modern",
                "That boy with the guitar. Tariq. Same four songs. Too modern.",
                "...The third one. The slow one. Where does he get a song like that? It goes up at the end, like the old island songs.",
                "Don't tell him I asked. Don't tell him I know the words, either."),
            When(P(4, "Whose roof",
                "Rain! Good. Now we find out whose roofs I did and whose somebody else did.",
                "Frank's store is leaking. Over the office. Right over that ridiculous cot. I put that roof on, thirty-one years ago, so it's MY leak.",
                "I'll be up there tomorrow with a bucket of tar. Don't tell Frank. He'll want to pay me, and then we'll have to argue about it."),
                new NpcCondition { weather = WeatherMask.Rain | WeatherMask.Storm }),
            When(P(5, "The stubborn old fool",
                "Two years. Two YEARS that idiot has been sleeping on a cot in his own office, and he didn't tell me.",
                "Forty years we've been friends. I've told him everything. He sold his house and he told me 'just a few late nights'.",
                "I shouted at him. First time ever. I'm not sorry. ...I'm a bit sorry."),
                AfterFlag("story_frank_5")),
            P(6, "Eleni",
                "My wife's name was Eleni. She sang. All day, in the kitchen, in the garden, at the top of her voice. Terrible voice. Wonderful songs.",
                "She's been gone nine years. The house got very quiet.",
                "That boy's slow song goes up at the end just like hers used to. That's why I sit through his whole set. Not for the four songs. For one line."),
            When(P(7, "My side",
                "Walt showed you the photo. Delgado & Petrakis. So now you'll want to know why I built that tower.",
                "Eleni was sick, the first time. The hospital in the city wanted money we didn't have. Sterling's contract paid for all of it.",
                "Walt said no on principle. I couldn't afford principles. I should have told him why. I was ashamed. Then it was too late to say it, so I said nothing for thirty years.",
                "She got better, that time. She had twenty more years. The tower bought her twenty years. I'd build it again."),
                AfterFlag("story_walt_6")),
            Fork(P(8, "How do I say it?",
                "Everybody says I should talk to Walt. Kenji says it. Frank says it. Eleni would have said it, loudly.",
                "I could go round there and say it to his face. All of it. The hospital, the money, thirty years of being too proud.",
                "Or... his fence. His fence has been crooked for twenty years and I've told him every single afternoon. I could just go and fix it. He'd know what it meant.",
                "Words or a fence. Which?"),
                "Say it to his face",
                L("Words, then. Out loud. Like a grown man.", "I'll go tomorrow. If I'm not back by lunch, he's hit me with a level."),
                L("gus_walt_talked"),
                "Fix his fence instead",
                L("The fence. Yes. He'll understand the fence.", "Twenty posts. Straight as a rule. And I won't say a single word while I do it. That'll be the hardest part."),
                L("gus_fixed_fence")),
            A(9, "Tea on the porch",
                "I said it. All of it. On his porch, with Rosa listening through the window and pretending to dust.",
                "He didn't say anything for a long time. Then he said, 'You should have told me, you old goat. I'd have lent you the money.'",
                "We didn't have the money, either of us. That's the stupid part. We sat there and drank tea until it was dark."),
            Happy(B(9, "Twenty straight posts",
                "Did it. Twenty posts. Started at six, finished at four, didn't say one word.",
                "Walt came out at about noon with two coffees and sat on his step and watched. Didn't say one word either.",
                "When I'd finished he got up, walked down the whole fence, and pushed on every post. Then he said, 'Adequate.' From Walt, that's a hug.")),
            Gift(Happy(A(10, "The song",
                "Come to the plaza tonight. Tariq's asked me to sing. Me! With my voice!",
                "Eleni's song. The one his slow song sounds like. I taught it to him last week. He plays it better than she sang it, and she'd have said so herself.",
                "Walt and Frank will be in the front row. Walt says he's only coming to heckle. Bring a handkerchief, farmer. For Walt.")),
                250),
            Gift(Happy(B(10, "The song",
                "Tariq's going to play tonight, in the plaza, and I'm going to sing. Don't laugh.",
                "Eleni's song. I taught it to him. Walt says he's coming to make sure I don't embarrass the street.",
                "He brought me a present this morning. A level. Old. Ours, from the truck: 'D & P' scratched on the side. He said, 'You'll need this if you're going to keep fixing my fence.' Front row tonight, farmer.")),
                250)));

        // ------------------------------------------------------------ KENJI TANAKA
        all.Add(Story("kenji", "The Crow and the Board", "Go home to Japan", "Tell Yuki first",
            "Kenji is gentle and wise, and has been greeting the same crow every morning without reply. He is quietly planning to go home to Japan, " +
            "and hasn't told his daughter Yuki. Fork: encourage him to go, or to tell Yuki and decide together.",
            P(1, "Sit or stand",
                "You came back. Sit, then. The bench is warm. Walt has been sitting on it all morning, losing.",
                "He thinks three moves ahead. I think about lunch. You would be surprised how often lunch wins."),
            P(2, "The crow",
                "Every morning I say good morning to the crow on the football field. Every morning, for two years.",
                "He has never said good morning back. But he is always there. That is a kind of answer.",
                "Some friendships are very slow. That is not the same as being nothing."),
            Gift(P(3, "One movement",
                "Stand with me. Feet like this. Knees soft. Breathe in while the hands rise.",
                "No, slower. Slower than that. Slower than you think a person can move.",
                "Good. That is the whole secret. The rest is only practice. Here, have some tea. It is also slow."),
                0, "Food_barley_tea", 2),
            P(4, "A forgotten opening",
                "Something odd happened. I sat down to play Walt and could not remember my opening. Forty years, the same opening.",
                "I laughed, and played another. I still won. Walt was very angry about that.",
                "Yuki did not laugh when I told her. She looked at me the way I used to look at her when she had a fever. I am not ill. I am only seventy-eight."),
            Ask(P(5, "A gift for the crow",
                "I would like to give the crow a present. Not bread. Walt says crows are clever, and clever creatures are insulted by bread.",
                "Corn, I think. Real corn, from a real farm. Three cobs. If he takes them, perhaps we will finally be introduced."),
                "Corn", 3, 0, "Corn for the crow", "Kenji wants three cobs of Corn to give to the crow on the football field.",
                L("Corn for the crow? Three, if you have them. He is waiting. He has been waiting two years; he can wait a little more."),
                L("Beautiful. He will be very pleased, or he will pretend not to be. Either is fine.", "I will leave them on the bench tomorrow at dawn. Thank you.")),
            P(6, "My brother's letters",
                "The crow took the corn. All three. He did not say thank you. But he sat on the bench afterwards, next to me, for one whole minute.",
                "My brother writes from Japan every week. Emeka brings the letters. Our old house is by the sea. My brother is old too, and alone there now.",
                "I have been thinking that I would like to see the sea again. And him. Before we are both too slow to sit together."),
            P(7, "A letter I cannot write",
                "I have tried to tell Yuki. Three times. Each time I open my mouth, and she says something about the museum, and I say something about lunch.",
                "I am trying to write it instead. Look: one page, and every line crossed out.",
                "My calligraphy is very good. My courage is not. It is strange to be so old and still find some things difficult."),
            Fork(P(8, "The slow river",
                "I have a ticket. For the spring. I have not told anyone. Now I have told you.",
                "Walt will be angry. Gus will be rude. Yuki will be... I do not know what Yuki will be. That is what frightens me.",
                "Should I simply go, and write to her from the boat, where it is easier? Or tell her first, face to face, and let her decide some of it with me?"),
                "Go home",
                L("Then I will go. The slow river reaches the sea too.", "I will tell her this week. Before I lose my nerve. And then I will tell Walt, who will need a chair."),
                L("kenji_going_home"),
                "Tell Yuki first",
                L("Face to face. Yes. You are right, though I wish you were not.", "Tonight, at dinner. I will make her favourite. That way, if it goes badly, at least the food will be good."),
                L("kenji_told_yuki")),
            A(9, "The farewell",
                "The street gave me a party in the plaza. Rosa made a cake shaped like a chessboard. Mi-sook made enough food for the whole of Japan.",
                "Walt cried. He says it was the pollen. Gus said, 'You'll be back,' and then he hugged me so hard my glasses came off.",
                "Yuki has promised to visit every summer. The crow was on the roof of the general store the whole evening. Watching. I think he knows."),
            Happy(B(9, "Yuki's answer",
                "I told her. She was quiet for a long time. Then she went into her room and came back with a folder.",
                "Inside were museum papers. Leave of absence, a whole season, already signed. She had been planning to ask ME if we could go to Japan together, to see her uncle.",
                "Two people, each too frightened to say the same thing. We laughed until the tea went cold. We will go together, in spring, and come back together.")),
            Gift(Happy(A(10, "Good morning",
                "A postcard came for you this morning. From Japan. Emeka brought it himself, very carefully, as if it were made of glass.",
                "'The sea is the same. My brother is slower than me at chess, which is wonderful. Please say good morning to the crow for me.'",
                "...And when you went to the field and said it, the crow said it back. Did you hear? 'Kraa.' That is good morning, in crow. He was waiting for the right person.")),
                0, "Food_barley_tea", 3),
            Gift(Happy(B(10, "Good morning",
                "We leave in spring, and we come back in autumn. Walt has made me promise to bring him a Japanese chess set so he can lose in two languages.",
                "And this morning, on the field, I said good morning to the crow as always.",
                "He looked at me. He hopped closer. And he said 'Kraa.' Two years, and a bag of your corn, and he said good morning. You see? The slow river reaches the sea.")),
                0, "Food_barley_tea", 3)));

        // ============================================================ THE NAIRS (House 3)
        // Linked: Priya's Part 4 needs Walt's Part 4 (his knee), and Dev's Part 7 needs Priya's Part 6 (the late birthday card).

        // ------------------------------------------------------------ PRIYA NAIR (nurse)
        all.Add(Story("priya", "Shoes That Don't Hurt", "Stay at the hospital", "Take the clinic job",
            "Priya is an exhausted nurse and single mum. She sees more and more patients sick from the river, and a shiny Sterling clinic offers her double pay " +
            "with a clause that forbids talking to the press. Fork: stay at the hospital and hand over her (anonymous) patient records, or take the clinic job.",
            P(1, "Before the duct tape",
                "I meant it, you know. If you cut yourself on a scythe, you come to me. Not to Frank. Frank's answer to everything is duct tape.",
                "Last month he taped up his own thumb and kept working. I had to take it off with olive oil and a lecture."),
            Gift(Ask(P(2, "My grandmother's tea",
                "Can I ask a favour? My grandmother used to make a tea with apples and spice when anyone was run down. I'm run down.",
                "The apples at the tower taste of nothing. Three from a real tree would be wonderful. Only if you have them spare."),
                "Apple", 3, 0, "Apples for Priya's tea", "Priya wants three apples for her grandmother's tea. She says she's run down.",
                L("Apples, if you've got any spare? Three. No rush. Well. A little rush. I'm very tired."),
                L("Oh, they smell like her kitchen.", "Thank you. Here, for your trouble. Don't argue, I'm a nurse, I win arguments professionally.")),
                120),
            P(3, "Asleep on the bench",
                "Did I fall asleep on the plaza bench yesterday? I did, didn't I. In the middle of the afternoon, with my shopping.",
                "Dev says you made sure he got dinner. Pasta, he says. Again.",
                "Thank you. I'm not used to people looking after us. I'm usually the one doing the looking."),
            When(P(4, "The worst patient",
                "I've just been to see Walt Delgado about that knee.",
                "He's the worst patient I've ever had, and I once treated a man who'd glued himself to a ladder. Walt argued with every word I said. Then he did everything I told him.",
                "Rosa walked me to the gate and cried a bit. That's the part of the job nobody trains you for."),
                AfterFlag("story_walt_4")),
            P(5, "From the river side",
                "Something's wrong in the city. More patients every week from the streets by the river. Rashes. Coughs that won't clear. Children, mostly.",
                "Nobody upstairs wants to say what everybody downstairs is thinking.",
                "I've started writing it all down. Dates, streets, symptoms. No names. Just in case anyone ever asks."),
            P(6, "A card, a month late",
                "Dev's father sent a birthday card. Dev's birthday was five weeks ago.",
                "Inside it said 'Sorry, buddy, crazy month'. Every month is a crazy month with him. Sixteen years of crazy months.",
                "I was furious for about an hour. Then I was just tired. Dev put the card in a drawer and asked what's for dinner. He's better at this than I am."),
            P(7, "The clinic",
                "A Sterling clinic is opening in the city. Glass, plants, coffee machines that work. They want me as head nurse.",
                "Double the pay. Regular hours. I'd see Dev's practice. I'd sleep.",
                "And a contract with a clause in it: staff may not 'discuss patient trends with outside parties, including the press'. They sent the clause in bold."),
            Fork(P(8, "Which kind of tired?",
                "I have to give the clinic an answer by Friday.",
                "If I stay at the hospital, I'm tired and broke and I keep my notebook. Lucia says city hall needs exactly that kind of record for the river case. No names, just patterns.",
                "If I take the clinic, I'm rested, and Dev gets new boots, and I never say a word about the river again.",
                "I know what my grandmother would say. I want to hear what you say."),
                "Stay, and give city hall the records",
                L("Stay. Yes. I'll be tired, but I'll be able to look at myself in the mirror.", "I'll copy the notebook tonight. Anonymous. Every street, every rash, every cough. Lucia can have it on Monday."),
                L("priya_stayed", "tower_file_patients"),
                "Take the clinic job",
                L("The clinic. Sleep. Boots that fit. I'm allowed to want that, aren't I?", "I'll keep the notebook in the drawer. With the birthday card."),
                L("priya_clinic")),
            A(9, "Tired and proud",
                "City hall has my records. Lucia called them 'the clearest thing on my desk'. The hospital board heard about it and gave me a pay rise. A small one. Very small. Still.",
                "I worked a double shift yesterday and came home to Dev asleep on the sofa with a pan of pasta for me on the stove.",
                "I'm tired. I'm always going to be tired. But it's a good tired now."),
            B(9, "Glass and plants",
                "The clinic's lovely. I've slept eight hours four nights running. I've watched three of Dev's practices.",
                "Today a little girl came in from the river streets with the same rash I've seen a hundred times. My manager told me to write 'allergy' on the form.",
                "I wrote 'allergy'. I went into the staff toilet and sat there for ten minutes. Then I went back to work."),
            Gift(Flags(Happy(A(10, "Community health day",
                "Saturday, the plaza: a Community Health Day! Free check-ups, blood pressure, and a stall for the river kids' rashes. The hospital lent us everything.",
                "And the tea stall is my grandmother's apple tea, made with apples from your farm. Dev is cooking pasta for everybody. It's always pasta. Nobody complains.",
                "Come by. Bring Walt. Tell him it's a party, not a check-up. He'll never know.")),
                "health_day"), 300),
            Gift(Flags(Happy(B(10, "Community health day",
                "I quit. I walked into my manager's office with the notebook and put it on the desk and said 'that isn't an allergy'.",
                "Then I took it to Lucia. The hospital took me back the same afternoon.",
                "And Saturday we're doing a free Health Day in the plaza, for the river kids. Grandmother's apple tea, your apples. Dev on pasta. Come by.")),
                "health_day", "tower_file_patients"), 300)));

        // ------------------------------------------------------------ DEV NAIR (16)
        all.Add(Story("dev", "The Best Left Foot", "Stay with the team", "Join the academy",
            "Dev is sixteen, cooks pasta on Thursdays, and has the best left foot Lucia has coached. A Sterling-sponsored academy wants him, " +
            "but he'd have to leave his mum alone. Fork: stay and play the Suburb Cup, or take the academy place.",
            P(1, "We need a keeper",
                "Oh. Hey. You're back. Do you play in goal yet? We still need a keeper. Ji-ho's still terrible.",
                "No pressure. Except, like, a lot of pressure. We play the city team next month."),
            P(2, "The slush debate",
                "Okay, settle this. Blue slush or red slush?",
                "Wrong. Wait, you didn't say anything yet. Whatever you were going to say, if it wasn't blue, it was wrong.",
                "Red slush is for people who've given up on life. Blue slush is for people with hope. Look it up."),
            Ask(P(3, "Actually good pasta",
                "Thursdays I cook, because Mum's on late shifts. It's always pasta. Nobody complains, but nobody says 'wow' either.",
                "I want somebody to say 'wow'. Real tomatoes, not tins. Could you get me some? Four."),
                "Tomato", 4, 0, "Tomatoes for pasta night", "Dev wants four real tomatoes so his Thursday pasta is 'actually good' for once.",
                L("Got the tomatoes? Four. I'm doing a sauce from a video. The guy in the video has a moustache. You can trust him."),
                L("These are huge. These are so red.", "Mum's going to say 'wow'. I'm going to make her say it twice.")),
            Happy(P(4, "She said wow",
                "She said WOW. Twice! And then she asked for the recipe, and then she fell asleep at the table. But still. Twice.",
                "Practice was good too. I scored four. Coach Lucia made me run laps anyway, for 'celebrating like a seagull'. That's not a rule.")),
            P(5, "The man on the touchline",
                "There was a man at practice today. On the touchline, with a clipboard and a Sterling jacket. He didn't watch anybody else. Just me.",
                "Coach says he's a scout. From the academy in the city. The proper one, with the big pitches and the TV cameras.",
                "I played like a donkey after that. My legs forgot how legs work."),
            P(6, "The offer",
                "The academy wants me. Boarding school, in the city. Training every day, all paid for. Sterling Sports Academy.",
                "If I'm good enough there, I could play properly. Like, for a living.",
                "I'd only be home on Sundays. Mum would be on her own all week. She's already on her own most of the week. I'm the one who does the dishes."),
            When(P(7, "I haven't told her",
                "I haven't told Mum. About the academy.",
                "After Dad's card, she sat in the kitchen for ages. I'm not adding to that. She'd tell me to go. She'd tell me to go and then sit in the kitchen on her own every night.",
                "Coach Lucia knows. She said, 'Your mum is tougher than both of us, Dev.' Which is true. But that's not the point."),
                AfterFlag("story_priya_6")),
            Fork(P(8, "Stay or go",
                "I have to tell the academy by Saturday. That's the day after the Suburb Cup draw.",
                "If I stay, I play the Cup with the street. Ji-ho, Nia, everybody. Against the city team. And Mum's not alone.",
                "If I go, I might actually make it. Proper football. I'd send money home. I'd have a Sterling badge on my shirt, though. Every day.",
                "You don't have to be nice about it. Just tell me what you'd do."),
                "Stay with the team",
                L("Stay. Yeah. I think I needed somebody to say it so I could stop arguing with myself.", "I'm telling Mum tonight. All of it. And then I'm telling Coach we're going to win the Cup."),
                L("dev_stayed"),
                "Join the academy",
                L("Go. Okay. Okay. I'm going.", "I'm telling Mum tonight. She's going to cry and then pack my bag before I've finished talking. I know her."),
                L("dev_academy")),
            Happy(A(9, "The Cup final",
                "We WON. Three-two, against the Sterling shirts. Nia saved a penalty, Ji-ho dived the right way for once, and I scored the winner with my left. My LEFT.",
                "Mum was on the touchline. She'd swapped three shifts to be there. She screamed so loud the crow flew off Kenji's bench.",
                "The academy scout came over after. He said the offer's still open next year. I said maybe. Maybe means we'll see.")),
            B(9, "The Sterling tracksuit",
                "The academy's amazing. Pitches like carpet. Coaches who know everything. A gym with machines I don't even know the names of.",
                "Every Sunday I come home in the Sterling tracksuit. I take it off before I get to the street. I don't really know why.",
                "Mum's okay. She says she's okay. Mrs. Delgado brings her soup now. I think that's a good sign and a bad sign."),
            Gift(Happy(A(10, "Thursday pasta",
                "Thursday pasta night has got out of hand. Mum invited Coach. Coach brought Mateo. Mateo brought Ji-ho. Ji-ho brought Nia, who brought a beetle.",
                "Eleven people in our kitchen. I made the sauce with your tomatoes. Everybody said wow. Not twice. Eleven times.",
                "Thanks for telling me to stay. Here. Blue slush. It's for people with hope.")),
                0, "Food_slush_blue", 3),
            Gift(Happy(B(10, "An accidental pass",
                "The academy played the suburb team in a friendly. Me in the Sterling shirt against everybody I grew up with. Worst day of my life. Best game, though.",
                "Last minute, I had the ball, and I passed it. To Ji-ho. By accident. Totally by accident. He scored. The suburb won.",
                "Coach at the academy shouted for ten minutes. Mum laughed for an hour. I'm coming home after this season. Here. Blue slush. You'll need hope too.")),
                0, "Food_slush_blue", 3)));

        // ============================================================ THE ROOMMATES (House 4)
        // Linked: Bea's Part 4 needs Soo-ah's Part 5 (three hours at the seed rack), Bea's Part 5 needs Nia's Part 5 (the science project),
        // and Tariq's Part 5 needs Bea's Part 3 (the labels).

        // ------------------------------------------------------------ BEA LINDQVIST (botany student)
        all.Add(Story("bea", "Forty Questions", "Publish the results", "Keep the grant",
            "Bea studies botany and thinks farmers are heroes. Her thesis on your soil turns into proof of the pollution, and her supervisor's grant comes from Sterling. " +
            "Fork: publish the true results and lose the grant, or keep the grant and pass the data on quietly.",
            P(1, "The list",
                "Okay. I've cut it down. It was forty questions. Now it's thirty-eight. Two of them were the same question in different words.",
                "Question one: when you put your hand in your soil, what does it feel like? No, really. Like a sponge? Like cake? Be specific."),
            P(2, "A little jar of dirt",
                "Could I take a soil sample from your farm? A tiny one. A jar. A small jar. With a label.",
                "Every farm's soil is different, like a fingerprint. Yours is older than the whole suburb. It's probably full of stories. And worms. Mostly worms."),
            Gift(Ask(P(3, "Roots",
                "My thesis is about roots. How they find water, how they share food, how they decide where to go. Plants make decisions! Slowly, but they do.",
                "Could you bring me some potatoes from your farm? Three. I want to look at the skins under the microscope. I promise I'll eat them afterwards."),
                "Potato", 3, 0, "Potatoes for the microscope", "Bea wants three potatoes from your farm to study under her microscope for her thesis.",
                L("Any potatoes? Three, please. I've already labelled the slides. 'Farm potato one', 'farm potato two', 'farm potato three'. I'm very organised."),
                L("Oh, look at the eyes on this one! That's where the new shoots come from. They're called eyes. Isn't that lovely?", "Here. A thank-you. Lab rules: always pay your farmer.")),
                100),
            When(P(4, "The seed rack",
                "I went to the general store on Tuesday to buy one packet of seeds.",
                "I came home three hours later with one packet of seeds. Soo-ah knows the germination rate of every packet on that rack. By heart.",
                "We're meeting again on Thursday. To talk about seeds. Purely seeds. Why are you smiling like that?"),
                AfterFlag("story_daughter_5")),
            When(P(5, "Nia's project",
                "I'm helping Nia Abara with her science fair project! She's counting bugs in three places and writing everything down in a notebook with a beetle on the cover.",
                "She's a better scientist than half my class. She doesn't decide what she wants to find before she looks. That's the whole trick, and grown-ups forget it.",
                "Her numbers from the river are... not good. I've started checking my own samples again."),
                AfterFlag("story_nia_5")),
            P(6, "The top rows",
                "I ran your soil samples again. The ones from the top rows of your farm, near the river.",
                "Heavy metals. Chrome, mostly. Way above safe levels. Your crops up there are pulling it out of the ground, which is why they look tired.",
                "The samples from the middle of your farm are fine. Better than fine. It's only where the river touches. I'm so sorry. It isn't anything you did."),
            P(7, "Leave them out",
                "I showed my supervisor the results. He went quiet. Then he shut his office door.",
                "He said my thesis is 'about roots, not rivers', and the river samples should be left out because they're 'outside the scope'.",
                "His grant is from Sterling AgriSeed. So is mine. I looked it up on the way home and then I had to sit on a wall for a while."),
            Fork(P(8, "In or out",
                "My thesis is due in a month.",
                "If I put the river results in, my supervisor won't sign it off, the grant stops, and I probably have to start again somewhere else. A year, at least.",
                "If I leave them out, I finish on time. And I give you the data, quietly, so somebody who isn't a student can use it.",
                "A good scientist writes down what she sees. Nia taught me that. But I didn't think it would cost this much."),
                "Publish the true results",
                L("In. All of it. Every sample.", "If they won't sign it, I'll find somebody who will. Data is data. It doesn't care who paid for the microscope."),
                L("bea_published", "tower_file_soil"),
                "Keep the grant, share the data",
                L("Out of the thesis, into your hands. Here: every sample, every number, on paper and on a stick.", "Keep it somewhere dry. Soil scientists are very fussy about dry."),
                L("bea_grant_kept", "tower_file_soil")),
            A(9, "Quoted at city hall",
                "My supervisor wouldn't sign it. So I took it to a professor at another university, and she signed it in five minutes and said 'about time someone did'.",
                "And then Lucia Reyes read pages forty to fifty-two out loud at a city hall meeting. My pages! With my graphs!",
                "No grant, no money, a year behind. I've never been this happy. Is that strange? Tariq says it's strange."),
            B(9, "On time, quietly",
                "I handed my thesis in. On time. About roots, not rivers. My supervisor said it was 'excellent work'.",
                "I felt sick all day. Then I remembered your box of data and felt a bit better.",
                "Soo-ah says you can do the right thing in two steps instead of one. I think she might be right. She usually is."),
            Gift(Flags(Happy(A(10, "The soil lab",
                "Come and see! The Kims gave me the back room behind the store. It's tiny. It's a lab.",
                "Anybody on the street can bring a jar of garden soil and I'll test it for free. Ada brought six jars. Mateo brought twenty. Walt brought one, and stood over me the whole time.",
                "And for you: fertilizer mixed to suit your soil. Use it on the top rows. We're going to help them get better.")),
                "soil_lab_open"), 0, "Fertilizer", 5),
            Gift(Flags(Happy(B(10, "The soil lab",
                "Lucia called. City hall used my data from your box. All of it. Anonymously, but she said the graphs were 'very convincing'.",
                "And with my degree finished, I could open this: a soil lab in the back room of the Kims' store. Free tests for everyone on the street.",
                "Here, the first batch of fertilizer mixed for your farm. For the top rows. Soo-ah helped. Soo-ah helps with everything now.")),
                "soil_lab_open"), 0, "Fertilizer", 5)));

        // ------------------------------------------------------------ TARIQ (busker)
        all.Add(Story("tariq", "Maple Lane", "Keep the song", "Sell the song",
            "Tariq busks in the plaza and wants to record a real album. Sterling Coffee takes over his city café gig, then Sterling's marketing team offers a lot of money " +
            "for his best song as an advert jingle. Fork: keep the song and record it cheaply, or sell it.",
            P(1, "Taste is extra",
                "Hey, hey. My favourite audience member. Well, second favourite. Gus is first. He doesn't know that.",
                "Requests are still free. Taste is still extra. You'd like something slow, I can tell. Farmers always like something slow."),
            P(2, "He knows the words",
                "Gus sat through my whole set again. Arms crossed, face like a thunderstorm. 'Too modern,' he says.",
                "But on the slow song, the third one, his lips move. Every word. I've seen it. Don't tell him I've seen it."),
            Gift(Ask(P(3, "A fish called sunfish",
                "I'm writing a song about the farm, but I'm stuck on the chorus. I need something real to look at.",
                "You fish, right? Is there really a fish called a sunfish? Bring me one. If it's called sunfish, it deserves a song."),
                "Fish_sunfish", 1, 0, "A fish for a song", "Tariq wants a sunfish to inspire the chorus of his song about the farm.",
                L("Any sunfish yet? I've got a whole verse waiting for it. The verse is getting impatient."),
                L("Look at it! It's yellow AND blue! It's showing off!", "That's the chorus. Right there. Thank you. Here, for the fish, and for the chorus.")),
                80),
            Happy(P(4, "The farm song",
                "Sit down. I finished it. 'Maple Lane'. It's about this street, and your farm, and the road between them.",
                "...And the bit at the end goes up, like that. I don't know where that came from. It just felt right.",
                "Gus was walking past and stopped. He didn't say 'too modern'. He didn't say anything. He just stood there.")),
            When(P(5, "Labelled",
                "Bea labelled me. Literally. I woke up on the sofa with a sticky note on my forehead: 'TARIQ. DO NOT WAKE BEFORE 11.'",
                "She's labelled the milk, the shelves, the light switches and the cat next door, who we don't even own.",
                "I love her, she's the best roommate I've ever had, and I'm going to put a label on her lab coat that says 'BEA. HAS FEELINGS ABOUT SEEDS.'"),
                AfterFlag("story_bea_3")),
            P(6, "No more café",
                "My Friday gig in the city's gone. The café was sold. It's a Sterling Coffee now.",
                "The new manager said 'we have a curated playlist'. Curated! It's forty songs on a loop, and they're all about being happy in an office.",
                "Fridays were the only paid gig I had. Now it's the plaza or nothing."),
            P(7, "The jingle",
                "A woman from Sterling's marketing team heard me in the plaza today. She wants 'Maple Lane'. For an advert.",
                "They'd change the words a bit. 'Maple Lane' becomes 'Sterling Lane'. They'd play it in every tower, every shop, every lift.",
                "And they'd pay. A lot. Enough to record a whole album properly, in a real studio, with a drummer who isn't a cardboard box."),
            Fork(P(8, "Sterling Lane",
                "I have to answer by Friday. The day I used to play the café, funnily enough.",
                "If I sell it, I get the money, I make the album, and every time I go into the city I hear my own song telling people to buy flat-pack doors.",
                "If I keep it, I record it myself, on Frank's old tape machine in his storeroom. Cheap. Rough. Mine.",
                "You gave me the sunfish. You're in this song. You get a vote."),
                "Keep the song",
                L("Keep it. Yeah. It's called Maple Lane. It's not called anything else.", "Frank's storeroom it is. Bea says the echo's terrible. Bea says that about every room I'm in."),
                L("tariq_kept_song"),
                "Sell the song",
                L("Sell it. Money for an album. Proper drums. A proper studio.", "I'll just... not go into the tower. Ever. Simple."),
                L("tariq_sold_song")),
            A(9, "Somebody paid",
                "Here's a weird thing. I went to pay Frank for the storeroom and he said it's already paid. A whole month, plus new strings, plus a microphone.",
                "Frank wouldn't say who. He went very red and started counting nails.",
                "There's only one person on this street who complains that loudly about my music and then knows all the words."),
            B(9, "In the lift",
                "I went into the city to buy strings. I took the lift in the tower by mistake.",
                "And there it was. 'Sterling Lane'. My song. My chords. Somebody else singing 'save more, live better' where the farm used to be.",
                "The album is finished, and it sounds amazing. I haven't listened to it once."),
            Gift(Happy(A(10, "The plaza concert",
                "Tonight. The plaza. A concert. The whole album, start to finish, Maple Lane last.",
                "And I've got a guest singer. He doesn't want me to say who. He says if anybody claps, he's leaving.",
                "Bring everybody. Bring the sunfish, if it's still around. It's in the chorus. It deserves to hear itself.")),
                0, "Letter", 1),
            Gift(Flags(Happy(B(10, "The plaza concert",
                "I gave the money back. All of it. Well, most of it. I'd spent some on the drummer.",
                "They can keep 'Sterling Lane', it's theirs now. But I wrote 'Maple Lane' again. New words. Better. It's about what happened.",
                "Tonight, the plaza. The whole street. And a guest singer who said if anybody claps, he's leaving. Everybody's going to clap.")),
                "tariq_kept_song"), 0, "Letter", 1)));

        // ============================================================ THE REYES (House 8)
        // Brother and sister on opposite sides of the same form: city hall plans to sell half the park to Sterling.
        // Linked: Mateo's Part 6 needs Lucia's Part 6, Lucia's Part 6 needs Mateo's Part 5, and Lucia's Part 5 needs Dev's Part 5 (the scout).

        // ------------------------------------------------------------ MATEO REYES (park gardener)
        all.Add(Story("mateo", "Eleven Kinds of Grass", "Save the park", "Take the deal",
            "Mateo looks after the park and worries about every blade of grass. City hall plans to sell half of it to Sterling for a car park, " +
            "and his sister is the one with the forms on her desk. Fork: a 'Save the Park' planting day, or a deal where Sterling keeps half and pays to clean the river.",
            P(1, "You can't hurry a plant",
                "Good to see you. Mind the edge of the pitch there, I re-seeded it on Tuesday.",
                "A farmer understands. People think a park just happens. It doesn't. It's a thousand small worries, every day, and if you do it right, nobody notices any of them."),
            P(2, "Checking each other's work",
                "Ada watered the plaza planters at eight. I checked them at ten. She checked my hedges at eleven.",
                "Neither of us has said a word about it for six years. It's the best working relationship I've ever had."),
            Gift(Ask(P(3, "Oaks for the far corner",
                "There's a corner of the park where the old oak came down in a storm. It's been bare for three years.",
                "Acorns from a real tree would be perfect. Five. I'll plant them all, and the strongest one gets the corner. It'll be a hundred years before it's finished, but somebody has to start."),
                "Acorn", 5, 0, "Acorns for the park", "Mateo wants five acorns to plant an oak in the bare corner of the park.",
                L("Any acorns? Five is plenty. The corner's waiting. Corners are very patient."),
                L("Lovely. Fat and brown. These will do very well.", "Here. For your trouble. Come back in a hundred years and sit under it.")),
                120),
            When(P(4, "Blossom week",
                "It's blossom week. The one week a year everybody notices the park.",
                "Rosa brought me a cake. Lena drew the trees. Walt said 'not bad', which from Walt is a medal.",
                "Fifty-one weeks of nobody noticing, one week of everybody. That's a fair deal, I think."),
                new NpcCondition { seasons = SeasonMask.Spring }),
            P(5, "The surveyors",
                "There were men on my football field this morning. In orange vests, with little tripods and a lot of wooden stakes.",
                "The vests said 'Sterling Holdings'. They put a stake right through the penalty spot.",
                "I asked them what it was for. They said 'parking'. Just that. Parking."),
            When(P(6, "My sister's desk",
                "Lucia told me last night. The papers for the park sale are on her desk at city hall. Waiting for her stamp.",
                "Half the park. The football field, the bench where Kenji plays chess, the playground. All of it, to the tower. For cars.",
                "I didn't say anything. I went out and raked the sandbox in the dark. She came and sat on the swing and didn't say anything either."),
                AfterFlag("story_lucia_6")),
            P(7, "The river trees",
                "Come and look at the trees by the river path. Look at the leaves.",
                "Yellow in summer. The bark's splitting at the bottom. They're drinking the river, and the river's making them sick.",
                "I can fight a car park. I don't know how to fight a river."),
            Fork(P(8, "Half a park",
                "A man from the tower came to see me. Not Lucia. Me. He had a deal.",
                "Sterling keeps half the park for parking. In return they pay to clean the river. A new filter at the plant, new trees on the river path, everything.",
                "Or we fight. Everybody on the street plants a tree in the park on the same day, and we make it impossible to build on. Lucia says city hall can't sell a park full of new trees.",
                "Half a park and a clean river. Or the whole park and a river nobody's fixing yet. I can't choose. Will you?"),
                "Save the park",
                L("The whole park. We plant. Every house, one tree. I'll find the saplings, you bring the strong arms.", "And then we find another way to fix the river. There's always another way. It's just slower."),
                L("save_the_park"),
                "Take the deal",
                L("The deal. Half the park. A clean river.", "I'll sign it. I'll mark the new car park lines myself, so at least they're straight."),
                L("park_deal")),
            Happy(A(9, "Planting day",
                "Did you see? Did you SEE? Two hundred trees in one morning. Nia planted four and named them all.",
                "The surveyors came back on Monday and couldn't find a single spot for a stake. They stood there for an hour and then went away.",
                "Lucia stamped the sale papers 'REJECTED: SITE UNSUITABLE'. In red. She says she's wanted to use the red stamp for years.")),
            B(9, "Car park lines",
                "The car park's on the field side. Grey, flat, straight. I did the lines. They're very straight.",
                "But come and look at the river path. New trees. Green leaves. The filter at the plant went in last week.",
                "The kids play football on the half that's left. It's small. They don't seem to mind as much as I do."),
            Gift(Happy(A(10, "A bench with my name",
                "Somebody put a bench in the far corner. By the little oak from your acorns.",
                "There's a plaque on it. 'For Mateo, who worried about every blade of grass.' I didn't ask for this. I don't like fuss.",
                "...I've sat on it every lunchtime this week. Don't tell anyone. Here, take some acorns. Your little oak's first ones.")),
                200, "Acorn", 5),
            Gift(Happy(B(10, "A bench with my name",
                "Lucia did something. The car park's 'on hold for review'. The tower's paying for the river anyway. It's in the contract.",
                "And somebody put a bench by the river path, under the new trees. 'For Mateo, who worried about every blade of grass.'",
                "I like it there. I can watch the river getting better. Slowly. That's the only speed plants know. Here, for the next oak.")),
                200, "Acorn", 5)));

        // ------------------------------------------------------------ LUCIA REYES (city hall + football coach)
        all.Add(Story("lucia", "Shouting and Oranges", "Run for council", "Lose the paperwork",
            "Lucia has energy for ten people. She wants to do good at city hall, but the system makes her stamp Sterling's forms. Coaching is where she's happiest. " +
            "Fork: run for city council against Sterling's candidate, or stay at her desk and very carefully lose the park-sale paperwork.",
            P(1, "Left back",
                "Hi! You came back! Have you thought about left back? You've got the shoulders for it. Farm shoulders.",
                "We train at four. Bring boots. Or don't, and just shout encouragement. Shouting is half of football."),
            P(2, "A sandwich and a book",
                "My job at city hall, in one sentence: somebody wants a permit, I give them a form, they fill it in wrong, they go home, they come back.",
                "Bring a sandwich. Bring a book. Bring a second sandwich, for me. I mean it."),
            Gift(Ask(P(3, "Half-time",
                "Big news: I've signed you up for the Suburb Cup squad. Reserves. Don't argue, it's already in pen.",
                "Your first job as a reserve: half-time fruit. Coaches are supposed to give out oranges, but nobody grows oranges round here. Six apples. Sliced is a bonus."),
                "Apple", 6, 0, "Half-time apples", "Lucia has signed you up as a reserve for the Suburb Cup squad. Your first job: bring six apples for half-time.",
                L("Got the half-time apples? Six. The kids get very grumpy without fruit. So do I."),
                L("Perfect! Look at the colour!", "You're officially part of the team now. Here, a reserve's wages. Don't spend it all at once.")),
                100),
            P(4, "City hall jobs",
                "You know those odd jobs on the city hall board? I write half of them. The good half.",
                "Clearing drains, fixing benches, cleaning up the river bank. The tower writes the other half, and those are all 'deliver this leaflet about how clean the river is'.",
                "Take mine. Always take mine."),
            When(P(5, "The man with the clipboard",
                "There's been a scout at my practices. Sterling Sports Academy. He only watches Dev.",
                "Dev's the best left foot I've ever coached. He deserves a shot. But an academy with Sterling on every shirt... I don't know.",
                "And he's sixteen, and his mum works four early shifts a week, and he does the dishes. Some things don't show up on a clipboard."),
                AfterFlag("story_dev_5")),
            When(P(6, "The forms on my desk",
                "There's a pile of forms on my desk at city hall. 'Sale of public land: Northfield Park, part.' Our park. Mateo's park.",
                "It's all legal. It's all been approved upstairs. All it needs is my stamp, and then it goes to the next desk, and the next.",
                "I haven't stamped it. It's been on my desk for nine days. My boss has started asking about it very politely."),
                AfterFlag("story_mateo_5")),
            P(7, "Two kids, two choices",
                "Dev's got his academy offer. Nia's got the science fair on the same day as our cup final. Everybody's choosing between two good things.",
                "I keep telling them 'whatever you choose is right'. And then I go home and I can't sleep.",
                "Mateo says I care too much. Mateo talks to grass. We're not a family that does things halfway."),
            Fork(P(8, "Inside or outside",
                "There's an election for city council in the spring. Sterling has a candidate. Smart suit, big smile, posters everywhere.",
                "Nobody's standing against him. Gus says I should. Grace says I should. Marcus says I should and then looks sick.",
                "Or I stay at my desk, where I can do the most good quietly. Things get lost on desks all the time. Forms slip behind filing cabinets. It happens.",
                "You've seen me shout at nine-year-olds about passing. Am I a councillor, or am I a very good desk?"),
                "Run for council",
                L("Council it is! I'm going to need posters. And a slogan. 'Pass the ball, Sterling'? No. Mateo will help.", "Oh no. I'm going to have to do speeches. I'll treat it like a team talk. Shouting and oranges."),
                L("lucia_running"),
                "Lose the paperwork",
                L("The desk. Quietly.", "Do you know how big the gap is behind the filing cabinet on the third floor? I measured it today. It's perfect."),
                L("lucia_lost_forms")),
            A(9, "Campaign days",
                "Campaign week! Rosa's doing bread for the volunteers. Tariq wrote a jingle. It's terrible. Everyone's singing it.",
                "The Sterling candidate came to the plaza to debate me. He talked about growth. I talked about the park, the river and Nia's beetle chart.",
                "Halfway through, a football came over the hedge from practice and hit him on the shoulder. Dev says it was an accident. I believe him. Mostly."),
            B(9, "Behind the cabinet",
                "The park forms are behind the filing cabinet on the third floor. My boss has asked about them three times. I've been very helpful and very sorry.",
                "Meanwhile, every river complaint that comes in, I copy and put in a folder. Marcus's papers, Priya's notes, Bea's graphs, Grace's maps.",
                "It's getting heavy. When it's heavy enough, I'm going to drop it on the right desk."),
            Gift(Flags(Happy(A(10, "Final whistle",
                "What a week. On Saturday we played the cup final on Mateo's pitch. On Tuesday they counted the votes.",
                "I won. Both of them. Well, the team won one, and I won the other, by three hundred and twelve votes.",
                "First thing I'm doing as a councillor: the river. Every paper the street has given you, bring it to me. We're taking Sterling to court. Shouting and oranges, farmer. Shouting and oranges.")),
                "lucia_councillor"), 300),
            Gift(Flags(Happy(B(10, "Final whistle",
                "The cup final was on Saturday, on Mateo's pitch. The whole park, because the forms are still behind the cabinet.",
                "And on Monday I put a folder on the mayor's desk. Two years of river complaints, and everything the street found. It's thick enough to stop a door.",
                "The mayor read it all, and then she sent for the city's lawyers. We're taking Sterling to court. And I'm still a very good desk.")),
                "river_case_opened"), 300)));

        // ============================================================ YUKI (House 5), LENA (House 6), ADA (House 9)
        // Linked: Yuki's Part 4 needs Grace's Part 4 (Sterling & Sons), Yuki's Part 5 needs Kenji's Part 4 (the forgotten opening),
        // Lena's Part 3 needs Nia's Part 2, Lena's Part 5 needs Marcus's Part 5, and Ada's Part 5 needs Emeka's Part 6 (J's letter).

        // ------------------------------------------------------------ YUKI TANAKA (museum curator)
        all.Add(Story("yuki", "The Wetland Room", "The honest exhibition", "The pop-up museum",
            "Yuki is precise and quietly funny. Her new exhibition is about the valley before the city, and the museum's new sponsor, Sterling, wants it rewritten. " +
            "Fork: open it honestly and lose the sponsor, or move it to a free pop-up museum in the suburb.",
            P(1, "First look",
                "Good day. You have not dug up anything interesting yet? Never mind. You will. Old farms always give something back eventually.",
                "When you do, I would like to see it first. Before Grace. Grace will want to write about it. I want to put it in a glass case and light it properly."),
            P(2, "Two degrees",
                "Frank's sign is still crooked. Two degrees to the left. I measured it last Monday with a protractor while pretending to tie my shoe.",
                "I have not said anything. It is the hardest thing I have ever not done."),
            Gift(Ask(P(3, "A living river",
                "My next exhibition is about the river as it used to be. Old maps, old photographs, old fishing nets.",
                "But I want one living thing in the room. An eel, in a tank. Eels used to be everywhere in this valley. Now people do not believe they were ever here.",
                "Could you catch one? Carefully. They are older and stranger than they look."),
                "Fish_eel", 1, 0, "An eel for the museum", "Yuki wants an eel from the river for her exhibition about the valley as it used to be.",
                L("Any luck with an eel? They are shy. Fish at dusk, my father says. He is usually right about things that move slowly."),
                L("Look at it. Look at how it moves. Like ink in water.", "It will have the best tank in the museum. And a label with your name on it, as the collector.")),
                150),
            When(P(4, "Sterling & Sons",
                "Grace showed me the boot. 'Sterling & Sons Tannery.' Then we spent four hours in the museum basement with the archive boxes.",
                "We found the tannery's own records. Every barrel of chrome, every load of lime. They wrote it all down, very neatly. They were proud of it.",
                "I have never been so angry at good handwriting."),
                AfterFlag("story_grace_4")),
            When(P(5, "My father",
                "My father forgot his chess opening last week. He laughed. I did not.",
                "He is seventy-eight. He does tai chi every morning and wins every game. But I have started watching him the way I watch old paper in the archive. Looking for the first crack.",
                "He would tell me the slow river reaches the sea too. He would be right. I still do not like it."),
                AfterFlag("story_kenji_4")),
            P(6, "Remove the wetland section",
                "The museum has a new sponsor. Sterling Holdings. They paid for the new roof, which leaked, and the new lights, which are very good.",
                "Today their office sent a note about my exhibition. One line: 'Please remove the wetland section, as it may confuse visitors.'",
                "The wetland section is the exhibition. Without it there is only a river, and no reason anyone should care what happened to it."),
            P(7, "After hours",
                "Come to the museum tonight, after closing. I want to show you the exhibition as I made it. Before anyone changes it.",
                "Here is the wetland, painted from Grace's maps. Here are the tannery records. Here is your eel, in the middle of everything, looking unimpressed.",
                "And here is the last room. Empty, for now. I wanted it to be the valley as it could be again. I have not decided what to put in it."),
            Fork(P(8, "Which room?",
                "The museum director says I must choose. Remove the wetland section, or lose the sponsor, and possibly my job.",
                "Or there is another way. The Kims' back room is empty in the evenings. I could take the whole exhibition there. Free. Small. Ours.",
                "In the museum, ten thousand people would see it. In the suburb, a few hundred would. But nobody could tell me to take anything out.",
                "My father says 'ask the farmer'. So I am asking the farmer."),
                "Open it honestly at the museum",
                L("The museum, then, and all of it. If they take the sponsorship away, they take it away.", "I will write the labels myself tonight. In very clear, very neat handwriting. The Sterlings taught me that."),
                L("yuki_honest_exhibition", "tower_file_tannery"),
                "Move it to a pop-up in the suburb",
                L("A pop-up. Yes. Small and stubborn, like this street.", "I will need Walt for the shelves, Lena for the signs, and you for carrying the eel. Very carefully."),
                L("suburb_museum_open", "tower_file_tannery")),
            A(9, "Ten thousand people",
                "The exhibition opened. The sponsor withdrew the same afternoon. The director shook my hand and said 'well done' in a voice that meant 'you're fired'.",
                "But the city paper reviewed it. 'The most important room in the museum is the one about a swamp.' People queued.",
                "Victor Sterling came on the second day. He stood in front of the tannery records for twenty minutes. Then he left without a word. That was the best review of all."),
            Happy(B(9, "The museum behind the shop",
                "The pop-up has been open for a week. Rosa brings bread for the visitors. Nia gives tours, mostly about the eel.",
                "Walt built the shelves and said they were 'adequate', which I am told is very high praise.",
                "And the last room is not empty any more. Everybody on the street brought one thing that shows the valley as it could be. A seed packet, a drawing, a jar of clean water.")),
            Gift(Happy(A(10, "Two degrees",
                "The museum has asked me back. As director. The new director. The old one has 'retired'.",
                "The wetland room is staying. The last room now holds what the street brought: seeds, drawings, a jar of clean water. Your eel is the most popular resident in the building.",
                "And this morning, Frank's sign was straight. I did not say anything. I only lent him the protractor. Here. A thank-you, from a curator who finally has nothing to fix.")),
                400),
            Gift(Happy(B(10, "Two degrees",
                "I have taken my first Monday off in eleven years. No plans. I spent it the way my father would: slowly, and mostly on a bench.",
                "The museum in the city has asked to borrow our exhibition. All of it, as it is, with nothing taken out. They asked very politely.",
                "And Frank's sign is straight. I only lent him the protractor. Here. A thank-you, from a curator who finally has nothing to fix.")),
                400)));

        // ------------------------------------------------------------ LENA BELL (illustrator)
        all.Add(Story("lena", "Every House on the Street", "Print fifty copies", "Sign with the big publisher",
            "Lena is drawing every house on the street for a picture book and asks Sam to write the words. A big publisher, Sterling Media, wants it, " +
            "if she makes the street 'less shabby' and puts the tower in the background. Fork: print fifty copies at the Kims', or sign the big deal.",
            P(1, "A good hat face",
                "Oh, hold still. Hold still! ...Sorry. You turned your head and the light was perfect.",
                "You still don't have a hat. You really should get a hat. Your face is wasted without one."),
            P(2, "Your farm at dawn",
                "I walked out to your farm early this morning and drew it from the road. I hope that's all right.",
                "The barn, the fields, the mist sitting on top of the crops like a cat. It's going to be the last page of my book. I don't know why yet. It just feels like an ending."),
            When(P(3, "Paid in beetle facts",
                "Nia Abara posed for me today, on the swings. She charges one beetle fact per minute. I mean, I have to LISTEN to one per minute.",
                "Did you know some beetles can lift eight hundred times their own weight? I didn't. I do now. I know forty-five beetle facts now.",
                "The drawing's lovely, though. She looks exactly like a nine-year-old who knows more than you."),
                AfterFlag("story_nia_2")),
            Ask(P(4, "Handmade paper",
                "I want the book printed on handmade paper. Real paper, made from plants, like they used to.",
                "You can make it from fibre: the stringy bits from grass and weeds. Ten bundles would do the cover. Do you have any? Scything makes heaps of it, Mateo says."),
                "Fiber", 10, 0, "Fibre for Lena's paper", "Lena wants 10 Fiber to make handmade paper for the cover of her picture book about the street.",
                L("Any fibre for the paper? Ten bundles. The scythe is your friend, apparently. Mateo says weeds are just plants with bad publicity."),
                L("Oh, it's perfect. Look at the colours in it! Green and gold and a bit of brown.", "The cover is going to look like your fields. Exactly like them.")),
            When(P(5, "Drawings of Marcus",
                "I looked through my sketchbook from this year. All the drawings of Marcus.",
                "Spring: he's laughing. Summer: he's not. Lately he just sits at the kitchen table staring at nothing.",
                "I haven't asked. I drew him instead. I think he'll tell me when he's ready. I think he'll tell you first, actually. You're easier to tell things to."),
                AfterFlag("story_marcus_5")),
            P(6, "Sam's words",
                "I asked Sam Whitaker to write the words for the book. One line per house. Sam went white, said 'I can't possibly', and left.",
                "This morning there were ten pages pushed under my door. One line per house. Every line is perfect.",
                "The line for your farm just says: 'Somebody here plants things for people they'll never meet.'"),
            P(7, "Less shabby",
                "A big publisher wants the book. Sterling Media. They'd print a hundred thousand. In shops everywhere. In the tower.",
                "They sent notes. Brighter curtains on Sam's house. No cracks on Walt's porch. No weeds on the pavement. And the tower, 'softly, in the background, for context', on every page.",
                "I sat with the notes for an hour. Then I drew the tower in, very lightly, on one page, to see. I rubbed it out so hard I tore the paper."),
            Fork(P(8, "A hundred thousand or fifty",
                "The Kims have an old printer in the back room. Jae-hoon says we could do fifty copies. One for every house, and a few for the library.",
                "Or a hundred thousand, everywhere, with brighter curtains and a tower on every page. Marcus says the money would mean he could leave his job.",
                "I keep looking at the drawing of your farm. It's the only page they didn't want to change.",
                "What would you do?"),
                "Print fifty copies",
                L("Fifty. One for every house. Cracks and weeds and all.", "Jae-hoon says the printer makes a noise like a goose. I'm going to love that goose."),
                L("street_book_printed"),
                "Sign with the big publisher",
                L("A hundred thousand. Everywhere. For Marcus. For us.", "I'll do the brighter curtains. I'll do the porch. I'll draw the tower very, very lightly."),
                L("lena_sterling_book")),
            Happy(A(9, "Fifty copies",
                "The goose printer printed fifty. Some of the pages are a bit wonky. Walt says that's 'character'.",
                "I gave them out yesterday, one on every doorstep. Emeka helped, of course. He said it was the best delivery of his life.",
                "Gus read his page three times and then told me his window was drawn the wrong size. It isn't. He knows it isn't.")),
            B(9, "Everywhere",
                "The book's everywhere. The tower bookshop has a whole window of it. I walked past and didn't recognise my own street.",
                "Bright curtains. No cracks. Walt's porch looks like it was built yesterday by somebody who's never been grumpy.",
                "And on every page, in the background, very lightly, the tower. Nia read it and said, 'That's not where the tower is.' She's right."),
            Gift(Happy(A(10, "The launch",
                "Launch party tonight! The plaza, Tariq playing, Rosa's bread, and Sam reading the words out loud, which Sam has agreed to do 'only if it's dark'.",
                "And here. Copy number one. I saved it for you.",
                "Turn to the last page. Your farm, at dawn, with the mist like a cat. And Sam's line underneath. You didn't think you'd end up in a book when you took the old farm, did you?")),
                0, "Letter", 1),
            Gift(Flags(Happy(B(10, "The launch",
                "I made another book. Fifty copies, on the Kims' goose printer, on paper made from your fibre.",
                "The same street, drawn properly this time. Cracks, weeds, sad curtains and all. No tower. Sam wrote new words. Better ones.",
                "The big book paid for Marcus to leave his job. This one is for us. Copy number one is yours. Turn to the last page.")),
                "street_book_printed"), 0, "Letter", 1)));

        // ------------------------------------------------------------ ADA KOWALCZYK (retired florist)
        all.Add(Story("ada", "Never Tell a Rose", "Reopen the stall", "Pass it to Bea",
            "Ada ran a flower stall in the city for thirty years until the tower's flower department closed it. She says she isn't bitter. " +
            "Nobody has ever seen her cat. Fork: reopen a flower stall in the plaza, or hand her name and secrets to Bea.",
            P(1, "The rules",
                "Hello again, petal. Have you remembered the rules? Never water after sunset. Never cut on a Friday.",
                "And never, ever tell a rose it looks tired. They hear you. They take it personally. I've seen a whole bed of roses sulk for a month over one careless remark."),
            P(2, "The sulking tulips",
                "The red tulips by the general store are still sulking. Somebody's been sitting on the planter again.",
                "I have my suspicions. Young, tall, carries a guitar, never up before eleven. I've said nothing. I've only moved the planter four inches so the edge is very uncomfortable."),
            Gift(Ask(P(3, "Cousins of the rose",
                "Did you know a strawberry is a rose's cousin? Same family. Look at the flowers some time: five little petals, just like a wild rose.",
                "Bring me five of your strawberries. I want to press the leaves for my book, and eat the rest with cream, like my mother did in June."),
                "Strawberry", 5, 0, "Strawberries for Ada", "Ada wants five strawberries, the rose's cousin, to press the leaves and eat the rest with cream.",
                L("Strawberries, petal? Five. Cream's already in the fridge, and Pan Tadeusz keeps looking at it."),
                L("Look at those. Little hearts.", "Here, for your trouble. And a pressed violet, for luck. Keep it in a book. Any book. Books like flowers.")),
                90),
            P(4, "The stall",
                "Thirty years I had the flower stall by the city fountain. Rain or shine, five in the morning till six at night.",
                "Then the tower opened a flower department on the ground floor. Roses flown in from somewhere hot. Half my price. In plastic. No smell.",
                "I lasted eight months. On the last day I gave every flower I had left to people walking past. That was a good day, actually. The best and the worst."),
            When(P(5, "J",
                "Emeka brought me a letter. Thirty-one years late. Wrong house number.",
                "It was from a man called Jan. He asked me to come to the coast for a week, and bring the cat. Not this cat, the one before. I never got it. I thought he'd stopped writing.",
                "We'd have been very happy, I think. Or very annoyed with each other. Probably both. I sat on the step and laughed until I cried. Silly old woman."),
                AfterFlag("story_emeka_6")),
            P(6, "The Bloom Show",
                "There's to be a Bloom Show in the plaza next spring! Flowers, vegetables, everything people grow. Ribbons for the best.",
                "I'll enter. For fun. Just for fun. I don't care about ribbons.",
                "...My sweet peas are going to win a ribbon. That's not caring, petal. That's just a fact."),
            P(7, "Plastic perfect",
                "The tower has entered the Bloom Show. Did you hear? A whole display. Flowers that never wilt, never drop a petal, never smell of anything.",
                "Half of them are silk. The judges can't tell from the far end of the plaza.",
                "Bea and Soo-ah came round last night and we stayed up till two, deciding what to grow. Those girls know more Latin than I ever did. They just don't know the rules yet."),
            Fork(P(8, "The name on the stall",
                "I've been offered a spot in the plaza. A stall. My old sign is still in the attic: 'Kowalczyk Flowers'.",
                "I could open it again. Your flowers, my know-how. 'Kowalczyk & Farm'. My knees might last a season or two.",
                "Or I could give it all to Bea. The sign, the rules, the secrets. She'd enter the Bloom Show under my name, and I'd watch from the bench with a flask.",
                "You're the grower, petal. You decide."),
                "Reopen the stall",
                L("Then it's back to five in the morning! Oh, my knees are going to have words with me.", "Bring me everything that blooms, petal. And tell the roses they look wonderful. Every day."),
                L("ada_stall_open"),
                "Pass it to Bea",
                L("Bea, then. She's got the hands for it, and the patience. And young knees.", "I'll teach her the rules tonight. All of them. Even the one about Fridays, which isn't really a rule. I just like Fridays off."),
                L("ada_passed_stall")),
            Happy(A(9, "Kowalczyk & Farm",
                "First Saturday: sold out by eleven. Second Saturday: sold out by ten. Rosa says I'm stealing her customers. I'm sharing them.",
                "And the Bloom Show! My sweet peas: a blue ribbon. The tower's silk roses: nothing. One of the judges sniffed them and made a face.",
                "My knees hurt. My hands smell of earth. I haven't been this happy since the fountain.")),
            Happy(B(9, "From the bench",
                "Bea won. The Bloom Show, best in show, under my old name. 'Kowalczyk Flowers, by B. Lindqvist.'",
                "I watched from the bench with a flask of tea and cried into the planters. The tulips didn't mind. They were in a good mood for once.",
                "The tower's silk roses got nothing. A judge sniffed them and made a face. I've never liked a judge so much.")),
            Gift(Flags(Happy(A(10, "Pan Tadeusz",
                "Come inside, petal. No, really, come in. There's somebody who wants to meet you.",
                "This is Pan Tadeusz. He's been listening to me talk about you for a year, and he's decided you're allowed.",
                "Yes, he's enormous. No, he doesn't go outside. He thinks outside is a rumour. Sit down; he'll sit on you. That means he approves. Here: flower money, your share of the stall.")),
                "met_pan_tadeusz"), 500),
            Gift(Flags(Happy(B(10, "Pan Tadeusz",
                "Come inside, petal. Mind the step. Somebody's been waiting to meet you.",
                "This is Pan Tadeusz. Twenty pounds of opinions. He's been listening to me talk about you, and he's decided you're allowed in.",
                "He's sitting on your feet. That means you're family now. Here, from Bea and me: a share of the Bloom Show prize. Growers should get the ribbon money. That's a rule.")),
                "met_pan_tadeusz"), 400)));

        return all;
    }

    // ------------------------------------------------------------------ asset helpers
    /// <summary>Finds an ItemData whose FILE name is exactly 'assetName' (for example "Wheat"), anywhere in the project.</summary>
    private static ItemData FindItem(string assetName)
    {
        foreach (string guid in AssetDatabase.FindAssets(assetName + " t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == assetName) return AssetDatabase.LoadAssetAtPath<ItemData>(path);
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
