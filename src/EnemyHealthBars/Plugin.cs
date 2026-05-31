using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Runtime;
using BepInEx;
using BepInEx.Logging;

namespace Auuueser.EnemyHealthBars;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInDependency("cn.codex.v81testchn", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("Lethal Company.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private HealthBarRuntime? runtime;

    private void Awake()
    {
        Log = Logger;

        var language = ChineseProjectLanguageDetector.Detect(Config, Logger);
        var config = ModConfig.Bind(Config, language);
        if (LethalConfigIntegration.IsAvailable())
        {
            LethalConfigIntegration.Register(config, Logger);
        }

        runtime = HealthBarRuntime.Start(config, Logger);

        Logger.LogInfo($"{PluginInfo.PluginName} {PluginInfo.PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        Logger.LogInfo("Plugin component destroyed; runtime remains active.");
    }
}
