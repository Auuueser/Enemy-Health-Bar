using UnityEngine;

namespace Auuueser.EnemyHealthBars.Game;

internal sealed class LocalPlayerCameraProvider
{
    public Camera? GetActiveCamera()
    {
        var round = StartOfRound.Instance;
        if (round != null)
        {
            if (round.localPlayerController != null && IsUsable(round.localPlayerController.gameplayCamera))
            {
                return round.localPlayerController.gameplayCamera;
            }

            if (IsUsable(round.activeCamera))
            {
                return round.activeCamera;
            }

            if (IsUsable(round.spectateCamera))
            {
                return round.spectateCamera;
            }
        }

        var hud = HUDManager.Instance;
        if (hud != null && hud.localPlayer != null && IsUsable(hud.localPlayer.gameplayCamera))
        {
            return hud.localPlayer.gameplayCamera;
        }

        return Camera.main != null ? Camera.main : Camera.current;
    }

    private static bool IsUsable(Camera? camera)
    {
        return camera != null && camera.isActiveAndEnabled;
    }
}
