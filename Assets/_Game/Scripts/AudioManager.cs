using UnityEngine;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    [Header("Optional Clips")]
    [SerializeField] private AudioClip slowEnterClip;
    [SerializeField] private AudioClip slowExitClip;
    [SerializeField] private AudioClip nearMissClip;
    [SerializeField] private AudioClip crashClip;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float slowEnterVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float slowExitVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float nearMissVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float crashVolume = 1f;

    [Header("Pitch")]
    [SerializeField, Min(0.01f)] private float normalPitch = 1f;
    [SerializeField, Min(0.01f)] private float slowPitch = 0.75f;

    private AudioSource oneShotSource;
    private bool slowPitchActive;

    private AudioClip fallbackSlowEnterClip;
    private AudioClip fallbackSlowExitClip;
    private AudioClip fallbackNearMissClip;
    private AudioClip fallbackCrashClip;

    private void Awake()
    {
        EnsureAudioSource();
        ApplyPitch();
    }

    private void OnValidate()
    {
        normalPitch = Mathf.Max(0.01f, normalPitch);
        slowPitch = Mathf.Max(0.01f, slowPitch);
        masterVolume = Mathf.Clamp01(masterVolume);

        if (oneShotSource != null)
        {
            ApplyPitch();
        }
    }

    public void PlaySlowEnter()
    {
        PlayOneShot(slowEnterClip, ref fallbackSlowEnterClip, "Fallback_SlowEnter", 460f, 300f, 0.06f, slowEnterVolume);
    }

    public void PlaySlowExit()
    {
        PlayOneShot(slowExitClip, ref fallbackSlowExitClip, "Fallback_SlowExit", 300f, 440f, 0.05f, slowExitVolume);
    }

    public void PlayNearMiss()
    {
        PlayOneShot(nearMissClip, ref fallbackNearMissClip, "Fallback_NearMiss", 840f, 1200f, 0.07f, nearMissVolume);
    }

    public void PlayCrash()
    {
        PlayOneShot(crashClip, ref fallbackCrashClip, "Fallback_Crash", 210f, 90f, 0.12f, crashVolume);
    }

    public void SetSlowPitch(bool active)
    {
        slowPitchActive = active;
        ApplyPitch();
    }

    private void PlayOneShot(
        AudioClip assignedClip,
        ref AudioClip fallbackClip,
        string fallbackName,
        float startFrequency,
        float endFrequency,
        float duration,
        float volume)
    {
        EnsureAudioSource();

        AudioClip clipToPlay = assignedClip;
        if (clipToPlay == null)
        {
            fallbackClip ??= CreateSweepClip(fallbackName, startFrequency, endFrequency, duration);
            clipToPlay = fallbackClip;
        }

        if (clipToPlay == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(clipToPlay, Mathf.Clamp01(masterVolume * volume));
    }

    private void EnsureAudioSource()
    {
        if (oneShotSource != null)
        {
            return;
        }

        oneShotSource = GetComponent<AudioSource>();
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
        }

        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.volume = 1f;
    }

    private void ApplyPitch()
    {
        if (oneShotSource == null)
        {
            return;
        }

        oneShotSource.pitch = slowPitchActive ? slowPitch : normalPitch;
    }

    private static AudioClip CreateSweepClip(string clipName, float startFrequency, float endFrequency, float duration)
    {
        int sampleRate = 22050;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];

        float safeStart = Mathf.Max(20f, startFrequency);
        float safeEnd = Mathf.Max(20f, endFrequency);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float frequency = Mathf.Lerp(safeStart, safeEnd, t);
            float phase = 2f * Mathf.PI * frequency * (i / (float)sampleRate);

            float attack = Mathf.Clamp01(t / 0.1f);
            float release = Mathf.Clamp01((1f - t) / 0.2f);
            float envelope = attack * release;

            samples[i] = Mathf.Sin(phase) * envelope * 0.25f;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
