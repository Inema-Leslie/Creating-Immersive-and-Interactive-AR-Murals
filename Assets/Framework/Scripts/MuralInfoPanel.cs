using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// On-screen text for every mural: a caption line at the bottom and an info panel for stories.
// Listens to MuralExperience events, so mural scripts never reference the UI directly.
// Builds its layout in code from the pieces in MuralTheme, so it matches the app screens.
public class MuralInfoPanel : MonoBehaviour
{
    [Tooltip("Seconds a caption stays on screen.")]
    public float captionSeconds = 4f;

    private GameObject captionBox;
    private TMP_Text captionText;
    private GameObject infoBox;
    private TMP_Text infoTitle;
    private TMP_Text infoBody;
    private Coroutine captionRoutine;

    // Sizes in canvas units (the canvas is 1080 by 1920).
    private const float CaptionBottom = 60f;
    private const float CaptionHeight = 150f;
    private const float InfoBottom = 200f;
    private const float InfoHeight = 620f;

    private void Awake()
    {
        BuildLayout();
        captionBox.SetActive(false);
        infoBox.SetActive(false);
    }

    private void OnEnable()
    {
        MuralExperience.CaptionRequested += ShowCaption;
        MuralExperience.InfoRequested += ShowInfo;
        MuralExperience.InfoHidden += HideInfo;
    }

    private void OnDisable()
    {
        MuralExperience.CaptionRequested -= ShowCaption;
        MuralExperience.InfoRequested -= ShowInfo;
        MuralExperience.InfoHidden -= HideInfo;
    }

    public void ShowCaption(string text)
    {
        captionText.text = text;
        captionBox.SetActive(true);
        PlaceCaption();
        if (captionRoutine != null)
        {
            StopCoroutine(captionRoutine);
        }
        captionRoutine = StartCoroutine(HideCaptionLater());
    }

    public void ShowInfo(string title, string body)
    {
        infoTitle.text = title;
        infoBody.text = body;
        infoBox.SetActive(true);
        PlaceCaption();
    }

    public void HideInfo()
    {
        infoBox.SetActive(false);
        PlaceCaption();
    }

    public void HideCaption()
    {
        if (captionRoutine != null)
        {
            StopCoroutine(captionRoutine);
            captionRoutine = null;
        }
        captionBox.SetActive(false);
    }

    // Clears everything a mural put on screen. Called when the app moves to another mural, so no text stays behind.
    public void ClearAll()
    {
        HideInfo();
        HideCaption();
    }

    // The caption sits at the bottom, or just above the info panel while that is open, so they never overlap.
    private void PlaceCaption()
    {
        RectTransform rect = captionBox.GetComponent<RectTransform>();
        float bottom = infoBox.activeSelf ? InfoBottom + InfoHeight + 20f : CaptionBottom;
        rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
        rect.offsetMax = new Vector2(rect.offsetMax.x, bottom + CaptionHeight);
    }

    private IEnumerator HideCaptionLater()
    {
        yield return new WaitForSeconds(captionSeconds);
        captionBox.SetActive(false);
    }

    // ---------- Layout ----------

    private void BuildLayout()
    {
        GameObject canvasObject = new GameObject("MuralInfoCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        Color deep = new Color(MuralTheme.EarthDeep.r, MuralTheme.EarthDeep.g, MuralTheme.EarthDeep.b, 0.93f);

        // Caption: one short line near the bottom of the screen, in a rounded box with a gold edge.
        RectTransform captionInside = MuralTheme.MakeCard("Caption", canvasObject.transform, deep, MuralTheme.Gold, 3f, 30f);
        captionBox = captionInside.parent.gameObject;
        MuralTheme.PinBottom((RectTransform)captionBox.transform, CaptionBottom, CaptionHeight, 40f);
        captionText = MuralTheme.MakeText("Text", captionInside, "", 40f, false, MuralTheme.Bone);
        MuralTheme.Stretch(captionText.rectTransform, 30f, 12f, 30f, 12f);
        captionText.enableAutoSizing = true;
        captionText.fontSizeMin = 26f;
        captionText.fontSizeMax = 40f;

        // Info panel: mudcloth border on top, then the title and the story.
        RectTransform infoInside = MuralTheme.MakeCard("Info", canvasObject.transform, deep, MuralTheme.Terracotta, 4f, 36f);
        infoBox = infoInside.parent.gameObject;
        MuralTheme.PinBottom((RectTransform)infoBox.transform, InfoBottom, InfoHeight, 40f);

        RawImage band = MuralTheme.MakeBand("Band", infoInside);
        MuralTheme.PinTop(band.rectTransform, 22f, 44f, 50f);

        infoTitle = MuralTheme.MakeText("Title", infoInside, "", 50f, true, MuralTheme.Gold);
        infoTitle.alignment = TextAlignmentOptions.Left;
        MuralTheme.PinTop(infoTitle.rectTransform, 84f, 80f, 44f);
        infoTitle.enableAutoSizing = true;
        infoTitle.fontSizeMin = 34f;
        infoTitle.fontSizeMax = 50f;

        infoBody = MuralTheme.MakeText("Body", infoInside, "", 36f, false, MuralTheme.Bone);
        infoBody.alignment = TextAlignmentOptions.TopLeft;
        MuralTheme.Stretch(infoBody.rectTransform, 44f, 176f, 44f, 34f);
        // Long stories shrink to fit inside the panel instead of running out of it.
        infoBody.enableAutoSizing = true;
        infoBody.fontSizeMin = 24f;
        infoBody.fontSizeMax = 36f;
    }
}
