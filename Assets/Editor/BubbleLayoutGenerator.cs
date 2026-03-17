using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that auto-generates BubbleLayoutData assets by scanning
/// each letter sprite's alpha channel on a world-space grid.
///
/// A bubble is placed wherever the sampled pixel is opaque (alpha ≥ threshold).
/// World coordinates are relative to the sprite's pivot point so they align
/// with the LetterBackground SpriteRenderer in the LetterPuzzle prefab.
///
/// Open via: PopIt Tools ▶ 4 - Generate Bubble Layouts From Sprites
/// </summary>
public class BubbleLayoutGeneratorWindow : EditorWindow
{
    // ── Settings ─────────────────────────────────────────────────────
    private float _gridSpacing    = 0.55f;
    private float _bubbleSize     = 0.28f;
    private float _alphaThreshold = 0.45f;
    private bool  _genUppercase   = true;
    private bool  _genLowercase   = true;

    // ── Preview ───────────────────────────────────────────────────────
    private bool      _prevUpper = true;
    private int       _prevIdx   = 0;        // 0-25
    private Texture2D _prevTex;
    private int       _prevCount = -1;

    private Vector2 _scroll;

    // ═══════════════════════════════════════════════════════════════════
    //  MENU ENTRY
    // ═══════════════════════════════════════════════════════════════════

    [MenuItem("PopIt Tools/4 - Generate Bubble Layouts From Sprites", priority = 4)]
    public static void OpenWindow() =>
        GetWindow<BubbleLayoutGeneratorWindow>("Bubble Layout Gen").Show();

    // ═══════════════════════════════════════════════════════════════════
    //  GUI
    // ═══════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bubble Layout Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scans each letter sprite's alpha channel on a world-space grid.\n" +
            "Erosion check: a bubble is placed only when its centre AND all 8 surrounding\n" +
            "sample points (at bubble-radius distance) are fully inside the letter.\n" +
            "This keeps bubbles away from the transparent border edge.\n\n" +
            "Recommended settings for ~10–14 bubbles per letter:\n" +
            "  Grid Spacing    0.50 – 0.60\n" +
            "  Bubble Size     0.24 – 0.32  (keep smaller than grid spacing)\n" +
            "  Alpha Threshold 0.40 – 0.55\n\n" +
            "Use Refresh Preview to check bubble count before generating all.",
            MessageType.Info);

        // ── Grid settings ─────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

        _gridSpacing    = EditorGUILayout.Slider(
            new GUIContent("Grid Spacing (world units)",
                "Distance between bubble centres. 0.50–0.60 gives ~10–14 bubbles per letter (recommended)."),
            _gridSpacing, 0.20f, 1.20f);

        _bubbleSize     = EditorGUILayout.Slider(
            new GUIContent("Bubble Size (world units)",
                "Scale of each bubble. Keep smaller than Grid Spacing so bubbles don't overlap."),
            _bubbleSize, 0.15f, 1.10f);

        _alphaThreshold = EditorGUILayout.Slider(
            new GUIContent("Alpha Threshold (0 – 1)",
                "Pixel must be at least this opaque to place a bubble. " +
                "Raise it to skip semi-transparent edges."),
            _alphaThreshold, 0.05f, 0.95f);

        // ── Which modes ────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);
        _genUppercase = EditorGUILayout.Toggle("Uppercase  A – Z", _genUppercase);
        _genLowercase = EditorGUILayout.Toggle("Lowercase  a – z", _genLowercase);

        // ── Preview ────────────────────────────────────────────────────
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Preview Single Letter", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _prevUpper = EditorGUILayout.Toggle("Uppercase", _prevUpper, GUILayout.Width(120));
        _prevIdx   = EditorGUILayout.IntSlider(_prevIdx, 0, 25);
        char prevChar = (char)((_prevUpper ? 'A' : 'a') + _prevIdx);
        EditorGUILayout.LabelField(
            prevChar.ToString(),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 22 },
            GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Refresh Preview"))
            RefreshPreview();

        if (_prevCount >= 0)
            EditorGUILayout.HelpBox($"Bubble count for '{prevChar}': {_prevCount}",
                MessageType.None);

        if (_prevTex != null)
        {
            float size = Mathf.Min(EditorGUIUtility.currentViewWidth - 20f, 420f);
            Rect r = GUILayoutUtility.GetRect(size, size);
            EditorGUI.DrawPreviewTexture(r, _prevTex, null, ScaleMode.ScaleToFit);
        }

        // ── Generate button ────────────────────────────────────────────
        EditorGUILayout.Space(10);
        var oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
        if (GUILayout.Button("▶  Generate All Selected Layouts", GUILayout.Height(38)))
            GenerateAll();
        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndScrollView();
    }

    private void OnDestroy()
    {
        if (_prevTex != null) DestroyImmediate(_prevTex);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════════════════════════

    private void RefreshPreview()
    {
        char   c    = (char)((_prevUpper ? 'A' : 'a') + _prevIdx);
        string sub  = _prevUpper ? "Uppercase" : "Lowercase";
        string path = $"Assets/Pop It Alphabets/Alphabets/{sub}/{c}.png";

        EnsureReadableSprite(path);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"[BubbleGen] Sprite not found: {path}");
            return;
        }

        BubbleEntry[] entries = GenerateEntries(sprite, _gridSpacing, _bubbleSize, _alphaThreshold);
        _prevCount = entries.Length;

        if (_prevTex != null) DestroyImmediate(_prevTex);
        _prevTex = BuildPreviewTexture(sprite, entries);
        Repaint();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GENERATION
    // ═══════════════════════════════════════════════════════════════════

    private void GenerateAll()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects/BubbleLayouts");

        int ok = 0, skip = 0;

        for (int i = 0; i < 26; i++)
        {
            if (_genUppercase)
            {
                if (ProcessLetter((char)('A' + i), upper: true))  ok++;
                else                                               skip++;
            }
            if (_genLowercase)
            {
                if (ProcessLetter((char)('a' + i), upper: false)) ok++;
                else                                               skip++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>[BubbleGen] Done — {ok} layouts written, {skip} skipped.</color>");
    }

    private bool ProcessLetter(char c, bool upper)
    {
        string sub   = upper ? "Uppercase" : "Lowercase";
        string sPath = $"Assets/Pop It Alphabets/Alphabets/{sub}/{c}.png";

        EnsureReadableSprite(sPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[BubbleGen] Sprite not found: {sPath}");
            return false;
        }

        BubbleEntry[] entries = GenerateEntries(sprite, _gridSpacing, _bubbleSize, _alphaThreshold);
        if (entries.Length == 0)
        {
            Debug.LogWarning($"[BubbleGen] '{c}': no opaque pixels found " +
                             $"(threshold={_alphaThreshold:F2}). Skipping.");
            return false;
        }

        string prefix = upper ? $"Layout_Uppercase_{c}" : $"Layout_Lowercase_{c}";
        string aPath  = $"Assets/ScriptableObjects/BubbleLayouts/{prefix}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(aPath);
        if (existing == null)
        {
            var asset   = ScriptableObject.CreateInstance<BubbleLayoutData>();
            asset.bubbles = entries;
            AssetDatabase.CreateAsset(asset, aPath);
        }
        else
        {
            existing.bubbles = entries;
            EditorUtility.SetDirty(existing);
        }

        Debug.Log($"[BubbleGen] '{c}': {entries.Length} bubbles → {aPath}");
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CORE ALGORITHM
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Samples the sprite on a world-space grid and returns one BubbleEntry per
    /// grid cell that passes the erosion test: the centre pixel AND all 8 cardinal/
    /// diagonal sample points at bubble-radius distance must have alpha ≥ threshold.
    ///
    /// This keeps every bubble fully INSIDE the letter outline — none will sit on
    /// the semi-transparent border edge.
    ///
    /// Coordinate origin = sprite pivot (visual centre), matching LetterPuzzle local space.
    /// </summary>
    private static BubbleEntry[] GenerateEntries(
        Sprite sprite, float gridSpacing, float bubbleSize, float alphaThreshold)
    {
        Texture2D tex   = sprite.texture;
        float     ppu   = sprite.pixelsPerUnit;
        Rect      rect  = sprite.rect;    // pixel bounds inside the full texture
        Vector2   pivot = sprite.pivot;   // pixels from rect's bottom-left corner

        // Erosion radius in pixels = half the bubble's visual radius
        // Using 0.48 of size so the check is slightly inset from the bubble edge
        int erosionPx = Mathf.Max(1, Mathf.RoundToInt(bubbleSize * ppu * 0.48f));

        // World-space bounds of this sprite (relative to its pivot)
        float wLeft   = -pivot.x / ppu;
        float wRight  =  (rect.width  - pivot.x) / ppu;
        float wBottom = -pivot.y / ppu;
        float wTop    =  (rect.height - pivot.y) / ppu;

        // Centre the grid so bubbles are evenly inset from the sprite edges
        float rangeW = wRight  - wLeft;
        float rangeH = wTop    - wBottom;
        float startX = wLeft   + (rangeW % gridSpacing) * 0.5f + gridSpacing * 0.5f;
        float startY = wBottom + (rangeH % gridSpacing) * 0.5f + gridSpacing * 0.5f;

        // 8-directional offsets for the erosion check (cardinal + diagonal)
        var offsets = new (int dx, int dy)[]
        {
            ( erosionPx,          0), (-erosionPx,          0),
            (         0,  erosionPx), (         0, -erosionPx),
            ( erosionPx,  erosionPx), (-erosionPx,  erosionPx),
            ( erosionPx, -erosionPx), (-erosionPx, -erosionPx),
        };

        var entries = new List<BubbleEntry>();

        for (float wy = startY; wy < wTop; wy += gridSpacing)
        {
            for (float wx = startX; wx < wRight; wx += gridSpacing)
            {
                // Convert world position to texture pixel using pivot as origin
                int cx = Mathf.RoundToInt(rect.x + pivot.x + wx * ppu);
                int cy = Mathf.RoundToInt(rect.y + pivot.y + wy * ppu);

                // Skip if centre is outside sprite rect
                if (cx < (int)rect.x || cx >= (int)(rect.x + rect.width))  continue;
                if (cy < (int)rect.y || cy >= (int)(rect.y + rect.height)) continue;

                // Centre pixel must be opaque
                if (tex.GetPixel(cx, cy).a < alphaThreshold) continue;

                // Erosion check: all 8 surrounding sample points must also be opaque.
                // This ensures the bubble sits fully inside the letter outline.
                bool inside = true;
                foreach (var (dx, dy) in offsets)
                {
                    int sx = cx + dx;
                    int sy = cy + dy;

                    // A sample point outside the texture rect means we're on the border — reject
                    if (sx < (int)rect.x || sx >= (int)(rect.x + rect.width) ||
                        sy < (int)rect.y || sy >= (int)(rect.y + rect.height))
                    {
                        inside = false;
                        break;
                    }

                    if (tex.GetPixel(sx, sy).a < alphaThreshold)
                    {
                        inside = false;
                        break;
                    }
                }

                if (inside)
                    entries.Add(new BubbleEntry
                    {
                        position = new Vector2(wx, wy),
                        size     = bubbleSize
                    });
            }
        }

        return entries.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PREVIEW TEXTURE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a Texture2D showing the letter sprite with filled red/orange
    /// circles overlaid at each bubble position for visual confirmation.
    /// </summary>
    private static Texture2D BuildPreviewTexture(Sprite sprite, BubbleEntry[] entries)
    {
        Texture2D src   = sprite.texture;
        Rect      rect  = sprite.rect;
        float     ppu   = sprite.pixelsPerUnit;
        Vector2   pivot = sprite.pivot;

        int w = (int)rect.width;
        int h = (int)rect.height;

        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.filterMode = FilterMode.Bilinear;

        // Copy the sprite's pixel region from the source texture
        Color[] pixels = src.GetPixels((int)rect.x, (int)rect.y, w, h);
        dst.SetPixels(pixels);

        // Draw a filled circle for every bubble
        Color fillColor = new Color(1f, 0.25f, 0.1f, 0.70f);  // translucent red-orange
        Color rimColor  = new Color(1f, 0.95f, 0f, 1f);        // solid yellow rim

        foreach (var e in entries)
        {
            // World → pixel in dst-local space (0,0 = rect bottom-left)
            int cx = Mathf.RoundToInt(pivot.x + e.position.x * ppu);
            int cy = Mathf.RoundToInt(pivot.y + e.position.y * ppu);
            int r  = Mathf.Max(3, Mathf.RoundToInt(e.size * ppu * 0.46f));
            int r2 = r * r;
            int ri = r - 2;
            int ri2 = ri * ri;

            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int dist2 = dx * dx + dy * dy;
                if (dist2 > r2) continue;

                int ox = cx + dx;
                int oy = cy + dy;
                if (ox < 0 || ox >= w || oy < 0 || oy >= h) continue;

                bool isRim = dist2 > ri2;
                Color c = isRim ? rimColor : fillColor;

                // Alpha-blend onto the existing pixel so the letter shows through
                Color bg  = dst.GetPixel(ox, oy);
                float a   = c.a;
                dst.SetPixel(ox, oy,
                    new Color(
                        bg.r * (1 - a) + c.r * a,
                        bg.g * (1 - a) + c.g * a,
                        bg.b * (1 - a) + c.b * a,
                        Mathf.Max(bg.a, a)));
            }
        }

        dst.Apply();
        return dst;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the PNG at assetPath is imported as Sprite (2D and UI)
    /// AND has Read/Write enabled so GetPixel() works.
    /// </summary>
    private static void EnsureReadableSprite(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;

        bool changed = false;

        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!imp.isReadable)
        {
            imp.isReadable = true;
            changed = true;
        }

        if (changed) imp.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(path)?.Replace('\\', '/'),
                Path.GetFileName(path));
    }
}
