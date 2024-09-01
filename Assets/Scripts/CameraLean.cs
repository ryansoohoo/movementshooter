using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.5f;
    [SerializeField] private float decayDamping = 0.3f;
    [SerializeField] private float strength = 0.1f;
    [SerializeField] private float slideStrength = 0.2f;
    [SerializeField] private float strengthResponse = 5f;

    private Vector3 _dampedAcceleration;
    private Vector3 _dampedAccelerationVel;

    private float _smoothStrength;
    public void Initialize()
    {
        _smoothStrength = strength;
    }

    public void UpdateLean(float deltaTime, bool sliding, Vector3 acceleration, Vector3 up)
    {
        var planarAcceleration = Vector3.ProjectOnPlane(acceleration, up);
        var damping = planarAcceleration.magnitude > _dampedAcceleration.magnitude
            ? attackDamping
            : decayDamping;

        _dampedAcceleration = Vector3.SmoothDamp(
            current: _dampedAcceleration,
            target: planarAcceleration,
            currentVelocity: ref _dampedAccelerationVel,
            smoothTime: damping,
            maxSpeed: float.PositiveInfinity,
            deltaTime: deltaTime
        );

        var leanAxis = Vector3.Cross(_dampedAcceleration.normalized, up).normalized;
        transform.localRotation = Quaternion.identity;

        var targetStrength = sliding ? slideStrength : strength;
        _smoothStrength= Mathf.Lerp(_smoothStrength, targetStrength, 1f-Mathf.Exp(-strengthResponse*deltaTime));
        transform.rotation = Quaternion.AngleAxis(-_dampedAcceleration.magnitude * targetStrength, leanAxis) * transform.rotation;
        Debug.DrawRay(transform.position, acceleration, Color.red);
        Debug.DrawRay(transform.position, _dampedAcceleration, Color.blue);
    }
}