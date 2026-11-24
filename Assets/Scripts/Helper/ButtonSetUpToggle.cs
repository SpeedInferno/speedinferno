using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonSetUpToggle : MonoBehaviour
{
    public bool isPressedToggle = false;
    private Button button;
    // Start is called before the first frame update
    void Start()
    {
        SetUpButton();
    }


    void SetUpButton()
    {
        /*EventTrigger trigger = gameObject.AddComponent<EventTrigger>();
        var pointerToggle = new EventTrigger.Entry();
        pointerToggle.eventID = EventTriggerType.PointerClick;
        pointerToggle.callback.AddListener((e) => onClick());*/

      /*  var pointerUp = new EventTrigger.Entry();
        pointerUp.eventID = EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((e) => onClickUp());*/

        //trigger.triggers.Add(pointerToggle);
        //trigger.triggers.Add(pointerUp);

        // Get the reference to the Button component attached to the GameObject
        button = GetComponent<Button>();

        // Add a listener to the button's onClick event
        button.onClick.AddListener(onClick);
    }

    public void onClick()
    {

        if(isPressedToggle == true)
        {
            isPressedToggle = false;
        }
        else
        {
            isPressedToggle = true;       
        }
    }

}
