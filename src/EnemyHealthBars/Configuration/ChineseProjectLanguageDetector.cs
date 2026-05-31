using System;
using System.IO;
using Auuueser.EnemyHealthBars.Core.Configuration;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace Auuueser.EnemyHealthBars.Configuration;

internal static class ChineseProjectLanguageDetector
{
    public static ConfigLanguage Detect(ConfigFile config, ManualLogSource logger)
    {
        if (DetectLoadedPlugin(logger) ||
            DetectExistingConfig(config, logger) ||
            DetectManifestNearConfig(config, logger))
        {
            return ConfigLanguage.Chinese;
        }

        return ConfigLanguage.English;
    }

    private static bool DetectLoadedPlugin(ManualLogSource logger)
    {
        foreach (var pluginInfoPair in Chainloader.PluginInfos)
        {
            var pluginInfo = pluginInfoPair.Value;
            if (pluginInfo == null || pluginInfo.Metadata == null)
            {
                continue;
            }

            if (!ChineseProjectDetection.IsChineseProjectPlugin(
                    pluginInfo.Metadata.GUID,
                    pluginInfo.Metadata.Name,
                    pluginInfo.Location))
            {
                continue;
            }

            logger.LogInfo($"LC Chinese Project detected from plugin '{pluginInfo.Metadata.GUID}'. Using Chinese config text.");
            return true;
        }

        return false;
    }

    private static bool DetectExistingConfig(ConfigFile config, ManualLogSource logger)
    {
        if (!File.Exists(config.ConfigFilePath))
        {
            return false;
        }

        try
        {
            var configText = File.ReadAllText(config.ConfigFilePath);
            if (!ChineseProjectDetection.ContainsChineseConfigSections(configText))
            {
                return false;
            }

            logger.LogInfo("Existing Chinese config sections detected. Using Chinese config text.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Could not inspect existing config language: {ex.Message}");
            return false;
        }
    }

    private static bool DetectManifestNearConfig(ConfigFile config, ManualLogSource logger)
    {
        var configDirectory = Path.GetDirectoryName(config.ConfigFilePath);
        if (string.IsNullOrEmpty(configDirectory))
        {
            return false;
        }

        var bepinexDirectory = Directory.GetParent(configDirectory);
        if (bepinexDirectory == null)
        {
            return false;
        }

        var pluginsDirectory = Path.Combine(bepinexDirectory.FullName, "plugins");
        if (!Directory.Exists(pluginsDirectory))
        {
            return false;
        }

        try
        {
            var manifestPaths = Directory.GetFiles(pluginsDirectory, "manifest.json", SearchOption.AllDirectories);
            for (var i = 0; i < manifestPaths.Length; i++)
            {
                var manifestText = File.ReadAllText(manifestPaths[i]);
                if (!ChineseProjectDetection.ContainsChineseProjectManifestText(manifestText))
                {
                    continue;
                }

                logger.LogInfo($"LC Chinese Project manifest detected at '{manifestPaths[i]}'. Using Chinese config text.");
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Could not inspect plugin manifests for config language: {ex.Message}");
        }

        return false;
    }
}
