using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace Auuueser.EnemyHealthBars.Configuration;

internal static class LethalConfigIntegration
{
    private const string PluginGuid = "ainavt.lc.lethalconfig";

    public static bool IsAvailable()
    {
        return Chainloader.PluginInfos.ContainsKey(PluginGuid);
    }

    public static void Register(ModConfig config, ManualLogSource logger)
    {
        try
        {
            var texts = config.Texts;

            LethalConfigManager.SetModDescription(texts.ModDescription);

            AddBool(config.EnabledEntry, texts.EnabledName, texts.GeneralSection, texts.EnabledDescription);
            AddBool(config.ShowInvulnerableEnemiesEntry, texts.ShowInvulnerableEnemiesName, texts.VisibilitySection, texts.ShowInvulnerableEnemiesDescription);
            AddEnum(config.MaxHealthModeEntry, texts.MaxHealthModeName, texts.VisibilitySection, texts.MaxHealthModeDescription);
            AddFloat(config.MaxDistanceEntry, texts.MaxDistanceName, texts.VisibilitySection, texts.MaxDistanceDescription);
            AddFloat(config.ScanIntervalEntry, texts.ScanIntervalName, texts.PerformanceSection, texts.ScanIntervalDescription);
            AddFloat(config.VerticalOffsetEntry, texts.VerticalOffsetName, texts.LayoutSection, texts.VerticalOffsetDescription);
            AddFloat(config.BarWidthEntry, texts.BarWidthName, texts.LayoutSection, texts.BarWidthDescription);
            AddFloat(config.BarHeightEntry, texts.BarHeightName, texts.LayoutSection, texts.BarHeightDescription);
            AddFloat(config.WorldScaleEntry, texts.WorldScaleName, texts.LayoutSection, texts.WorldScaleDescription);
            AddEnum(config.DisplayModeEntry, texts.DisplayModeName, texts.LayoutSection, texts.DisplayModeDescription);
            AddBool(config.ShowHealthNumbersEntry, texts.ShowHealthNumbersName, texts.LayoutSection, texts.ShowHealthNumbersDescription);
            AddEnum(config.HealthTextFormatEntry, texts.HealthTextFormatName, texts.LayoutSection, texts.HealthTextFormatDescription);
            AddBool(config.ShowEnemyNameEntry, texts.ShowEnemyNameName, texts.LayoutSection, texts.ShowEnemyNameDescription);
            AddFloat(config.SideBarWidthEntry, texts.SideBarWidthName, texts.LayoutSection, texts.SideBarWidthDescription);
            AddFloat(config.SideBarHeightEntry, texts.SideBarHeightName, texts.LayoutSection, texts.SideBarHeightDescription);
            AddFloat(config.SideBarHorizontalOffsetEntry, texts.SideBarHorizontalOffsetName, texts.LayoutSection, texts.SideBarHorizontalOffsetDescription);
            AddBool(config.DebugEnabledEntry, texts.DebugEnabledName, texts.DebugSection, texts.DebugEnabledDescription);
            AddBool(config.DebugShowFullHealthEnemiesEntry, texts.DebugShowFullHealthEnemiesName, texts.DebugSection, texts.DebugShowFullHealthEnemiesDescription);
            AddBool(config.DebugDiagnosticsEnabledEntry, texts.DebugDiagnosticsEnabledName, texts.DebugSection, texts.DebugDiagnosticsEnabledDescription);
            AddFloat(config.DebugDiagnosticsLogIntervalEntry, texts.DebugDiagnosticsLogIntervalName, texts.DebugSection, texts.DebugDiagnosticsLogIntervalDescription);
            AddBool(config.DebugShowTestBarEntry, texts.DebugShowTestBarName, texts.DebugSection, texts.DebugShowTestBarDescription);

            logger.LogInfo("Registered localized LethalConfig entries.");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Could not register LethalConfig entries: {ex.Message}");
        }
    }

    private static void AddBool(ConfigEntry<bool> entry, string name, string section, string description)
    {
        LethalConfigManager.SkipAutoGenFor(entry);
        LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(entry, new BoolCheckBoxOptions
        {
            Name = name,
            Section = section,
            Description = description,
            RequiresRestart = false,
        }));
    }

    private static void AddFloat(ConfigEntry<float> entry, string name, string section, string description)
    {
        LethalConfigManager.SkipAutoGenFor(entry);
        LethalConfigManager.AddConfigItem(new FloatInputFieldConfigItem(entry, new FloatInputFieldOptions
        {
            Name = name,
            Section = section,
            Description = description,
            RequiresRestart = false,
        }));
    }

    private static void AddEnum<T>(ConfigEntry<T> entry, string name, string section, string description)
        where T : Enum
    {
        LethalConfigManager.SkipAutoGenFor(entry);
        LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<T>(entry, new EnumDropDownOptions
        {
            Name = name,
            Section = section,
            Description = description,
            RequiresRestart = false,
        }));
    }
}
