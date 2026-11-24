using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewCameraContoller : MonoBehaviour
{

    [SerializeField] GameObject cameraPivot, cameraDefault;
    private PlayerCarController playerScript;
    private Rigidbody playerRB;
    private Camera mainCam;
    private float smoothTime = 0.99f, currentMaxSmoothTime;
    private int currentCarSpeed;    

    [Header("Nitro Camera")]
    [SerializeField] float nitroKickBackDistance = 1.6f;   // extra distance behind normal right after nitro press
    [SerializeField] float nitroHoldDistance = 1.0f;       // extra distance kept while nitro stays on
    [SerializeField] float nitroKickDuration = 1.0f;       // seconds before easing from kick-back to hold
    [SerializeField] float kickBackTime = 0.12f;           // smoothing time of the quick pull back (settles in ~0.3s)
    [SerializeField] float settleTime = 0.35f;             // smoothing time when easing from kick-back to hold
    [SerializeField] float returnTime = 0.45f;             // smoothing time when returning after nitro (~1s)
    [SerializeField] float nitroFovBoost = 12f;            // extra field of view at full kick-back (scales with the pull back)

    [Header("Camera Rotation")]
    [SerializeField] float rotationFollowSpeed = 6f;       // how fast the camera turns to follow the car's travel direction

    private float nitroOffset = 0f, nitroOffsetVel = 0f, nitroTimer = 0f, baseFov = 60f;
    private bool wasNitroActive = false;

    void Start()
    {
        playerScript = GameObject.Find("FixedAltima").GetComponent<PlayerCarController>();
        playerRB = playerScript.GetComponent<Rigidbody>();
        mainCam = GetComponentInChildren<Camera>();
        if(mainCam != null)
        {
            baseFov = mainCam.fieldOfView;
        }
        fun_SetCamDefault();
    }

    void FixedUpdate()
    {
        currentCarSpeed = ((int)playerScript.currentSpeed);
        fun_changeCameraLook();
        smoothTime = fun_EvaluteSmoothValue();
        transform.position = Vector3.Lerp(transform.position, fun_GetNitroTargetPosition(), smoothTime);
        fun_UpdateRotation();
        fun_UpdateFov();
    }


    private void fun_SetCamDefault()
    {
        transform.position = cameraDefault.transform.position;
        transform.rotation = cameraDefault.transform.rotation;
    }

    private float fun_changeCameraLook()
    {
        if(playerScript.nitroStart)
        {
            // light speed lag only; the nitro pull back itself is handled by fun_GetNitroTargetPosition
            currentMaxSmoothTime = 0.85f;   
        }
        else if(currentCarSpeed > 5)
        {
            currentMaxSmoothTime = 0.99f;
        }
        else if(currentCarSpeed < 5)
        {
            smoothTime = 1f;
            currentMaxSmoothTime = 1f;
        }

        return currentMaxSmoothTime;
    }

    private float fun_EvaluteSmoothValue()
    {
        if(currentMaxSmoothTime>smoothTime)
        {
            smoothTime = smoothTime+Time.fixedDeltaTime/2;
        }
        if(currentMaxSmoothTime<smoothTime)
        {
            smoothTime = smoothTime-Time.fixedDeltaTime;
        }

        return smoothTime;
    }

    // Returns the camera follow target, pushed back behind the car while nitro is active.
    // Nitro pressed -> quick eased pull back to nitroKickBackDistance; after nitroKickDuration -> ease in to nitroHoldDistance;
    // nitro released -> slow eased return to the normal pivot position.
    private Vector3 fun_GetNitroTargetPosition()
    {
        bool nitroActive = playerScript.nitroStart;
        if(nitroActive && !wasNitroActive)
        {
            nitroTimer = 0f;
        }
        wasNitroActive = nitroActive;

        float targetOffset, dampTime;
        if(nitroActive)
        {
            nitroTimer += Time.fixedDeltaTime;
            if(nitroTimer < nitroKickDuration)
            {
                targetOffset = nitroKickBackDistance;
                dampTime = kickBackTime;
            }
            else
            {
                targetOffset = nitroHoldDistance;
                dampTime = settleTime;
            }
        }
        else
        {
            targetOffset = 0f;
            dampTime = returnTime;
        }

        nitroOffset = Mathf.SmoothDamp(nitroOffset, targetOffset, ref nitroOffsetVel, dampTime, Mathf.Infinity, Time.fixedDeltaTime);

        Vector3 back = -playerScript.transform.forward;
        back.y = 0f;
        if(back.sqrMagnitude > 0.0001f)
        {
            back.Normalize();
        }
        return cameraPivot.transform.position + back * nitroOffset;
    }

    // Smoothly turns the camera to follow the car's travel direction (trails the car a little in drifts and turns).
    private void fun_UpdateRotation()
    {
        Vector3 dir = playerScript.transform.forward;
        if(playerRB != null)
        {
            Vector3 vel = playerRB.linearVelocity;
            if(vel.sqrMagnitude > 4f && Vector3.Dot(vel, dir) > 0f)   // moving forward faster than ~7 km/h
            {
                dir = vel;
            }
        }
        dir.y = 0f;
        if(dir.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationFollowSpeed * Time.fixedDeltaTime));
    }

    // Widens the field of view together with the nitro pull back for a stronger sense of speed.
    private void fun_UpdateFov()
    {
        if(mainCam == null)
        {
            return;
        }
        float amount = nitroKickBackDistance > 0f ? nitroOffset / nitroKickBackDistance : 0f;
        mainCam.fieldOfView = baseFov + nitroFovBoost * Mathf.Clamp(amount, 0f, 1.2f);
    }
}
