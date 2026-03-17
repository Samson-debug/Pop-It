using UnityEngine;

/// <summary>
/// Colour theme for the bubble sprites of a given letter.
/// Must match the filenames: blue-1/2, green-1/2, pink-1/2, red-1/2, yellow-1/2.
/// </summary>
public enum BubbleColor { Blue, Green, Pink, Red, Yellow }

/// <summary>
/// ScriptableObject that describes one letter in the game.
/// Create via: Right-click > Create > PopIt > Letter Data
/// </summary>
[CreateAssetMenu(fileName = "Letter_A", menuName = "PopIt/Letter Data")]
public class LetterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("The alphabet character this asset represents (e.g. 'A').")]
    public char letter;

    [Tooltip("True = uppercase (A-Z), False = lowercase (a-z).")]
    public bool isUppercase;

    [Header("Visuals")]
    [Tooltip("The letter sprite shown as the pop-it board background (e.g. A.png).")]
    public Sprite letterSprite;

    [Tooltip("Which bubble colour set to use for this letter.")]
    public BubbleColor bubbleColor;

    [Header("Layout")]
    [Tooltip("Bubble spawn positions for this letter shape.")]
    public BubbleLayoutData bubbleLayout;
}
