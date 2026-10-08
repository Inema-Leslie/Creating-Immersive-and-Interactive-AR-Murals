using System.Collections;
using UnityEngine;

public class MuralExperience : MonoBehaviour
{
    [SerializeField] TramSequence tram;
    [SerializeField] StreetLightSet streetLights;

    [Tooltip("Everything that should be hidden when the mural is not tracked.")]
    [SerializeField] GameObject visuals;

    [Tooltip("Seconds to wait after tracking is lost before resetting, so brief flickers don't restart the show.")]
    [SerializeField] float lostGraceSeconds = 1.5f;

    bool started;
    Coroutine lostRoutine;

    void Awake()
    {
        if (visuals != null) visuals.SetActive(false);
    }

    public void OnTrackingFound()
    {
        if (lostRoutine != null)
        {
            StopCoroutine(lostRoutine);
            lostRoutine = null;
        }

        if (visuals != null) visuals.SetActive(true);

        if (!started)
        {
            started = true;
            if (streetLights != null) streetLights.ResetAll();
            if (tram != null) tram.Play();
        }
    }

    public void OnTrackingLost()
    {
        if (!started || lostRoutine != null) return;
        lostRoutine = StartCoroutine(LostRoutine());
    }

    IEnumerator LostRoutine()
    {
        yield return new WaitForSeconds(lostGraceSeconds);

        if (tram != null) tram.ResetToWall();
        if (streetLights != null) streetLights.ResetAll();
        if (visuals != null) visuals.SetActive(false);

        started = false;
        lostRoutine = null;
    }
}