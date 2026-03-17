using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene-based Bubble Layout Editor.
///
/// Workflow:
///   1. Pick a letter (uppercase or lowercase).
///   2. Click "Spawn in Scene" — the letter sprite appears with existing bubbles.
///   3. Move / add / delete bubbles freely in the Scene view.
///   4. Click "Save Layout from Scene" — positions are written to the
///      BubbleLayoutData ScriptableObject for that letter.
///   5. Click "Clear Scene" when done.
///
/// Open via: PopIt Tools ▶ 5 - Edit Bubble Layout in Scene
/// </summary>
public class BubbleLayoutSceneEditor : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────
    private int   _letterIdx  = 0;       // 0-25
    private bool  _uppercase  = true;
    private float _bubbleSize = 0.60f;

    private GameObject _editRoot;        // "[EDIT] F" root object
    private Transform  _bubblesContainer;

    private Vector2 _scroll;

    // ═══════════════════════════════════════════════════════════════════
    //  MENU
    // ═══════════════════════════════════════════════════════════════════

    [MenuItem("PopIt Tools/5 - Edit Bubble Layout in Scene", priority = 5)]
    public static void OpenWindow() =>
        GetWindow<BubbleLayoutSceneEditor>("Bubble Layout Editor").Show();

    // ═══════════════════════════════════════════════════════════════════
    //  GUI
    // ═══════════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ── Header ────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bubble Layout Scene Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Pick a letter and click Spawn in Scene.\n" +
            "2. Select, move or delete bubbles in the Scene view (Transform gizmos).\n" +
            "3. Use Add Bubble to place a new one at the scene origin.\n" +
            "4. Click Save Layout — writes positions to the BubbleLayoutData asset.\n" +
            "5. Click Clear Scene when finished.",
            MessageType.Info);

        // ── Letter picker ─────────────────────────────────────────────
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Letter", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _uppercase  = EditorGUILayout.Toggle("Uppercase", _uppercase, GUILayout.Width(130));
        _letterIdx  = EditorGUILayout.IntSlider(_letterIdx, 0, 25);
        char letter = CurrentLetter();
        EditorGUILayout.LabelField(
            letter.ToString(),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 24 },
            GUILayout.Width(32));
        EditorGUILayout.EndHorizontal();

        _bubbleSize = EditorGUILayout.Slider(
            new GUIContent("Default Bubble Size",
                "Scale applied to new bubbles added with 'Add Bubble'."),
            _bubbleSize, 0.20f, 1.10f);

        // ── Spawn button ──────────────────────────────────────────────
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.5f, 0.75f, 1f);
        if (GUILayout.Button($"Spawn  '{letter}'  in Scene", GUILayout.Height(32)))
            SpawnForEditing();
        GUI.backgroundColor = Color.white;

        // ── Active edit section ───────────────────────────────────────
        if (_editRoot != null)
        {
            SyncContainerRef();   // re-link if Undo wiped the reference
            int count = _bubblesContainer != null ? _bubblesContainer.childCount : 0;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Active Edit Session", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Editing: '{letter}'   |   Bubbles in scene: {count}\n" +
                "Move bubbles with the standard Transform gizmo.\n" +
                "Delete a bubble: select it → press Delete.",
                count > 0 ? MessageType.Info : MessageType.Warning);

            // Letter A-Z quick-jump grid
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick-Jump (keeps current case)", EditorStyles.miniLabel);
            DrawLetterGrid();

            EditorGUILayout.Space(6);

            // Add bubble button
            if (GUILayout.Button("＋  Add Bubble at Origin"))
                AddBubble();

            // Select all / frame
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All Bubbles"))
                SelectAllBubbles();
            if (GUILayout.Button("Frame in Scene View"))
                FrameEditRoot();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Save button
            GUI.backgroundColor = new Color(0.3f, 0.88f, 0.3f);
            if (GUILayout.Button($"  Save Layout from Scene  ({count} bubbles)", GUILayout.Height(36)))
                SaveLayout();
            GUI.backgroundColor = Color.white;

            // Clear button
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.55f, 0.4f);
            if (GUILayout.Button("Clear Scene (remove edit objects)"))
                ClearScene();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        // Repaint every frame so bubble count stays current
        if (_editRoot != null) Repaint();
    }

    // ── Quick-jump grid A-Z ───────────────────────────────────────────
    private void DrawLetterGrid()
    {
        const int cols = 13;
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 26; i++)
        {
            if (i > 0 && i % cols == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
            char c    = (char)((_uppercase ? 'A' : 'a') + i);
            bool active = i == _letterIdx;
            GUI.backgroundColor = active ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(c.ToString(), GUILayout.Width(28), GUILayout.Height(22)))
            {
                _letterIdx = i;
                SpawnForEditing();
            }
        }
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SPAWN
    // ═══════════════════════════════════════════════════════════════════

    private void SpawnForEditing()
    {
        ClearScene(silent: true);

        char   letter = CurrentLetter();
        string sub    = _uppercase ? "Uppercase" : "Lowercase";

        // ── Letter sprite ─────────────────────────────────────────────
        string spritePath = $"Assets/Pop It Alphabets/Alphabets/{sub}/{letter}.png";
        EnsureReadable(spritePath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        // ── Existing layout ───────────────────────────────────────────
        string layoutPath = LayoutAssetPath(letter);
        var    layout     = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(layoutPath);

        // ── Bubble prefab ─────────────────────────────────────────────
        var bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bubble.prefab");

        // ── Build scene hierarchy ─────────────────────────────────────
        _editRoot = new GameObject($"[EDIT]  {letter}");
        Undo.RegisterCreatedObjectUndo(_editRoot, $"Spawn Edit {letter}");

        // Letter background
        var bgGO = new GameObject("LetterBackground");
        bgGO.transform.SetParent(_editRoot.transform, false);
        bgGO.transform.localPosition = Vector3.zero;
        var sr = bgGO.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 1;
        // Make it non-selectable so clicks go to bubbles instead
        bgGO.hideFlags = HideFlags.NotEditable;

        // Bubbles container
        var containerGO = new GameObject("BubblesContainer");
        containerGO.transform.SetParent(_editRoot.transform, false);
        _bubblesContainer = containerGO.transform;

        // Spawn bubbles from existing layout
        if (layout != null && layout.bubbles != null)
        {
            foreach (var entry in layout.bubbles)
                SpawnBubbleGO(entry.position, entry.size, bubblePrefab);
        }

        // Focus scene view
        Selection.activeGameObject = _editRoot;
        FrameEditRoot();
        Repaint();

        Debug.Log($"[BubbleEditor] Spawned '{letter}' with " +
                  $"{_bubblesContainer.childCount} bubbles. " +
                  "Edit in Scene view then Save.");
    }

    private GameObject SpawnBubbleGO(Vector2 pos, float size, GameObject prefab)
    {
        GameObject go;
        if (prefab != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _bubblesContainer);
        else
        {
            go = new GameObject("Bubble");
            go.transform.SetParent(_bubblesContainer, false);
            // Minimal visual so you can see it
            var sr          = go.AddComponent<SpriteRenderer>();
            sr.color        = new Color(0.3f, 0.7f, 1f, 0.8f);
            sr.sortingOrder = 2;
        }
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale    = Vector3.one * size;
        return go;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ADD BUBBLE
    // ═══════════════════════════════════════════════════════════════════

    private void AddBubble()
    {
        if (_bubblesContainer == null) { Debug.LogWarning("[BubbleEditor] No active edit session."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bubble.prefab");
        var go     = SpawnBubbleGO(Vector2.zero, _bubbleSize, prefab);
        Undo.RegisterCreatedObjectUndo(go, "Add Bubble");

        Selection.activeGameObject = go;
        SceneView.FrameLastActiveSceneView();
        Repaint();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SAVE
    // ═══════════════════════════════════════════════════════════════════

    private void SaveLayout()
    {
        SyncContainerRef();
        if (_bubblesContainer == null) { Debug.LogWarning("[BubbleEditor] No BubblesContainer found."); return; }

        char letter = CurrentLetter();
        var  entries = new List<BubbleEntry>();

        foreach (Transform child in _bubblesContainer)
        {
            entries.Add(new BubbleEntry
            {
                position = new Vector2(child.localPosition.x, child.localPosition.y),
                size     = child.localScale.x    // uniform scale
            });
        }

        if (entries.Count == 0)
        {
            if (!EditorUtility.DisplayDialog("Save Empty Layout",
                $"No bubbles in scene for '{letter}'. Save empty layout?",
                "Yes", "Cancel"))
                return;
        }

        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects/BubbleLayouts");

        string path     = LayoutAssetPath(letter);
        var    existing = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(path);

        if (existing == null)
        {
            var asset   = ScriptableObject.CreateInstance<BubbleLayoutData>();
            asset.bubbles = entries.ToArray();
            AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            Undo.RecordObject(existing, $"Save Bubble Layout {letter}");
            existing.bubbles = entries.ToArray();
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"<color=lime>[BubbleEditor] Saved {entries.Count} bubbles for '{letter}' → {path}</color>");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CLEAR
    // ═══════════════════════════════════════════════════════════════════

    private void ClearScene(bool silent = false)
    {
        if (_editRoot != null)
        {
            Undo.DestroyObjectImmediate(_editRoot);
            _editRoot         = null;
            _bubblesContainer = null;
        }
        else
        {
            // Also clean up any stale [EDIT] objects (e.g. from a previous session)
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && go.name.StartsWith("[EDIT]") && go.transform.parent == null)
                    Undo.DestroyObjectImmediate(go);
            }
        }
        if (!silent) Repaint();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private char CurrentLetter() =>
        (char)((_uppercase ? 'A' : 'a') + _letterIdx);

    private string LayoutAssetPath(char c) =>
        $"Assets/ScriptableObjects/BubbleLayouts/" +
        $"Layout_{(_uppercase ? "Uppercase" : "Lowercase")}_{c}.asset";

    /// Re-links _bubblesContainer if the root still exists but the ref was lost.
    private void SyncContainerRef()
    {
        if (_editRoot == null) return;
        if (_bubblesContainer != null) return;
        var t = _editRoot.transform.Find("BubblesContainer");
        if (t != null) _bubblesContainer = t;
    }

    private void SelectAllBubbles()
    {
        SyncContainerRef();
        if (_bubblesContainer == null) return;
        var gos = new List<GameObject>();
        foreach (Transform child in _bubblesContainer)
            gos.Add(child.gameObject);
        Selection.objects = gos.ToArray();
    }

    private void FrameEditRoot()
    {
        if (_editRoot == null) return;
        Selection.activeGameObject = _editRoot;
        SceneView.FrameLastActiveSceneView();
    }

    private static void EnsureReadable(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.textureType != TextureImporterType.Sprite)
        { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!imp.isReadable) { imp.isReadable = true; changed = true; }
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
