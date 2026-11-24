using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSounds : MonoBehaviour
{
    public AudioSource runningSound;
    public float runningMaxVolume;
    public float runningMaxPitch;

    public AudioSource driftSound;
    public float driftMaxVolume;
    public float driftMaxPitch;

    private PlayerCarController carController;
    private float speedRatio;
    private float revLimiter;
    public float LimiterSound =  1f;
    public float LimiterFreq = 3f;
    public float LimiterEngage = 0.8f;

    // Start is called before the first frame update
    void Start()
    {
        carController =  GetComponent<PlayerCarController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (carController)
        {
            speedRatio = carController.fun_getSpeedRatio();

        }
        else
            Debug.Log("PlayerCarController Script not found");
        

        if (speedRatio> LimiterEngage)
        {
            revLimiter = (Mathf.Sin(Time.time * LimiterFreq) + 1f) * LimiterSound * (speedRatio - LimiterEngage);
        }
        runningSound.volume = Mathf.Lerp(0.1f, runningMaxVolume, speedRatio);
        runningSound.pitch = Mathf.Lerp(runningSound.pitch, Mathf.Lerp(0.5f, runningMaxPitch, speedRatio) + revLimiter, Time.deltaTime);

    
    }
}
