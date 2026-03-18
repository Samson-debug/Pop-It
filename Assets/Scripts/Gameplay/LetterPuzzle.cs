using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages one pop-it letter board: spawns bubbles, processes taps,
/// tracks completion, and fires OnLetterCompleted when all bubbles are popped.
///
/// Hierarchy expected:
///   LetterPuzzle (this script)
///     LetterBackground  (SpriteRenderer — shows the letter shape)
///     BubblesContainer  (empty Transform — bubbles are spawned here)
///
/// Lifetime: instantiated by GameManager for each letter; destroyed when
/// the next letter loads.
/// </summary>
public class LetterPuzzle : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector references (wired in the prefab)
    // ------------------------------------------------------------------ //
    [Tooltip("Child SpriteRenderer that displays the letter shape (e.g. F.png).")]
    public SpriteRenderer letterBackground;

    [Tooltip("Parent Transform under which bubbles are spawned at runtime.")]
    public Transform bubblesContainer;

    // ------------------------------------------------------------------ //
    //  Runtime data (set by Initialize)
    // ------------------------------------------------------------------ //
    /// <summary>The LetterData this puzzle was built from.</summary>
    public LetterData Data { get; private set; }

    // ------------------------------------------------------------------ //
    //  Events
    // ------------------------------------------------------------------ //
    /// <summary>Fired (after a short delay) when every bubble has been popped.</summary>
    public event Action<LetterData> OnLetterCompleted;

    // ------------------------------------------------------------------ //
    //  Inspector — Puzzle animation
    // ------------------------------------------------------------------ //
    [Header("Puzzle Squash & Stretch")]
    [Tooltip("X spread when the puzzle squashes on a pop-in tap.")]
    public float puzzleSquashX = 1.06f;
    [Tooltip("Y compress when the puzzle squashes on a pop-in tap.")]
    public float puzzleSquashY = 0.92f;
    [Tooltip("Overshoot bounce multiplier (applied in the opposite axis).")]
    public float puzzleBounce  = 1.03f;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //
    private readonly List<Bubble> _bubbles = new List<Bubble>();
    private int _poppedCount;
    private bool _completing;   // guard: prevent re-triggering completion
    private Vector3 _baseScale;
    private Coroutine _puzzleAnim;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void OnEnable()
    {
        if (InputHandler.Instance != null)
            InputHandler.Instance.OnTap += HandleTap;
    }

    private void OnDisable()
    {
        if (InputHandler.Instance != null)
            InputHandler.Instance.OnTap -= HandleTap;
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Configures this puzzle for the given letter.
    /// Must be called by GameManager immediately after Instantiate().
    /// </summary>
    /// <param name="letterData">Data asset for this letter.</param>
    /// <param name="bubblePrefab">Prefab used to spawn each bubble.</param>
    public void Initialize(LetterData letterData, Bubble bubblePrefab)
    {
        Data = letterData;
        _poppedCount = 0;
        _completing  = false;
        _baseScale   = transform.localScale;

        // ---- Letter background sprite ----
        if (letterBackground == null)
        {
            Debug.LogError("[LetterPuzzle] 'letterBackground' SpriteRenderer is not assigned! " +
                           "Open the LetterPuzzle prefab and wire the LetterBackground child.");
            return;
        }
        if (letterData.letterSprite == null)
        {
            Debug.LogError($"[LetterPuzzle] LetterData '{letterData.name}' has no letterSprite! " +
                           "Re-run PopIt Tools > Run Full Setup to rebuild ScriptableObjects.");
        }
        letterBackground.sprite = letterData.letterSprite;

        // ---- Destroy any previously spawned bubbles ----
        foreach (Transform child in bubblesContainer)
            Destroy(child.gameObject);
        _bubbles.Clear();

        // ---- Retrieve colour sprites from registry ----
        var (unpoppedSprite, poppedSprite) = BubbleSpriteRegistry.Get(letterData.bubbleColor);

        if (unpoppedSprite == null || poppedSprite == null)
        {
            Debug.LogError($"[LetterPuzzle] Missing sprites for BubbleColor.{letterData.bubbleColor}. " +
                           "Ensure GameManager.RegisterBubbleSprites() ran before Initialize().");
            return;
        }

        // ---- Spawn bubbles from layout ----
        BubbleLayoutData layout = letterData.bubbleLayout;
        if (layout == null || layout.bubbles == null || layout.bubbles.Length == 0)
        {
            Debug.LogError($"[LetterPuzzle] BubbleLayoutData is null or empty for letter '{letterData.letter}'.");
            return;
        }

        for (int i = 0; i < layout.bubbles.Length; i++)
        {
            Bubble b = Instantiate(bubblePrefab, bubblesContainer);
            b.transform.localPosition = new Vector3(
                layout.bubbles[i].position.x,
                layout.bubbles[i].position.y,
                0f);
            b.transform.localScale = Vector3.one * layout.bubbles[i].size;

            // Assign sprites BEFORE ResetBubble so the SpriteRenderer picks them up
            b.unpoppedSprite = unpoppedSprite;
            b.poppedSprite   = poppedSprite;

            // Subscribe before ResetBubble in case something fires immediately
            b.OnPopped    += OnBubblePopped;
            b.OnUnpopped  += OnBubbleUnpopped;

            // Apply sprites and re-enable collider
            b.ResetBubble();

            // Bubbles need a pop sound clip; assign from AudioManager if not already set in prefab
            if (b.audioSource != null && b.audioSource.clip == null && AudioManager.Instance != null)
                b.audioSource.clip = AudioManager.Instance.popClip;

            _bubbles.Add(b);
        }
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Receives world-space tap position from InputHandler.
    /// Uses Physics2D.OverlapPoint to find the bubble collider at that position.
    /// </summary>
    private void HandleTap(Vector2 worldPos)
    {
        // Ignore taps during the completion coroutine to prevent re-entry
        if (_completing) return;

        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null) return;

        Bubble bubble = hit.GetComponent<Bubble>();
        if (bubble == null || !_bubbles.Contains(bubble)) return;

        bubble.TryPop();

        // Animate the whole puzzle — direction matches bubble's new state
        if (_puzzleAnim != null) StopCoroutine(_puzzleAnim);
        _puzzleAnim = StartCoroutine(PuzzleSquashAndStretch(bubble.IsPopped));
    }

    /// <summary>Called when any bubble is toggled into the popped-in state.</summary>
    private void OnBubblePopped(Bubble b)
    {
        _poppedCount++;

        if (_poppedCount >= _bubbles.Count && !_completing)
        {
            _completing = true;
            StartCoroutine(CompleteRoutine());
        }
    }

    /// <summary>Called when any bubble is toggled back to the raised (popped-out) state.</summary>
    private void OnBubbleUnpopped(Bubble b)
    {
        _poppedCount--;
    }

    /// <summary>
    /// Subtle squash-and-stretch on the whole LetterPuzzle when any bubble is tapped.
    /// Pop-in  → squash wide+short, bounce tall+thin, settle.
    /// Pop-out → stretch tall+thin, bounce wide+short, settle.
    /// </summary>
    private IEnumerator PuzzleSquashAndStretch(bool poppingIn)
    {
        Vector3 orig = _baseScale;

        Vector3 punch = poppingIn
            ? new Vector3(orig.x * puzzleSquashX, orig.y * puzzleSquashY, orig.z)
            : new Vector3(orig.x / puzzleSquashX, orig.y / puzzleSquashY, orig.z);

        Vector3 bounce = poppingIn
            ? new Vector3(orig.x / puzzleBounce,  orig.y * puzzleBounce,  orig.z)
            : new Vector3(orig.x * puzzleBounce,  orig.y / puzzleBounce,  orig.z);

        yield return LerpScale(orig,   punch,  0.04f);
        yield return LerpScale(punch,  bounce, 0.07f);
        yield return LerpScale(bounce, orig,   0.09f);

        transform.localScale = orig;
        _puzzleAnim = null;
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
    /// Plays the completion sound, waits for it to finish, then fires OnLetterCompleted.
    /// </summary>
    private IEnumerator CompleteRoutine()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLetterComplete();

        // Wait long enough for the jingle to play before transitioning
        yield return new WaitForSeconds(1.2f);

        OnLetterCompleted?.Invoke(Data);
    }
}
