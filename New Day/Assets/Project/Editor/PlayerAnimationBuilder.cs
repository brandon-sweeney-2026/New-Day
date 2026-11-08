using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Turns a player sprite sheet into 8 animations (Idle and Walk, facing Down / Up / Left / Right),
/// builds the Animator Controller for them, and attaches everything to your Player.
///
/// Menu: Farm > Player > Build Animations From Sheet
///
/// Sheet layout (read from the TOP, each cell 16 x 24 pixels):
///   row 1  Idle Down   (2 frames)      row 5  Walk Down   (4 frames)
///   row 2  Idle Up     (2 frames)      row 6  Walk Up     (4 frames)
///   row 3  Idle Left   (2 frames)      row 7  Walk Left   (4 frames)
///   row 4  Idle Right  (2 frames)      row 8  Walk Right  (4 frames)
///   row 9-12  Use Tool Down / Up / Left / Right (4 frames each, played once when you use a tool)
///
/// Select the sheet PNG in the Project window first (or it looks for one called player_sheet).
/// This file must live in a folder named "Editor".
/// </summary>
public static class PlayerAnimationBuilder
{
    private const int CellW = 16;
    private const int CellH = 24;
    private const float PixelsPerUnit = 16f;
    private const float IdleFps = 2f;
    private const float WalkFps = 8f;
    private const float UseFps = 12f;   // 4 frames at 12 fps = 1/3 second, matching PlayerActions "Tool Duration"

    // Order of the rows in the sheet.
    private static readonly string[] Directions = { "Down", "Up", "Left", "Right" };
    private static readonly Vector2[] DirectionVectors =
    {
        new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f)
    };

    [MenuItem("Farm/Player/Build Animations From Sheet")]
    public static void Build()
    {
        string sheetPath = FindSheetPath();
        if (sheetPath == null)
        {
            EditorUtility.DisplayDialog("Select the sheet",
                "Click your player sprite sheet (the PNG) in the Project window, then run this menu again.", "OK");
            return;
        }

        SliceSheet(sheetPath);

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
        var byName = new Dictionary<string, Sprite>();
        foreach (Sprite s in sprites) byName[s.name] = s;

        string baseName = Path.GetFileNameWithoutExtension(sheetPath);
        string folder = Path.GetDirectoryName(sheetPath).Replace('\\', '/') + "/PlayerAnimations";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(sheetPath).Replace('\\', '/'), "PlayerAnimations");

        // ---- clips ----
        var idle = new AnimationClip[4];
        var walk = new AnimationClip[4];
        var use = new AnimationClip[4];
        for (int d = 0; d < 4; d++)
        {
            idle[d] = MakeClip(folder, "Idle_" + Directions[d], FrameSprites(byName, baseName, d, 2), IdleFps);
            walk[d] = MakeClip(folder, "Walk_" + Directions[d], FrameSprites(byName, baseName, 4 + d, 4), WalkFps);
            use[d] = MakeClip(folder, "UseTool_" + Directions[d], FrameSprites(byName, baseName, 8 + d, 4), UseFps, false);
        }

        // ---- controller ----
        string controllerPath = folder + "/PlayerAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null) AssetDatabase.DeleteAsset(controllerPath);   // rebuilt from scratch
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        // These triggers are used by PlayerActions. Add states for them when you have attack / tool / cast animations.
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("UseTool", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        AnimatorState walkState = machine.AddState("Walk");
        AnimatorState useState = machine.AddState("UseTool");
        idleState.motion = MakeBlendTree(controller, "Idle Directions", idle);
        useState.motion = MakeBlendTree(controller, "UseTool Directions", use);
        walkState.motion = MakeBlendTree(controller, "Walk Directions", walk);
        machine.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        // Using a tool: jump in from any state when the UseTool trigger fires, play once, then return to idle.
        AnimatorStateTransition toUse = machine.AddAnyStateTransition(useState);
        toUse.hasExitTime = false;
        toUse.duration = 0f;
        toUse.canTransitionToSelf = false;
        toUse.AddCondition(AnimatorConditionMode.If, 0f, "UseTool");

        AnimatorStateTransition fromUse = useState.AddTransition(idleState);
        fromUse.hasExitTime = true;
        fromUse.exitTime = 1f;
        fromUse.duration = 0f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // ---- attach to the player ----
        string result = AttachToPlayer(controller, byName.ContainsKey(baseName + "_0_0") ? byName[baseName + "_0_0"] : null);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Animations built",
            "Created 12 clips (idle, walk and use-tool, 4 directions each) and an Animator Controller in:\n" + folder + "\n\n" + result, "OK");
    }

    // ------------------------------------------------------------------
    // Pieces
    // ------------------------------------------------------------------

    private static string FindSheetPath()
    {
        if (Selection.activeObject is Texture2D)
        {
            string p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(p)) return p;
        }

        foreach (string guid in AssetDatabase.FindAssets("player_sheet t:Texture2D"))
            return AssetDatabase.GUIDToAssetPath(guid);

        return null;
    }

    /// <summary>Slices the PNG into 16x24 cells with a bottom-centre pivot, named name_column_row (row counted from the top).</summary>
    private static void SliceSheet(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width / CellW;
        int rows = tex.height / CellH;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = baseName + "_" + c + "_" + r;
                rect.rect = new Rect(c * CellW, tex.height - (r + 1) * CellH, CellW, CellH);
                rect.alignment = SpriteAlignment.BottomCenter;
                rect.pivot = new Vector2(0.5f, 0f);
                rect.spriteID = GUID.Generate();
                rects.Add(rect);
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        idProvider.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static List<Sprite> FrameSprites(Dictionary<string, Sprite> byName, string baseName, int row, int count)
    {
        var list = new List<Sprite>();
        for (int c = 0; c < count; c++)
        {
            Sprite s;
            if (byName.TryGetValue(baseName + "_" + c + "_" + row, out s)) list.Add(s);
        }
        return list;
    }

    private static AnimationClip MakeClip(string folder, string name, List<Sprite> frames, float fps, bool loop = true)
    {
        string path = folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = fps;

        // One keyframe per sprite, plus a repeat of the first at the end so the last frame lasts its full time.
        var keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Count] = new ObjectReferenceKeyframe { time = frames.Count / fps, value = frames.Count > 0 ? frames[0] : null };

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static BlendTree MakeBlendTree(AnimatorController controller, string name, AnimationClip[] clips)
    {
        var tree = new BlendTree();
        tree.name = name;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.useAutomaticThresholds = false;

        for (int d = 0; d < 4; d++) tree.AddChild(clips[d], DirectionVectors[d]);

        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static string AttachToPlayer(AnimatorController controller, Sprite firstSprite)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null)
            return "No Player found in the open scene, so nothing was attached.\nAdd an Animator to your player and drag the controller into its Controller slot.";

        // The Animator goes on the same object as the SpriteRenderer (the clips animate that renderer).
        SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(player.gameObject);

        Animator animator = sr.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(sr.gameObject);

        Undo.RecordObject(animator, "Assign controller");
        animator.runtimeAnimatorController = controller;

        Undo.RecordObject(sr, "Player sprite");
        if (firstSprite != null) sr.sprite = firstSprite;
        sr.sortingOrder = 10;

        if (player.GetComponent<PlayerAnimator>() == null) Undo.AddComponent<PlayerAnimator>(player.gameObject);
        if (player.GetComponent<HeldItemVisual>() == null && player.GetComponent<PlayerActions>() != null)
            Undo.AddComponent<HeldItemVisual>(player.gameObject);

        EditorUtility.SetDirty(player.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

        return "Attached to '" + player.name + "': SpriteRenderer, Animator and PlayerAnimator.\n" +
               "The sprite's pivot is at the FEET, so the player's position is where it stands.";
    }
}
