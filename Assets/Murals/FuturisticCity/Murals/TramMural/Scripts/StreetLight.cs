using System.Collections;
using UnityEngine;

public class StreetLight : MonoBehaviour
{
    [SerializeField] Renderer bulb;
    [SerializeField] Light pointLight;
    [Tooltip("Pick this color from the mural with the eyedropper (warm yellow).")]
    [SerializeField] Color glowColor = new Color(1f, 0.82f, 0.35f);
    [SerializeField] float emissionIntensity = 2.5f;
    [SerializeField] float lightIntensity = 1.2f;
    [SerializeField] float flickerSeconds = 0.5f;

    Material bulbMaterial;
    Coroutine routine;

    public bool IsOn { get; private set; }

    void Awake()
    {
        if (bulb != null)
        {
            bulbMaterial = bulb.material; // instance, so other lights are not affected
            bulbMaterial.EnableKeyword("_EMISSION");
        }
        TurnOff();
    }

    public void TurnOn()
    {
        if (IsOn) return;
        IsOn = true;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlickerOn());
    }

    public void TurnOff()
    {
        IsOn = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        SetBrightness(0f);
    }

    IEnumerator FlickerOn()
    {
        float t = 0f;
        while (t < flickerSeconds)
        {
            t += Time.deltaTime;
            float ramp = t / flickerSeconds;
            
            float level = Random.value < (1f - ramp) * 0.5f ? 0.1f : ramp;
            SetBrightness(level);
            yield return null;
        }
        SetBrightness(1f);
        routine = null;
    }

    void SetBrightness(float level)
    {
        if (bulbMaterial != null)
            bulbMaterial.SetColor("_EmissionColor", glowColor * (emissionIntensity * level));
        if (pointLight != null)
        {
            pointLight.color = glowColor;
            pointLight.intensity = lightIntensity * level;
        }
    }
}