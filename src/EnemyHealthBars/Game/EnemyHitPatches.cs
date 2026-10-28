using System;
using Auuueser.EnemyHealthBars.Core.Domain;
using BepInEx.Logging;
using HarmonyLib;

namespace Auuueser.EnemyHealthBars.Game;

// The only Harmony patch in the plugin: an explicit prefix on EnemyAI.HitEnemy, installed once.
// Every vanilla HitEnemy override calls base.HitEnemy before it lowers enemyHP, so the prefix
// sees the health right before each hit on every peer that applies the damage.
internal static class EnemyHitPatches
{
    private const string HarmonyId = PluginInfo.PluginGuid + ".hit-ledger";

    private static Harmony? harmony;

    public static EnemyHitLedger Ledger { get; } = new();

    public static bool IsInstalled => harmony != null;

    public static void Install(ManualLogSource logger)
    {
        if (harmony != null)
        {
            return;
        }

        try
        {
            var target = AccessTools.Method(typeof(EnemyAI), nameof(EnemyAI.HitEnemy));
            var prefix = AccessTools.Method(typeof(EnemyHitPatches), nameof(RecordHitPrefix));
            var instance = new Harmony(HarmonyId);
            instance.Patch(target, prefix: new HarmonyMethod(prefix));
            harmony = instance;
            logger.LogInfo("Enemy hit ledger patch installed.");
        }
        catch (Exception exception)
        {
            logger.LogWarning($"Enemy hit ledger patch failed; early hits may be read as reduced spawn health. {exception.Message}");
        }
    }

    private static void RecordHitPrefix(EnemyAI __instance)
    {
        if (__instance == null || __instance.isEnemyDead)
        {
            return;
        }

        Ledger.RecordHit(__instance.GetInstanceID(), __instance.enemyHP);
    }
}
