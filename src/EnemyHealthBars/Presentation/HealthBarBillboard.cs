using UnityEngine;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class HealthBarBillboard : MonoBehaviour
{
    public void SetCamera(Camera? camera)
    {
    }

    public void ApplyRotation(Quaternion rotation)
    {
        transform.rotation = rotation;
    }

    public static Quaternion CalculateRotation(Camera camera)
    {
        var cameraTransform = camera.transform;
        var cameraForward = cameraTransform.forward;
        var cameraUp = cameraTransform.up;
        if (cameraForward == Vector3.zero || cameraUp == Vector3.zero)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(cameraForward, cameraUp);
    }
}
