using System.Collections.Generic;
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
    public FlyingCarsUI ui;

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
        ui.CardsRequested += BuildCards;
        experience.OnStateChanged += HandleState;
        systems.SetActive(true);
    }

    private void OnDestroy()
    {
        woman.OnTapAreaCreated -= MarkTappable;
        ui.CardsRequested -= BuildCards;
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
        HideInfo();
        tracker.ReportFound();
    }

    public override void OnTrackingFound()
    {
        if (framePaused)
        {
            framePaused = false;
            base.OnTrackingFound();
        }
        HideInfo();
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

        switch (state)
        {
            case ExperienceState.Awakening: ShowCaption(0); break;
            case ExperienceState.CarsActive: ShowCaption(1); break;
            case ExperienceState.WomanEmerging: ShowCaption(2); break;
            case ExperienceState.Savanna: ShowCaption(3); break;
            case ExperienceState.Complete: ShowCaption(4); break;
        }
    }

    private IReadOnlyList<MuralCard> BuildCards()
    {
        List<MuralCard> cards = new List<MuralCard>();
        MuralSpawner source = spawner != null ? spawner : FindAnyObjectByType<MuralSpawner>();
        List<MuralData> murals = source != null ? source.murals : new List<MuralData>();
        if (data != null && !murals.Contains(data))
        {
            murals = new List<MuralData>(murals) { data };
        }

        foreach (MuralData mural in murals)
        {
            if (mural == null)
            {
                continue;
            }
            List<string> kinds = new List<string>();
            if (mural.transformations != null)
            {
                foreach (TransformationType kind in mural.transformations)
                {
                    kinds.Add(kind.ToString());
                }
            }
            cards.Add(new MuralCard
            {
                title = string.IsNullOrEmpty(mural.displayTitle) ? mural.imageName : mural.displayTitle,
                location = mural.location,
                about = mural.description,
                whatHappens = string.Join(", ", kinds),
                whereToStand = mural.scanHint,
                isThisMural = mural == data,
                story = mural == data ? tracker.Config.muralStory : null,
            });
        }
        return cards;
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
