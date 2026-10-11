using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// The look of the app: colours, fonts, pattern textures and the pieces every screen is built from.
// MuralAppUI and MuralInfoPanel both use it, so all screens match.
// Textures and fonts live in Assets/Framework/Resources/Theme.
public static class MuralTheme
{
    // Colours taken from the patterns: earth browns, terracotta, gold and bone.
    public static readonly Color Earth = new Color(0.17f, 0.12f, 0.1f);
    public static readonly Color EarthDeep = new Color(0.1f, 0.07f, 0.06f);
    public static readonly Color Terracotta = new Color(0.81f, 0.46f, 0.19f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.42f);
    public static readonly Color Bone = new Color(0.96f, 0.92f, 0.86f);
    public static readonly Color BoneSoft = new Color(0.96f, 0.92f, 0.86f, 0.8f);

    // Width divided by height of one tile of each pattern texture.
    public const float RockArtAspect = 1124f / 1103f;
    public const float BandAspect = 3.735f;
    public const float CutoutsAspect = 16f / 9f;

    private static TMP_FontAsset titleFont;
    private static TMP_FontAsset bodyFont;
    private static Sprite roundedSprite;
    private static Texture2D fadeTexture;

    // ---------- Assets ----------

    public static TMP_FontAsset TitleFont
    {
        get
        {
            if (titleFont == null)
            {
                titleFont = LoadFont("Theme/Poppins-SemiBold");
            }
            return titleFont;
        }
    }

    public static TMP_FontAsset BodyFont
    {
        get
        {
            if (bodyFont == null)
            {
                bodyFont = LoadFont("Theme/Poppins-Regular");
            }
            return bodyFont;
        }
    }

    // Builds a TextMeshPro font from the font file. Returns null (the TMP default font) if the file is missing.
    private static TMP_FontAsset LoadFont(string path)
    {
        Font font = Resources.Load<Font>(path);
        return font != null ? TMP_FontAsset.CreateFontAsset(font) : null;
    }

    public static Texture2D LoadPattern(string name)
    {
        Texture2D texture = Resources.Load<Texture2D>("Theme/" + name);
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
        }
        return texture;
    }

    // A white rounded rectangle drawn in code. Used sliced, so the corners stay round at any size.
    public static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite == null)
            {
                const int size = 128;
                const float radius = 48f;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Distance outside the rounded corner, smoothed over one pixel for clean edges.
                        float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                        float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                        float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                        byte alpha = (byte)(Mathf.Clamp01(0.5f - outside) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                Vector4 border = new Vector4(radius + 8f, radius + 8f, radius + 8f, radius + 8f);
                roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, border);
            }
            return roundedSprite;
        }
    }

    // Transparent at the top, solid at the bottom. Tinted to darken a background behind text.
    public static Texture2D FadeTexture
    {
        get
        {
            if (fadeTexture == null)
            {
                fadeTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
                fadeTexture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 64; y++)
                {
                    float alpha = 1f - y / 63f;
                    fadeTexture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha)));
                }
                fadeTexture.Apply();
            }
            return fadeTexture;
        }
    }

    // ---------- Building blocks ----------

    public static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    // A rounded card with a coloured border. Returns the inside, where the content goes.
    public static RectTransform MakeCard(string name, Transform parent, Color fill, Color border, float borderWidth, float radius)
    {
        RectTransform outer = MakeRect(name, parent);
        Image outerImage = outer.gameObject.AddComponent<Image>();
        SetRounded(outerImage, border, radius);

        RectTransform inner = MakeRect("Inside", outer);
        Stretch(inner, borderWidth, borderWidth, borderWidth, borderWidth);
        Image innerImage = inner.gameObject.AddComponent<Image>();
        SetRounded(innerImage, fill, Mathf.Max(radius - borderWidth, 4f));
        return inner;
    }

    public static void SetRounded(Image image, Color color, float radius)
    {
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 48f / radius;
        image.color = color;
        image.raycastTarget = false;
    }

    // Primary buttons are terracotta with a gold edge; secondary ones are dark with a terracotta edge.
    public static Button MakeButton(string name, Transform parent, string label, bool primary, UnityAction onClick)
    {
        RectTransform outer = MakeRect(name, parent);
        Image edge = outer.gameObject.AddComponent<Image>();
        SetRounded(edge, primary ? Gold : Terracotta, 30f);

        RectTransform inner = MakeRect("Fill", outer);
        Stretch(inner, 4f, 4f, 4f, 4f);
        Image fill = inner.gameObject.AddComponent<Image>();
        SetRounded(fill, primary ? Terracotta : new Color(EarthDeep.r, EarthDeep.g, EarthDeep.b, 0.92f), 26f);
        fill.raycastTarget = true;

        Button button = outer.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TMP_Text text = MakeText("Label", inner, label, 42f, true, Bone);
        Stretch(text.rectTransform, 12f, 6f, 12f, 6f);
        text.characterSpacing = 2f;
        return button;
    }

    public static TMP_Text MakeText(string name, Transform parent, string content, float size, bool title, Color color)
    {
        RectTransform rect = MakeRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = title ? TitleFont : BodyFont;
        if (font != null)
        {
            text.font = font;
        }
        else if (title)
        {
            text.fontStyle = FontStyles.Bold;
        }
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    // A strip of the mudcloth band, repeated sideways. Used as a border on cards and bars.
    public static RawImage MakeBand(string name, Transform parent)
    {
        return MakePattern(name, parent, "Theme_MudclothBand", PatternFill.FillMode.RepeatAcross, BandAspect, 0f);
    }

    public static RawImage MakePattern(string name, Transform parent, string texture, PatternFill.FillMode mode, float aspect, float tileSize)
    {
        RectTransform rect = MakeRect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = LoadPattern(texture);
        image.raycastTarget = false;
        if (image.texture == null)
        {
            image.color = Earth;
        }
        PatternFill fill = rect.gameObject.AddComponent<PatternFill>();
        fill.mode = mode;
        fill.tileAspect = aspect;
        fill.tileSize = tileSize;
        return image;
    }

    // Darkens the lower part of whatever is behind it, so text on top stays readable.
    public static RawImage MakeFade(string name, Transform parent, Color color)
    {
        RectTransform rect = MakeRect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = FadeTexture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ---------- Layout helpers ----------

    // Stretches a rect to fill its parent with padding (left, top, right, bottom).
    public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // Full width of the parent, a set height, pinned to the top (y measured down from the top edge).
    public static void PinTop(RectTransform rect, float top, float height, float side)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(side, -top - height);
        rect.offsetMax = new Vector2(-side, -top);
    }

    // Full width of the parent, a set height, pinned to the bottom (y measured up from the bottom edge).
    public static void PinBottom(RectTransform rect, float bottom, float height, float side)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(side, bottom);
        rect.offsetMax = new Vector2(-side, bottom + height);
    }

    // A fixed size, centred on a point given as a fraction of the parent (0 to 1).
    public static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(x, y);
        rect.anchorMax = new Vector2(x, y);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
    }
}
