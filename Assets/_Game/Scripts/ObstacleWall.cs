using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleWall : MonoBehaviour
{
    private enum GapMotionMode
    {
        Static = 0,
        Oscillating = 1
    }

    [Header("Parts")]
    [SerializeField] private Transform leftPart;
    [SerializeField] private Transform rightPart;
    [SerializeField] private BoxCollider2D leftCollider;
    [SerializeField] private BoxCollider2D rightCollider;
    [SerializeField] private SpriteRenderer leftSprite;
    [SerializeField] private SpriteRenderer rightSprite;

    [Header("Geometry")]
    [SerializeField, Min(0.1f)] private float wallHeight = 0.95f;
    [Header("Visuals")]
    [SerializeField] private Color lowDangerColor = new Color(0.98f, 0.98f, 1f, 1f);
    [SerializeField] private Color highDangerColor = new Color(0.82f, 0.96f, 1f, 1f);
    [SerializeField] private Gradient dangerGradient;
    [SerializeField, Range(0f, 1f)] private float minDangerAlpha = 0.82f;
    [SerializeField] private int baseSortingOrder;
    [SerializeField, Min(0)] private int maxAdditionalSortingOrder = 3;
    [SerializeField] private AnimationCurve dangerResponseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private const float MinSegmentWidth = 0.01f;
    private static Sprite fallbackSquareSprite;
    private float dangerIntensity;
    private float configuredGapWidth;
    private float configuredWallHalfWidth;
    private float baseGapCenter;
    private float centerMinLimit;
    private float centerMaxLimit;
    private GapMotionMode gapMotionMode;
    private float oscillationAmplitude;
    private float oscillationAngularFrequency;
    private float oscillationPhase;
    private float oscillationElapsed;

    public float GapLeft { get; private set; }
    public float GapRight { get; private set; }
    public float WallHeight => wallHeight;
    public float WallHalfWidth => Mathf.Max(0.1f, configuredWallHalfWidth);
    public bool PassedPlayer { get; private set; }
    public bool WasNearMiss { get; private set; }
    public float NearMissDistance { get; private set; }

    private void Awake()
    {
        EnsureParts();
        EnsureLethalMarkers();
        ApplyDangerVisuals();
    }

    public void Configure(float gapCenterX, float gapWidth, float wallHalfWidth, float y)
    {
        EnsureLethalMarkers();
        configuredWallHalfWidth = Mathf.Max(0.1f, wallHalfWidth);
        configuredGapWidth = Mathf.Max(0.1f, gapWidth);
        float safeGapHalf = configuredGapWidth * 0.5f;
        centerMinLimit = -configuredWallHalfWidth + safeGapHalf;
        centerMaxLimit = configuredWallHalfWidth - safeGapHalf;
        baseGapCenter = Mathf.Clamp(gapCenterX, centerMinLimit, centerMaxLimit);
        PassedPlayer = false;
        WasNearMiss = false;
        NearMissDistance = float.PositiveInfinity;
        ResetGapMotion();
        SetGapCenter(baseGapCenter);
        SetY(y);
        ApplyDangerVisuals();
    }

    public void ConfigureOscillation(float amplitude, float frequencyHz, float phase, float minCenter, float maxCenter)
    {
        float limitedMinCenter = Mathf.Max(centerMinLimit, Mathf.Min(minCenter, maxCenter));
        float limitedMaxCenter = Mathf.Min(centerMaxLimit, Mathf.Max(minCenter, maxCenter));
        if (limitedMaxCenter < limitedMinCenter)
        {
            limitedMaxCenter = limitedMinCenter;
        }

        centerMinLimit = limitedMinCenter;
        centerMaxLimit = limitedMaxCenter;
        baseGapCenter = Mathf.Clamp(baseGapCenter, centerMinLimit, centerMaxLimit);
        float maxAmplitude = Mathf.Max(
            0f,
            Mathf.Min(baseGapCenter - centerMinLimit, centerMaxLimit - baseGapCenter));

        oscillationAmplitude = Mathf.Clamp(amplitude, 0f, maxAmplitude);
        oscillationAngularFrequency = Mathf.Max(0f, frequencyHz) * Mathf.PI * 2f;
        oscillationPhase = phase;
        oscillationElapsed = 0f;

        if (oscillationAmplitude <= 0f || oscillationAngularFrequency <= 0f || centerMaxLimit <= centerMinLimit)
        {
            ResetGapMotion();
            SetGapCenter(baseGapCenter);
            return;
        }

        gapMotionMode = GapMotionMode.Oscillating;
        UpdateOscillatingGapCenter();
    }

    public void Simulate(float dt)
    {
        if (gapMotionMode != GapMotionMode.Oscillating)
        {
            return;
        }

        oscillationElapsed += Mathf.Max(0f, dt);
        UpdateOscillatingGapCenter();
    }

    public bool OverlapsSolidBounds(Bounds bounds, float edgePadding = 0f)
    {
        float halfHeight = wallHeight * 0.5f;
        float wallY = transform.position.y;
        if (bounds.max.y < wallY - halfHeight || bounds.min.y > wallY + halfHeight)
        {
            return false;
        }

        float wallMinX = -WallHalfWidth;
        float wallMaxX = WallHalfWidth;
        if (bounds.max.x <= wallMinX || bounds.min.x >= wallMaxX)
        {
            return false;
        }

        float safePadding = Mathf.Max(0f, edgePadding);
        float paddedLeft = GapLeft + safePadding;
        float paddedRight = GapRight - safePadding;
        if (paddedLeft >= paddedRight)
        {
            return true;
        }

        float overlapMinX = Mathf.Max(bounds.min.x, wallMinX);
        float overlapMaxX = Mathf.Min(bounds.max.x, wallMaxX);
        if (overlapMinX >= overlapMaxX)
        {
            return false;
        }

        return overlapMinX < paddedLeft || overlapMaxX > paddedRight;
    }

    public void SetY(float y)
    {
        Vector3 position = transform.position;
        position.y = y;
        transform.position = position;
    }

    public void MoveDown(float amount)
    {
        Vector3 position = transform.position;
        position.y -= amount;
        transform.position = position;
    }

    public bool TryRegisterPass(float playerY, float playerX, float nearMissThreshold)
    {
        if (PassedPlayer || transform.position.y > playerY)
        {
            return false;
        }

        PassedPlayer = true;
        NearMissDistance = CalculateDistanceToNearestGapEdge(playerX);
        WasNearMiss = nearMissThreshold > 0f && NearMissDistance <= nearMissThreshold;
        return true;
    }

    public bool IsInsideGap(float playerX, float edgePadding = 0f)
    {
        float safePadding = Mathf.Max(0f, edgePadding);
        float paddedLeft = GapLeft + safePadding;
        float paddedRight = GapRight - safePadding;

        if (paddedLeft >= paddedRight)
        {
            return false;
        }

        return playerX > paddedLeft && playerX < paddedRight;
    }

    public void SetDangerIntensity(float normalizedIntensity)
    {
        dangerIntensity = Mathf.Clamp01(normalizedIntensity);
        ApplyDangerVisuals();
    }

    private float CalculateDistanceToNearestGapEdge(float playerX)
    {
        if (playerX <= GapLeft)
        {
            return GapLeft - playerX;
        }

        if (playerX >= GapRight)
        {
            return playerX - GapRight;
        }

        float leftDistance = playerX - GapLeft;
        float rightDistance = GapRight - playerX;
        return leftDistance < rightDistance ? leftDistance : rightDistance;
    }

    private void ApplyGeometry(float wallHalfWidth)
    {
        float totalWidth = wallHalfWidth * 2f;
        float leftWidth = Mathf.Clamp(GapLeft + wallHalfWidth, 0f, totalWidth);
        float rightWidth = Mathf.Clamp(wallHalfWidth - GapRight, 0f, totalWidth);

        ConfigurePart(
            leftPart,
            leftCollider,
            leftSprite,
            -wallHalfWidth + (leftWidth * 0.5f),
            leftWidth);

        ConfigurePart(
            rightPart,
            rightCollider,
            rightSprite,
            GapRight + (rightWidth * 0.5f),
            rightWidth);
    }

    private void ConfigurePart(Transform part, BoxCollider2D collider, SpriteRenderer sprite, float centerX, float width)
    {
        if (part == null)
        {
            return;
        }

        bool visible = width > MinSegmentWidth;
        part.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector3 localPosition = part.localPosition;
        localPosition.x = centerX;
        localPosition.y = 0f;
        part.localPosition = localPosition;

        if (collider != null)
        {
            collider.size = new Vector2(width, wallHeight);
            collider.offset = Vector2.zero;
            collider.isTrigger = true;
        }

        if (sprite != null)
        {
            if (sprite.drawMode == SpriteDrawMode.Simple)
            {
                sprite.drawMode = SpriteDrawMode.Tiled;
            }

            sprite.size = new Vector2(width, wallHeight);
        }

        part.localScale = Vector3.one;
    }

    private void SetGapCenter(float gapCenterX)
    {
        float clampedCenter = Mathf.Clamp(gapCenterX, centerMinLimit, centerMaxLimit);
        float safeGapHalf = configuredGapWidth * 0.5f;
        GapLeft = clampedCenter - safeGapHalf;
        GapRight = clampedCenter + safeGapHalf;
        ApplyGeometry(Mathf.Max(0.1f, configuredWallHalfWidth));
    }

    private void ResetGapMotion()
    {
        gapMotionMode = GapMotionMode.Static;
        oscillationAmplitude = 0f;
        oscillationAngularFrequency = 0f;
        oscillationPhase = 0f;
        oscillationElapsed = 0f;
    }

    private void UpdateOscillatingGapCenter()
    {
        float offset = Mathf.Sin(oscillationPhase + (oscillationElapsed * oscillationAngularFrequency)) * oscillationAmplitude;
        SetGapCenter(baseGapCenter + offset);
    }

    private void EnsureParts()
    {
        if (leftPart == null)
        {
            CreatePart("Left", out leftPart, out leftCollider, out leftSprite);
        }

        if (rightPart == null)
        {
            CreatePart("Right", out rightPart, out rightCollider, out rightSprite);
        }

        if (leftCollider == null && leftPart != null)
        {
            leftCollider = leftPart.GetComponent<BoxCollider2D>() ?? leftPart.gameObject.AddComponent<BoxCollider2D>();
        }

        if (rightCollider == null && rightPart != null)
        {
            rightCollider = rightPart.GetComponent<BoxCollider2D>() ?? rightPart.gameObject.AddComponent<BoxCollider2D>();
        }

        if (leftSprite == null && leftPart != null)
        {
            leftSprite = leftPart.GetComponent<SpriteRenderer>();
        }

        if (rightSprite == null && rightPart != null)
        {
            rightSprite = rightPart.GetComponent<SpriteRenderer>();
        }

        if (leftSprite != null && leftSprite.sprite == null)
        {
            leftSprite.sprite = GetFallbackSquareSprite();
        }

        if (rightSprite != null && rightSprite.sprite == null)
        {
            rightSprite.sprite = GetFallbackSquareSprite();
        }

        if (leftSprite != null && baseSortingOrder == 0)
        {
            baseSortingOrder = leftSprite.sortingOrder;
        }
        else if (rightSprite != null && baseSortingOrder == 0)
        {
            baseSortingOrder = rightSprite.sortingOrder;
        }
    }

    private void CreatePart(string partName, out Transform part, out BoxCollider2D collider, out SpriteRenderer sprite)
    {
        GameObject partObject = new GameObject(partName);
        partObject.transform.SetParent(transform, false);

        part = partObject.transform;
        collider = partObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        sprite = partObject.AddComponent<SpriteRenderer>();
        sprite.sprite = GetFallbackSquareSprite();
        sprite.color = lowDangerColor;
    }

    private void EnsureLethalMarkers()
    {
        EnsureMarker(gameObject);
        if (leftPart != null)
        {
            EnsureMarker(leftPart.gameObject);
        }

        if (rightPart != null)
        {
            EnsureMarker(rightPart.gameObject);
        }
    }

    private static void EnsureMarker(GameObject target)
    {
        if (target == null || target.GetComponent<LethalObstacle>() != null)
        {
            return;
        }

        target.AddComponent<LethalObstacle>();
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

    private void ApplyDangerVisuals()
    {
        float curvedIntensity = EvaluateDangerCurve(dangerIntensity);
        Color dangerColor = EvaluateDangerColor(curvedIntensity);
        int sortingOrder = baseSortingOrder + Mathf.RoundToInt(Mathf.Lerp(0f, maxAdditionalSortingOrder, curvedIntensity));

        if (leftSprite != null)
        {
            leftSprite.color = dangerColor;
            leftSprite.sortingOrder = sortingOrder;
        }

        if (rightSprite != null)
        {
            rightSprite.color = dangerColor;
            rightSprite.sortingOrder = sortingOrder;
        }
    }

    private float EvaluateDangerCurve(float normalizedIntensity)
    {
        if (dangerResponseCurve == null || dangerResponseCurve.length == 0)
        {
            return Mathf.Clamp01(normalizedIntensity);
        }

        return Mathf.Clamp01(dangerResponseCurve.Evaluate(normalizedIntensity));
    }

    private Color EvaluateDangerColor(float curvedIntensity)
    {
        Color gradientColor;
        if (dangerGradient != null && dangerGradient.colorKeys != null && dangerGradient.colorKeys.Length > 0)
        {
            gradientColor = dangerGradient.Evaluate(curvedIntensity);
        }
        else
        {
            gradientColor = Color.Lerp(lowDangerColor, highDangerColor, curvedIntensity);
        }

        gradientColor.a = Mathf.Max(minDangerAlpha, gradientColor.a);
        return gradientColor;
    }
}
