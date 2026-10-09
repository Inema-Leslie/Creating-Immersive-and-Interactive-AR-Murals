using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Spawns the matching mural prefab when a reference image is detected, by image name.
// Tells the mural when tracking is found or lost. Place on the same object as the ARTrackedImageManager.
[RequireComponent(typeof(ARTrackedImageManager))]
public class MuralSpawner : MonoBehaviour
{
    [Tooltip("One entry per mural. Each imageName must match a name in the reference image library.")]
    public List<MuralData> murals = new List<MuralData>();

    [Tooltip("Seconds without tracking before the mural counts as lost. Prevents flicker.")]
    public float lostDelay = 0.5f;

    [Tooltip("Scales content by detected image width / data width. Lets a printout or screen stand in for the real mural.")]
    public bool scaleToImageSize = true;

    [Header("Events for the UI")]
    public UnityEvent<MuralExperience> muralFound = new UnityEvent<MuralExperience>();
    public UnityEvent<MuralExperience> muralLost = new UnityEvent<MuralExperience>();

    // The mural that was found most recently.
    public MuralExperience ActiveMural { get; private set; }

    private ARTrackedImageManager imageManager;

    // All dictionaries use the reference image name as the key.
    private readonly Dictionary<string, ARTrackedImage> images = new Dictionary<string, ARTrackedImage>();
    private readonly Dictionary<string, MuralExperience> spawned = new Dictionary<string, MuralExperience>();
    private readonly Dictionary<string, bool> isTracked = new Dictionary<string, bool>();
    private readonly Dictionary<string, float> lastSeenTime = new Dictionary<string, float>();
    private readonly Dictionary<TrackableId, string> idToName = new Dictionary<TrackableId, string>();
    private readonly HashSet<string> missingData = new HashSet<string>();

    private void Awake()
    {
        imageManager = GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        imageManager.trackablesChanged.AddListener(OnTrackablesChanged);

        // Picks up images that were found while the spawner was switched off, for example on the Start screen.
        foreach (ARTrackedImage image in imageManager.trackables)
        {
            string imageName = image.referenceImage.name;
            images[imageName] = image;
            idToName[image.trackableId] = imageName;
        }
    }

    private void OnDisable()
    {
        imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (ARTrackedImage image in args.added)
        {
            string imageName = image.referenceImage.name;
            images[imageName] = image;
            idToName[image.trackableId] = imageName;
        }

        foreach (KeyValuePair<TrackableId, ARTrackedImage> pair in args.removed)
        {
            string imageName;
            if (idToName.TryGetValue(pair.Key, out imageName))
            {
                RemoveMural(imageName);
                images.Remove(imageName);
                idToName.Remove(pair.Key);
            }
        }
    }

    // Tracking state is checked every frame instead of only on change events, which keeps the logic in one place.
    private void Update()
    {
        foreach (KeyValuePair<string, ARTrackedImage> pair in images)
        {
            string imageName = pair.Key;
            ARTrackedImage image = pair.Value;
            if (image == null)
            {
                continue;
            }

            bool trackingNow = image.trackingState == TrackingState.Tracking;

            if (trackingNow)
            {
                lastSeenTime[imageName] = Time.time;

                if (!spawned.ContainsKey(imageName))
                {
                    SpawnMural(imageName, image);
                }

                if (!GetTracked(imageName) && spawned.ContainsKey(imageName))
                {
                    isTracked[imageName] = true;
                    MuralExperience mural = spawned[imageName];
                    ActiveMural = mural;
                    mural.NotifyTrackingFound();
                    muralFound.Invoke(mural);
                }
            }
            else if (GetTracked(imageName) && Time.time - lastSeenTime[imageName] > lostDelay)
            {
                isTracked[imageName] = false;
                MuralExperience mural = spawned[imageName];
                mural.NotifyTrackingLost();
                muralLost.Invoke(mural);
            }
        }
    }

    private void SpawnMural(string imageName, ARTrackedImage image)
    {
        MuralData muralData = FindData(imageName);
        if (muralData == null || muralData.prefab == null)
        {
            if (missingData.Add(imageName))
            {
                Debug.LogWarning("MuralSpawner: no data or prefab for image " + imageName);
            }
            return;
        }

        GameObject instance = Instantiate(muralData.prefab, image.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        if (scaleToImageSize && muralData.widthMeters > 0f && image.size.x > 0f)
        {
            instance.transform.localScale = Vector3.one * (image.size.x / muralData.widthMeters);
        }

        MuralExperience mural = instance.GetComponent<MuralExperience>();
        if (mural == null)
        {
            Debug.LogWarning("MuralSpawner: prefab for " + imageName + " has no MuralExperience script on its root.");
            Destroy(instance);
            return;
        }

        if (mural.data == null)
        {
            mural.data = muralData;
        }
        mural.spawner = this;

        spawned[imageName] = mural;
        isTracked[imageName] = false;
    }

    private MuralData FindData(string imageName)
    {
        foreach (MuralData muralData in murals)
        {
            if (muralData != null && muralData.imageName == imageName)
            {
                return muralData;
            }
        }
        return null;
    }

    private bool GetTracked(string imageName)
    {
        bool tracked;
        return isTracked.TryGetValue(imageName, out tracked) && tracked;
    }

    private void RemoveMural(string imageName)
    {
        MuralExperience mural;
        if (spawned.TryGetValue(imageName, out mural))
        {
            if (mural != null)
            {
                Destroy(mural.gameObject);
            }
            if (ActiveMural == mural)
            {
                ActiveMural = null;
            }
        }
        spawned.Remove(imageName);
        isTracked.Remove(imageName);
    }

    // Destroys a mural instance. A fresh copy spawns on the next frame the image is tracked, and its intro plays.
    public void Respawn(MuralExperience mural)
    {
        foreach (KeyValuePair<string, MuralExperience> pair in spawned)
        {
            if (pair.Value == mural)
            {
                RemoveMural(pair.Key);
                return;
            }
        }
    }

    // Destroys every spawned mural. Used by the UI when the experience restarts.
    public void ClearAll()
    {
        List<string> names = new List<string>(spawned.Keys);
        foreach (string imageName in names)
        {
            RemoveMural(imageName);
        }
    }
}
