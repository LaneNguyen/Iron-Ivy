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

    // next BGM name, SE name
    private string nextBGMName;
    private string nextSEName;

    // flag check BGM đang fade out không
    private bool isFadeOut = false;

    // audio source riêng cho BGM và SE
    public AudioSource AttachBGMSource;
    public AudioSource AttachSESource;

    // default BGM cho toàn game (set trong Inspector)
    [Header("Default BGM Settings")]
    public string defaultBGMName;

    // keep all audio clips đã load
    private Dictionary<string, AudioClip> bgmDic;
    private Dictionary<string, AudioClip> seDic;

    protected override void Awake()
    {
        base.Awake();
        // Load all SE & BGM files from resource folder
        bgmDic = new Dictionary<string, AudioClip>();
        seDic = new Dictionary<string, AudioClip>();

        object[] bgmList = Resources.LoadAll("Audio/BGM");
        object[] seList = Resources.LoadAll("Audio/SE");

        foreach (AudioClip bgm in bgmList)
        {
            bgmDic[bgm.name] = bgm;
        }
        foreach (AudioClip se in seList)
        {
            seDic[se.name] = se;
        }
    }

    private void Start()
    {
        // load volume đã lưu hoặc dùng default
        AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
        AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);

        // load trạng thái mute BGM
        bool isMuteBgm = (PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) == BGM_MUTE_DEFAULT) ? false : true;
        AttachBGMSource.mute = isMuteBgm;

        // load trạng thái mute SE (sửa lại cho đúng SE_MUTE_DEFAULT)
        bool isMuteSe = (PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) == SE_MUTE_DEFAULT) ? false : true;
        AttachSESource.mute = isMuteSe;

        // auto play BGM mặc định nếu có set tên và chưa có gì đang chạy
        // idea: dùng BGM này làm nhạc nền chung cho game, BGM khác sau này gọi PlayBGM sẽ tự fade đổi
        if (!string.IsNullOrEmpty(defaultBGMName))
        {
            if (!AttachBGMSource.isPlaying || AttachBGMSource.clip == null)
            {
                PlayBGM(defaultBGMName);
            }
        }
    }

    public void PlaySE(string seName, float delay = 0.0f)
    {
        if (!seDic.ContainsKey(seName))
        {
            Debug.Log(seName + "There is no SE named");
            return;
        }

        nextSEName = seName;
        Invoke(nameof(DelayPlaySE), delay);
    }

    private void DelayPlaySE()
    {
        AttachSESource.PlayOneShot(seDic[nextSEName] as AudioClip);
    }

    public void PlayBGM(string bgmName, float fadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH)
    {
        if (!bgmDic.ContainsKey(bgmName))
        {
            Debug.Log(bgmName + "There is no BGM named");
            return;
        }

        // If BGM is not currently playing, or clip bị null (weird case) thì play luôn
        if (!AttachBGMSource.isPlaying || AttachBGMSource.clip == null)
        {
            nextBGMName = "";
            AttachBGMSource.clip = bgmDic[bgmName] as AudioClip;
            AttachBGMSource.Play();
        }
        // When a different BGM is playing, fade out the BGM that is playing before playing the next one.
        // Ignore when the same BGM is playing
        else if (AttachBGMSource.clip.name != bgmName)
        {
            nextBGMName = bgmName;
            FadeOutBGM(fadeSpeedRate);
        }
    }

    public void FadeOutBGM(float fadeSpeedRate = BGM_FADE_SPEED_RATE_LOW)
    {
        bgmFadeSpeedRate = fadeSpeedRate;
        isFadeOut = true;
    }

    private void Update()
    {
        if (!isFadeOut)
        {
            return;
        }

        // Gradually lower the volume, and when the volume reaches 0
        // return the volume and play the next song
        AttachBGMSource.volume -= Time.deltaTime * bgmFadeSpeedRate;
        if (AttachBGMSource.volume <= 0)
        {
            AttachBGMSource.Stop();
            AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
            isFadeOut = false;

            if (!string.IsNullOrEmpty(nextBGMName))
            {
                PlayBGM(nextBGMName);
            }
        }
    }

    public void ChangeBGMVolume(float BGMVolume)
    {
        AttachBGMSource.volume = BGMVolume;
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, BGMVolume);
    }

    public void ChangeSEVolume(float SEVolume)
    {
        AttachSESource.volume = SEVolume;
        PlayerPrefs.SetFloat(SE_VOLUME_KEY, SEVolume);
    }

    public void MuteBGM(bool isMute)
    {
        AttachBGMSource.mute = isMute;

        int isMuteValue = 0;

        if (isMute)
        {
            isMuteValue = 1;
        }

        PlayerPrefs.SetInt(BGM_MUTE_KEY, isMuteValue);
    }

    public void MuteSE(bool isMute)
    {
        AttachSESource.mute = isMute;

        int isMuteValue = 0;

        if (isMute)
        {
            isMuteValue = 1;
        }

        PlayerPrefs.SetInt(SE_MUTE_KEY, isMuteValue);
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
        float baseVolume = (AttachSESource != null) ? AttachSESource.volume : SE_VOLUME_DEFAULT;
        float finalVolume = Mathf.Clamp01(baseVolume * volumeScale);

        // dung PlayClipAtPoint nhung thong qua audio manager 1 cho cho de control
        AudioSource.PlayClipAtPoint(clip, position, finalVolume);
    }
}
