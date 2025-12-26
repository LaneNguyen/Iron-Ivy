using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : BaseManager<AudioManager>
{
    private const float BGM_FADE_SPEED_RATE_HIGH = 0.9f;
    private const float BGM_FADE_SPEED_RATE_LOW = 0.3f;

    private const string BGM_VOLUME_KEY = "BGM_VOLUME_KEY";
    private const string SE_VOLUME_KEY = "SE_VOLUME_KEY";
    private const float BGM_VOLUME_DEFAULT = 0.2f;
    private const float SE_VOLUME_DEFAULT = 1f;

    private const string BGM_MUTE_KEY = "BGM_MUTE_KEY";
    private const string SE_MUTE_KEY = "SE_MUTE_KEY";
    private const int BGM_MUTE_DEFAULT = 0;
    private const int SE_MUTE_DEFAULT = 0;

    private float bgmFadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH;

    // ===== NEW: BGM STATE MEMORY =====
    private string currentBGMName = "";
    private string previousBGMName = "";
    private float previousBGMTime = 0f;

    // next BGM name
    private string nextBGMName;
    private bool isFadeOut = false;

    public AudioSource AttachBGMSource;
    public AudioSource AttachSESource;

    [Header("Default BGM Settings")]
    public string defaultBGMName;

    private Dictionary<string, AudioClip> bgmDic;
    private Dictionary<string, AudioClip> seDic;

    protected override void Awake()
    {
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
        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);

        AttachBGMSource.mute = PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) != 0;
        AttachSESource.mute = PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) != 0;

        if (!string.IsNullOrEmpty(defaultBGMName))
        {
            PlayBGM(defaultBGMName);
        }
    }

    // ===============================
    // ===== PUBLIC BGM CONTROL =====
    // ===============================

    public void PlayBGM(string bgmName, float fadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH)
    {
        if (!bgmDic.ContainsKey(bgmName))
        {
            Debug.LogWarning($"[AudioManager] No BGM named {bgmName}");
            return;
        }

        // same BGM, ignore
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

    /// <summary>
    /// Dùng khi vào Minigame
    /// </summary>
    public void PushBGM(string bgmName)
    {
        PlayBGM(bgmName);
    }

    /// <summary>
    /// Dùng khi thoát Minigame
    /// </summary>
    public void PopBGM()
    {
        if (!string.IsNullOrEmpty(previousBGMName))
        {
            PlayBGM(previousBGMName);
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

    // ===============================
    // ===== INTERNAL UPDATE LOOP ====
    // ===============================

    private void Update()
    {
        if (!isFadeOut) return;

        AttachBGMSource.volume -= Time.deltaTime * bgmFadeSpeedRate;
        if (AttachBGMSource.volume > 0f) return;

        AttachBGMSource.Stop();
        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        isFadeOut = false;

        PlayNextBGM();
    }

    private void PlayNextBGM()
    {
        if (string.IsNullOrEmpty(nextBGMName)) return;

        AttachBGMSource.clip = bgmDic[nextBGMName];
        AttachBGMSource.Play();

        currentBGMName = nextBGMName;
        nextBGMName = "";
    }

    private void CacheCurrentBGM()
    {
        if (AttachBGMSource.clip == null) return;

        previousBGMName = AttachBGMSource.clip.name;
        previousBGMTime = AttachBGMSource.time;
    }

    // ===============================
    // ===== SE HANDLING (UNCHANGED)
    // ===============================

    public void PlaySE(string seName, float delay = 0.0f)
    {
        if (!seDic.ContainsKey(seName)) return;
        StartCoroutine(DelayPlaySE(seDic[seName], delay));
    }

    private IEnumerator DelayPlaySE(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttachSESource.PlayOneShot(clip);
    }

    public void PlaySEClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || AttachSESource.mute) return;
        AttachSESource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void ChangeBGMVolume(float volume)
    {
        AttachBGMSource.volume = volume;
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
    }

    public void ChangeSEVolume(float volume)
    {
        AttachSESource.volume = volume;
        PlayerPrefs.SetFloat(SE_VOLUME_KEY, volume);
    }

    public void MuteBGM(bool mute)
    {
        AttachBGMSource.mute = mute;
        PlayerPrefs.SetInt(BGM_MUTE_KEY, mute ? 1 : 0);
    }

    public void MuteSE(bool mute)
    {
        AttachSESource.mute = mute;
        PlayerPrefs.SetInt(SE_MUTE_KEY, mute ? 1 : 0);
    }

    // helper nho de phat SE tai vi tri 3d (dung cho tieng keu animal ngoai world)
    public void PlaySEAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return;
        }

        // neu SE dang mute thi thoi, de game setting control
        if (AttachSESource != null && AttachSESource.mute)
        {
            return;
        }

        // lay volume goc tu SE channel chinh
        float baseVolume = (AttachSESource != null) ? AttachSESource.volume : 1f;
        float finalVolume = Mathf.Clamp01(baseVolume * volumeScale);

        // dung PlayClipAtPoint nhung thong qua audio manager 1 cho cho de control
        AudioSource.PlayClipAtPoint(clip, position, finalVolume);
    }

    public void ApplyPrefsNow()
    {
        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);

        AttachBGMSource.mute = PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) != 0;
        AttachSESource.mute = PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) != 0;
    }

}
