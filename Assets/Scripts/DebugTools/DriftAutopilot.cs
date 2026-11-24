using System.Collections;
using UnityEngine;

/// <summary>
/// Drift roadmap - test helper: drives FixedAltima through a fixed test scenario so
/// telemetry runs are repeatable. It only "presses" the existing on-screen buttons
/// (rightBtn / leftBtn / driftBtn on PlayerCarController), exactly like a player would.
/// It never touches the Rigidbody, wheels or friction.
///
/// Meant to be added at runtime (Play Mode) by a test command, not saved in the scene.
/// Requires DriftTelemetry on the same GameObject for recording.
/// </summary>
[DisallowMultipleComponent]
public class DriftAutopilot : MonoBehaviour
{
    public enum Scenario { Straight, Corner, DriftQ, DriftRelease, CounterSteerExit, SpinKick, Chain }

    public Scenario scenario = Scenario.Straight;
    [Tooltip("Speed (km/h) at which the manoeuvre starts.")]
    public float triggerSpeed = 80f;
    [Tooltip("Label for the CSV file name.")]
    public string runLabel = "auto";
    public float steerHoldSeconds = 2f;
    public float maxRunSeconds = 25f;
    [Tooltip("SpinKick only: extra yaw rate (deg/s) added into the drift, like clipping a wall.")] public float spinKickDegPerSec = 180f;

    public bool Finished { get; private set; }
    public int Collisions { get; private set; }
    public string CollisionLog { get; private set; } = "";
    public string Status { get; private set; } = "starting";

    private PlayerCarController car;
    private DriftTelemetry telemetry;
    private Rigidbody rb;
    private float SpeedKmh => rb.linearVelocity.magnitude * 3.6f;

    void Start()
    {
        car = GetComponent<PlayerCarController>();
        telemetry = GetComponent<DriftTelemetry>();
        rb = GetComponent<Rigidbody>();
        Application.runInBackground = true; // runtime only; project setting unchanged
        StartCoroutine(Run());
    }

    void SetInputs(int steer, bool drift)
    {
        car.rightBtn.isPressed = steer > 0;
        car.leftBtn.isPressed = steer < 0;
        car.driftBtn.isPressed = drift;
    }

    IEnumerator WaitForSpeed(float kmh, float timeout)
    {
        timeout = Mathf.Max(timeout, 45f);
        float t = 0f;
        while (SpeedKmh < kmh && t < timeout) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator Hold(int steer, bool drift, float seconds)
    {
        SetInputs(steer, drift);
        yield return new WaitForSeconds(seconds);
        SetInputs(0, false);
    }

    IEnumerator Run()
    {
        float start = Time.time;
        if (telemetry != null) { telemetry.runLabel = runLabel; telemetry.StartRecording(); }
        SetInputs(0, false);

        switch (scenario)
        {
            case Scenario.Straight:
                Status = "straight";
                yield return new WaitForSeconds(steerHoldSeconds > 2.5f ? steerHoldSeconds : 15f);
                break;

            case Scenario.Corner:
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "left";
                yield return Hold(-1, false, steerHoldSeconds);
                yield return new WaitForSeconds(1f);
                Status = "right";
                yield return Hold(1, false, steerHoldSeconds);
                yield return new WaitForSeconds(2f);
                break;

            case Scenario.DriftQ:
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "drift left";
                yield return Hold(-1, true, steerHoldSeconds);
                yield return new WaitForSeconds(1.5f);
                Status = "waiting for speed (2)";
                yield return WaitForSpeed(triggerSpeed, 6f);
                Status = "drift right";
                yield return Hold(1, true, steerHoldSeconds);
                yield return new WaitForSeconds(2f);
                break;

            case Scenario.Chain:
                // Drift left, then steer right while still holding drift (chain into a right drift), then let go.
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "drift left";
                SetInputs(-1, true);
                yield return new WaitForSeconds(steerHoldSeconds);
                Status = "chain right";
                SetInputs(1, true);
                yield return new WaitForSeconds(steerHoldSeconds + 1.5f);
                SetInputs(0, false);
                Status = "released";
                yield return new WaitForSeconds(2.5f);
                break;

            case Scenario.SpinKick:
                // TEST ONLY: drift left, then add a sudden yaw-rate kick into the drift to force a spin,
                // keep holding the drift for a moment, then let go.
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "drift left";
                SetInputs(-1, true);
                yield return new WaitForSeconds(1.0f);
                Status = "spin kick";
                yield return new WaitForFixedUpdate();
                rb.AddTorque(transform.up * (-spinKickDegPerSec * Mathf.Deg2Rad), ForceMode.VelocityChange);
                yield return new WaitForSeconds(steerHoldSeconds);
                SetInputs(0, false);
                Status = "released";
                yield return new WaitForSeconds(3f);
                break;

            case Scenario.CounterSteerExit:
                // Drift left, then steer fully right while still holding drift (full counter-steer exit), then let go.
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "drift left";
                SetInputs(-1, true);
                yield return new WaitForSeconds(steerHoldSeconds);
                Status = "full counter-steer, drift held";
                SetInputs(1, true);
                yield return new WaitForSeconds(1f);
                SetInputs(0, false);
                yield return new WaitForSeconds(2.5f);
                break;

            case Scenario.DriftRelease:
                Status = "waiting for speed";
                yield return WaitForSpeed(triggerSpeed, 15f);
                Status = "drift left";
                SetInputs(-1, true);
                yield return new WaitForSeconds(steerHoldSeconds);
                Status = "release drift, keep steering";
                SetInputs(-1, false);
                yield return new WaitForSeconds(1.5f);
                SetInputs(0, false);
                yield return new WaitForSeconds(2f);
                break;
        }

        SetInputs(0, false);
        if (telemetry != null) telemetry.StopRecording();
        Status = "finished in " + (Time.time - start).ToString("0.0") + " s";
        Finished = true;
    }

    void Update()
    {
        if (!Finished && Time.time > maxRunSeconds + 1f)
        {
            StopAllCoroutines();
            SetInputs(0, false);
            if (telemetry != null) telemetry.StopRecording();
            Status = "timed out";
            Finished = true;
        }
    }

    void OnCollisionEnter(Collision c)
    {
        Collisions++;
        if (CollisionLog.Length < 300)
            CollisionLog += c.gameObject.name + "@" + (SpeedKmh).ToString("0") + "km/h t=" + Time.timeSinceLevelLoad.ToString("0.0") + "; ";
    }

    void OnDisable()
    {
        if (car != null) SetInputs(0, false);
    }
    // ---------------- Editor test bootstrap ----------------
    // When a request file exists, add the autopilot as soon as the scene loads, so a run
    // starts from a standstill. Editor only; does nothing in builds or without the file.
    // File: <Project>/Telemetry/autopilot_request.txt  with  scenario;triggerSpeed;runLabel
    // The file is emptied after use.
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootFromRequestFile()
    {
        string f = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Telemetry", "autopilot_request.txt");
        if (!System.IO.File.Exists(f)) return;
        string txt = System.IO.File.ReadAllText(f).Trim();
        if (txt.Length == 0) return;
        System.IO.File.WriteAllText(f, "");
        string[] p = txt.Split(';');
        var car = GameObject.Find("FixedAltima");
        if (car == null || p.Length < 3) return;
        var ap = car.AddComponent<DriftAutopilot>();
        ap.scenario = (Scenario)System.Enum.Parse(typeof(Scenario), p[0]);
        ap.triggerSpeed = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
        ap.runLabel = p[2];
        ap.maxRunSeconds = 40f;
        if (p.Length >= 4) ap.steerHoldSeconds = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
        if (p.Length >= 5) ap.spinKickDegPerSec = float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture);
        // TEST-ONLY maxSpeed override for this Play session (not saved). Set before PlayerCarController.Start
        // so its gear bands are computed from it.
        if (p.Length >= 6) { car.GetComponent<PlayerCarController>().maxSpeed = float.Parse(p[5], System.Globalization.CultureInfo.InvariantCulture); }
        ap.maxRunSeconds = 90f;
    }
#endif
}
