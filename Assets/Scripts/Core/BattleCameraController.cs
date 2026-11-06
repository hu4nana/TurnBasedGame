using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BattleCameraController : MonoBehaviour
{
    public event System.Action RotationChanged;
    private Quaternion lastNotifiedRotation;
    private bool hasNotifiedRotation;

    public void NotifyRotationChanged()
    {
        Quaternion current = transform.rotation;
        if (hasNotifiedRotation && Quaternion.Angle(lastNotifiedRotation, current) < 0.001f) return;
        lastNotifiedRotation = current;
        hasNotifiedRotation = true;
        RotationChanged?.Invoke();
    }
    [SerializeField] private Vector3 focusPoint = new(0f, 0.5f, 0f);
    [SerializeField, Range(20f, 80f)] private float pitch = 43f;
    [SerializeField] private float yaw = -35f;
    [SerializeField, Min(3f)] private float distance = 19f;
    [SerializeField, Min(1f)] private float panSpeed = 8f;
    [SerializeField, Min(1f)] private float rotationSpeed = 75f;
    [SerializeField, Min(1f)] private float zoomSpeed = 8f;

    public void Configure(Vector3 newFocusPoint)
    {
        focusPoint = newFocusPoint;
        ApplyTransform();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null)
        {
            Vector2 movement = Vector2.zero;
            if (keyboard.wKey.isPressed) movement.y += 1f;
            if (keyboard.sKey.isPressed) movement.y -= 1f;
            if (keyboard.dKey.isPressed) movement.x += 1f;
            if (keyboard.aKey.isPressed) movement.x -= 1f;

            Quaternion planarRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 worldMovement = planarRotation * new Vector3(movement.x, 0f, movement.y);
            if (worldMovement.sqrMagnitude > 1f) worldMovement.Normalize();
            focusPoint += worldMovement * (panSpeed * Time.deltaTime);

            float rotation = 0f;
            if (keyboard.qKey.isPressed) rotation -= 1f;
            if (keyboard.eKey.isPressed) rotation += 1f;
            yaw += rotation * rotationSpeed * Time.deltaTime;
        }

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y / 120f;
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.35f, 6f, 28f);
        }

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = focusPoint + rotation * (Vector3.back * distance);
        if (transform.position != position || transform.rotation != rotation)
            transform.SetPositionAndRotation(position, rotation);
        NotifyRotationChanged();
    }
}
