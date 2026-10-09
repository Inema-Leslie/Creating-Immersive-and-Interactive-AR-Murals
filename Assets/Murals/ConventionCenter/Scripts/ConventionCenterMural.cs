using System.Collections;
using UnityEngine;

// Convention Center mural. The dome, tower and cups grow out of the wall in a pale day look,
// the bands light up green, then yellow, then blue, and a ribbon of light spirals around the dome
// while the painted plant grows and sways.
// Tap the dome to change the light style, tap the tower cups to switch day and night,
// and tap the plant to make it bloom.
public class ConventionCenterMural : MuralExperience
{
    private enum LightStyle { SpiralUp, SpiralDown, BandChase, SoftPulse }

    private static readonly string[] StyleNames = { "Spiral up", "Spiral down", "Band chase", "Soft pulse" };

    // Light-up order, bottom to top: green bands, yellow bands, blue bands. Band 0 is the top cap.
    private static readonly int[][] LightGroups =
    {
        new int[] { 6, 7 },
        new int[] { 4, 5 },
        new int[] { 0, 1, 2, 3 },
    };

    [Header("Dome")]
    [Tooltip("Top cap first, bottom band last.")]
    public Renderer[] bands;
    public Renderer backGlow;
    public ParticleSystem dust;
    public ParticleSystem domeSparkles;

    [Header("Tower")]
    public Transform tower;
    public Transform[] cups;

    [Header("Plant")]
    public Transform plantPivot;
    public Renderer plantArt;
    public ParticleSystem plantSparkles;

    [Header("Lights")]
    public Color[] ribbonColors =
    {
        new Color(0.13f, 0.38f, 0.24f),
        new Color(0.98f, 0.82f, 0f),
        new Color(0f, 0.63f, 0.87f),
    };
    public float ribbonSpeed = 0.25f;

    [Header("Audio")]
    public AudioClip ambienceLoop;
    public AudioClip bandClip;
    public AudioClip chimeClip;
    public AudioClip switchClip;
    public AudioClip bloomClip;

    private Vector3 towerScale;
    private Vector3[] cupScales;
    private LightStyle style;
    private bool introDone;
    private bool night;
    private bool switching;
    private bool plantGrown;
    private float lightTime;

    // Everything starts flat against the wall, unlit.
    private void Awake()
    {
        foreach (Renderer band in bands)
        {
            band.transform.localScale = new Vector3(1f, 0.01f, 1f);
            band.material.SetFloat("_Lit", 0f);
            band.material.SetFloat("_RibbonStrength", 0f);
            band.material.SetFloat("_Glow", 0f);
        }

        towerScale = tower.localScale;
        tower.localScale = new Vector3(towerScale.x, towerScale.y, 0.001f);

        cupScales = new Vector3[cups.Length];
        for (int i = 0; i < cups.Length; i++)
        {
            cupScales[i] = cups[i].localScale;
            cups[i].localScale = Vector3.zero;
            CupMaterial(i).SetFloat("_Lit", 0f);
        }

        plantArt.material.SetFloat("_Grow", 0f);
        SetAlpha(backGlow, 0f);
    }

    public override void PlayIntro()
    {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        StartAmbience();
        ShowCaption(0);

        // Stage 1: a soft glow appears behind the painted dome.
        StartCoroutine(FadeAlpha(backGlow, 0.6f, 1f));
        yield return Wait(1f);

        // Stage 2: the bands grow out of the wall one at a time, bottom to top, then the tower and cups.
        for (int i = bands.Length - 1; i >= 0; i--)
        {
            StartCoroutine(GrowBand(i));
            yield return Wait(0.45f);
        }
        yield return Wait(0.4f);
        yield return ScaleTo(tower, towerScale, 0.8f);
        for (int i = 0; i < cups.Length; i++)
        {
            StartCoroutine(ScaleTo(cups[i], cupScales[i], 0.3f));
            yield return Wait(0.25f);
        }
        yield return Wait(0.3f);

        // Stage 3: the lights come on, bottom to top.
        foreach (int[] group in LightGroups)
        {
            PlaySound(chimeClip);
            foreach (int index in group)
            {
                if (index < bands.Length)
                {
                    StartCoroutine(FadeFloat(bands[index].material, "_Lit", 1f, 0.6f));
                }
            }
            yield return Wait(0.8f);
        }
        for (int i = 0; i < cups.Length; i++)
        {
            StartCoroutine(FadeFloat(CupMaterial(i), "_Lit", 1f, 0.6f));
        }
        night = true;

        // Stage 4: the light show starts and the plant grows.
        introDone = true;
        domeSparkles.Play();
        StartCoroutine(GrowPlant());
        ShowCaption(1);
    }

    private IEnumerator GrowBand(int index)
    {
        PlaySound(bandClip);
        EmitDust(index);
        yield return ScaleTo(bands[index].transform, Vector3.one, 0.8f);
    }

    // A puff of dust along the bottom edge of the band.
    private void EmitDust(int index)
    {
        Bounds bandBounds = bands[index].GetComponent<MeshFilter>().sharedMesh.bounds;
        float domeX = bands[index].transform.localPosition.x;
        dust.transform.localPosition = new Vector3(domeX, 0.05f, bandBounds.min.z);
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.scale = new Vector3(bandBounds.size.x, 0.05f, 0.05f);
        dust.Emit(25);
    }

    private IEnumerator GrowPlant()
    {
        yield return FadeFloat(plantArt.material, "_Grow", 1f, 3f);
        plantGrown = true;
        plantSparkles.Play();
    }

    // The light show and the plant sway. Everything stops while tracking is lost.
    private void Update()
    {
        if (!introDone || IsPaused)
        {
            return;
        }

        lightTime += DeltaTime;
        plantPivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(lightTime * 0.8f) * 2f, 0f);

        if (night && !switching)
        {
            ApplyLightStyle();
        }
    }

    private void ApplyLightStyle()
    {
        Color ribbonColor = RibbonColor();
        int chaseBand = bands.Length - 1 - ((int)(lightTime * 4f) % bands.Length);

        for (int i = 0; i < bands.Length; i++)
        {
            Material material = bands[i].material;
            material.SetColor("_RibbonColor", ribbonColor);

            if (style == LightStyle.SpiralUp || style == LightStyle.SpiralDown)
            {
                float direction = style == LightStyle.SpiralUp ? 1f : -1f;
                material.SetFloat("_RibbonPhase", lightTime * ribbonSpeed * direction);
                material.SetFloat("_RibbonStrength", 1f);
                material.SetFloat("_Glow", 0f);
            }
            else if (style == LightStyle.BandChase)
            {
                material.SetFloat("_RibbonStrength", 0f);
                material.SetFloat("_Glow", i == chaseBand ? 0.9f : 0f);
            }
            else
            {
                material.SetFloat("_RibbonStrength", 0f);
                material.SetFloat("_Glow", 0.3f + 0.3f * Mathf.Sin(lightTime * 2f));
            }
        }
    }

    // Blends through the ribbon colours over time: green, yellow, blue.
    private Color RibbonColor()
    {
        float position = (lightTime * 0.3f) % ribbonColors.Length;
        int from = (int)position;
        int to = (from + 1) % ribbonColors.Length;
        return Color.Lerp(ribbonColors[from], ribbonColors[to], position - from);
    }

    public override void OnTapped(MuralTappable tappable)
    {
        if (!introDone || switching)
        {
            return;
        }

        if (tappable.id == "Dome")
        {
            ChangeLightStyle();
        }
        else if (tappable.id == "Cups")
        {
            StartCoroutine(SwitchDayNight(!night));
        }
        else if (tappable.id == "Plant" && plantGrown)
        {
            StartCoroutine(Bloom());
        }
    }

    // ---------- Interaction 1: tap the dome ----------

    private void ChangeLightStyle()
    {
        if (!night)
        {
            ShowCaption("Tap the tower cups to turn the lights on.");
            return;
        }

        style = (LightStyle)(((int)style + 1) % StyleNames.Length);
        PlaySound(chimeClip);
        ShowCaption(StyleNames[(int)style]);
    }

    // ---------- Interaction 2: tap the tower cups ----------

    private IEnumerator SwitchDayNight(bool toNight)
    {
        switching = true;
        PlaySound(switchClip);
        ShowCaption(toNight ? "Night: lights on" : "Day: lights off");

        float target = toNight ? 1f : 0f;
        foreach (Renderer band in bands)
        {
            band.material.SetFloat("_RibbonStrength", 0f);
            band.material.SetFloat("_Glow", 0f);
            StartCoroutine(FadeFloat(band.material, "_Lit", target, 1f));
        }
        for (int i = 0; i < cups.Length; i++)
        {
            StartCoroutine(FadeFloat(CupMaterial(i), "_Lit", target, 1f));
        }
        yield return Wait(1f);

        night = toNight;
        switching = false;
    }

    // ---------- Interaction 3: tap the plant ----------

    private IEnumerator Bloom()
    {
        PlaySound(bloomClip);
        plantSparkles.Emit(40);
        yield return FadeFloat(plantArt.material, "_Glow", 0.6f, 0.3f);
        yield return FadeFloat(plantArt.material, "_Glow", 0f, 0.8f);
    }

    // ---------- Helpers ----------

    private Material CupMaterial(int index)
    {
        return cups[index].GetComponent<Renderer>().material;
    }

    private void StartAmbience()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source != null && ambienceLoop != null)
        {
            source.clip = ambienceLoop;
            source.loop = true;
            source.Play();
        }
    }

    // Changes a material number over time. Stops while tracking is lost.
    private IEnumerator FadeFloat(Material material, string property, float to, float duration)
    {
        float from = material.GetFloat(property);
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            material.SetFloat(property, Mathf.Lerp(from, to, time / duration));
            yield return null;
        }
        material.SetFloat(property, to);
    }

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
        Color color = target.material.color;
        color.a = alpha;
        target.material.color = color;
    }
}
