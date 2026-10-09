using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// Base class for every mural. Each mural script inherits from this class:
//     public class LeadersMural : MuralExperience { ... }
// MuralSpawner calls the Notify methods. Mural scripts override PlayIntro and OnTapped,
// and may override OnTrackingLost, OnTrackingFound and ResetExperience (call the base version inside).
public class MuralExperience : MonoBehaviour
{
    [Header("Mural")]
    public MuralData data;

    [Header("Tracking loss")]
    [Tooltip("How visible the content stays while tracking is lost. 0 is invisible, 1 is full.")]
    [Range(0f, 1f)]
    public float lostFadeAmount = 0.3f;

    [Tooltip("Seconds for the fade out and the fade back in.")]
    public float fadeDuration = 0.5f;

    // Raised when a mural wants to show a caption. The UI listens to this event.
    public static event Action<string> CaptionRequested;

    // Raised when a mural wants to show or hide longer text (title and body) in the on-screen info panel.
    public static event Action<string, string> InfoRequested;
    public static event Action InfoHidden;

    // True once the intro has started. The intro never plays twice for the same spawn.
    public bool IntroPlayed { get; private set; }

    // True while tracking is lost. Mural animations should stop while this is true.
    public bool IsPaused { get; private set; }

    // Set by MuralSpawner when the prefab is spawned.
    [HideInInspector]
    public MuralSpawner spawner;

    // Delta time that is zero while paused. Use this instead of Time.deltaTime in Update.
    protected float DeltaTime
    {
        get { return IsPaused ? 0f : Time.deltaTime; }
    }

    private Coroutine fadeRoutine;
    private float currentFade = 1f;
    private MaterialPropertyBlock propertyBlock;

    private readonly Dictionary<Animator, float> pausedAnimators = new Dictionary<Animator, float>();
    private readonly List<ParticleSystem> pausedParticles = new List<ParticleSystem>();
    private readonly List<AudioSource> pausedAudio = new List<AudioSource>();
    private readonly List<PlayableDirector> pausedDirectors = new List<PlayableDirector>();

    // ---------- Called by MuralSpawner ----------

    public void NotifyTrackingFound()
    {
        if (!IntroPlayed)
        {
            IntroPlayed = true;
            StartCoroutine(PlayIntroNextFrame());
        }
        else
        {
            OnTrackingFound();
        }
    }

    public void NotifyTrackingLost()
    {
        OnTrackingLost();
    }

    // Editor testing without a phone: in Play mode, use the component menu (three dots) on the mural script.
    [ContextMenu("Test: Tracking Found")]
    protected void TestTrackingFound()
    {
        if (Application.isPlaying)
        {
            NotifyTrackingFound();
        }
    }

    [ContextMenu("Test: Tracking Lost")]
    protected void TestTrackingLost()
    {
        if (Application.isPlaying)
        {
            NotifyTrackingLost();
        }
    }

    // Waits one frame so that Awake and Start have run on the mural script before the intro begins.
    private IEnumerator PlayIntroNextFrame()
    {
        yield return null;
        PlayIntro();
    }

    // ---------- Methods that mural scripts override ----------

    // Starts the transition out of the flat mural. Called once, the first time the image is tracked.
    public virtual void PlayIntro()
    {
    }

    // Freezes animation, particles, audio and Timeline, then fades the content down.
    public virtual void OnTrackingLost()
    {
        SetPaused(true);
        FadeTo(lostFadeAmount);
    }

    // Fades the content back up and resumes from where it stopped. Does not replay the intro.
    public virtual void OnTrackingFound()
    {
        SetPaused(false);
        FadeTo(1f);
    }

    // Removes this instance and spawns a fresh copy, which plays the intro again.
    public virtual void ResetExperience()
    {
        if (spawner != null)
        {
            spawner.Respawn(this);
        }
    }

    // Called by MuralTappable when the user taps one of this mural's objects.
    public virtual void OnTapped(MuralTappable tappable)
    {
    }

    // ---------- Helpers for mural scripts ----------

    // Waits for the given seconds. The timer stops while tracking is lost.
    protected IEnumerator Wait(float seconds)
    {
        float time = 0f;
        while (time < seconds)
        {
            time += DeltaTime;
            yield return null;
        }
    }

    // Smoothly changes local scale. Stops while tracking is lost.
    protected IEnumerator ScaleTo(Transform target, Vector3 endScale, float duration)
    {
        Vector3 startScale = target.localScale;
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            yield return null;
        }
        target.localScale = endScale;
    }

    // Smoothly changes local position. Stops while tracking is lost.
    protected IEnumerator MoveTo(Transform target, Vector3 endLocalPosition, float duration)
    {
        Vector3 startPosition = target.localPosition;
        float time = 0f;
        while (time < duration)
        {
            time += DeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
            target.localPosition = Vector3.LerpUnclamped(startPosition, endLocalPosition, t);
            yield return null;
        }
        target.localPosition = endLocalPosition;
    }

    // Plays a one shot sound from the AudioSource on the prefab root. Adds an AudioSource if missing.
    protected void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
        source.PlayOneShot(clip);
    }

    // Sends a caption to the UI. Logs it when no UI is listening, for example in a sandbox scene.
    protected void ShowCaption(string text)
    {
        if (CaptionRequested != null)
        {
            CaptionRequested(text);
        }
        else
        {
            Debug.Log("[Caption] " + text);
        }
    }

    // Sends the caption with the given index from the data asset.
    protected void ShowCaption(int index)
    {
        if (data != null && data.captions != null && index >= 0 && index < data.captions.Length)
        {
            ShowCaption(data.captions[index]);
        }
    }

    // Shows a title and longer text on screen, where it stays readable at any distance.
    protected void ShowInfo(string title, string body)
    {
        if (InfoRequested != null)
        {
            InfoRequested(title, body);
        }
        else
        {
            Debug.Log("[Info] " + title + ": " + body);
        }
    }

    protected void HideInfo()
    {
        if (InfoHidden != null)
        {
            InfoHidden();
        }
    }

    // ---------- Pause and fade ----------

    private void SetPaused(bool paused)
    {
        IsPaused = paused;

        if (paused)
        {
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                if (!pausedAnimators.ContainsKey(animator))
                {
                    pausedAnimators.Add(animator, animator.speed);
                    animator.speed = 0f;
                }
            }
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.isPlaying)
                {
                    particles.Pause(false);
                    pausedParticles.Add(particles);
                }
            }
            foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
            {
                if (source.isPlaying)
                {
                    source.Pause();
                    pausedAudio.Add(source);
                }
            }
            foreach (PlayableDirector director in GetComponentsInChildren<PlayableDirector>(true))
            {
                if (director.state == PlayState.Playing)
                {
                    director.Pause();
                    pausedDirectors.Add(director);
                }
            }
        }
        else
        {
            foreach (KeyValuePair<Animator, float> pair in pausedAnimators)
            {
                if (pair.Key != null)
                {
                    pair.Key.speed = pair.Value;
                }
            }
            foreach (ParticleSystem particles in pausedParticles)
            {
                if (particles != null)
                {
                    particles.Play(false);
                }
            }
            foreach (AudioSource source in pausedAudio)
            {
                if (source != null)
                {
                    source.UnPause();
                }
            }
            foreach (PlayableDirector director in pausedDirectors)
            {
                if (director != null)
                {
                    director.Resume();
                }
            }
            pausedAnimators.Clear();
            pausedParticles.Clear();
            pausedAudio.Clear();
            pausedDirectors.Clear();
        }
    }

    private void FadeTo(float target)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    // Uses real time, not DeltaTime, so the fade still runs while paused.
    private IEnumerator FadeRoutine(float target)
    {
        float start = currentFade;
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            currentFade = Mathf.Lerp(start, target, time / fadeDuration);
            ApplyFade(currentFade);
            yield return null;
        }
        currentFade = target;
        ApplyFade(currentFade);
        fadeRoutine = null;
    }

    // Transparent materials fade their alpha. Opaque materials darken instead.
    // At full visibility the overrides are cleared so the original materials show again.
    private void ApplyFade(float amount)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
            {
                continue;
            }

            Material[] materials = rend.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                propertyBlock.Clear();

                if (amount < 0.999f)
                {
                    string colorName = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                    if (!material.HasProperty(colorName))
                    {
                        continue;
                    }

                    Color color = material.GetColor(colorName);
                    bool isTransparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
                    if (isTransparent)
                    {
                        color.a *= amount;
                    }
                    else
                    {
                        color.r *= amount;
                        color.g *= amount;
                        color.b *= amount;
                    }
                    propertyBlock.SetColor(colorName, color);
                }

                rend.SetPropertyBlock(propertyBlock, i);
            }
        }
    }
}
