using System.Collections;
using UnityEngine;

public class StreetLightSet : MonoBehaviour
{
    public enum Mode { PassByTram, Sequence }

    [SerializeField] Mode mode = Mode.PassByTram;
    [SerializeField] StreetLight[] lights;

    [Header("PassByTram")]
    [SerializeField] TramSequence tram;
    [Tooltip("Distance (in mural-width units) at which a light notices the tram.")]
    [SerializeField] float triggerDistance = 0.15f;

    [Header("Sequence")]
    [SerializeField] float startDelay = 1.0f;
    [SerializeField] float gapBetweenLights = 0.35f;

    Coroutine sequenceRoutine;
    bool watching;

    public void ResetAll()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        foreach (var light in lights)
            if (light != null) light.TurnOff();

        watching = mode == Mode.PassByTram;

        if (mode == Mode.Sequence && gameObject.activeInHierarchy)
            sequenceRoutine = StartCoroutine(RunSequence());
    }

    void Update()
    {
        if (!watching || tram == null || tram.Tram == null) return;

  
        Vector3 tramPos = tram.Tram.localPosition;
        foreach (var light in lights)
        {
            if (light == null || light.IsOn) continue;
            if (Vector3.Distance(light.transform.localPosition, tramPos) <= triggerDistance)
                light.TurnOn();
        }
    }

    IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(startDelay);
        foreach (var light in lights)
        {
            if (light != null) light.TurnOn();
            yield return new WaitForSeconds(gapBetweenLights);
        }
        sequenceRoutine = null;
    }

    
    public void LightAll()
    {
        foreach (var light in lights)
            if (light != null) light.TurnOn();
    }
}