using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Represents one pop-it bubble on the letter board.
///
/// Bubbles default to "popped out" (raised). Tapping toggles between
/// popped-in (depressed) and popped-out (raised). A punch-scale animation
/// and haptic feedback fire on every tap.
///
/// Attach to a GameObject that also has:
///   - SpriteRenderer   (displays raised / depressed state)
///   - CircleCollider2D (hit detection via Physics2D.OverlapPoint in LetterPuzzle)
///   - AudioSource      (plays the pop sound locally so taps can overlap)
///
/// LetterPuzzle calls TryPop() after hit-testing; this script does NOT poll
/// input itself — that responsibility belongs to InputHandler + LetterPuzzle.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Bubble : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector references (set before ResetBubble() is called)
    // ------------------------------------------------------------------ //
    [Header("Sprites — assigned by LetterPuzzle.Initialize()")]
    [Tooltip("Sprite shown when the bubble is raised (popped out).")]
    public Sprite unpoppedSprite;

    [Tooltip("Sprite shown when the bubble is depressed (popped in).")]
    public Sprite poppedSprite;

    [Header("Audio")]
    [Tooltip("Local AudioSource component. Assign the child AudioSource in the prefab.")]
    public AudioSource audioSource;

    [Header("Squash & Stretch")]
    [Tooltip("How much the bubble squashes on the first hit (< 1 = shorter, > 1 = wider).")]
    public float squashY = 0.75f;       // Y squash on pop-in
    public float squashX = 1.18f;       // X spread on pop-in

    [Tooltip("Overshoot stretch amount after the punch.")]
    public float bounceY = 1.08f;
    public float bounceX = 0.92f;

    [Tooltip("Phase durations in seconds.")]
    public float phasePunchDuration  = 0.04f;   // fast hit
    public float phaseBounceDuration = 0.07f;   // overshoot spring
    public float phaseSettleDuration = 0.09f;   // settle back

    // ------------------------------------------------------------------ //
    //  State
    // ------------------------------------------------------------------ //
    /// <summary>True when this bubble is currently popped in (depressed).</summary>
    public bool IsPopped { get; private set; }

    // ------------------------------------------------------------------ //
    //  Events
    // ------------------------------------------------------------------ //
    /// <summary>Fired when this bubble is toggled INTO the popped-in state.</summary>
    public event Action<Bubble> OnPopped;

    /// <summary>Fired when this bubble is toggled back to the raised state.</summary>
    public event Action<Bubble> OnUnpopped;

    /// <summary>Fired on any tap (pop-in or pop-out) for global listeners.</summary>
    public static event Action OnAnyBubblePopped;

    // ------------------------------------------------------------------ //
    //  Private fields
    // ------------------------------------------------------------------ //
    private SpriteRenderer _sr;
    private CircleCollider2D _col;
    private Vector3 _baseScale;
    private Coroutine _punchCoroutine;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _col = GetComponent<CircleCollider2D>();
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Toggle this bubble between popped-in and popped-out.
    /// Fires a punch-scale animation and haptic feedback on every tap.
    /// </summary>
    public void TryPop()
    {
        // Toggle state
        IsPopped = !IsPopped;

        // Visual: switch sprite to match new state
        _sr.sprite = IsPopped ? poppedSprite : unpoppedSprite;

        // Audio: play pop sound (works for both directions)
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        // Squash & stretch animation (direction-aware)
        if (_punchCoroutine != null)
            StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(SquashAndStretch(IsPopped));

        // Haptic feedback
        TriggerHaptic();

        // Notify listeners
        if (IsPopped)
            OnPopped?.Invoke(this);
        else
            OnUnpopped?.Invoke(this);

        OnAnyBubblePopped?.Invoke();
    }

    /// <summary>
    /// Reset this bubble to its initial raised (popped-out) state.
    /// Called by LetterPuzzle.Initialize() after sprites and scale are assigned.
    /// </summary>
    public void ResetBubble()
    {
        IsPopped = false;
        _col.enabled = true;

        // Capture the scale set by LetterPuzzle before this call
        _baseScale = transform.localScale;

        if (unpoppedSprite != null)
        {
            _sr.sprite = unpoppedSprite;

            // Fit the collider to the sprite's actual pixel bounds.
            // sprite.bounds.extents.x is the half-width in local units (scale-independent),
            // which is exactly what CircleCollider2D.radius expects.
            _col.offset = Vector2.zero;
            _col.radius = unpoppedSprite.bounds.extents.x;
        }
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Direction-aware squash-and-stretch with a punch, overshoot bounce, and settle.
    ///
    /// Pop-in  : squash wide+short  → bounce tall+thin  → settle
    /// Pop-out : stretch tall+thin  → bounce wide+short → settle
    /// </summary>
    private IEnumerator SquashAndStretch(bool poppingIn)
    {
        Vector3 original = _baseScale;

        // Phase 1 key: the fast "punch" hit
        Vector3 punch = poppingIn
            ? new Vector3(original.x * squashX, original.y * squashY,  original.z)   // squash down
            : new Vector3(original.x / squashX, original.y / squashY,  original.z);  // stretch up

        // Phase 2 key: the overshoot spring in the opposite direction
        Vector3 bounce = poppingIn
            ? new Vector3(original.x * bounceX, original.y * bounceY, original.z)    // slight tall rebound
            : new Vector3(original.x * bounceY, original.y * bounceX, original.z);   // slight wide rebound

        // Phase 1 — fast punch
        yield return LerpScale(original, punch,  phasePunchDuration);

        // Phase 2 — overshoot bounce
        yield return LerpScale(punch,    bounce, phaseBounceDuration);

        // Phase 3 — settle back
        yield return LerpScale(bounce,   original, phaseSettleDuration);

        transform.localScale = original;
        _punchCoroutine = null;
    }

    private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
    }

    /// <summary>
    /// Triggers a short light vibration. Platform-specific.
    /// </summary>
    private void TriggerHaptic()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            vibrator.Call("vibrate", 25L);   // 25 ms — just a tap pulse
        }
        catch { /* fail silently on devices without a vibrator */ }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
