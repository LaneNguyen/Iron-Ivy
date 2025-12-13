using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Data;
using IronIvy.Core;

namespace IronIvy.UI
{
    public class ArchiveNodeUI : MonoBehaviour
    {
        [Header("UI Components")]
        public Button btnSelect;
        public Image iconImage;
        public Image borderImage;       // Viền ô
        public GameObject lockOverlay;  // Icon ổ khóa che lên nếu chưa mở
        public GameObject lineConnectorPrefab; // (Nâng cao) Để vẽ đường nối

        [Header("Colors (Config)")]
        public Color lockedColor = Color.gray;
        public Color unlockedColor = new Color(0f, 1f, 1f, 1f); // Cyan
        public Color affordableColor = new Color(1f, 0.9f, 0.4f, 1f); // Vàng
        public Color selectedColor = Color.white;

        // Data Runtime
        private ArchiveNodeDefinition _data;
        private ArchivePanel _parentPanel;

        public void Setup(ArchiveNodeDefinition data, ArchivePanel parent)
        {
            _data = data;
            _parentPanel = parent;

            // setup icon đúng theo definition
            if (iconImage != null)
            {
                iconImage.sprite = (_data != null) ? _data.icon : null;
                iconImage.enabled = (iconImage.sprite != null);
            }

            RefreshVisual();

            // Gán sự kiện click
            btnSelect.onClick.RemoveAllListeners();
            btnSelect.onClick.AddListener(OnNodeClicked);
        }

        public void RefreshVisual()
        {
            if (_data == null) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_data.id);
            bool canAfford = ArchiveManager.Instance.currentPoints >= _data.costToUnlock;

            if (isUnlocked)
            {
                // Đã mở: Sáng màu Cyan, tắt ổ khóa
                if (borderImage) borderImage.color = unlockedColor;

                // icon giữ trắng cho dễ nhìn (nếu có icon)
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;

                if (lockOverlay) lockOverlay.SetActive(false);
            }
            else
            {
                // Chưa mở: Hiện ổ khóa
                if (lockOverlay) lockOverlay.SetActive(true);

                if (canAfford)
                {
                    // Đủ tiền mua: Màu vàng nhấp nháy (hoặc sáng hơn)
                    if (borderImage) borderImage.color = affordableColor;

                    // icon hơi mờ cho cảm giác "chưa mở"
                    if (iconImage && iconImage.enabled) iconImage.color = new Color(1, 1, 1, 0.5f);
                }
                else
                {
                    // Nghèo / Chưa đủ điều kiện: Màu xám tối
                    if (borderImage) borderImage.color = lockedColor;

                    // icon tối lại
                    if (iconImage && iconImage.enabled) iconImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                }
            }
        }

        private void OnNodeClicked()
        {
            // Báo cho Panel cha biết "Tao vừa bị bấm"
            if (_parentPanel != null)
            {
                _parentPanel.SelectNode(_data);
            }
        }
    }
}
