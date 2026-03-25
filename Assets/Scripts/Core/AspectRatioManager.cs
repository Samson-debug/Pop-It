using UnityEngine;

public class AspectRatioManager : MonoBehaviour
{
    public float targetAspect = 16f / 9f;

    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private Camera _cam;

    void Start()
    {
        _cam = GetComponent<Camera>();
        UpdateAspectRatio();
    }

    void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            UpdateAspectRatio();
        }
    }

    void UpdateAspectRatio()
    {
        if (_cam == null) return;

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1.0f)
        {
            // Add letterbox (black bars top & bottom)
            Rect rect = _cam.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            _cam.rect = rect;
        }
        else
        {
            // Add pillarbox (black bars left & right)
            float scaleWidth = 1.0f / scaleHeight;

            Rect rect = _cam.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            _cam.rect = rect;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
    }
}