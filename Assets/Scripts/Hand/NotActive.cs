using UnityEngine;

public class NotActive : MonoBehaviour
{
    public GameObject objectToTurnOn;
    public float idleTime = 5f;

    private float timer = 0f;
    private bool isGameStarted = false;

    private void OnEnable()
    {
        GameManager.OnGameStartedEvent += HandleGameStarted;
        Bubble.OnAnyBubblePopped += HandleBubblePopped;
    }

    private void OnDisable()
    {
        GameManager.OnGameStartedEvent -= HandleGameStarted;
        Bubble.OnAnyBubblePopped -= HandleBubblePopped;
    }

    private void HandleGameStarted()
    {
        isGameStarted = true;
        timer = 0f;
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    private void HandleBubblePopped()
    {
        timer = 0f;
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    void Start()
    {
        if (objectToTurnOn != null)
            objectToTurnOn.SetActive(false);
    }

    void Update()
    {
        if (!isGameStarted) return;
        else
        {
            timer += Time.deltaTime;

            // If no input for idle time -> turn on
            if (timer >= idleTime)
            {
                if (objectToTurnOn != null && !objectToTurnOn.activeSelf)
                {
                    PositionOnUnpoppedBubble();
                    objectToTurnOn.SetActive(true);
                }
            }
        }
    }

    private void PositionOnUnpoppedBubble()
    {
        Bubble[] bubbles = FindObjectsOfType<Bubble>();
        foreach (var bubble in bubbles)
        {
            if (!bubble.IsPopped)
            {
                transform.position = bubble.transform.position;
                break;
            }
        }
    }
}
