using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the little scene of walking through a door, used by HouseDoor and SuburbHouseDoor:
///   1. the door you use swings open (if it has a DoorSwing that found its picture),
///   2. the screen fades to black, you are moved to the other side,
///   3. the screen fades back in, and the door on the other side (if any) swings shut behind you.
/// The player can't move during steps 1-2. It runs on a hidden object that survives scene changes,
/// so the controls can never stay locked if a door object disappears halfway.
/// </summary>
public static class DoorTransition
{
    /// <summary>True while a door is being walked through (a second press is ignored).</summary>
    public static bool Busy { get; private set; }

    private static Runner runner;
    private static Image fadeImage;

    // A plain MonoBehaviour just so the static class has something to run coroutines on.
    private class Runner : MonoBehaviour { }

    /// <summary>
    /// Walks the player through a door.
    /// 'arrive' is called while the screen is black, right after the player is moved (enter or leave the room there).
    /// </summary>
    public static void Walk(PlayerActions player, DoorSwing usedDoor, Transform destination, Vector2 arriveFacing,
                            Action arrive, float fadeSeconds)
    {
        if (Busy || player == null || destination == null) return;
        if (runner == null)
        {
            var go = new GameObject("DoorTransition");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        runner.StartCoroutine(Play(player, usedDoor, destination, arriveFacing, arrive, fadeSeconds));
    }

    private static IEnumerator Play(PlayerActions player, DoorSwing usedDoor, Transform destination, Vector2 arriveFacing,
                                    Action arrive, float fadeSeconds)
    {
        Busy = true;
        GameState.LockInput();
        try
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            // 1. Open the door in front of you.
            bool animated = usedDoor != null && usedDoor.IsReady;
            if (animated)
            {
                if (player.Controller != null) player.Controller.FaceDirection(Vector2.up);
                yield return usedDoor.Open();
            }

            // 2. Fade out and move to the other side.
            if (fadeSeconds > 0f) yield return Fade(0f, 1f, fadeSeconds);
            if (player == null || destination == null) yield break;

            if (rb != null) { rb.position = destination.position; rb.linearVelocity = Vector2.zero; }
            player.transform.position = destination.position;
            if (arrive != null) arrive();
            if (player.Controller != null) player.Controller.FaceDirection(arriveFacing);
            var cam = UnityEngine.Object.FindAnyObjectByType<CameraFollow2D>();
            if (cam != null) cam.SnapToTarget();

            // While it's black: shut the door we came through, and show the door on the far side standing open.
            if (animated && usedDoor != null) usedDoor.SetOpenInstantly(false);
            DoorSwing farDoor = DoorSwing.FindNear(destination);
            if (farDoor != null) farDoor.SetOpenInstantly(true);

            // 3. Fade in, give the controls back, and let the far door swing shut behind you.
            if (fadeSeconds > 0f) yield return Fade(1f, 0f, fadeSeconds);
            if (farDoor != null) farDoor.CloseInBackground();
        }
        finally
        {
            // Always runs, even if the coroutine ends early, so the controls never stay locked.
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);
            GameState.UnlockInput();
            Busy = false;
        }
    }

    // A full-screen black picture, built on the fly so there is nothing to set up.
    private static IEnumerator Fade(float from, float to, float seconds)
    {
        if (fadeImage == null)
        {
            var go = new GameObject("DoorFade");
            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 500;
            var img = new GameObject("Black");
            img.transform.SetParent(go.transform, false);
            fadeImage = img.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
            RectTransform rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}
