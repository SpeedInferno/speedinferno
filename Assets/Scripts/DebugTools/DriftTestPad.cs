#if UNITY_EDITOR
using System.IO;
using UnityEngine;

/// <summary>
/// Test helper (Editor only). When <Project>/Telemetry/testpad_request.txt holds a height (e.g. 101.2),
/// FixedAltima is moved to that height at the start of Play Mode, so autopilot test runs use the flat
/// test pad at y = 100 instead of the track. Nothing is saved to the scene. Empty or missing file = no move.
/// </summary>
public static class DriftTestPad
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Move()
    {
        string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Telemetry", "testpad_request.txt");
        if (!File.Exists(file)) return;
        string s = File.ReadAllText(file).Trim();
        if (!float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) return;
        var car = GameObject.Find("FixedAltima");
        if (car == null) return;
        var p = car.transform.position;
        car.transform.position = new Vector3(p.x, y, p.z);
        var rb = car.GetComponent<Rigidbody>();
        if (rb != null) rb.position = car.transform.position;
        Debug.Log("DriftTestPad: FixedAltima moved to y = " + y + " for this Play session only.");
    }
}
#endif
