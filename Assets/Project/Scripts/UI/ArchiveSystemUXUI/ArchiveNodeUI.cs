using IronIvy.Core;
using IronIvy.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace IronIvy.UI
{
    public class ArchiveNodeUI : MonoBehaviour
    {
        [Header("Node Data (Manual Placement)")]
        public ArchiveNodeDefinition definition;

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

        [Header("Unaffordable Visual (not enough progress/energy)")]
        [Range(0.1f, 1f)] public float unaffordableAlpha = 0.65f;

        [Header("Inspect Border Rotation (when reading description)")]
        public bool useInspectRotation = true;
        public float inspectRotateSpeed = 10f;

        [Header("Colors (Config)")]
        public Color lockedColor = Color.gray;
        public Color unlockedColor = new Color(0f, 1f, 1f, 1f);
        public Color affordableColor = new Color(1f, 0.9f, 0.4f, 1f);
        public Color blockedByParentColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [Header("Ending Timeline (Scene Reference)")]
        [Tooltip("PlayableDirector của ending timeline. Kéo object có PlayableDirector vào đây.")]
        public PlayableDirector endingTimelineDirector;

        private ArchiveNodeDefinition _data;
        private ArchivePanel _parentPanel;
        private CanvasGroup _cg;

        private bool _isInspecting;
        private Quaternion _borderBaseRot;

        private static ArchiveNodeUI _currentInspectNode;

        private bool hasTriggeredEndingTimeline = false;

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

            if (borderImage != null)
                _borderBaseRot = borderImage.rectTransform.localRotation;

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

            if (_currentInspectNode == this)
            {
                _currentInspectNode = null;
                StopInspect();
            }
        }

        public void Setup(ArchiveNodeDefinition data, ArchivePanel parent)
        {
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

            if (borderImage != null)
                _borderBaseRot = borderImage.rectTransform.localRotation;

            RefreshVisual();
        }

        private void Update()
        {
            if (!useInspectRotation) return;
            if (!_isInspecting) return;
            if (borderImage == null) return;

            float z = Time.unscaledTime * inspectRotateSpeed;
            borderImage.rectTransform.localRotation = _borderBaseRot * Quaternion.Euler(0f, 0f, z);
        }

        public void RefreshVisual()
        {
            if (Data == null || !ArchiveManager.HasInstance) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(Data.id);

            float requiredPercent = Mathf.Clamp(Data.costToUnlock, 0f, 100f);
            float currentPercent = ArchiveManager.Instance.CurrentPercent100;

            bool canUnlockByProgress = currentPercent + 0.0001f >= requiredPercent;
            bool parentUnlocked = IsParentUnlocked();

            SetCostTextsVisible(!isUnlocked);

            if (!isUnlocked)
            {
                if (dataCostText != null) dataCostText.text = $"{requiredPercent:0.#}%";
                if (currentDataText != null) currentDataText.text = $"{currentPercent:0.#}%";
            }

            SetWholeNodeAlpha(1f);

            if (isUnlocked)
            {
                if (borderImage) borderImage.color = unlockedColor;
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;
                if (lockOverlay) lockOverlay.SetActive(false);

                if (btnSelect != null) btnSelect.interactable = true;
                return;
            }

            if (!parentUnlocked)
            {
                SetWholeNodeAlpha(blockedAlpha);

                if (borderImage) borderImage.color = blockedByParentColor;
                if (iconImage && iconImage.enabled) iconImage.color = Color.white;

                if (lockOverlay) lockOverlay.SetActive(true);

                if (btnSelect != null) btnSelect.interactable = true;
                return;
            }

            if (canUnlockByProgress)
            {
                if (borderImage) borderImage.color = affordableColor;
                if (iconImage && iconImage.enabled) iconImage.color = new Color(1f, 1f, 1f, 0.8f);
                if (lockOverlay) lockOverlay.SetActive(true);

                if (btnSelect != null) btnSelect.interactable = true;
                return;
            }

            SetWholeNodeAlpha(unaffordableAlpha);

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
            if (_parentPanel == null || Data == null) return;

            AudioManager.Instance?.PlayInterfaceSE();

            _parentPanel.SelectNode(Data);
            SetAsInspectNode();

            // Không chạy timeline ở đây nữa.
            // Timeline sẽ chạy khi node được unlock (HandleNodeUnlocked).
        }

        private void HandleNodeUnlocked(string id)
        {
            if (Data == null) return;
            if (Data.id != id) return;

            RefreshVisual();

            if (hasTriggeredEndingTimeline) return;
            if (!Data.triggerEndingTimelineOnUnlock) return;

            hasTriggeredEndingTimeline = true;
            TryStartEndingTimeline();
        }

        private void TryStartEndingTimeline()
        {
            if (!UIManager.HasInstance) return;

            if (endingTimelineDirector == null)
            {
                Debug.LogWarning("<color=yellow>[ArchiveNodeUI]</color> triggerEndingTimelineOnUnlock=true nhưng node chưa assign endingTimelineDirector.");
                return;
            }

            UIManager.Instance.PlayEndingTimeline(endingTimelineDirector);
        }

        private void SetAsInspectNode()
        {
            if (_currentInspectNode != null && _currentInspectNode != this)
                _currentInspectNode.StopInspect();

            _currentInspectNode = this;
            StartInspect();
        }

        private void StartInspect()
        {
            _isInspecting = true;
        }

        private void StopInspect()
        {
            _isInspecting = false;

            if (borderImage != null)
                borderImage.rectTransform.localRotation = _borderBaseRot;
        }
    }
}
