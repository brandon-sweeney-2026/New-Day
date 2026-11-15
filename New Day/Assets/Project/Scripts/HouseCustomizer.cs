using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The design table inside the house. Press the action button in front of it to change the house:
/// pick a category (floor, walls, siding, roof, stove, windows...), then buy or switch to an option.
/// Buying uses gold and materials from your backpack.
/// If the HouseManager has 'Buy Only At Hardware Store' ticked, new things can only be BOUGHT at Frank's, but you can still
/// switch between things you own here.
/// Needs a Collider2D (a trigger works well) and a HouseManager in the scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseCustomizer : MonoBehaviour, IInteractable
{
    private static readonly string[] CategoryNames =
    {
        "Floor", "Walls", "Siding (outside walls)", "Roof", "Bed", "Stove / kitchen", "Table", "Rug", "Bookshelf", "Plant", "Lamp", "Wall art",
        "Front door", "Shutters", "Kitchen cabinets", "Window style", "Window layout"
    };

    public string InteractPrompt { get { return "Design your house"; } }

    public void Interact(PlayerActions p)
    {
        HouseManager hm = HouseManager.Instance;
        OpenCategories(p, "House Design", null, hm == null || !hm.buyOnlyAtHardwareStore);
    }

    /// <summary>
    /// Opens the upgrade menus for the given categories (null or empty = all of them). Used by this design table and by Frank's
    /// store displays. 'allowBuying' false = you can only switch between things you already own.
    /// </summary>
    public static void OpenCategories(PlayerActions p, string title, IList<HouseCategory> only, bool allowBuying)
    {
        if (HouseManager.Instance == null) { GameEvents.ShowToast("There is no HouseManager in the scene."); return; }
        var menus = new Menus { player = p, only = only, allowBuying = allowBuying };
        MenuUI.Open(title, menus.BuildCategories, "Up/Down: choose     Space: open     Esc: close");
    }

    private class Menus
    {
        public PlayerActions player;
        public IList<HouseCategory> only;
        public bool allowBuying;

        public List<MenuRow> BuildCategories()
        {
            HouseManager house = HouseManager.Instance;
            var rows = new List<MenuRow>();

            var categories = (HouseCategory[])System.Enum.GetValues(typeof(HouseCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                HouseCategory cat = categories[i];
                if (only != null && only.Count > 0 && !only.Contains(cat)) continue;

                int owned = 0, total = 0;
                foreach (HouseUpgradeData o in house.Options(cat)) { total++; if (house.IsOwned(o)) owned++; }
                if (total == 0) continue;              // nothing exists for this category yet
                HouseUpgradeData current = house.Selected(cat);
                string title = CategoryNames[(int)cat];

                rows.Add(new MenuRow
                {
                    label = title,
                    right = (current != null ? current.displayName : "-") + "   (" + owned + "/" + total + ")",
                    detail = "Gold: " + GameState.Gold + "g",
                    onSelect = () => MenuUI.Open(title, () => BuildOptions(cat), "Space: buy / use     Esc: back")
                });
            }

            if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing available yet)", enabled = false });
            rows.Add(new MenuRow { label = "Close", onSelect = MenuUI.CloseAll });
            return rows;
        }

        private List<MenuRow> BuildOptions(HouseCategory cat)
        {
            HouseManager house = HouseManager.Instance;
            Inventory inv = player != null ? player.Inventory : null;
            var rows = new List<MenuRow>();

            foreach (HouseUpgradeData option in house.Options(cat))
            {
                HouseUpgradeData o = option;
                bool owned = house.IsOwned(o);
                bool inUse = house.IsSelected(o);

                string missing;
                bool canBuy = house.CanAfford(o, inv, out missing);

                var row = new MenuRow { label = o.displayName };

                if (inUse)
                {
                    row.right = "In use";
                    row.color = new Color(0.6f, 1f, 0.6f);
                    row.detail = o.description;
                }
                else if (owned)
                {
                    row.right = "Owned";
                    row.detail = o.description;
                    row.onSelect = () => house.Select(o);
                }
                else if (!allowBuying)
                {
                    row.right = o.CostText();
                    row.enabled = false;
                    row.detail = o.description + "\nBuy this at Frank's Home & Hardware.";
                }
                else
                {
                    row.right = o.CostText();
                    row.enabled = canBuy;
                    row.color = canBuy ? (Color?)new Color(1f, 0.9f, 0.5f) : null;
                    row.detail = o.description + (canBuy ? "" : "\nStill needed: " + missing);
                    row.onSelect = () => house.TryBuy(o, inv);
                }
                rows.Add(row);
            }

            if (rows.Count == 0) rows.Add(new MenuRow { label = "(no options yet)", enabled = false });
            return rows;
        }
    }
}
