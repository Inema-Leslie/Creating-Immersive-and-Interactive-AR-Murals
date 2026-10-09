using System.Collections;
using UnityEngine;

// Futuristic City mural. The tram powers up over the painted tram (shudder, flickering headlights),
// then slides out of the wall toward the viewer while the painted tram goes dark behind it.
// Street lamps flicker on as it passes them, and the story panel appears when it arrives.
// Tap the tram for the next run, tap the overhead wire for a power surge, tap a lamp to switch it.
public class FuturisticCityMural : MuralExperience
{
    [Header("Tram")]
    public Transform tram;
    [Tooltip("Bend point of the curved path, between the wall and the destination.")]
    public Transform pathBend;
    public Transform destination;
    public Renderer tramShadow;
    public Renderer[] headlightGlows;
    public Light headlight;
    [Tooltip("Size at the destination compared to the painted size.")]
    public float endScale = 1.5f;
    public float runSeconds = 6.5f;
    [Tooltip("How far the tram noses out of the wall before it sets off, in meters.")]
    public float peekDistance = 0.3f;
    [Tooltip("A lamp turns on when the tram comes this close to it, in meters.")]
    public float lampTriggerDistance = 0.6f;

    [Header("Street lamps")]
    [Tooltip("Lamp holders standing on the floor along the tram's path.")]
    public Transform[] lamps;
    public Renderer[] lampBodies;
    public Light[] lampLights;
    public Color lampColor = new Color(1f, 0.82f, 0.35f);
    public float lampEmission = 2.5f;
    public float lampLightIntensity = 1.2f;

    [Header("Overhead wire")]
    public Transform wireStart;
    public Transform wireEnd;
    public ParticleSystem sparks;

    [Header("Story panel")]
    public string storyTitle = "Futuristic City";
    [TextArea(3, 8)]
    public string storyText = "This mural shows a futuristic city. With vibrant colorful lights all over. a tram roaming the skies, we see the future materializing before our eyes. The train represents transportation and progress, connecting people, places, and opportunities. The surrounding buildings and sweeping colors capture the rhythm of urban life, where innovation meets everyday experiences. Together, these elements tell a story of a city moving forward, driven by connection, creativity, and the possibilities of the future.";

    [Header("Audio")]
    public AudioClip tramClip;
    public AudioClip bellClip;
    public AudioClip surgeClip;
    public AudioClip lampClip;

    private Vector3 startPosition;
    private Vector3 peekPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private float headlightIntensity;
    private bool[] lampOn;
    private bool running;
    private bool arrived;
    private bool surging;
    private float hoverTime;

    // The tram waits inside the wall with only its front showing; lamps and headlights start off.
    private void Awake()
    {
        startPosition = tram.localPosition;
        startRotation = tram.localRotation;
        startScale = tram.localScale;
        peekPosition = startPosition + startRotation * Vector3.forward * peekDistance;

        headlightIntensity = headlight != null ? headlight.intensity : 0f;
        SetHeadlights(0f);
        SetAlpha(tramShadow, 0f);

        lampOn = new bool[lampBodies.Length];
        for (int i = 0; i < lampBodies.Length; i++)
        {
            lampBodies[i].material.EnableKeyword("_EMISSION");
            SetLamp(i, 0f);
        }
    }

    public override void PlayIntro()
    {
        ShowCaption(0);
        StartCoroutine(TramRun(true));
    }

    // ---------- The tram run ----------

    private IEnumerator TramRun(bool firstRun)
    {
        running = true;
        arrived = false;
        HideInfo();

        // Noses slowly out of the painted windshield, then shudders and flashes its headlights.
        yield return MoveTo(tram, peekPosition, 1.5f);
        yield return Shudder(0.7f);
        yield return FlickerHeadlights(0.6f);

        PlaySound(tramClip);
        StartCoroutine(FadeAlpha(tramShadow, 1f, 0.8f));
        yield return Depart();

        arrived = true;
        running = false;
        hoverTime = 0f;
        ShowInfo(storyTitle, storyText);
        if (firstRun)
        {
            ShowCaption(1);
        }
    }

    private IEnumerator Shudder(float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            float strength = Mathf.Sin(time / duration * Mathf.PI);
            tram.localPosition = peekPosition + Random.insideUnitSphere * 0.006f * strength;
            yield return null;
        }
        tram.localPosition = peekPosition;
    }

    private IEnumerator FlickerHeadlights(float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            SetHeadlights(Random.value > 0.45f ? 1f : 0f);
            yield return Wait(0.05f);
        }
        SetHeadlights(1f);
    }

    // Out of the wall front first, then curving off to the side, slowly. The tram turns to follow the path.
    // Each lamp lights up when the tram comes near it.
    private IEnumerator Depart()
    {
        Vector3 fullScale = startScale * endScale;
        float time = 0f;
        while (time < runSeconds)
        {
            time += DeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / runSeconds));
            MoveAlongPath(t);
            tram.localScale = Vector3.Lerp(startScale, fullScale, t);
            LightPassedLamps();
            yield return null;
        }
        MoveAlongPath(1f);
        tram.localScale = fullScale;
    }

    // Curved path from the wall (t = 0) through the bend point to the destination (t = 1).
    private void MoveAlongPath(float t)
    {
        Vector3 a = peekPosition;
        Vector3 b = pathBend.localPosition;
        Vector3 c = destination.localPosition;
        float u = 1f - t;
        tram.localPosition = u * u * a + 2f * u * t * b + t * t * c;

        Vector3 direction = 2f * u * (b - a) + 2f * t * (c - b);
        direction.z = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            tram.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.forward);
        }
    }

    private void LightPassedLamps()
    {
        for (int i = 0; i < lampBodies.Length; i++)
        {
            Vector3 offset = lamps[i].localPosition - tram.localPosition;
            offset.z = 0f;
            if (!lampOn[i] && offset.magnitude <= lampTriggerDistance)
            {
                lampOn[i] = true;
                StartCoroutine(FlickerLamp(i, 1f));
            }
        }
    }

    // The tram floats gently once it has arrived.
    private void Update()
    {
        if (!arrived || IsPaused)
        {
            return;
        }
        hoverTime += DeltaTime;
        tram.localPosition = destination.localPosition + Vector3.forward * Mathf.Sin(hoverTime * 1.5f) * 0.01f;
    }

    public override void OnTapped(MuralTappable tappable)
    {
        if (!IntroPlayed)
        {
            return;
        }

        if (tappable.id == "Tram" && arrived)
        {
            StartCoroutine(NextStop());
        }
        else if (tappable.id == "Wire" && !surging)
        {
            StartCoroutine(PowerSurge());
        }
        else if (tappable.id == "Lamp")
        {
            ToggleLamp(tappable.index);
        }
    }

    // ---------- Interaction 1: tap the tram ----------

    // The bell rings, the tram slides back into the wall, the lamps go out, and it runs again.
    private IEnumerator NextStop()
    {
        arrived = false;
        running = true;
        HideInfo();
        PlaySound(bellClip);
        ShowCaption("Next stop");

        for (int i = 0; i < lampBodies.Length; i++)
        {
            lampOn[i] = false;
            StartCoroutine(FlickerLamp(i, 0f));
        }

        // Reverses back along its path into the wall.
        StartCoroutine(FadeAlpha(tramShadow, 0f, 3f));
        Vector3 fromScale = tram.localScale;
        float time = 0f;
        while (time < 3f)
        {
            time += DeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 3f));
            MoveAlongPath(1f - t);
            tram.localScale = Vector3.Lerp(fromScale, startScale, t);
            yield return null;
        }
        tram.localRotation = startRotation;
        yield return MoveTo(tram, startPosition, 1f);
        yield return TramRun(false);
    }

    // ---------- Interaction 2: tap the overhead wire ----------

    // Sparks run along the painted wire and every light flares for a moment.
    private IEnumerator PowerSurge()
    {
        surging = true;
        PlaySound(surgeClip);
        ShowCaption("Power surge");
        sparks.transform.localPosition = wireStart.localPosition;
        sparks.Play();

        float time = 0f;
        while (time < 1.2f)
        {
            time += DeltaTime;
            float t = Mathf.Clamp01(time / 1.2f);
            sparks.transform.localPosition = Vector3.Lerp(wireStart.localPosition, wireEnd.localPosition, t);
            float flare = 1f + Mathf.Sin(t * Mathf.PI);
            for (int i = 0; i < lampBodies.Length; i++)
            {
                if (lampOn[i])
                {
                    SetLamp(i, flare);
                }
            }
            if (running || arrived)
            {
                SetHeadlights(flare);
            }
            yield return null;
        }

        sparks.Stop();
        for (int i = 0; i < lampBodies.Length; i++)
        {
            SetLamp(i, lampOn[i] ? 1f : 0f);
        }
        if (running || arrived)
        {
            SetHeadlights(1f);
        }
        surging = false;
    }

    // ---------- Interaction 3: tap a lamp ----------

    private void ToggleLamp(int index)
    {
        if (index < 0 || index >= lampBodies.Length)
        {
            return;
        }
        lampOn[index] = !lampOn[index];
        PlaySound(lampClip);
        StartCoroutine(FlickerLamp(index, lampOn[index] ? 1f : 0f));
    }

    // ---------- Lights ----------

    private IEnumerator FlickerLamp(int index, float target)
    {
        float time = 0f;
        while (time < 0.5f)
        {
            time += DeltaTime;
            float ramp = time / 0.5f;
            float level = Random.value < (1f - ramp) * 0.5f ? 0.1f : Mathf.Lerp(1f - target, target, ramp);
            SetLamp(index, level);
            yield return null;
        }
        SetLamp(index, target);
    }

    private void SetLamp(int index, float level)
    {
        lampBodies[index].material.SetColor("_EmissionColor", lampColor * (lampEmission * level));
        if (index < lampLights.Length && lampLights[index] != null)
        {
            lampLights[index].color = lampColor;
            lampLights[index].intensity = lampLightIntensity * level;
        }
    }

    private void SetHeadlights(float level)
    {
        foreach (Renderer glow in headlightGlows)
        {
            SetAlpha(glow, Mathf.Clamp01(level));
        }
        if (headlight != null)
        {
            headlight.intensity = headlightIntensity * level;
        }
    }

    // ---------- Tracking loss ----------

    public override void OnTrackingLost()
    {
        base.OnTrackingLost();
        HideInfo();
    }

    public override void OnTrackingFound()
    {
        base.OnTrackingFound();
        if (arrived)
        {
            ShowInfo(storyTitle, storyText);
        }
    }

    // ---------- Helpers ----------

    private IEnumerator FadeAlpha(Renderer target, float to, float duration)
    {
        Material material = target.material;
        Color color = material.color;
        float from = color.a;
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            color.a = Mathf.Lerp(from, to, time / duration);
            material.color = color;
            yield return null;
        }
        color.a = to;
        material.color = color;
    }

    private void SetAlpha(Renderer target, float alpha)
    {
        if (target == null)
        {
            return;
        }
        Color color = target.material.color;
        color.a = alpha;
        target.material.color = color;
    }
}
