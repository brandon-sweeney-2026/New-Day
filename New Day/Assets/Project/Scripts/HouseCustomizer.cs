using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The design table inside the house. Press the action button in front of it to change the house:
/// pick a category (floor, walls, siding, roof, bed, kitchen...), then buy or switch to an option.
/// Buying uses gold and materials from your backpack.
/// Needs a Collider2D (a trigger works well) and a HouseManager in the scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseCustomizer : MonoBehaviour, IInteractable
{
    private static readonly string[] CategoryNames =
    {
        "Floor", "Walls", "Siding (outside walls)", "Roof", "Bed", "Kitchen", "Table", "Rug", "Bookshelf", "Plant", "Lamp", "Wall art"
    };

    public string InteractPrompt { get { return "Design your house"; } }

    private PlayerActions player;

    public void Interact(PlayerActions p)
    {
        if (HouseManager.Instance == null)
        {
            GameEvents.ShowToast("There is no HouseManager in the scene.");
            return;
        }
        player = p;
        MenuUI.Open("House Design", BuildCategories, "Up/Down: choose     Space: open     Esc: close");
    }

    private List<MenuRow> BuildCategories()
    {
        HouseManager house = HouseManager.Instance;
        var rows = new List<MenuRow>();

        var categories = (HouseCategory[])System.Enum.GetValues(typeof(HouseCategory));
        for (int i = 0; i < categories.Length; i++)
        {
            HouseCategory cat = categories[i];
            HouseUpgradeData current = house.Selected(cat);
            int owned = 0, total = 0;
            foreach (HouseUpgradeData o in house.Options(cat)) { total++; if (house.IsOwned(o)) owned++; }

            rows.Add(new MenuRow
            {
                label = CategoryNames[i],
                right = (current != null ? current.displayName : "-") + "   (" + owned + "/" + total + ")",
                detail = "Gold: " + GameState.Gold + "g",
                onSelect = () => OpenCategory(cat, CategoryNames[(int)cat])
            });
        }

        rows.Add(new MenuRow { label = "Close", onSelect = MenuUI.CloseAll });
        return rows;
    }

    private void OpenCategory(HouseCategory cat, string title)
    {
        MenuUI.Open(title, () => BuildOptions(cat), "Space: buy / use     Esc: back");
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
