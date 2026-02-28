using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    private const int MousePointerId = -1;
    private const int InvalidPointerId = int.MinValue;

    [Header("Bounds")]
    [SerializeField] private float maxX = 3.5f;

    [Header("Movement")]
    [SerializeField] private float smoothing = 18f;
    [SerializeField] private bool enableTrail = true;
    [SerializeField, Min(0f)] private float trailTime = 0.18f;

    [Header("Collision")]
    [SerializeField] private LayerMask lethalLayers;
    [SerializeField] private string lethalTag = "Obstacle";
    [SerializeField] private bool allowObstacleWallFallback = true;
    [SerializeField] private bool enableLethalOverlapFailSafe = true;
    [SerializeField, Min(1)] private int lethalOverlapBufferSize = 8;

    private Camera mainCamera;
    private Collider2D playerCollider;
    private ContactFilter2D lethalOverlapFilter;
    private Collider2D[] lethalOverlapResults;
    private Vector3 runStartPosition;
    private float targetX;
    private bool wasPointerDown;
    private float dragOffsetX;
    private int activePointerId = InvalidPointerId;

#if UNITY_INCLUDE_TESTS
    private static bool pointerOverrideEnabled;
    private static bool pointerOverridePressed;
    private static Vector2 pointerOverrideScreenPosition;
    private static int pointerOverrideId = MousePointerId;
    private static bool deltaTimeOverrideEnabled;
    private static float deltaTimeOverrideValue;
#endif
    private static Sprite fallbackSquareSprite;
    private static Material trailMaterial;

    public float MaxX => maxX;

    private void Awake()
    {
        EnsureRuntimeComponents();
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

        if (TryGetMovementPointerScreenPosition(activePointerId, out Vector2 pointerScreenPosition, out int pointerId))
        {
            if (!wasPointerDown || activePointerId != pointerId)
            {
                float worldX = ScreenToWorldX(pointerScreenPosition);
                dragOffsetX = transform.position.x - worldX;
                wasPointerDown = true;
                activePointerId = pointerId;
            }

            float desiredX = ScreenToWorldX(pointerScreenPosition) + dragOffsetX;
            targetX = Mathf.Clamp(desiredX, -maxX, maxX);
        }
        else
        {
            wasPointerDown = false;
            activePointerId = InvalidPointerId;
        }

        float t = 1f - Mathf.Exp(-smoothing * GetDeltaTime());
        Vector3 nextPosition = transform.position;
        nextPosition.x = Mathf.Lerp(nextPosition.x, targetX, t);
        transform.position = nextPosition;
        CheckLethalOverlapFailSafe();
    }

    public void ResetRunPosition()
    {
        transform.position = runStartPosition;
        targetX = runStartPosition.x;
        wasPointerDown = false;
        activePointerId = InvalidPointerId;
    }

#if UNITY_INCLUDE_TESTS
    public static void SetMovementPointerOverrideForTests(bool pressed, Vector2 screenPosition, int pointerId = MousePointerId)
    {
        pointerOverrideEnabled = true;
        pointerOverridePressed = pressed;
        pointerOverrideScreenPosition = screenPosition;
        pointerOverrideId = pointerId;
    }

    public static void ClearMovementPointerOverrideForTests()
    {
        pointerOverrideEnabled = false;
        pointerOverridePressed = false;
        pointerOverrideScreenPosition = default;
        pointerOverrideId = MousePointerId;
    }

    public static void SetDeltaTimeOverrideForTests(float deltaTime)
    {
        deltaTimeOverrideEnabled = true;
        deltaTimeOverrideValue = Mathf.Max(0f, deltaTime);
    }

    public static void ClearDeltaTimeOverrideForTests()
    {
        deltaTimeOverrideEnabled = false;
        deltaTimeOverrideValue = 0f;
    }
#endif

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        if (IsLethalCollider(collision.collider))
        {
            NotifyCollisionDeath("collision_enter");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLethalCollider(other))
        {
            NotifyCollisionDeath("trigger_enter");
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        if (IsLethalCollider(collision.collider))
        {
            NotifyCollisionDeath("collision_stay");
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsLethalCollider(other))
        {
            NotifyCollisionDeath("trigger_stay");
        }
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

    private static bool TryGetMovementPointerScreenPosition(int preferredPointerId, out Vector2 screenPosition, out int pointerId)
    {
#if UNITY_INCLUDE_TESTS
        if (pointerOverrideEnabled)
        {
            if (pointerOverridePressed)
            {
                screenPosition = pointerOverrideScreenPosition;
                pointerId = pointerOverrideId;
                return true;
            }

            screenPosition = default;
            pointerId = InvalidPointerId;
            return false;
        }
#endif

        if (preferredPointerId == MousePointerId)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse preferredMouse = Mouse.current;
            if (preferredMouse != null && preferredMouse.leftButton.isPressed)
            {
                screenPosition = preferredMouse.position.ReadValue();
                pointerId = MousePointerId;
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0))
            {
                screenPosition = Input.mousePosition;
                pointerId = MousePointerId;
                return true;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            if (preferredPointerId >= 0 && TryGetInputSystemTouchById(touch, preferredPointerId, out screenPosition))
            {
                pointerId = preferredPointerId;
                return true;
            }

            if (TryGetInputSystemTouchOnLeftHalf(touch, out screenPosition, out pointerId))
            {
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            screenPosition = mouse.position.ReadValue();
            pointerId = MousePointerId;
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (preferredPointerId >= 0 && TryGetLegacyTouchById(preferredPointerId, out screenPosition))
        {
            pointerId = preferredPointerId;
            return true;
        }

        if (TryGetLegacyTouchOnLeftHalf(out screenPosition, out pointerId))
        {
            return true;
        }

        if (Input.GetMouseButton(0))
        {
            screenPosition = Input.mousePosition;
            pointerId = MousePointerId;
            return true;
        }
#endif

        screenPosition = default;
        pointerId = InvalidPointerId;
        return false;
    }

    private bool IsLethalCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.GetComponentInParent<LethalObstacle>() != null)
        {
            return true;
        }

        if (IsLethalLayerMatch(other.transform))
        {
            return true;
        }

        if (HasLethalTagInHierarchy(other.transform))
        {
            return true;
        }

        return allowObstacleWallFallback && other.GetComponentInParent<ObstacleWall>() != null;
    }

    private bool IsLethalLayerMatch(Transform transformRoot)
    {
        if (lethalLayers.value == 0 || transformRoot == null)
        {
            return false;
        }

        Transform current = transformRoot;
        while (current != null)
        {
            int mask = 1 << current.gameObject.layer;
            if ((lethalLayers.value & mask) != 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool HasLethalTagInHierarchy(Transform transformRoot)
    {
        if (string.IsNullOrWhiteSpace(lethalTag) || transformRoot == null)
        {
            return false;
        }

        Transform current = transformRoot;
        while (current != null)
        {
            if (SafeCompareTag(current.gameObject, lethalTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool SafeCompareTag(GameObject candidate, string requiredTag)
    {
        try
        {
            return candidate != null && candidate.CompareTag(requiredTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryGetInputSystemTouchById(Touchscreen touchScreen, int touchId, out Vector2 position)
    {
        foreach (var touch in touchScreen.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            if (touch.touchId.ReadValue() != touchId)
            {
                continue;
            }

            position = touch.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private static bool TryGetInputSystemTouchOnLeftHalf(Touchscreen touchScreen, out Vector2 position, out int pointerId)
    {
        int pressedCount = 0;
        Vector2 singleTouchPosition = default;
        int singleTouchId = InvalidPointerId;

        foreach (var touch in touchScreen.touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            pressedCount++;
            Vector2 candidate = touch.position.ReadValue();
            singleTouchPosition = candidate;
            singleTouchId = touch.touchId.ReadValue();

            if (!IsOnLeftHalf(candidate.x))
            {
                continue;
            }

            position = candidate;
            pointerId = singleTouchId;
            return true;
        }

        if (pressedCount == 1 && singleTouchId != InvalidPointerId)
        {
            position = singleTouchPosition;
            pointerId = singleTouchId;
            return true;
        }

        position = default;
        pointerId = InvalidPointerId;
        return false;
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private static bool TryGetLegacyTouchById(int touchId, out Vector2 position)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != touchId)
            {
                continue;
            }

            position = touch.position;
            return true;
        }

        position = default;
        return false;
    }

    private static bool TryGetLegacyTouchOnLeftHalf(out Vector2 position, out int pointerId)
    {
        int pressedCount = 0;
        Vector2 singleTouchPosition = default;
        int singleTouchId = InvalidPointerId;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            pressedCount++;
            singleTouchPosition = touch.position;
            singleTouchId = touch.fingerId;

            if (!IsOnLeftHalf(touch.position.x))
            {
                continue;
            }

            position = touch.position;
            pointerId = touch.fingerId;
            return true;
        }

        if (pressedCount == 1 && singleTouchId != InvalidPointerId)
        {
            position = singleTouchPosition;
            pointerId = singleTouchId;
            return true;
        }

        position = default;
        pointerId = InvalidPointerId;
        return false;
    }
#endif

    private static bool IsOnLeftHalf(float x)
    {
        int width = Screen.width;
        if (width <= 0)
        {
            return true;
        }

        return x <= width * 0.5f;
    }

    private static void NotifyCollisionDeath(string deathCause)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            GameManager.Instance.KillPlayerWithCause(deathCause);
        }
    }

    private static float GetDeltaTime()
    {
#if UNITY_INCLUDE_TESTS
        if (deltaTimeOverrideEnabled)
        {
            return deltaTimeOverrideValue;
        }
#endif

        return Time.deltaTime;
    }

    private void EnsureRuntimeComponents()
    {
        if (TryGetComponent<Rigidbody2D>(out Rigidbody2D rb2d) == false)
        {
            rb2d = gameObject.AddComponent<Rigidbody2D>();
        }

        rb2d.bodyType = RigidbodyType2D.Kinematic;
        rb2d.gravityScale = 0f;
        rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2d.useFullKinematicContacts = true;
        rb2d.freezeRotation = true;

        if (TryGetComponent<CircleCollider2D>(out CircleCollider2D circleCollider) == false)
        {
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        playerCollider = circleCollider;

        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer renderer) == false)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = GetFallbackSquareSprite();
        }

        if (renderer.color.a <= 0f)
        {
            renderer.color = new Color(0.19f, 0.94f, 1f, 1f);
        }

        if (renderer.sortingOrder < 10)
        {
            renderer.sortingOrder = 10;
        }

        ConfigureLethalOverlapFailSafe();
        EnsureTrailRenderer();
    }

    private static Sprite GetFallbackSquareSprite()
    {
        if (fallbackSquareSprite != null)
        {
            return fallbackSquareSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        fallbackSquareSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        return fallbackSquareSprite;
    }

    private void EnsureTrailRenderer()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (!enableTrail)
        {
            if (trail != null)
            {
                trail.enabled = false;
            }

            return;
        }

        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.enabled = true;
        trail.time = Mathf.Max(0f, trailTime);
        trail.minVertexDistance = 0.03f;
        trail.numCapVertices = 2;
        trail.numCornerVertices = 2;
        trail.startWidth = 0.09f;
        trail.endWidth = 0.01f;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.sortingOrder = 9;
        trail.startColor = new Color(0.22f, 0.88f, 1f, 0.7f);
        trail.endColor = new Color(0.22f, 0.88f, 1f, 0f);
        trail.material = GetTrailMaterial();
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null)
        {
            return trailMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        trailMaterial = new Material(shader);
        return trailMaterial;
    }

    private void ConfigureLethalOverlapFailSafe()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
        }

        lethalOverlapFilter = default;
        lethalOverlapFilter.useLayerMask = false;
        lethalOverlapFilter.useDepth = false;
        lethalOverlapFilter.useTriggers = true;
        EnsureOverlapBufferCapacity();
    }

    private void EnsureOverlapBufferCapacity()
    {
        int desiredSize = Mathf.Max(1, lethalOverlapBufferSize);
        if (lethalOverlapResults != null && lethalOverlapResults.Length == desiredSize)
        {
            return;
        }

        lethalOverlapResults = new Collider2D[desiredSize];
    }

    private void CheckLethalOverlapFailSafe()
    {
        if (!enableLethalOverlapFailSafe)
        {
            return;
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                return;
            }
        }

        EnsureOverlapBufferCapacity();
        int overlapCount = playerCollider.Overlap(lethalOverlapFilter, lethalOverlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D candidate = lethalOverlapResults[i];
            if (candidate == null || candidate == playerCollider)
            {
                continue;
            }

            if (candidate.attachedRigidbody != null &&
                playerCollider.attachedRigidbody != null &&
                candidate.attachedRigidbody == playerCollider.attachedRigidbody)
            {
                continue;
            }

            if (IsLethalCollider(candidate))
            {
                NotifyCollisionDeath("overlap_failsafe");
                return;
            }
        }
    }
}
