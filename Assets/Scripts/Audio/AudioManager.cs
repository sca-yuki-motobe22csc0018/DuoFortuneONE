using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Library")]
    public AudioLibrary library;

    [Header("Sources")]
    public AudioSource uiSource;
    public AudioSource sfxSource;
    public AudioSource bgmSource;

    [Header("Default Volumes")]
    [Range(0f, 1f)] public float uiVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 1f;

    [Header("Burst Tuning (Intervals)")]
    public bool accumulateBursts = true;

    [Range(0.01f, 0.30f)] public float cardMoveBurstInterval = 0.10f;
    [Range(0.01f, 0.30f)] public float manaMaxUpBurstInterval = 0.12f;
    [Range(0.01f, 0.30f)] public float manaMaxDownBurstInterval = 0.12f;
    [Range(0.01f, 0.30f)] public float manaRecoverBurstInterval = 0.10f;

    // バースト（キュー式）
    Coroutine _burstRoutine;
    private SfxClipId _burstId;
    private int _burstPending = 0;
    private float _burstInterval = 0.06f;
    private float _burstVolumeScale = 1f;

    [Header("BGM Fade")]
    [Range(0f, 3f)] public float bgmFadeOutSeconds = 0.4f;
    [Range(0f, 3f)] public float bgmFadeInSeconds = 0.6f;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------
    // UI
    // -------------------------
    public void PlayUI(UIClipId id, float volumeScale = 1f)
    {
        if (library == null) return;
        PlayUI(library.GetUI(id), volumeScale);
    }

    public void PlayUI(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || uiSource == null) return;
        uiSource.PlayOneShot(clip, uiVolume * volumeScale);
    }

    // -------------------------
    // SFX
    // -------------------------
    public void PlaySfx(SfxClipId id, float volumeScale = 1f)
    {
        if (library == null) return;
        PlaySfx(library.GetSfx(id), volumeScale);
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    /// <summary>
    /// 枚数分「シュシュシュ…」を鳴らす（キュー式）
    /// </summary>
    public void PlaySfxBurst(SfxClipId id, int count, float intervalSeconds = 0.06f, float volumeScale = 1f)
    {
        if (count <= 0) return;

        if (_burstRoutine != null && accumulateBursts && id == _burstId)
        {
            _burstPending += count;
            _burstInterval = intervalSeconds;
            _burstVolumeScale = volumeScale;
            return;
        }

        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }

        _burstId = id;
        _burstPending = count;
        _burstInterval = intervalSeconds;
        _burstVolumeScale = volumeScale;

        _burstRoutine = StartCoroutine(SfxBurstRoutine());
    }

    private IEnumerator SfxBurstRoutine()
    {
        while (_burstPending > 0)
        {
            _burstPending--;

            PlaySfx(_burstId, _burstVolumeScale);

            if (_burstInterval > 0f)
                yield return new WaitForSecondsRealtime(_burstInterval);
            else
                yield return null;
        }

        _burstRoutine = null;
    }

    // -------------------------
    // Burst wrappers (intervals are configurable in Inspector)
    // -------------------------
    public void PlayCardMoveBurst(int count, float volumeScale = 1f)
    {
        PlaySfxBurst(SfxClipId.CardMove, count, cardMoveBurstInterval, volumeScale);
    }

    public void PlayManaMaxUpBurst(int count, float volumeScale = 1f)
    {
        PlaySfxBurst(SfxClipId.ManaMaxUp, count, manaMaxUpBurstInterval, volumeScale);
    }

    public void PlayManaMaxDownBurst(int count, float volumeScale = 1f)
    {
        PlaySfxBurst(SfxClipId.ManaMaxDown, count, manaMaxDownBurstInterval, volumeScale);
    }

    public void PlayManaRecoverBurst(int count, float volumeScale = 1f)
    {
        PlaySfxBurst(SfxClipId.ManaRecover, count, manaRecoverBurstInterval, volumeScale);
    }


    IEnumerator SfxBurstRoutine(SfxClipId id, int count, float intervalSeconds, float volumeScale)
    {
        for (int i = 0; i < count; i++)
        {
            PlaySfx(id, volumeScale);

            // 演出中や一時停止でも気持ちよく鳴るように unscaled を使う
            if (intervalSeconds > 0f)
                yield return new WaitForSecondsRealtime(intervalSeconds);
            else
                yield return null;
        }

        _burstRoutine = null;
    }

    // -------------------------
    // BGM（最小）
    // -------------------------
    public void PlayBgm(AudioClip clip, bool loop = true, float volumeScale = 1f)
    {
        if (clip == null || bgmSource == null) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = bgmVolume * volumeScale;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    private Coroutine _bgmFadeRoutine;

    public void ChangeBgm(AudioClip clip, bool loop = true, float volumeScale = 1f)
    {
        if (bgmSource == null) return;

        // 同じ曲なら何もしない
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (_bgmFadeRoutine != null)
        {
            StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = null;
        }

        _bgmFadeRoutine = StartCoroutine(Co_ChangeBgm(clip, loop, volumeScale));
    }

    private IEnumerator Co_ChangeBgm(AudioClip clip, bool loop, float volumeScale)
    {
        float startVol = bgmSource.volume;

        // Fade Out
        if (bgmFadeOutSeconds > 0f && bgmSource.isPlaying)
        {
            float t = 0f;
            while (t < bgmFadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / bgmFadeOutSeconds);
                bgmSource.volume = Mathf.Lerp(startVol, 0f, a);
                yield return null;
            }
        }

        bgmSource.Stop();
        bgmSource.clip = clip;

        if (clip == null)
        {
            bgmSource.volume = bgmVolume * volumeScale;
            _bgmFadeRoutine = null;
            yield break;
        }

        bgmSource.loop = loop;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // Fade In
        float targetVol = bgmVolume * volumeScale;
        if (bgmFadeInSeconds > 0f)
        {
            float t = 0f;
            while (t < bgmFadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / bgmFadeInSeconds);
                bgmSource.volume = Mathf.Lerp(0f, targetVol, a);
                yield return null;
            }
        }

        bgmSource.volume = targetVol;
        _bgmFadeRoutine = null;
    }

}
