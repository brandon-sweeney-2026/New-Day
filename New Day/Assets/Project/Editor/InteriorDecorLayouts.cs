using UnityEngine;
using C = DecorCategory;
using F = DecorFacing;

/// <summary>
/// The STARTING furnishing of every room, written in code. Used only when a room has no layout asset yet
/// (Assets/Farm/Interiors/Layouts/&lt;room id&gt;.asset): once the asset exists, your changes in the Interior Decorator win.
/// Delete a layout asset and run Farm > Set Up > Interior Decor (or rebuild the scene) to get the starting version back.
///
/// Every piece is "the [category] of this room's theme", so changing the theme gives a matching room.
/// Positions are the bottom-left corner of the piece's floor space, in tiles from the room's bottom-left corner.
/// The walk lines the neighbours use (their path nodes) are kept clear.
/// This file must live in a folder named "Editor".
/// </summary>
public static class InteriorDecorLayouts
{
    /// <summary>The theme each suburb house gets: house 1 Cottage, 2 Modern, 3 Rustic, 4 Retro, 5 Coastal, 6 Cottage...</summary>
    public static string ThemeForHouse(int n) { return DecorCatalog.Themes[Mathf.Abs(n - 1) % DecorCatalog.Themes.Length]; }

    // ------------------------------------------------------------------ suburb houses (SuburbHousesMenu layouts)

    /// <summary>"Cosy": 14 x 10. Kitchen top-left, bedroom top-right, bathroom bottom-right, living room bottom-left.</summary>
    public static void Cosy(LayoutBuilder b, bool mirror)
    {
        b.Floor(b.ThemeFloor).WallCovering(b.ThemeWall).Zone("Kitchen", b.ThemeKitchenFloor).Zone("Bath", "floor_mosaic").Zone("Bedroom", "floor_carpet");
        // kitchen along the back wall
        b.Add(C.Fridge, 0, 9).Add(C.KitchenCounter, 1, 9).Add(C.Sink, 3, 9).Add(C.Oven, 5, 9)
         .Add(C.Blender, 1, 9).Add(C.SmallAppliance, 2, 9)
         .Add(C.KitchenTable, 0, 5).Add(C.Chair, 2, 6, F.Left)
         .Wall(C.Window, 3, 0.5f).WallItem("utensil_rail", 5, 1.2f);
        // living room: sofa looking down at the TV, which you see from behind
        b.Add(C.Rug, 1, 1).Add(C.Couch, 1, 3).Add(C.CoffeeTable, 1, 2).Add(C.TV, 1, 0, F.Back).Add(C.Armchair, 4, 3, F.Left)
         .Add(C.FloorLamp, 0, 3).Add(C.Plant, 0, 0);
        // bedroom
        b.Add(C.Bed, 10, 7).Add(C.SideTable, 12, 9).Add(C.Dresser, 13, 5, F.Left).Add(C.Plant, 8, 9).Add(C.FloorLamp, 9, 9)
         .Wall(C.Window, 12, 0.5f);
        // bathroom
        b.Item("builtin_tub", 11, 3).Item("pedestal_sink", 11, 0).Item("toilet", 13, 0).Item("bath_mat", 11, 1).Add(C.Washer, 10, 3);
        if (mirror) b.Mirror();
    }

    /// <summary>"Family": 20 x 14. Living room bottom-left, kitchen top-left, hall in the middle, bathroom and two bedrooms on the right.</summary>
    public static void Family(LayoutBuilder b)
    {
        b.Floor(b.ThemeFloor).WallCovering(b.ThemeWall).Zone("Kitchen", b.ThemeKitchenFloor).Zone("Bath", "floor_mosaic")
         .Zone("Bedroom A", "floor_carpet").Zone("Bedroom B", "floor_carpet");
        // living room
        b.Add(C.Rug, 0, 2).Add(C.Couch, 0, 5).Add(C.CoffeeTable, 0, 3).Add(C.TV, 0, 1, F.Back).Add(C.Armchair, 3, 3, F.Left)
         .Add(C.FloorLamp, 3, 5).Add(C.Plant, 3, 0)
         .Add(C.Armchair, 7, 4).Add(C.SideTable, 8, 4).Add(C.FloorLamp, 9, 5).Add(C.Plant, 9, 0);
        // kitchen
        b.Add(C.KitchenCounter, 0, 13).Add(C.Sink, 2, 13).Add(C.KitchenCounter, 4, 13).Add(C.Oven, 6, 13).Add(C.Fridge, 8, 13).Add(C.Plant, 9, 13)
         .Add(C.Blender, 0, 13).Add(C.SmallAppliance, 4, 13).Item("toaster_retro", 5, 13)
         .Add(C.KitchenTable, 1, 9).Add(C.Chair, 0, 9, F.Right).Add(C.Chair, 3, 9, F.Left).Add(C.Chair, 1, 8, F.Back).Add(C.Chair, 2, 8, F.Back)
         .Wall(C.Window, 2, 0.5f).WallItem("utensil_rail", 6, 1.2f).WallItem("clock", 9, 1.5f);
        // hall: laundry, a desk and a bookshelf
        b.Add(C.Washer, 13, 13).Add(C.Dryer, 14, 13).Add(C.ComputerDesk, 11, 7, F.Right).Add(C.Bookshelf, 11, 4, F.Right).Add(C.Plant, 11, 0)
         .Wall(C.Window, 12, 0.5f);
        // bathroom
        b.Item("builtin_tub", 17, 3).Item("toilet", 19, 0).Item("vanity", 16, 0).Item("bath_mat", 17, 1);
        // bedrooms
        b.Add(C.Bed, 18, 6).Add(C.SideTable, 17, 8);
        b.Add(C.Bed, 18, 11).Add(C.SideTable, 17, 13).Add(C.Plant, 16, 13).Wall(C.Window, 16.5f, 0.5f);
    }

    /// <summary>"Loft": 12 x 12. Bathroom bottom-left, kitchen top-left, sleeping corner top-right, living area bottom-right.</summary>
    public static void Loft(LayoutBuilder b)
    {
        b.Floor(b.ThemeFloor).WallCovering(b.ThemeWall).Zone("Kitchen", b.ThemeKitchenFloor).Zone("Bath", "floor_mosaic").Zone("Sleeping", "floor_carpet");
        // kitchen
        b.Add(C.Fridge, 0, 11).Add(C.KitchenCounter, 1, 11).Add(C.Sink, 3, 11).Add(C.Oven, 5, 11).Add(C.Blender, 2, 11)
         .Add(C.KitchenTable, 1, 7).Add(C.Chair, 0, 7, F.Right).Add(C.Chair, 3, 7, F.Left)
         .Wall(C.Window, 2, 0.5f);
        // living area: the TV against the path side, sofa against the right wall
        b.Add(C.Rug, 8, 1).Add(C.TV, 7, 2, F.Right).Add(C.Couch, 11, 1, F.Left).Add(C.CoffeeTable, 9, 2, F.Right).Add(C.Armchair, 9, 4, F.Left)
         .Add(C.FloorLamp, 11, 4).Add(C.Plant, 11, 0);
        // sleeping corner
        b.Add(C.Bed, 9, 9).Add(C.SideTable, 11, 11).Add(C.Dresser, 11, 6, F.Left).Add(C.FloorLamp, 8, 11).Add(C.Plant, 7, 11)
         .Wall(C.Picture, 7, 1f);
        // bathroom with the laundry
        b.Item("builtin_tub", 0, 3).Item("toilet", 0, 0).Add(C.Washer, 2, 0).Add(C.Dryer, 3, 0);
    }

    // ------------------------------------------------------------------ the Kims' apartment above the General Store (20 x 20)

    public static void KimApartment(LayoutBuilder b)
    {
        b.Floor(b.ThemeFloor).WallCovering(b.ThemeWall).Zone("Kitchen", "floor_checker").Zone("Bathroom 1", "floor_mosaic").Zone("Bathroom 2", "floor_mosaic")
         .Zone("Bedroom 1", "floor_carpet").Zone("Bedroom 2", "floor_carpet").Zone("Bedroom 3", "floor_carpet");
        // living room (the stairs and the middle, x 9 - 11, stay clear)
        b.Add(C.Rug, 1, 3).Add(C.Couch, 1, 6).Add(C.CoffeeTable, 1, 4).Add(C.TV, 1, 1, F.Back).Add(C.Armchair, 4, 4, F.Left)
         .Add(C.Fireplace, 0, 3, F.Right).Add(C.FloorLamp, 0, 6).Add(C.Plant, 0, 0).Add(C.Bookshelf, 7, 0, F.Back).Add(C.Plant, 8, 7);
        // kitchen along the inside wall
        b.Add(C.KitchenCounter, 11, 7).Add(C.Oven, 13, 7).Add(C.Sink, 15, 7).Add(C.KitchenCounter, 17, 7).Add(C.Fridge, 19, 7)
         .Add(C.Blender, 11, 7).Item("kettle_cottage", 18, 7).Item("microwave_modern", 12, 7)
         .Add(C.KitchenTable, 14, 3).Add(C.Chair, 13, 3, F.Right).Add(C.Chair, 16, 3, F.Left).Add(C.Chair, 15, 2, F.Back)
         .Add(C.Washer, 19, 2, F.Left).Add(C.Dryer, 19, 1, F.Left);
        // hall
        b.Add(C.Plant, 6, 12).Add(C.Plant, 13, 12).Add(C.Bookshelf, 7, 13).Add(C.SideTable, 12, 13);
        // bathrooms
        b.Item("builtin_tub", 0, 13).Item("pedestal_sink", 1, 9).Item("toilet", 3, 9);
        b.Item("shower", 18, 12).Item("vanity", 16, 9).Item("toilet", 19, 9);
        // bedrooms (parents, then the two kids)
        b.Add(C.Bed, 3, 17).Add(C.SideTable, 2, 19).Add(C.SideTable, 5, 19).Add(C.Dresser, 0, 15, F.Right).Add(C.Plant, 6, 15);
        b.Add(C.Bed, 8, 17).Add(C.ComputerDesk, 11, 19).Add(C.Chair, 11, 18, F.Back).Add(C.Plant, 13, 19);
        b.Add(C.Bed, 16, 17).Add(C.Bookshelf, 19, 15, F.Left).Add(C.SideTable, 18, 19);
        // back wall
        b.Wall(C.Window, 0, 0.5f).WallItem("picture_photos", 5, 1f).Wall(C.Window, 11, 0.5f).Wall(C.Window, 17, 0.5f);
    }

    // ------------------------------------------------------------------ the farmhouse (20 x 20; its bed, stove, slots and design table stay)

    public static void Farmhouse(LayoutBuilder b)
    {
        if (b.W < 16) { SmallFarmhouse(b); return; }
        // living corner by a fireplace on the left wall
        b.Add(C.Fireplace, 0, 6, F.Right).Add(C.Rug, 1, 5).Add(C.Couch, 4, 5, F.Left).Add(C.CoffeeTable, 2, 6, F.Right)
         .Add(C.FloorLamp, 4, 8).Add(C.SideTable, 4, 4).Add(C.Armchair, 1, 3, F.Back).Add(C.Plant, 0, 0).Add(C.Bookshelf, 0, 12, F.Right);
        // kitchen next to the stove
        b.Add(C.Fridge, 11, 17).Add(C.KitchenCounter, 12, 17).Add(C.Sink, 16, 17).Add(C.Blender, 12, 17).Add(C.SmallAppliance, 13, 17)
         .Add(C.KitchenTable, 13, 13).Add(C.Chair, 12, 13, F.Right).Add(C.Chair, 15, 13, F.Left).Add(C.Chair, 13, 12, F.Back).Add(C.Chair, 14, 12, F.Back);
        // laundry and a desk on the right
        b.Add(C.Washer, 19, 10, F.Left).Add(C.Dryer, 19, 9, F.Left).Add(C.ComputerDesk, 19, 5, F.Left).Add(C.Plant, 19, 0).Add(C.Plant, 0, 15);
        // back wall
        b.Wall(C.Window, 0, 0.5f).Wall(C.Window, 16, 0.5f).WallItem("clock", 12, 1.5f).WallItem("wall_shelf", 4, 1.2f);
    }

    /// <summary>
    /// The 10 x 10 farmhouse (the one in the Farm scene): bed top-left, stove bottom-left, design table and table on the left wall.
    /// A fireplace on the back wall with a sofa looking at it, a kitchen run along the right wall, the laundry above it.
    /// The way in from the door (x 6 - 8) stays clear.
    /// </summary>
    private static void SmallFarmhouse(LayoutBuilder b)
    {
        b.Add(C.Fireplace, 3, 9).Add(C.Rug, 3, 5).Add(C.Couch, 3, 4, F.Back).Add(C.CoffeeTable, 4, 6).Add(C.Armchair, 6, 6, F.Left).Add(C.FloorLamp, 6, 4);
        b.Add(C.KitchenCounter, 9, 1, F.Left).Add(C.Sink, 9, 3, F.Left).Add(C.Fridge, 9, 5, F.Left).Add(C.Blender, 9, 1).Add(C.Plant, 9, 0)
         .Add(C.Washer, 9, 6, F.Left).Add(C.Dryer, 9, 7, F.Left);
        b.Wall(C.Window, 5, 0.5f).WallItem("clock", 8, 1.5f);
    }
}
