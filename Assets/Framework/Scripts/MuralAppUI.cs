using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using TMPro;

// The four app screens: Start, Scanning, AR and Exit.
// Listens to MuralSpawner events, so mural scripts never need to know about these screens.
// Builds its own simple layout in code, like MuralInfoPanel.
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

    private static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.09f, 0.92f);
    private static readonly Color BarColor = new Color(0.04f, 0.06f, 0.09f, 0.75f);
    private static readonly Color AccentColor = new Color(1f, 0.82f, 0.45f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.27f, 0.95f);

    private RectTransform safeRoot;
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
        if (spawner != null)
        {
            spawner.ClearAll();
        }
        SetSpawnerOn(false);
        currentMural = null;
        lostMural = null;

        int total = spawner != null ? spawner.murals.Count : 0;
        exitSummary.text = "You explored " + visited.Count + " of " + total + " murals.\n\n"
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
            CloseAbout();
        }
    }

    private void OnAboutPressed()
    {
        if (aboutOpen)
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

        if (currentMural != mural)
        {
            CloseAbout();
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
        if (lostMural == mural && CurrentScreen == AppScreen.AR)
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
        safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
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

        GameObject safeObject = new GameObject("SafeArea", typeof(RectTransform));
        safeObject.transform.SetParent(canvasObject.transform, false);
        safeRoot = safeObject.GetComponent<RectTransform>();
        Stretch(safeRoot, 0f, 0f, 0f, 0f);

        BuildStartScreen();
        BuildScanScreen();
        BuildARScreen();
        BuildExitScreen();
    }

    private void BuildStartScreen()
    {
        startScreen = MakePanel("StartScreen", PanelColor);

        TMP_Text title = MakeText("Title", startScreen.transform, 96f, FontStyles.Bold, AccentColor);
        title.text = appTitle;
        Place(title.rectTransform, 0.5f, 0.68f, 960f, 160f);

        TMP_Text welcome = MakeText("Welcome", startScreen.transform, 44f, FontStyles.Normal, Color.white);
        welcome.text = welcomeText;
        Place(welcome.rectTransform, 0.5f, 0.55f, 900f, 220f);

        startButton = MakeButton("StartButton", startScreen.transform, "Start", OnStartPressed);
        Place(startButton.GetComponent<RectTransform>(), 0.5f, 0.38f, 560f, 150f);

        startStatus = MakeText("Status", startScreen.transform, 34f, FontStyles.Italic, new Color(1f, 0.6f, 0.55f));
        startStatus.text = "";
        Place(startStatus.rectTransform, 0.5f, 0.28f, 900f, 140f);
    }

    private void BuildScanScreen()
    {
        scanScreen = MakePanel("ScanScreen", new Color(0f, 0f, 0f, 0f));

        // Four corner brackets around the middle of the screen, like a camera viewfinder.
        float size = 760f;
        float arm = 140f;
        float thickness = 12f;
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -1f : 1f;
            float y = (i < 2) ? 1f : -1f;
            Vector2 corner = new Vector2(x * size * 0.5f, y * size * 0.5f);
            AddCornerBar(corner + new Vector2(-x * arm * 0.5f, 0f), new Vector2(arm, thickness));
            AddCornerBar(corner + new Vector2(0f, -y * arm * 0.5f), new Vector2(thickness, arm));
        }

        GameObject hintBox = MakeBox("HintBox", scanScreen.transform, BarColor);
        RectTransform hintRect = hintBox.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.offsetMin = new Vector2(40f, 80f);
        hintRect.offsetMax = new Vector2(-40f, 300f);

        scanTitle = MakeText("Title", hintBox.transform, 46f, FontStyles.Bold, Color.white);
        Stretch(scanTitle.rectTransform, 30f, 20f, 30f, 110f);

        TMP_Text scanHint = MakeText("Hint", hintBox.transform, 34f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f));
        scanHint.text = "Stand a few steps back so the whole mural fits on the screen.";
        Stretch(scanHint.rectTransform, 30f, 110f, 30f, 20f);

        Button exit = MakeButton("ExitButton", scanScreen.transform, "Exit", OnExitPressed);
        PlaceTopRight(exit.GetComponent<RectTransform>(), 240f, 110f);
    }

    private void AddCornerBar(Vector2 position, Vector2 size)
    {
        GameObject bar = new GameObject("Corner", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(scanScreen.transform, false);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.55f);
        rect.anchorMax = new Vector2(0.5f, 0.55f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = bar.GetComponent<Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
        scanCorners.Add(image);
    }

    private void BuildARScreen()
    {
        arScreen = MakePanel("ARScreen", new Color(0f, 0f, 0f, 0f));

        // Title bar at the top. The bottom of the screen is left free for captions and stories.
        GameObject bar = MakeBox("TopBar", arScreen.transform, BarColor);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.offsetMin = new Vector2(0f, -330f);
        barRect.offsetMax = new Vector2(0f, 0f);

        muralTitle = MakeText("MuralTitle", bar.transform, 54f, FontStyles.Bold, AccentColor);
        muralTitle.alignment = TextAlignmentOptions.Left;
        Stretch(muralTitle.rectTransform, 40f, 20f, 300f, 230f);

        muralLocation = MakeText("MuralLocation", bar.transform, 32f, FontStyles.Normal, new Color(0.85f, 0.88f, 0.92f));
        muralLocation.alignment = TextAlignmentOptions.Left;
        Stretch(muralLocation.rectTransform, 40f, 100f, 300f, 170f);

        Button exit = MakeButton("ExitButton", bar.transform, "Exit", OnExitPressed);
        PlaceTopRight(exit.GetComponent<RectTransform>(), 240f, 110f);

        Button about = MakeButton("AboutButton", bar.transform, "About", OnAboutPressed);
        RectTransform aboutRect = about.GetComponent<RectTransform>();
        aboutRect.anchorMin = new Vector2(0f, 0f);
        aboutRect.anchorMax = new Vector2(0.5f, 0f);
        aboutRect.offsetMin = new Vector2(40f, 30f);
        aboutRect.offsetMax = new Vector2(-15f, 140f);

        Button replay = MakeButton("ReplayButton", bar.transform, "Replay", OnReplayPressed);
        RectTransform replayRect = replay.GetComponent<RectTransform>();
        replayRect.anchorMin = new Vector2(0.5f, 0f);
        replayRect.anchorMax = new Vector2(1f, 0f);
        replayRect.offsetMin = new Vector2(15f, 30f);
        replayRect.offsetMax = new Vector2(-40f, 140f);
    }

    private void BuildExitScreen()
    {
        exitScreen = MakePanel("ExitScreen", PanelColor);

        TMP_Text title = MakeText("Title", exitScreen.transform, 80f, FontStyles.Bold, AccentColor);
        title.text = "Thanks for visiting";
        Place(title.rectTransform, 0.5f, 0.68f, 960f, 150f);

        exitSummary = MakeText("Summary", exitScreen.transform, 42f, FontStyles.Normal, Color.white);
        Place(exitSummary.rectTransform, 0.5f, 0.54f, 900f, 260f);

        Button again = MakeButton("ScanAgainButton", exitScreen.transform, "Scan again", OnScanAgainPressed);
        Place(again.GetComponent<RectTransform>(), 0.5f, 0.38f, 560f, 150f);

        Button close = MakeButton("CloseButton", exitScreen.transform, "Close app", OnClosePressed);
        Place(close.GetComponent<RectTransform>(), 0.5f, 0.28f, 560f, 150f);
    }

    // A screen that fills the safe area.
    private GameObject MakePanel(string name, Color color)
    {
        GameObject panel = MakeBox(name, safeRoot, color);
        Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        // A see-through screen must let taps reach the murals behind it.
        panel.GetComponent<Image>().raycastTarget = color.a > 0f;
        return panel;
    }

    private GameObject MakeBox(string name, Transform parent, Color color)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        Image image = box.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
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
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TMP_Text text = MakeText("Label", buttonObject.transform, 44f, FontStyles.Bold, Color.white);
        text.text = label;
        Stretch(text.rectTransform, 10f, 10f, 10f, 10f);
        return button;
    }

    // Centers a rect at a point given as a fraction of the screen (0 to 1), with a fixed size.
    private void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(x, y);
        rect.anchorMax = new Vector2(x, y);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
    }

    private void PlaceTopRight(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(-30f, -30f);
    }

    // Stretches a rect to fill its parent with padding (left, top, right, bottom).
    private void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
