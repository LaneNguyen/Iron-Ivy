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
        public Image borderImage;
        public GameObject lockOverlay;

        [Header("Optional Texts (auto hide when unlocked)")]
        public TextMeshProUGUI dataCostText;
        public TextMeshProUGUI currentDataText;

        [Header("Parent Gating Visual")]
        [Range(0.1f, 1f)] public float blockedAlpha = 0.5f;

        [Header("Colors (Config)")]
        public Color lockedColor = Color.gray;
        public Color unlockedColor = new Color(0f, 1f, 1f, 1f);
        public Color affordableColor = new Color(1f, 0.9f, 0.4f, 1f);
        public Color blockedByParentColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        private ArchiveNodeDefinition _data;
        private ArchivePanel _parentPanel;
        private CanvasGroup _cg;

        public void Setup(ArchiveNodeDefinition data, ArchivePanel parent)
        {
            _data = data;
            _parentPanel = parent;

            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            if (iconImage != null)
            {
                iconImage.sprite = (_data != null) ? _data.icon : null;
                iconImage.enabled = (iconImage.sprite != null);
            }

            btnSelect.onClick.RemoveAllListeners();
            btnSelect.onClick.AddListener(OnNodeClicked);

            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (_data == null || !ArchiveManager.HasInstance) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_data.id);
            bool canAfford = ArchiveManager.Instance.currentPoints >= _data.costToUnlock;
            bool parentUnlocked = IsParentUnlocked();

            // node đã mở -> hide 2 text refs
            SetCostTextsVisible(!isUnlocked);

            // reset alpha default
            SetWholeNodeAlpha(1f);

            if (isUnlocked)
            {
                if (borderImage) borderImage.color = unlockedColor;
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;
                if (lockOverlay) lockOverlay.SetActive(false);

                btnSelect.interactable = true;
                return;
            }

            // blocked bởi parent: mờ toàn bộ node + màu riêng
            if (!parentUnlocked)
            {
                SetWholeNodeAlpha(blockedAlpha);

                if (borderImage) borderImage.color = blockedByParentColor;
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;

                if (lockOverlay) lockOverlay.SetActive(true);

                // vẫn cho click để xem mô tả (unlock bị chặn ở panel + manager)
                btnSelect.interactable = true;
                return;
            }

            // unlockable bình thường
            if (canAfford)
            {
                if (borderImage) borderImage.color = affordableColor;
                if (iconImage && iconImage.enabled) iconImage.color = new Color(1f, 1f, 1f, 0.8f);
                if (lockOverlay) lockOverlay.SetActive(true);

                btnSelect.interactable = true;
                return;
            }

            if (borderImage) borderImage.color = lockedColor;
            if (iconImage && iconImage.enabled) iconImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            if (lockOverlay) lockOverlay.SetActive(true);

            btnSelect.interactable = true;
        }

        private bool IsParentUnlocked()
        {
            if (_data.requiredParent == null) return true;
            return ArchiveManager.Instance.IsNodeUnlocked(_data.requiredParent.id);
        }

        private void SetCostTextsVisible(bool visible)
        {
            if (dataCostText != null) dataCostText.gameObject.SetActive(visible);
            if (currentDataText != null) currentDataText.gameObject.SetActive(visible);
        }

        private void SetWholeNodeAlpha(float a)
        {
            if (_cg == null) return;
            _cg.alpha = Mathf.Clamp01(a);
        }

        private void OnNodeClicked()
        {
            if (_parentPanel != null)
                _parentPanel.SelectNode(_data);
        }
    }
}
