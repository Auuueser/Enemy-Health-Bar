using Auuueser.EnemyHealthBars.Core.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using Auuueser.EnemyHealthBars.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class HealthBarView
{
    private const float FillPadding = 1f;
    private const float SideTextPadding = 6f;
    private const float TextFontSize = 18f;
    private static TMP_FontAsset? resolvedTextFont;
    private static bool textFontResolved;

    private readonly GameObject gameObject;
    private readonly RectTransform root;
    private readonly Image backgroundImage;
    private readonly Image fillImage;
    private readonly Outline border;
    private readonly RectTransform backgroundRect;
    private readonly RectTransform fillRect;
    private readonly TextMeshProUGUI healthText;
    private readonly RectTransform textRect;
    private readonly Canvas worldCanvas;
    private readonly HealthBarBillboard billboard;
    private Camera? lastAppliedCanvasCamera;
    private HealthBarStyle currentStyle;
    private int lastTextCurrentHealth = int.MinValue;
    private int lastTextMaxHealth = int.MinValue;
    private HealthTextFormat lastTextFormat = (HealthTextFormat)(-1);
    private HealthBarDisplayMode lastDisplayMode = (HealthBarDisplayMode)(-1);
    private HealthBarDisplayMode lastFillDisplayMode = (HealthBarDisplayMode)(-1);
    private float lastFillFraction = -1f;
    private bool lastShowEnemyName;
    private string? lastDisplayName;

    private HealthBarView(
        GameObject gameObject,
        RectTransform root,
        Image backgroundImage,
        Image fillImage,
        Outline border,
        RectTransform backgroundRect,
        RectTransform fillRect,
        TextMeshProUGUI healthText,
        RectTransform textRect,
        Canvas worldCanvas,
        HealthBarBillboard billboard,
        HealthBarStyle style)
    {
        this.gameObject = gameObject;
        this.root = root;
        this.backgroundImage = backgroundImage;
        this.fillImage = fillImage;
        this.border = border;
        this.backgroundRect = backgroundRect;
        this.fillRect = fillRect;
        this.healthText = healthText;
        this.textRect = textRect;
        this.worldCanvas = worldCanvas;
        this.billboard = billboard;
        currentStyle = style;
    }

    public static HealthBarView Create(Transform parent, HealthBarStyle style)
    {
        var gameObject = new GameObject("EnemyHealthBar");
        gameObject.transform.SetParent(parent, false);
        var billboard = gameObject.AddComponent<HealthBarBillboard>();
        var root = gameObject.AddComponent<RectTransform>();

        var worldCanvas = gameObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 500;
        gameObject.AddComponent<CanvasScaler>();

        var backgroundImage = CreateImage("Background", root, style.BackgroundColor);
        var backgroundRect = backgroundImage.GetComponent<RectTransform>();
        var border = backgroundImage.gameObject.AddComponent<Outline>();
        var fillImage = CreateImage("Fill", backgroundImage.transform, style.FillColor);
        var fillRect = fillImage.GetComponent<RectTransform>();
        var healthText = CreateHealthText(root, style.TextColor);
        var textRect = healthText.GetComponent<RectTransform>();

        SetLayerRecursively(gameObject, parent.gameObject.layer);

        var view = new HealthBarView(
            gameObject,
            root,
            backgroundImage,
            fillImage,
            border,
            backgroundRect,
            fillRect,
            healthText,
            textRect,
            worldCanvas,
            billboard,
            style);
        view.ApplyStyle(style);
        view.SetVisible(false);
        return view;
    }

    public void ApplyStyle(HealthBarStyle style)
    {
        currentStyle = style;
        root.localScale = Vector3.one * style.WorldScale;
        backgroundImage.color = style.BackgroundColor;
        border.effectColor = style.BorderColor;
        border.effectDistance = new Vector2(1f, -1f);
        fillImage.color = style.FillColor;
        healthText.color = style.TextColor;
        healthText.fontSize = TextFontSize;
        healthText.gameObject.SetActive(ShouldShowText(style));

        switch (style.DisplayMode)
        {
            case HealthBarDisplayMode.VerticalSideBar:
                ApplyVerticalSideLayout(style);
                break;
            case HealthBarDisplayMode.NumbersOnly:
                ApplyNumbersOnlyLayout(style);
                break;
            default:
                ApplyHorizontalLayout(style);
                break;
        }

        ResetTextCache();
    }

    public void SetFill(float fraction)
    {
        if (fraction < 0f)
        {
            fraction = 0f;
        }
        else if (fraction > 1f)
        {
            fraction = 1f;
        }

        if (lastFillFraction == fraction && lastFillDisplayMode == currentStyle.DisplayMode)
        {
            return;
        }

        if (currentStyle.DisplayMode == HealthBarDisplayMode.NumbersOnly)
        {
            SetActiveIfChanged(backgroundImage.gameObject, false);
            lastFillFraction = fraction;
            lastFillDisplayMode = currentStyle.DisplayMode;
            return;
        }

        SetActiveIfChanged(backgroundImage.gameObject, true);
        if (currentStyle.DisplayMode == HealthBarDisplayMode.VerticalSideBar)
        {
            fillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Lerp(0f, Mathf.Max(0f, currentStyle.SideBarHeight - FillPadding * 2f), fraction));
            lastFillFraction = fraction;
            lastFillDisplayMode = currentStyle.DisplayMode;
            return;
        }

        fillRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Lerp(0f, Mathf.Max(0f, currentStyle.Width - FillPadding * 2f), fraction));
        lastFillFraction = fraction;
        lastFillDisplayMode = currentStyle.DisplayMode;
    }

    public void SetHealth(EnemyHealthSnapshot snapshot)
    {
        SetFill(snapshot.HealthFraction);

        if (!ShouldShowText(currentStyle))
        {
            if (healthText.text.Length > 0)
            {
                healthText.text = string.Empty;
            }

            return;
        }

        var displayName = snapshot.DisplayName ?? string.Empty;
        if (lastTextCurrentHealth == snapshot.CurrentHealth &&
            lastTextMaxHealth == snapshot.MaxHealth &&
            lastTextFormat == currentStyle.HealthTextFormat &&
            lastDisplayMode == currentStyle.DisplayMode &&
            lastShowEnemyName == currentStyle.ShowEnemyName &&
            lastDisplayName == displayName)
        {
            return;
        }

        var text = HealthTextFormatter.Format(
            snapshot.CurrentHealth,
            snapshot.MaxHealth,
            currentStyle.HealthTextFormat);
        if (currentStyle.ShowEnemyName && displayName.Length > 0)
        {
            text = displayName + " " + text;
        }

        healthText.text = text;
        lastTextCurrentHealth = snapshot.CurrentHealth;
        lastTextMaxHealth = snapshot.MaxHealth;
        lastTextFormat = currentStyle.HealthTextFormat;
        lastDisplayMode = currentStyle.DisplayMode;
        lastShowEnemyName = currentStyle.ShowEnemyName;
        lastDisplayName = displayName;
    }

    public void SetWorldPosition(Vector3 worldPosition, Camera camera, Quaternion billboardRotation)
    {
        root.position = worldPosition;
        ApplyCanvasCamera(camera);
        billboard.ApplyRotation(billboardRotation);
    }

    public void SetVisible(bool visible)
    {
        SetActiveIfChanged(gameObject, visible);
    }

    private void ApplyHorizontalLayout(HealthBarStyle style)
    {
        root.sizeDelta = new Vector2(style.Width, style.Height);
        SetActiveIfChanged(backgroundImage.gameObject, true);

        SetRect(backgroundRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(style.Width, style.Height));
        SetRect(fillRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(FillPadding, 0f), new Vector2(0f, Mathf.Max(0f, style.Height - FillPadding * 2f)));
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
        SetFullTextRect();
    }

    private void ApplyVerticalSideLayout(HealthBarStyle style)
    {
        var sideTextWidth = GetVerticalSideTextWidth(style);
        var sideContentExtent = Mathf.Abs(style.SideBarHorizontalOffset) + style.SideBarWidth * 0.5f + SideTextPadding + sideTextWidth;
        var width = Mathf.Max(style.Width, sideContentExtent * 2f);
        var height = Mathf.Max(style.SideBarHeight, Mathf.Max(style.Height, TextFontSize + 6f));
        root.sizeDelta = new Vector2(width, height);
        SetActiveIfChanged(backgroundImage.gameObject, true);

        SetRect(
            backgroundRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(style.SideBarHorizontalOffset, 0f),
            new Vector2(style.SideBarWidth, style.SideBarHeight));
        SetRect(
            fillRect,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, FillPadding),
            new Vector2(Mathf.Max(0f, style.SideBarWidth - FillPadding * 2f), 0f));
        fillRect.pivot = new Vector2(0.5f, 0f);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        SetVerticalSideTextRect(style);
    }

    private void ApplyNumbersOnlyLayout(HealthBarStyle style)
    {
        root.sizeDelta = new Vector2(style.Width, Mathf.Max(style.Height, TextFontSize + 6f));
        SetActiveIfChanged(backgroundImage.gameObject, false);
        SetFullTextRect();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);

        imageObject.AddComponent<RectTransform>();
        imageObject.AddComponent<CanvasRenderer>();

        var image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TextMeshProUGUI CreateHealthText(Transform parent, Color color)
    {
        var textObject = new GameObject("HealthText");
        textObject.transform.SetParent(parent, false);

        textObject.AddComponent<RectTransform>();
        textObject.AddComponent<CanvasRenderer>();

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = TextFontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = TextFontSize;
        text.color = color;

        var font = ResolveTextFont();
        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static TMP_FontAsset? ResolveTextFont()
    {
        if (textFontResolved)
        {
            return resolvedTextFont;
        }

        textFontResolved = true;
        resolvedTextFont = TMP_Settings.defaultFontAsset;
        if (resolvedTextFont != null)
        {
            return resolvedTextFont;
        }

        var builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (builtinFont != null)
        {
            resolvedTextFont = TMP_FontAsset.CreateFontAsset(builtinFont);
        }

        return resolvedTextFont;
    }

    private void SetFullTextRect()
    {
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        healthText.alignment = TextAlignmentOptions.Center;
    }

    private void SetVerticalSideTextRect(HealthBarStyle style)
    {
        var side = style.SideBarHorizontalOffset >= 0f ? 1f : -1f;
        var textWidth = GetVerticalSideTextWidth(style);
        var textHeight = Mathf.Max(style.SideBarHeight, TextFontSize + 6f);
        var barOuterEdge = style.SideBarHorizontalOffset + side * style.SideBarWidth * 0.5f;
        var textCenterX = barOuterEdge + side * (SideTextPadding + textWidth * 0.5f);

        SetRect(
            textRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(textCenterX, 0f),
            new Vector2(textWidth, textHeight));
        healthText.alignment = side > 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
    }

    private static float GetVerticalSideTextWidth(HealthBarStyle style)
    {
        return Mathf.Max(style.Width, TextFontSize * 4f);
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static bool ShouldShowText(HealthBarStyle style)
    {
        return style.ShowHealthNumbers || style.DisplayMode == HealthBarDisplayMode.NumbersOnly;
    }

    private void ResetTextCache()
    {
        lastTextCurrentHealth = int.MinValue;
        lastTextMaxHealth = int.MinValue;
        lastTextFormat = (HealthTextFormat)(-1);
        lastDisplayMode = (HealthBarDisplayMode)(-1);
        lastFillDisplayMode = (HealthBarDisplayMode)(-1);
        lastFillFraction = -1f;
        lastShowEnemyName = false;
        lastDisplayName = null;
    }

    private void ApplyCanvasCamera(Camera camera)
    {
        billboard.SetCamera(camera);

        if (lastAppliedCanvasCamera == camera && worldCanvas.worldCamera == camera)
        {
            return;
        }

        worldCanvas.worldCamera = camera;
        lastAppliedCanvasCamera = camera;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        var transform = target.transform;
        for (var i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target.activeSelf == active)
        {
            return;
        }

        target.SetActive(active);
    }
}
