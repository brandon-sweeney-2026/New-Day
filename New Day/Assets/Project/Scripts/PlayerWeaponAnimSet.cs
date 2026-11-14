using UnityEngine;

/// <summary>
/// Holds the sliced pictures of the player weapon sheet (player_weapon_sheet.png), in sheet order (row by row from the top).
/// Made by Farm > Combat > Set Up Weapons. WeaponController reads it. The frame data (timings, where the hand is) is in PlayerWeaponFrames.
/// </summary>
public class PlayerWeaponAnimSet : ScriptableObject
{
    public Sprite[] sprites;

    public Sprite Get(int row, int col)
    {
        int i = row * PlayerWeaponFrames.Columns + col;
        return sprites != null && i >= 0 && i < sprites.Length ? sprites[i] : null;
    }
}
