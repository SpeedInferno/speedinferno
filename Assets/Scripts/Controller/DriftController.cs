using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriftController : MonoBehaviour
{
    public GameObject targetObject; // Reference to the target GameObject
    public float maxRotationAngle = 45.0f; // Maximum rotation angle during drifting
    public float counterSteeringSpeed = 5.0f; // Speed at which counter-steering occurs

    public float currentYawAngle = 0.0f;
    private float initialYawAngle = 0.0f;
    private float targetCounterSteerYaw = 0.0f;
    private float counterSteerAmount;
    private bool isDrifting = false;

    private PlayerCarController playerCar;
    void Start()
    {   
        playerCar = GameObject.Find("FixedAltima").GetComponent<PlayerCarController>();
    }
    void FixedUpdate()
    {
        if(playerCar.driftMode== true)
        {
            maxRotationAngle = 45.0f;
        }
        else
        {
            maxRotationAngle = 45.0f;
        }
        // Calculate current yaw angle (rotation around vertical axis)
        float currentRotationAngle = transform.eulerAngles.y;
        // Calculate current yaw angle between car forward direction and local z-axis
        currentYawAngle = CalculateYawAngle(transform.forward, targetObject.transform.forward);
        //Debug.Log("Angle :" +currentYawAngle);
        // Calculate yaw angle between car's forward direction and target object's forward direction
        float targetYawAngle = Mathf.Abs(currentYawAngle) - maxRotationAngle;
 
        if (targetYawAngle>0) //&& playerCar.driftMode== true)
        {
            Debug.Log("Angle :" +targetYawAngle);
            // Apply counter-steering to gradually bring the car's rotation angle back within the limit
            if(currentYawAngle>0)
            {    
                counterSteerAmount = Mathf.MoveTowardsAngle(currentRotationAngle, currentRotationAngle-targetYawAngle, counterSteeringSpeed * Time.deltaTime);
            }
            else
            {
                counterSteerAmount = Mathf.MoveTowardsAngle(currentRotationAngle, currentRotationAngle+targetYawAngle, counterSteeringSpeed * Time.deltaTime); 
            }
            // Apply the counter-steering angle to the car's rotation
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, counterSteerAmount, transform.eulerAngles.z);            
        }

    }

    // Calculate yaw angle between two forward directions
    private float CalculateYawAngle(Vector3 forward1, Vector3 forward2)
    {
        float angle = Vector3.SignedAngle(forward1, forward2, Vector3.down);
        return angle;
    }
}
