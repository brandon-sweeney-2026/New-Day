using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The mine cart in the hub. Press the action button next to it to ride straight to a floor you have already reached.
/// It offers every 5th floor (5, 10, 15 ...) up to the deepest floor reached; the "5" is 'Cart Every N Floors' in the mine's Config asset.
///
/// The little menu is made by this script, so there is nothing to set up:
///   Left / Right (or A / D)  = choose a floor        Space / Enter / E = go        Esc / Q = cancel
/// Needs a Collider2D (the setup menu adds one).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MineCart : MonoBehaviour, IInteractable
{
    public string prompt = "Ride the mine cart";
    [TextArea] public string nothingYetMessage = "The cart can take you to every {0}th floor you have reached. Get down to floor {0} first.";

    private Canvas canvas;
    private Text floorText, themeText;
    private bool open;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions player)
    {
        MineShaftManager mine = MineShaftManager.Instance;
        if (open || mine == null || mine.config == null) return;

        List<int> floors = OfferedFloors(mine.config);
        if (floors.Count == 0)
        {
            GameEvents.ShowToast(string.Format(nothingYetMessage, mine.config.cartEveryNFloors));
            return;
        }
        StartCoroutine(Choose(mine, floors));
    }

    /// <summary>Every Nth floor the player has reached, shallowest first.</summary>
    public static List<int> OfferedFloors(MineShaftConfig config)
    {
        var list = new List<int>();
        int step = Mathf.Max(1, config.cartEveryNFloors);
        int deepest = Mathf.Min(GameState.GetCounter(MineShaftManager.DeepestFloorKey), config.floorCount);
        for (int f = step; f <= deepest; f += step) list.Add(f);
        return list;
    }

    private IEnumerator Choose(MineShaftManager mine, List<int> floors)
    {
        open = true;
        GameState.LockInput();
        if (canvas == null) BuildMenu();
        canvas.gameObject.SetActive(true);
        int index = floors.Count - 1;                   // start on the deepest one
        int chosen = -1;
        yield return null;                              // so the key press that opened the menu isn't read again

        while (true)
        {
            int floor = floors[index];
            MineTheme theme = mine.config.ThemeFor(floor);
            floorText.text = (index > 0 ? "<   " : "    ") + "Floor " + floor + (index < floors.Count - 1 ? "   >" : "    ");
            themeText.text = theme != null ? theme.themeName : "";

            Keyboard kb = Keyboard.current; Gamepad gp = Gamepad.current;
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (gp != null && gp.dpad.left.wasPressedThisFrame);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (gp != null && gp.dpad.right.wasPressedThisFrame);
            bool go = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            bool cancel = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) || (gp != null && gp.buttonEast.wasPressedThisFrame);

            if (left) index = Mathf.Max(0, index - 1);
            if (right) index = Mathf.Min(floors.Count - 1, index + 1);
            if (go) { chosen = floors[index]; break; }
            if (cancel) break;
            yield return null;
        }

        canvas.gameObject.SetActive(false);
        yield return null;
        GameState.UnlockInput();
        open = false;
        if (chosen > 0) mine.GoToFloor(chosen);
    }

    // The menu: a dark panel in the middle of the screen with three lines of text.
    private void BuildMenu()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cg = new GameObject("MineCartMenu");
        cg.transform.SetParent(transform, false);
        canvas = cg.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 235;
        var scaler = cg.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        panel.transform.SetParent(cg.transform, false);
        panel.color = new Color(0.08f, 0.07f, 0.1f, 0.9f);
        panel.raycastTarget = false;
        panel.rectTransform.sizeDelta = new Vector2(520f, 230f);

        MakeText(panel.transform, font, "Mine cart - where to?", 26, new Color(1f, 0.85f, 0.5f), 75f);
        floorText = MakeText(panel.transform, font, "", 44, Color.white, 15f);
        themeText = MakeText(panel.transform, font, "", 24, new Color(0.75f, 0.85f, 1f), -35f);
        MakeText(panel.transform, font, "Left / Right: choose     Space: go     Esc: cancel", 18, new Color(0.7f, 0.7f, 0.7f), -85f);
    }

    private static Text MakeText(Transform parent, Font font, string words, int size, Color color, float y)
    {
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.text = words; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = new Vector2(0f, y);
        t.rectTransform.sizeDelta = new Vector2(500f, 50f);
        return t;
    }
}
