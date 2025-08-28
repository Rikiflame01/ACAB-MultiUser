using UnityEngine;
using UnityEngine.InputSystem;

public class DesktopPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float lookSensitivity = 2f;

    private CharacterController characterController;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float rotationX = 0f;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("DesktopPlayerController requires a CharacterController component.");
        }
    }

    void Update()
    {
        // WASD movement
        moveInput = Keyboard.current != null ? new Vector2(
            (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
            (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)) : Vector2.zero;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        characterController?.Move(move * moveSpeed * Time.deltaTime);

        // Mouse look
        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue() * lookSensitivity * Time.deltaTime;
            rotationX -= lookInput.y;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);
            transform.localEulerAngles = new Vector3(rotationX, transform.localEulerAngles.y + lookInput.x, 0f);
        }
    }
}
