using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuralAR
{
    [Serializable]
    public struct MuralCard
    {
        public string title;
        public string location;
        public string about;
        public string whatHappens;
        public string whereToStand;
        public string story;
        public bool isThisMural;
    }

    public sealed class FlyingCarsUI : MonoBehaviour
    {
        public static readonly Color Terracotta = new(0.81f, 0.48f, 0.19f, 1f);
        public static readonly Color Earth = new(0.17f, 0.12f, 0.1f, 0.94f);
        public static readonly Color EarthLight = new(0.28f, 0.2f, 0.16f, 0.96f);
        public static readonly Color Bone = new(0.96f, 0.94f, 0.91f, 1f);
        public static readonly Color Gold = new(1f, 0.8f, 0.42f, 1f);

        [SerializeField] MuralTracker tracker;
        [SerializeField] ExperienceController experience;
        [SerializeField, Tooltip("Terracotta rock-art pattern (header bands).")] Texture2D rockArt;
        [SerializeField, Tooltip("Black and white mud-cloth pattern (accents).")] Texture2D mudCloth;
        [SerializeField, Tooltip("Black and white geometric pattern (footer band).")] Texture2D geometric;

        public event Func<IReadOnlyList<MuralCard>> CardsRequested;

        public bool GalleryOpen => _gallery != null && _gallery.activeSelf;
        public bool BarVisible => _bar != null && _bar.activeSelf;
        public Button MuralsButton { get; private set; }
        public Button CallButton { get; private set; }
        public IReadOnlyList<Button> CardButtons => _cardButtons;

        GameObject _bar;
        GameObject _hint;
        TMP_Text _hintText;
        GameObject _gallery;
        GameObject _list;
        GameObject _detail;
        RectTransform _listContent;
        TMP_Text _detailTitle;
        TMP_Text _detailBody;
        readonly List<Button> _cardButtons = new();
        bool _visible;

        void Awake()
        {
            Build();
            _bar.SetActive(false);
            _gallery.SetActive(false);
        }

        void OnEnable()
        {
            tracker.OnMuralFound += HandleFound;
            tracker.OnMuralRelocalized += HandleRelocalized;
            tracker.OnMuralLost += HandleLost;
            experience.OnStateChanged += HandleState;
        }

        void OnDisable()
        {
            tracker.OnMuralFound -= HandleFound;
            tracker.OnMuralRelocalized -= HandleRelocalized;
            tracker.OnMuralLost -= HandleLost;
            experience.OnStateChanged -= HandleState;
        }

        void HandleFound(Pose pose) => SetVisible(true);

        void HandleRelocalized() => SetVisible(true);

        void HandleLost()
        {
            if (experience.State == ExperienceState.Savanna) return;
            SetVisible(false);
        }

        void HandleState(ExperienceState state) => Refresh();

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible) CloseGallery();
            Refresh();
        }

        void Refresh()
        {
            var state = experience.State;
            bool immersed = state == ExperienceState.Savanna;
            _bar.SetActive(_visible && !immersed);
            if (!_bar.activeSelf || !CallButton.gameObject.activeSelf) _hint.SetActive(false);
            CallButton.gameObject.SetActive(state is ExperienceState.Awakening or ExperienceState.CarsActive);
            if (immersed) CloseGallery();
        }

        public void OpenGallery()
        {
            var cards = CardsRequested?.Invoke() ?? Array.Empty<MuralCard>();
            foreach (Transform child in _listContent) Destroy(child.gameObject);
            _cardButtons.Clear();
            foreach (var card in cards) _cardButtons.Add(MakeCardButton(card));
            _list.SetActive(true);
            _detail.SetActive(false);
            _gallery.SetActive(true);
        }

        public void CloseGallery()
        {
            if (_gallery != null) _gallery.SetActive(false);
        }

        void ShowCard(MuralCard card)
        {
            _detailTitle.text = card.title;
            var body = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(card.story)) body.Append($"<color=#F2B266><b>The story</b></color>\n{card.story}\n\n");
            if (!string.IsNullOrEmpty(card.location)) body.Append($"<color=#F2B266><b>Where</b></color>\n{card.location}\n\n");
            if (!string.IsNullOrEmpty(card.about)) body.Append($"<color=#F2B266><b>About</b></color>\n{card.about}\n\n");
            if (!string.IsNullOrEmpty(card.whatHappens)) body.Append($"<color=#F2B266><b>What happens</b></color>\n{card.whatHappens}\n\n");
            if (!string.IsNullOrEmpty(card.whereToStand)) body.Append($"<color=#F2B266><b>Where to stand</b></color>\n{card.whereToStand}");
            _detailBody.text = body.ToString().TrimEnd();
            _list.SetActive(false);
            _detail.SetActive(true);
        }

        public bool HowToCallOpen => _hint != null && _hint.activeSelf;

        void ShowHowToCall()
        {
            _hintText.text = tracker.Config.howToCall;
            _hint.SetActive(true);
        }

        // ---------- Layout ----------

        void Build()
        {
            var barCanvas = MakeCanvas("FlyingCars Bar", 25);
            // The bar sits just under the app's header card (which ends 384 units below the top of the
            // safe area), inside the same safe area, so it never covers About, Replay or Exit.
            var safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(barCanvas, false);
            var safeRect = (RectTransform)safeArea.transform;
            Rect area = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0)
            {
                safeRect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safeRect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            }
            else
            {
                safeRect.anchorMin = Vector2.zero;
                safeRect.anchorMax = Vector2.one;
            }
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            _bar = new GameObject("Bar", typeof(RectTransform));
            _bar.transform.SetParent(safeArea.transform, false);
            Stretch((RectTransform)_bar.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -520f), new Vector2(-30f, -410f));
            MuralsButton = MakeButton(_bar.transform, "MuralsButton", "Murals", new Vector2(0f, 0f), new Vector2(0.38f, 1f), OpenGallery, primary: false);
            BlackAndWhite(MuralsButton);
            CallButton = MakeButton(_bar.transform, "CallButton", "How to call AMANIRENAS", new Vector2(0.4f, 0f), new Vector2(1f, 1f), ShowHowToCall, primary: true);

            _hint = Panel(barCanvas, "HowToCall", new Vector2(60f, 700f), new Vector2(-60f, -700f));
            Band(_hint.transform, "Pattern", rockArt, top: true, height: 90f, new Rect(0f, 0f, 3f, 0.15f));
            var hintTitle = Text(_hint.transform, "Title", 46f, FontStyles.Bold, Gold, TextAlignmentOptions.Center);
            Stretch(hintTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -170f), new Vector2(-20f, -95f));
            hintTitle.text = "How to call AMANIRENAS";
            _hintText = Text(_hint.transform, "Text", 38f, FontStyles.Normal, Bone, TextAlignmentOptions.Center);
            Fill(_hintText.rectTransform, 40f, 180f, 40f, 140f);
            _hintText.enableAutoSizing = true;
            _hintText.fontSizeMin = 26f;
            _hintText.fontSizeMax = 38f;
            var gotIt = MakeButton(_hint.transform, "GotItButton", "Got it", new Vector2(0.3f, 0f), new Vector2(0.7f, 0f), () => _hint.SetActive(false), primary: true);
            var gotItRect = (RectTransform)gotIt.transform;
            gotItRect.offsetMin = new Vector2(0f, 30f);
            gotItRect.offsetMax = new Vector2(0f, 120f);
            _hint.SetActive(false);

            var galleryCanvas = MakeCanvas("FlyingCars Gallery", 30);
            _gallery = Panel(galleryCanvas, "Gallery", new Vector2(30f, 40f), new Vector2(-30f, -60f));
            var header = Band(_gallery.transform, "Header", rockArt, top: true, height: 190f, new Rect(0f, 0f, 3f, 0.3f));
            var heading = Text(header, "Title", 64f, FontStyles.Bold, Bone, TextAlignmentOptions.Center);
            heading.text = "The Murals";
            Fill(heading.rectTransform, 0f, 0f, 0f, 0f);
            var shade = heading.gameObject.AddComponent<Shadow>();
            shade.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shade.effectDistance = new Vector2(3f, -3f);
            Band(_gallery.transform, "Footer", geometric, top: false, height: 70f, new Rect(0f, 0f, 3f, 0.12f));

            _list = Section("List");
            var listArea = new GameObject("Area", typeof(RectTransform));
            listArea.transform.SetParent(_list.transform, false);
            Fill((RectTransform)listArea.transform, 0f, 0f, 0f, 130f);
            _listContent = ScrollArea(listArea.transform, spacing: 20f);
            var closeList = MakeButton(_list.transform, "CloseButton", "Close", new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), CloseGallery, primary: true);
            ((RectTransform)closeList.transform).offsetMax = new Vector2(0f, 105f);

            _detail = Section("Detail");
            _detailTitle = Text(_detail.transform, "Title", 56f, FontStyles.Bold, Gold, TextAlignmentOptions.TopLeft);
            Stretch(_detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -90f), new Vector2(-10f, 0f));
            var bodyHolder = new GameObject("Body", typeof(RectTransform));
            bodyHolder.transform.SetParent(_detail.transform, false);
            Fill((RectTransform)bodyHolder.transform, 10f, 100f, 10f, 130f);
            var bodyContent = ScrollArea(bodyHolder.transform, spacing: 0f);
            _detailBody = Text(bodyContent, "Text", 38f, FontStyles.Normal, Bone, TextAlignmentOptions.TopLeft);
            _detailBody.gameObject.AddComponent<LayoutElement>();
            var back = MakeButton(_detail.transform, "BackButton", "Back", new Vector2(0f, 0f), new Vector2(0.48f, 0f), () => { _detail.SetActive(false); _list.SetActive(true); }, primary: false);
            ((RectTransform)back.transform).offsetMax = new Vector2(0f, 105f);
            var closeDetail = MakeButton(_detail.transform, "CloseButton", "Close", new Vector2(0.52f, 0f), new Vector2(1f, 0f), CloseGallery, primary: true);
            ((RectTransform)closeDetail.transform).offsetMax = new Vector2(0f, 105f);
        }

        GameObject Section(string name)
        {
            var section = new GameObject(name, typeof(RectTransform));
            section.transform.SetParent(_gallery.transform, false);
            Fill((RectTransform)section.transform, 30f, 220f, 30f, 95f);
            return section;
        }

        Button MakeCardButton(MuralCard card)
        {
            var item = new GameObject(card.title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            item.transform.SetParent(_listContent, false);
            item.GetComponent<LayoutElement>().preferredHeight = 170f;
            var image = item.GetComponent<Image>();
            image.color = card.isThisMural ? new Color(0.36f, 0.22f, 0.13f, 1f) : EarthLight;
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            Tint(button, image.color);
            var copy = card;
            button.onClick.AddListener(() => ShowCard(copy));

            var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(RawImage));
            stripe.transform.SetParent(item.transform, false);
            var stripeImage = stripe.GetComponent<RawImage>();
            stripeImage.texture = card.isThisMural ? rockArt : mudCloth;
            stripeImage.uvRect = new Rect(0f, 0f, 0.15f, 1f);
            stripeImage.raycastTarget = false;
            Stretch((RectTransform)stripe.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(34f, 0f));

            var title = Text(item.transform, "Title", 46f, FontStyles.Bold, Gold, TextAlignmentOptions.BottomLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(64f, 0f), new Vector2(-90f, -14f));
            title.text = card.isThisMural ? card.title + "  <size=30><color=#F4EFE8>(here)</color></size>" : card.title;
            var where = Text(item.transform, "Location", 30f, FontStyles.Normal, Bone, TextAlignmentOptions.TopLeft);
            Stretch(where.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(64f, 14f), new Vector2(-90f, -4f));
            where.text = string.IsNullOrEmpty(card.location) ? "Location coming soon" : card.location;
            var arrow = Text(item.transform, "Arrow", 60f, FontStyles.Bold, Terracotta, TextAlignmentOptions.Center);
            Stretch(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-90f, 0f), new Vector2(-10f, 0f));
            arrow.text = "›";
            return button;
        }

        Transform MakeCanvas(string name, int order)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject.transform;
        }

        GameObject Panel(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = Earth;
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = Terracotta;
            outline.effectDistance = new Vector2(4f, -4f);
            return panel;
        }

        Transform Band(Transform parent, string name, Texture2D texture, bool top, float height, Rect uv)
        {
            var band = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            band.transform.SetParent(parent, false);
            var image = band.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            var rect = (RectTransform)band.transform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.offsetMin = new Vector2(0f, top ? -height : 0f);
            rect.offsetMax = new Vector2(0f, top ? 0f : height);
            return band.transform;
        }

        RectTransform ScrollArea(Transform parent, float spacing)
        {
            var view = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            view.transform.SetParent(parent, false);
            Fill((RectTransform)view.transform, 0f, 0f, 0f, 0f);
            view.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.004f);
            view.GetComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(view.transform, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.GetComponent<ScrollRect>();
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return rect;
        }

        Button MakeButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick, bool primary)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = primary ? Terracotta : Earth;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = primary ? new Color(0.23f, 0.17f, 0.15f, 1f) : Terracotta;
            outline.effectDistance = new Vector2(3f, -3f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            Tint(button, image.color);
            button.onClick.AddListener(onClick);
            var text = Text(go.transform, "Label", 42f, FontStyles.Bold, Bone, TextAlignmentOptions.Center);
            Fill(text.rectTransform, 10f, 0f, 10f, 0f);
            text.text = label;
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 42f;
            return button;
        }

        void BlackAndWhite(Button button)
        {
            var image = (Image)button.targetGraphic;
            image.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            var outline = button.GetComponent<Outline>();
            outline.effectColor = Bone;
            outline.effectDistance = new Vector2(3f, -3f);
            var pattern = new GameObject("Pattern", typeof(RectTransform), typeof(RawImage));
            pattern.transform.SetParent(button.transform, false);
            pattern.transform.SetAsFirstSibling();
            var raw = pattern.GetComponent<RawImage>();
            raw.texture = geometric;
            raw.uvRect = new Rect(0f, 0f, 0.9f, 0.25f);
            raw.color = new Color(1f, 1f, 1f, 0.45f);
            raw.raycastTarget = false;
            Fill((RectTransform)pattern.transform, 0f, 0f, 0f, 0f);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            var labelBack = new GameObject("LabelBack", typeof(RectTransform), typeof(Image));
            labelBack.transform.SetParent(button.transform, false);
            labelBack.transform.SetSiblingIndex(1);
            var back = labelBack.GetComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.85f);
            back.raycastTarget = false;
            Fill((RectTransform)labelBack.transform, 50f, 22f, 50f, 22f);
        }

        static void Tint(Button button, Color normal)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.targetGraphic.color = normal;
        }

        static TMP_Text Text(Transform parent, string name, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static void Fill(RectTransform rect, float left, float top, float right, float bottom) =>
            Stretch(rect, Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
