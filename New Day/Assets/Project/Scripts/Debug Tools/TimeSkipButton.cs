using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// An on-screen "+1 HOUR" button under the clock. Click it (or press the shortcut key) to move the game clock forward one hour.
/// It creates itself when the game starts, so there is nothing to set up. To change where it sits or what key it uses,
/// drag this script onto any GameObject in the scene and edit the fields there (it then won't create a second one).
///
/// Skipping past the pass-out hour makes you pass out, exactly like staying up too late.
/// </summary>
public class TimeSkipButton : MonoBehaviour
{
    [Header("Button")]
    [Tooltip("Distance from the top-right corner of the screen (in a 1280x720 layout). The clock panel is above this.")]
    public Vector2 offsetFromTopRight = new Vector2(-12f, -232f);
    public Vector2 size = new Vector2(250f, 34f);
    public string label = "+1 HOUR  >>";

    [Header("Time")]
    [Min(1)] public int minutesToSkip = 60;

    [Header("Shortcut")]
    public bool useShortcutKey = true;
    public Key shortcutKey = Key.H;
    [Tooltip("Also skip with this gamepad button.")]
    public bool useGamepadButton = false;

    private Canvas canvas;
    private Image bg, border;
    private Text text;
    private float pressedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<TimeManager>() == null) return;          // no clock in this scene (menus, other areas)
        if (FindAnyObjectByType<TimeSkipButton>() != null) return;
        new GameObject("TimeSkipButton").AddComponent<TimeSkipButton>();
    }

    private void Awake()
    {
        Build();
    }

    private void Update()
    {
        TimeManager time = TimeManager.Instance;
        bool usable = time != null && !time.Paused && !GameState.InputLocked;
        canvas.enabled = time != null;

        Color idle = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        Color hover = new Color(0.16f, 0.2f, 0.26f, 0.96f);
        Color down = new Color(0.3f, 0.4f, 0.3f, 1f);

        bool over = false, clicked = false;
        Mouse mouse = Mouse.current;
        if (mouse != null && canvas.enabled)
        {
            over = RectTransformUtility.RectangleContainsScreenPoint(bg.rectTransform, mouse.position.ReadValue(), null);
            clicked = over && mouse.leftButton.wasPressedThisFrame;
        }

        Keyboard kb = Keyboard.current;
        if (useShortcutKey && kb != null && kb[shortcutKey].wasPressedThisFrame) clicked = true;
        if (useGamepadButton && Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame) clicked = true;

        if (clicked && usable)
        {
            time.AddMinutes(minutesToSkip);
            pressedUntil = Time.unscaledTime + 0.15f;
        }

        bg.color = Time.unscaledTime < pressedUntil ? down : (over && usable ? hover : idle);
        text.color = usable ? new Color(0.65f, 1f, 0.7f) : new Color(0.5f, 0.55f, 0.5f);
    }

    private void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;                                         // under dialogue and menus
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        border = MakeImage(root, "Border", new Color(0.55f, 0.42f, 0.25f, 1f));
        Place(border.rectTransform, offsetFromTopRight, size);

        bg = MakeImage(border.rectTransform, "Fill", new Color(0.08f, 0.09f, 0.12f, 0.92f));
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = new Vector2(2f, 2f);
        bg.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(bg.rectTransform, false);
        text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label + (useShortcutKey ? "   [" + shortcutKey + "]" : "");
        text.raycastTarget = false;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
    }

    private static Image MakeImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
