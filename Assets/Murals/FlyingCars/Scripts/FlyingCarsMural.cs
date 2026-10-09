using MuralAR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.ARFoundation;

public class FlyingCarsMural : MuralExperience, IMuralHost
{
    [Header("FlyingCars systems")]
    [Tooltip("Holds every system. Inactive in the prefab, switched on once the scene objects are handed over.")]
    public GameObject systems;
    public MuralTracker tracker;
    public ExperienceController experience;
    public WomanController woman;

    private bool framePaused;

    private void Awake()
    {
        Camera arCamera = Camera.main;
        Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
        ARCameraBackground background = arCamera != null ? arCamera.GetComponent<ARCameraBackground>() : null;
        SceneRefs refs = new SceneRefs(arCamera, sun, background);
        foreach (ISceneBound bound in systems.GetComponentsInChildren<ISceneBound>(true))
        {
            bound.Bind(refs);
        }

        if (data != null)
        {
            tracker.SetSize(new Vector2(data.widthMeters, data.heightMeters));
        }

        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        woman.OnTapAreaCreated += MarkTappable;
        experience.OnStateChanged += HandleState;
        systems.SetActive(true);
    }

    private void OnDestroy()
    {
        woman.OnTapAreaCreated -= MarkTappable;
        experience.OnStateChanged -= HandleState;
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }

    public override void PlayIntro()
    {
        tracker.ReportFound();
    }

    public override void OnTrackingFound()
    {
        if (framePaused)
        {
            framePaused = false;
            base.OnTrackingFound();
        }
        tracker.ReportFound();
    }

    public override void OnTrackingLost()
    {
        tracker.ReportLost();
        if (experience.State == ExperienceState.Savanna)
        {
            return;
        }
        framePaused = true;
        base.OnTrackingLost();
    }

    public override void OnTapped(MuralTappable tappable)
    {
        woman.HandleTap(tappable.gameObject);
    }

    private void HandleState(ExperienceState state)
    {
        if (state == ExperienceState.Savanna && framePaused)
        {
            framePaused = false;
            base.OnTrackingFound();
        }
    }

    private void MarkTappable(GameObject target, string id)
    {
        MuralTappable tappable = target.AddComponent<MuralTappable>();
        tappable.id = id;
    }

    public void SimulateFound()
    {
        NotifyTrackingFound();
    }

    public void SimulateLost()
    {
        NotifyTrackingLost();
    }

    public void SimulateTap(GameObject target)
    {
        MuralTappable tappable = target.GetComponentInParent<MuralTappable>();
        if (tappable != null)
        {
            tappable.Tap();
        }
    }
}
