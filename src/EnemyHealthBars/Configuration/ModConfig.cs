using System;
using System.IO;
using Auuueser.EnemyHealthBars.Core.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using BepInEx.Configuration;

namespace Auuueser.EnemyHealthBars.Configuration;

internal sealed class ModConfig
{
    private const float ConfigFilePollInterval = 0.5f;

    private readonly ConfigFile configFile;
    private readonly string configFilePath;
    private readonly ConfigEntry<bool> enabled;
    private readonly ConfigEntry<bool> showInvulnerableEnemies;
    private readonly ConfigEntry<MaxHealthMode> maxHealthMode;
    private readonly ConfigEntry<float> maxDistance;
    private readonly ConfigEntry<float> scanInterval;
    private readonly ConfigEntry<float> verticalOffset;
    private readonly ConfigEntry<float> barWidth;
    private readonly ConfigEntry<float> barHeight;
    private readonly ConfigEntry<float> worldScale;
    private readonly ConfigEntry<HealthBarDisplayMode> displayMode;
    private readonly ConfigEntry<bool> showHealthNumbers;
    private readonly ConfigEntry<HealthTextFormat> healthTextFormat;
    private readonly ConfigEntry<bool> showEnemyName;
    private readonly ConfigEntry<float> sideBarWidth;
    private readonly ConfigEntry<float> sideBarHeight;
    private readonly ConfigEntry<float> sideBarHorizontalOffset;
    private readonly ConfigEntry<bool> debugEnabled;
    private readonly ConfigEntry<bool> debugShowFullHealthEnemies;
    private readonly ConfigEntry<bool> debugDiagnosticsEnabled;
    private readonly ConfigEntry<float> debugDiagnosticsLogInterval;
    private readonly ConfigEntry<bool> debugShowTestBar;
    private DateTime lastConfigWriteTimeUtc;
    private float nextConfigFilePollTime;

    private ModConfig(ConfigFile config, ConfigLanguage language)
    {
        configFile = config;
        configFilePath = config.ConfigFilePath;
        Language = language;
        Texts = ConfigTextCatalog.Get(language);

        enabled = config.Bind(Texts.GeneralSection, "Enabled", true, Texts.EnabledDescription);
        showInvulnerableEnemies = config.Bind(Texts.VisibilitySection, "ShowInvulnerableEnemies", false, Texts.ShowInvulnerableEnemiesDescription);
        maxHealthMode = config.Bind(Texts.VisibilitySection, "MaxHealthMode", MaxHealthMode.Hybrid, Texts.MaxHealthModeDescription);
        maxDistance = config.Bind(Texts.VisibilitySection, "MaxDistance", 35f, Texts.MaxDistanceDescription);
        scanInterval = config.Bind(Texts.PerformanceSection, "ScanInterval", 0.2f, Texts.ScanIntervalDescription);
        verticalOffset = config.Bind(Texts.LayoutSection, "VerticalOffset", 0.45f, Texts.VerticalOffsetDescription);
        barWidth = config.Bind(Texts.LayoutSection, "BarWidth", 1.25f, Texts.BarWidthDescription);
        barHeight = config.Bind(Texts.LayoutSection, "BarHeight", 0.14f, Texts.BarHeightDescription);
        worldScale = config.Bind(Texts.LayoutSection, "WorldScale", 0.7f, Texts.WorldScaleDescription);
        displayMode = config.Bind(Texts.LayoutSection, "DisplayMode", HealthBarDisplayMode.HorizontalBar, Texts.DisplayModeDescription);
        showHealthNumbers = config.Bind(Texts.LayoutSection, "ShowHealthNumbers", true, Texts.ShowHealthNumbersDescription);
        healthTextFormat = config.Bind(Texts.LayoutSection, "HealthTextFormat", HealthTextFormat.CurrentAndMax, Texts.HealthTextFormatDescription);
        showEnemyName = config.Bind(Texts.LayoutSection, "ShowEnemyName", false, Texts.ShowEnemyNameDescription);
        sideBarWidth = config.Bind(Texts.LayoutSection, "SideBarWidth", 0.14f, Texts.SideBarWidthDescription);
        sideBarHeight = config.Bind(Texts.LayoutSection, "SideBarHeight", 0.55f, Texts.SideBarHeightDescription);
        sideBarHorizontalOffset = config.Bind(Texts.LayoutSection, "SideBarHorizontalOffset", 0.55f, Texts.SideBarHorizontalOffsetDescription);
        debugEnabled = config.Bind(Texts.DebugSection, "Enabled", false, Texts.DebugEnabledDescription);
        debugShowFullHealthEnemies = config.Bind(Texts.DebugSection, "ShowFullHealthEnemies", false, Texts.DebugShowFullHealthEnemiesDescription);
        debugDiagnosticsEnabled = config.Bind(Texts.DebugSection, "DiagnosticsEnabled", false, Texts.DebugDiagnosticsEnabledDescription);
        debugDiagnosticsLogInterval = config.Bind(Texts.DebugSection, "DiagnosticsLogInterval", 3f, Texts.DebugDiagnosticsLogIntervalDescription);
        debugShowTestBar = config.Bind(Texts.DebugSection, "ShowTestBar", false, Texts.DebugShowTestBarDescription);

        config.SettingChanged += OnConfigChanged;
        config.ConfigReloaded += OnConfigReloaded;
        config.Save();
        RefreshLastWriteTime();
    }

    public ConfigLanguage Language { get; }

    public ConfigTexts Texts { get; }

    public int SettingsVersion { get; private set; }

    public bool Enabled => enabled.Value;

    public bool ShowInvulnerableEnemies => showInvulnerableEnemies.Value;

    public MaxHealthMode MaxHealthMode => maxHealthMode.Value;

    public bool DebugEnabled => debugEnabled.Value;

    public bool DiagnosticsEnabled => DebugEnabled && debugDiagnosticsEnabled.Value;

    public bool ShowFullHealthEnemies => DebugEnabled && debugShowFullHealthEnemies.Value;

    public bool DebugShowTestBar => DebugEnabled && debugShowTestBar.Value;

    public float DiagnosticsLogInterval => debugDiagnosticsLogInterval.Value < 1f ? 1f : debugDiagnosticsLogInterval.Value;

    public float ScanInterval => scanInterval.Value < 0.02f ? 0.02f : scanInterval.Value;

    public float VerticalOffset => verticalOffset.Value;

    public float BarWidth => barWidth.Value < 0.1f ? 0.1f : barWidth.Value;

    public float BarHeight => barHeight.Value < 0.03f ? 0.03f : barHeight.Value;

    public float WorldScale => worldScale.Value < 0.01f ? 0.01f : worldScale.Value;

    public HealthBarDisplayMode DisplayMode => displayMode.Value;

    public bool ShowHealthNumbers => showHealthNumbers.Value;

    public HealthTextFormat HealthTextFormat => healthTextFormat.Value;

    public bool ShowEnemyName => showEnemyName.Value;

    public float SideBarWidth => sideBarWidth.Value < 0.03f ? 0.03f : sideBarWidth.Value;

    public float SideBarHeight => sideBarHeight.Value < 0.1f ? 0.1f : sideBarHeight.Value;

    public float SideBarHorizontalOffset => sideBarHorizontalOffset.Value;

    internal ConfigEntry<bool> EnabledEntry => enabled;

    internal ConfigEntry<bool> ShowInvulnerableEnemiesEntry => showInvulnerableEnemies;

    internal ConfigEntry<MaxHealthMode> MaxHealthModeEntry => maxHealthMode;

    internal ConfigEntry<float> MaxDistanceEntry => maxDistance;

    internal ConfigEntry<float> ScanIntervalEntry => scanInterval;

    internal ConfigEntry<float> VerticalOffsetEntry => verticalOffset;

    internal ConfigEntry<float> BarWidthEntry => barWidth;

    internal ConfigEntry<float> BarHeightEntry => barHeight;

    internal ConfigEntry<float> WorldScaleEntry => worldScale;

    internal ConfigEntry<HealthBarDisplayMode> DisplayModeEntry => displayMode;

    internal ConfigEntry<bool> ShowHealthNumbersEntry => showHealthNumbers;

    internal ConfigEntry<HealthTextFormat> HealthTextFormatEntry => healthTextFormat;

    internal ConfigEntry<bool> ShowEnemyNameEntry => showEnemyName;

    internal ConfigEntry<float> SideBarWidthEntry => sideBarWidth;

    internal ConfigEntry<float> SideBarHeightEntry => sideBarHeight;

    internal ConfigEntry<float> SideBarHorizontalOffsetEntry => sideBarHorizontalOffset;

    internal ConfigEntry<bool> DebugEnabledEntry => debugEnabled;

    internal ConfigEntry<bool> DebugShowFullHealthEnemiesEntry => debugShowFullHealthEnemies;

    internal ConfigEntry<bool> DebugDiagnosticsEnabledEntry => debugDiagnosticsEnabled;

    internal ConfigEntry<float> DebugDiagnosticsLogIntervalEntry => debugDiagnosticsLogInterval;

    internal ConfigEntry<bool> DebugShowTestBarEntry => debugShowTestBar;

    public static ModConfig Bind(ConfigFile config, ConfigLanguage language)
    {
        return new ModConfig(config, language);
    }

    public HealthBarVisibilityRules CreateVisibilityRules()
    {
        return new HealthBarVisibilityRules(ShowFullHealthEnemies, maxDistance.Value);
    }

    public void ReloadIfChangedOnDisk(float currentTime)
    {
        if (currentTime < nextConfigFilePollTime || !File.Exists(configFilePath))
        {
            return;
        }

        nextConfigFilePollTime = currentTime + ConfigFilePollInterval;
        var currentWriteTimeUtc = File.GetLastWriteTimeUtc(configFilePath);
        if (currentWriteTimeUtc <= lastConfigWriteTimeUtc)
        {
            return;
        }

        configFile.Reload();
        RefreshLastWriteTime();
    }

    private void OnConfigChanged(object sender, SettingChangedEventArgs args)
    {
        SettingsVersion++;
        RefreshLastWriteTime();
    }

    private void OnConfigReloaded(object sender, EventArgs args)
    {
        SettingsVersion++;
        RefreshLastWriteTime();
    }

    private void RefreshLastWriteTime()
    {
        if (File.Exists(configFilePath))
        {
            lastConfigWriteTimeUtc = File.GetLastWriteTimeUtc(configFilePath);
        }
    }
}
