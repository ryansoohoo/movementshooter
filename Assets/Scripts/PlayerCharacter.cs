using KinematicCharacterController;
using UnityEngine;

public enum CrouchInput
{
    None, Toggle
}

public enum Stance//17:03
{
    Stand, Crouch, Slide
}

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance; //31:07
    public Vector3 Velocity; // 43:01
    public Vector3 Acceleration; // 56:43
}
public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public CrouchInput Crouch;
    public bool JumpSustain;
}
public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform root;
    [Space]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkResponse = 7f; //24:41
    [SerializeField] private float crouchResponse = 7f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [Range(0, 1f)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0, 1f)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravity = 20f;
    [SerializeField] private float gravity = -90.81f;
    [SerializeField] private float slideStartSpeed = 25f; // 32:46
    [SerializeField] private float slideEndSpeed = 15f;
    [SerializeField] private float slideFriction = 0.8f;
    [SerializeField] private float slideSteerAcceleration = 5f;
    [SerializeField] private float slideGravity = -90f;

    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedJumpSustain;//25:55
    private bool _requestedCrouch; //17:35 
    private bool _requestedCrouchInAir; 
    private float _timeSinceUngrounded; //48:35
    private float _timeSinceJumpRequest;
    private bool _ungroundedDueToJump;

    private Collider[] _uncrouchOverlapResults; // 22:01
    public void Initialize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;

        _uncrouchOverlapResults = new Collider[8];
        motor.CharacterController = this;
    }
    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        _requestedMovement = input.Rotation * _requestedMovement;

        var wasRequestingJump = _requestedJump;
        _requestedJump = _requestedJump || input.Jump;
        if (_requestedJump && !wasRequestingJump)
        {
            _timeSinceJumpRequest = 0f;
        }
        _requestedJumpSustain = input.JumpSustain;
        var wasRequestingCrouch = _requestedCrouch;
        _requestedCrouch = input.Crouch switch
        { //If the input is to toggle crouch, it flips the current crouch state.
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch, //Not Toggle it keeps the current crouch state as is.
            _ => _requestedCrouch // For any other input, it also keeps the current crouch state unchanged.
        };

        if (_requestedCrouch && !wasRequestingCrouch)
        {
            _requestedCrouchInAir = !_state.Grounded;
        }
        else if (!_requestedCrouch && wasRequestingCrouch)
        {
            _requestedCrouchInAir = false;
        }
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;
        var cameraTargetHeight = currentHeight * (_state.Stance is Stance.Stand ? standCameraTargetHeight : crouchCameraTargetHeight);

        //cameraTarget.localPosition = new Vector3(0f, cameraTargetHeight, 0f);
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp
        (
            a: cameraTarget.localPosition,
            b: new Vector3(0f, cameraTargetHeight, 0f),
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );

        root.localScale = Vector3.Lerp
        (
            a: root.localScale,
            b: rootTargetScale,
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
    }

    //fixed update
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _state.Acceleration = Vector3.zero;
        if (motor.GroundingStatus.IsStableOnGround) // GROUNDED
        {
            _timeSinceUngrounded = 0f;
            _ungroundedDueToJump = false;
            // Snap the requested movement direction to the angle of the surface
            // the character is currently walking on.
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude;

            //Start Sliding
            {
                var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = _state.Stance is Stance.Crouch;
                var wasStanding = _lastState.Stance is Stance.Stand;
                var wasInAir = !_lastState.Grounded;
                if (moving && crouching && (wasStanding || wasInAir))
                {
                    _state.Stance = Stance.Slide;
                    if (wasInAir)
                    {
                        currentVelocity = Vector3.ProjectOnPlane(_lastState.Velocity, planeNormal:motor.GroundingStatus.GroundNormal);
                    }
                    var effectiveSlideStartSpeed = slideStartSpeed;
                    if(!_lastState.Grounded && !_requestedCrouchInAir)
                    {
                        effectiveSlideStartSpeed = 0f;
                        _requestedCrouchInAir = false;
                    }
                    var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = motor.GetDirectionTangentToSurface
                    (
                        direction: currentVelocity,
                        surfaceNormal: motor.GroundingStatus.GroundNormal
                    ) * slideSpeed;
                }
            }

            if (_state.Stance is Stance.Stand or Stance.Crouch)
            {
                // Calculate the speed and responsiveness of movement based
                // on the character's stance.
                var speed = _state.Stance is Stance.Stand ? walkSpeed : crouchSpeed;
                var response = _state.Stance is Stance.Stand ? walkResponse : crouchResponse;

                // And smoothly move along the ground in that direction.
                var targetVelocity = groundedMovement * speed;
                var moveVelocity = Vector3.Lerp
                (
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f - Mathf.Exp(-response * deltaTime)
                );
                _state.Acceleration = moveVelocity - currentVelocity;
                currentVelocity = moveVelocity;
            }
            else
            {
                currentVelocity -= currentVelocity * slideFriction * deltaTime;

                //Slope
                {
                    var force = Vector3.ProjectOnPlane(
                        vector: -motor.CharacterUp,
                        planeNormal: motor.GroundingStatus.GroundNormal
                    ) * slideGravity;

                    currentVelocity -= force * deltaTime;
                }
                //Steer  
                {
                    var currentSpeed = currentVelocity.magnitude;
                    var targetVelocity = groundedMovement * currentSpeed;
                    var steerVelocity = currentVelocity;
                    var steerForce = (targetVelocity - steerVelocity) * slideSteerAcceleration * deltaTime;


                    steerVelocity += steerForce;
                    steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);

                    _state.Acceleration = (steerVelocity - currentVelocity) / deltaTime;
                    currentVelocity = steerVelocity;
                }
                if (currentVelocity.magnitude < slideEndSpeed) // end slide
                    _state.Stance = Stance.Stand;
            }
        }
        else // IN AIR
        {
            _timeSinceUngrounded+= deltaTime;
            // Move.
            if (_requestedMovement.sqrMagnitude > 0f)
            {
                // Requested movement projected onto movement plane. (magnitude preserved)
                var planarMovement = Vector3.ProjectOnPlane
                (
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                ) * _requestedMovement.magnitude;

                // Current velocity on movement plane.
                var currentPlanarVelocity = Vector3.ProjectOnPlane
                (
                    vector: currentVelocity, //29:00
                    planeNormal: motor.CharacterUp
                );

                // Calculate movement force.
                var movementForce = planarMovement * airAcceleration * deltaTime;

                if (currentPlanarVelocity.magnitude < airSpeed)
                {
                    // Add it to the current planar velocity for a target velocity.
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;

                    // Limit target velocity to air speed.
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);

                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                {
                    var contrainedMovementForce = Vector3.ProjectOnPlane
                    (
                        vector: movementForce,
                        planeNormal: currentPlanarVelocity.normalized
                    );
                    movementForce = contrainedMovementForce;
                }
                if (motor.GroundingStatus.FoundAnyGround)
                {
                    if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                    {
                        var obstructionNormal = Vector3.Cross(
                            motor.CharacterUp, Vector3.Cross(motor.GroundingStatus.GroundNormal, motor.GroundingStatus.GroundNormal)
                        ).normalized;
                        currentVelocity = Vector3.ProjectOnPlane(movementForce, obstructionNormal);
                    }
                }
                currentVelocity += movementForce;
            }

            //Gravity.
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp); //26:57
            if (_requestedJumpSustain && verticalSpeed > 0f)
                effectiveGravity *= jumpSustainGravity;
            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
        }

        if (_requestedJump) // 14:05
        {
            var grounded = motor.GroundingStatus.IsStableOnGround; 
            var canCoyoteJump = _timeSinceUngrounded <= coyoteTime;

            if (grounded || canCoyoteJump && !_ungroundedDueToJump)
            {
                _requestedCrouch = false;
                _requestedJump = false;
                _requestedCrouchInAir = false;

                motor.ForceUnground(time: 0.1f);
                _ungroundedDueToJump = true;
                var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp); //15:15
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else
            {
                _timeSinceJumpRequest += deltaTime;

                var canJumpLater = _timeSinceJumpRequest <= coyoteTime;
                //Deny jump request
                _requestedJump = canJumpLater;
            }
        }
    }
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane(_requestedRotation * Vector3.forward, motor.CharacterUp);

        if (forward != Vector3.zero)
            currentRotation = Quaternion.LookRotation(forward);
    }
    public void BeforeCharacterUpdate(float deltaTime)
    {
        _tempState = _state;
        //Crouch.
        if (_requestedCrouch && _state.Stance is Stance.Stand)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );
        }
    }
    public void PostGroundingUpdate(float deltaTime)
    {
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
        {
            _state.Stance = Stance.Crouch;
        }
    }
    public void AfterCharacterUpdate(float deltaTime)
    {
        // Uncrouch.
        if (!_requestedCrouch && _state.Stance is not Stance.Stand)
        {
            // Tentatively "standup" the character capsule.
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );

            // Then see if the capsule overlaps any colliders before actually
            // allowing the character to standup.
            var pos = motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
            if (motor.CharacterOverlap(pos, rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) > 0)
            {
                // Re-crouch.
                _requestedCrouch = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );
            }
            else
            {
                _state.Stance = Stance.Stand;
            }
        }
        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }
    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }
    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public Transform GetCameraTarget() => cameraTarget;

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if (killVelocity)
            motor.BaseVelocity = Vector3.zero;
    }

    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _lastState;
}
