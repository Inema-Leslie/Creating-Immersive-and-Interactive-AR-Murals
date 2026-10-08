using System.Collections;
using UnityEngine;
using TMPro;

// Leaders mural. The gold veins light up, the mask's eyes open, and the five portraits
// lift out of the wall one by one. Tap a portrait to bring it forward with its story.
// Tap the mask to wake all portraits at once. Viewing all five plays a closing moment.
public class LeadersMural : MuralExperience
{
    [System.Serializable]
    public class Portrait
    {
        public string title = "Ask guest relations";

        [TextArea(2, 6)]
        public string story = "Ask guest relations";

        public AudioClip narration;

        [Header("Parts (set by the builder)")]
        public Transform lift;
        public Transform floater;
        public Renderer figure;
        public Renderer glow;
        public Renderer shadow;
        public ParticleSystem dust;

        [HideInInspector] public bool viewed;
        [HideInInspector] public Vector3 glowScale;
    }

    [Header("Portraits")]
    public Portrait[] portraits;

    [Header("Mural parts")]
    public Renderer veins;
    public Transform[] maskEyes;
    public ParticleSystem ambientDust;

    [Header("Extra 3D content (edit the LeadersExtras prefab)")]
    public Transform extras;

    [Header("Guide character")]
    public Transform guide;
    public Animator guideAnimator;
    public AudioClip[] guideLines;

    [TextArea(2, 4)]
    public string[] guideCaptions;

    [Tooltip("Seconds of talking animation when a line has no audio clip yet.")]
    public float silentTalkSeconds = 3f;

    [Header("River")]
    public Renderer waterfallSheet;
    public Renderer streamWater;
    public ParticleSystem splash;
    public ParticleSystem fireflies;
    public Transform[] reeds;
    public float flowSpeed = 0.35f;

    [Header("Info card")]
    public GameObject infoCard;
    public TMP_Text cardTitle;
    public float labelGap = 0.12f;

    [Header("Motion")]
    public float restHeight = 0.12f;
    public float selectedHeight = 0.45f;
    public float selectedScale = 1.4f;
    public float dimAlpha = 0.45f;

    [Header("Audio")]
    public AudioSource voiceSource;
    public AudioClip ambientLoop;
    public AudioClip riseClip;
    public AudioClip selectClip;
    public AudioClip maskClip;
    public AudioClip completeClip;

    private Vector3[] eyeScales;
    private Vector3 extrasScale;
    private Vector3 guideScale;
    private Quaternion guideFacing;
    private bool guideArrived;
    private int nextGuideLine;
    private Vector3[] reedScales;
    private Quaternion[] reedRotations;
    private float flowOffset;
    private float talkTimer;
    private int selected = -1;
    private bool introDone;
    private bool busy;
    private bool completed;
    private float idleTime;

    // Everything starts hidden, sitting flat on the painted mural.
    private void Awake()
    {
        eyeScales = new Vector3[maskEyes.Length];
        for (int i = 0; i < maskEyes.Length; i++)
        {
            eyeScales[i] = maskEyes[i].localScale;
            maskEyes[i].localScale = Vector3.zero;
        }

        foreach (Portrait p in portraits)
        {
            p.glowScale = p.glow.transform.localScale;
            p.glow.transform.localScale = Vector3.zero;
            p.lift.localPosition = Vector3.zero;
            SetAlpha(p.figure, 0f);
            SetAlpha(p.shadow, 0f);
        }

        SetAlpha(veins, 0f);
        infoCard.SetActive(false);

        if (extras != null)
        {
            extrasScale = extras.localScale;
            extras.localScale = Vector3.zero;
        }

        if (streamWater != null)
        {
            waterfallSheet.material.SetFloat("_Reveal", 0f);
            streamWater.material.SetFloat("_Reveal", 0f);
            reedScales = new Vector3[reeds.Length];
            reedRotations = new Quaternion[reeds.Length];
            for (int i = 0; i < reeds.Length; i++)
            {
                reedScales[i] = reeds[i].localScale;
                reedRotations[i] = reeds[i].localRotation;
                reeds[i].localScale = Vector3.zero;
            }
        }

        if (guide != null)
        {
            guideScale = guide.localScale;
            guideFacing = guide.localRotation;
            guide.localScale = Vector3.zero;
        }
    }

    public override void PlayIntro()
    {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        StartAmbient();
        ShowCaption(0);

        // Stage 1: the gold veins glow.
        StartCoroutine(FadeRenderer(veins, 0.9f, 1.5f));
        yield return Wait(1f);

        // Stage 2: the mask's eyes open.
        for (int i = 0; i < maskEyes.Length; i++)
        {
            StartCoroutine(ScaleTo(maskEyes[i], eyeScales[i], 0.6f));
        }
        yield return Wait(0.8f);

        // Stage 3: the portraits lift out of the wall one at a time.
        foreach (Portrait p in portraits)
        {
            StartCoroutine(RisePortrait(p));
            yield return Wait(0.7f);
        }
        yield return Wait(1f);

        if (ambientDust != null)
        {
            ambientDust.Play();
        }
        if (extras != null)
        {
            StartCoroutine(ScaleTo(extras, extrasScale, 1f));
        }
        if (streamWater != null)
        {
            StartCoroutine(RiverFlows());
        }
        if (guide != null)
        {
            StartCoroutine(GuideArrives());
        }
        ShowCaption(1);
        introDone = true;
    }

    // The cut-out appears exactly over the painted portrait, then rises along +Y.
    private IEnumerator RisePortrait(Portrait p)
    {
        PlaySound(riseClip);
        yield return FadeRenderer(p.figure, 1f, 0.3f);

        if (p.dust != null)
        {
            p.dust.Play();
        }
        StartCoroutine(FadeRenderer(p.shadow, 1f, 0.8f));
        StartCoroutine(ScaleTo(p.glow.transform, p.glowScale, 0.8f));
        yield return MoveTo(p.lift, new Vector3(0f, restHeight, 0f), 0.8f);
    }

    // Gentle floating and a slow pulse in the veins once the intro has finished.
    private void Update()
    {
        if (!introDone || IsPaused)
        {
            return;
        }

        idleTime += DeltaTime;

        for (int i = 0; i < portraits.Length; i++)
        {
            float bob = Mathf.Sin(idleTime * 1.2f + i) * 0.015f;
            portraits[i].floater.localPosition = new Vector3(0f, bob, 0f);
        }

        if (!busy && !completed)
        {
            SetAlpha(veins, 0.65f + Mathf.Sin(idleTime * 1.5f) * 0.25f);
        }

        // The stream flows toward the viewer and the reeds sway.
        if (streamWater != null)
        {
            flowOffset += DeltaTime * flowSpeed;
            streamWater.material.SetFloat("_FlowOffset", flowOffset);
            waterfallSheet.material.SetFloat("_FlowOffset", flowOffset * 3f);
            for (int i = 0; i < reeds.Length; i++)
            {
                float swayX = Mathf.Sin(idleTime * 1.3f + i) * 5f;
                float swayZ = Mathf.Sin(idleTime * 0.9f + i * 1.7f) * 3f;
                reeds[i].localRotation = reedRotations[i] * Quaternion.Euler(swayX, 0f, swayZ);
            }
        }

        // The guide talks whenever a voice line is playing, and stands calmly otherwise.
        if (talkTimer > 0f)
        {
            talkTimer -= DeltaTime;
        }
        if (guideAnimator != null)
        {
            bool talking = talkTimer > 0f || (voiceSource != null && voiceSource.isPlaying);
            guideAnimator.SetBool("Talking", talking);
        }
    }

    public override void OnTapped(MuralTappable tappable)
    {
        if (!introDone || busy)
        {
            return;
        }

        if (tappable.id == "Mask")
        {
            StartCoroutine(MaskSequence());
        }
        else if (tappable.id == "Portrait")
        {
            SelectPortrait(tappable.index);
        }
        else if (tappable.id == "Guide")
        {
            GuideSay();
        }
    }

    // ---------- Interaction 1: tap a portrait ----------

    private void SelectPortrait(int index)
    {
        if (selected == index)
        {
            StartCoroutine(Lower(index));
            selected = -1;
            infoCard.SetActive(false);
            HideInfo();
            SetDimmed(false);
            TurnGuide(guideFacing);
            return;
        }

        if (selected >= 0)
        {
            StartCoroutine(Lower(selected));
        }

        selected = index;
        StartCoroutine(Raise(index));
        SetDimmed(true);
        ShowCard(index);

        Portrait p = portraits[index];
        PlaySound(selectClip);
        PlayVoice(p.narration);
        if (p.narration == null)
        {
            talkTimer = silentTalkSeconds;
        }
        ShowCaption(p.title);
        GuidePointAt(index);

        p.viewed = true;
        CheckComplete();
    }

    private IEnumerator Raise(int index)
    {
        Portrait p = portraits[index];
        StartCoroutine(ScaleTo(p.lift, Vector3.one * selectedScale, 0.6f));
        yield return MoveTo(p.lift, new Vector3(0f, selectedHeight, 0f), 0.6f);
    }

    private IEnumerator Lower(int index)
    {
        Portrait p = portraits[index];
        StartCoroutine(ScaleTo(p.lift, Vector3.one, 0.5f));
        yield return MoveTo(p.lift, new Vector3(0f, restHeight, 0f), 0.5f);
    }

    // Dims every portrait except the selected one, or restores them all.
    private void SetDimmed(bool dimmed)
    {
        for (int i = 0; i < portraits.Length; i++)
        {
            float target = (dimmed && i != selected) ? dimAlpha : 1f;
            StartCoroutine(FadeRenderer(portraits[i].figure, target, 0.4f));
        }
    }

    // A large name label floats above the selected portrait. The story goes to the on-screen panel.
    private void ShowCard(int index)
    {
        Portrait p = portraits[index];
        Vector3 portraitPosition = p.lift.parent.localPosition;
        float halfHeight = p.figure.transform.localScale.y * selectedScale / 2f;
        infoCard.transform.localPosition = new Vector3(portraitPosition.x, selectedHeight + 0.02f, portraitPosition.z + halfHeight + labelGap);
        cardTitle.text = p.title;
        infoCard.SetActive(true);
        ShowInfo(p.title, p.story);
    }

    // ---------- Interaction 2: tap the mask ----------

    private IEnumerator MaskSequence()
    {
        busy = true;
        PlaySound(maskClip);
        ShowCaption(2);

        for (int i = 0; i < maskEyes.Length; i++)
        {
            StartCoroutine(Pulse(maskEyes[i], eyeScales[i]));
        }
        yield return FadeRenderer(veins, 1f, 0.4f);

        // A wave runs through the portraits, one after another.
        for (int i = 0; i < portraits.Length; i++)
        {
            StartCoroutine(Hop(i));
            yield return Wait(0.15f);
        }
        yield return Wait(0.6f);
        busy = false;
    }

    private IEnumerator Pulse(Transform target, Vector3 normalScale)
    {
        yield return ScaleTo(target, normalScale * 1.5f, 0.25f);
        yield return ScaleTo(target, normalScale, 0.35f);
    }

    private IEnumerator Hop(int index)
    {
        Portrait p = portraits[index];
        float height = (index == selected) ? selectedHeight : restHeight;
        if (p.dust != null)
        {
            p.dust.Play();
        }
        yield return MoveTo(p.lift, new Vector3(0f, height + 0.08f, 0f), 0.2f);
        yield return MoveTo(p.lift, new Vector3(0f, height, 0f), 0.3f);
    }

    // ---------- Closing moment: all five portraits viewed ----------

    private void CheckComplete()
    {
        if (completed)
        {
            return;
        }

        foreach (Portrait p in portraits)
        {
            if (!p.viewed)
            {
                return;
            }
        }

        completed = true;
        StartCoroutine(CompleteSequence());
    }

    private IEnumerator CompleteSequence()
    {
        yield return Wait(1.5f);
        PlaySound(completeClip);
        ShowCaption(3);
        StartCoroutine(FadeRenderer(veins, 1f, 1f));

        foreach (Portrait p in portraits)
        {
            StartCoroutine(ScaleTo(p.glow.transform, p.glowScale * 1.3f, 1f));
        }

        if (ambientDust != null)
        {
            ambientDust.Emit(60);
        }
        if (guideAnimator != null)
        {
            guideAnimator.SetTrigger("Wave");
        }
    }

    // ---------- River ----------

    // The painted river spills out of the wall onto the floor, then reeds grow along its banks.
    private IEnumerator RiverFlows()
    {
        yield return RevealWater(waterfallSheet, 0.6f);
        if (splash != null)
        {
            splash.Play();
        }

        StartCoroutine(RevealWater(streamWater, 2f));
        for (int i = 0; i < reeds.Length; i++)
        {
            StartCoroutine(ScaleTo(reeds[i], reedScales[i], 0.6f));
            yield return Wait(0.15f);
        }

        if (fireflies != null)
        {
            fireflies.Play();
        }
    }

    // Uncovers the water from the start of its shape to the end. Stops while tracking is lost.
    private IEnumerator RevealWater(Renderer water, float duration)
    {
        Material material = water.material;
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            material.SetFloat("_Reveal", Mathf.Clamp01(time / duration));
            yield return null;
        }
        material.SetFloat("_Reveal", 1f);
    }

    // ---------- Guide character ----------

    // The guide grows in beside the mask, waves, then gives the first line.
    private IEnumerator GuideArrives()
    {
        yield return ScaleTo(guide, guideScale, 1f);
        guideArrived = true;
        if (guideAnimator != null)
        {
            guideAnimator.SetTrigger("Wave");
        }
        yield return Wait(1.5f);
        GuideSay();
    }

    // Interaction: tap the guide. He says his next line, looping back to the first.
    private void GuideSay()
    {
        if (!guideArrived)
        {
            return;
        }

        int clipCount = guideLines != null ? guideLines.Length : 0;
        int captionCount = guideCaptions != null ? guideCaptions.Length : 0;
        int count = Mathf.Max(clipCount, captionCount);
        if (count == 0)
        {
            return;
        }

        int line = nextGuideLine % count;
        nextGuideLine++;

        if (line < captionCount)
        {
            ShowCaption(guideCaptions[line]);
        }

        AudioClip clip = line < clipCount ? guideLines[line] : null;
        PlayVoice(clip);
        if (clip == null)
        {
            talkTimer = silentTalkSeconds;
        }
        TurnGuide(guideFacing);
    }

    private void GuidePointAt(int index)
    {
        if (!guideArrived)
        {
            return;
        }

        if (guideAnimator != null)
        {
            guideAnimator.SetTrigger("Point");
        }

        // Turns halfway between the viewer (+Y) and the portrait, so his face stays visible.
        Vector3 toPortrait = portraits[index].lift.parent.localPosition - guide.localPosition;
        toPortrait.z = 0f;
        Vector3 forward = Vector3.Slerp(Vector3.up, toPortrait.normalized, 0.5f);
        TurnGuide(Quaternion.LookRotation(forward, Vector3.forward));
    }

    private void TurnGuide(Quaternion target)
    {
        if (guide != null && guideArrived)
        {
            StartCoroutine(TurnRoutine(target));
        }
    }

    private IEnumerator TurnRoutine(Quaternion target)
    {
        Quaternion start = guide.localRotation;
        float time = 0f;
        while (time < 0.5f)
        {
            time += DeltaTime;
            guide.localRotation = Quaternion.Slerp(start, target, time / 0.5f);
            yield return null;
        }
        guide.localRotation = target;
    }

    // ---------- Tracking loss ----------

    public override void OnTrackingLost()
    {
        base.OnTrackingLost();
        infoCard.SetActive(false);
        HideInfo();
    }

    public override void OnTrackingFound()
    {
        base.OnTrackingFound();
        if (selected >= 0)
        {
            ShowCard(selected);
        }
    }

    // ---------- Helpers ----------

    private void StartAmbient()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source != null && ambientLoop != null)
        {
            source.clip = ambientLoop;
            source.loop = true;
            source.Play();
        }
    }

    private void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null)
        {
            return;
        }

        voiceSource.Stop();
        if (clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.Play();
        }
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

    // Fades a renderer's alpha. Stops while tracking is lost.
    private IEnumerator FadeRenderer(Renderer target, float to, float duration)
    {
        if (target == null)
        {
            yield break;
        }

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
}
