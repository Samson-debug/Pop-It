using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LoadingPanel : MonoBehaviour
{
    public Image progressbar;
    public TextMeshProUGUI progressText;

    private void Update()
    {
        float progress = progressbar.fillAmount;

        progressText.text = "Loading..." + (progress * 100f).ToString("0") + "%";

        if (progress >= 0.99f)
        {
            progressText.text = "Loading...100%";
            StartCoroutine(loadscene());
        }
    }

    IEnumerator loadscene()
    {
        yield return new WaitForSeconds(0.8f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

    }
}
