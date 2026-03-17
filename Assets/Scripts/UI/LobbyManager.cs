using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Lobby panel shown at game start.
///
/// The lobby presents two mode buttons:
///   - Capital Letters  → starts gameplay in uppercase mode
///   - Small Letters    → starts gameplay in lowercase mode
///
/// When a button is tapped:
///   1. LobbyPanel is hidden
///   2. GameplayPanel is shown
///   3. GameManager.StartGame(uppercase) is called
///
/// ─── Scene hierarchy expected ───────────────────────────────────
/// Canvas
///   LobbyPanel                   ← assign to lobbyPanel
///     Background      (Image: Alphabet BG.png, stretch fill)
///     InfoButton      (Button: Alphabet info button.png, top-right)
///     ButtonsContainer
///       CapitalButton            ← assign to capitalButton
///         ButtonBG    (Image: capital letters.png)
///         LabelText   (TextMeshProUGUI / Text: "CAPITAL\nLETTERS")
///       SmallButton              ← assign to smallButton
///         ButtonBG    (Image: small letters.png)
///         LabelText   (TextMeshProUGUI / Text: "SMALL\nLETTERS")
///   GameplayPanel                ← assign to gameplayPanel (inactive at start)
///     … (all gameplay UI)
/// ────────────────────────────────────────────────────────────────
/// </summary>
public class LobbyManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector references
    // ------------------------------------------------------------------ //
    [Header("Panels")]
    [Tooltip("The root lobby panel GameObject. Shown on app launch.")]
    public GameObject lobbyPanel;

    [Tooltip("The root gameplay panel GameObject. Hidden on app launch.")]
    public GameObject gameplayPanel;

    [Header("Lobby Buttons")]
    [Tooltip("Button that launches Capital / Uppercase letter mode.")]
    public Button capitalButton;

    [Tooltip("Button that launches Small / Lowercase letter mode.")]
    public Button smallButton;

    [Header("Info Panel (shared between lobby and gameplay)")]
    public Button infoButton;
    public GameObject infoPanel;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void Start()
    {
        // Make sure we start on the lobby
        ShowLobby();

        // Wire buttons
        if (capitalButton != null)
            capitalButton.onClick.AddListener(OnCapitalLettersPressed);

        if (smallButton != null)
            smallButton.onClick.AddListener(OnSmallLettersPressed);

        if (infoButton != null)
            infoButton.onClick.AddListener(ToggleInfoPanel);

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ //
    //  Button callbacks
    // ------------------------------------------------------------------ //

    private void OnCapitalLettersPressed()
    {
        LaunchGame(uppercase: true);
    }

    private void OnSmallLettersPressed()
    {
        LaunchGame(uppercase: false);
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>Return to the lobby (e.g. from a Home button inside gameplay).</summary>
    public void ShowLobby()
    {
        if (lobbyPanel   != null) lobbyPanel.SetActive(true);
        if (gameplayPanel != null) gameplayPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    private void LaunchGame(bool uppercase)
    {
        // Hide lobby, reveal gameplay
        if (lobbyPanel    != null) lobbyPanel.SetActive(false);
        if (gameplayPanel != null) gameplayPanel.SetActive(true);

        // Kick off the game in the chosen mode
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame(uppercase);
        else
            Debug.LogError("[LobbyManager] GameManager.Instance is null. " +
                           "Make sure GameManager is in the scene.");
    }

    private void ToggleInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(!infoPanel.activeSelf);
    }
}
