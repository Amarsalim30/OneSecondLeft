using UnityEngine;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    [Header("Optional Clips")]
    [SerializeField] private AudioClip slowEnterClip;
    [SerializeField] private AudioClip slowExitClip;
    [SerializeField] private AudioClip nearMissClip;
    [SerializeField] private AudioClip crashClip;
    [SerializeField] private AudioClip signatureMomentClip;
    [SerializeField] private AudioClip shatterClip;
    [SerializeField] private AudioClip ambientHumClip;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float slowEnterVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float slowExitVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float nearMissVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float crashVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float signatureMomentVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float shatterVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float ambientHumVolume = 0.3f;

    [Header("Pitch")]
    [SerializeField, Min(0.01f)] private float normalPitch = 1f;
    [SerializeField, Min(0.01f)] private float slowPitch = 0.75f;

    [Header("Ambient Hum Fallback")]
    [SerializeField, Min(20f)] private float ambientHumBaseFrequency = 72f;
    [SerializeField, Min(0.1f)] private float ambientHumDetune = 1.3f;
    [SerializeField, Min(0.25f)] private float ambientHumFallbackDuration = 2f;

    private AudioSource oneShotSource;
    private AudioSource ambientHumSource;
    private bool slowPitchActive;
    private bool ambientHumActive;
    private float ambientHumIntensity = 1f;

    private AudioClip fallbackSlowEnterClip;
    private AudioClip fallbackSlowExitClip;
    private AudioClip fallbackNearMissClip;
    private AudioClip fallbackCrashClip;
    private AudioClip fallbackSignatureMomentClip;
    private AudioClip fallbackShatterClip;
    private AudioClip fallbackAmbientHumClip;

    private void Awake()
    {
        EnsureAudioSources();
        ApplyPitch();
        ApplyAmbientHumState();
    }

    private void OnValidate()
    {
        normalPitch = Mathf.Max(0.01f, normalPitch);
        slowPitch = Mathf.Max(0.01f, slowPitch);
        masterVolume = Mathf.Clamp01(masterVolume);
        slowEnterVolume = Mathf.Clamp01(slowEnterVolume);
        slowExitVolume = Mathf.Clamp01(slowExitVolume);
        nearMissVolume = Mathf.Clamp01(nearMissVolume);
        crashVolume = Mathf.Clamp01(crashVolume);
        signatureMomentVolume = Mathf.Clamp01(signatureMomentVolume);
        shatterVolume = Mathf.Clamp01(shatterVolume);
        ambientHumVolume = Mathf.Clamp01(ambientHumVolume);
        ambientHumBaseFrequency = Mathf.Max(20f, ambientHumBaseFrequency);
        ambientHumDetune = Mathf.Max(0.1f, ambientHumDetune);
        ambientHumFallbackDuration = Mathf.Max(0.25f, ambientHumFallbackDuration);

        if (oneShotSource != null)
        {
            ApplyPitch();
        }

        if (ambientHumSource != null)
        {
            ApplyAmbientHumState();
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

    public void PlaySignatureMoment()
    {
        PlayOneShot(signatureMomentClip, ref fallbackSignatureMomentClip, "Fallback_SignatureMoment", 92f, 74f, 0.35f, signatureMomentVolume);
    }

    public void PlayShatter()
    {
        PlayOneShot(shatterClip, ref fallbackShatterClip, "Fallback_Shatter", 1500f, 260f, 0.1f, shatterVolume);
    }

    public void SetSlowPitch(bool active)
    {
        slowPitchActive = active;
        ApplyPitch();
    }

    public void SetAmbientHum(bool active, float intensity = 1f)
    {
        ambientHumActive = active;
        ambientHumIntensity = Mathf.Clamp01(intensity);
        EnsureAudioSources();
        ApplyAmbientHumState();
    }

    public void SetAmbientHumIntensity(float intensity)
    {
        ambientHumIntensity = Mathf.Clamp01(intensity);
        if (ambientHumActive)
        {
            ApplyAmbientHumState();
        }
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
        EnsureAudioSources();

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

    private void EnsureAudioSources()
    {
        if (oneShotSource != null)
        {
            EnsureAmbientHumSource();
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

        EnsureAmbientHumSource();
    }

    private void EnsureAmbientHumSource()
    {
        if (ambientHumSource != null)
        {
            return;
        }

        AudioSource[] sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource candidate = sources[i];
            if (candidate != null && candidate != oneShotSource)
            {
                ambientHumSource = candidate;
                break;
            }
        }

        if (ambientHumSource == null)
        {
            ambientHumSource = gameObject.AddComponent<AudioSource>();
        }

        ambientHumSource.playOnAwake = false;
        ambientHumSource.loop = true;
        ambientHumSource.spatialBlend = 0f;
        ambientHumSource.pitch = 1f;
        ambientHumSource.volume = 0f;
    }

    private void ApplyAmbientHumState()
    {
        if (ambientHumSource == null)
        {
            return;
        }

        if (!ambientHumActive)
        {
            ambientHumSource.volume = 0f;
            if (ambientHumSource.isPlaying)
            {
                ambientHumSource.Stop();
            }

            return;
        }

        AudioClip humClip = ambientHumClip;
        if (humClip == null)
        {
            fallbackAmbientHumClip ??= CreateHumClip("Fallback_AmbientHum", ambientHumBaseFrequency, ambientHumDetune, ambientHumFallbackDuration);
            humClip = fallbackAmbientHumClip;
        }

        if (humClip == null)
        {
            ambientHumSource.volume = 0f;
            if (ambientHumSource.isPlaying)
            {
                ambientHumSource.Stop();
            }

            return;
        }

        if (ambientHumSource.clip != humClip)
        {
            ambientHumSource.clip = humClip;
        }

        float targetVolume = Mathf.Clamp01(masterVolume * ambientHumVolume * ambientHumIntensity);
        ambientHumSource.volume = targetVolume;

        if (targetVolume <= 0f)
        {
            if (ambientHumSource.isPlaying)
            {
                ambientHumSource.Stop();
            }

            return;
        }

        if (!ambientHumSource.isPlaying)
        {
            ambientHumSource.Play();
        }
    }

    private void ApplyPitch()
    {
        if (oneShotSource == null)
        {
            return;
        }

        oneShotSource.pitch = slowPitchActive ? slowPitch : normalPitch;
    }

    private static AudioClip CreateHumClip(string clipName, float baseFrequency, float detune, float duration)
    {
        int sampleRate = 22050;
        float safeDuration = Mathf.Max(0.25f, duration);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(safeDuration * sampleRate));
        float[] samples = new float[sampleCount];

        float safeBase = Mathf.Max(20f, baseFrequency);
        float safeDetune = Mathf.Max(0.1f, detune);

        float primaryCycles = Mathf.Max(1f, Mathf.Round(safeBase * safeDuration));
        float secondaryCycles = Mathf.Max(1f, Mathf.Round((safeBase + safeDetune) * safeDuration));
        float subCycles = Mathf.Max(1f, Mathf.Round((safeBase * 0.5f) * safeDuration));

        float primaryFrequency = primaryCycles / safeDuration;
        float secondaryFrequency = secondaryCycles / safeDuration;
        float subFrequency = subCycles / safeDuration;
        float wobbleFrequency = 1f / safeDuration;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float primary = Mathf.Sin(2f * Mathf.PI * primaryFrequency * t);
            float secondary = Mathf.Sin(2f * Mathf.PI * secondaryFrequency * t);
            float sub = Mathf.Sin(2f * Mathf.PI * subFrequency * t);
            float wobble = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * wobbleFrequency * t);
            samples[i] = (primary * 0.5f + secondary * 0.35f + sub * 0.15f) * wobble * 0.22f;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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
