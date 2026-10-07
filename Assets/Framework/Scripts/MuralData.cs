using UnityEngine;

// The kinds of transformation a mural can use. A mural may list more than one.
public enum TransformationType
{
    Emergence,
    Animation,
    Reconstruction,
    Expansion,
    Storytelling
}

// Information about one mural. One asset per mural: Assets/Murals/<MuralName>/Data/<MuralName>Data.asset
[CreateAssetMenu(fileName = "NewMuralData", menuName = "AR Murals/Mural Data")]
public class MuralData : ScriptableObject
{
    [Header("Tracking")]
    [Tooltip("Must match the image name in the reference image library exactly, for example FabLab.")]
    public string imageName;

    [Tooltip("Real width of the tracking image area in meters.")]
    public float widthMeters = 1f;

    [Tooltip("Real height of the tracking image area in meters.")]
    public float heightMeters = 1f;

    [Tooltip("The mural prefab spawned when this image is detected.")]
    public GameObject prefab;

    [Header("Display")]
    public string displayTitle;

    public string location;

    public TransformationType[] transformations;

    [TextArea(3, 8)]
    public string description;

    [Tooltip("Short lines shown as captions during the experience.")]
    [TextArea(2, 4)]
    public string[] captions;

    [Tooltip("Hint shown on the scanning screen, for example where to stand.")]
    [TextArea(2, 4)]
    public string scanHint;
}
