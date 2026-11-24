using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // [INPUT SYSTEM MIGRATION]
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

    // [INPUT SYSTEM MIGRATION] 3-second delay before motor power applies
    private float motorPowerStartTime;
    public float motorPowerDelay = 3f;

    // [INPUT SYSTEM MIGRATION]
    // Keyboard actions built in code so no Inspector or .inputactions wiring is required.
    private InputAction steerAction;
    private InputAction driftAction;
    private InputAction nitroAction;

    // [DRIFT STEP 3] Read-only input values for ArcadeDriftController. They only read input;
    // nothing in this controller uses them, so driving is unchanged.
    public float RawSteerInput
    {
        get
        {
            if (rightBtn != null && rightBtn.isPressed) return rightBtn.holdPress;
            if (leftBtn != null && leftBtn.isPressed) return -leftBtn.holdPress;
            return steerAction != null ? steerAction.ReadValue<float>() : 0f;
        }
    }
    public bool DriftInputHeld => (driftAction != null && driftAction.IsPressed()) || (driftBtn != null && driftBtn.isPressed);

    // [DRIFT STEP 13] When true, ArcadeDriftController drives drift smoke, tyre trails, the brake light, nitro refill
    // and the nitro cut from the real drift state. fun_ApplyDrift then only sets driftMode (raw input), which the
    // old friction toggle and the telemetry still use. Camera code is not touched.
    [Tooltip("[DRIFT STEP 14] How fast steering returns to centre when no steering key is held (input units per second). 6 = full lock to straight in ~0.17 s. 0 = old instant snap.")]
    public float steerReturnRate = 6f;
    [HideInInspector] public bool useExternalDriftEffects = false;
    [HideInInspector] public bool externalDriftActive = false; // nitro is cut while this is true
    private bool fxBrakeOn = false;

    public void SetDriftEffects(bool fl, bool fr, bool rl, bool rr, bool brakeLight)
    {
        SetWheelFx(particleSmokeFL, tireTrails != null ? tireTrails.wheelFL : null, fl);
        SetWheelFx(particleSmokeFR, tireTrails != null ? tireTrails.wheelFR : null, fr);
        SetWheelFx(particleSmokeRL, tireTrails != null ? tireTrails.wheelRL : null, rl);
        SetWheelFx(particleSmokeRR, tireTrails != null ? tireTrails.wheelRR : null, rr);
        if (brakeLight != fxBrakeOn && tailLights) tailLights.SetDrift(brakeLight); // tail lights (TailLights component) glow with the drift smoke
        fxBrakeOn = brakeLight;
    }

    static void SetWheelFx(ParticleSystem ps, TrailRenderer tr, bool on)
    {
        if (ps) { if (on) { if (!ps.isPlaying) ps.Play(); } else if (ps.isPlaying) ps.Stop(); }
        if (tr) tr.emitting = on;
    }

    // [DRIFT STEP 13] Adds nitro and never goes above nitroMaxCapacity (the old refill could).
    public void AddNitro(float amount) { nitroCapacity = Mathf.Min(nitroCapacity + amount, nitroMaxCapacity); }

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
    // Tail lights are a separate child object (TailLights component); the car only tells it when to light up
    private TailLights tailLights;

    //Nitro PowerUp
    private bool nitoInput ;
    public float nitroCapacity,nitroMaxCapacity = 10000f;
    public bool nitroStart =  false, driftMode = false;
    private bool bolNitroParticleStart = false; 
    public ParticleSystem nitroPart1,nitroPart2;
 

    //Collectables

    public HudElements hudElement;
    //private DriftController driftController;

    // [INPUT SYSTEM MIGRATION]
    // Sets up the New Input System keyboard bindings: A/D or Left/Right steer, Q drift, E nitro.
    void Awake()
    {
        steerAction = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
        steerAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");
        steerAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftArrow")
            .With("Positive", "<Keyboard>/rightArrow");

        driftAction = new InputAction("Drift", InputActionType.Button, "<Keyboard>/q");
        nitroAction = new InputAction("Nitro", InputActionType.Button, "<Keyboard>/e");
    }

    // [INPUT SYSTEM MIGRATION]
    void OnEnable()
    {
        steerAction?.Enable();
        driftAction?.Enable();
        nitroAction?.Enable();
    }

    // [INPUT SYSTEM MIGRATION]
    void OnDisable()
    {
        steerAction?.Disable();
        driftAction?.Disable();
        nitroAction?.Disable();
    }

    // [INPUT SYSTEM MIGRATION]
    void OnDestroy()
    {
        steerAction?.Dispose();
        driftAction?.Dispose();
        nitroAction?.Dispose();
    }

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

        tailLights = GetComponentInChildren<TailLights>(true);

        // [INPUT SYSTEM MIGRATION] Fixed: DriftController is on the same GameObject (FixedAltima).
        //driftController = GetComponent<DriftController>();

        // [INPUT SYSTEM MIGRATION] Record start time for 3-second motor power delay
        motorPowerStartTime = Time.timeSinceLevelLoad;

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //float radiusOfWheel = tireColliders.wheelRR.radius;
        //float wheelRpm = tireColliders.wheelRR.rpm;
        //currentSpeed =(2f * Mathf.PI * radiusOfWheel * wheelRpm * 60f)/1000f;
        currentSpeed = playerCarRB.linearVelocity.magnitude * 3.6f;
        currentSpeedClamped = Mathf.Lerp(currentSpeedClamped,currentSpeed,Time.deltaTime);
        // [DRIFT STEP 2] Friction is applied here, once per physics step (was in Update via fun_ApplyDrift).
        fun_FetchFrictionValues();
        fun_ApplyFrictionValuesOnTires();
        fun_ApplyTireCollidersToMeshes();
        fun_ApplymotorPower();
        fun_ApplyDriftDriveScale(); // [DRIFT STEP 5]
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
        // [INPUT SYSTEM MIGRATION] 3-second delay before applying motor power
        if (Time.timeSinceLevelLoad - motorPowerStartTime < motorPowerDelay)
        {
            return;
        }

        // [INPUT SYSTEM MIGRATION]
        if((driftAction.IsPressed() || driftBtn.isPressed == true) && !useDriftDriveScale) // [DRIFT STEP 5]
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
        // [INPUT SYSTEM MIGRATION]
        // MoveTowards reproduces the legacy Horizontal axis smoothing (sensitivity/gravity = 3).
        slipAngle = Vector3.Angle(transform.forward, playerCarRB.linearVelocity-transform.forward);
        float prevSteerInput = steeringInput; // [DRIFT STEP 14]
        steeringInput = Mathf.MoveTowards(steeringInput, steerAction.ReadValue<float>(), 3f * Time.deltaTime);
        if (rightBtn.isPressed == true)
        {
           steeringInput = rightBtn.holdPress; 
        }
        else if( leftBtn.isPressed == true)
        {
            steeringInput = -leftBtn.holdPress; 
        }

        steeringAngle = steeringInput*steeringCurve.Evaluate(currentSpeed);

        // [INPUT SYSTEM MIGRATION] Zero steering input immediately when no input detected
        if (Mathf.Abs(steerAction.ReadValue<float>()) < 0.01f && !rightBtn.isPressed && !leftBtn.isPressed)
        {
            // [DRIFT STEP 14] Return to centre at steerReturnRate instead of snapping to 0 in one physics step (was a 41 deg wheel jump).
            steeringInput = steerReturnRate > 0f ? Mathf.MoveTowards(prevSteerInput, 0f, steerReturnRate * Time.deltaTime) : 0f;
        }


        if (slipAngle < 120f )//&& bolCarOnGround == true)
        //if(driftMode)
        {
            if (Mathf.Abs(steeringInput) > 0.1f)
            {
                steeringAngle += 0.887f * Vector3.SignedAngle(transform.forward, playerCarRB.linearVelocity + transform.forward, Vector3.up);
            }            
            steeringAngle = Mathf.Clamp(steeringAngle, -45f, 45f);
        }
        
        tireColliders.wheelFL.steerAngle = steeringAngle;
        tireColliders.wheelFR.steerAngle = steeringAngle;

        /*if (driftController.currentYawAngle> 42)
        {
           steeringAngle =  -(steeringInput*driftController.currentYawAngle-15f);
           Debug.Log("steeringAngle: "+steeringAngle);
        }*/
        tireColliders.wheelFL.steerAngle = steeringAngle;
        tireColliders.wheelFR.steerAngle = steeringAngle;

    }
   
    
    // Apply drift to the vechicle
    void fun_ApplyDrift()
    {
        // [DRIFT STEP 13] Effects come from ArcadeDriftController; driftMode stays raw input.
        if (useExternalDriftEffects) { driftMode = driftAction.IsPressed() || driftBtn.isPressed == true; return; }
        WheelHit[] wheelHits = new WheelHit[4];
        tireColliders.wheelFL.GetGroundHit(out wheelHits[0]);
        tireColliders.wheelFR.GetGroundHit(out wheelHits[1]);
        tireColliders.wheelRL.GetGroundHit(out wheelHits[2]);
        tireColliders.wheelRR.GetGroundHit(out wheelHits[3]);
        float slipAllowance = 0.45f;

        // [INPUT SYSTEM MIGRATION]
        if(driftAction.IsPressed() || driftBtn.isPressed == true)
        {
            driftMode =  true;
            // [DRIFT STEP 2] friction is now applied in FixedUpdate
            nitroCapacity = nitroCapacity + 5 * Time.deltaTime;

            if(tailLights)
            {
                tailLights.SetDrift(true);
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
            // [DRIFT STEP 2] friction is now applied in FixedUpdate

            particleSmokeFL.Stop();
            particleSmokeFR.Stop();
            particleSmokeRL.Stop();
            particleSmokeRR.Stop();
            tireTrails.wheelFL.emitting = false;
            tireTrails.wheelFR.emitting = false;
            tireTrails.wheelRL.emitting = false;
            tireTrails.wheelRR.emitting = false;
            if(tailLights)
            {
                tailLights.SetDrift(false);
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

    // [DRIFT STEP 2] Per-axle grip multipliers. Each one scales WheelFrictionCurve.stiffness
    // for one axle (front or rear) and one direction (sideways or forward).
    // All default to 1, which reproduces the previous behaviour exactly.
    // A future drift component will set them through SetAxleGripMultipliers().
    private float frontSideGripMul = 1f, rearSideGripMul = 1f, frontFwdGripMul = 1f, rearFwdGripMul = 1f;
    private float baseStiffnessFF = -1f, baseStiffnessSF = -1f; // Inspector stiffness, captured on first use
    // [DRIFT STEP 4] Set by ArcadeDriftController. When true, the old all-wheel drift friction set is
    // not used: the car keeps its normal friction and the drift system lowers rear grip itself.
    [HideInInspector] public bool useNormalFrictionInDrift = false;
    // [DRIFT STEP 5] Set by ArcadeDriftController. When true, holding drift no longer replaces drive
    // with -200 Nm; the normal engine torque is used and scaled by driveTorqueScale instead.
    [HideInInspector] public bool useDriftDriveScale = false;
    [HideInInspector] public float driveTorqueScale = 1f;

    public void SetAxleGripMultipliers(float frontSide, float rearSide, float frontForward, float rearForward)
    {
        frontSideGripMul = Mathf.Max(0f, frontSide);
        rearSideGripMul = Mathf.Max(0f, rearSide);
        frontFwdGripMul = Mathf.Max(0f, frontForward);
        rearFwdGripMul = Mathf.Max(0f, rearForward);
    }

    public void ResetAxleGripMultipliers()
    {
        SetAxleGripMultipliers(1f, 1f, 1f, 1f);
    }

    // [DRIFT STEP 5] Scales positive (driving) rear torque only. The -2000 Nm speed limiter and the
    // start delay are left exactly as they are. With driveTorqueScale = 1 nothing changes.
    private void fun_ApplyDriftDriveScale()
    {
        if (!useDriftDriveScale || driveTorqueScale == 1f) return;
        if (tireColliders.wheelRL.motorTorque > 0f) tireColliders.wheelRL.motorTorque *= driveTorqueScale;
        if (tireColliders.wheelRR.motorTorque > 0f) tireColliders.wheelRR.motorTorque *= driveTorqueScale;
    }

    private void fun_ApplyFrictionValuesOnTires()
    {
        // Capture the Inspector stiffness once. The old code re-read it from wheelRL every frame;
        // reading it once stops a rear multiplier from feeding back into the front wheels.
        if (baseStiffnessFF < 0f)
        {
            baseStiffnessFF = tireColliders.wheelRL.forwardFriction.stiffness;
            baseStiffnessSF = tireColliders.wheelRL.sidewaysFriction.stiffness;
        }

        WheelFrictionCurve frictionCurveForward = tireColliders.wheelRL.forwardFriction;
        WheelFrictionCurve frictionCurveSideway = tireColliders.wheelRL.sidewaysFriction;
        if (driftMode == true && !useNormalFrictionInDrift) // [DRIFT STEP 4]
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

        // Front axle
        frictionCurveForward.stiffness = baseStiffnessFF * frontFwdGripMul;
        frictionCurveSideway.stiffness = baseStiffnessSF * frontSideGripMul;
        tireColliders.wheelFR.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelFL.sidewaysFriction = frictionCurveSideway;
        tireColliders.wheelFR.forwardFriction = frictionCurveForward;
        tireColliders.wheelFL.forwardFriction = frictionCurveForward;

        // Rear axle
        frictionCurveForward.stiffness = baseStiffnessFF * rearFwdGripMul;
        frictionCurveSideway.stiffness = baseStiffnessSF * rearSideGripMul;
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

        // [INPUT SYSTEM MIGRATION]
        if( nitoInput = nitroAction.WasPressedThisFrame())
        {
            Debug.Log("Nitro touch button pressed:");
            if(nitroStart == false && nitroCapacity>0f)
                nitroStart = true;
            else
                nitroStart = false;

        }

        if(useExternalDriftEffects ? externalDriftActive : driftMode) // [DRIFT STEP 13] nitro cut follows the real drift
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
