using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// On-screen text for every mural: a caption line at the bottom and an info panel for stories.
// Listens to MuralExperience events, so mural scripts never reference the UI directly.
// Builds its own simple layout; the UI design can restyle or replace it by listening to the same events.
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
        float bottom = infoBox.activeSelf ? 800f : 60f;
        rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
        rect.offsetMax = new Vector2(rect.offsetMax.x, bottom + 140f);
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

        // Caption: one short line near the bottom of the screen.
        captionBox = MakeBox("Caption", canvasObject.transform, 40f, 60f, 140f);
        captionText = MakeText("Text", captionBox.transform, 42f, FontStyles.Normal, Color.white);
        captionText.alignment = TextAlignmentOptions.Center;
        Fill(captionText.rectTransform, 30f, 15f, 30f, 15f);

        // Info panel: title and story, above the caption.
        infoBox = MakeBox("Info", canvasObject.transform, 40f, 220f, 560f);
        infoTitle = MakeText("Title", infoBox.transform, 56f, FontStyles.Bold, new Color(1f, 0.8f, 0.42f));
        Fill(infoTitle.rectTransform, 40f, 30f, 40f, 440f);
        infoBody = MakeText("Body", infoBox.transform, 38f, FontStyles.Normal, Color.white);
        Fill(infoBody.rectTransform, 40f, 120f, 40f, 30f);
        // Long stories shrink to fit inside the panel instead of running out of it.
        infoBody.enableAutoSizing = true;
        infoBody.fontSizeMin = 24f;
        infoBody.fontSizeMax = 38f;
        captionText.enableAutoSizing = true;
        captionText.fontSizeMin = 26f;
        captionText.fontSizeMax = 42f;
    }

    // A dark box stretched across the screen width, a set distance from the bottom.
    private GameObject MakeBox(string name, Transform parent, float sideMargin, float bottom, float height)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        box.transform.SetParent(parent, false);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(sideMargin, bottom);
        rect.offsetMax = new Vector2(-sideMargin, bottom + height);
        Image image = box.GetComponent<Image>();
        image.color = new Color(0.17f, 0.12f, 0.1f, 0.9f);
        image.raycastTarget = false;
        Outline outline = box.GetComponent<Outline>();
        outline.effectColor = new Color(0.81f, 0.48f, 0.19f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);
        return box;
    }

    private TMP_Text MakeText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    // Stretches a rect to fill its parent with padding (left, top, right, bottom).
    private void Fill(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
