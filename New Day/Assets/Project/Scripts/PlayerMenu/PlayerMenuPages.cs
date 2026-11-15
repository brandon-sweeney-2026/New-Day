using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each tab of the player menu shows. Every tab is just a list of rows (PlayerMenuRow):
/// a label, some text on the right, a description for the panel on the right, and what happens when it is chosen.
/// PlayerMenu.cs does the drawing and the keys; to change what a tab SAYS, this is the file to edit.
///
/// To add a tab: add its name to TabNames and a case to BuildTab.
/// </summary>
public static class PlayerMenuPages
{
    public static readonly string[] TabNames = { "Character", "Skills", "Crafting", "Quests", "Achievements", "People", "Settings" };
    public const int TabCount = 7;

    private static readonly Color HealthColor = new Color(0.85f, 0.2f, 0.25f);
    private static readonly Color MagicColor = new Color(0.25f, 0.5f, 0.95f);
    private static readonly Color XpColor = new Color(0.45f, 0.85f, 0.45f);
    private static readonly Color HeartColor = new Color(0.95f, 0.4f, 0.55f);
    private static readonly Color SliderColor = new Color(0.95f, 0.75f, 0.3f);

    private static PlayerMenuSettings Settings { get { return PlayerMenuSettings.Current; } }

    /// <summary>The rows of a tab's first page.</summary>
    public static List<PlayerMenuRow> BuildTab(int tab)
    {
        switch (tab)
        {
            case 0: return Character();
            case 1: return Skills();
            case 2: return Crafting();
            case 3: return Quests();
            case 4: return AchievementRows();
            case 5: return People();
            default: return SettingsRows();
        }
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    /// <summary>A row that only shows information (nothing happens when it is chosen).</summary>
    private static PlayerMenuRow Info(string label, string right, string detail)
    {
        return new PlayerMenuRow { label = label, right = right, detail = detail };
    }

    /// <summary>A heading between groups of rows.</summary>
    private static PlayerMenuRow Heading(string label, string detail = "")
    {
        return new PlayerMenuRow { label = "- " + label + " -", detailTitle = label, detail = detail, color = Settings.headingColor };
    }

    private static PlayerMenuRow Empty(string label, string detail)
    {
        return new PlayerMenuRow { label = label, detailTitle = " ", detail = detail, enabled = false };
    }

    private static string Percent(float value) { return Mathf.RoundToInt(value * 100f) + "%"; }

    // ------------------------------------------------------------------
    // CHARACTER: health, magic, bonuses and what you are wearing
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> Character()
    {
        var rows = new List<PlayerMenuRow>();
        PlayerStats stats = PlayerStats.Instance;

        if (stats != null)
        {
            rows.Add(new PlayerMenuRow
            {
                label = "Health", right = Mathf.CeilToInt(stats.Health) + " / " + stats.maxHealth,
                bar = stats.maxHealth > 0 ? stats.Health / stats.maxHealth : 0f, barColor = HealthColor,
                detail = "If this runs out you are knocked out and wake up in bed the next day.\n\nFood and a night's sleep bring it back. Perks and equipment can raise the maximum."
                       + (PlayerBonuses.MaxHealth != 0 ? "\n\nFrom perks and equipment: +" + PlayerBonuses.MaxHealth : ""),
            });
            rows.Add(new PlayerMenuRow
            {
                label = "Magic", right = Mathf.CeilToInt(stats.Magic) + " / " + Mathf.CeilToInt(stats.maxMagic),
                bar = stats.maxMagic > 0f ? stats.Magic / stats.maxMagic : 0f, barColor = MagicColor,
                detail = "Spells use magic. It slowly refills by itself."
                       + (PlayerBonuses.MaxMagic != 0f ? "\n\nFrom perks and equipment: +" + PlayerBonuses.MaxMagic.ToString("0.#") : ""),
            });
        }

        rows.Add(Info("Defense", PlayerBonuses.Defense.ToString(), "Each point of defense takes 1 off every hit you take. A hit always does at least 1."));
        rows.Add(Info("Damage", Percent(PlayerBonuses.DamageMultiplier), "How hard your weapons and spells hit, compared with normal."));
        rows.Add(Info("Walking speed", Percent(PlayerBonuses.MoveSpeedMultiplier), "How fast you walk, compared with normal."));
        rows.Add(Info("Total level", PlayerSkills.TotalLevel.ToString(), "All of your skill levels added together. See the Skills tab."));
        rows.Add(Info("Gold", GameState.Gold + " g", "The money you are carrying."));

        rows.Add(Heading("Equipment", "Things you wear. Choose a slot to put something on or take it off.\n\nAn item can be worn when its Type is Equipment (set on the item in the Inspector)."));

        for (int i = 0; i < PlayerEquipment.SlotCount; i++)
        {
            int slot = i;
            ItemData worn = PlayerEquipment.Get(slot);
            rows.Add(new PlayerMenuRow
            {
                label = PlayerEquipment.SlotNames[slot],
                right = worn != null ? worn.itemName : "(empty)",
                icon = worn != null ? worn.icon : null,
                detailIcon = worn != null ? worn.icon : null,
                detailTitle = worn != null ? worn.itemName : PlayerEquipment.SlotNames[slot],
                detail = worn != null
                    ? worn.description + "\n\n" + PlayerEquipment.StatsText(worn) + "\n\nSpace: change"
                    : "Nothing here.\n\nSpace: choose something from your backpack",
                onSelect = () => PlayerMenu.Push(PlayerEquipment.SlotNames[slot], () => EquipChoices(slot)),
            });
        }
        return rows;
    }

    /// <summary>The backpack items that fit one equipment slot.</summary>
    private static List<PlayerMenuRow> EquipChoices(int slot)
    {
        var rows = new List<PlayerMenuRow>();
        Inventory inv = Inventory.Instance;
        ItemData worn = PlayerEquipment.Get(slot);

        if (worn != null)
        {
            rows.Add(new PlayerMenuRow
            {
                label = "Take off " + worn.itemName, icon = worn.icon, detailIcon = worn.icon, detailTitle = worn.itemName,
                detail = "Puts it back in your backpack.\n\n" + PlayerEquipment.StatsText(worn),
                onSelect = () => { if (PlayerEquipment.Unequip(slot, inv)) PlayerMenu.Back(); },
            });
        }

        // Each different item is listed once, however many slots it fills.
        var seen = new HashSet<ItemData>();
        if (inv != null)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                ItemStack s = inv.GetSlot(i);
                if (s == null || s.IsEmpty || !PlayerEquipment.Fits(s.item, slot) || !seen.Add(s.item)) continue;

                ItemData item = s.item;
                rows.Add(new PlayerMenuRow
                {
                    label = item.itemName, right = "x" + inv.CountOf(item), icon = item.icon, detailIcon = item.icon,
                    detail = item.description + "\n\n" + PlayerEquipment.StatsText(item) + "\n\nSpace: put on",
                    onSelect = () => { if (PlayerEquipment.Equip(slot, item, inv)) PlayerMenu.Back(); },
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(Empty("(nothing in your backpack fits here)", "Find, buy or craft something for this slot first."));
        return rows;
    }

    // ------------------------------------------------------------------
    // SKILLS: levels and experience trees
    // ------------------------------------------------------------------

    private static string HowToEarn(PlayerSkills.Skill skill)
    {
        switch (skill)
        {
            case PlayerSkills.Skill.Farming: return "Earned by planting seeds, harvesting crops and cooking.";
            case PlayerSkills.Skill.Mining: return "Earned by breaking rocks and ore with a pickaxe.";
            case PlayerSkills.Skill.Foraging: return "Earned by chopping trees and clearing wood and grass.";
            default: return "Earned by defeating enemies and banishing shadows.";
        }
    }

    private static List<PlayerMenuRow> Skills()
    {
        var rows = new List<PlayerMenuRow>();

        foreach (PlayerSkills.Skill s in PlayerSkills.All)
        {
            PlayerSkills.Skill skill = s;
            int level = PlayerSkills.Level(skill);
            int points = PlayerSkills.PointsAvailable(skill);
            rows.Add(new PlayerMenuRow
            {
                label = skill.ToString() + (points > 0 ? "  (+" + points + ")" : ""),
                right = "Level " + level,
                bar = PlayerSkills.LevelProgress(skill), barColor = XpColor,
                color = points > 0 ? (Color?)Settings.goodColor : null,
                detailTitle = skill.ToString(),
                detail = HowToEarn(skill)
                       + "\n\nXP: " + PlayerSkills.Xp(skill) + " / " + PlayerSkills.XpForLevel(level + 1) + " for level " + (level + 1)
                       + "\nPerk points to spend: " + points
                       + "\n\nEvery level gives one perk point.\nSpace: open the experience tree",
                onSelect = () => PlayerMenu.Push(skill.ToString(), () => Perks(skill)),
            });
        }

        // Fishing keeps its own numbers (FishingTalents), but it is shown here with the others.
        int fishLevel = FishingTalents.FishingLevel;
        int fishPoints = FishingTalents.PointsAvailable;
        int fishFrom = 25 * fishLevel * (fishLevel + 1) / 2, fishTo = 25 * (fishLevel + 1) * (fishLevel + 2) / 2;
        rows.Add(new PlayerMenuRow
        {
            label = "Fishing" + (fishPoints > 0 ? "  (+" + fishPoints + ")" : ""),
            right = "Level " + fishLevel,
            bar = Mathf.Clamp01((FishingTalents.Xp - fishFrom) / (float)(fishTo - fishFrom)), barColor = XpColor,
            color = fishPoints > 0 ? (Color?)Settings.goodColor : null,
            detailTitle = "Fishing",
            detail = "Earned by catching fish."
                   + "\n\nXP: " + FishingTalents.Xp + " / " + fishTo + " for level " + (fishLevel + 1)
                   + "\nTalent points to spend: " + fishPoints
                   + "\n\nEvery level gives one talent point.\nSpace: open the experience tree",
            onSelect = () => PlayerMenu.Push("Fishing", FishingPerks),
        });
        return rows;
    }

    private static List<PlayerMenuRow> Perks(PlayerSkills.Skill skill)
    {
        var rows = new List<PlayerMenuRow>();
        int points = PlayerSkills.PointsAvailable(skill);

        foreach (PlayerMenuSettings.Perk p in PlayerSkills.PerksOf(skill))
        {
            PlayerMenuSettings.Perk perk = p;
            int level = PlayerSkills.PerkLevel(perk.id);
            bool full = level >= perk.maxLevel;
            rows.Add(new PlayerMenuRow
            {
                label = perk.perkName, right = level + " / " + perk.maxLevel,
                bar = perk.maxLevel > 0 ? level / (float)perk.maxLevel : 0f, barColor = XpColor,
                enabled = !full && points > 0,
                color = full ? (Color?)Settings.goodColor : null,
                detail = perk.description + "\n\nPoints to spend: " + points
                       + (full ? "\n\nThis perk is at its top level." : "\n\nSpace: spend 1 point"),
                onSelect = () =>
                {
                    if (PlayerSkills.TrySpend(perk)) GameEvents.ShowToast(perk.perkName + " is now level " + PlayerSkills.PerkLevel(perk.id) + ".");
                    else GameEvents.ShowToast(full ? "That perk is already at its top level." : "No perk points. Gain a " + skill + " level first.");
                },
            });
        }

        if (rows.Count == 0) rows.Add(Empty("(no perks yet)", "Add some in the Player Menu Settings asset, under Perks."));
        return rows;
    }

    private static List<PlayerMenuRow> FishingPerks()
    {
        var rows = new List<PlayerMenuRow>();
        int points = FishingTalents.PointsAvailable;

        foreach (FishingTalents.Talent t in FishingTalents.All)
        {
            FishingTalents.Talent talent = t;
            int level = FishingTalents.Level(talent.id);
            bool full = level >= talent.maxLevel;
            rows.Add(new PlayerMenuRow
            {
                label = talent.name, right = level + " / " + talent.maxLevel,
                bar = level / (float)Mathf.Max(1, talent.maxLevel), barColor = XpColor,
                enabled = !full && points > 0,
                color = full ? (Color?)Settings.goodColor : null,
                detail = talent.description + "\n\nPoints to spend: " + points
                       + (full ? "\n\nThis talent is at its top level." : "\n\nSpace: spend 1 point"),
                onSelect = () =>
                {
                    if (FishingTalents.TrySpend(talent.id)) GameEvents.ShowToast(talent.name + " is now level " + FishingTalents.Level(talent.id) + ".");
                    else GameEvents.ShowToast(full ? "That talent is already at its top level." : "No talent points. Gain a Fishing level first.");
                },
            });
        }
        return rows;
    }

    // ------------------------------------------------------------------
    // CRAFTING: make things anywhere from what is in the backpack
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> Crafting()
    {
        var rows = new List<PlayerMenuRow>();
        Inventory inv = Inventory.Instance;

        foreach (Recipe recipe in Settings.craftingRecipes)
        {
            if (recipe == null || recipe.result == null) continue;
            Recipe r = recipe;
            bool can = r.CanCook(inv);

            string stats = r.result.type == ItemData.ItemType.Equipment ? "\n" + PlayerEquipment.StatsText(r.result) : "";
            rows.Add(new PlayerMenuRow
            {
                label = r.recipeName, right = can ? "Ready" : "Missing",
                icon = r.result.icon, detailIcon = r.result.icon,
                enabled = can,
                color = can ? (Color?)Settings.goodColor : null,
                detail = r.description + "\n\nNeeds: " + r.IngredientText(inv)
                       + "\nMakes: " + r.resultCount + " " + r.result.itemName + stats
                       + (r.cookMinutes > 0 ? "\nTakes: " + r.cookMinutes + " minutes" : "")
                       + (can ? "\n\nSpace: make it" : ""),
                onSelect = () =>
                {
                    // If the backpack is full the new item is dropped at the player's feet.
                    Vector3 drop = PlayerStats.Instance != null ? PlayerStats.Instance.transform.position + Vector3.down * 0.5f : Vector3.zero;
                    if (!r.TryCook(inv, drop, 0f)) GameEvents.ShowToast("You don't have everything it needs.");
                },
            });
        }

        if (rows.Count == 0)
            rows.Add(Empty("(no crafting recipes yet)",
                "Run Farm > Set Up > Player Menu to make some starter recipes.\n\nTo add your own: Create > Farm > Recipe, tick 'Crafted', save it in Assets/Farm/Data/Crafting and run that menu again."));
        return rows;
    }

    // ------------------------------------------------------------------
    // QUESTS
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> Quests()
    {
        var rows = new List<PlayerMenuRow>();

        // Unfinished quests first, then the finished ones.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (QuestLog.Entry e in QuestLog.Entries)
            {
                if (e.done != (pass == 1)) continue;
                string progress = e.ProgressText;
                rows.Add(new PlayerMenuRow
                {
                    label = e.title, right = e.done ? "Done" : "Active",
                    color = e.done ? (Color?)Settings.goodColor : null,
                    detail = (string.IsNullOrEmpty(e.giver) ? "" : "From: " + e.giver + "\n\n") + e.description
                           + (string.IsNullOrEmpty(progress) ? "" : "\n\n" + progress)
                           + (e.done ? "\n\nFinished." : ""),
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(Empty("(no quests yet)", "Talk to the people and creatures you meet. When someone asks for help and you agree, it is written down here."));
        return rows;
    }

    // ------------------------------------------------------------------
    // ACHIEVEMENTS
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> AchievementRows()
    {
        var rows = new List<PlayerMenuRow>();
        List<PlayerMenuSettings.Achievement> all = Settings.achievements;

        rows.Add(Heading(Achievements.DoneCount + " of " + all.Count + " earned", "Achievements are earned by playing. Some give gold."));

        foreach (PlayerMenuSettings.Achievement a in all)
        {
            if (a == null) continue;
            bool done = Achievements.IsDone(a);
            int have, need;
            Achievements.Progress(a, out have, out need);

            rows.Add(new PlayerMenuRow
            {
                label = a.title, right = done ? "Done" : (need > 1 ? have + " / " + need : ""),
                icon = a.icon, detailIcon = a.icon,
                bar = have / (float)need, barColor = done ? XpColor : SliderColor,
                color = done ? (Color?)Settings.goodColor : null,
                detail = a.description + (a.rewardGold > 0 ? "\n\nReward: " + a.rewardGold + " g" : "") + (done ? "\n\nEarned!" : ""),
            });
        }
        return rows;
    }

    // ------------------------------------------------------------------
    // PEOPLE: friendship and what you know about each villager
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> People()
    {
        var rows = new List<PlayerMenuRow>();
        int maxHearts = Settings.maxHearts;

        foreach (Friendship.Person person in Friendship.Known)
        {
            int hearts = Friendship.Hearts(person.id);
            string detail = "Friendship: " + hearts + " / " + maxHearts + " hearts"
                          + "\n" + (Friendship.TalkedToday(person.id) ? "You have talked today." : "You haven't talked today.")
                          + "\n" + (Friendship.GiftedToday(person.id) ? "You gave a gift today." : "You can give a gift today.")
                          + "\n\n" + TasteText(person);

            // A little more about them once you are on good terms.
            if (person.profile != null && !string.IsNullOrEmpty(person.profile.extraNote) && hearts >= 2)
                detail += "\n\n" + person.profile.extraNote;

            rows.Add(new PlayerMenuRow
            {
                label = person.name, right = hearts + " / " + maxHearts + " hearts",
                icon = person.portrait, detailIcon = person.portrait,
                bar = Friendship.HeartProgress(person.id), barColor = HeartColor,
                detail = detail,
            });
        }

        if (rows.Count == 0)
            rows.Add(Empty("(you haven't met anyone yet)",
                "People appear here after you talk to them.\n\nChat once a day and give gifts to become friends: hold the gift, talk to them, and choose 'Give'."));
        return rows;
    }

    /// <summary>What the player has found out about someone's taste by giving gifts.</summary>
    private static string TasteText(Friendship.Person person)
    {
        NpcProfile profile = person.profile;
        if (profile == null) return "Gifts: you don't know what they like yet.";

        return "Loves: " + KnownItems(person.id, profile.loves)
             + "\nLikes: " + KnownItems(person.id, profile.likes)
             + "\nDislikes: " + KnownItems(person.id, profile.dislikes);
    }

    private static string KnownItems(string personId, ItemData[] items)
    {
        if (items == null || items.Length == 0) return "-";
        var names = new List<string>();
        int unknown = 0;
        foreach (ItemData item in items)
        {
            if (item == null) continue;
            if (Friendship.KnowsTaste(personId, item)) names.Add(item.itemName);
            else unknown++;
        }
        if (unknown > 0) names.Add(unknown == 1 ? "1 thing you haven't found" : unknown + " things you haven't found");
        return names.Count == 0 ? "-" : string.Join(", ", names.ToArray());
    }

    // ------------------------------------------------------------------
    // SETTINGS
    // ------------------------------------------------------------------

    private static List<PlayerMenuRow> SettingsRows()
    {
        var rows = new List<PlayerMenuRow>();
        const float step = 0.05f;

        rows.Add(new PlayerMenuRow
        {
            label = "UI size", right = "<  " + Percent(GameSettings.UiScale) + "  >",
            bar = Mathf.InverseLerp(GameSettings.MinUiScale, GameSettings.MaxUiScale, GameSettings.UiScale), barColor = SliderColor,
            detail = "How big the menus and the bars on screen are.\n\nLeft / Right: change",
            onLeft = () => GameSettings.UiScale -= step,
            onRight = () => GameSettings.UiScale += step,
        });
        rows.Add(new PlayerMenuRow
        {
            label = "Sound volume", right = "<  " + Percent(GameSettings.MasterVolume) + "  >",
            bar = GameSettings.MasterVolume, barColor = SliderColor,
            detail = "How loud the whole game is.\n\nLeft / Right: change",
            onLeft = () => GameSettings.MasterVolume -= step,
            onRight = () => GameSettings.MasterVolume += step,
        });
        rows.Add(new PlayerMenuRow
        {
            label = "Screen brightness", right = "<  " + Percent(GameSettings.Brightness) + "  >",
            bar = Mathf.InverseLerp(GameSettings.MinBrightness, GameSettings.MaxBrightness, GameSettings.Brightness), barColor = SliderColor,
            detail = "Makes the whole picture darker or lighter. 100% is normal.\n\nLeft / Right: change",
            onLeft = () => GameSettings.Brightness -= step,
            onRight = () => GameSettings.Brightness += step,
        });
        rows.Add(new PlayerMenuRow
        {
            label = "Fullscreen", right = GameSettings.Fullscreen ? "On" : "Off",
            detail = "Fill the whole screen, or play in a window.\n(This only works in a built game, not inside the Unity editor.)\n\nSpace: switch",
            onSelect = () => GameSettings.Fullscreen = !GameSettings.Fullscreen,
            onLeft = () => GameSettings.Fullscreen = false,
            onRight = () => GameSettings.Fullscreen = true,
        });
        rows.Add(new PlayerMenuRow
        {
            label = "Reset settings",
            detail = "Puts UI size, volume and brightness back to 100%.\n\nSpace: reset",
            onSelect = () => { GameSettings.ResetToDefaults(); GameEvents.ShowToast("Settings reset."); },
        });
        rows.Add(new PlayerMenuRow
        {
            label = "Back to the game",
            detail = "Closes this menu.",
            onSelect = PlayerMenu.Close,
        });
        return rows;
    }
}
