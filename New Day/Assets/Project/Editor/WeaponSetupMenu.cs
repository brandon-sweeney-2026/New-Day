using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Combat > Set Up Weapons
///   1. finds the weapon pictures (sword_stone.png ... arrow_proj.png) anywhere in your Assets and sets them up as sprites;
///   2. makes 25 weapon items (Sword, Dagger, Flail, Hammer, Bow x Stone, Copper, Iron, Gold, Platinum) and an Arrow item, in Assets/Farm/Data/Weapons;
///   3. adds the WeaponController to the Player in the open scene.
/// Running it again only refreshes the pictures and keeps stats you changed.
/// Farm > Combat > Reset Weapon Stats To Defaults puts every weapon's numbers back to the defaults below.
/// Must live in a folder named "Editor".
/// </summary>
public static class WeaponSetupMenu
{
    private const string Folder = "Assets/Farm/Data/Weapons";
    private static readonly string[] Tiers = { "stone", "copper", "iron", "gold", "platinum" };
    private static readonly string[] TierNames = { "Stone", "Copper", "Iron", "Gold", "Platinum" };
    private static readonly string[] Kinds = { "sword", "dagger", "flail", "hammer", "bow" };
    private static readonly string[] KindNames = { "Sword", "Dagger", "Flail", "Hammer", "Bow" };

    // ------------------------------------------------------------------ the numbers (one value per tier: stone ... platinum)
    private static readonly int[] SwordDmg = { 6, 8, 11, 15, 20 };
    private static readonly float[] SwordSpeed = { 1.0f, 1.05f, 1.1f, 1.15f, 1.2f };
    private static readonly int[] DaggerDmg = { 4, 5, 7, 10, 13 };
    private static readonly float[] DaggerCrit = { 0.20f, 0.24f, 0.28f, 0.33f, 0.40f };
    private static readonly float[] DaggerSpeed = { 1.0f, 1.05f, 1.1f, 1.15f, 1.2f };
    private static readonly int[] FlailDmg = { 5, 7, 10, 14, 19 };
    private static readonly float[] FlailMaxSpin = { 560f, 600f, 640f, 680f, 720f };
    private static readonly float[] FlailSpinUp = { 1.4f, 1.3f, 1.2f, 1.1f, 1.0f };
    private static readonly int[] HammerDmg = { 12, 16, 22, 30, 40 };
    private static readonly float[] HammerKnock = { 14f, 15f, 16f, 17f, 18f };
    private static readonly float[] HammerSplash = { 1.3f, 1.4f, 1.5f, 1.6f, 1.8f };
    private static readonly float[] HammerSpeed = { 1.0f, 1.04f, 1.08f, 1.12f, 1.16f };
    private static readonly int[] BowDmg = { 6, 8, 11, 15, 20 };
    private static readonly float[] BowCharge = { 1.1f, 0.95f, 0.8f, 0.65f, 0.5f };
    private static readonly float[] BowSpeed = { 12f, 13f, 14f, 15f, 16f };
    private static readonly float[] BowCooldown = { 0.45f, 0.38f, 0.3f, 0.22f, 0.15f };
    private static readonly int[] Prices = { 20, 45, 80, 150, 300 };

    [MenuItem("Farm/Combat/Set Up Weapons")]
    public static void SetUp()
    {
        Run(false);
    }

    [MenuItem("Farm/Combat/Reset Weapon Stats To Defaults")]
    public static void ResetStats()
    {
        if (!EditorUtility.DisplayDialog("Reset weapon stats", "Put the damage, speed, critical chance and the other numbers of all 25 weapons back to the defaults? Pictures stay.", "Reset", "Cancel")) return;
        Run(true);
    }

    private static void Run(bool resetStats)
    {
        var missing = new System.Collections.Generic.List<string>();

        // 1. pictures
        foreach (string t in Tiers)
        {
            foreach (string k in Kinds) SetupSprite(k + "_" + t, k == "bow" ? Pivot.Center : Pivot.Grip, missing);
            SetupSprite("flailhandle_" + t, Pivot.Grip, missing);
            SetupSprite("flailball_" + t, Pivot.Center, missing);
            for (int d = 0; d < 3; d++) SetupSprite("bowheld_" + t + "_" + d, Pivot.Center, missing);
        }
        SetupSprite("arrow_proj", Pivot.Center, missing);
        SetupSprite("arrow_icon", Pivot.Center, missing);
        AssetDatabase.Refresh();

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Weapon pictures missing",
                "Copy the weapon PNGs (the Weapons folder from the zip) into your project's Assets first, then run this again.\n\nNot found: " +
                string.Join(", ", missing.Take(6).ToArray()) + (missing.Count > 6 ? " and " + (missing.Count - 6) + " more" : ""), "OK");
            return;
        }

        // 2. items
        EnsureFolder(Folder);
        ItemData arrow = LoadOrCreate("Arrow", resetStats);
        arrow.itemName = "Arrow"; arrow.type = ItemData.ItemType.Resource; arrow.maxStack = 99;
        arrow.description = "A plain arrow. Every bow shot uses one up.";
        arrow.sellPrice = 2; arrow.buyPrice = 6;
        arrow.icon = LoadSprite("arrow_icon");
        EditorUtility.SetDirty(arrow);

        for (int k = 0; k < Kinds.Length; k++)
            for (int t = 0; t < Tiers.Length; t++)
            {
                string assetName = TierNames[t] + KindNames[k];
                bool isNew;
                ItemData it = LoadOrCreate(assetName, resetStats, out isNew);
                bool setStats = isNew || resetStats;
                it.itemName = TierNames[t] + " " + KindNames[k];
                it.type = ItemData.ItemType.Weapon;
                it.maxStack = 1;
                it.weaponKind = (ItemData.WeaponKind)(k + 1);
                it.tier = (ItemData.WeaponTier)t;
                it.icon = LoadSprite(Kinds[k] + "_" + Tiers[t]);
                it.heldSprite = k == 2 ? LoadSprite("flailhandle_" + Tiers[t]) : null;
                it.ballSprite = k == 2 ? LoadSprite("flailball_" + Tiers[t]) : null;
                it.drawFrames = k == 4 ? new[] { LoadSprite("bowheld_" + Tiers[t] + "_0"), LoadSprite("bowheld_" + Tiers[t] + "_1"), LoadSprite("bowheld_" + Tiers[t] + "_2") } : new Sprite[0];
                if (k == 4) { it.ammo = arrow; it.projectileSprite = LoadSprite("arrow_proj"); }
                if (setStats) ApplyStats(it, k, t);
                EditorUtility.SetDirty(it);
            }
        AssetDatabase.SaveAssets();

        // 2b. the player's weapon animation pictures
        PlayerWeaponAnimSet animSet = BuildAnimSet(out string animMsg);

        // 3. the player
        string playerMsg;
        var pa = Object.FindAnyObjectByType<PlayerActions>();
        if (pa == null) playerMsg = "No Player found in the open scene. Open your farm scene and run this again to add the WeaponController.";
        else if (pa.GetComponent<WeaponController>() != null) playerMsg = "The Player already has the WeaponController.";
        else
        {
            Undo.AddComponent<WeaponController>(pa.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pa.gameObject.scene);
            playerMsg = "Added the WeaponController to the Player. Save the scene (Ctrl+S).";
        }
        if (pa != null && animSet != null)
        {
            var wc = pa.GetComponent<WeaponController>();
            var so = new SerializedObject(wc);
            so.FindProperty("animSet").objectReferenceValue = animSet;
            so.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pa.gameObject.scene);
        }
        playerMsg += "\n" + animMsg;

        EditorUtility.DisplayDialog("Weapons ready",
            "Made 25 weapons and the Arrow in " + Folder + ".\n\n" + playerMsg +
            "\n\nTo try them: Play, open Farm > Debug > Give Items, give yourself a weapon (and Arrows for the bow), equip it and press Space.\n" +
            "Sword/dagger/hammer: tap. Flail: hold to keep spinning. Bow: hold to draw, release to shoot.\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    private static void ApplyStats(ItemData it, int kind, int t)
    {
        it.sellPrice = Prices[t] * (kind == 3 ? 2 : 1); it.buyPrice = 0;
        it.critChance = 0.05f; it.critMultiplier = 2f; it.swingSpeed = 1f; it.moveSpeedWhileUsing = 1f;
        it.attackCooldown = 0.1f; it.hitDelay = -1f;
        switch (kind)
        {
            case 0: // SWORD: medium knockback, medium swing speed
                it.description = "Quick and light. Medium knockback.";
                it.damage = SwordDmg[t]; it.knockbackForce = 6f; it.swingSpeed = SwordSpeed[t];
                it.attackRange = 0.85f; it.attackRadius = 0.7f; it.swingDuration = 0.4f; it.attackCooldown = 0.06f;
                break;
            case 1: // DAGGER: short straight poke, small knockback, high crit chance
                it.description = "A fast, short poke. Small knockback, but strikes critically more often.";
                it.damage = DaggerDmg[t]; it.knockbackForce = 2.5f; it.swingSpeed = DaggerSpeed[t];
                it.critChance = DaggerCrit[t]; it.critMultiplier = 2.5f;
                it.attackRange = 0.85f; it.attackRadius = 0.38f; it.swingDuration = 0.24f; it.attackCooldown = 0.02f;
                break;
            case 2: // FLAIL: circles the player at 1.5 tiles, slow then fast, slows the player
                it.description = "Hold to swing a spiked ball in a circle. Starts slow, speeds up. You move slowly while it spins.";
                it.damage = FlailDmg[t]; it.knockbackForce = 8f;
                it.flailRadius = 1.5f; it.flailStartSpin = 110f; it.flailMaxSpin = FlailMaxSpin[t]; it.flailSpinUpTime = FlailSpinUp[t]; it.flailMaxSeconds = 3.5f;
                it.moveSpeedWhileUsing = 0.45f; it.attackCooldown = 0.3f;
                break;
            case 3: // HAMMER: slow over-the-top swing, big knockback, splash
                it.description = "A slow, heavy swing over the top. Huge knockback and splash damage around the hit.";
                it.damage = HammerDmg[t]; it.knockbackForce = HammerKnock[t]; it.swingSpeed = HammerSpeed[t];
                it.attackRange = 1.0f; it.attackRadius = 0.75f; it.splashRadius = HammerSplash[t]; it.splashDamagePercent = 0.6f;
                it.attackCooldown = 0.2f;
                break;
            case 4: // BOW: hold to draw, release to shoot, faster with each upgrade
                it.description = "Hold to draw, let go to shoot. Uses one Arrow per shot. Better bows draw and reload faster.";
                it.damage = BowDmg[t]; it.knockbackForce = 3f; it.chargeSeconds = BowCharge[t];
                it.projectileSpeed = BowSpeed[t]; it.projectileLifetime = 0.8f; it.moveSpeedWhileUsing = 0.4f;
                it.attackCooldown = BowCooldown[t]; it.critChance = 0f;
                break;
        }
    }

    // ------------------------------------------------------------------ player weapon animation sheet
    /// <summary>Slices player_weapon_sheet.png into 24x32 cells (bottom-centre pivot, same as the player) and makes the PlayerWeaponAnimSet asset.</summary>
    private static PlayerWeaponAnimSet BuildAnimSet(out string msg)
    {
        string path = FindTexture("player_weapon_sheet");
        if (path == null) { msg = "player_weapon_sheet.png was not found - copy it into Assets and run this again."; return null; }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.isReadable = true;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cw = PlayerWeaponFrames.CellWidth, ch = PlayerWeaponFrames.CellHeight, cols = PlayerWeaponFrames.Columns;
        int rows = tex.height / ch;
        var sheetData = new List<SpriteMetaData>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var m = new SpriteMetaData();
                m.name = "pw_" + r + "_" + c;
                m.rect = new Rect(c * cw, tex.height - (r + 1) * ch, cw, ch);   // row 0 is at the top of the picture
                m.alignment = (int)SpriteAlignment.BottomCenter;
                m.pivot = new Vector2(0.5f, 0f);
                sheetData.Add(m);
            }
#pragma warning disable 618
        imp.spritesheet = sheetData.ToArray();
#pragma warning restore 618
        imp.SaveAndReimport();

        var byName = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (o is Sprite sp) byName[sp.name] = sp;

        EnsureFolder(Folder);
        string assetPath = Folder + "/PlayerWeaponAnimSet.asset";
        var set = AssetDatabase.LoadAssetAtPath<PlayerWeaponAnimSet>(assetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<PlayerWeaponAnimSet>(); AssetDatabase.CreateAsset(set, assetPath); }
        set.sprites = new Sprite[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Sprite sp;
                byName.TryGetValue("pw_" + r + "_" + c, out sp);
                set.sprites[r * cols + c] = sp;
            }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        msg = "Sliced the player weapon animations (" + rows + " rows) into " + assetPath + " and linked them.";
        return set;
    }

    // ------------------------------------------------------------------ helpers
    private enum Pivot { Grip, Center }

    private static void SetupSprite(string name, Pivot pivot, System.Collections.Generic.List<string> missing)
    {
        string path = FindTexture(name);
        if (path == null) { missing.Add(name); return; }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(s);
        imp.spritePivot = pivot == Pivot.Grip ? new Vector2(2.5f / 24f, 2.5f / 24f) : new Vector2(0.5f, 0.5f);   // the grip sits at the bottom-left of the weapon pictures
        imp.SaveAndReimport();
    }

    private static string FindTexture(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    private static Sprite LoadSprite(string name)
    {
        string p = FindTexture(name);
        return p == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    private static ItemData LoadOrCreate(string assetName, bool reset)
    {
        bool isNew;
        return LoadOrCreate(assetName, reset, out isNew);
    }

    private static ItemData LoadOrCreate(string assetName, bool reset, out bool isNew)
    {
        string path = Folder + "/" + assetName + ".asset";
        var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        isNew = it == null;
        if (it == null)
        {
            it = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(it, path);
        }
        return it;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
