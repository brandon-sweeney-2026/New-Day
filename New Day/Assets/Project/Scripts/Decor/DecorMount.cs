/// <summary>
/// Where a decor piece goes:
///  Floor   - stands on the floor and blocks the player (sofas, tables, fridges)
///  Flat    - lies flat on the floor, you walk over it (rugs, bath mats)
///  Surface - sits on top of another piece (a blender on a counter, a toaster)
///  Wall    - hangs on the back wall (windows, pictures, clocks); these only have a front
/// </summary>
public enum DecorMount { Floor, Flat, Surface, Wall }
