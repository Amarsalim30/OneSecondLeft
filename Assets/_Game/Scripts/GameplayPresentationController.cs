using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameplayPresentationController : MonoBehaviour
{
    private const float NearMissEventEpsilon = 0.0001f;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayerController player;
    [SerializeField] private TimeAbility timeAbility;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ObstacleSpawner obstacleSpawner;
    [SerializeField] private AudioManager audioManager;

    [Header("Palette")]
    [SerializeField] private Color baseBackgroundColor = new Color(0.01f, 0.01f, 0.015f, 1f);
    [SerializeField] private Color accentColor = new Color(0.2f, 0.95f, 1f, 1f);
    [SerializeField] private Color signatureBackgroundColor = new Color(0.02f, 0.12f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float signatureTintStrength = 0.5f;
    [SerializeField] private Color slowVignetteColor = new Color(0.05f, 0.2f, 0.25f, 1f);
    [SerializeField, Range(0f, 1f)] private float slowVignetteMaxAlpha = 0.24f;
    [SerializeField, Min(0.01f)] private float vignetteLerpSpeed = 6f;

    [Header("Signature Moment")]
    [SerializeField, Min(0f)] private float signatureMomentSeconds = 15f;
    [SerializeField, Min(0.05f)] private float signatureTransitionDuration = 0.4f;

    [Header("Particles")]
    [SerializeField] private Color nearMissParticleColor = new Color(0.28f, 0.98f, 1f, 1f);
    [SerializeField] private Color deathParticleColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField, Min(1)] private int nearMissBurstCount = 14;
    [SerializeField, Min(1)] private int deathBurstCount = 28;
    [SerializeField, Min(0f)] private float nearMissBurstRadius = 0.18f;
    [SerializeField, Min(0f)] private float deathBurstRadius = 0.28f;
    [SerializeField, Min(0.01f)] private float particleLifetime = 0.32f;
    [SerializeField, Min(0.01f)] private float particleStartSize = 0.1f;
    [SerializeField, Min(0.01f)] private float particleStartSpeed = 3f;

    [Header("Death Feel")]
    [SerializeField, Min(0f)] private float deathFreezeSeconds = 0.15f;
    [SerializeField, Min(0f)] private float deathShakeDuration = 0.2f;
    [SerializeField, Min(0f)] private float deathShakeMagnitude = 0.16f;

    private static Sprite cachedVignetteSprite;

    private Image vignetteImage;
    private ParticleSystem burstParticles;
    private float vignetteAlpha;
    private float signatureBlend;
    private bool signatureTriggeredThisRun;
    private bool wasPlayingLastFrame;
    private float lastNearMissEventTime = float.NegativeInfinity;
    private Coroutine deathRoutine;
    private Vector3 cameraBasePosition;
    private bool hasCameraBasePosition;

    public void Configure(
        Camera cameraRef,
        PlayerController playerRef,
        TimeAbility ability,
        ScoreManager scoreRef,
        ObstacleSpawner spawnerRef,
        AudioManager audioRef)
    {
        targetCamera = cameraRef;
        player = playerRef;
        timeAbility = ability;
        scoreManager = scoreRef;
        obstacleSpawner = spawnerRef;
        audioManager = audioRef;
        CacheCameraBasePosition();
        ApplyIdentityPalette();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureVignetteOverlay();
        EnsureBurstParticles();
        CacheCameraBasePosition();
        ApplyIdentityPalette();
        wasPlayingLastFrame = IsGamePlaying();
    }

    private void Update()
    {
        ResolveReferences();

        bool isPlaying = IsGamePlaying();
        if (isPlaying && !wasPlayingLastFrame)
        {
            OnRunStarted();
        }
        else if (!isPlaying && wasPlayingLastFrame)
        {
            OnRunEnded();
        }

        wasPlayingLastFrame = isPlaying;

        HandleNearMissBurst();
        HandleSignatureMoment(isPlaying);
        UpdatePresentation(isPlaying);
    }

    private void OnDisable()
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        if (targetCamera != null && hasCameraBasePosition)
        {
            targetCamera.transform.position = cameraBasePosition;
        }
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        player ??= FindFirstObjectByType<PlayerController>();
        timeAbility ??= FindFirstObjectByType<TimeAbility>();
        scoreManager ??= FindFirstObjectByType<ScoreManager>();
        obstacleSpawner ??= FindFirstObjectByType<ObstacleSpawner>();
        audioManager ??= FindFirstObjectByType<AudioManager>();
    }

    private bool IsGamePlaying()
    {
        return GameManager.Instance == null || GameManager.Instance.IsPlaying;
    }

    private void OnRunStarted()
    {
        signatureTriggeredThisRun = false;
        signatureBlend = 0f;
        lastNearMissEventTime = float.NegativeInfinity;

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        CacheCameraBasePosition();
        if (targetCamera != null && hasCameraBasePosition)
        {
            targetCamera.transform.position = cameraBasePosition;
        }
    }

    private void OnRunEnded()
    {
        EmitBurstAtPlayer(deathBurstCount, deathBurstRadius, deathParticleColor);
        audioManager?.PlayShatter();

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
        }

        deathRoutine = StartCoroutine(PlayDeathFeelRoutine());
    }

    private IEnumerator PlayDeathFeelRoutine()
    {
        CacheCameraBasePosition();

        float freeze = Mathf.Max(0f, deathFreezeSeconds);
        if (freeze > 0f)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(freeze);
            Time.timeScale = 1f;
        }

        if (targetCamera != null && hasCameraBasePosition && deathShakeDuration > 0f && deathShakeMagnitude > 0f)
        {
            float elapsed = 0f;
            while (elapsed < deathShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, deathShakeDuration));
                Vector2 offset = Random.insideUnitCircle * deathShakeMagnitude * damper;
                targetCamera.transform.position = cameraBasePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            targetCamera.transform.position = cameraBasePosition;
        }

        deathRoutine = null;
    }

    private void HandleNearMissBurst()
    {
        if (scoreManager == null)
        {
            return;
        }

        float eventTime = scoreManager.LastNearMissUnscaledTime;
        if (float.IsNegativeInfinity(eventTime) || eventTime <= (lastNearMissEventTime + NearMissEventEpsilon))
        {
            return;
        }

        lastNearMissEventTime = eventTime;
        EmitBurstAtPlayer(nearMissBurstCount, nearMissBurstRadius, nearMissParticleColor);
    }

    private void HandleSignatureMoment(bool isPlaying)
    {
        if (!isPlaying || signatureTriggeredThisRun || obstacleSpawner == null)
        {
            return;
        }

        if (obstacleSpawner.RunElapsedSeconds < Mathf.Max(0f, signatureMomentSeconds))
        {
            return;
        }

        signatureTriggeredThisRun = true;
        audioManager?.PlaySignatureMoment();
    }

    private void UpdatePresentation(bool isPlaying)
    {
        float transitionDuration = Mathf.Max(0.05f, signatureTransitionDuration);
        float targetSignatureBlend = signatureTriggeredThisRun ? 1f : 0f;
        signatureBlend = Mathf.MoveTowards(signatureBlend, targetSignatureBlend, Time.unscaledDeltaTime / transitionDuration);

        if (targetCamera != null)
        {
            Color signatureTintColor = Color.Lerp(baseBackgroundColor, signatureBackgroundColor, Mathf.Clamp01(signatureTintStrength));
            targetCamera.backgroundColor = Color.Lerp(baseBackgroundColor, signatureTintColor, signatureBlend);
        }

        float targetVignetteAlpha = 0f;
        if (isPlaying && timeAbility != null && timeAbility.SlowActive)
        {
            targetVignetteAlpha = Mathf.Clamp01(slowVignetteMaxAlpha);
        }

        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, targetVignetteAlpha, Mathf.Max(0.01f, vignetteLerpSpeed) * Time.unscaledDeltaTime);
        if (vignetteImage != null)
        {
            Color color = slowVignetteColor;
            color.a = vignetteAlpha;
            vignetteImage.color = color;
        }

        if (audioManager != null && isPlaying)
        {
            float slowPenalty = timeAbility != null && timeAbility.SlowActive ? 0.08f : 0f;
            float humIntensity = Mathf.Clamp01(Mathf.Lerp(0.55f, 0.95f, signatureBlend) - slowPenalty);
            audioManager.SetAmbientHumIntensity(humIntensity);
        }
    }

    private void ApplyIdentityPalette()
    {
        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = baseBackgroundColor;
        }

        if (player != null && player.TryGetComponent(out SpriteRenderer renderer))
        {
            renderer.color = accentColor;
        }

        if (player != null && player.TryGetComponent(out TrailRenderer trail))
        {
            Color start = accentColor;
            start.a = Mathf.Max(start.a, 0.8f);
            Color end = accentColor;
            end.a = 0f;
            trail.startColor = start;
            trail.endColor = end;
        }
    }

    private void CacheCameraBasePosition()
    {
        if (targetCamera == null)
        {
            return;
        }

        cameraBasePosition = targetCamera.transform.position;
        hasCameraBasePosition = true;
    }

    private void EmitBurstAtPlayer(int count, float radius, Color color)
    {
        if (burstParticles == null || player == null || count <= 0)
        {
            return;
        }

        Vector3 center = player.transform.position;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            startColor = color,
            startLifetime = Mathf.Max(0.01f, particleLifetime),
            startSize = Mathf.Max(0.01f, particleStartSize)
        };

        float safeRadius = Mathf.Max(0f, radius);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * safeRadius;
            emit.position = center + new Vector3(offset.x, offset.y, 0f);
            burstParticles.Emit(emit, 1);
        }
    }

    private void EnsureBurstParticles()
    {
        if (burstParticles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("GameplayBurstParticles");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.zero;

        burstParticles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = burstParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        main.startLifetime = Mathf.Max(0.01f, particleLifetime);
        main.startSpeed = Mathf.Max(0.01f, particleStartSpeed);
        main.startSize = Mathf.Max(0.01f, particleStartSize);
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = burstParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = burstParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        var renderer = burstParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 20;
    }

    private void EnsureVignetteOverlay()
    {
        if (vignetteImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("GameplayVignetteCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;

        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject imageObject = new GameObject("Vignette");
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        vignetteImage = imageObject.AddComponent<Image>();
        vignetteImage.raycastTarget = false;
        vignetteImage.sprite = GetOrCreateVignetteSprite();
        vignetteImage.color = new Color(0f, 0f, 0f, 0f);
    }

    private static Sprite GetOrCreateVignetteSprite()
    {
        if (cachedVignetteSprite != null)
        {
            return cachedVignetteSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                float alpha = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 1f, distance));
                alpha *= alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cachedVignetteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        return cachedVignetteSprite;
    }
}
