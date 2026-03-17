using UnityEngine;

/// <summary>
/// One entry in a bubble layout — stores the local-space position and
/// the individual scale for that specific bubble.
/// </summary>
[System.Serializable]
public struct BubbleEntry
{
    [Tooltip("Local-space position of this bubble relative to the LetterPuzzle centre.")]
    public Vector2 position;

    [Tooltip("World-space scale of this bubble (radius in Unity units).")]
    [Range(0.1f, 1f)]
    public float size;
}

/// <summary>
/// ScriptableObject that stores per-bubble position AND size for one letter.
/// Both uppercase and lowercase layouts use this; lowercase letters have
/// their own distinct positions reflecting the actual glyph shapes
/// (x-height body, ascenders, descenders).
///
/// Create via: Right-click > Create > PopIt > Bubble Layout Data
/// </summary>
[CreateAssetMenu(fileName = "Layout_A", menuName = "PopIt/Bubble Layout Data")]
public class BubbleLayoutData : ScriptableObject
{
    [Tooltip("Per-bubble data: each entry has its own position and size.")]
    public BubbleEntry[] bubbles;
}
