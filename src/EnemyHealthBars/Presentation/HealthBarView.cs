using System.Collections.Generic;
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
    private const float SegmentPadding = 2f;
    private const float SegmentGap = 2f;
    private const float TickThickness = 2f;
    private const int TickCount = 3;
    private const float SideTextPadding = 6f;
    private const float TextFontSize = 18f;
    private const float SideNumberWidth = TextFontSize * 3.4f;
    private static TMP_FontAsset? resolvedTextFont;
    private static bool textFontResolved;

    private readonly GameObject gameObject;
    private readonly RectTransform root;
    private readonly Image backgroundImage;
    private readonly Image trailImage;
    private readonly Image fillImage;
    private readonly Outline border;
    private readonly RectTransform backgroundRect;
    private readonly RectTransform trailRect;
    private readonly RectTransform fillRect;
    private readonly Image[] tickImages;
    private readonly List<Image> segmentImages = new(HealthBarSegments.MaxSegments);
    private readonly TextMeshProUGUI healthText;
    private readonly RectTransform textRect;
    private readonly Canvas worldCanvas;
    private readonly HealthBarBillboard billboard;
    private Camera? lastAppliedCanvasCamera;
    private HealthBarStyle currentStyle;
    private DamageTrail damageTrail;
    private int layoutSegmentCount = -1;
    private int lastBarCurrentHealth = int.MinValue;
    private int lastBarMaxHealth = int.MinValue;
    private int lastTextCurrentHealth = int.MinValue;
    private int lastTextMaxHealth = int.MinValue;
    private HealthTextFormat lastTextFormat = (HealthTextFormat)(-1);
    private HealthBarDisplayMode lastDisplayMode = (HealthBarDisplayMode)(-1);
    private HealthBarDisplayMode lastFillDisplayMode = (HealthBarDisplayMode)(-1);
    private float lastFillFraction = -1f;
    private bool lastShowEnemyName;
    private bool lastTextLowHealth;
    private string? lastDisplayName;

    private HealthBarView(
        GameObject gameObject,
        RectTransform root,
        Image backgroundImage,
        Image trailImage,
        Image fillImage,
        Image[] tickImages,
        Outline border,
        TextMeshProUGUI healthText,
        Canvas worldCanvas,
        HealthBarBillboard billboard,
        HealthBarStyle style)
    {
        this.gameObject = gameObject;
        this.root = root;
        this.backgroundImage = backgroundImage;
        this.trailImage = trailImage;
        this.fillImage = fillImage;
        this.tickImages = tickImages;
        this.border = border;
        this.healthText = healthText;
        this.worldCanvas = worldCanvas;
        this.billboard = billboard;
        backgroundRect = backgroundImage.rectTransform;
        trailRect = trailImage.rectTransform;
        fillRect = fillImage.rectTransform;
        textRect = healthText.rectTransform;
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
        var border = backgroundImage.gameObject.AddComponent<Outline>();
        var trailImage = CreateImage("DamageTrail", backgroundImage.transform, style.DamageTrailColor);
        var fillImage = CreateImage("Fill", backgroundImage.transform, style.FillColor);
        var tickImages = new Image[TickCount];
        for (var i = 0; i < TickCount; i++)
        {
            tickImages[i] = CreateImage("Tick", backgroundImage.transform, style.FrameColor);
        }

        var healthText = CreateHealthText(root, style.TextColor);

        SetLayerRecursively(gameObject, parent.gameObject.layer);

        var view = new HealthBarView(
            gameObject,
            root,
            backgroundImage,
            trailImage,
            fillImage,
            tickImages,
            border,
            healthText,
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
        backgroundImage.color = style.IsSegmented ? style.FrameColor : style.BackgroundColor;
        border.effectColor = style.BorderColor;
        border.effectDistance = new Vector2(1f, -1f);
        fillImage.color = style.IsSegmented ? style.SegmentFillColor : style.FillColor;
        trailImage.color = style.DamageTrailColor;
        healthText.color = style.TextColor;
        healthText.fontSize = TextFontSize;
        healthText.gameObject.SetActive(ShouldShowText(style));
        for (var i = 0; i < tickImages.Length; i++)
        {
            tickImages[i].color = style.FrameColor;
        }

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

        damageTrail.Clear();
        layoutSegmentCount = -1;
        SetActiveIfChanged(trailImage.gameObject, false);
        ShowSegmentLayout(0, showTicks: false);
        SetActiveIfChanged(fillImage.gameObject, true);
        ResetTextCache();
    }

    public void SetHealth(EnemyHealthSnapshot snapshot)
    {
        UpdateBar(snapshot.CurrentHealth, snapshot.MaxHealth, snapshot.HealthFraction);

        if (!ShouldShowText(currentStyle))
        {
            if (healthText.text.Length > 0)
            {
                healthText.text = string.Empty;
            }

            return;
        }

        var lowHealth = currentStyle.IsSegmented && HealthBarSegments.IsLowHealth(snapshot.CurrentHealth, snapshot.HealthFraction);
        if (lowHealth != lastTextLowHealth)
        {
            healthText.color = lowHealth ? currentStyle.LowHealthTextColor : currentStyle.TextColor;
            lastTextLowHealth = lowHealth;
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

    // Advances the damage trail; does nothing unless a trail is draining.
    public void Tick(float currentTime)
    {
        if (!damageTrail.IsActive || currentStyle.DisplayMode == HealthBarDisplayMode.NumbersOnly)
        {
            return;
        }

        RenderSegmentedBar(currentTime);
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

    private void UpdateBar(int currentHealth, int maxHealth, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);

        if (currentStyle.DisplayMode == HealthBarDisplayMode.NumbersOnly)
        {
            SetActiveIfChanged(backgroundImage.gameObject, false);
            lastFillFraction = fraction;
            lastFillDisplayMode = currentStyle.DisplayMode;
            return;
        }

        SetActiveIfChanged(backgroundImage.gameObject, true);
        if (!currentStyle.IsSegmented)
        {
            SetClassicFill(fraction);
            return;
        }

        var segmentCount = HealthBarSegments.ResolveSegmentCount(maxHealth);
        if (segmentCount != layoutSegmentCount)
        {
            LayoutSegments(segmentCount);
        }

        if (lastFillFraction == fraction &&
            lastFillDisplayMode == currentStyle.DisplayMode &&
            lastBarCurrentHealth == currentHealth &&
            lastBarMaxHealth == maxHealth)
        {
            return;
        }

        var currentTime = Time.unscaledTime;
        if (lastBarMaxHealth == maxHealth)
        {
            damageTrail.OnHealthChanged(lastFillFraction, fraction, currentTime);
        }
        else
        {
            damageTrail.Clear();
        }

        lastFillFraction = fraction;
        lastFillDisplayMode = currentStyle.DisplayMode;
        lastBarCurrentHealth = currentHealth;
        lastBarMaxHealth = maxHealth;
        RenderSegmentedBar(currentTime);
    }

    private void SetClassicFill(float fraction)
    {
        if (lastFillFraction == fraction && lastFillDisplayMode == currentStyle.DisplayMode)
        {
            return;
        }

        SetBarLength(fillRect, fraction, FillPadding);
        lastFillFraction = fraction;
        lastFillDisplayMode = currentStyle.DisplayMode;
    }

    private void RenderSegmentedBar(float currentTime)
    {
        var trailFraction = damageTrail.Evaluate(currentTime, lastFillFraction, out var trailStrength);
        var fillColor = HealthBarSegments.IsLowHealth(lastBarCurrentHealth, lastFillFraction)
            ? currentStyle.LowHealthFillColor
            : currentStyle.SegmentFillColor;

        if (layoutSegmentCount > 0)
        {
            var filledSegments = HealthBarSegments.ResolveFilledSegments(lastBarCurrentHealth, layoutSegmentCount);
            var trailSegments = damageTrail.IsActive ? Mathf.RoundToInt(damageTrail.FromFraction * layoutSegmentCount) : 0;
            var trailColor = Color.Lerp(currentStyle.EmptySegmentColor, currentStyle.DamageTrailColor, trailStrength);
            for (var i = 0; i < layoutSegmentCount; i++)
            {
                segmentImages[i].color = i < filledSegments
                    ? fillColor
                    : i < trailSegments ? trailColor : currentStyle.EmptySegmentColor;
            }

            return;
        }

        fillImage.color = fillColor;
        SetBarLength(fillRect, lastFillFraction, SegmentPadding);
        SetActiveIfChanged(trailImage.gameObject, damageTrail.IsActive);
        if (damageTrail.IsActive)
        {
            SetBarLength(trailRect, trailFraction, SegmentPadding);
        }
    }

    private void LayoutSegments(int segmentCount)
    {
        EnsureSegmentImages(segmentCount);
        layoutSegmentCount = segmentCount;
        lastFillFraction = -1f;
        damageTrail.Clear();
        ShowSegmentLayout(segmentCount, showTicks: segmentCount == 0);
        SetActiveIfChanged(fillImage.gameObject, segmentCount == 0);
        SetActiveIfChanged(trailImage.gameObject, false);

        var vertical = currentStyle.DisplayMode == HealthBarDisplayMode.VerticalSideBar;
        var barWidth = vertical ? currentStyle.SideBarWidth : currentStyle.Width;
        var barHeight = vertical ? currentStyle.SideBarHeight : currentStyle.Height;
        var crossSize = Mathf.Max(0f, (vertical ? barWidth : barHeight) - SegmentPadding * 2f);
        var innerLength = Mathf.Max(0f, (vertical ? barHeight : barWidth) - SegmentPadding * 2f);

        if (segmentCount > 0)
        {
            var segmentLength = Mathf.Max(1f, (innerLength - SegmentGap * (segmentCount - 1)) / segmentCount);
            for (var i = 0; i < segmentCount; i++)
            {
                var offset = SegmentPadding + i * (segmentLength + SegmentGap);
                PlaceAlongBar(segmentImages[i].rectTransform, vertical, offset, segmentLength, crossSize, 0f);
            }

            return;
        }

        for (var i = 0; i < tickImages.Length; i++)
        {
            var offset = SegmentPadding + innerLength * (i + 1) / (TickCount + 1);
            PlaceAlongBar(tickImages[i].rectTransform, vertical, offset, TickThickness, crossSize, 0.5f);
        }
    }

    private void ShowSegmentLayout(int segmentCount, bool showTicks)
    {
        for (var i = 0; i < segmentImages.Count; i++)
        {
            SetActiveIfChanged(segmentImages[i].gameObject, i < segmentCount);
        }

        for (var i = 0; i < tickImages.Length; i++)
        {
            SetActiveIfChanged(tickImages[i].gameObject, showTicks);
        }
    }

    private void EnsureSegmentImages(int segmentCount)
    {
        while (segmentImages.Count < segmentCount)
        {
            var segment = CreateImage("Segment", backgroundImage.transform, currentStyle.EmptySegmentColor);
            SetLayerRecursively(segment.gameObject, gameObject.layer);
            segmentImages.Add(segment);
        }
    }

    private void SetBarLength(RectTransform rect, float fraction, float padding)
    {
        if (currentStyle.DisplayMode == HealthBarDisplayMode.VerticalSideBar)
        {
            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Lerp(0f, Mathf.Max(0f, currentStyle.SideBarHeight - padding * 2f), fraction));
            return;
        }

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Lerp(0f, Mathf.Max(0f, currentStyle.Width - padding * 2f), fraction));
    }

    private void ApplyHorizontalLayout(HealthBarStyle style)
    {
        var padding = GetFillPadding(style);
        var numberBesideBar = style.IsSegmented && !style.ShowEnemyName && ShouldShowText(style);
        var sideTextExtent = numberBesideBar ? SideTextPadding + SideNumberWidth : 0f;
        // The bar itself stays centered on the enemy; the number hangs off its right edge.
        root.sizeDelta = new Vector2(style.Width + sideTextExtent * 2f, style.Height);
        SetActiveIfChanged(backgroundImage.gameObject, true);

        SetRect(backgroundRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(style.Width, style.Height));
        PlaceFillAlongBar(fillRect, vertical: false, padding, Mathf.Max(0f, style.Height - padding * 2f));
        PlaceFillAlongBar(trailRect, vertical: false, padding, Mathf.Max(0f, style.Height - padding * 2f));

        if (!style.IsSegmented)
        {
            SetFullTextRect();
            return;
        }

        if (numberBesideBar)
        {
            SetRect(
                textRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(style.Width * 0.5f + SideTextPadding + SideNumberWidth * 0.5f, 0f),
                new Vector2(SideNumberWidth, Mathf.Max(style.Height, TextFontSize + 6f)));
            healthText.alignment = TextAlignmentOptions.Left;
            return;
        }

        var textHeight = TextFontSize + 6f;
        SetRect(
            textRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, style.Height * 0.5f + 2f + textHeight * 0.5f),
            new Vector2(style.Width, textHeight));
        healthText.alignment = TextAlignmentOptions.Center;
    }

    private void ApplyVerticalSideLayout(HealthBarStyle style)
    {
        var padding = GetFillPadding(style);
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
        PlaceFillAlongBar(fillRect, vertical: true, padding, Mathf.Max(0f, style.SideBarWidth - padding * 2f));
        PlaceFillAlongBar(trailRect, vertical: true, padding, Mathf.Max(0f, style.SideBarWidth - padding * 2f));
        SetVerticalSideTextRect(style);
    }

    private void ApplyNumbersOnlyLayout(HealthBarStyle style)
    {
        root.sizeDelta = new Vector2(style.Width, Mathf.Max(style.Height, TextFontSize + 6f));
        SetActiveIfChanged(backgroundImage.gameObject, false);
        SetFullTextRect();
    }

    private static void PlaceFillAlongBar(RectTransform rect, bool vertical, float padding, float crossSize)
    {
        if (vertical)
        {
            SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, padding), new Vector2(crossSize, 0f));
            rect.pivot = new Vector2(0.5f, 0f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            return;
        }

        SetRect(rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(padding, 0f), new Vector2(0f, crossSize));
        rect.pivot = new Vector2(0f, 0.5f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
    }

    private static void PlaceAlongBar(RectTransform rect, bool vertical, float offset, float length, float crossSize, float pivotAlong)
    {
        if (vertical)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, pivotAlong);
            rect.anchoredPosition = new Vector2(0f, offset);
            rect.sizeDelta = new Vector2(crossSize, length);
            return;
        }

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(pivotAlong, 0.5f);
        rect.anchoredPosition = new Vector2(offset, 0f);
        rect.sizeDelta = new Vector2(length, crossSize);
    }

    private static float GetFillPadding(HealthBarStyle style)
    {
        return style.IsSegmented ? SegmentPadding : FillPadding;
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
        lastBarCurrentHealth = int.MinValue;
        lastBarMaxHealth = int.MinValue;
        lastTextFormat = (HealthTextFormat)(-1);
        lastDisplayMode = (HealthBarDisplayMode)(-1);
        lastFillDisplayMode = (HealthBarDisplayMode)(-1);
        lastFillFraction = -1f;
        lastShowEnemyName = false;
        lastTextLowHealth = false;
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
