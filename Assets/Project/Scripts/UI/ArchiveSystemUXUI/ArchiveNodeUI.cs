using IronIvy.Core;
using IronIvy.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronIvy.UI
{
    public class ArchiveNodeUI : MonoBehaviour
    {
        [Header("Node Data (Manual Placement)")]
        [Tooltip("Lane tự kéo ArchiveNodeDefinition vào đây cho từng node trên cây.")]
        public ArchiveNodeDefinition definition;

        [Header("UI Components")]
        public Button btnSelect;
        public Image iconImage;
        public Image borderImage;
        public GameObject lockOverlay;

        [Header("Optional Texts (auto hide when unlocked)")]
        public TextMeshProUGUI dataCostText;     // hiển thị required %
        public TextMeshProUGUI currentDataText;  // hiển thị current %

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

        // Panel sẽ dùng property này thay vì truy cập field tên "definition" (tránh nhầm).
        public ArchiveNodeDefinition Data => _data != null ? _data : definition;


        private void Awake()
        {

            if (_parentPanel == null)
                _parentPanel = GetComponentInParent<ArchivePanel>(true);

            if (_data == null && definition != null)
                _data = definition;

            if (btnSelect != null)
            {
                btnSelect.onClick.RemoveAllListeners();
                btnSelect.onClick.AddListener(OnNodeClicked);
            }

            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            // optional: refresh visual luôn (nếu manager sẵn)
            if (ArchiveManager.HasInstance)
                RefreshVisual();
        }

        private void OnEnable()
        {
            if (ArchiveManager.HasInstance)
                ArchiveManager.Instance.OnNodeUnlocked += HandleNodeUnlocked;
        }
        private void OnDisable()
        {
            if (ArchiveManager.HasInstance)
                ArchiveManager.Instance.OnNodeUnlocked -= HandleNodeUnlocked;
        }


        public void Setup(ArchiveNodeDefinition data, ArchivePanel parent)
        {
            // Ưu tiên data truyền vào; nếu null thì dùng definition set trong Inspector.
            _data = data != null ? data : definition;
            _parentPanel = parent;

            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            if (iconImage != null)
            {
                iconImage.sprite = (_data != null) ? _data.icon : null;
                iconImage.enabled = (iconImage.sprite != null);
            }

            if (btnSelect != null)
            {
                btnSelect.onClick.RemoveAllListeners();
                btnSelect.onClick.AddListener(OnNodeClicked);
            }

            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (Data == null || !ArchiveManager.HasInstance) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(Data.id);

            // ✅ NEW RULE: costToUnlock là Required Progress Percent (%)
            float requiredPercent = Mathf.Clamp(Data.costToUnlock, 0f, 100f);
            float currentPercent = ArchiveManager.Instance.CurrentPercent100;

            bool canUnlockByProgress = currentPercent + 0.0001f >= requiredPercent;
            bool parentUnlocked = IsParentUnlocked();

            // node đã mở -> hide 2 text refs
            SetCostTextsVisible(!isUnlocked);

            // update optional texts
            if (!isUnlocked)
            {
                if (dataCostText != null) dataCostText.text = $"{requiredPercent:0.#}%";
                if (currentDataText != null) currentDataText.text = $"{currentPercent:0.#}%";
            }

            // reset alpha default
            SetWholeNodeAlpha(1f);

            if (isUnlocked)
            {
                if (borderImage) borderImage.color = unlockedColor;
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;
                if (lockOverlay) lockOverlay.SetActive(false);

                if (btnSelect != null) btnSelect.interactable = true;
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
                if (btnSelect != null) btnSelect.interactable = true;
                return;
            }

            // unlockable bình thường
            if (canUnlockByProgress)
            {
                if (borderImage) borderImage.color = affordableColor;
                if (iconImage && iconImage.enabled) iconImage.color = new Color(1f, 1f, 1f, 0.8f);
                if (lockOverlay) lockOverlay.SetActive(true);

                if (btnSelect != null) btnSelect.interactable = true;
                return;
            }

            if (borderImage) borderImage.color = lockedColor;
            if (iconImage && iconImage.enabled) iconImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            if (lockOverlay) lockOverlay.SetActive(true);

            if (btnSelect != null) btnSelect.interactable = true;
        }

        private bool IsParentUnlocked()
        {
            if (Data.requiredParent == null) return true;
            return ArchiveManager.Instance.IsNodeUnlocked(Data.requiredParent.id);
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
            if (_parentPanel != null && Data != null)
                _parentPanel.SelectNode(Data);
        }

        private void HandleNodeUnlocked(string id)
        {
            if (Data == null) return;
            if (Data.id != id) return;

            RefreshVisual();
        }
    }
}
