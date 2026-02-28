using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TimeAbility : MonoBehaviour
{
    [SerializeField, Range(0.05f, 1f)] private float slowScale = 0.32f;
    [SerializeField, Range(0.1f, 5f)] private float maxSlowSeconds = 1f;
    [SerializeField] private AudioManager audioManager;

    private float baseFixedDeltaTime;
    private bool slowActive;

    public float RemainingSeconds { get; private set; }
    public float MaxSlowSeconds => maxSlowSeconds;
    public bool SlowActive => slowActive;

    private void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
        RemainingSeconds = maxSlowSeconds;
        SetSlowActive(false, playAudio: false);
    }

    private void OnDisable()
    {
        ForceNormalTime();
    }

    private void OnDestroy()
    {
        ForceNormalTime();
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
        {
            ForceNormalTime();
            return;
        }

        bool wantsSlow = IsHoldActive() && RemainingSeconds > 0f;
        if (wantsSlow)
        {
            SetSlowActive(true);
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.unscaledDeltaTime);
            if (RemainingSeconds <= 0f)
            {
                SetSlowActive(false);
            }
            return;
        }

        SetSlowActive(false);
    }

    public void ResetMeter()
    {
        RemainingSeconds = maxSlowSeconds;
        SetSlowActive(false, playAudio: false);
    }

    public void ForceNormalTime()
    {
        SetSlowActive(false, playAudio: false);
    }

    public void Configure(AudioManager manager)
    {
        audioManager = manager;
        audioManager?.SetSlowPitch(slowActive);
    }

    private void SetSlowActive(bool active, bool playAudio = true)
    {
        if (slowActive == active)
        {
            ApplyTimeScale(active);
            return;
        }

        slowActive = active;
        ApplyTimeScale(slowActive);

        if (audioManager != null)
        {
            audioManager.SetSlowPitch(slowActive);
            if (playAudio)
            {
                if (slowActive)
                {
                    audioManager.PlaySlowEnter();
                }
                else
                {
                    audioManager.PlaySlowExit();
                }
            }
        }
    }

    private void ApplyTimeScale(bool slow)
    {
        if (slow)
        {
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = baseFixedDeltaTime * slowScale;
            return;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    private static bool IsHoldActive()
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.touchCount > 0 || Input.GetMouseButton(0);
#else
        return false;
#endif
    }
}
