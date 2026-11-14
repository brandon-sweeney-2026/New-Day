using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Works out the best tip for what the player could do RIGHT NOW: the closest thing worth doing
/// (harvest ripe crops, water thirsty plants, plant seeds in empty soil, chop a tree, smash a rock, fight an enemy,
/// ship your crops, go to bed...). Meatball says the result. Edit the text below to change what he says.
/// </summary>
public static class MeatballTips
{
    /// <summary>Things farther than this (in tiles) are ignored.</summary>
    public const float SearchRadius = 16f;

    private struct Cand
    {
        public float score;      // lower = more important (distance, with a bonus for urgent things)
        public string text;
        public Cand(float score, string text) { this.score = score; this.text = text; }
    }

    private static readonly string[] GeneralTips =
    {
        "Press the number keys or use the mouse wheel to pick an item on your toolbar. Then press Space (or J) to use it!",
        "Crops only grow on days they were watered. Grab the Watering Can every morning!",
        "Use the Hoe on grass or dirt to till it. Seeds can only be planted in tilled soil.",
        "Put crops in the mailbox and you get paid at the end of the day. Changed your mind? Take them back out before bed!",
        "Trees grow through the seasons. Fully grown ones drop items, and sometimes seeds, when you chop them.",
        "Keep an eye on your health bar. Food restores it, and sleeping in your bed refills everything.",
        "Pollution makes crops grow slower and sell for less. Keep an eye on it!",
    };

    public static string FindTip(PlayerActions player)
    {
        if (player == null) return GeneralTips[0];

        var list = new List<Cand>();
        Vector2 pos = player.transform.position;
        Inventory inv = player.Inventory;
        FarmManager farm = Object.FindAnyObjectByType<FarmManager>();
        CropManager crops = CropManager.Instance;
        TreeManager trees = TreeManager.Instance;
        TimeManager time = TimeManager.Instance;

        // ---- urgent: low health ----
        PlayerStats stats = player.Stats;
        if (stats != null && !stats.IsDead && stats.Health < stats.maxHealth * 0.35f)
        {
            int food = FindSlot(inv, it => it.type == ItemData.ItemType.Food);
            string how = food >= 0 ? "Select your " + inv.GetSlot(food).item.itemName + " (" + SlotName(food) + ") and press Space to eat it." : "Eat some food, or sleep in your bed.";
            list.Add(new Cand(-100f, "Your health is low! " + how));
        }

        // ---- late at night ----
        if (time != null && (time.HourOfDay >= 22 || time.HourOfDay < 5))
            list.Add(new Cand(-20f, "It's really late! If you stay up you'll pass out. Go sleep in your bed so the farm can grow."));

        // ---- enemies ----
        float enemyD = float.MaxValue;
        MonoBehaviour enemy = null;
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>())
        {
            var dmg = mb as IDamageable;
            if (dmg == null || dmg.DamageTeam != Team.Enemy) continue;
            float d = Vector2.Distance(pos, mb.transform.position);
            if (d < enemyD) { enemyD = d; enemy = mb; }
        }
        if (enemy != null && enemyD <= SearchRadius)
        {
            int weapon = FindSlot(inv, it => it.type == ItemData.ItemType.Weapon);
            string how = weapon >= 0
                ? EquipLine(inv, weapon, inv.GetSlot(weapon).item.itemName) + " Face it and press Space to swing. Back off when your health runs low!"
                : "You don't have a weapon yet. Check the shop, then come back and swing it at them with Space.";
            list.Add(new Cand(enemyD - 8f, "Watch out! An enemy is " + Where(pos, enemy.transform.position) + ". " + how));
        }

        // ---- crops ----
        if (farm != null && crops != null)
        {
            float ripeD = float.MaxValue, thirstD = float.MaxValue;
            Vector3Int ripeCell = default(Vector3Int), thirstCell = default(Vector3Int);
            foreach (Vector3Int cell in crops.PlantedCells)
            {
                float d = Vector2.Distance(pos, farm.CellToWorldCenter(cell));
                if (crops.IsRipe(cell)) { if (d < ripeD) { ripeD = d; ripeCell = cell; } }
                else if (crops.IsThirsty(cell)) { if (d < thirstD) { thirstD = d; thirstCell = cell; } }
            }
            if (ripeD <= SearchRadius)
            {
                string name = CropName(crops, ripeCell);
                list.Add(new Cand(ripeD - 1f, "Your " + name + " (" + Where(pos, farm.CellToWorldCenter(ripeCell)) + ") is ready to harvest! "
                    + Equip(inv, ItemData.ToolAction.Sickle, "Sickle") + " Face the plant and press Space."));
            }
            if (thirstD <= SearchRadius)
            {
                string name = CropName(crops, thirstCell);
                list.Add(new Cand(thirstD, "Your " + name + " (" + Where(pos, farm.CellToWorldCenter(thirstCell)) + ") is thirsty! "
                    + Equip(inv, ItemData.ToolAction.WateringCan, "Watering Can") + " Face the soil and press Space. Plants only grow on days they were watered."));
            }

            // empty tilled soil
            float soilD = float.MaxValue; Vector3Int soilCell = default(Vector3Int); int tilledCount = 0;
            foreach (Vector3Int cell in farm.TilledCells)
            {
                tilledCount++;
                if (crops.HasCrop(cell) || farm.HasCrop(cell)) continue;
                float d = Vector2.Distance(pos, farm.CellToWorldCenter(cell));
                if (d < soilD) { soilD = d; soilCell = cell; }
            }
            if (soilD <= SearchRadius)
            {
                int seed = FindSlot(inv, it => it.type == ItemData.ItemType.Seed && crops.FindBySeed(it) != null);
                string text = seed >= 0
                    ? "There's empty tilled soil (" + Where(pos, farm.CellToWorldCenter(soilCell)) + "). " + EquipLine(inv, seed, inv.GetSlot(seed).item.itemName) + " Face the soil and press Space to plant."
                    : "That tilled soil (" + Where(pos, farm.CellToWorldCenter(soilCell)) + ") is empty. Buy some seeds at the shop and plant them!";
                list.Add(new Cand(soilD, text));
            }
            else if (tilledCount == 0)
            {
                list.Add(new Cand(SearchRadius - 1f, "Let's start a garden! " + Equip(inv, ItemData.ToolAction.Hoe, "Hoe") + " Face some grass or dirt and press Space to till it."));
            }
        }

        // ---- trees ----
        if (trees != null)
        {
            float treeD = float.MaxValue; GrowingTree tree = null;
            foreach (GrowingTree t in trees.AllTrees)
            {
                if (t == null || !t.IsMature) continue;
                float d = Vector2.Distance(pos, t.transform.position);
                if (d < treeD) { treeD = d; tree = t; }
            }
            if (tree != null && treeD <= SearchRadius)
                list.Add(new Cand(treeD + 2f, "That tree (" + Where(pos, tree.transform.position) + ") is fully grown! "
                    + Equip(inv, ItemData.ToolAction.Axe, "Axe") + " Chop it for wood. Sometimes trees drop seeds you can plant!"));
        }

        // ---- rocks, stumps and other breakables ----
        float brkD = float.MaxValue; Breakable brk = null;
        foreach (Breakable b in Object.FindObjectsByType<Breakable>())
        {
            if (b == null || !b.IsIntact) continue;
            float d = Vector2.Distance(pos, b.transform.position);
            if (d < brkD) { brkD = d; brk = b; }
        }
        // ---- mineable rocks (mountain) ----
        float rockD = float.MaxValue; MineableRock rock = null;
        foreach (MineableRock mr in Object.FindObjectsByType<MineableRock>())
        {
            if (mr == null || !mr.IsIntact) continue;
            float d = Vector2.Distance(pos, mr.transform.position);
            if (d < rockD) { rockD = d; rock = mr; }
        }
        if (rock != null && rockD <= SearchRadius)
        {
            bool ore = rock.rockName != "Rock";
            list.Add(new Cand(rockD + 1f, "You can mine that " + (ore ? rock.rockName + " ore " : "rock ") + "(" + Where(pos, rock.transform.position) + "). "
                + Equip(inv, ItemData.ToolAction.Pickaxe, "Pickaxe") + " Face it and press Space a few times"
                + (ore ? " - ore sells for a lot more than plain stone!" : ". Rocks drop stone, and sometimes you find coal or copper hiding in the rocks.")));
        }

        if (brk != null && brkD <= SearchRadius)
        {
            ItemData.ToolAction need = brk.RequiredTool;
            list.Add(new Cand(brkD + 1f, "You can break that " + brk.gameObject.name + " (" + Where(pos, brk.transform.position) + "). "
                + Equip(inv, need, ToolName(need)) + " Face it and press Space a few times."));
        }

        // ---- places to use ----
        ShippingBin bin = Object.FindAnyObjectByType<ShippingBin>();
        if (bin != null)
        {
            float d = Vector2.Distance(pos, bin.transform.position);
            int sellable = inv != null ? inv.CountMatching(it => bin.Accepts(it)) : 0;
            if (d <= SearchRadius && sellable > 0)
                list.Add(new Cand(d, "You're carrying things you can sell! Put them in the mailbox (" + Where(pos, bin.transform.position)
                    + "). You get paid when the day ends, and you can take items back out until then."));
        }
        ShopCounter shop = Object.FindAnyObjectByType<ShopCounter>();
        if (shop != null)
        {
            float d = Vector2.Distance(pos, shop.transform.position);
            bool hasSeeds = FindSlot(inv, it => it.type == ItemData.ItemType.Seed) >= 0;
            if (d <= SearchRadius && !hasSeeds)
                list.Add(new Cand(d + 3f, "You're out of seeds! The shop (" + Where(pos, shop.transform.position) + ") sells more."));
        }
        CookingStation stove = Object.FindAnyObjectByType<CookingStation>();
        if (stove != null)
        {
            float d = Vector2.Distance(pos, stove.transform.position);
            if (d <= SearchRadius) list.Add(new Cand(d + 6f, "You can cook meals at the stove (" + Where(pos, stove.transform.position) + "). Food heals you!"));
        }

        // ---- pick the best ----
        if (list.Count == 0) return NextGeneralTip();
        Cand best = list[0];
        for (int i = 1; i < list.Count; i++) if (list[i].score < best.score) best = list[i];
        return best.text;
    }

    private static string NextGeneralTip()
    {
        int i = GameState.GetCounter("meatball_general_tip");
        GameState.SetCounter("meatball_general_tip", i + 1);
        return GeneralTips[i % GeneralTips.Length];
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string CropName(CropManager crops, Vector3Int cell)
    {
        CropData c = crops.CropAt(cell);
        return c != null ? c.cropName : "crop";
    }

    private static string ToolName(ItemData.ToolAction a)
    {
        switch (a)
        {
            case ItemData.ToolAction.WateringCan: return "Watering Can";
            case ItemData.ToolAction.FishingRod: return "Fishing Rod";
            default: return a.ToString();
        }
    }

    private static int FindSlot(Inventory inv, System.Predicate<ItemData> match)
    {
        if (inv == null) return -1;
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            ItemStack s = inv.GetSlot(i);
            if (s != null && !s.IsEmpty && match(s.item)) return i;
        }
        return -1;
    }

    private static string SlotName(int index)
    {
        if (index >= Inventory.Columns) return "in your backpack";
        return "slot " + (index == 9 ? "0" : (index + 1).ToString());
    }

    /// <summary>"Equip the Hoe (slot 2)." / "You already have the Hoe ready." / "You don't have a Hoe yet."</summary>
    private static string Equip(Inventory inv, ItemData.ToolAction action, string toolName)
    {
        int idx = FindSlot(inv, it => it.type == ItemData.ItemType.Tool && it.toolAction == action);
        if (idx < 0) return "You don't have a " + toolName + " yet.";
        return EquipLine(inv, idx, toolName);
    }

    private static string EquipLine(Inventory inv, int idx, string itemName)
    {
        if (inv != null && inv.SelectedIndex == idx) return "You already have the " + itemName + " ready.";
        if (idx >= Inventory.Columns) return "Your " + itemName + " is in your backpack. Move it to your toolbar, then select it.";
        return "Equip the " + itemName + " (" + SlotName(idx) + ").";
    }

    private static string Where(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        float dist = d.magnitude;
        if (dist < 2f) return "right next to you";
        string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
        int idx = Mathf.RoundToInt(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg / 45f);
        idx = ((idx % 8) + 8) % 8;
        return "about " + Mathf.RoundToInt(dist) + " tiles " + names[idx];
    }
}
