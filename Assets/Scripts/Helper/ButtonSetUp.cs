using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonSetUp : MonoBehaviour
{
    public bool isPressed;
    public float holdPress = 0f;
    public float sensitivity = 10f; 
    // Start is called before the first frame update
    void Start()
    {
        SetUpButton();
    }

    // Update is called once per frame
    void Update()
    {
        if (isPressed)
        {
            holdPress += sensitivity * Time.deltaTime/3;
        }
        else
        {
            holdPress -= sensitivity * Time.deltaTime/4;  
        }
        holdPress = Mathf.Clamp01(holdPress);
    }

    void SetUpButton()
    {
        EventTrigger trigger = gameObject.AddComponent<EventTrigger>();
        var pointerDown = new EventTrigger.Entry();
        pointerDown.eventID = EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((e) => onClickDown());

        var pointerUp = new EventTrigger.Entry();
        pointerUp.eventID = EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((e) => onClickUp());

        trigger.triggers.Add(pointerDown);
        trigger.triggers.Add(pointerUp);
    }

    void onClickDown()
    {
        isPressed = true;
    }

        void onClickUp()
    {
        isPressed = false;
    }
}
