using UnityEngine;

namespace IronIvy.UI
{
    // panel start cho Animal Rhythm
    // giờ chỉ là stub để code compile, sau này sẽ viết logic riêng
    public class AnimalRhythmStartPanel : MonoBehaviour
    {
        [Header("Note")]
        [TextArea]
        public string todoNote =
            "TODO: implement animal start panel.\n" +
            "Chỗ này sẽ chọn animal / check Energy / gọi StartGame cho Animal Rhythm.";

        public GameObject panelRoot;

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                AudioManager.Instance?.PlayOpenPanelSE();
            }
            else
            {
                gameObject.SetActive(true);
                AudioManager.Instance?.PlayOpenPanelSE();
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }
}
