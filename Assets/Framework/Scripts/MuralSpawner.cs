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

    [Tooltip("Keeps murals on walls standing straight up. Small errors in the wall angle no longer tip the content forward or back.")]
    public bool keepUpright = true;

    [Tooltip("How quickly the content follows tracking corrections. Lower is steadier, higher reacts faster. 0 turns smoothing off.")]
    public float smoothing = 8f;

    [Tooltip("Turns the scene light to shine on the found mural from the visitor's side, so 3D content is never lit from behind.")]
    public bool lightFromViewer = true;

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
    private readonly Dictionary<string, Pose> shownPose = new Dictionary<string, Pose>();

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
                    AimLightAt(mural.transform);
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

    // Runs after tracking has moved the images this frame. Each mural follows its image smoothly,
    // standing upright on the wall, so tracking wobble does not shake or tip the content.
    private void LateUpdate()
    {
        foreach (KeyValuePair<string, MuralExperience> pair in spawned)
        {
            ARTrackedImage image;
            if (pair.Value == null || !images.TryGetValue(pair.Key, out image) || image == null)
            {
                continue;
            }

            Vector3 targetPosition = image.transform.position;
            Quaternion targetRotation = TargetRotation(image.transform);

            // Start from where the mural was shown last frame, not where the image moved it to.
            Pose pose;
            if (!shownPose.TryGetValue(pair.Key, out pose))
            {
                pose = new Pose(targetPosition, targetRotation);
            }
            float blend = smoothing > 0f ? 1f - Mathf.Exp(-smoothing * Time.deltaTime) : 1f;
            pose.position = Vector3.Lerp(pose.position, targetPosition, blend);
            pose.rotation = Quaternion.Slerp(pose.rotation, targetRotation, blend);
            shownPose[pair.Key] = pose;
            pair.Value.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }

    // The world's directions are random compared to each wall, so a fixed light can end up behind the mural.
    // Aim it from in front of the wall, above and a little to the left. A mural may still change it afterwards.
    private void AimLightAt(Transform mural)
    {
        if (!lightFromViewer)
        {
            return;
        }
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }
        }
        if (sun == null)
        {
            return;
        }
        // On a mural: Y points out of the wall, Z up the wall, X to the right.
        Vector3 direction = -mural.up * 0.6f - mural.forward * 0.7f + mural.right * 0.35f;
        sun.transform.rotation = Quaternion.LookRotation(direction.normalized, mural.forward);
    }

    // The image's own rotation, or for a wall, the same rotation with the wall made exactly vertical.
    // On an image, Y points out of the picture and Z points to the top of the picture.
    private Quaternion TargetRotation(Transform image)
    {
        Vector3 outOfWall = image.up;
        if (!keepUpright || Mathf.Abs(outOfWall.y) > 0.7f)
        {
            return image.rotation;
        }
        outOfWall.y = 0f;
        return Quaternion.LookRotation(Vector3.up, outOfWall.normalized);
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
        instance.transform.rotation = TargetRotation(image.transform);
        shownPose[imageName] = new Pose(instance.transform.position, instance.transform.rotation);

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
        shownPose.Remove(imageName);
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
