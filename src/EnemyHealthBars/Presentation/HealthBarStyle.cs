using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Configuration;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class HealthBarStyle
{
    private const float PixelUnitScale = 140f;
    private const float WorldScaleMultiplier = 0.012f;

    private HealthBarStyle(
        float width,
        float height,
        float worldScale,
        HealthBarDisplayMode displayMode,
        bool showHealthNumbers,
        HealthTextFormat healthTextFormat,
        bool showEnemyName,
        float sideBarWidth,
        float sideBarHeight,
        float sideBarHorizontalOffset)
    {
        Width = width;
        Height = height;
        WorldScale = worldScale;
        DisplayMode = displayMode;
        ShowHealthNumbers = showHealthNumbers;
        HealthTextFormat = healthTextFormat;
        ShowEnemyName = showEnemyName;
        SideBarWidth = sideBarWidth;
        SideBarHeight = sideBarHeight;
        SideBarHorizontalOffset = sideBarHorizontalOffset;
    }

    public float Width { get; }

    public float Height { get; }

    public float WorldScale { get; }

    public HealthBarDisplayMode DisplayMode { get; }

    public bool ShowHealthNumbers { get; }

    public HealthTextFormat HealthTextFormat { get; }

    public bool ShowEnemyName { get; }

    public float SideBarWidth { get; }

    public float SideBarHeight { get; }

    public float SideBarHorizontalOffset { get; }

    public Color BackgroundColor { get; } = new(0.02f, 0.02f, 0.02f, 0.78f);

    public Color FillColor { get; } = new(0.78f, 0.08f, 0.07f, 0.92f);

    public Color BorderColor { get; } = new(0f, 0f, 0f, 0.95f);

    public Color TextColor { get; } = new(0.96f, 0.96f, 0.96f, 1f);

    public static HealthBarStyle FromConfig(ModConfig config)
    {
        return new HealthBarStyle(
            config.BarWidth * PixelUnitScale,
            config.BarHeight * PixelUnitScale,
            config.WorldScale * WorldScaleMultiplier,
            config.DisplayMode,
            config.ShowHealthNumbers,
            config.HealthTextFormat,
            config.ShowEnemyName,
            config.SideBarWidth * PixelUnitScale,
            config.SideBarHeight * PixelUnitScale,
            config.SideBarHorizontalOffset * PixelUnitScale);
    }
}
