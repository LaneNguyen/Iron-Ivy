using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IronIvy.UI
{
    public class UIItemSlot : MonoBehaviour
    {
        [Header("Refs")]
        public Image iconImage;
        public TextMeshProUGUI countText;

        public void Setup(Sprite icon, int count)
        {
            if (iconImage)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true; // Đảm bảo bật lên
                
                // Fix tỉ lệ ảnh nếu cần
                iconImage.preserveAspect = true; 
            }

            if (countText)
            {
                countText.text = count.ToString();
            }
        }
    }
}