using UnityEngine;
using UnityEngine.UI;
using IronIvy.Core;

public class SettingPanel : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Toggle bgmMute;
    [SerializeField] private Toggle seMute;

    float bgmValue;
    float seValue;

    bool isBGMMute;
    bool isSEMute;

    bool isBound;

    void OnEnable()
    {
        // panel bat len la sync data truoc, roi moi bind event
        SyncFromAudioManager();
        BindUI();
    }

    void OnDisable()
    {
        UnbindUI();
    }

    void SyncFromAudioManager()
    {
        if (!AudioManager.HasInstance) return;

        // lấy thẳng từ source (đúng với AudioManager bạn đang dùng)
        bgmValue = AudioManager.Instance.AttachBGMSource != null
            ? AudioManager.Instance.AttachBGMSource.volume
            : bgmSlider.value;

        seValue = AudioManager.Instance.AttachSESource != null
            ? AudioManager.Instance.AttachSESource.volume
            : seSlider.value;

        isBGMMute = AudioManager.Instance.AttachBGMSource != null
            ? AudioManager.Instance.AttachBGMSource.mute
            : bgmMute.isOn;

        isSEMute = AudioManager.Instance.AttachSESource != null
            ? AudioManager.Instance.AttachSESource.mute
            : seMute.isOn;

        // set UI nhưng không trigger callback
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgmValue);
        if (seSlider != null) seSlider.SetValueWithoutNotify(seValue);

        if (bgmMute != null) bgmMute.SetIsOnWithoutNotify(isBGMMute);
        if (seMute != null) seMute.SetIsOnWithoutNotify(isSEMute);
    }

    void BindUI()
    {
        if (isBound) return;
        isBound = true;

        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnSliderChangeBGMValue);
        if (seSlider != null) seSlider.onValueChanged.AddListener(OnSliderChangeSEValue);

        if (bgmMute != null) bgmMute.onValueChanged.AddListener(OnChangeValueBGMMute);
        if (seMute != null) seMute.onValueChanged.AddListener(OnChangeValueSEMute);
    }

    void UnbindUI()
    {
        if (!isBound) return;
        isBound = false;

        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnSliderChangeBGMValue);
        if (seSlider != null) seSlider.onValueChanged.RemoveListener(OnSliderChangeSEValue);

        if (bgmMute != null) bgmMute.onValueChanged.RemoveListener(OnChangeValueBGMMute);
        if (seMute != null) seMute.onValueChanged.RemoveListener(OnChangeValueSEMute);
    }

    // note: đổi realtime luôn cho "healing" feel, khỏi bấm submit
    public void OnSliderChangeBGMValue(float v)
    {
        bgmValue = v;

        if (!AudioManager.HasInstance) return;

        // kéo slider mà đang mute thì unmute luôn cho đỡ khó hiểu
        if (isBGMMute && bgmMute != null)
        {
            isBGMMute = false;
            bgmMute.SetIsOnWithoutNotify(false);
            AudioManager.Instance.MuteBGM(false);
        }

        AudioManager.Instance.ChangeBGMVolume(bgmValue);
    }

    public void OnSliderChangeSEValue(float v)
    {
        seValue = v;

        if (!AudioManager.HasInstance) return;

        if (isSEMute && seMute != null)
        {
            isSEMute = false;
            seMute.SetIsOnWithoutNotify(false);
            AudioManager.Instance.MuteSE(false);
        }

        // cái này sẽ ảnh hưởng luôn SE 3D ngoài world vì PlaySEAtPosition dùng AttachSESource.volume
        AudioManager.Instance.ChangeSEVolume(seValue);
    }

    public void OnChangeValueBGMMute(bool v)
    {
        isBGMMute = v;
        if (!AudioManager.HasInstance) return;

        AudioManager.Instance.MuteBGM(isBGMMute);
    }

    public void OnChangeValueSEMute(bool v)
    {
        isSEMute = v;
        if (!AudioManager.HasInstance) return;

        AudioManager.Instance.MuteSE(isSEMute);
    }

    // giữ lại cho UI cũ nếu panel của bạn vẫn có nút "Apply"
    // nhưng thực tế đã apply realtime rồi
    public void OnSubmitButtonClick()
    {
        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.ChangeBGMVolume(bgmValue);
            AudioManager.Instance.ChangeSEVolume(seValue);
            AudioManager.Instance.MuteBGM(isBGMMute);
            AudioManager.Instance.MuteSE(isSEMute);
        }

        UIManager.Instance?.CloseSettings();
    }

    public void OnCloseButtonClick()
    {
        // đóng panel thôi, audio đã apply rồi
        UIManager.Instance?.CloseSettings();
    }
}
