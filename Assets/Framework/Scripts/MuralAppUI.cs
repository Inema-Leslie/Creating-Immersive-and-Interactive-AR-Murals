using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using TMPro;

// The four app screens: Start, Scanning, AR and Exit.
// Listens to MuralSpawner events, so mural scripts never need to know about these screens.
// Builds its layout in code from the pieces in MuralTheme, like MuralInfoPanel.
public class MuralAppUI : MonoBehaviour
{
    public enum AppScreen
    {
        Start,
        Scanning,
        AR,
        Exit
    }

    [Tooltip("The spawner on the XR Origin. Switched off on the Start and Exit screens.")]
    public MuralSpawner spawner;

    [Tooltip("Shows the About text of the current mural.")]
    public MuralInfoPanel infoPanel;

    public string appTitle = "AR Murals";

    [TextArea(2, 4)]
    public string welcomeText = "Point your phone at a mural on campus and watch it come to life.";

    [Tooltip("Seconds without the mural before the scanning hint comes back.")]
    public float lostToScanSeconds = 3f;

    public AppScreen CurrentScreen { get; private set; }

    // Colours, fonts and patterns come from MuralTheme.
    private Transform canvasRoot;
    private readonly List<RectTransform> safeAreas = new List<RectTransform>();
    private Rect appliedSafeArea;

    private GameObject startScreen;
    private GameObject scanScreen;
    private GameObject arScreen;
    private GameObject exitScreen;

    private Button startButton;
    private TMP_Text startStatus;
    private TMP_Text scanTitle;
    private TMP_Text muralTitle;
    private TMP_Text muralLocation;
    private TMP_Text exitSummary;
    private readonly List<Image> scanCorners = new List<Image>();

    private MuralExperience currentMural;
    private MuralExperience lostMural;
    private bool aboutOpen;

    // Murals the visitor has seen at least once, by image name.
    private readonly HashSet<string> visited = new HashSet<string>();

    private void Awake()
    {
        BuildLayout();
    }

    private void OnEnable()
    {
        if (spawner != null)
        {
            spawner.muralFound.AddListener(OnMuralFound);
            spawner.muralLost.AddListener(OnMuralLost);
        }
    }

    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.muralFound.RemoveListener(OnMuralFound);
            spawner.muralLost.RemoveListener(OnMuralLost);
        }
    }

    private void Start()
    {
        SetSpawnerOn(false);
        ShowScreen(AppScreen.Start);
        StartCoroutine(CheckARSupport());
    }

    private void Update()
    {
        ApplySafeArea();

        // The Android back button arrives as the Escape key.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            OnBackPressed();
        }

        // A slow pulse on the scanning frame so the screen does not look frozen.
        if (CurrentScreen == AppScreen.Scanning)
        {
            float alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
            foreach (Image corner in scanCorners)
            {
                Color color = corner.color;
                color.a = alpha;
                corner.color = color;
            }
        }
    }

    // ---------- Screens ----------

    public void ShowScreen(AppScreen screen)
    {
        CurrentScreen = screen;
        startScreen.SetActive(screen == AppScreen.Start);
        scanScreen.SetActive(screen == AppScreen.Scanning);
        arScreen.SetActive(screen == AppScreen.AR);
        exitScreen.SetActive(screen == AppScreen.Exit);

        if (screen != AppScreen.AR)
        {
            CloseAbout();
        }
    }

    private void OnStartPressed()
    {
        scanTitle.text = "Point your camera at a mural";
        SetSpawnerOn(true);
        ShowScreen(AppScreen.Scanning);
    }

    private void OnExitPressed()
    {
        ClearMuralText();
        if (spawner != null)
        {
            spawner.ClearAll();
        }
        SetSpawnerOn(false);
        currentMural = null;
        lostMural = null;

        int total = spawner != null ? spawner.murals.Count : 0;
        exitSummary.text = "You explored " + visited.Count + " of " + total + " murals.\n"
            + "To learn more about a mural, ask ALU guest relations.";
        ShowScreen(AppScreen.Exit);
    }

    private void OnScanAgainPressed()
    {
        OnStartPressed();
    }

    private void OnClosePressed()
    {
        Application.Quit();
    }

    private void OnBackPressed()
    {
        if (CurrentScreen == AppScreen.Start)
        {
            Application.Quit();
        }
        else if (CurrentScreen == AppScreen.Exit)
        {
            ShowScreen(AppScreen.Start);
        }
        else
        {
            OnExitPressed();
        }
    }

    // Replays the intro: the spawner removes the mural and a fresh copy spawns while the image is tracked.
    private void OnReplayPressed()
    {
        if (spawner != null && currentMural != null)
        {
            spawner.Respawn(currentMural);
            currentMural = null;
            ClearMuralText();
        }
    }

    private void OnAboutPressed()
    {
        // The panel's own Close button may have closed it already.
        if (aboutOpen && infoPanel != null && infoPanel.IsInfoOpen)
        {
            CloseAbout();
            return;
        }

        if (infoPanel != null && currentMural != null && currentMural.data != null)
        {
            infoPanel.ShowInfo(MuralName(currentMural.data), currentMural.data.description);
            aboutOpen = true;
        }
    }

    private void ClearMuralText()
    {
        aboutOpen = false;
        if (infoPanel != null)
        {
            infoPanel.ClearAll();
        }
    }

    private void CloseAbout()
    {
        if (aboutOpen && infoPanel != null)
        {
            infoPanel.HideInfo();
        }
        aboutOpen = false;
    }

    // ---------- Spawner events ----------

    private void OnMuralFound(MuralExperience mural)
    {
        if (CurrentScreen != AppScreen.Scanning && CurrentScreen != AppScreen.AR)
        {
            return;
        }

        // A different mural: clear the text the previous one left (its info panel and caption).
        if (currentMural != mural)
        {
            ClearMuralText();
        }
        currentMural = mural;
        lostMural = null;

        MuralData data = mural.data;
        if (data != null)
        {
            visited.Add(data.imageName);
            muralTitle.text = MuralName(data);
            muralLocation.text = data.location;
        }
        ShowScreen(AppScreen.AR);
    }

    private void OnMuralLost(MuralExperience mural)
    {
        if (CurrentScreen != AppScreen.AR || mural != currentMural)
        {
            return;
        }
        lostMural = mural;
        StartCoroutine(BackToScanLater(mural));
    }

    // The mural fades while tracking is lost. If it does not come back, the scanning hint returns.
    private IEnumerator BackToScanLater(MuralExperience mural)
    {
        yield return new WaitForSeconds(lostToScanSeconds);
        // Only if the mural actually paused for the loss. A mural that keeps playing on purpose
        // (FlyingCars during its full-screen savanna story) is not interrupted by the scanning screen.
        if (lostMural == mural && CurrentScreen == AppScreen.AR && mural != null && mural.IsPaused)
        {
            scanTitle.text = "Point your camera back at the mural";
            ShowScreen(AppScreen.Scanning);
        }
    }

    // ---------- Helpers ----------

    private void SetSpawnerOn(bool on)
    {
        if (spawner != null)
        {
            spawner.enabled = on;
        }
    }

    private static string MuralName(MuralData data)
    {
        return string.IsNullOrEmpty(data.displayTitle) ? data.imageName : data.displayTitle;
    }

    // Phones without ARCore cannot show the murals, so the Start button explains why instead.
    private IEnumerator CheckARSupport()
    {
#if UNITY_EDITOR
        yield break;
#else
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        if (ARSession.state == ARSessionState.Unsupported)
        {
            startStatus.text = "This phone does not support ARCore, so the murals cannot be shown.";
            startButton.interactable = false;
        }
#endif
    }

    // Keeps buttons away from the notch and the rounded corners.
    private void ApplySafeArea()
    {
        Rect area = Screen.safeArea;
        if (area == appliedSafeArea || Screen.width == 0 || Screen.height == 0)
        {
            return;
        }
        appliedSafeArea = area;
        foreach (RectTransform content in safeAreas)
        {
            content.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            content.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
        }
    }

    // ---------- Layout ----------

    private void BuildLayout()
    {
        GameObject canvasObject = new GameObject("AppCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot = canvasObject.transform;

        BuildStartScreen();
        BuildScanScreen();
        BuildARScreen();
        BuildExitScreen();
    }

    // A screen fills the whole display. Backgrounds go on the screen itself; buttons and text go in
    // the returned content area, which stays inside the safe area (away from the notch).
    private GameObject MakeScreen(string name, out RectTransform content)
    {
        RectTransform screen = MuralTheme.MakeRect(name, canvasRoot);
        MuralTheme.Stretch(screen, 0f, 0f, 0f, 0f);
        content = MuralTheme.MakeRect("Content", screen);
        MuralTheme.Stretch(content, 0f, 0f, 0f, 0f);
        safeAreas.Add(content);
        return screen.gameObject;
    }

    private static Color Deep(float alpha)
    {
        return new Color(MuralTheme.EarthDeep.r, MuralTheme.EarthDeep.g, MuralTheme.EarthDeep.b, alpha);
    }

    // Start: the rock art pattern, mudcloth borders at the top and bottom, and a card in the middle.
    private void BuildStartScreen()
    {
        RectTransform content;
        startScreen = MakeScreen("StartScreen", out content);
        Transform screen = startScreen.transform;

        RawImage pattern = MuralTheme.MakePattern("RockArt", screen, "Theme_RockArt", PatternFill.FillMode.RepeatBoth, MuralTheme.RockArtAspect, 620f);
        MuralTheme.Stretch(pattern.rectTransform, 0f, 0f, 0f, 0f);
        pattern.raycastTarget = true;
        pattern.transform.SetSiblingIndex(0);
        RawImage fade = MuralTheme.MakeFade("Fade", screen, Deep(0.85f));
        MuralTheme.Stretch(fade.rectTransform, 0f, 0f, 0f, 0f);
        fade.transform.SetSiblingIndex(1);
        AddScreenBorders(screen);
        content.SetAsLastSibling();

        RectTransform card = MuralTheme.MakeCard("Card", content, Deep(0.95f), MuralTheme.Terracotta, 5f, 44f);
        MuralTheme.Place((RectTransform)card.parent, 0.5f, 0.48f, 940f, 1060f);

        TMP_Text label = MuralTheme.MakeText("Label", card, "ALU CAMPUS", 34f, true, MuralTheme.Gold);
        label.characterSpacing = 24f;
        MuralTheme.PinTop(label.rectTransform, 80f, 60f, 40f);

        TMP_Text title = MuralTheme.MakeText("Title", card, appTitle, 120f, true, MuralTheme.Bone);
        MuralTheme.PinTop(title.rectTransform, 150f, 170f, 40f);

        RawImage divider = MuralTheme.MakeBand("Divider", card);
        MuralTheme.PinTop(divider.rectTransform, 345f, 64f, 90f);

        TMP_Text welcome = MuralTheme.MakeText("Welcome", card, welcomeText, 40f, false, MuralTheme.BoneSoft);
        MuralTheme.PinTop(welcome.rectTransform, 445f, 170f, 80f);

        int total = spawner != null ? spawner.murals.Count : 5;
        TMP_Text count = MuralTheme.MakeText("Count", card, total + " murals to discover", 34f, true, MuralTheme.Gold);
        MuralTheme.PinTop(count.rectTransform, 625f, 60f, 60f);

        startButton = MuralTheme.MakeButton("StartButton", card, "Start exploring", true, OnStartPressed);
        MuralTheme.PinBottom((RectTransform)startButton.transform, 140f, 150f, 130f);

        startStatus = MuralTheme.MakeText("Status", card, "", 30f, false, new Color(1f, 0.66f, 0.58f));
        MuralTheme.PinBottom(startStatus.rectTransform, 30f, 100f, 60f);
    }

    // Scanning: the camera stays visible. A gold viewfinder in the middle and a hint card at the bottom.
    private void BuildScanScreen()
    {
        RectTransform content;
        scanScreen = MakeScreen("ScanScreen", out content);

        // Four corner brackets around the middle of the screen, like a camera viewfinder.
        float size = 760f;
        float arm = 150f;
        float thickness = 14f;
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -1f : 1f;
            float y = (i < 2) ? 1f : -1f;
            Vector2 corner = new Vector2(x * size * 0.5f, y * size * 0.5f);
            AddCornerBar(content, corner + new Vector2(-x * (arm - thickness) * 0.5f, 0f), new Vector2(arm, thickness));
            AddCornerBar(content, corner + new Vector2(0f, -y * (arm - thickness) * 0.5f), new Vector2(thickness, arm));
        }

        Button exit = MuralTheme.MakeButton("ExitButton", content, "Exit", false, OnExitPressed);
        PlaceTopRight((RectTransform)exit.transform, 220f, 110f);

        RectTransform card = MuralTheme.MakeCard("HintCard", content, Deep(0.92f), MuralTheme.Terracotta, 4f, 36f);
        MuralTheme.PinBottom((RectTransform)card.parent, 60f, 300f, 40f);

        RawImage band = MuralTheme.MakeBand("Band", card);
        MuralTheme.PinTop(band.rectTransform, 24f, 50f, 60f);

        scanTitle = MuralTheme.MakeText("Title", card, "", 44f, true, MuralTheme.Bone);
        MuralTheme.PinTop(scanTitle.rectTransform, 90f, 80f, 30f);

        TMP_Text hint = MuralTheme.MakeText("Hint", card, "Stand a few steps back so the whole mural fits on the screen.", 32f, false, MuralTheme.BoneSoft);
        MuralTheme.PinTop(hint.rectTransform, 170f, 100f, 50f);
    }

    private void AddCornerBar(Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = MuralTheme.MakeRect("Corner", parent);
        rect.anchorMin = new Vector2(0.5f, 0.56f);
        rect.anchorMax = new Vector2(0.5f, 0.56f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        MuralTheme.SetRounded(image, MuralTheme.Gold, 6f);
        scanCorners.Add(image);
    }

    // AR: a card at the top with the mural name, its place on campus and the buttons.
    // The bottom of the screen is left free for captions and stories.
    private void BuildARScreen()
    {
        RectTransform content;
        arScreen = MakeScreen("ARScreen", out content);

        RectTransform card = MuralTheme.MakeCard("TopCard", content, Deep(0.88f), MuralTheme.Terracotta, 4f, 36f);
        MuralTheme.PinTop((RectTransform)card.parent, 24f, 360f, 24f);

        muralTitle = MuralTheme.MakeText("MuralTitle", card, "", 54f, true, MuralTheme.Gold);
        muralTitle.alignment = TextAlignmentOptions.Left;
        muralTitle.textWrappingMode = TextWrappingModes.NoWrap;
        muralTitle.overflowMode = TextOverflowModes.Ellipsis;
        MuralTheme.PinTop(muralTitle.rectTransform, 26f, 76f, 36f);
        muralTitle.rectTransform.offsetMax = new Vector2(-270f, muralTitle.rectTransform.offsetMax.y);

        muralLocation = MuralTheme.MakeText("MuralLocation", card, "", 30f, false, MuralTheme.BoneSoft);
        muralLocation.alignment = TextAlignmentOptions.Left;
        muralLocation.textWrappingMode = TextWrappingModes.NoWrap;
        muralLocation.overflowMode = TextOverflowModes.Ellipsis;
        muralLocation.enableAutoSizing = true;
        muralLocation.fontSizeMin = 22f;
        muralLocation.fontSizeMax = 30f;
        MuralTheme.PinTop(muralLocation.rectTransform, 104f, 50f, 36f);
        muralLocation.rectTransform.offsetMax = new Vector2(-270f, muralLocation.rectTransform.offsetMax.y);

        Button exit = MuralTheme.MakeButton("ExitButton", card, "Exit", false, OnExitPressed);
        PlaceTopRight((RectTransform)exit.transform, 210f, 100f);

        Button about = MuralTheme.MakeButton("AboutButton", card, "About", true, OnAboutPressed);
        RectTransform aboutRect = (RectTransform)about.transform;
        aboutRect.anchorMin = new Vector2(0f, 0f);
        aboutRect.anchorMax = new Vector2(0.5f, 0f);
        aboutRect.offsetMin = new Vector2(30f, 62f);
        aboutRect.offsetMax = new Vector2(-12f, 172f);

        Button replay = MuralTheme.MakeButton("ReplayButton", card, "Replay", false, OnReplayPressed);
        RectTransform replayRect = (RectTransform)replay.transform;
        replayRect.anchorMin = new Vector2(0.5f, 0f);
        replayRect.anchorMax = new Vector2(1f, 0f);
        replayRect.offsetMin = new Vector2(12f, 62f);
        replayRect.offsetMax = new Vector2(-30f, 172f);

        RawImage band = MuralTheme.MakeBand("Band", card);
        MuralTheme.PinBottom(band.rectTransform, 14f, 34f, 40f);
    }

    // Exit: the cut-out pattern in dark earth tones, the same borders as Start, and a thank-you card.
    private void BuildExitScreen()
    {
        RectTransform content;
        exitScreen = MakeScreen("ExitScreen", out content);
        Transform screen = exitScreen.transform;

        RawImage pattern = MuralTheme.MakePattern("Cutouts", screen, "Theme_Cutouts", PatternFill.FillMode.RepeatBoth, MuralTheme.CutoutsAspect, 1900f);
        MuralTheme.Stretch(pattern.rectTransform, 0f, 0f, 0f, 0f);
        pattern.raycastTarget = true;
        pattern.transform.SetSiblingIndex(0);
        RawImage fade = MuralTheme.MakeFade("Fade", screen, Deep(0.7f));
        MuralTheme.Stretch(fade.rectTransform, 0f, 0f, 0f, 0f);
        fade.transform.SetSiblingIndex(1);
        AddScreenBorders(screen);
        content.SetAsLastSibling();

        RectTransform card = MuralTheme.MakeCard("Card", content, Deep(0.95f), MuralTheme.Terracotta, 5f, 44f);
        MuralTheme.Place((RectTransform)card.parent, 0.5f, 0.48f, 940f, 1100f);

        TMP_Text label = MuralTheme.MakeText("Label", card, "SEE YOU AGAIN", 34f, true, MuralTheme.Gold);
        label.characterSpacing = 24f;
        MuralTheme.PinTop(label.rectTransform, 80f, 60f, 40f);

        TMP_Text title = MuralTheme.MakeText("Title", card, "Thanks for visiting", 84f, true, MuralTheme.Bone);
        MuralTheme.PinTop(title.rectTransform, 150f, 200f, 50f);

        RawImage divider = MuralTheme.MakeBand("Divider", card);
        MuralTheme.PinTop(divider.rectTransform, 370f, 64f, 90f);

        exitSummary = MuralTheme.MakeText("Summary", card, "", 36f, false, MuralTheme.BoneSoft);
        MuralTheme.PinTop(exitSummary.rectTransform, 465f, 190f, 80f);

        Button again = MuralTheme.MakeButton("ScanAgainButton", card, "Scan again", true, OnScanAgainPressed);
        MuralTheme.PinBottom((RectTransform)again.transform, 200f, 140f, 130f);

        Button close = MuralTheme.MakeButton("CloseButton", card, "Close app", false, OnClosePressed);
        MuralTheme.PinBottom((RectTransform)close.transform, 44f, 130f, 130f);
    }

    // Mudcloth borders along the top and bottom edges of a full screen, each with a thin terracotta line.
    private void AddScreenBorders(Transform screen)
    {
        RawImage top = MuralTheme.MakeBand("TopBorder", screen);
        MuralTheme.PinTop(top.rectTransform, 0f, 120f, 0f);
        RectTransform topLine = MuralTheme.MakeRect("TopLine", screen);
        MuralTheme.PinTop(topLine, 120f, 8f, 0f);
        topLine.gameObject.AddComponent<Image>().color = MuralTheme.Terracotta;

        RawImage bottom = MuralTheme.MakeBand("BottomBorder", screen);
        MuralTheme.PinBottom(bottom.rectTransform, 0f, 120f, 0f);
        RectTransform bottomLine = MuralTheme.MakeRect("BottomLine", screen);
        MuralTheme.PinBottom(bottomLine, 120f, 8f, 0f);
        bottomLine.gameObject.AddComponent<Image>().color = MuralTheme.Terracotta;
    }

    private void PlaceTopRight(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(-28f, -28f);
    }
}
