using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    public GameObject instructionsPanel;

    private void Awake()
    {
        if (instructionsPanel != null)
            instructionsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.OnGameStartedEvent += HandleGameStart;
        GameManager.OnReturnedToLobbyEvent += DeactivateInstructions;
        Bubble.OnAnyBubblePopped += DeactivateInstructions;
    }

    private void OnDisable()
    {
        GameManager.OnGameStartedEvent -= HandleGameStart;
        Bubble.OnAnyBubblePopped -= DeactivateInstructions;
    }

    private void HandleGameStart()
    {
        if (instructionsPanel != null)
            instructionsPanel.SetActive(true);
    }

    private void DeactivateInstructions()
    {
        if (instructionsPanel != null && instructionsPanel.activeSelf)
        {
            instructionsPanel.SetActive(false);
        }
    }
}