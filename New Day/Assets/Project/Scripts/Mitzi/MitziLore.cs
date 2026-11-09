using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What Mitzi says about magic, crafting and fishing. The text adapts to what the player has (spells, recipes, a rod).
/// Edit the strings here to change her words.
/// </summary>
public static class MitziLore
{
    public static string[] Magic(PlayerActions p)
    {
        var pages = new List<string>();
        pages.Add("Magic is like catnip: it fills you up, you spend it, and it comes back. Every spell drains your magic bar, then it slowly refills by itself.");

        PlayerStats st = p != null ? p.Stats : null;
        if (st != null)
        {
            string low = st.Magic < st.maxMagic * 0.3f ? " Hmph. Low. Rest a moment, or eat something." : "";
            pages.Add("Right now you have " + Mathf.RoundToInt(st.Magic) + " of " + Mathf.RoundToInt(st.maxMagic) + " magic." + low);
        }

        Inventory inv = p != null ? p.Inventory : null;
        var spellLines = new List<string>();
        var foodLines = new List<string>();
        var seen = new HashSet<ItemData>();
        if (inv != null)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                ItemStack s = inv.GetSlot(i);
                if (s == null || s.IsEmpty || !seen.Add(s.item)) continue;
                ItemData it = s.item;
                if (it.type == ItemData.ItemType.Spell)
                {
                    string effect = it.spellEffect == ItemData.SpellEffect.Heal
                        ? "heals " + it.healAmount + " health"
                        : "hits for " + it.spellDamage + " damage";
                    spellLines.Add("Your " + it.itemName + " costs " + Mathf.RoundToInt(it.magicCost) + " magic and " + effect + ".");
                }
                else if (it.type == ItemData.ItemType.Food && it.magicRestored > 0)
                    foodLines.Add(it.itemName + " gives back " + it.magicRestored + " magic");
            }
        }

        if (spellLines.Count == 0)
            pages.Add("You don't have a spell yet. When you find one, put it on your toolbar, select it and press Space to cast. I'll be watching from a safe distance.");
        else
            pages.Add(string.Join(" ", spellLines.ToArray()) + " Select it on your toolbar and press Space to cast. Ranged attacks: very dignified. Less touching.");

        if (foodLines.Count > 0)
            pages.Add("Food can top you up too: " + string.Join(", ", foodLines.ToArray()) + ".");

        pages.Add("A full night's sleep refills your health and magic. Naps are underrated. Take one.");
        return pages.ToArray();
    }

    public static string[] Crafting(PlayerActions p)
    {
        var pages = new List<string>();
        pages.Add("Crafting, for you humans, mostly means cooking. Take ingredients to the stove, pick a recipe, and out comes food. Meals restore health, and some restore magic too.");

        Inventory inv = p != null ? p.Inventory : null;
        var all = new List<Recipe>();
        foreach (CookingStation stove in Object.FindObjectsByType<CookingStation>())
            foreach (Recipe r in stove.Recipes) if (r != null && r.result != null && !all.Contains(r)) all.Add(r);

        if (all.Count == 0)
        {
            pages.Add("There aren't any recipes at the stove yet. Check back once someone teaches the kitchen a few.");
        }
        else
        {
            var ready = new List<Recipe>();
            foreach (Recipe r in all) if (r.CanCook(inv)) ready.Add(r);

            if (ready.Count > 0)
            {
                var names = new List<string>();
                foreach (Recipe r in ready) names.Add(r.recipeName);
                pages.Add("You can cook " + string.Join(", ", names.ToArray()) + " right now. Go on. I'll supervise from over here.");
            }

            // describe up to three recipes, the ones you can cook first
            var order = new List<Recipe>(ready);
            foreach (Recipe r in all) if (!order.Contains(r)) order.Add(r);
            var lines = new List<string>();
            for (int i = 0; i < Mathf.Min(3, order.Count); i++)
            {
                Recipe r = order[i];
                lines.Add(r.recipeName + ": needs " + r.IngredientText(inv) + ", makes " + r.resultCount + " " + r.result.itemName + ".");
            }
            pages.Add(string.Join("  ", lines.ToArray()));
        }

        pages.Add("Cooked meals remember how polluted their ingredients were, so grow clean crops for better food.");
        pages.Add("Want to upgrade the house? The design table uses gold and materials. Chop trees for wood, and the General Store sells building materials. Make it cozy. Cozy is important.");
        return pages.ToArray();
    }

    public static string[] Fishing(PlayerActions p, bool unlocked, string[] customPages)
    {
        var pages = new List<string>();
        if (!unlocked)
        {
            pages.Add("Fishing? Ahh. My favourite subject. Water, patience, and a very good chance of a snack.");
            pages.Add("But the ponds around here are quiet. The fish haven't moved in yet. Come back later and I'll tell you everything.");
            return pages.ToArray();
        }

        if (customPages != null) foreach (string s in customPages) if (!string.IsNullOrEmpty(s)) pages.Add(s);

        Inventory inv = p != null ? p.Inventory : null;
        bool hasRod = false;
        if (inv != null)
            for (int i = 0; i < Inventory.SlotCount && !hasRod; i++)
            {
                ItemStack s = inv.GetSlot(i);
                hasRod = s != null && !s.IsEmpty && s.item.type == ItemData.ItemType.Tool && s.item.toolAction == ItemData.ToolAction.FishingRod;
            }
        pages.Add(hasRod
            ? "You already have a Fishing Rod. Select it, face the water and press Space. Then wait. Waiting is a skill. I'm very skilled."
            : "You don't have a Fishing Rod yet. Get one first. Even I can't fish with my paws. (I've tried.)");
        return pages.ToArray();
    }
}
