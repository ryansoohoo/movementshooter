using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private CameraSpring cameraSpring;
    [SerializeField] private CameraLean cameraLean;
    private PlayerActions _inputActions;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _inputActions = new PlayerActions();
        _inputActions.Enable();
        playerCharacter.Initialize();
        cameraSpring.Initiaize();
        cameraLean.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        var input = _inputActions.Gameplay;
        var deltaTime = Time.deltaTime;
        var cameraInput = new CameraInput {Look = input.Look.ReadValue<Vector2>()}; 
        playerCamera.UpdateRotation(cameraInput);

        var characterInput = new CharacterInput {
            Rotation = playerCamera.transform.rotation,
            Move = input.Move.ReadValue<Vector2>(),
            JumpSustain = input.Jump.IsPressed(),
            Jump = input.Jump.WasPressedThisFrame(),
            Crouch = input.Crouch.WasPressedThisFrame()
            ? CrouchInput.Toggle
            : CrouchInput.None
        };
        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(deltaTime);

#if UNITY_EDITOR 
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out var hit))
            {
                Teleport(hit.point);
            }
        }
#endif
    }

    private void LateUpdate()
    {
        var deltaTime = Time.deltaTime;
        var cameraTarget = playerCharacter.GetCameraTarget();
        var state=playerCharacter.GetState();

        playerCamera.UpdatePosition(cameraTarget);
        cameraSpring.UpdateSpring(deltaTime, cameraTarget.up);
        cameraLean.UpdateLean(deltaTime,state.Stance is Stance.Slide,state.Acceleration, cameraTarget.up);
    }

    public void Teleport(Vector3 position)
    {
        playerCharacter.SetPosition(position);
    }
}
