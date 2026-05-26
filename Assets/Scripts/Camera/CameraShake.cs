using Unity.Cinemachine;
using UnityEngine;

// Attach to the Main Camera (same GameObject as CinemachineImpulseSource).
// On the Cinemachine Camera (virtual camera) add a CinemachineImpulseListener component.
// Call CameraShake.instance.Shake(intensity, duration) from anywhere.
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;

    private CinemachineImpulseSource _source;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(this); return; }

        _source = GetComponent<CinemachineImpulseSource>();
    }

    // intensity : shake force (maps to GenerateImpulse force)
    // duration  : seconds the impulse lasts
    public void Shake(float intensity, float duration)
    {
        if (_source == null || intensity <= 0f) return;

        _source.ImpulseDefinition.ImpulseDuration = duration;
        _source.GenerateImpulse(intensity);
    }
}
