﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class AudioManager : BaseManager<AudioManager>
{
    private const float BGM_FADE_SPEED_RATE_HIGH = 0.9f;
    private const float BGM_FADE_SPEED_RATE_LOW = 0.3f;

    private const string BGM_VOLUME_KEY = "BGM_VOLUME_KEY";
    private const string SE_VOLUME_KEY = "SE_VOLUME_KEY";
    private const float BGM_VOLUME_DEFAULT = 0.5f;
    private const float SE_VOLUME_DEFAULT = 0.3f;

    private const string BGM_MUTE_KEY = "BGM_MUTE_KEY";
    private const string SE_MUTE_KEY = "SE_MUTE_KEY";
    private const int BGM_MUTE_DEFAULT = 0;
    private const int SE_MUTE_DEFAULT = 0;

    private float bgmFadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH;

    private string currentBGMName = "";
    private string previousBGMName = "";
    private float previousBGMTime = 0f;

    private string nextBGMName;
    private bool isFadeOut = false;

    public AudioSource AttachBGMSource;
    public AudioSource AttachSESource;

    [Header("Default BGM Settings")]
    public string defaultBGMName;

    // ===== NEW (safe default = false): cinematic intro có thể chặn autoplay =====
    [Header("Cinematic Boot Guard")]
    [Tooltip("Nếu true: KHÔNG auto PlayBGM(defaultBGMName) trong Start(). " +
             "Dùng cho flow cinematic intro để tránh BGM chạy sớm.")]
    public bool cinematicControlledBoot = false;

    [Header("UI SE Settings")]
    [Tooltip("Optional override UI interface SE name. If empty, fallback to default (InterfaceSound).")]
    public string interfaceSEName = "InterfaceSound";
    private const string DEFAULT_UI_SE_NAME = "InterfaceSound";

    [Header("UI Panel SE Settings")]
    [Tooltip("SE phát khi mở bất kỳ panel UI nào.")]
    public string openPanelSEName = "PanelOpen";
    private const string DEFAULT_OPEN_PANEL_SE_NAME = "PanelOpen";

    private Dictionary<string, AudioClip> bgmDic;
    private Dictionary<string, AudioClip> seDic;

    // ===== HARD GUARD: only one AudioManager may live =====
    private static AudioManager _persistent;

    // ===== Runtime pause (không ghi PlayerPrefs) =====
    private bool _bgmPausedRuntime;
    private bool _bgmMuteCached;
    private float _bgmTimeCached;

    protected override void Awake()
    {
        if (_persistent != null && _persistent != this)
        {
            if (!string.IsNullOrEmpty(defaultBGMName))
            {
                _persistent.RequestSceneDefaultBGM(defaultBGMName);
            }

            Destroy(gameObject);
            return;
        }

        _persistent = this;
        DontDestroyOnLoad(gameObject);

        base.Awake();

        bgmDic = new Dictionary<string, AudioClip>();
        seDic = new Dictionary<string, AudioClip>();

        foreach (AudioClip bgm in Resources.LoadAll<AudioClip>("Audio/BGM"))
            bgmDic[bgm.name] = bgm;

        foreach (AudioClip se in Resources.LoadAll<AudioClip>("Audio/SE"))
            seDic[se.name] = se;
    }

    private void Start()
    {
        if (AttachBGMSource == null || AttachSESource == null)
        {
            Debug.LogWarning("[AudioManager] Missing AttachBGMSource / AttachSESource on the persistent instance.");
            return;
        }

        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);

        AttachBGMSource.mute = PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) != 0;
        AttachSESource.mute = PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) != 0;

        if (!string.IsNullOrEmpty(defaultBGMName))
        {
            // ===== NEW: cinematic guard (default false => giữ nguyên behavior cũ) =====
            if (!cinematicControlledBoot)
            {
                PlayBGM(defaultBGMName);
            }
        }
    }

    private void OnDestroy()
    {
        if (_persistent == this)
        {
            _persistent = null;
            Debug.LogWarning("[AudioManager] Persistent instance was destroyed. Check scene unload / singleton duplicate logic.");
        }
    }

    // ===============================
    // ===== Runtime pause helpers ===
    // ===============================

    public void PauseBGMRuntime()
    {
        if (AttachBGMSource == null) return;
        if (_bgmPausedRuntime) return;

        _bgmPausedRuntime = true;
        _bgmMuteCached = AttachBGMSource.mute;
        _bgmTimeCached = AttachBGMSource.time;

        // Pause ngay để khỏi đè tiếng, không ghi prefs
        AttachBGMSource.Pause();
        AttachBGMSource.mute = true;
    }

    public void ResumeBGMRuntime()
    {
        if (AttachBGMSource == null) return;
        if (!_bgmPausedRuntime) return;

        _bgmPausedRuntime = false;

        AttachBGMSource.mute = _bgmMuteCached;

        // Nếu có clip và trước đó đang pause thì unpause
        if (AttachBGMSource.clip != null)
        {
            AttachBGMSource.time = Mathf.Clamp(_bgmTimeCached, 0f, AttachBGMSource.clip.length - 0.01f);
            AttachBGMSource.UnPause();
        }
        else if (!string.IsNullOrEmpty(defaultBGMName))
        {
            // fallback: play default
            PlayBGM(defaultBGMName);
        }
    }

    // Scene mới yêu cầu đổi nhạc nền theo scene
    public void RequestSceneDefaultBGM(string bgmName)
    {
        if (string.IsNullOrEmpty(bgmName)) return;

        defaultBGMName = bgmName;
        PlayBGM(bgmName);
    }

    // ===============================
    // ===== PUBLIC BGM CONTROL =====
    // ===============================

    public void PlayBGM(string bgmName, float fadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH)
    {
        if (AttachBGMSource == null)
        {
            Debug.LogWarning("[AudioManager] AttachBGMSource is null.");
            return;
        }

        if (!bgmDic.ContainsKey(bgmName))
        {
            Debug.LogWarning($"[AudioManager] No BGM named {bgmName}");
            return;
        }

        if (AttachBGMSource.isPlaying && AttachBGMSource.clip != null && AttachBGMSource.clip.name == bgmName)
            return;

        CacheCurrentBGM();

        nextBGMName = bgmName;
        bgmFadeSpeedRate = fadeSpeedRate;

        if (!AttachBGMSource.isPlaying || AttachBGMSource.clip == null)
        {
            PlayNextBGM();
        }
        else
        {
            isFadeOut = true;
        }
    }

    public void PushBGM(string bgmName)
    {
        PlayBGM(bgmName);
    }

    public void PopBGM()
    {
        if (!string.IsNullOrEmpty(previousBGMName))
        {
            PlayBGM(previousBGMName);
            if (AttachBGMSource != null)
                AttachBGMSource.time = previousBGMTime;
        }
        else if (!string.IsNullOrEmpty(defaultBGMName))
        {
            PlayBGM(defaultBGMName);
        }
    }

    public void FadeOutBGM(float fadeSpeedRate = BGM_FADE_SPEED_RATE_LOW)
    {
        bgmFadeSpeedRate = fadeSpeedRate;
        isFadeOut = true;
        nextBGMName = "";
    }

    private void Update()
    {
        if (!isFadeOut) return;
        if (AttachBGMSource == null) return;

        // Nếu đang runtime pause thì đừng fade (tránh đánh nhau)
        if (_bgmPausedRuntime) return;

        AttachBGMSource.volume -= Time.deltaTime * bgmFadeSpeedRate;
        if (AttachBGMSource.volume > 0f) return;

        AttachBGMSource.Stop();
        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        isFadeOut = false;

        PlayNextBGM();
    }

    private void PlayNextBGM()
    {
        if (AttachBGMSource == null) return;
        if (string.IsNullOrEmpty(nextBGMName)) return;

        AttachBGMSource.clip = bgmDic[nextBGMName];
        AttachBGMSource.Play();

        currentBGMName = nextBGMName;
        nextBGMName = "";
    }

    private void CacheCurrentBGM()
    {
        if (AttachBGMSource == null) return;
        if (AttachBGMSource.clip == null) return;

        previousBGMName = AttachBGMSource.clip.name;
        previousBGMTime = AttachBGMSource.time;
    }

    // ===============================
    // ===== SE HANDLING (KEEP OLD)
    // ===============================

    public void PlaySE(string seName, float delay = 0.0f)
    {
        if (!seDic.ContainsKey(seName)) return;
        StartCoroutine(DelayPlaySE(seDic[seName], delay));
    }

    private IEnumerator DelayPlaySE(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AttachSESource != null)
            AttachSESource.PlayOneShot(clip);
    }

    public void PlaySEClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (AttachSESource == null) return;
        if (AttachSESource.mute) return;

        AttachSESource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlaySEAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (AttachSESource != null && AttachSESource.mute) return;

        float baseVolume = (AttachSESource != null) ? AttachSESource.volume : 1f;
        float finalVolume = Mathf.Clamp01(baseVolume * volumeScale);
        AudioSource.PlayClipAtPoint(clip, position, finalVolume);
    }

    // ===== NEW: overload theo tên SE để fix compile lỗi cũ =====
    // Giữ nguyên API cũ (AudioClip) + thêm API mới (string) cho các script đang gọi bằng tên.
    public void PlaySEAtPosition(string seName, Vector3 position, float volumeScale = 1f, float delay = 0.0f)
    {
        if (string.IsNullOrEmpty(seName)) return;
        if (!seDic.ContainsKey(seName))
        {
            Debug.LogWarning($"[AudioManager] No SE named {seName}");
            return;
        }

        AudioClip clip = seDic[seName];
        if (clip == null) return;

        if (delay <= 0.0f)
        {
            PlaySEAtPosition(clip, position, volumeScale);
        }
        else
        {
            StartCoroutine(DelayPlaySEAtPosition(clip, position, volumeScale, delay));
        }
    }

    private IEnumerator DelayPlaySEAtPosition(AudioClip clip, Vector3 position, float volumeScale, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySEAtPosition(clip, position, volumeScale);
    }

    public void PlayInterfaceSE(float delay = 0.0f)
    {
        string seName = string.IsNullOrEmpty(interfaceSEName) ? DEFAULT_UI_SE_NAME : interfaceSEName;
        PlaySE(seName, delay);
    }

    public void PlayOpenPanelSE(float delay = 0.0f)
    {
        string seName = string.IsNullOrEmpty(openPanelSEName) ? DEFAULT_OPEN_PANEL_SE_NAME : openPanelSEName;
        PlaySE(seName, delay);
    }

    public void ChangeBGMVolume(float volume)
    {
        if (AttachBGMSource == null) return;
        AttachBGMSource.volume = volume;
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
    }

    public void ChangeSEVolume(float volume)
    {
        if (AttachSESource == null) return;
        AttachSESource.volume = volume;
        PlayerPrefs.SetFloat(SE_VOLUME_KEY, volume);
    }

    public void MuteBGM(bool mute)
    {
        if (AttachBGMSource == null) return;
        AttachBGMSource.mute = mute;
        PlayerPrefs.SetInt(BGM_MUTE_KEY, mute ? 1 : 0);
    }

    public void MuteSE(bool mute)
    {
        if (AttachSESource == null) return;
        AttachSESource.mute = mute;
        PlayerPrefs.SetInt(SE_MUTE_KEY, mute ? 1 : 0);
    }

    public void ApplyPrefsNow()
    {
        if (AttachBGMSource == null || AttachSESource == null) return;

        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);

        AttachBGMSource.mute = PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) != 0;
        AttachSESource.mute = PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) != 0;
    }
}
