using UnityEngine;
using UnityEngine.UI;
using TMPro; 

namespace IronIvy.UI
{
    public class LoadingUIController : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;

        // Cập nhật giá trị hiển thị mỗi khung hình dựa trên progress từ Bootstrapper
        private void Update()
        {
            if (IronIvy.Core.GameBootstrapper.HasInstance)
            {
                float progress = IronIvy.Core.GameBootstrapper.Instance.loadProgress;
                
                // Cập nhật thanh slider (giá trị từ 0 đến 1)
                if (progressBar != null)
                    progressBar.value = progress;

                // Cập nhật text hiển thị phần trăm
                if (progressText != null)
                    progressText.text = $"{(progress * 100f):F0}%";
            }
        }
    }
}