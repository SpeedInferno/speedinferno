using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    // Start is called before the first frame update

    public bool bolGameFinished = false;


    //*************************************
    //**        Collision Events    *******
    //*************************************
     public void OnTriggerEnter(Collider collider)
     {
        if(collider.CompareTag("Player"))
        {
            bolGameFinished =  true;
        }    
     
     }

}
