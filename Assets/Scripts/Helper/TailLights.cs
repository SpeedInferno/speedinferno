using UnityEngine;

// Tail lights as their own component (a separate child object on each car).
// Other systems only tell it *why* the lights should be on; it decides how the lamps look.
// Currently driven by drift. Other triggers (e.g. brake) can be added later as extra requests.
public class TailLights : MonoBehaviour
{
    [Tooltip("Renderers whose material glows while the tail lights are on. Their material needs emission enabled (e.g. TailLightMat).")]
    public Renderer[] emissiveRenderers;

    [Tooltip("Renderers that are only visible while the tail lights are on (e.g. lamp overlays on a body mesh without separate lamps).")]
    public Renderer[] showWhenOnRenderers;

    [Tooltip("Optional real lights switched together with the lamps (red glow on the road).")]
    public Light[] lights;

    [Header("Glow")]
    public Color glowColor = new Color(1f, 0.05f, 0.02f);
    [Tooltip("HDR multiplier for the emission colour while on.")]
    public float glowIntensity = 6f;

    public bool IsOn { get; private set; }

    private bool driftRequest;
    private MaterialPropertyBlock block;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        block = new MaterialPropertyBlock();
        Apply(false);
    }

    // Called by the car while the drift button / key is held.
    public void SetDrift(bool on)
    {
        if (driftRequest == on)
        {
            return;
        }
        driftRequest = on;
        Refresh();
    }

    private void Refresh()
    {
        bool want = driftRequest;
        if (want != IsOn)
        {
            Apply(want);
        }
    }

    private void Apply(bool on)
    {
        IsOn = on;

        Color emission = on ? glowColor * glowIntensity : Color.black;
        if (emissiveRenderers != null)
        {
            foreach (Renderer r in emissiveRenderers)
            {
                if (r == null)
                {
                    continue;
                }
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }

        if (showWhenOnRenderers != null)
        {
            foreach (Renderer r in showWhenOnRenderers)
            {
                if (r == null)
                {
                    continue;
                }
                r.enabled = on;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }

        if (lights != null)
        {
            foreach (Light l in lights)
            {
                if (l != null)
                {
                    l.enabled = on;
                }
            }
        }
    }
}
