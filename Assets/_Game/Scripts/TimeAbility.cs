using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TimeAbility : MonoBehaviour
{
    [SerializeField, Range(0.05f, 1f)] private float slowScale = 0.32f;
    [SerializeField, Range(0.1f, 5f)] private float maxSlowSeconds = 1f;
    [SerializeField] private bool requireTwoTouchForSlow = true;
    [SerializeField, Min(0f)] private float touchStartSlowActivationLockSeconds = 0.2f;
    [SerializeField] private bool requireTouchReleaseAfterRunStart = true;
    [SerializeField] private AudioManager audioManager;

    private float baseFixedDeltaTime;
    private bool slowActive;
    private bool slowMeterDepletedEmitted;
    private float touchSlowUnlockUnscaledTime;
    private bool touchReleasedSinceRunStart;
#if UNITY_INCLUDE_TESTS
    private static bool slowHoldOverrideEnabled;
    private static bool slowHoldOverrideValue;
    private static bool unscaledDeltaTimeOverrideEnabled;
    private static float unscaledDeltaTimeOverrideValue;
#endif

    public float RemainingSeconds { get; private set; }
    public float MaxSlowSeconds => maxSlowSeconds;
    public bool SlowActive => slowActive;

    private void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;
        RemainingSeconds = maxSlowSeconds;
        slowActive = false;
        slowMeterDepletedEmitted = false;
        ArmTouchStartGate();
        ApplyTimeScale(false);
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
        GameManager manager = GameManager.Instance;
        if (manager != null && !manager.IsPlaying)
        {
            ForceNormalTime();
            return;
        }

        if (!touchReleasedSinceRunStart && !HasAnySlowInputPressed())
        {
            touchReleasedSinceRunStart = true;
        }

        bool wantsSlow = IsSlowHoldActive() && RemainingSeconds > 0f;
        if (wantsSlow)
        {
            SetSlowActive(true);
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - GetUnscaledDeltaTime());
            if (RemainingSeconds <= 0f)
            {
                SetSlowActive(false);
                EmitSlowMeterDepletedAnalytics();
            }
            return;
        }

        SetSlowActive(false);
    }

    public void ResetMeter()
    {
        RemainingSeconds = maxSlowSeconds;
        slowMeterDepletedEmitted = false;
        ArmTouchStartGate();
        ForceNormalTime();
    }

    public void ForceNormalTime()
    {
        if (slowActive)
        {
            SetSlowActive(false, playAudio: false);
            return;
        }

        ApplyTimeScale(false);
        audioManager?.SetSlowPitch(false);
    }

    public void Configure(AudioManager manager)
    {
        audioManager = manager;
        audioManager?.SetSlowPitch(slowActive);
    }

#if UNITY_INCLUDE_TESTS
    public static void SetSlowHoldOverrideForTests(bool active)
    {
        slowHoldOverrideEnabled = true;
        slowHoldOverrideValue = active;
    }

    public static void ClearSlowHoldOverrideForTests()
    {
        slowHoldOverrideEnabled = false;
        slowHoldOverrideValue = false;
    }

    public static void SetUnscaledDeltaTimeOverrideForTests(float deltaTime)
    {
        unscaledDeltaTimeOverrideEnabled = true;
        unscaledDeltaTimeOverrideValue = Mathf.Max(0f, deltaTime);
    }

    public static void ClearUnscaledDeltaTimeOverrideForTests()
    {
        unscaledDeltaTimeOverrideEnabled = false;
        unscaledDeltaTimeOverrideValue = 0f;
    }
#endif

    private void SetSlowActive(bool active, bool playAudio = true)
    {
        if (slowActive == active)
        {
            return;
        }

        slowActive = active;
        ApplyTimeScale(slowActive);
        EmitSlowStateAnalytics(slowActive);

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

    private void EmitSlowStateAnalytics(bool enteredSlow)
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(6)
        {
            ["remaining_slow_seconds"] = RemainingSeconds,
            ["max_slow_seconds"] = maxSlowSeconds
        };

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track(enteredSlow ? "slow_enter" : "slow_exit", fields);
    }

    private void EmitSlowMeterDepletedAnalytics()
    {
        if (slowMeterDepletedEmitted)
        {
            return;
        }

        slowMeterDepletedEmitted = true;

        Dictionary<string, object> fields = new Dictionary<string, object>(6)
        {
            ["remaining_slow_seconds"] = RemainingSeconds,
            ["max_slow_seconds"] = maxSlowSeconds
        };

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track("slow_meter_depleted", fields);
    }

    private void ApplyTimeScale(bool slow)
    {
        float targetTimeScale = slow ? slowScale : 1f;
        float targetFixedDelta = slow ? baseFixedDeltaTime * slowScale : baseFixedDeltaTime;
        if (Mathf.Approximately(Time.timeScale, targetTimeScale) &&
            Mathf.Approximately(Time.fixedDeltaTime, targetFixedDelta))
        {
            return;
        }

        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = targetFixedDelta;
    }

    private void ArmTouchStartGate()
    {
        touchSlowUnlockUnscaledTime = Time.unscaledTime + Mathf.Max(0f, touchStartSlowActivationLockSeconds);
        touchReleasedSinceRunStart = !requireTouchReleaseAfterRunStart || !HasAnyTouchPressed();
    }

    private bool IsTouchSlowAllowed()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.IsPlaying)
        {
            return true;
        }

        if (Time.unscaledTime < touchSlowUnlockUnscaledTime)
        {
            return false;
        }

        return touchReleasedSinceRunStart;
    }

    private bool IsSlowHoldActive()
    {
#if UNITY_INCLUDE_TESTS
        if (slowHoldOverrideEnabled)
        {
            return slowHoldOverrideValue;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            int pressedTouches = 0;
            bool rightHalfPressed = false;
            foreach (var candidate in touch.touches)
            {
                if (!candidate.press.isPressed)
                {
                    continue;
                }

                pressedTouches++;
                if (IsOnRightHalf(candidate.position.ReadValue().x))
                {
                    rightHalfPressed = true;
                }
            }

            if (rightHalfPressed)
            {
                if (!requireTwoTouchForSlow || pressedTouches >= 2)
                {
                    if (IsTouchSlowAllowed())
                    {
                        return true;
                    }
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            return IsTouchSlowAllowed();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        int pressedTouches = 0;
        bool rightHalfPressed = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            pressedTouches++;
            if (IsOnRightHalf(Input.GetTouch(i).position.x))
            {
                rightHalfPressed = true;
            }
        }

        if (rightHalfPressed && (!requireTwoTouchForSlow || pressedTouches >= 2))
        {
            if (IsTouchSlowAllowed())
            {
                return true;
            }
        }

        if (Input.GetMouseButton(1))
        {
            return IsTouchSlowAllowed();
        }

        return false;
#else
        return false;
#endif
    }

    private static bool HasAnySlowInputPressed()
    {
        if (HasAnyTouchPressed())
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
        {
            return true;
        }
#endif

        return false;
    }

    private static bool HasAnyTouchPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            foreach (var candidate in touch.touches)
            {
                if (candidate.press.isPressed)
                {
                    return true;
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            return true;
        }
#endif

        return false;
    }

    private static bool IsOnRightHalf(float x)
    {
        int width = Screen.width;
        if (width <= 0)
        {
            return false;
        }

        return x > width * 0.5f;
    }

    private static float GetUnscaledDeltaTime()
    {
#if UNITY_INCLUDE_TESTS
        if (unscaledDeltaTimeOverrideEnabled)
        {
            return unscaledDeltaTimeOverrideValue;
        }
#endif

        return Time.unscaledDeltaTime;
    }
}
