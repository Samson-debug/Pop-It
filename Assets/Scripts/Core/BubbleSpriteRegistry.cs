using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry that maps a BubbleColor to its two sprites (unpopped / popped).
/// Populated once by GameManager.Awake() before any LetterPuzzle is spawned.
/// No MonoBehaviour lifecycle needed — pure data lookup.
/// </summary>
public static class BubbleSpriteRegistry
{
    private static readonly Dictionary<BubbleColor, (Sprite unpopped, Sprite popped)> _map
        = new Dictionary<BubbleColor, (Sprite, Sprite)>();

    /// <summary>Register a colour pair. Called by GameManager.Awake().</summary>
    public static void Register(BubbleColor color, Sprite unpopped, Sprite popped)
    {
        _map[color] = (unpopped, popped);
    }

    /// <summary>
    /// Retrieve the sprite pair for a colour.
    /// Returns (null, null) if the colour was never registered — Unity will log a warning.
    /// </summary>
    public static (Sprite unpopped, Sprite popped) Get(BubbleColor color)
    {
        if (_map.TryGetValue(color, out var pair))
            return pair;

        Debug.LogWarning($"[BubbleSpriteRegistry] No sprites registered for {color}. " +
                         "Make sure GameManager.RegisterBubbleSprites() ran before this call.");
        return (null, null);
    }

    /// <summary>Clear all entries (useful for unit tests or scene reloads).</summary>
    public static void Clear() => _map.Clear();
}
