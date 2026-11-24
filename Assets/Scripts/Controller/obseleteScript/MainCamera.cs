using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MainCamera : MonoBehaviour
{

    public GameObject[] cameraPivot;
    private PlayerCarController playerScript;
    private int currentCarSpeed ,camlocIndexNew = 0,camlocIndexOld = 0;

    [SerializeField]private float smoothTime = 0f;


    void Start()
    {
        playerScript = GameObject.Find("FixedAltima").GetComponent<PlayerCarController>();
        fun_SetCamDefault();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        currentCarSpeed = ((int)playerScript.currentSpeed);
        camlocIndexNew = fun_fetchCamLocationIndex(currentCarSpeed);
        
        if(camlocIndexNew!= camlocIndexOld)
        {
            smoothTime = 0f;
        }

        smoothTime = fun_changeCameraLook();
        transform.position = Vector3.Lerp(transform.position,cameraPivot[camlocIndexNew].transform.position, smoothTime);
        transform.LookAt(cameraPivot[camlocIndexNew].transform);
        camlocIndexOld = camlocIndexNew;
    }

    //function to set default position of camera at the start of the game
    private void fun_SetCamDefault()
    {
        transform.position = cameraPivot[camlocIndexNew].transform.position;
    }

    // function to return camera location value according to speed
    private int fun_fetchCamLocationIndex(int speed)
    {   
        Debug.Log(smoothTime);
        if(speed < 15)
        {
            return 0;
        }
        else
            return 1;
    }

    private float fun_changeCameraLook()
    {
        if(smoothTime < 0.96f && currentCarSpeed > 5)
        {
            smoothTime = smoothTime + Time.fixedDeltaTime/3;
        }
        else if(currentCarSpeed < 2)
        {
            smoothTime = 1f;
        }

        return smoothTime;
    }
}
