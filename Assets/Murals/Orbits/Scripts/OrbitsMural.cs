using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Orbits mural. The painted rings lift off the wall one by one and orbit the head like a gyroscope,
// the bangles lift off the wrists and spin, the painted dots float out as bubbles, and the two painted
// cubes rise out of the wall. Tap the head to spin the rings faster, tap the raised hand for an orb,
// tap a bubble to pop it, and tap the cube in the lower hand to open it and send its energy up to the rings.
// With the cube open and the orb orbiting, everything falls into line for the finale.
public class OrbitsMural : MuralExperience
{
    [Header("Rings (top first)")]
    public Transform[] rings;
    [Tooltip("How far the centre of each ring sits in front of the wall. 0 centres it on the painted head; the wall hides the back half.")]
    public float ringDepth = 0f;

    [Header("Bangles")]
    public Transform[] bangles;

    [Header("Bubbles")]
    public Transform[] bubbles;

    [Header("Orb")]
    public Transform orb;
    public Transform palm;
    public Transform orbitCenter;
    public float orbitRadius = 1.1f;
    public ParticleSystem orbGlow;

    [Header("Power cube (lower left hand)")]
    public Transform powerCube;
    public Transform[] cubeFaces;
    public Transform cubeCore;
    public ParticleSystem coreGlow;
    public ParticleSystem energyBeam;
    [Tooltip("How far the cube floats out of the wall.")]
    public float cubeLift = 0.35f;

    [Header("Small cube (raised hand)")]
    public Transform smallCube;
    public float smallCubeOrbit = 0.25f;

    [Header("Effects")]
    public ParticleSystem sparkles;
    public Color ringChargeColor = new Color(1.4f, 1.3f, 1.05f);

    [Header("Audio")]
    public AudioClip ambienceLoop;
    public AudioClip liftClip;
    public AudioClip chimeClip;
    public AudioClip spinClip;
    public AudioClip popClip;
    public AudioClip orbClip;
    public AudioClip cubeClip;
    public AudioClip chargeClip;
    public AudioClip finaleClip;

    private Vector3[] ringWallPosition;
    private Vector3[] ringWallScale;
    private Vector3[] ringOrbitPosition;
    private float[] ringSpin;
    private float[] ringSpeed;
    private float[] ringTilt;
    private bool[] ringLive;

    private Vector3[] bangleWallPosition;
    private Quaternion[] bangleArmRotation;
    private float[] bangleSpin;
    private bool[] bangleLive;

    private Vector3[] bubbleWallPosition;
    private Vector3[] bubbleFullScale;
    private Vector3[] bubbleFloatPosition;
    private bool[] bubbleLive;

    private Vector3 orbScale;
    private int orbState;
    private bool orbMoving;
    private float orbAngle;

    private Vector3 cubeHome;
    private Quaternion cubeTilt;
    private Vector3[] faceClosed;
    private Vector3 coreScale;
    private bool cubeOut;
    private bool cubeOpen;
    private bool cubeBusy;
    private Vector3 smallHome;
    private Quaternion smallTilt;
    private bool smallOut;

    private float[] ringCharge;
    private float align;
    private bool finaleDone;

    private readonly Dictionary<AudioClip, AudioSource> trimmedSources = new Dictionary<AudioClip, AudioSource>();
    private readonly Dictionary<AudioSource, Coroutine> trimmedFades = new Dictionary<AudioSource, Coroutine>();

    private bool introDone;
    private float time;
    private float energy;

    // Everything starts flat on the wall, exactly over the paint.
    private void Awake()
    {
        int count = rings.Length;
        ringWallPosition = new Vector3[count];
        ringWallScale = new Vector3[count];
        ringOrbitPosition = new Vector3[count];
        ringSpin = new float[count];
        ringSpeed = new float[count];
        ringTilt = new float[count];
        ringLive = new bool[count];
        for (int i = 0; i < count; i++)
        {
            ringWallPosition[i] = rings[i].localPosition;
            ringWallScale[i] = rings[i].localScale;
            ringOrbitPosition[i] = new Vector3(ringWallPosition[i].x, ringDepth, ringWallPosition[i].z);
            ringSpeed[i] = (i % 2 == 0 ? 1f : -1f) * (25f + 5f * i);
            ringTilt[i] = 8f + 2f * i;
        }

        // Each bangle already sits around its painted forearm (its axis runs along the arm).
        count = bangles.Length;
        bangleWallPosition = new Vector3[count];
        bangleArmRotation = new Quaternion[count];
        bangleSpin = new float[count];
        bangleLive = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bangleWallPosition[i] = bangles[i].localPosition;
            bangleArmRotation[i] = bangles[i].localRotation;
        }

        count = bubbles.Length;
        bubbleWallPosition = new Vector3[count];
        bubbleFullScale = new Vector3[count];
        bubbleFloatPosition = new Vector3[count];
        bubbleLive = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bubbleWallPosition[i] = bubbles[i].localPosition;
            bubbleFullScale[i] = Vector3.one * bubbles[i].localScale.x;
            bubbleFloatPosition[i] = bubbleWallPosition[i] + new Vector3(0f, 0.3f + 0.1f * (i % 6), 0f);
            bubbles[i].localScale = FlatOnWall(bubbleFullScale[i]);
        }

        orbScale = orb.localScale;
        orb.localScale = Vector3.zero;
        orb.localPosition = palm.localPosition;

        ringCharge = new float[rings.Length];

        // The cubes wait behind the wall; the invisible wall hides them until they rise out.
        if (powerCube == null || smallCube == null)
        {
            Debug.LogWarning("OrbitsMural: cubes are missing. Rebuild the Orbits prefab.");
            return;
        }
        cubeHome = new Vector3(powerCube.localPosition.x, cubeLift, powerCube.localPosition.z);
        cubeTilt = powerCube.localRotation;
        faceClosed = new Vector3[cubeFaces.Length];
        for (int i = 0; i < cubeFaces.Length; i++)
        {
            faceClosed[i] = cubeFaces[i].localPosition;
        }
        coreScale = cubeCore.localScale;
        cubeCore.localScale = Vector3.zero;

        smallHome = new Vector3(smallCube.localPosition.x, 0.25f, smallCube.localPosition.z);
        smallTilt = smallCube.localRotation;
    }

    public override void PlayIntro()
    {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        StartAmbience();
        ShowCaption(0);
        yield return Wait(0.6f);

        // Stage 1: the rings lift off the wall one by one, bottom to top, and start to orbit.
        for (int i = rings.Length - 1; i >= 0; i--)
        {
            PlaySound(liftClip);
            StartCoroutine(LiftRing(i));
            yield return Wait(0.5f);
        }
        yield return Wait(0.8f);

        // Stage 2: the bangles lift off the wrists and spin.
        for (int i = 0; i < bangles.Length; i++)
        {
            StartCoroutine(LiftBangle(i));
            yield return Wait(0.15f);
        }
        yield return Wait(0.6f);

        // Stage 3: the painted dots float out as bubbles.
        for (int i = 0; i < bubbles.Length; i++)
        {
            StartCoroutine(FloatBubble(i));
            yield return Wait(0.1f);
        }

        // Stage 4: the two painted cubes rise out of the wall.
        if (powerCube != null && smallCube != null)
        {
            StartCoroutine(RiseCube(powerCube, cubeHome, () => cubeOut = true));
            yield return Wait(0.4f);
            StartCoroutine(RiseCube(smallCube, smallHome, () => smallOut = true));
            yield return Wait(1f);
        }

        // A hint of sparkle in the raised hand.
        BurstAt(palm.localPosition, 15);
        PlaySound(chimeClip);
        introDone = true;
        ShowCaption(1);
    }

    // The flat painted ring swings round to lie level, circling the painted head through the wall.
    private IEnumerator LiftRing(int i)
    {
        Transform ring = rings[i];
        Quaternion startRotation = ring.localRotation;
        Quaternion endRotation = Quaternion.AngleAxis(ringTilt[i], Vector3.right) * Quaternion.Euler(90f, 0f, 0f);
        Vector3 startScale = ring.localScale;
        float t = 0f;
        while (t < 1.2f)
        {
            t += DeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.2f));
            ring.localPosition = Vector3.Lerp(ringWallPosition[i], ringOrbitPosition[i], k);
            ring.localRotation = Quaternion.Slerp(startRotation, endRotation, k);
            ring.localScale = Vector3.Lerp(startScale, Vector3.one, k);
            yield return null;
        }
        ringLive[i] = true;
    }

    // The bangle gives a small jolt, then starts spinning around the arm.
    private IEnumerator LiftBangle(int i)
    {
        yield return ScaleTo(bangles[i], Vector3.one * 1.15f, 0.2f);
        yield return ScaleTo(bangles[i], Vector3.one, 0.3f);
        bangleLive[i] = true;
    }

    // The flat painted dot puffs up into a ball and drifts out of the wall.
    private IEnumerator FloatBubble(int i)
    {
        Transform bubble = bubbles[i];
        bubble.localPosition = bubbleWallPosition[i];
        StartCoroutine(ScaleTo(bubble, bubbleFullScale[i], 0.5f));
        yield return MoveTo(bubble, bubbleFloatPosition[i], 1.5f);
        bubbleLive[i] = true;
    }

    private IEnumerator RiseCube(Transform cube, Vector3 home, System.Action done)
    {
        PlaySound(liftClip);
        yield return MoveTo(cube, home, 1.2f);
        done();
    }

    // Orbiting rings, spinning bangles, bobbing bubbles, turning cubes. Everything stops while tracking is lost.
    private void Update()
    {
        if (IsPaused)
        {
            return;
        }

        time += DeltaTime;
        float speedUp = 1f + 3f * energy;
        float spread = 1f + 0.25f * energy;

        for (int i = 0; i < rings.Length; i++)
        {
            if (!ringLive[i])
            {
                continue;
            }
            float speed = Mathf.Lerp(ringSpeed[i], Mathf.Abs(ringSpeed[i]), align);
            ringSpin[i] += speed * speedUp * DeltaTime;
            float wobble = ringTilt[i] * Mathf.Sin(time * 0.6f + i) * (1f - align);
            rings[i].localRotation = Quaternion.AngleAxis(ringSpin[i], Vector3.forward)
                * Quaternion.AngleAxis(wobble, Vector3.right) * Quaternion.Euler(90f, 0f, 0f);
            rings[i].localScale = Vector3.one * spread;
            rings[i].GetComponent<Renderer>().material.color = Color.Lerp(Color.white, ringChargeColor, ringCharge[i]);
        }

        for (int i = 0; i < bangles.Length; i++)
        {
            if (bangleLive[i])
            {
                // Spins around the forearm and slides a little up and down it.
                bangleSpin[i] += 90f * speedUp * DeltaTime;
                bangles[i].localRotation = bangleArmRotation[i] * Quaternion.AngleAxis(bangleSpin[i], Vector3.up);
                Vector3 alongArm = bangleArmRotation[i] * Vector3.up;
                bangles[i].localPosition = bangleWallPosition[i] + alongArm * (Mathf.Sin(time * 1.2f + i) * 0.03f);
            }
        }

        for (int i = 0; i < bubbles.Length; i++)
        {
            if (bubbleLive[i])
            {
                float bob = Mathf.Sin(time * 1.3f + i * 0.9f) * 0.04f;
                bubbles[i].localPosition = bubbleFloatPosition[i] + new Vector3(0f, 0f, bob);
            }
        }

        if (orbState == 2 && !orbMoving)
        {
            orbAngle += 40f * speedUp * DeltaTime;
            orb.localPosition = OrbitPoint(orbAngle);
        }

        // The power cube turns slowly, keeping its painted tilt; its core spins the other way.
        if (cubeOut)
        {
            powerCube.localRotation = Quaternion.AngleAxis(time * 20f, Vector3.forward) * cubeTilt;
            cubeCore.localRotation = Quaternion.Euler(time * -60f, time * 45f, 0f);
        }

        // The small cube circles the raised hand.
        if (smallOut)
        {
            float angle = time * 50f * Mathf.Deg2Rad;
            smallCube.localPosition = smallHome + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * smallCubeOrbit;
            smallCube.localRotation = Quaternion.AngleAxis(time * 90f, Vector3.forward) * smallTilt;
        }
    }

    public override void OnTapped(MuralTappable tappable)
    {
        if (!introDone)
        {
            return;
        }

        if (tappable.id == "Head")
        {
            StartCoroutine(Energize());
        }
        else if (tappable.id == "Hand" && !orbMoving)
        {
            StartCoroutine(UseOrb());
        }
        else if (tappable.id == "Bubble" && bubbleLive[tappable.index])
        {
            StartCoroutine(PopBubble(tappable.index));
        }
        else if (tappable.id == "Cube" && powerCube != null && cubeOut && !cubeBusy)
        {
            StartCoroutine(cubeOpen ? CloseCube() : OpenCube());
        }
    }

    // ---------- Interaction 1: tap the head ----------

    // The rings spread out and spin much faster for a moment, then settle back.
    private IEnumerator Energize()
    {
        PlayTrimmed(spinClip, 2.5f);
        BurstAt(orbitCenter.localPosition, 30);
        float t = 0f;
        while (t < 0.4f)
        {
            t += DeltaTime;
            energy = Mathf.Max(energy, Mathf.Clamp01(t / 0.4f));
            yield return null;
        }
        yield return Wait(1.5f);
        t = 0f;
        while (t < 1.5f)
        {
            t += DeltaTime;
            energy = 1f - Mathf.Clamp01(t / 1.5f);
            yield return null;
        }
        energy = 0f;
    }

    // ---------- Interaction 2: tap the raised hand ----------

    // First tap: an orb forms in the palm. Second tap: it flies up and orbits with the rings.
    // Third tap: it comes back to the hand.
    private IEnumerator UseOrb()
    {
        orbMoving = true;
        PlayTrimmed(orbClip, 2.5f);

        if (orbState == 0)
        {
            ShowCaption("An orb forms in the hand.");
            orbGlow.Play();
            BurstAt(palm.localPosition, 20);
            yield return ScaleTo(orb, orbScale, 0.8f);
            orbState = 1;
        }
        else if (orbState == 1)
        {
            ShowCaption("The orb joins the rings.");
            orbAngle = 0f;
            yield return MoveTo(orb, OrbitPoint(orbAngle), 1.2f);
            orbState = 2;
            CheckFinale();
        }
        else
        {
            ShowCaption("The orb returns to the hand.");
            yield return MoveTo(orb, palm.localPosition, 1.2f);
            orbState = 1;
        }
        orbMoving = false;
    }

    private Vector3 OrbitPoint(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return orbitCenter.localPosition + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * orbitRadius;
    }

    // ---------- Interaction 3: tap a bubble ----------

    // The bubble pops into sparkles, then a new one puffs out of the painted dot a few seconds later.
    private IEnumerator PopBubble(int i)
    {
        bubbleLive[i] = false;
        PlaySound(popClip);
        BurstAt(bubbles[i].localPosition, 25);
        yield return ScaleTo(bubbles[i], Vector3.zero, 0.15f);
        yield return Wait(4f);
        bubbles[i].localPosition = bubbleWallPosition[i];
        bubbles[i].localScale = FlatOnWall(bubbleFullScale[i]);
        yield return Wait(0.3f);
        yield return FloatBubble(i);
    }

    // ---------- Interaction 4: tap the power cube ----------

    // The panels spring apart, the core lights up, and its energy climbs to the rings,
    // charging them one by one from the bottom.
    private IEnumerator OpenCube()
    {
        cubeBusy = true;
        PlayTrimmed(cubeClip, 1.5f);
        ShowCaption("The cube opens.");
        for (int i = 0; i < cubeFaces.Length; i++)
        {
            StartCoroutine(MoveTo(cubeFaces[i], faceClosed[i] * 1.8f, 0.6f));
        }
        coreGlow.Play();
        yield return ScaleTo(cubeCore, coreScale, 0.6f);
        cubeOpen = true;

        // Energy travels from the cube up to the rings.
        PlaySound(chargeClip);
        energyBeam.transform.localPosition = powerCube.localPosition;
        energyBeam.Play();
        yield return MoveTo(energyBeam.transform, orbitCenter.localPosition, 1f);
        energyBeam.Stop();

        for (int i = rings.Length - 1; i >= 0; i--)
        {
            StartCoroutine(ChargeRing(i));
            yield return Wait(0.15f);
        }
        StartCoroutine(Energize());
        cubeBusy = false;
        CheckFinale();
    }

    private IEnumerator CloseCube()
    {
        cubeBusy = true;
        PlayTrimmed(cubeClip, 1.5f);
        for (int i = 0; i < cubeFaces.Length; i++)
        {
            StartCoroutine(MoveTo(cubeFaces[i], faceClosed[i], 0.6f));
        }
        coreGlow.Stop();
        yield return ScaleTo(cubeCore, Vector3.zero, 0.6f);
        for (int i = 0; i < rings.Length; i++)
        {
            ringCharge[i] = 0f;
        }
        cubeOpen = false;
        cubeBusy = false;
    }

    // A bright flash that settles to a soft glow while the cube stays open.
    private IEnumerator ChargeRing(int i)
    {
        float t = 0f;
        while (t < 0.25f)
        {
            t += DeltaTime;
            ringCharge[i] = Mathf.Clamp01(t / 0.25f);
            yield return null;
        }
        yield return Wait(0.3f);
        t = 0f;
        while (t < 0.6f)
        {
            t += DeltaTime;
            ringCharge[i] = Mathf.Lerp(1f, 0.35f, t / 0.6f);
            yield return null;
        }
    }

    // ---------- Finale ----------

    // With the cube open and the orb orbiting, the rings straighten into one stack and pulse together.
    private void CheckFinale()
    {
        if (!finaleDone && cubeOpen && orbState == 2)
        {
            StartCoroutine(Finale());
        }
    }

    private IEnumerator Finale()
    {
        finaleDone = true;
        PlaySound(finaleClip);
        ShowCaption("Everything is in orbit.");

        float t = 0f;
        while (t < 2f)
        {
            t += DeltaTime;
            align = Mathf.SmoothStep(0f, 1f, t / 2f);
            yield return null;
        }

        for (int pulse = 0; pulse < 3; pulse++)
        {
            BurstAt(orbitCenter.localPosition, 30);
            t = 0f;
            while (t < 0.8f)
            {
                t += DeltaTime;
                float glow = Mathf.Sin(t / 0.8f * Mathf.PI);
                for (int i = 0; i < rings.Length; i++)
                {
                    ringCharge[i] = Mathf.Max(0.35f, glow);
                }
                yield return null;
            }
        }
        yield return Wait(3f);

        t = 0f;
        while (t < 2f)
        {
            t += DeltaTime;
            align = 1f - Mathf.SmoothStep(0f, 1f, t / 2f);
            yield return null;
        }
        align = 0f;
    }

    // ---------- Helpers ----------

    // Plays a sound for at most the given seconds, then fades it out.
    // Playing it again restarts it instead of stacking a second copy.
    private void PlayTrimmed(AudioClip clip, float seconds)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source;
        if (!trimmedSources.TryGetValue(clip, out source))
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            trimmedSources[clip] = source;
        }

        Coroutine running;
        if (trimmedFades.TryGetValue(source, out running) && running != null)
        {
            StopCoroutine(running);
        }
        source.volume = 1f;
        source.Play();
        trimmedFades[source] = StartCoroutine(FadeOutAfter(source, seconds));
    }

    private IEnumerator FadeOutAfter(AudioSource source, float seconds)
    {
        yield return Wait(seconds);
        float t = 0f;
        while (t < 0.3f)
        {
            t += DeltaTime;
            source.volume = 1f - t / 0.3f;
            yield return null;
        }
        source.Stop();
    }

    // A soft background hum that loops for as long as the mural is tracked.
    private void StartAmbience()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source != null && ambienceLoop != null)
        {
            source.clip = ambienceLoop;
            source.loop = true;
            source.volume = 0.5f;
            source.Play();
        }
    }

    private void BurstAt(Vector3 localPosition, int count)
    {
        sparkles.transform.localPosition = localPosition;
        sparkles.Emit(count);
    }

    // A ball squashed flat against the wall (thin along Y) looks like the painted dot.
    private static Vector3 FlatOnWall(Vector3 fullScale)
    {
        return new Vector3(fullScale.x, fullScale.y * 0.05f, fullScale.z);
    }
}
