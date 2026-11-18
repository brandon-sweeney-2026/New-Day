/// <summary>
/// The kinds of things in the interior decor catalogue. Each category has (at least) one piece per theme
/// (Cottage, Modern, Rustic, Retro, Coastal), so a whole room can be furnished in one matching style.
/// New categories go at the END so saved assets keep their category.
/// </summary>
public enum DecorCategory
{
    Couch, Armchair, Ottoman, Fireplace, TV, ComputerDesk, SideTable, CoffeeTable, Chair, KitchenTable,
    Rug, Plant, FloorLamp, Bookshelf, Bed, Dresser,
    KitchenCounter, Sink, Oven, Fridge, Washer, Dryer, Blender, SmallAppliance,
    Bathroom,
    Picture, Window, WallDecor,
    Flooring, WallCovering
}
