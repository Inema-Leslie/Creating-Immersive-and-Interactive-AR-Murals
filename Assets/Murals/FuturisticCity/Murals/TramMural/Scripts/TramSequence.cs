using System.Collections;
using UnityEngine;

public class TramSequence : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Transform tram;
    [Tooltip("Where the tram ends up. One empty object, same parent as the tram.")]
    [SerializeField] Transform destination;
    [Tooltip("Optional quad with a clean-plate texture that covers the painted tram once the 3D tram has left.")]
    [SerializeField] GameObject wallPatch;
    [Tooltip("Optional roof arm. Must be a child of the tram.")]
    [SerializeField] Transform arm;
    [SerializeField] ParticleSystem sparks;
    [SerializeField] Light[] headlights;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clampClip;
    [SerializeField] AudioClip departClip;

    [Header("Movement")]
    [Tooltip("How big the tram becomes at the destination, compared to its painted size.")]
    [SerializeField] float endScaleMultiplier = 1.5f;
    [Tooltip("Sideways curve in the path. 0 = straight line. Positive curves to the right, negative to the left.")]
    [SerializeField] float sideCurve = 0f;
    [Tooltip("Gentle floating at the end. 0 = none.")]
    [SerializeField] float hoverHeight = 0.01f;
    [Tooltip("Off = the tram keeps its painted orientation and slides straight ahead, like a tram on rails. On = it turns to face its direction of travel.")]
    [SerializeField] bool faceTravelDirection = false;

    [Header("Timing")]
    [SerializeField] float shudderSeconds = 0.7f;
    [SerializeField] float headlightSeconds = 0.6f;
    [SerializeField] float armSeconds = 0.5f;
    [SerializeField] float leaveSeconds = 4f;

    [Header("Model orientation")]
    [Tooltip("Extra rotation if the model's front is not along +Z.")]
    [SerializeField] Vector3 modelRotationOffset = Vector3.zero;
    [Tooltip("Rotation applied to the arm to reach the wire.")]
    [SerializeField] Vector3 armClampEuler = new Vector3(-12f, 0f, 0f);

   
    Vector3 wallPos;
    Quaternion wallRot;
    Vector3 wallScale;
    Quaternion armRestRot;
    float[] headlightMax;
    Coroutine running;

    public Transform Tram => tram;

    void Awake()
    {
        wallPos = tram.localPosition;
        wallRot = tram.localRotation;
        wallScale = tram.localScale;

        if (arm != null) armRestRot = arm.localRotation;

        headlightMax = new float[headlights.Length];
        for (int i = 0; i < headlights.Length; i++)
            headlightMax[i] = headlights[i].intensity;

        ResetToWall();
    }

    public void Play()
    {
        ResetToWall();
        running = StartCoroutine(Run());
    }

    public void ResetToWall()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        tram.localPosition = wallPos;
        tram.localRotation = wallRot;
        tram.localScale = wallScale;

        if (arm != null) arm.localRotation = armRestRot;
        for (int i = 0; i < headlights.Length; i++) headlights[i].intensity = 0f;
        if (wallPatch != null) wallPatch.SetActive(false);
        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (audioSource != null) audioSource.Stop();
    }

    IEnumerator Run()
    {
        yield return Shudder();
        yield return HeadlightsOn();
        yield return ArmClamp();
        yield return Depart();
        yield return Hover();
    }

    IEnumerator Shudder()
    {
        float t = 0f;
        while (t < shudderSeconds)
        {
            t += Time.deltaTime;
            float strength = Mathf.Sin(t / shudderSeconds * Mathf.PI); 
            Vector3 jitter = Random.insideUnitSphere * 0.003f * strength;
            tram.localPosition = wallPos + jitter;
            yield return null;
        }
        tram.localPosition = wallPos;
    }

    IEnumerator HeadlightsOn()
    {
        float t = 0f;
        while (t < headlightSeconds)
        {
            t += Time.deltaTime;
            bool on = Random.value > 0.45f;
            for (int i = 0; i < headlights.Length; i++)
                headlights[i].intensity = on ? headlightMax[i] : 0f;
            yield return null;
        }
        for (int i = 0; i < headlights.Length; i++) headlights[i].intensity = headlightMax[i];
    }

    IEnumerator ArmClamp()
    {
        if (arm == null) yield break;

        Quaternion from = arm.localRotation;
        Quaternion to = armRestRot * Quaternion.Euler(armClampEuler);
        float t = 0f;
        while (t < armSeconds)
        {
            t += Time.deltaTime;
            arm.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / armSeconds));
            yield return null;
        }
        arm.localRotation = to;

        if (sparks != null) sparks.Play();
        PlayClip(clampClip);
    }

    IEnumerator Depart()
    {
        if (destination == null) yield break;

        if (wallPatch != null) wallPatch.SetActive(true);
        PlayClip(departClip);

        Vector3 startPos = wallPos;
        Vector3 endPos = destination.localPosition;
        Vector3 endScale = wallScale * endScaleMultiplier;

        Vector3 travel = endPos - startPos;
        if (travel.sqrMagnitude < 1e-8f) yield break;

        Quaternion startRot = tram.localRotation;
        Quaternion faceRot = faceTravelDirection
            ? Quaternion.LookRotation(travel.normalized, Vector3.up) * Quaternion.Euler(modelRotationOffset)
            : startRot;

        
        Vector3 side = Vector3.Cross(Vector3.up, travel.normalized).normalized;

        float t = 0f;
        while (t < leaveSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / leaveSeconds);

            Vector3 pos = Vector3.Lerp(startPos, endPos, k);
            pos += side * (sideCurve * Mathf.Sin(k * Mathf.PI)); 
            tram.localPosition = pos;

            
            tram.localRotation = Quaternion.Slerp(startRot, faceRot, Mathf.SmoothStep(0f, 1f, k * 2f));
            tram.localScale = Vector3.Lerp(wallScale, endScale, k);
            yield return null;
        }

        tram.localPosition = endPos;
        tram.localRotation = faceRot;
        tram.localScale = endScale;
    }

    IEnumerator Hover()
    {
        if (destination == null) yield break;

        Vector3 basePos = destination.localPosition;
        while (true)
        {
            tram.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 1.5f) * hoverHeight);
            yield return null;
        }
    }

    void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        if (destination == null || tram == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(tram.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.03f);
    }
}