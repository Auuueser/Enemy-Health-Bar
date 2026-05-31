using System;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Game;
using Auuueser.EnemyHealthBars.Presentation;
using BepInEx.Logging;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Runtime;

internal sealed class HealthBarRuntime : IDisposable
{
    private static GameObject? runtimeObject;
    private static EnemyHealthBarController? runtimeController;

    private readonly GameObject host;

    private HealthBarRuntime(GameObject host)
    {
        this.host = host;
    }

    public static HealthBarRuntime Start(ModConfig config, ManualLogSource logger)
    {
        if (runtimeObject == null)
        {
            runtimeObject = new GameObject("EnemyHealthBars.Runtime");
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
        }
        else if (!runtimeObject.activeSelf)
        {
            runtimeObject.SetActive(true);
        }

        var host = runtimeObject;

        runtimeController = host.GetComponent<EnemyHealthBarController>();
        if (runtimeController == null)
        {
            var presenter = new EnemyHealthBarPresenter(config, host.transform);
            runtimeController = host.AddComponent<EnemyHealthBarController>();
            runtimeController.Initialize(
                config,
                logger,
                new RoundManagerEnemySource(),
                new LocalPlayerCameraProvider(),
                presenter);

            logger.LogInfo("Runtime controller active.");
        }
        else
        {
            runtimeController.SetEnabled(true);
            logger.LogInfo("Runtime controller active.");
        }

        return new HealthBarRuntime(host);
    }

    public void Dispose()
    {
        if (host != null)
        {
            UnityEngine.Object.Destroy(host);
        }
    }
}
