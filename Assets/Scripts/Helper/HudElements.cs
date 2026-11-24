using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class HudElements : MonoBehaviour
{

    public Slider nitroSlider;
    public Gradient nitroGradient;
    public RawImage nitroFill;


    private float currentTimer = 0f; 

    public FinishLine finishLine;

    public TextMeshProUGUI timer;
    public GameObject finishedTxt;
    void Start()
    {
        InvokeRepeating("fun_Displaytimer", 0.4f, 0.2f );
    }
    void Update()
    {
        if (finishLine.bolGameFinished == false)
        {
            currentTimer = currentTimer + Time.deltaTime;
           // Debug.Log("Current timer : "+currentTimer);
        }
        else
        {
            Debug.Log("Race finished  at : "+currentTimer);
            //if race is finsihed
            finishedTxt.SetActive(true);
     
        }

    }

    public void fun_SetMaxNitroCapacity(float maxNiroCapacity)
    {
        nitroSlider.maxValue = maxNiroCapacity;
        nitroSlider.minValue = 0f;
        nitroFill.color = nitroGradient.Evaluate(nitroSlider.normalizedValue);
    }

    public void fun_SetCurentNitro(float currentNitro)
    {
        nitroSlider.value = currentNitro;
        nitroFill.color = nitroGradient.Evaluate(nitroSlider.normalizedValue);
    }

    public void fun_Displaytimer()
    {
        timer.text = "Time Lapse:"+((int)currentTimer).ToString();
    }
}
