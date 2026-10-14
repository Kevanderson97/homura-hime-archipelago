using UnityEngine;

namespace HomuraHimeAP.UI;

/// <summary>Keeps a flat sprite turned towards the camera.</summary>
public sealed class SenbeiBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        var camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }
}
