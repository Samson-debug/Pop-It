using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component attached to each letter button in the bottom alphabet tray.
///
/// Prefab structure expected:
///   LetterButton (GameObject)
///     Button component           ← this.button
///     LetterImage (Image)        ← displays A.png / a.png etc.
///     CheckmarkOverlay (Image)   ← tick button.png, disabled by default
/// </summary>
public class LetterButtonUI : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector references (wire in the prefab)
    // ------------------------------------------------------------------ //
    [Tooltip("The Button component on this GameObject.")]
    public Button button;

    [Tooltip("Image that displays the letter sprite (A.png, a.png, etc.).")]
    public Image letterImage;

    [Tooltip("Image showing the green tick. Disabled until the letter is completed.")]
    public GameObject checkmarkOverlay;

    // ------------------------------------------------------------------ //
    //  Colour constants
    // ------------------------------------------------------------------ //
    private static readonly Color ActiveColor = Color.white;
    private static readonly Color DimColor    = new Color(1f, 1f, 1f, 0.45f);

    // ------------------------------------------------------------------ //
    //  Public API — called by UIManager
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Initial setup called once when the tray is built.
    /// </summary>
    public void Setup(Sprite sprite)
    {
        SetLetterSprite(sprite);
        checkmarkOverlay.SetActive(false);
        SetHighlight(false);
    }

    /// <summary>Swap the letter image (called when toggling uppercase ↔ lowercase).</summary>
    public void SetLetterSprite(Sprite sprite)
    {
        if (letterImage != null)
            letterImage.sprite = sprite;
    }

    /// <summary>
    /// Highlight this button as the currently active letter, or dim it.
    /// </summary>
    public void SetHighlight(bool active)
    {
        if (letterImage != null)
            letterImage.color = active ? ActiveColor : DimColor;
    }

    /// <summary>Show or hide the green checkmark overlay.</summary>
    public void ShowCheckmark(bool show)
    {
        if (checkmarkOverlay != null)
            checkmarkOverlay.SetActive(show);
    }
}
