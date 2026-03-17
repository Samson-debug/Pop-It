using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton MonoBehaviour that centralises all player input.
/// Reads from Unity's new Input System (Mouse + Touchscreen) and fires
/// a single world-space OnTap event that other systems subscribe to.
///
/// This keeps individual gameplay objects free of input boilerplate and
/// allows LetterPuzzle to easily block taps during completion animations.
/// </summary>
public class InputHandler : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Singleton
    // ------------------------------------------------------------------ //
    public static InputHandler Instance { get; private set; }

    // ------------------------------------------------------------------ //
    //  Events
    // ------------------------------------------------------------------ //
    /// <summary>
    /// Fired every frame a tap/click begins, with the world-space position.
    /// </summary>
    public event Action<Vector2> OnTap;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        Vector2 screenPos = Vector2.zero;
        bool tapped = false;

        // ----- Mouse (editor / desktop) -----
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            tapped = true;
        }

        // ----- Touch (mobile) -----
        // Only process touch if no mouse was detected this frame, to avoid double-firing
        // on desktop with touch simulation.
        if (!tapped && Touchscreen.current != null)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            if (primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = primaryTouch.position.ReadValue();
                tapped = true;
            }
        }

        if (!tapped) return;

        // Convert screen → world space at the camera's near-clip plane depth.
        // Works correctly for an orthographic 2D camera.
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[InputHandler] Camera.main is null — cannot convert tap position.");
            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));

        OnTap?.Invoke(new Vector2(world.x, world.y));
    }
}
