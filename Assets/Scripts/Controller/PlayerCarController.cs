using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerCarController : MonoBehaviour
{
    private Rigidbody playerCarRB;
    public WheelMeshes tireObjects;
    public WheelsTrail tireTrails;
    public WheelsCollider tireColliders;

    public float maxSpeed ,currentSpeed= 0.0f,slipAngle;
    private float currentSpeedClamped;
    public AnimationCurve steeringCurve , motorPowerCurve , nitroCurve;
    private float steeringInput;
    public float steeringAngle;

    //private float defaultRearSideFiction=1f , driftRearSideFiction =0.4f; //defaultRearSideFiction=3.0f , driftRearSideFiction = 1.2
    //Values for wheel collider forward and sideway frixtion in non-drift state
    private float nonDriftExtreSlipFF, nonDriftExtreValueFF, nonDriftAsymSlipFF, nonDriftAsymValueFF;
    private float nonDriftExtreSlipSF, nonDriftExtreValueSF,nonDriftAsymSlipSF, nonDriftAsymValueSF;

     //Values for wheel collider forward and sideway frixtion in drift state   
    private float DriftExtreSlipFF, DriftExtreValueFF, DriftAsymSlipFF, DriftAsymValueFF;
    private float DriftExtreSlipSF, DriftExtreValueSF, DriftAsymSlipSF, DriftAsymValueSF;

    public ParticleSystem particleSmokeRL,particleSmokeRR,particleSmokeFL,particleSmokeFR;
 
    //GUI button
    public ButtonSetUp rightBtn, leftBtn, driftBtn;
    public ButtonSetUpToggle nitroBtn;
    public TextMeshProUGUI speedText, gearNoText;
   // public bool bolCarOnGround = true; bolGearChanged = false;
    public bool bolGearChanged = false;
    public int numberOfGears = 4;
    private float maxSpeedGear1, maxSpeedGear2, maxSpeedGear3 ,maxSpeedGear4, currentMaxSpeedGear;
    //private float gravityModifier = 1.35f;//1.35f;
    private int intCurrentGear =1;

    public GameObject tireTrail;
    public Material brakeMat;
    public Color brakeColor;
    public float brakeColorIntensity=2f;

    //Nitro PowerUp
    private bool nitoInput ;
    public float nitroCapacity,nitroMaxCapacity = 10000f;
    public bool nitroStart =  false, driftMode = false;
    private bool bolNitroParticleStart = false; 
    public ParticleSystem nitroPart1,nitroPart2;
 

    //Collectables

    public HudElements hudElement;
    private DriftController driftController;

    // Start is called before the first frame update
    void Start()
    {
        //hudElement =  GetComponent<HudElements>();
        playerCarRB = GetComponent<Rigidbody>();
        playerCarRB.centerOfMass = new Vector3(0,-0.2f,0); // for stopping topple over 0.24

        //Physics.gravity = Physics.gravity * gravityModifier;    // modify overall gravity

        InvokeRepeating("fun_DisplayTextUI", 0.4f, 0.2f );
        InvokeRepeating("fun_gearSpeed", 0.4f, 0.4f );
        
        //Gear system
        maxSpeedGear1 = maxSpeed / numberOfGears;
        maxSpeedGear2 = 2 * maxSpeedGear1;
        maxSpeedGear3 = 3 * maxSpeedGear1;
        maxSpeedGear4 = maxSpeed;
        currentMaxSpeedGear  = maxSpeedGear1;

        //Instantiate tire trails on wheels
        fun_InitiateTireTrail();

        //Nitro Related variables
        nitroStart =  false;
        nitroCapacity = nitroMaxCapacity;

        hudElement.fun_SetMaxNitroCapacity(nitroMaxCapacity);
        hudElement.fun_SetCurentNitro(nitroCapacity);

        driftController = GameObject.Find("FixedAltima").GetComponent<DriftController>();

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //float radiusOfWheel = tireColliders.wheelRR.radius;
        //float wheelRpm = tireColliders.wheelRR.rpm;
        //currentSpeed =(2f * Mathf.PI * radiusOfWheel * wheelRpm * 60f)/1000f;
        currentSpeed = playerCarRB.linearVelocity.magnitude * 3.6f;
        currentSpeedClamped = Mathf.Lerp(currentSpeedClamped,currentSpeed,Time.deltaTime);
        fun_ApplyTireCollidersToMeshes();
        fun_ApplymotorPower();
        fun_ApplySteering();


    }
    void Update()
    {
        fun_ApplyNiro();
        fun_ApplyDrift();
        hudElement.fun_SetCurentNitro(nitroCapacity);
    }




     //#####################################################################
    //#####              User Defined Functions Section               #####
    //#####################################################################   

    //apply collider to the meshes for tires
    void fun_ApplyTireCollidersToMeshes()
    {
        tireColliders.wheelFL.GetWorldPose(out Vector3 pos1, out Quaternion quat1);
        tireColliders.wheelFR.GetWorldPose(out Vector3 pos2, out Quaternion quat2);
        tireColliders.wheelRL.GetWorldPose(out Vector3 pos3, out Quaternion quat3);
        tireColliders.wheelRR.GetWorldPose(out Vector3 pos4, out Quaternion quat4);

        tireObjects.wheelFL.transform.position = pos1;
        tireObjects.wheelFR.transform.position = pos2;
        tireObjects.wheelRL.transform.position = pos3;
        tireObjects.wheelRR.transform.position = pos4;

   
        tireObjects.wheelFL.transform.rotation = quat1;
        tireObjects.wheelFR.transform.rotation = quat2;
        tireObjects.wheelRL.transform.rotation = quat3;
        tireObjects.wheelRR.transform.rotation = quat4;

    }

    // Apply motor power to the vechicle
    void fun_ApplymotorPower()
    {   

        if(Input.GetKey(KeyCode.Q)|| driftBtn.isPressed == true)
        {
            tireColliders.wheelRL.motorTorque = -200 ;
            tireColliders.wheelRR.motorTorque = -200 ;            
        }
        else
        {
            if (currentSpeed < maxSpeed)
            {
                // Debug.Log(currentSpeed);
                if(nitroStart == true)
                {
                    tireColliders.wheelRL.motorTorque =  motorPowerCurve.Evaluate(currentSpeed) * Time.deltaTime *200 *nitroCurve.Evaluate(currentSpeed);
                    tireColliders.wheelRR.motorTorque =  motorPowerCurve.Evaluate(currentSpeed) * Time.deltaTime *200 *nitroCurve.Evaluate(currentSpeed);
                }
                else
                {
                    tireColliders.wheelRL.motorTorque =  motorPowerCurve.Evaluate(currentSpeed) * Time.deltaTime *200;
                    tireColliders.wheelRR.motorTorque =  motorPowerCurve.Evaluate(currentSpeed) * Time.deltaTime *200;
                    //tireColliders.wheelRL.motorTorque =  motorPower * Time.deltaTime ;
                // tireColliders.wheelRR.motorTorque =  motorPower * Time.deltaTime ;
                }

            }
            else
            {
                tireColliders.wheelRL.motorTorque = -2000 ;
                tireColliders.wheelRR.motorTorque = -2000 ;
            }        
        }

        

    }

    // Apply steering to the vechicle
    void fun_ApplySteering()
    {
        steeringInput = Input.GetAxis("Horizontal");
        if (rightBtn.isPressed == true)
        {
           steeringInput = rightBtn.holdPress; 
        }
        else if( leftBtn.isPressed == true)
        {
            steeringInput = -leftBtn.holdPress; 
        }

        steeringAngle = steeringInput*steeringCurve.Evaluate(currentSpeed);

        if (slipAngle < 120f )//&& bolCarOnGround == true)
        //if(driftMode)
        {
            steeringAngle += 0.887f*Vector3.SignedAngle(transform.forward, playerCarRB.linearVelocity + transform.forward, Vector3.up);
            steeringAngle = Mathf.Clamp(steeringAngle, -45f, 45f);
        }
        
        tireColliders.wheelFL.steerAngle = steeringAngle;
        tireColliders.wheelFR.steerAngle = steeringAngle;

        if (driftController.currentYawAngle> 42)
        {
           steeringAngle =  -(steeringInput*driftController.currentYawAngle-15f);
           Debug.Log("steeringAngle: "+steeringAngle);
        }
        tireColliders.wheelFL.steerAngle = steeringAngle;
        tireColliders.wheelFR.steerAngle = steeringAngle;

    }
   
    
    // Apply drift to the vechicle
    void fun_ApplyDrift()
    {   WheelHit[] wheelHits = new WheelHit[4];
        tireColliders.wheelFL.GetGroundHit(out wheelHits[0]);
        tireColliders.wheelFR.GetGroundHit(out wheelHits[1]);
        tireColliders.wheelRL.GetGroundHit(out wheelHits[2]);
        tireColliders.wheelRR.GetGroundHit(out wheelHits[3]);
        float slipAllowance = 0.45f;

        if(Input.GetKey(KeyCode.Q)|| driftBtn.isPressed == true)
        {
            driftMode =  true;
            fun_FetchFrictionValues();
            fun_ApplyFrictionValuesOnTires();
            nitroCapacity = nitroCapacity + 5 * Time.deltaTime;

            if(brakeMat)
            {
                brakeMat.EnableKeyword("_EMISSION");
                brakeMat.SetColor("_EmissionColor",brakeColor*Mathf.Pow(2,brakeColorIntensity));
            }
            if(Mathf.Abs(wheelHits[0].forwardSlip) + Mathf.Abs(wheelHits[0].sidewaysSlip) > slipAllowance)
            {
                particleSmokeFL.Play();
                tireTrails.wheelFL.emitting = true;
            }
            if(Mathf.Abs(wheelHits[1].forwardSlip) + Mathf.Abs(wheelHits[1].sidewaysSlip) > slipAllowance)
            {
                particleSmokeFR.Play();
                tireTrails.wheelFR.emitting = true;
            }
            if(Mathf.Abs(wheelHits[2].forwardSlip) + Mathf.Abs(wheelHits[2].sidewaysSlip) > slipAllowance)
            {
                particleSmokeRL.Play();            
                tireTrails.wheelRL.emitting = true;
            }
            if(Mathf.Abs(wheelHits[3].forwardSlip) + Mathf.Abs(wheelHits[3].sidewaysSlip) > slipAllowance)
            {
                particleSmokeRR.Play();
                tireTrails.wheelRR.emitting = true;
            }

            }
        else
        {   
            driftMode = false;
            fun_FetchFrictionValues();    
            fun_ApplyFrictionValuesOnTires();

            particleSmokeFL.Stop();
            particleSmokeFR.Stop();
            particleSmokeRL.Stop();
            particleSmokeRR.Stop();
            tireTrails.wheelFL.emitting = false;
            tireTrails.wheelFR.emitting = false;
            tireTrails.wheelRL.emitting = false;
            tireTrails.wheelRR.emitting = false;
            if(brakeMat)
            {
                brakeMat.DisableKeyword("_EMISSION");
                brakeMat.SetColor("_EmissionColor",Color.black);
            }                
        }   
    }

    // Fetch values of forward and sideway fiction according to speed
    private void fun_FetchFrictionValues()
    {
        if(currentSpeed<100) // speed is between 0 to 90
        {
            nonDriftExtreSlipFF = 0.1f;
            nonDriftExtreValueFF =  1f;
            nonDriftAsymSlipFF = 0.8f; 
            nonDriftAsymValueFF = 0.5f;

            nonDriftExtreSlipSF = 0.1f;
            nonDriftExtreValueSF =  1f;            
            nonDriftAsymSlipSF = 0.5f; 
            nonDriftAsymValueSF = 0.75f;


            /*DriftExtreSlipFF = 0.5f;
            DriftExtreValueFF = 1f;
            DriftAsymSlipFF = 0.6f;;
            DriftAsymValueFF = 0.45f;

            DriftExtreSlipSF = 0.6f;
            DriftExtreValueSF = 1f;
            DriftAsymSlipSF = 0.61f;
            DriftAsymValueSF = 0.45f;*/

            DriftExtreSlipFF = 0.5f;
            DriftExtreValueFF = 1f;
            DriftAsymSlipFF = 0.6f;
            DriftAsymValueFF = 0.6f;

            DriftExtreSlipSF = 0.65f;
            DriftExtreValueSF = 1f;
            DriftAsymSlipSF = 0.8f;
            DriftAsymValueSF = 0.45f;

        }
        else if(currentSpeed>100) // speed is between 90 to 200
        {
            nonDriftExtreSlipFF = 0.07f;
            nonDriftExtreValueFF =  1.5f;            
            nonDriftAsymSlipFF = 0.6f; 
            nonDriftAsymValueFF = 1.2f;

            nonDriftExtreSlipSF = 0.06f;
            nonDriftExtreValueSF =  1.5f;            
            nonDriftAsymSlipSF = 0.5f;  
            nonDriftAsymValueSF = 1.2f;


            DriftExtreSlipFF = 0.5f;
            DriftExtreValueFF = 0.8f;
            DriftAsymSlipFF = 0.6f;
            DriftAsymValueFF = 0.6f;

            DriftExtreSlipSF = 0.65f;
            DriftExtreValueSF = 0.7f;
            DriftAsymSlipSF = 0.8f;
            DriftAsymValueSF = 0.45f;
        }
    }

    private void fun_ApplyFrictionValuesOnTires()
    {
        WheelFrictionCurve frictionCurveForward = tireColliders.wheelRL.forwardFriction;
        WheelFrictionCurve frictionCurveSideway = tireColliders.wheelRL.sidewaysFriction;
        if (driftMode == true)
        {
            //Forward friction values
            frictionCurveForward.extremumSlip = DriftExtreSlipFF;
            frictionCurveForward.extremumValue = DriftExtreValueFF;
            frictionCurveForward.asymptoteSlip = DriftAsymSlipFF;
            frictionCurveForward.asymptoteValue = DriftAsymValueFF;
            //Sideway friction values
            frictionCurveSideway.extremumSlip = DriftExtreSlipSF;
            frictionCurveSideway.extremumValue = DriftExtreValueSF;
            frictionCurveSideway.asymptoteSlip = DriftAsymSlipSF;
            frictionCurveSideway.asymptoteValue = DriftAsymValueSF;
        }
        else
        {
            //Forward friction values
            frictionCurveForward.extremumSlip = nonDriftExtreSlipFF;
            frictionCurveForward.extremumValue = nonDriftExtreValueFF;
            frictionCurveForward.asymptoteSlip = nonDriftAsymSlipFF;
            frictionCurveForward.asymptoteValue = nonDriftAsymValueFF;
            //Sideway friction values
            frictionCurveSideway.extremumSlip = nonDriftExtreSlipSF;
            frictionCurveSideway.extremumValue = nonDriftExtreValueSF;
            frictionCurveSideway.asymptoteSlip = nonDriftAsymSlipSF;
            frictionCurveSideway.asymptoteValue = nonDriftAsymValueSF;            
        }    
        //frictionCurve.stiffness = driftRearSideFiction;
        tireColliders.wheelFR.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelFL.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelFR.forwardFriction = frictionCurveForward;
        tireColliders.wheelFL.forwardFriction = frictionCurveForward;

        tireColliders.wheelRL.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelRR.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelRL.forwardFriction = frictionCurveForward;
        tireColliders.wheelRR.forwardFriction = frictionCurveForward;

    }
    // to fetch speed ratio for shift gear effect
    public float fun_getSpeedRatio()
    {
        //return currentSpeedClamped/maxSpeed;
        if (bolGearChanged == true)
        {
            bolGearChanged = false;
            currentSpeed = 10f;
           // StartCoroutine(nitroDepletion()); // does  not effect anything
            
        }
        return (currentSpeed)/(currentMaxSpeedGear*1.34F); //multipled with 1.3 to produce shiftgear effect
    }

    //Display Speed and gear in HUD
    public void fun_DisplayTextUI()
    {
        speedText.text = ((int)currentSpeed).ToString();
        gearNoText.text = ((int)intCurrentGear).ToString();
    }
    

    //Calculate and return speed according to gear
    private float fun_gearSpeed()
    {
        if(maxSpeedGear1 > currentSpeed && currentMaxSpeedGear != maxSpeedGear1)
        {   
            currentMaxSpeedGear = maxSpeedGear1;
            intCurrentGear = 1;
            bolGearChanged = true;
            
        }
        if(maxSpeedGear2 > currentSpeed && currentSpeed> maxSpeedGear1 && currentMaxSpeedGear != maxSpeedGear2)
        {
            currentMaxSpeedGear = maxSpeedGear2;
            intCurrentGear = 2;
            bolGearChanged = true;
        }
        if(maxSpeedGear3 > currentSpeed && currentSpeed> maxSpeedGear2 && currentMaxSpeedGear != maxSpeedGear3)
        {
            currentMaxSpeedGear = maxSpeedGear3;
            intCurrentGear = 3;
            bolGearChanged = true;
        }
        if(maxSpeedGear4 > currentSpeed && currentSpeed> maxSpeedGear3 && currentMaxSpeedGear != maxSpeedGear4)
        { 
            currentMaxSpeedGear = maxSpeedGear4;
            intCurrentGear = 4;
            bolGearChanged = true  ;
        }

        
        return currentMaxSpeedGear;
    }

    // Function to instantiate tire trails on each tire
    void fun_InitiateTireTrail()
    {
        tireTrails.wheelFL = Instantiate(tireTrail, tireColliders.wheelFL.transform.position -Vector3.up *tireColliders.wheelFL.radius, Quaternion.identity,tireColliders.wheelFL.transform).GetComponent<TrailRenderer>();
        tireTrails.wheelFR = Instantiate(tireTrail, tireColliders.wheelFR.transform.position -Vector3.up *tireColliders.wheelFR.radius, Quaternion.identity,tireColliders.wheelFR.transform).GetComponent<TrailRenderer>();
        tireTrails.wheelRL = Instantiate(tireTrail, tireColliders.wheelRL.transform.position -Vector3.up *tireColliders.wheelRL.radius, Quaternion.identity,tireColliders.wheelRL.transform).GetComponent<TrailRenderer>();
        tireTrails.wheelRR = Instantiate(tireTrail, tireColliders.wheelRR.transform.position -Vector3.up *tireColliders.wheelRR.radius, Quaternion.identity,tireColliders.wheelRR.transform).GetComponent<TrailRenderer>();
    }

    // function to apply nitro according to player input
    public void fun_ApplyNiro()
    { 

        //########################## Code for Nitro touch button #######################################
        //############################## Uncomment before using on android   ##########################
        
       /* if((nitroBtn.isPressedToggle == true) && (bolNitroParticleStart == false) && (nitroCapacity>0f))
        {
            nitroStart = true;
            nitroPart1.Play();
            nitroPart2.Play();
            bolNitroParticleStart = true;
            Debug.Log("inside play : " + nitroBtn.isPressedToggle);
        }


        if((nitroBtn.isPressedToggle == false && bolNitroParticleStart == true)||(nitroCapacity < 1f ))
        {
            nitroStart = false;
            nitroPart1.Stop();
            nitroPart2.Stop();
            bolNitroParticleStart = false;
            Debug.Log("inside stop : " + nitroBtn.isPressedToggle);           
        }

        if(driftMode == true)
        {
            nitroBtn.isPressedToggle = false;          
        }

        if(nitroStart== true)
        {
            nitroCapacity = nitroCapacity - 15 * Time.deltaTime;
        }
        if(nitroCapacity > nitroMaxCapacity )
        {
            nitroCapacity = nitroMaxCapacity;
            Debug.Log("Nitro tank more than 100:");
        }*/

      //######################### Uncomment before using on android   ##########################################################


        //########################## Code for Nitro keyboard button ########################################################
        //########################################### Uncomment before using on PC   ########################################

        if( nitoInput = Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Nitro touch button pressed:");
            if(nitroStart == false && nitroCapacity>0f)
                nitroStart = true;
            else
                nitroStart = false;

        }

        if(driftMode == true)
        {
           nitroStart = false; 
        }

        if(nitroStart== true)
        {
            nitroCapacity = nitroCapacity - 15 * Time.deltaTime;
            if(nitroCapacity < 1 )
            {
                 nitroCapacity = 0f;
                 nitroStart = false;
                 bolNitroParticleStart = false;   
                 nitroPart1.Stop();
                 nitroPart2.Stop();
                 Debug.Log("Nitro tank less than 1");
            }

            if(nitroCapacity > nitroMaxCapacity )
            {
                nitroCapacity = nitroMaxCapacity;
                Debug.Log("Nitro tank more than 100");
            }
        }

        if(nitroStart== true && bolNitroParticleStart == false)
        {
            nitroPart1.Play();
            nitroPart2.Play();
            bolNitroParticleStart = true; 
            Debug.Log("inside NitroPlay");
        }
        if(nitroStart== false && bolNitroParticleStart == true)
        {
            nitroPart1.Stop();
            nitroPart2.Stop();
            bolNitroParticleStart = false;
            Debug.Log("inside NitroStop"); 
        }

    //########################################### Uncomment before using on PC   ##########################################
        //Debug.Log("inside fun_ApplyNiro : "+nitroStart+" Capacity : "+nitroCapacity);

    }

    //*************************************
    //**        Collision Events    *******
    //*************************************
     public void OnTriggerEnter(Collider collider)
     {
        if(collider.CompareTag("NitroCollectable"))
        {
            Destroy(collider.gameObject);
            nitroCapacity = nitroCapacity + 18;
        }    
     
     }

    //#####################################################################
    //#####               User Defined Class Section                  #####
    //#####################################################################

    //class for definining wheel meshes
    [System.Serializable]
    public class WheelMeshes
    {
        public MeshRenderer wheelFL, wheelFR, wheelRL, wheelRR ;
    }
     
     //Class for defining wheel collider
    [System.Serializable]
    public class WheelsCollider
    {
        public WheelCollider wheelFL, wheelFR, wheelRL, wheelRR ;
    }
    [System.Serializable]
    public class WheelsTrail
    {
        public TrailRenderer wheelFL, wheelFR, wheelRL, wheelRR ;
    }
}
