using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that acts as the central game coordinator.
///
/// Responsibilities:
///   - Registers bubble sprites in BubbleSpriteRegistry at startup
///   - Loads/saves progress via PlayerPrefs (26-bit bitmask per mode)
///   - Instantiates / destroys LetterPuzzle prefabs as the player advances
///   - Exposes StartGame(), SelectLetter(), and SetMode() for UI/Lobby to call
///
/// Flow: LobbyManager calls StartGame(uppercase) → gameplay begins.
///       GameManager does NOT auto-start; it waits for the lobby choice.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Global Events
    // ------------------------------------------------------------------ //
    public static event System.Action OnGameStartedEvent;

    // ------------------------------------------------------------------ //
    //  Singleton
    // ------------------------------------------------------------------ //
    public static GameManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    //  Inspector — Letter Data arrays
    // ------------------------------------------------------------------ //
    [Header("Letter Data (index 0 = A … 25 = Z)")]
    [Tooltip("26 LetterData assets, uppercase A-Z in order.")]
    public LetterData[] uppercaseLetters;

    [Tooltip("26 LetterData assets, lowercase a-z in order.")]
    public LetterData[] lowercaseLetters;

    // ------------------------------------------------------------------ //
    //  Inspector — Bubble Sprites
    // ------------------------------------------------------------------ //
    [Header("Bubble Sprites — unpopped (raised) state")]
    public Sprite blueUnpopped;
    public Sprite greenUnpopped;
    public Sprite pinkUnpopped;
    public Sprite redUnpopped;
    public Sprite yellowUnpopped;

    [Header("Bubble Sprites — popped (depressed) state")]
    public Sprite bluePopped;
    public Sprite greenPopped;
    public Sprite pinkPopped;
    public Sprite redPopped;
    public Sprite yellowPopped;

    // ------------------------------------------------------------------ //
    //  Inspector — Prefabs & Scene references
    // ------------------------------------------------------------------ //
    [Header("Prefabs")]
    public LetterPuzzle letterPuzzlePrefab;
    public Bubble bubblePrefab;

    [Header("Scene References")]
    [Tooltip("The RectTransform (or Transform) in the Canvas where LetterPuzzle is spawned.")]
    public Transform letterPuzzleArea;

    [Tooltip("Drag the UIManager component here.")]
    public UIManager uiManager;

    [Header("Animation Setup")]
    [Tooltip("Scene object for the jiggling alphabet animation. Expected to have a SpriteRenderer and ParticleSystems.")]
    public GameObject jiggleLetterObject;
    
    private SpriteRenderer _jiggleLetterRenderer;
    private ParticleSystem[] _jiggleParticles;
    private bool _animationInitialized;

    // ------------------------------------------------------------------ //
    //  PlayerPrefs keys
    // ------------------------------------------------------------------ //
    private const string PREFS_UPPER = "ProgressUppercase";
    private const string PREFS_LOWER = "ProgressLowercase";
    private const string PREFS_MODE  = "IsUppercase";

    // ------------------------------------------------------------------ //
    //  Runtime state
    // ------------------------------------------------------------------ //
    private LetterData[]  _activeLetters;
    private bool[]        _completedFlags;   // length 26
    private int           _currentIndex;
    private bool          _isUppercase;
    private LetterPuzzle  _currentPuzzle;

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

        // Register sprites before any LetterPuzzle can call BubbleSpriteRegistry.Get()
        RegisterBubbleSprites();
    }

    private void Start()
    {
        // Do NOT auto-start. LobbyManager calls StartGame() once the player
        // chooses a mode. This keeps the gameplay panel hidden until needed.
    }

    // ------------------------------------------------------------------ //
    //  Public API — called by LobbyManager / UIManager
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Called by LobbyManager when the player picks Capital or Small letters.
    /// Loads progress for that mode and spawns the first incomplete letter.
    /// </summary>
    public void StartGame(bool uppercase)
    {
        _isUppercase   = uppercase;
        _activeLetters = uppercase ? uppercaseLetters : lowercaseLetters;
        PlayerPrefs.SetInt(PREFS_MODE, uppercase ? 1 : 0);
        PlayerPrefs.Save();

        LoadProgress();
        LoadLetter(_currentIndex);

        if (uiManager != null)
        {
            uiManager.SwitchTraySprites(uppercase);
            uiManager.RefreshAll(_completedFlags, _currentIndex);
        }

        OnGameStartedEvent?.Invoke();
    }

    /// <summary>
    /// Switch between uppercase and lowercase mode from within gameplay.
    /// Saves the preference and reloads from persisted progress.
    /// </summary>
    public void SetMode(bool uppercase)
    {
        _isUppercase   = uppercase;
        _activeLetters = uppercase ? uppercaseLetters : lowercaseLetters;
        PlayerPrefs.SetInt(PREFS_MODE, uppercase ? 1 : 0);
        PlayerPrefs.Save();

        LoadProgress();
        LoadLetter(_currentIndex);

        if (uiManager != null)
        {
            uiManager.SwitchTraySprites(uppercase);
            uiManager.RefreshAll(_completedFlags, _currentIndex);
        }
    }

    /// <summary>
    /// Jump directly to a letter by index (0 = A … 25 = Z).
    /// Called when the player taps a letter button in the bottom tray.
    /// </summary>
    public void SelectLetter(int index)
    {
        index = Mathf.Clamp(index, 0, 25);
        _currentIndex = index;
        LoadLetter(index);

        if (uiManager != null)
            uiManager.SetActiveHighlight(index);
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>Instantiate (or replace) the LetterPuzzle for the given index.</summary>
    private void LoadLetter(int index)
    {
        // Clean up the previous puzzle
        if (_currentPuzzle != null)
        {
            _currentPuzzle.OnLetterCompleted -= HandleLetterCompleted;
            Destroy(_currentPuzzle.gameObject);
            _currentPuzzle = null;
        }

        if (_activeLetters == null || index < 0 || index >= _activeLetters.Length)
        {
            Debug.LogError($"[GameManager] Invalid letter index {index} or letter array not set.");
            return;
        }

        // Spawn new puzzle
        _currentPuzzle = Instantiate(letterPuzzlePrefab, letterPuzzleArea);
        _currentPuzzle.transform.localPosition = Vector3.zero;
        _currentPuzzle.transform.localRotation = Quaternion.identity;
        _currentPuzzle.transform.localScale    = Vector3.one;

        _currentPuzzle.Initialize(_activeLetters[index], bubblePrefab);
        _currentPuzzle.OnLetterCompleted += HandleLetterCompleted;

        _currentIndex = index;

        if (uiManager != null)
            uiManager.SetActiveHighlight(index);
    }

    /// <summary>
    /// Invoked by LetterPuzzle once all bubbles are popped (after jingle delay).
    /// Marks the letter complete, saves progress, then auto-advances.
    /// </summary>
    private void HandleLetterCompleted(LetterData completedData)
    {
        _completedFlags[_currentIndex] = true;
        SaveProgress();

        StartCoroutine(AnimateAlphabetAndAdvance(completedData));
    }

    private IEnumerator AnimateAlphabetAndAdvance(LetterData data)
    {
        if (!_animationInitialized && jiggleLetterObject != null)
        {
            _jiggleLetterRenderer = jiggleLetterObject.GetComponent<SpriteRenderer>();
            if (_jiggleLetterRenderer == null)
                _jiggleLetterRenderer = jiggleLetterObject.GetComponentInChildren<SpriteRenderer>();

            _jiggleParticles = jiggleLetterObject.GetComponentsInChildren<ParticleSystem>();
            _animationInitialized = true;
        }

        GameObject animObj = jiggleLetterObject;
        SpriteRenderer sr = _jiggleLetterRenderer;

        // Fallback if no object is assigned
        bool isFallback = false;
        if (animObj == null || sr == null)
        {
            animObj = new GameObject("JiggleLetter");
            sr = animObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1000;
            isFallback = true;
        }

        animObj.SetActive(true);
        sr.sprite = data.letterSprite;

        if (_jiggleParticles != null)
        {
            foreach (var p in _jiggleParticles)
            {
                p.Play();
            }
        }

        Vector3 unscaledSize = sr.sprite.bounds.size;
        Vector3 originalScale = Vector3.one;

        if (_currentPuzzle != null)
        {
            if (_currentPuzzle.letterBackground != null)
            {
                animObj.transform.position = _currentPuzzle.letterBackground.transform.position;
                animObj.transform.rotation = _currentPuzzle.letterBackground.transform.rotation;
                originalScale = _currentPuzzle.letterBackground.transform.lossyScale;
            }
            
            _currentPuzzle.OnLetterCompleted -= HandleLetterCompleted;
            Destroy(_currentPuzzle.gameObject);
            _currentPuzzle = null;
        }

        animObj.transform.localScale = originalScale;
        Quaternion originalRotation = animObj.transform.rotation;

        // Jiggle Phase
        float waitTime = 1f;
        float tiltAmount = 15f;
        float tiltSpeed = 15f;
        float timer = 0f;

        while (timer < waitTime)
        {
            timer += Time.deltaTime;
            float angle = Mathf.Sin(Time.time * tiltSpeed) * tiltAmount;
            animObj.transform.rotation = originalRotation * Quaternion.Euler(0, 0, angle);
            yield return null;
        }

        animObj.transform.rotation = originalRotation;

        // Move Phase
        RectTransform targetRect = uiManager != null ? uiManager.GetLetterTargetRect(_currentIndex) : null;
        if (targetRect != null)
        {
            Vector3 moveStart = animObj.transform.position;
            Vector3 moveEnd = targetRect.position;

            Canvas rootCanvas = targetRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (Camera.main != null)
                {
                    Vector3 screenPoint = targetRect.position;
                    screenPoint.z = Mathf.Abs(Camera.main.transform.position.z);
                    moveEnd = Camera.main.ScreenToWorldPoint(screenPoint);
                    moveEnd.z = 0f;
                }
            }
            else
            {
                moveEnd.z = 0f;
            }

            Vector3 targetScale = originalScale;
            if (unscaledSize.x > 0.001f && unscaledSize.y > 0.001f)
            {
                Vector3 targetWorldSize = new Vector3(
                    targetRect.rect.width * targetRect.lossyScale.x,
                    targetRect.rect.height * targetRect.lossyScale.y,
                    1f
                );
                
                if (rootCanvas != null && rootCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Camera.main != null)
                {
                    Vector3 p1 = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, Mathf.Abs(Camera.main.transform.position.z)));
                    Vector3 p2 = Camera.main.ScreenToWorldPoint(new Vector3(targetRect.rect.width * targetRect.lossyScale.x, targetRect.rect.height * targetRect.lossyScale.y, Mathf.Abs(Camera.main.transform.position.z)));
                    targetWorldSize = new Vector3(Mathf.Abs(p2.x - p1.x), Mathf.Abs(p2.y - p1.y), 1f);
                }

                targetScale = new Vector3(
                    targetWorldSize.x / unscaledSize.x,
                    targetWorldSize.y / unscaledSize.y,
                    originalScale.z
                );
            }

            float moveTime = 0f;
            float moveDuration = 1f;

            while (moveTime < moveDuration)
            {
                moveTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, moveTime / moveDuration);

                animObj.transform.position = Vector3.Lerp(moveStart, moveEnd, t);
                animObj.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);

                yield return null;
            }
        }

        if (isFallback)
            Destroy(animObj);
        else
            animObj.SetActive(false);

        if (uiManager != null)
            uiManager.MarkLetterComplete(_currentIndex);

        yield return new WaitForSeconds(0.2f);
        
        // Cycle: after Z go back to A
        int next = (_currentIndex + 1) % 26;
        _currentIndex = next;
        LoadLetter(next);
    }

    /// <summary>
    /// Reads the 26-bit bitmask from PlayerPrefs and populates _completedFlags.
    /// Also sets _currentIndex to the first incomplete letter.
    /// </summary>
    private void LoadProgress()
    {
        string key   = _isUppercase ? PREFS_UPPER : PREFS_LOWER;
        int bitmask  = PlayerPrefs.GetInt(key, 0);

        _completedFlags = new bool[26];
        for (int i = 0; i < 26; i++)
            _completedFlags[i] = (bitmask & (1 << i)) != 0;

        // Find the first incomplete letter to resume from
        _currentIndex = 0;
        for (int i = 0; i < 26; i++)
        {
            if (!_completedFlags[i])
            {
                _currentIndex = i;
                break;
            }
        }
        // If all 26 are complete, start back at A
    }

    /// <summary>Packs _completedFlags into a 26-bit int and writes to PlayerPrefs.</summary>
    private void SaveProgress()
    {
        string key  = _isUppercase ? PREFS_UPPER : PREFS_LOWER;
        int bitmask = 0;
        for (int i = 0; i < 26; i++)
            if (_completedFlags[i]) bitmask |= (1 << i);

        PlayerPrefs.SetInt(key, bitmask);
        PlayerPrefs.Save();
    }

    /// <summary>Populate BubbleSpriteRegistry with the sprites assigned in the Inspector.</summary>
    private void RegisterBubbleSprites()
    {
        BubbleSpriteRegistry.Clear();
        BubbleSpriteRegistry.Register(BubbleColor.Blue,   blueUnpopped,   bluePopped);
        BubbleSpriteRegistry.Register(BubbleColor.Green,  greenUnpopped,  greenPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Pink,   pinkUnpopped,   pinkPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Red,    redUnpopped,    redPopped);
        BubbleSpriteRegistry.Register(BubbleColor.Yellow, yellowUnpopped, yellowPopped);
    }
}
