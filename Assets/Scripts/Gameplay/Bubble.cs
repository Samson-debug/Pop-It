using System;
using UnityEngine;

/// <summary>
/// Represents one pop-it bubble on the letter board.
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
    [Tooltip("Sprite shown when the bubble is raised (not yet popped).")]
    public Sprite unpoppedSprite;

    [Tooltip("Sprite shown after the bubble has been popped (depressed).")]
    public Sprite poppedSprite;

    [Header("Audio")]
    [Tooltip("Local AudioSource component. Assign the child AudioSource in the prefab.")]
    public AudioSource audioSource;

    // ------------------------------------------------------------------ //
    //  State
    // ------------------------------------------------------------------ //
    /// <summary>True once this bubble has been tapped.</summary>
    public bool IsPopped { get; private set; }

    // ------------------------------------------------------------------ //
    //  Events
    // ------------------------------------------------------------------ //
    /// <summary>Fired when this bubble is successfully popped. Payload = this bubble.</summary>
    public event Action<Bubble> OnPopped;

    public static event Action OnAnyBubblePopped;

    // ------------------------------------------------------------------ //
    //  Private fields
    // ------------------------------------------------------------------ //
    private SpriteRenderer _sr;
    private CircleCollider2D _col;

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
    /// Attempt to pop this bubble.
    /// Does nothing if already popped (double-tap guard).
    /// </summary>
    public void TryPop()
    {
        if (IsPopped) return;

        IsPopped = true;

        // Visual: switch to depressed sprite
        _sr.sprite = poppedSprite;

        // Physics: disable collider to prevent further hit detection
        _col.enabled = false;

        // Audio: play pop sound locally (allows overlapping pops)
        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        // Notify LetterPuzzle (and anyone else listening)
        OnPopped?.Invoke(this);
        OnAnyBubblePopped?.Invoke();
    }

    /// <summary>
    /// Reset this bubble to its initial raised state.
    /// Called by LetterPuzzle.Initialize() after sprites are assigned.
    /// </summary>
    public void ResetBubble()
    {
        IsPopped = false;
        _col.enabled = true;

        if (unpoppedSprite != null)
            _sr.sprite = unpoppedSprite;
    }
}
