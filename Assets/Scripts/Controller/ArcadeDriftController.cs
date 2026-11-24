using UnityEngine;

/// <summary>
/// Arcade drift system for FixedAltima - roadmap Step 3: state machine only.
///
/// This step DETECTS and DISPLAYS drift states. It applies no forces, no torque and no
/// grip changes, so the car drives exactly as before. The "planned" values (rear grip,
/// drive scale) are only calculated and shown, so later steps can be checked against them.
///
/// States: Normal -> Initiation -> Active -> Recovery -> Normal, plus SpinRecovery (safety).
/// One value, driftBlend (0..1), will drive every physics change in later steps.
///
/// Angle convention (from DriftTelemetry): beta = SignedAngle(forward, planar velocity, up).
/// In a correct drift the nose points further into the corner than the car travels, so
/// beta has the opposite sign to the steering direction. alpha = -beta * direction is the
/// drift angle measured "into" the drift: positive = correct drift, negative = wrong way.
/// </summary>
[DefaultExecutionOrder(50)] // after PlayerCarController (0), before DriftTelemetry (100)
[DisallowMultipleComponent]
public class ArcadeDriftController : MonoBehaviour
{
    public enum DriftState { Normal, Initiation, Active, Recovery, SpinRecovery }

    [Header("Entry")]
    [Tooltip("km/h. Above gear 1 (32.5 km/h).")] public float minDriftSpeed = 40f;
    [Tooltip("Steering input (0-1) needed to start a drift.")] public float initiationSteerThreshold = 0.3f;
    [Tooltip("Seconds for driftBlend to go 0 -> 1.")] public float entryDuration = 0.25f;

    [Header("Drift angle targets (deg)")]
    public float neutralDriftAngle = 18f;
    public float maxDriftAngle = 35f;
    [Tooltip("Target when full counter-steer is held.")] public float counterSteerAngle = 5f;
    [Tooltip("How fast the target angle may change, deg/s.")] public float targetAngleRate = 50f;

    [Header("Exit")]
    [Tooltip("km/h. Lower than Min Drift Speed so the state does not flicker.")] public float exitSpeed = 30f;
    [Tooltip("Drift angle counted as 'straight'. Normal cornering stays under ~4.2 deg.")] public float exitAngle = 5f;
    public float exitAngleHoldTime = 0.2f;
    public float fullCounterSteerHoldTime = 0.3f;
    [Tooltip("[Step 11] Drift held + steering firmly the other way starts the opposite drift (chain flick).")] public bool allowChainFlick = true;
    [Tooltip("[Step 11] Opposite steering needed for a chain flick (0-1).")] [Range(0.3f, 1f)] public float chainSteerThreshold = 0.6f;
    [Tooltip("[Step 11] Seconds the opposite steering must be held before the flick.")] public float chainDelay = 0.15f;
    [Tooltip("[Step 11] How fast the drift system follows steering input (units/s). The controller's own steering keeps its 3/s.")] public float driftSteerResponse = 6f;
    public float airborneExitTime = 0.2f;
    [Tooltip("Seconds for driftBlend to go 1 -> 0 when the car is straight.")] public float recoveryDuration = 0.5f;

    [Header("Spin detection")]
    public float spinAngle = 60f;
    [Tooltip("Planned yaw-rate cap during drift, deg/s. Spin = 1.3 x this.")] public float yawRateLimit = 90f;
    public float spinRecoveryExitAngle = 30f;
    public float spinRecoveryMaxTime = 2f;
    [Tooltip("[Step 10] Turning-acceleration cap while catching a spin (deg/s^2). Normal drift cap is maxYawAccel.")] public float spinMaxYawAccel = 900f;
    [Tooltip("[Step 10] driftBlend held during SpinRecovery. Lower = more rear grip to stop the rotation.")] [Range(0f, 1f)] public float spinGripBlend = 0.25f;
    [Tooltip("[Step 10] Seconds after leaving SpinRecovery before the angle can trigger it again.")] public float spinReentryCooldown = 0.5f;

    [Header("Speed curves (applied from Step 11; x = km/h, y = multiplier)")]
    [Tooltip("[DRIFT STEP 11] Multiplier on maxRelativeYawRate and yawRateLimit. 1 up to 130 km/h (tested range); the taper above is a placeholder for the 220 km/h step.")]
    public AnimationCurve yawRateBySpeed = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(130f, 1f), new Keyframe(220f, 0.75f));
    [Tooltip("[DRIFT STEP 11] Multiplier on maxYawAccel and spinMaxYawAccel. Same placeholder taper above 130 km/h.")]
    public AnimationCurve yawAccelBySpeed = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(130f, 1f), new Keyframe(220f, 0.75f));
    [Tooltip("[DRIFT STEP 11] Multiplier on how much rear grip is removed (1 = rearGripMultiplier as set).")]
    public AnimationCurve rearGripLossBySpeed = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(220f, 1f));

    [Header("Rear grip (applied from Step 4)")]
    [Tooltip("Turn off to get the old Q drift back exactly, for comparison.")] public bool applyRearGrip = true;
    [Tooltip("Rear sideways grip at full drift (x normal). Floor 0.3; below ~0.4 spins get likely.")]
    [Range(0.3f, 1f)] public float rearGripMultiplier = 0.5f;
    [Header("Drive during drift (applied from Step 5)")]
    [Tooltip("Turn off to get the old -200 Nm drift drag back.")] public bool applyDriveScale = true;
    [Tooltip("Rear engine torque at full drift (x normal).")]
    [Range(0f, 1f)] public float driftDriveScale = 0.7f;

    [Header("Yaw assistance (applied from Step 6)")]
    [Tooltip("Turn off to remove all turning assistance.")] public bool applyYawAssist = true;
    [Tooltip("Target yaw rate per degree of angle error (1/s).")] public float angleGain = 4f;
    [Tooltip("Extra angle gain while entering the drift.")] public float initiationGainBoost = 1.5f;
    [Tooltip("Largest yaw rate the angle error may ask for, on top of the travel-direction turn rate (deg/s).")] public float maxRelativeYawRate = 90f;
    [Tooltip("Turning acceleration per deg/s of yaw-rate error (1/s).")] public float rateGain = 8f;
    [Tooltip("Cap on the assist's turning acceleration (deg/s^2). 460 = 8 rad/s^2.")] public float maxYawAccel = 460f;
    [Tooltip("Angle gain toward straight during Recovery and SpinRecovery.")] public float recoveryAngleGain = 2f;
    [Tooltip("Yaw-rate gain during SpinRecovery (stronger damping).")] public float spinRateGain = 12f;

    [Header("Front-wheel counter-steer (applied from Step 7)")]
    [Tooltip("Turn off to leave front steering to PlayerCarController.")] public bool applyFrontCounterSteer = true;
    [Tooltip("Front wheel angle per degree of drift angle. ~0.9 = wheels point along the direction of travel.")] public float counterSteerFactor = 0.9f;
    [Tooltip("How far the player's steering adjusts the wheels during a drift (deg at full input).")] public float playerSteerTrim = 12f;
    [Tooltip("Same limit as the existing steering clamp.")] public float maxWheelAngle = 45f;

    [Header("Drift speed control and sideways damping (applied from Step 8)")]
    [Tooltip("Turn off to remove drift drag and sideways damping.")] public bool applySpeedControl = true;
    [Tooltip("Speed lost over speedLossWindow seconds of drifting (%). Same at every speed.")] public float targetSpeedLossPercent = 10f;
    [Tooltip("Seconds over which targetSpeedLossPercent is lost.")] public float speedLossWindow = 2f;
    [Tooltip("How firmly speed follows the target (1/s).")] public float speedTrackGain = 6f;
    [Tooltip("Largest slowing or pushing correction (m/s^2).")] public float maxSpeedCorrection = 7f; // [DRIFT STEP 12] was 4
    [Tooltip("How strongly sideways sliding is damped (1/s).")] public float lateralDamping = 0.5f;

    [Header("Recovery (applied from Step 9)")]
    [Tooltip("Turn the direction of travel toward the car's nose during Recovery.")] public bool applyVelocityAlignment = true;
    [Tooltip("How fast the direction of travel turns toward the nose (deg/s at full blend).")] public float velocityAlignRate = 75f;

    // [DRIFT STEP 12c] Normal-driving stability at high speed. Normal state only, faded out by driftBlend,
    // so drifting is untouched. Below stabilityStartSpeed nothing changes.
    [Header("High-speed stability, Normal driving only (Step 12c)")]
    [Tooltip("Turn off to get the Step 12b high-speed handling back.")] public bool applyStability = true;
    [Tooltip("km/h where the assist starts to fade in.")] public float stabilityStartSpeed = 100f;
    [Tooltip("km/h where the assist is fully on.")] public float stabilityFullSpeed = 140f;
    [Tooltip("Sideways acceleration budget (g). Turn rate is capped to budget / speed (1.5 g = ~16 deg/s at 190 km/h).")] public float maxLateralG = 1.5f;
    [Tooltip("Slip angle (deg) allowed before the assist turns the nose back toward the travel direction.")] public float stabilitySlipDeadband = 4f;
    [Tooltip("Yaw rate per degree of extra slip (1/s).")] public float stabilitySlipGain = 4f;
    [Tooltip("Turning acceleration per deg/s of yaw-rate error (1/s).")] public float stabilityRateGain = 8f;
    [Tooltip("Cap on the stability assist's turning acceleration (deg/s^2).")] public float stabilityMaxYawAccel = 300f;
    [Tooltip("Rear sideways grip in Normal driving above 100 km/h (x normal). >1 = the front lets go first (safe push wide).")]
    [Range(1f, 1.5f)] public float highSpeedRearGrip = 1.15f;
    [Tooltip("km/h over which highSpeedRearGrip fades in, starting at stabilityStartSpeed.")] public float highSpeedRearGripFade = 20f;

    // [DRIFT STEP 13] Smoke, tyre trails, brake light and nitro refill follow the real drift state (camera not touched).
    [Header("Drift effects (Step 13)")]
    [Tooltip("Turn off to get the old effects back (smoke, trails, brake light and nitro refill while Q is held).")] public bool applyDriftEffects = true;
    [Tooltip("Slip angle (deg) needed before smoke and trails show.")] public float effectsMinAngle = 8f;
    [Tooltip("driftBlend needed before smoke and trails show.")] [Range(0f, 1f)] public float effectsMinBlend = 0.3f;
    [Tooltip("Front wheels smoke only when their own slip (forward + sideways) is above this. Same 0.45 as the old code.")] public float frontEffectsSlip = 0.45f;
    [Tooltip("Nitro added per second while drifting (old code: 5/s whenever Q was held).")] public float driftNitroRefillRate = 5f;
    [Tooltip("Drift angle (deg) needed before nitro refills.")] public float nitroRefillMinAngle = 10f;

    [Header("Debug")]
    public bool showOverlay = true;

    // ---------- Read-only state ----------
    public DriftState State { get; private set; } = DriftState.Normal;
    public float DriftBlend { get; private set; }          // smoothed 0..1
    public int Direction { get; private set; }             // +1 right, -1 left, 0 none
    public float Beta { get; private set; }                // signed drift angle, deg
    public float Alpha { get; private set; }               // drift angle into the drift, deg
    public float TargetAngle { get; private set; }         // deg
    public float YawRate { get; private set; }             // deg/s
    public float SpeedKmh { get; private set; }
    public float SteerInput { get; private set; }          // smoothed copy, -1..1
    public float TimeInState { get; private set; }
    public string LastTransition { get; private set; } = "";
    public float PlannedRearGrip => 1f - (1f - rearGripMultiplier) * rearGripLossBySpeed.Evaluate(SpeedKmh) * DriftBlend; // [DRIFT STEP 11]
    public float PlannedDriveScale => Mathf.Lerp(1f, driftDriveScale, DriftBlend);
    public float YawAssist { get; private set; }          // deg/s^2 applied (+ = turning right)
    public float VelocityTurnRate { get; private set; }   // deg/s the travel direction turns
    public float DriftWheelAngle { get; private set; }    // deg, wheel angle the drift wants
    public float FrontWheelAngle { get; private set; }    // deg, wheel angle actually applied
    public float DriftDrag { get; private set; }          // m/s^2 correction applied (+ = slowing)
    public float SpeedTargetKmh { get; private set; }     // km/h the drift is following
    public float AlignRate { get; private set; }          // deg/s the travel direction is being turned
    public bool EffectsActive { get; private set; }       // [DRIFT STEP 13] drift smoke/trails/brake light on
    public bool NitroRefilling { get; private set; }      // [DRIFT STEP 13]
    public bool[] WheelEffects { get; } = new bool[4];    // [DRIFT STEP 13] FL, FR, RL, RR
    public float StabilityAssist { get; private set; }    // [DRIFT STEP 12c] deg/s^2 applied in Normal driving (+ = turning right)
    public float StabilityYawCap { get; private set; }    // [DRIFT STEP 12c] deg/s turn-rate cap from maxLateralG
    // [DRIFT STEP 12c] Rear side grip bias for Normal driving: 1 below stabilityStartSpeed and while drifting.
    public float NormalRearGripBias => applyStability
        ? Mathf.Lerp(1f, highSpeedRearGrip, Mathf.Clamp01((SpeedKmh - stabilityStartSpeed) / Mathf.Max(1f, highSpeedRearGripFade)) * (1f - DriftBlend))
        : 1f;

    private PlayerCarController car;
    private Rigidbody rb;
    private WheelCollider[] wheels;
    private float rawBlend;
    private float straightTimer, counterSteerTimer, airTimer;
    private bool releasedSinceRecovery; // re-entry from Recovery needs a fresh press or a direction change
    private GUIStyle style;
    private Vector3 prevPlanar;
    private float spinCooldown; // [DRIFT STEP 10]
    private float chainTimer;   // [DRIFT STEP 11]
    private float driftStartTime, driftStartSpeed;   // [DRIFT STEP 8] set when a drift starts

    void Awake()
    {
        car = GetComponent<PlayerCarController>();
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (car == null || rb == null || car.tireColliders == null) { enabled = false; return; }
        var t = car.tireColliders;
        wheels = new[] { t.wheelFL, t.wheelFR, t.wheelRL, t.wheelRR };
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Measure(dt);
        TimeInState += dt;

        bool held = car.DriftInputHeld;
        if (!held) releasedSinceRecovery = true;
        bool grounded = wheels[2].isGrounded && wheels[3].isGrounded && (wheels[0].isGrounded || wheels[1].isGrounded);
        airTimer = grounded ? 0f : airTimer + dt;
        float forwardKmh = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
        // [DRIFT STEP 10] Angle uses the unwrapped drift angle; after a spin the angle test waits spinReentryCooldown.
        spinCooldown -= dt;
        float absAngle = Direction != 0 ? Mathf.Abs(Alpha) : Mathf.Abs(Beta);
        bool spinning = (absAngle > spinAngle && spinCooldown <= 0f) || Mathf.Abs(YawRate) > 1.3f * yawRateLimit;

        switch (State)
        {
            case DriftState.Normal:
                rawBlend = Mathf.MoveTowards(rawBlend, 0f, dt / recoveryDuration);
                if (CanStart(held, grounded, forwardKmh)) Begin();
                break;

            case DriftState.Initiation:
                rawBlend = Mathf.MoveTowards(rawBlend, 1f, dt / entryDuration);
                UpdateTarget(dt);
                if (spinning) Go(DriftState.SpinRecovery, "spin during entry");
                else if (ChainRequested(held, grounded, forwardKmh, dt)) Begin(); // [DRIFT STEP 11]
                else if (!held) Go(DriftState.Recovery, "drift released during entry");
                else if (SpeedKmh < exitSpeed) Go(DriftState.Recovery, "too slow");
                else if (Alpha >= 0.7f * TargetAngle) Go(DriftState.Active, "angle reached");
                // [DRIFT STEP 11] After a chain flick the car is still on the old side (angle < 0): wait for it to swing across.
                else if ((TimeInState >= entryDuration + 0.3f && Alpha > 0f) || TimeInState >= 1.2f) Go(DriftState.Active, "entry time out");
                break;

            case DriftState.Active:
                rawBlend = Mathf.MoveTowards(rawBlend, 1f, dt / entryDuration);
                UpdateTarget(dt);
                straightTimer = Mathf.Abs(Alpha) < exitAngle ? straightTimer + dt : 0f; // [DRIFT STEP 11] abs: a car still swinging across is not 'straight'
                counterSteerTimer = SteerInput * Direction <= -0.9f ? counterSteerTimer + dt : 0f;
                if (spinning) Go(DriftState.SpinRecovery, "spin");
                else if (ChainRequested(held, grounded, forwardKmh, dt)) Begin(); // [DRIFT STEP 11]
                else if (!held) Go(DriftState.Recovery, "drift released");
                else if (SpeedKmh < exitSpeed) Go(DriftState.Recovery, "too slow");
                else if (straightTimer >= exitAngleHoldTime) Go(DriftState.Recovery, "car straightened");
                else if (counterSteerTimer >= fullCounterSteerHoldTime) Go(DriftState.Recovery, "full counter-steer");
                else if (airTimer >= airborneExitTime) Go(DriftState.Recovery, "airborne");
                break;

            case DriftState.Recovery:
            {
                // Grip comes back only as the car straightens: full rate under 10 deg, 30% at 30 deg.
                float align = Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(10f, 30f, Mathf.Abs(Beta)));
                rawBlend = Mathf.MoveTowards(rawBlend, 0f, align * dt / recoveryDuration);
                if (spinning) Go(DriftState.SpinRecovery, "spin during recovery");
                else if (CanStart(held, grounded, forwardKmh) && (releasedSinceRecovery || (SteerInput > 0f ? 1 : -1) != Direction)) Begin();
                else if (rawBlend <= 0f && Mathf.Abs(Beta) < 3f) Go(DriftState.Normal, "recovered");
                else if (SpeedKmh < 5f) { rawBlend = 0f; Go(DriftState.Normal, "stopped"); }
                break;
            }

            case DriftState.SpinRecovery:
                rawBlend = Mathf.MoveTowards(rawBlend, spinGripBlend, dt / recoveryDuration); // [DRIFT STEP 10]
                if (Mathf.Abs(Alpha) < spinRecoveryExitAngle && Mathf.Abs(YawRate) < yawRateLimit) Go(DriftState.Recovery, "spin caught");
                else if (TimeInState >= spinRecoveryMaxTime) Go(DriftState.Recovery, "spin time out");
                else if (SpeedKmh < 5f) { rawBlend = 0f; Go(DriftState.Normal, "stopped"); }
                break;
        }

        DriftBlend = rawBlend * rawBlend * (3f - 2f * rawBlend); // smoothstep
        if (State == DriftState.Normal && rawBlend <= 0f) Direction = 0;

        // [DRIFT STEP 4] Rear sideways grip follows driftBlend. Front grip and all forward grip stay at 1.
        // With driftBlend = 0 the multiplier is exactly 1, so normal driving is unchanged.
        car.useNormalFrictionInDrift = applyRearGrip;
        car.SetAxleGripMultipliers(1f, (applyRearGrip ? PlannedRearGrip : 1f) * NormalRearGripBias, 1f, 1f); // [DRIFT STEP 12c] x bias
        // [DRIFT STEP 5] Engine torque follows driftBlend (1 when not drifting) instead of the old -200 Nm.
        car.useDriftDriveScale = applyDriveScale;
        car.driveTorqueScale = applyDriveScale ? PlannedDriveScale : 1f;
        ApplyYawAssist(); // [DRIFT STEP 6]
        ApplyStability(); // [DRIFT STEP 12c]
        ApplyFrontWheels(); // [DRIFT STEP 7]
        ApplyDriftForces(); // [DRIFT STEP 8]
        ApplyVelocityAlignment(); // [DRIFT STEP 9]
    }

    // [DRIFT STEP 6] Yaw assistance: a cascaded, acceleration-limited controller.
    // 1) The angle error sets a target yaw rate (on top of how fast the travel direction is turning).
    // 2) The yaw-rate error sets a turning acceleration, capped by maxYawAccel.
    // The angle error never makes torque directly, so it cannot wind up into a spin.
    // Applied as ForceMode.Acceleration (independent of mass/inertia) and scaled by driftBlend,
    // so it is exactly zero in Normal driving.
    void ApplyYawAssist()
    {
        YawAssist = 0f;
        if (!applyYawAssist || Direction == 0 || SpeedKmh < 5f) return;
        float weight, targetAlpha, kAngle, kRate = rateGain;
        switch (State)
        {
            case DriftState.Initiation:
                weight = DriftBlend; targetAlpha = TargetAngle; kAngle = angleGain * initiationGainBoost; break;
            case DriftState.Active:
                weight = DriftBlend; targetAlpha = TargetAngle; kAngle = angleGain; break;
            case DriftState.Recovery:
                weight = DriftBlend; targetAlpha = 0f; kAngle = recoveryAngleGain; break;
            case DriftState.SpinRecovery:
                weight = 1f; targetAlpha = 0f; kAngle = recoveryAngleGain; kRate = spinRateGain; break;
            default: return;
        }
        if (weight <= 0.001f) return;
        float into = YawRate * Direction;               // deg/s, + = rotating further into the drift
        // [DRIFT STEP 9] Leave out the turning that velocity alignment itself caused (last step), otherwise
        // the nose chases the travel direction and the travel direction chases the nose.
        float velInto = (VelocityTurnRate - AlignRate) * Direction; // deg/s the travel direction turns on its own
        float rateScale = yawRateBySpeed.Evaluate(SpeedKmh); // [DRIFT STEP 11]
        float wTarget = velInto + Mathf.Clamp(kAngle * (targetAlpha - Alpha), -maxRelativeYawRate * rateScale, maxRelativeYawRate * rateScale);
        wTarget = Mathf.Clamp(wTarget, -yawRateLimit * rateScale, yawRateLimit * rateScale);
        float cap = (State == DriftState.SpinRecovery ? spinMaxYawAccel : maxYawAccel) * yawAccelBySpeed.Evaluate(SpeedKmh); // [DRIFT STEP 10/11]
        float acc = Mathf.Clamp(kRate * (wTarget - into), -cap, cap) * weight; // deg/s^2
        YawAssist = acc * Direction;
        rb.AddTorque(transform.up * (YawAssist * Mathf.Deg2Rad), ForceMode.Acceleration);
    }

    // [DRIFT STEP 7] Front-wheel counter-steer. During a drift the front wheels point along the car's
    // real direction of travel (Beta x counterSteerFactor), plus a small trim from the player's steering.
    // Blended with the angle PlayerCarController just set, by driftBlend: 0 = controller steering
    // untouched (normal driving), 1 = full drift steering. Runs after the controller (execution order 50),
    // so its value is the one the physics step uses.
    void ApplyFrontWheels()
    {
        float controllerAngle = wheels[0].steerAngle;
        float trimInput = State == DriftState.SpinRecovery ? 0f : SteerInput; // [DRIFT STEP 10] player trim ignored while catching a spin
        DriftWheelAngle = Mathf.Clamp(Beta * counterSteerFactor + trimInput * playerSteerTrim, -maxWheelAngle, maxWheelAngle);
        if (!applyFrontCounterSteer || Direction == 0 || DriftBlend <= 0f) { FrontWheelAngle = controllerAngle; return; }
        float a = Mathf.Lerp(controllerAngle, DriftWheelAngle, DriftBlend);
        wheels[0].steerAngle = a;
        wheels[1].steerAngle = a;
        FrontWheelAngle = a;
    }

    // [DRIFT STEP 8] Drift speed control and sideways damping, both scaled by driftBlend (zero in Normal).
    // Speed: during Initiation/Active the speed follows a target that falls smoothly from the entry speed,
    // so every drift loses the same share (default 10% over 2 s) at any speed. A capped correction slows
    // the car or pushes it as needed; it never pushes above maxSpeed.
    // Sideways damping: reduces sliding sideways so the car does not drift wide of its line.
    void ApplyDriftForces()
    {
        DriftDrag = 0f;
        if (!applySpeedControl || Direction == 0 || DriftBlend <= 0f) return;
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        float v = planar.magnitude;
        if (v < 1f) return;
        if (State == DriftState.Initiation || State == DriftState.Active || State == DriftState.Recovery) // Recovery: fades out with driftBlend
        {
            // Target speed falls smoothly from the entry speed: exactly targetSpeedLossPercent after speedLossWindow s.
            float k = -Mathf.Log(1f - Mathf.Clamp(targetSpeedLossPercent, 0f, 90f) / 100f) / Mathf.Max(0.1f, speedLossWindow);
            float vT = Mathf.Min(driftStartSpeed * Mathf.Exp(-k * (Time.fixedTime - driftStartTime)), car.maxSpeed / 3.6f);
            SpeedTargetKmh = vT * 3.6f;
            DriftDrag = Mathf.Clamp(speedTrackGain * (v - vT), -maxSpeedCorrection, maxSpeedCorrection) * DriftBlend;
            rb.AddForce(-planar / v * DriftDrag, ForceMode.Acceleration);
        }
        float vLat = Vector3.Dot(planar, transform.right);
        rb.AddForce(-transform.right * (vLat * lateralDamping * DriftBlend), ForceMode.Acceleration);
    }

    // [DRIFT STEP 13] Drift effects follow the real drift state, not the Q key:
    // - smoke + trails while the car is really sliding (any drift state, blend >= effectsMinBlend, slip >= effectsMinAngle),
    //   rear wheels always, front wheels only when they slip themselves, and only on grounded wheels (no trails in the air);
    // - brake light with the smoke; nitro refills during Initiation/Active above nitroRefillMinAngle, capped at max;
    // - nitro is cut during Initiation/Active/SpinRecovery (old code: whenever Q was held).
    // Runs in Update after PlayerCarController (execution order 50), same frame rate as the old fun_ApplyDrift.
    void Update()
    {
        if (car == null || wheels == null) return;
        car.useExternalDriftEffects = applyDriftEffects;
        if (!applyDriftEffects) { EffectsActive = NitroRefilling = false; car.externalDriftActive = false; return; }
        bool sliding = State != DriftState.Normal && DriftBlend >= effectsMinBlend && Mathf.Abs(Beta) >= effectsMinAngle;
        EffectsActive = sliding;
        for (int i = 0; i < 4; i++)
        {
            bool on = false;
            if (sliding && wheels[i].GetGroundHit(out WheelHit hit))
                on = i >= 2 || Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip) > frontEffectsSlip;
            WheelEffects[i] = on;
        }
        car.SetDriftEffects(WheelEffects[0], WheelEffects[1], WheelEffects[2], WheelEffects[3], sliding);
        bool drifting = State == DriftState.Initiation || State == DriftState.Active;
        car.externalDriftActive = drifting || State == DriftState.SpinRecovery;
        NitroRefilling = drifting && Alpha >= nitroRefillMinAngle;
        if (NitroRefilling) car.AddNitro(driftNitroRefillRate * Time.deltaTime);
    }

    // [DRIFT STEP 12c] High-speed stability in Normal driving. Two jobs, same cascaded method as the yaw assist:
    // 1) Turn-rate cap: yaw rate is kept within maxLateralG / speed, so the car never asks the tyres for more
    //    sideways grip than they have (the cause of the 35-40 deg slides at 180-210 km/h).
    // 2) Slip catch: if the slip angle passes stabilitySlipDeadband, the nose is turned back toward the
    //    direction of travel, also while no steering key is held.
    // Inside the cap and the deadband it does nothing. Faded in from stabilityStartSpeed to stabilityFullSpeed
    // and out by driftBlend, so drifts and low-speed driving are unchanged.
    void ApplyStability()
    {
        StabilityAssist = 0f;
        StabilityYawCap = 0f;
        if (!applyStability || State != DriftState.Normal) return;
        float fade = Mathf.Clamp01((SpeedKmh - stabilityStartSpeed) / Mathf.Max(1f, stabilityFullSpeed - stabilityStartSpeed)) * (1f - DriftBlend);
        if (fade <= 0f || !wheels[2].isGrounded || !wheels[3].isGrounded) return;
        float v = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up).magnitude;
        if (v < 5f) return;
        float wMax = maxLateralG * 9.81f / v * Mathf.Rad2Deg; // deg/s
        StabilityYawCap = wMax;
        float excess = Mathf.Sign(Beta) * Mathf.Max(0f, Mathf.Abs(Beta) - stabilitySlipDeadband);
        float err;
        if (excess != 0f)
            err = Mathf.Clamp(VelocityTurnRate + stabilitySlipGain * excess, -wMax, wMax) - YawRate;
        else if (Mathf.Abs(YawRate) > wMax)
            err = Mathf.Sign(YawRate) * wMax - YawRate;
        else return;
        float acc = Mathf.Clamp(stabilityRateGain * err, -stabilityMaxYawAccel, stabilityMaxYawAccel) * fade;
        StabilityAssist = acc;
        YawAssist = acc; // telemetry column yawAssist_degs2 shows it (drift yaw assist is zero in Normal)
        rb.AddTorque(transform.up * (acc * Mathf.Deg2Rad), ForceMode.Acceleration);
    }

    // [DRIFT STEP 9] Velocity alignment during Recovery: the direction of travel is turned toward
    // where the car points, at up to velocityAlignRate deg/s, keeping its speed. The car body is never
    // turned by this, so it feels like the car catches its line, not like it is being steered.
    // Scaled by driftBlend, so it fades out as normal grip returns.
    void ApplyVelocityAlignment()
    {
        AlignRate = 0f;
        if (!applyVelocityAlignment || State != DriftState.Recovery || DriftBlend <= 0f) return;
        Vector3 up = transform.up;
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float v = planar.magnitude;
        if (v < 3f) return;
        Vector3 nose = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        float angle = Vector3.SignedAngle(planar, nose, up);
        float step = Mathf.Clamp(angle, -velocityAlignRate * DriftBlend * Time.fixedDeltaTime, velocityAlignRate * DriftBlend * Time.fixedDeltaTime);
        Vector3 newPlanar = Quaternion.AngleAxis(step, up) * planar; // same length, new direction
        rb.AddForce(newPlanar - planar, ForceMode.VelocityChange);
        AlignRate = step / Time.fixedDeltaTime;
    }

    // [DRIFT STEP 11] Chain flick: with drift held, steering firmly the other way (at least
    // chainSteerThreshold for chainDelay s) starts the opposite drift straight away. driftBlend is kept,
    // so the rear stays loose and the yaw assist swings the car across. Lighter counter-steer only
    // lowers the target angle; releasing drift exits.
    bool ChainRequested(bool held, bool grounded, float forwardKmh, float dt)
    {
        bool opposite = held && SteerInput * Direction <= -chainSteerThreshold;
        chainTimer = opposite ? chainTimer + dt : 0f;
        return allowChainFlick && chainTimer >= chainDelay && grounded && forwardKmh >= exitSpeed;
    }

    void OnDisable()
    {
        if (car == null) return;
        car.useNormalFrictionInDrift = false;
        car.ResetAxleGripMultipliers();
        car.useDriftDriveScale = false;
        car.driveTorqueScale = 1f;
        car.useExternalDriftEffects = false; // [DRIFT STEP 13] hand effects back to the old code
        car.externalDriftActive = false;
        car.SetDriftEffects(false, false, false, false, false);
    }

    void Measure(float dt)
    {
        Vector3 up = transform.up;
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;
        Beta = planar.magnitude > 3f ? Vector3.SignedAngle(transform.forward, planar, up) : 0f;
        YawRate = Vector3.Dot(rb.angularVelocity, up) * Mathf.Rad2Deg;
        // [DRIFT STEP 6] How fast the direction of travel is turning (smoothed over ~0.1 s).
        float rawTurn = (planar.sqrMagnitude > 9f && prevPlanar.sqrMagnitude > 9f) ? Vector3.SignedAngle(prevPlanar, planar, up) / dt : 0f;
        VelocityTurnRate = Mathf.Lerp(VelocityTurnRate, rawTurn, Mathf.Clamp01(dt / 0.1f));
        prevPlanar = planar;
        // Own smoothed steering copy: same ramp as the controller (3/s), but no instant snap to 0.
        SteerInput = Mathf.MoveTowards(SteerInput, Mathf.Clamp(car.RawSteerInput, -1f, 1f), driftSteerResponse * dt); // [DRIFT STEP 11] was 3/s
        // [DRIFT STEP 10] Unwrapped past +/-180 deg, so a car spun past backwards is still turned back the right way.
        float rawAlpha = Direction != 0 ? -Beta * Direction : 0f;
        Alpha = Direction != 0 ? Alpha + Mathf.DeltaAngle(Alpha, rawAlpha) : 0f;
    }

    bool CanStart(bool held, bool grounded, float forwardKmh)
    {
        return held && grounded && forwardKmh >= minDriftSpeed && Mathf.Abs(SteerInput) >= initiationSteerThreshold;
    }

    void Begin()
    {
        int newDir = SteerInput > 0f ? 1 : -1;
        string why = State == DriftState.Normal ? "drift started" : (newDir != Direction ? "drift chained (direction change)" : "drift resumed");
        if (State == DriftState.Normal || newDir != Direction) TargetAngle = neutralDriftAngle;
        Direction = newDir;
        Alpha = -Beta * Direction;
        driftStartTime = Time.fixedTime; // [DRIFT STEP 8] each drift (also a chained one) starts its own 10% budget
        driftStartSpeed = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up).magnitude;
        Go(DriftState.Initiation, why);
    }

    void UpdateTarget(float dt)
    {
        float into = SteerInput * Direction;
        float goal = into >= 0f
            ? Mathf.Lerp(neutralDriftAngle, maxDriftAngle, into)
            : Mathf.Lerp(neutralDriftAngle, counterSteerAngle, -into);
        TargetAngle = Mathf.MoveTowards(TargetAngle, goal, targetAngleRate * dt);
    }

    void Go(DriftState next, string reason)
    {
        LastTransition = State + " -> " + next + " (" + reason + ") at " + SpeedKmh.ToString("0") + " km/h, " + Alpha.ToString("0") + "°";
        if (State == DriftState.SpinRecovery) spinCooldown = spinReentryCooldown; // [DRIFT STEP 10]
        if (next == DriftState.Recovery && State != DriftState.SpinRecovery) releasedSinceRecovery = false;
        State = next;
        TimeInState = 0f;
        straightTimer = counterSteerTimer = chainTimer = 0f;
    }

    void OnGUI()
    {
        if (!showOverlay) return;
        if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 16 };
        string col = State == DriftState.Normal ? "#aaaaaa" : State == DriftState.SpinRecovery ? "#ff5555" : State == DriftState.Recovery ? "#66dd66" : "#ffcc00";
        string dir = Direction > 0 ? "RIGHT" : Direction < 0 ? "LEFT" : "-";
        string txt =
            "<b>ARCADE DRIFT (Step 13: + drift effects)</b>\n" +
            "effects: " + (EffectsActive ? "<color=#ffcc00>SMOKE</color>" : "off") + "  wheels FL" + (WheelEffects[0] ? 1 : 0) + " FR" + (WheelEffects[1] ? 1 : 0) + " RL" + (WheelEffects[2] ? 1 : 0) + " RR" + (WheelEffects[3] ? 1 : 0) + "  nitro " + car.nitroCapacity.ToString("0") + (NitroRefilling ? " +" : "") + "  (" + (applyDriftEffects ? "ON" : "OFF") + ")\n" +
            "stability: " + StabilityAssist.ToString("0") + " \u00B0/s\u00B2, cap " + StabilityYawCap.ToString("0") + " \u00B0/s, rear grip bias x" + NormalRearGripBias.ToString("0.00") + "  (" + (applyStability ? "ON" : "OFF") + ")\n" +
            "alignment: " + AlignRate.ToString("0") + " \u00B0/s  (" + (applyVelocityAlignment ? "ON" : "OFF") + ")\n" +
            "speed target: " + SpeedTargetKmh.ToString("0") + " km/h, correction " + DriftDrag.ToString("0.00") + " m/s\u00B2  (" + (applySpeedControl ? "ON" : "OFF") + ")\n" +
            "front wheels: " + FrontWheelAngle.ToString("0.0") + "\u00B0  (drift wants " + DriftWheelAngle.ToString("0.0") + "\u00B0, " + (applyFrontCounterSteer ? "ON" : "OFF") + ")\n" +
            "yaw assist: " + YawAssist.ToString("0") + " \u00B0/s\u00B2  (" + (applyYawAssist ? "ON" : "OFF") + ")\n" +
            "state: <color=" + col + "><b>" + State + "</b></color>  (" + TimeInState.ToString("0.0") + " s)\n" +
            "direction: " + dir + "   blend: " + DriftBlend.ToString("0.00") + "\n" +
            "drift angle: " + Alpha.ToString("0.0") + "°  target: " + TargetAngle.ToString("0.0") + "°\n" +
            "steer: " + SteerInput.ToString("0.00") + "   drift held: " + car.DriftInputHeld + "\n" +
            "planned rear grip x" + PlannedRearGrip.ToString("0.00") + "  drive x" + PlannedDriveScale.ToString("0.00") + (" grip:" + (applyRearGrip ? "ON" : "OFF") + " drive:" + (applyDriveScale ? "ON" : "OFF") + "\n") +
            "last: " + LastTransition;
        var c = new GUIContent(txt);
        float w = 520f, h = style.CalcHeight(c, w);
        GUI.Box(new Rect(Screen.width - w - 10f, 10f, w, h), c, style);
    }
}
