using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    [SerializeField]
    private Button infoBtn;
    [SerializeField]
    private GameObject infopanel; // Reference to the Info Panel GameObject
    [SerializeField]
    private Animator anim; // Reference to the Info Panel GameObject


    [SerializeField]
    private float popupoff = 0.5f; // Duration for the popup animation

    private float timestore; // Duration for the popup animation

    private void Awake()
    {
        infopanel.SetActive(false);
    }

    private void Start()
    {
        infoBtn.onClick.AddListener(infodo);
    }

    public void infodo()
    {
        Debug.Log("infodo");
        infopanel.SetActive(true);
        OnpauseFun();
    }

    public void OnpauseFun()
    {
        timestore = Time.timeScale;
        Time.timeScale = 0;
    }

    public void OFFpauseFun()
    {
        Time.timeScale = 1;

    }

    public void undoinfodo()
    {
        Debug.Log("Info Panel Closed");
        StartCoroutine(off());

    }

    IEnumerator off()
    {
        Debug.Log("Info Panel Closing Animation Started");
        yield return new WaitForSecondsRealtime(popupoff);
        Debug.Log("Info Panel Closing Animation Ended");
        OFFpauseFun();
        infopanel.SetActive(false);
    }

    public void OpenURL()
    {
        Application.OpenURL("https://toddlyfun.com/");
        Debug.Log("URL opened!");
    }
}
