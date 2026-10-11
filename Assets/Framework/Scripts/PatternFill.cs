using UnityEngine;
using UnityEngine.UI;

// Repeats a pattern texture across a RawImage without stretching it, whatever the screen size.
// Used by the app screens for the rock art background, the mudcloth borders and the cut-out background.
[RequireComponent(typeof(RawImage))]
public class PatternFill : MonoBehaviour
{
    public enum FillMode
    {
        // Repeats sideways; each tile is as tall as the rect. For borders.
        RepeatAcross,
        // Repeats in both directions; each tile is tileSize wide. For backgrounds.
        RepeatBoth,
        // One copy that covers the whole rect, cropped at the edges. For pictures that do not repeat.
        Cover
    }

    public FillMode mode = FillMode.RepeatBoth;

    [Tooltip("Width divided by height of one tile of the texture.")]
    public float tileAspect = 1f;

    [Tooltip("Width of one tile in canvas units. Only used by RepeatBoth.")]
    public float tileSize = 540f;

    private RawImage rawImage;
    private RectTransform rectTransform;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnRectTransformDimensionsChange()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (rawImage == null || rectTransform == null)
        {
            return;
        }

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;
        if (width <= 0f || height <= 0f || tileAspect <= 0f)
        {
            return;
        }

        if (mode == FillMode.RepeatAcross)
        {
            float tileWidth = height * tileAspect;
            rawImage.uvRect = new Rect(0f, 0f, width / tileWidth, 1f);
        }
        else if (mode == FillMode.RepeatBoth)
        {
            float tileHeight = tileSize / tileAspect;
            rawImage.uvRect = new Rect(0f, 0f, width / tileSize, height / tileHeight);
        }
        else
        {
            float rectAspect = width / height;
            if (rectAspect > tileAspect)
            {
                float visible = tileAspect / rectAspect;
                rawImage.uvRect = new Rect(0f, (1f - visible) * 0.5f, 1f, visible);
            }
            else
            {
                float visible = rectAspect / tileAspect;
                rawImage.uvRect = new Rect((1f - visible) * 0.5f, 0f, visible, 1f);
            }
        }
    }
}
