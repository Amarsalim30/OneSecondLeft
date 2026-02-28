using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private float maxX = 3.5f;

    [Header("Movement")]
    [SerializeField] private float smoothing = 18f;

    private Camera mainCamera;
    private Vector3 runStartPosition;
    private float targetX;
    private bool wasPointerDown;
    private float dragOffsetX;

    public float MaxX => maxX;

    private void Awake()
    {
        mainCamera = Camera.main;
        runStartPosition = transform.position;
        targetX = runStartPosition.x;
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            return;
        }

        if (TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            if (!wasPointerDown)
            {
                float worldX = ScreenToWorldX(pointerScreenPosition);
                dragOffsetX = transform.position.x - worldX;
                wasPointerDown = true;
            }

            float desiredX = ScreenToWorldX(pointerScreenPosition) + dragOffsetX;
            targetX = Mathf.Clamp(desiredX, -maxX, maxX);
        }
        else
        {
            wasPointerDown = false;
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector3 nextPosition = transform.position;
        nextPosition.x = Mathf.Lerp(nextPosition.x, targetX, t);
        transform.position = nextPosition;
    }

    public void ResetRunPosition()
    {
        transform.position = runStartPosition;
        targetX = runStartPosition.x;
        wasPointerDown = false;
    }

    private void OnCollisionEnter2D(Collision2D _)
    {
        NotifyCollisionDeath();
    }

    private void OnTriggerEnter2D(Collider2D _)
    {
        NotifyCollisionDeath();
    }

    private float ScreenToWorldX(Vector2 screenPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));
        return world.x;
    }

    private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            screenPosition = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButton(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private static void NotifyCollisionDeath()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            GameManager.Instance.KillPlayer();
        }
    }
}
