using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drift roadmap - Step 1: read-only telemetry for FixedAltima.
///
/// This component only OBSERVES the car. It never writes to the Rigidbody, the
/// WheelColliders or PlayerCarController, so it cannot change how the car drives.
/// It runs after PlayerCarController (execution order 100) so it sees the values
/// that controller applied in the same physics step.
///
/// F1  = show / hide the on-screen overlay
/// F9  = start / stop recording a CSV (one row per physics step)
/// F10 = reset the peak values shown in the overlay
///
/// CSV files go to <ProjectFolder>/Telemetry/ in the Editor (outside Assets, so
/// Unity does not import them) and to Application.persistentDataPath/Telemetry
/// in a build.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class DriftTelemetry : MonoBehaviour
{
    [Header("Display")]
    public bool showOverlay = true;
    public Key toggleOverlayKey = Key.F1;
    public bool drawGizmos = true;
    [Range(10, 40)] public int overlayFontSize = 16;

    [Header("Recording")]
    public Key toggleRecordKey = Key.F9;
    public Key resetPeaksKey = Key.F10;
    public string outputFolder = "Telemetry";
    [Tooltip("Optional label added to the CSV file name, e.g. 'baseline_corner_80'.")]
    public string runLabel = "baseline";

    [Header("Measurement")]
    [Tooltip("Below this planar speed (m/s) the drift angle is not meaningful and is reported as 0.")]
    public float minSpeedForAngle = 3f;

    // ---------- Live values (read-only for other scripts) ----------
    public float SpeedKmh { get; private set; }
    public float ForwardSpeed { get; private set; }   // m/s, + = forwards
    public float LateralSpeed { get; private set; }   // m/s, + = to the car's right
    public float DriftAngle { get; private set; }     // deg, SignedAngle(forward, planar velocity, up)
    public float YawRate { get; private set; }        // deg/s, + = turning right
    public float FrontSteerAngle { get; private set; }
    public bool DriftMode { get; private set; }
    public bool IsRecording => writer != null;

    public float PeakAbsDriftAngle { get; private set; }
    public float PeakAbsYawRate { get; private set; }
    public float PeakAbsLateralSpeed { get; private set; }

    private Rigidbody rb;
    private PlayerCarController car;
    private ArcadeDriftController drift;
    private WheelCollider[] wheels;
    private readonly string[] wheelNames = { "FL", "FR", "RL", "RR" };
    private readonly WheelHit[] hits = new WheelHit[4];
    private readonly bool[] grounded = new bool[4];

    private StreamWriter writer;
    private string currentFile;
    private string lastSavedFile;
    private float recordStartTime;
    private int recordedRows;
    private GUIStyle style;
    private readonly StringBuilder sb = new StringBuilder(1024);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        car = GetComponent<PlayerCarController>();
        drift = GetComponent<ArcadeDriftController>();
    }

    void Start()
    {
        if (car != null && car.tireColliders != null)
        {
            wheels = new[]
            {
                car.tireColliders.wheelFL, car.tireColliders.wheelFR,
                car.tireColliders.wheelRL, car.tireColliders.wheelRR
            };
        }
        if (rb == null || wheels == null)
        {
            Debug.LogWarning("DriftTelemetry: Rigidbody or PlayerCarController wheel colliders not found. Telemetry disabled.");
            enabled = false;
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[toggleOverlayKey].wasPressedThisFrame) showOverlay = !showOverlay;
        if (kb[toggleRecordKey].wasPressedThisFrame) { if (IsRecording) StopRecording(); else StartRecording(); }
        if (kb[resetPeaksKey].wasPressedThisFrame) ResetPeaks();
    }

    void FixedUpdate()
    {
        Vector3 up = transform.up;
        Vector3 vel = rb.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(vel, up);

        SpeedKmh = vel.magnitude * 3.6f;
        ForwardSpeed = Vector3.Dot(planar, transform.forward);
        LateralSpeed = Vector3.Dot(planar, transform.right);
        DriftAngle = planar.magnitude > minSpeedForAngle
            ? Vector3.SignedAngle(transform.forward, planar, up)
            : 0f;
        YawRate = Vector3.Dot(rb.angularVelocity, up) * Mathf.Rad2Deg;
        FrontSteerAngle = wheels[0] != null ? wheels[0].steerAngle : 0f;
        DriftMode = car != null && car.driftMode;

        for (int i = 0; i < 4; i++)
        {
            grounded[i] = wheels[i] != null && wheels[i].GetGroundHit(out hits[i]);
            if (!grounded[i]) hits[i] = default;
        }

        PeakAbsDriftAngle = Mathf.Max(PeakAbsDriftAngle, Mathf.Abs(DriftAngle));
        PeakAbsYawRate = Mathf.Max(PeakAbsYawRate, Mathf.Abs(YawRate));
        PeakAbsLateralSpeed = Mathf.Max(PeakAbsLateralSpeed, Mathf.Abs(LateralSpeed));

        if (writer != null) WriteRow();
    }

    // ---------------- Recording ----------------

    public void StartRecording()
    {
        if (writer != null) return;
        string root = Application.isEditor
            ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputFolder)
            : Path.Combine(Application.persistentDataPath, outputFolder);
        Directory.CreateDirectory(root);
        string label = string.IsNullOrWhiteSpace(runLabel) ? "" : "_" + Sanitize(runLabel);
        currentFile = Path.Combine(root, "drift" + label + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        writer = new StreamWriter(currentFile, false, Encoding.UTF8);

        sb.Clear();
        sb.Append("time_s,driftMode,speed_kmh,vForward_ms,vLateral_ms,driftAngle_deg,yawRate_degs,frontSteer_deg,");
        sb.Append("controllerSlipAngle_deg,motorTorqueRL,motorTorqueRR,rearSideExtremumValue,rearSideStiffness");
        foreach (var n in wheelNames)
            sb.Append(',').Append(n).Append("_grounded,").Append(n).Append("_fwdSlip,").Append(n).Append("_sideSlip");
        sb.Append(",driftState,driftBlend,driftDir,driftAlpha_deg,driftTarget_deg,yawAssist_degs2,velTurnRate_degs");
        sb.Append(",fx_FL,fx_FR,fx_RL,fx_RR,nitroCapacity,nitroRefill"); // [DRIFT STEP 13]
        writer.WriteLine(sb.ToString());

        recordStartTime = Time.time;
        recordedRows = 0;
        ResetPeaks();
        Debug.Log("DriftTelemetry: recording to " + currentFile);
    }

    public void StopRecording()
    {
        if (writer == null) return;
        writer.Flush();
        writer.Close();
        writer = null;
        lastSavedFile = currentFile;
        Debug.Log("DriftTelemetry: saved " + recordedRows + " rows to " + lastSavedFile);
    }

    public void ResetPeaks()
    {
        PeakAbsDriftAngle = PeakAbsYawRate = PeakAbsLateralSpeed = 0f;
    }

    private void WriteRow()
    {
        var rl = wheels[2];
        var side = rl != null ? rl.sidewaysFriction : default;
        sb.Clear();
        sb.Append(F(Time.time - recordStartTime)).Append(',')
          .Append(DriftMode ? 1 : 0).Append(',')
          .Append(F(SpeedKmh)).Append(',')
          .Append(F(ForwardSpeed)).Append(',')
          .Append(F(LateralSpeed)).Append(',')
          .Append(F(DriftAngle)).Append(',')
          .Append(F(YawRate)).Append(',')
          .Append(F(FrontSteerAngle)).Append(',')
          .Append(F(car != null ? car.slipAngle : 0f)).Append(',')
          .Append(F(rl != null ? rl.motorTorque : 0f)).Append(',')
          .Append(F(wheels[3] != null ? wheels[3].motorTorque : 0f)).Append(',')
          .Append(F(side.extremumValue)).Append(',')
          .Append(F(side.stiffness));
        for (int i = 0; i < 4; i++)
            sb.Append(',').Append(grounded[i] ? 1 : 0)
              .Append(',').Append(F(hits[i].forwardSlip))
              .Append(',').Append(F(hits[i].sidewaysSlip));
        sb.Append(',').Append(drift != null ? drift.State.ToString() : "-")
          .Append(',').Append(F(drift != null ? drift.DriftBlend : 0f))
          .Append(',').Append(drift != null ? drift.Direction : 0)
          .Append(',').Append(F(drift != null ? drift.Alpha : 0f))
          .Append(',').Append(F(drift != null ? drift.TargetAngle : 0f))
          .Append(',').Append(F(drift != null ? drift.YawAssist : 0f))
          .Append(',').Append(F(drift != null ? drift.VelocityTurnRate : 0f));
        for (int i = 0; i < 4; i++) sb.Append(',').Append(drift != null && drift.WheelEffects[i] ? 1 : 0); // [DRIFT STEP 13]
        sb.Append(',').Append(F(car != null ? car.nitroCapacity : 0f)).Append(',').Append(drift != null && drift.NitroRefilling ? 1 : 0);
        writer.WriteLine(sb.ToString());
        recordedRows++;
    }

    void OnDisable() { StopRecording(); }
    void OnApplicationQuit() { StopRecording(); }

    private static string F(float v) { return v.ToString("0.###", Inv); }

    private static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    // ---------------- Overlay ----------------

    void OnGUI()
    {
        if (!showOverlay || wheels == null) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
        }
        style.fontSize = overlayFontSize;

        sb.Clear();
        sb.Append("<b>DRIFT TELEMETRY</b>  (F1 hide, F9 rec, F10 reset peaks)\n");
        sb.Append(IsRecording
            ? "<color=#ff5555>● REC</color> " + recordedRows + " rows\n"
            : (lastSavedFile != null ? "saved: " + Path.GetFileName(lastSavedFile) + "\n" : "not recording\n"));
        sb.Append("driftMode: ").Append(DriftMode ? "<color=#ffcc00>ON</color>" : "off").Append('\n');
        sb.Append("speed: ").Append(SpeedKmh.ToString("0", Inv)).Append(" km/h   fwd ")
          .Append(ForwardSpeed.ToString("0.0", Inv)).Append("  lat ").Append(LateralSpeed.ToString("0.0", Inv)).Append(" m/s\n");
        sb.Append("drift angle: ").Append(DriftAngle.ToString("0.0", Inv)).Append("°   yaw rate: ")
          .Append(YawRate.ToString("0", Inv)).Append(" °/s\n");
        sb.Append("front steer: ").Append(FrontSteerAngle.ToString("0.0", Inv)).Append("°   rear torque: ")
          .Append(wheels[2] != null ? wheels[2].motorTorque.ToString("0", Inv) : "-").Append(" Nm\n");
        if (wheels[2] != null)
            sb.Append("rear side grip peak: ").Append(wheels[2].sidewaysFriction.extremumValue.ToString("0.00", Inv))
              .Append("  stiffness: ").Append(wheels[2].sidewaysFriction.stiffness.ToString("0.00", Inv)).Append('\n');
        sb.Append("side slip  FL ").Append(hits[0].sidewaysSlip.ToString("0.00", Inv))
          .Append("  FR ").Append(hits[1].sidewaysSlip.ToString("0.00", Inv))
          .Append("  RL ").Append(hits[2].sidewaysSlip.ToString("0.00", Inv))
          .Append("  RR ").Append(hits[3].sidewaysSlip.ToString("0.00", Inv)).Append('\n');
        sb.Append("grounded: ");
        for (int i = 0; i < 4; i++) sb.Append(wheelNames[i]).Append(grounded[i] ? "✓ " : "✗ ");
        sb.Append('\n');
        sb.Append("<b>peaks</b>  angle ").Append(PeakAbsDriftAngle.ToString("0.0", Inv))
          .Append("°  yaw ").Append(PeakAbsYawRate.ToString("0", Inv))
          .Append(" °/s  lat ").Append(PeakAbsLateralSpeed.ToString("0.0", Inv)).Append(" m/s");

        var content = new GUIContent(sb.ToString());
        float w = overlayFontSize * 30f;
        float h = style.CalcHeight(content, w);
        GUI.Box(new Rect(10, 10, w, h), content, style);
    }

    // ---------------- Scene view gizmos ----------------

    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying || rb == null) return;
        Vector3 p = transform.position + transform.up * 1.5f;
        Gizmos.color = Color.cyan;   // where the car points
        Gizmos.DrawLine(p, p + transform.forward * 4f);
        Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        if (planar.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.yellow; // where the car is going
            Gizmos.DrawLine(p, p + planar.normalized * 4f);
        }
    }
}
