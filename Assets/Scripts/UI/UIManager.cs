using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all UI elements:
///   - Bottom alphabet tray (26 LetterButtonUI instances, built at runtime)
///   - Green checkmarks for completed letters
///   - Uppercase / Lowercase mode toggle buttons
///   - Info button panel toggle
///
/// Attach to a persistent GameObject in the scene and wire all references
/// in the Inspector before pressing Play.
/// </summary>
public class UIManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector references
    // ------------------------------------------------------------------ //
    [Header("Bottom Tray")]
    [Tooltip("The content RectTransform inside the ScrollRect. Has a HorizontalLayoutGroup.")]
    public RectTransform alphabetTrayContent;

    [Tooltip("Prefab for each letter button (has LetterButtonUI component).")]
    public GameObject letterButtonPrefab;

    [Header("Letter Sprites for Tray")]
    [Tooltip("26 uppercase letter sprites in order A-Z.")]
    public Sprite[] uppercaseLetterSprites;

    [Tooltip("26 lowercase letter sprites in order a-z.")]
    public Sprite[] lowercaseLetterSprites;

    [Header("Mode Toggle Buttons")]
    [Tooltip("Button that switches to uppercase mode.")]
    public Button uppercaseButton;

    [Tooltip("Button that switches to lowercase mode.")]
    public Button lowercaseButton;

    // ------------------------------------------------------------------ //
    //  Private state
    // ------------------------------------------------------------------ //
    private LetterButtonUI[] _letterButtons;  // 26 entries, A-Z
    private bool _isUppercase = true;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void Awake()
    {
        BuildTray();

        // Mode toggle buttons
        if (uppercaseButton != null)
            uppercaseButton.onClick.AddListener(() => GameManager.Instance.SetMode(true));

        if (lowercaseButton != null)
            lowercaseButton.onClick.AddListener(() => GameManager.Instance.SetMode(false));
    }

    // ------------------------------------------------------------------ //
    //  Public API — called by GameManager
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Rebuilds the tray letter sprites for the new mode (uppercase / lowercase).
    /// </summary>
    public void SwitchTraySprites(bool uppercase)
    {
        _isUppercase = uppercase;
        Sprite[] sprites = uppercase ? uppercaseLetterSprites : lowercaseLetterSprites;

        if (_letterButtons == null) return;

        for (int i = 0; i < 26; i++)
        {
            if (i < sprites.Length && _letterButtons[i] != null)
                _letterButtons[i].SetLetterSprite(sprites[i]);
        }
    }

    /// <summary>
    /// Sets the active (bright) highlight to <paramref name="index"/>;
    /// all other buttons are dimmed.
    /// </summary>
    public void SetActiveHighlight(int index)
    {
        if (_letterButtons == null) return;

        for (int i = 0; i < 26; i++)
        {
            if (_letterButtons[i] != null)
                _letterButtons[i].SetHighlight(i == index);
        }
    }

    /// <summary>Shows the green checkmark on the tray button for <paramref name="index"/>.</summary>
    public void MarkLetterComplete(int index)
    {
        if (_letterButtons != null && index >= 0 && index < 26 && _letterButtons[index] != null)
            _letterButtons[index].ShowCheckmark(true);
    }

    /// <summary>Returns the RectTransform of the target letterImage.</summary>
    public RectTransform GetLetterTargetRect(int index)
    {
        if (_letterButtons != null && index >= 0 && index < 26 && _letterButtons[index] != null)
            return _letterButtons[index].letterImage.rectTransform;
        return null;
    }

    /// <summary>
    /// Full refresh: sets checkmarks and highlight from saved state.
    /// Called on start or after a mode switch.
    /// </summary>
    public void RefreshAll(bool[] completedFlags, int activeIndex)
    {
        if (_letterButtons == null) return;

        for (int i = 0; i < 26; i++)
        {
            if (_letterButtons[i] == null) continue;
            _letterButtons[i].ShowCheckmark(completedFlags != null && i < completedFlags.Length && completedFlags[i]);
            _letterButtons[i].SetHighlight(i == activeIndex);
        }
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Instantiates 26 LetterButtonUI objects inside alphabetTrayContent.
    /// Wires onClick → GameManager.SelectLetter(i).
    /// </summary>
    private void BuildTray()
    {
        if (alphabetTrayContent == null || letterButtonPrefab == null)
        {
            Debug.LogError("[UIManager] alphabetTrayContent or letterButtonPrefab is not assigned.");
            return;
        }

        // Clear any existing children (e.g. placeholder buttons in the prefab)
        foreach (Transform child in alphabetTrayContent)
            Destroy(child.gameObject);

        _letterButtons = new LetterButtonUI[26];
        Sprite[] sprites = _isUppercase ? uppercaseLetterSprites : lowercaseLetterSprites;

        for (int i = 0; i < 26; i++)
        {
            int capturedIndex = i;   // capture for lambda closure

            GameObject go = Instantiate(letterButtonPrefab, alphabetTrayContent);
            LetterButtonUI lb = go.GetComponent<LetterButtonUI>();

            if (lb == null)
            {
                Debug.LogError("[UIManager] letterButtonPrefab is missing LetterButtonUI component.");
                continue;
            }

            Sprite sprite = (sprites != null && i < sprites.Length) ? sprites[i] : null;
            lb.Setup(sprite);
            lb.button.onClick.AddListener(() => GameManager.Instance.SelectLetter(capturedIndex));

            _letterButtons[i] = lb;
        }
    }
}
