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


    private float timestore; // Duration for the popup animation

    private void Awake()
    {
        infopanel.SetActive(false);
    }

    private void Start()
    {
        infoBtn.onClick.AddListener(infodo);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)){
            Debug.Log("Escape");
            StartCoroutine(off());
        }
    }
    
    public void infodo()
    {
        Debug.Log("infodo");
        infopanel.SetActive(true);
        if (anim != null) anim.Play("Open");
        OnpauseFun();
    }

    public void OnpauseFun()
    {
        timestore = Time.timeScale;
        Time.timeScale = 0;
    }

    public void OFFpauseFun()
    {
        Time.timeScale = timestore;
    }

    public void undoinfodo()
    {
        Debug.Log("Info Panel Closed");
        StartCoroutine(off());

    }

    IEnumerator off()
    {
        if (!infopanel.activeSelf) yield break;
        
        float waitTime = 0.5f; // fallback

        if (anim != null) 
        {
            anim.Play("Close");
            anim.Update(0f); // Force Animator state update
            waitTime = anim.GetCurrentAnimatorStateInfo(0).length;
        }

        Debug.Log("Info Panel Closing Animation Started. Expected duration: " + waitTime);
        yield return new WaitForSecondsRealtime(waitTime);
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
