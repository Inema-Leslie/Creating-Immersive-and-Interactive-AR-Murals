using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


[RequireComponent(typeof(ARTrackedImageManager))]
public class MuralTrackingHandler : MonoBehaviour
{
    [Tooltip("Prefab containing the MuralExperience component (tram, lights, wall patch).")]
    [SerializeField] GameObject experiencePrefab;

    [Tooltip("Scale the experience so 1 unit = physical width of the mural image.")]
    [SerializeField] bool scaleToImageWidth = true;

    ARTrackedImageManager manager;
    readonly Dictionary<TrackableId, MuralExperience> spawned = new Dictionary<TrackableId, MuralExperience>();

    void Awake()
    {
        manager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        manager.trackablesChanged.AddListener(OnTrackablesChanged);
    }

    void OnDisable()
    {
        manager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var image in args.added)
            Spawn(image);

        foreach (var image in args.updated)
            Refresh(image);

        foreach (var removed in args.removed)
        {
            if (spawned.TryGetValue(removed.Key, out var experience))
                experience.OnTrackingLost();
        }
    }

    void Spawn(ARTrackedImage image)
    {
        if (spawned.ContainsKey(image.trackableId)) return;

        
        var instance = Instantiate(experiencePrefab, image.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        if (scaleToImageWidth && image.size.x > 0f)
            instance.transform.localScale = Vector3.one * image.size.x;

        var experience = instance.GetComponent<MuralExperience>();
        spawned[image.trackableId] = experience;
        Refresh(image);
    }

    void Refresh(ARTrackedImage image)
    {
        if (!spawned.TryGetValue(image.trackableId, out var experience)) return;

       
        if (image.trackingState == TrackingState.Tracking)
            experience.OnTrackingFound();
        else
            experience.OnTrackingLost();
    }
}