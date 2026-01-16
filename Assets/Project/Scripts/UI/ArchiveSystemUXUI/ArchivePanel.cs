using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronIvy.UI
{
    public class ArchivePanel : MonoBehaviour, IPointerClickHandler
    {
        [Header("Container (Manual Placement)")]
        public RectTransform nodesContainer;
        public RectTransform zoomContent;

        [Header("Detail Section")]
        public GameObject detailPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI costText;
        public Button unlockButton;
        public TextMeshProUGUI unlockButtonLabel;

        [Header("Global")]
        public TextMeshProUGUI totalPointsText;
        public Button closeButton;

        [Header("Typewriter (Description)")]
        public float descTypeSpeed = 0.02f;
        public bool allowClickSkipDesc = true;

        [Header("Node Reveal Animation")]
        public float containerAnimDuration = 0.3f;
        public float nodeRevealInterval = 0.04f;
        public float nodeFadeDuration = 0.2f;
        public float nodeStartScale = 0.9f;

        [Header("Optional Labels (auto hide when unlocked)")]
        public GameObject costLabelObject;
        public GameObject totalLabelObject;

        [Header("First Open Guide (Show once)")]
        public GuidePanelView firstOpenGuidePanel;
        public string firstOpenGuideStepId = "GUIDE_ARCHIVE_FIRST_OPEN";
        public bool ignorePrefsInEditorForTesting = true;
        [Min(0f)] public float firstOpenGuideDelaySeconds = 1.2f;
        public bool markShownWhenGuideCloses = true;

        [Header("Debug / Cheat (while panel is open)")]
        [Tooltip("Optional button: click để set Archive progress 100%.")]
        public Button debugFill100Button;

        [Tooltip("Phím tắt để set 100% khi panel đang mở.")]
        public KeyCode debugFillKey = KeyCode.F9;

        [Tooltip("Nếu true: set xong sẽ auto refresh node visuals + unlock button state.")]
        public bool refreshAfterDebugFill = true;

        private Coroutine _firstOpenGuideRoutine;

        private readonly List<ArchiveNodeUI> _spawnedNodes = new List<ArchiveNodeUI>();

        private ArchiveNodeDefinition _currentSelection;

        private Coroutine _descTypeRoutine;
        private bool _isTypingDesc;

        private Coroutine _revealRoutine;

        private bool _guideShownThisSession = false;

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);

            if (descText != null)
                descText.maxVisibleCharacters = int.MaxValue;

            if (zoomContent == null && nodesContainer != null)
                zoomContent = nodesContainer;

            if (firstOpenGuidePanel != null && markShownWhenGuideCloses)
            {
                firstOpenGuidePanel.onClosed.AddListener(OnFirstOpenGuideClosed);
            }

            if (debugFill100Button != null)
            {
                debugFill100Button.onClick.RemoveAllListeners();
                debugFill100Button.onClick.AddListener(OnDebugFill100Clicked);
            }
        }

        private void OnDestroy()
        {
            if (firstOpenGuidePanel != null && markShownWhenGuideCloses)
            {
                firstOpenGuidePanel.onClosed.RemoveListener(OnFirstOpenGuideClosed);
            }

            if (debugFill100Button != null)
            {
                debugFill100Button.onClick.RemoveListener(OnDebugFill100Clicked);
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (Input.GetKeyDown(debugFillKey))
            {
                if (IsTypingInInputField()) return;
                ApplyDebugFill100();
            }
        }

        private bool IsTypingInInputField()
        {
            if (EventSystem.current == null) return false;

            var go = EventSystem.current.currentSelectedGameObject;
            if (go == null) return false;

            if (go.GetComponent<TMP_InputField>() != null) return true;
            if (go.GetComponent<InputField>() != null) return true;

            return false;
        }

        // Public API cho node tự đăng ký
        public void RegisterNode(ArchiveNodeUI node)
        {
            if (node == null) return;
            if (_spawnedNodes.Contains(node)) return;

            _spawnedNodes.Add(node);

            if (node.Data != null)
                node.Setup(node.Data, this);

            PrepareNodeHidden(node);
        }

        public void UnregisterNode(ArchiveNodeUI node)
        {
            if (node == null) return;
            _spawnedNodes.Remove(node);
        }

        public void Show()
        {
            AudioManager.Instance?.PlayOpenPanelSE();
            gameObject.SetActive(true);

            StopNodeReveal();
            StopDescTyping(resetVisible: true);

            UpdateTotalPoints();
            if (detailPanel) detailPanel.SetActive(false);

            RefreshAllNodes();

            _revealRoutine = StartCoroutine(RevealNodesSequence());

            _guideShownThisSession = false;
            StartFirstOpenGuideDelayed();
        }

        public void Hide()
        {
            StopFirstOpenGuideDelayed();

            StopNodeReveal();
            StopDescTyping(resetVisible: true);
            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            AudioManager.Instance?.PlayInterfaceSE();
            if (UIManager.HasInstance)
            {
                UIManager.Instance.CloseArchiveUI();
                return;
            }

            Hide();
        }

        private void OnDebugFill100Clicked()
        {
            AudioManager.Instance?.PlayInterfaceSE();
            ApplyDebugFill100();
        }

        private void ApplyDebugFill100()
        {
            if (!ArchiveManager.HasInstance) return;

            ArchiveManager.Instance.SetProgressPercent100(save: true);

            if (!refreshAfterDebugFill) return;

            UpdateTotalPoints();
            RefreshAllNodes();

            if (_currentSelection != null)
                UpdateUnlockButtonState();
        }

        private void StartFirstOpenGuideDelayed()
        {
            StopFirstOpenGuideDelayed();

            if (firstOpenGuidePanel == null) return;
            if (!GuidePanelManager.HasInstance) return;

            bool ignorePrefs = GuidePanelManager.Instance.ShouldIgnorePrefsForTesting(ignorePrefsInEditorForTesting);
            if (!ignorePrefs && GuidePanelManager.Instance.HasShown(firstOpenGuideStepId))
                return;

            _firstOpenGuideRoutine = StartCoroutine(FirstOpenGuideDelayRoutine());
        }

        private void StopFirstOpenGuideDelayed()
        {
            if (_firstOpenGuideRoutine != null)
            {
                StopCoroutine(_firstOpenGuideRoutine);
                _firstOpenGuideRoutine = null;
            }
        }

        private IEnumerator FirstOpenGuideDelayRoutine()
        {
            float delay = Mathf.Max(0f, firstOpenGuideDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            _firstOpenGuideRoutine = null;

            if (!gameObject.activeInHierarchy) yield break;
            if (_guideShownThisSession) yield break;

            TryShowFirstOpenGuide();
        }

        private void TryShowFirstOpenGuide()
        {
            if (!gameObject.activeInHierarchy) return;
            if (firstOpenGuidePanel == null) return;
            if (!GuidePanelManager.HasInstance) return;

            if (_guideShownThisSession) return;

            var view = GuidePanelManager.Instance.ShowPanelIfNotComplete(
                firstOpenGuideStepId,
                firstOpenGuidePanel.gameObject,
                pauseGameWhenShow: true,
                forceShowOnTop: true,
                sortingOrderOverride: 6000,
                ignorePrefsInEditor: ignorePrefsInEditorForTesting,
                disableMarkInEditor: true
            );

            if (view == null) return;

            _guideShownThisSession = true;
        }

        private void OnFirstOpenGuideClosed()
        {
            if (!GuidePanelManager.HasInstance) return;
            GuidePanelManager.Instance.MarkShown(firstOpenGuideStepId);
        }

        private void RefreshAllNodes()
        {
            for (int i = 0; i < _spawnedNodes.Count; i++)
            {
                if (_spawnedNodes[i] != null)
                    _spawnedNodes[i].RefreshVisual();
            }
        }

        public void SelectNode(ArchiveNodeDefinition node)
        {
            _currentSelection = node;
            if (detailPanel) detailPanel.SetActive(true);

            if (titleText) titleText.text = node.title;
            if (descText) PlayDescTypewriter(node.description);

            UpdateUnlockButtonState();
        }

        private void UpdateUnlockButtonState()
        {
            if (_currentSelection == null) return;
            if (!ArchiveManager.HasInstance) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_currentSelection.id);
            SetCostTotalVisible(!isUnlocked);

            if (costText)
            {
                if (isUnlocked) costText.gameObject.SetActive(false);
                else
                {
                    float requiredPercent = Mathf.Clamp(_currentSelection.costToUnlock, 0f, 100f);
                    costText.gameObject.SetActive(true);
                    costText.text = $"{requiredPercent:0.#}%";
                }
            }

            if (unlockButton == null) return;

            unlockButton.onClick.RemoveAllListeners();

            if (isUnlocked)
            {
                unlockButton.interactable = false;
                if (unlockButtonLabel) unlockButtonLabel.text = "Đã sở hữu";
                return;
            }

            bool canUnlock = ArchiveManager.Instance.CanUnlockNode(_currentSelection, out string reason);
            unlockButton.interactable = canUnlock;

            if (unlockButtonLabel)
            {
                if (canUnlock) unlockButtonLabel.text = "MỞ KHÓA";
                else
                {
                    if (reason.StartsWith("Need parent")) unlockButtonLabel.text = "CẦN MỞ NODE TRƯỚC";
                    else if (reason.Contains("Duplicate id")) unlockButtonLabel.text = "LỖI ID (TRÙNG)";
                    else if (reason == "Not enough progress") unlockButtonLabel.text = "CHƯA ĐẠT MỨC DATA";
                    else unlockButtonLabel.text = "KHÔNG THỂ MỞ";
                }
            }

            if (canUnlock)
                unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        private void OnUnlockClicked()
        {
            if (_currentSelection == null) return;

            if (nodesContainer != null)
            {
                var nodes = nodesContainer.GetComponentsInChildren<ArchiveNodeUI>(true);
                for (int i = 0; i < nodes.Length; i++)
                    nodes[i].RefreshVisual();
            }

            if (!ArchiveManager.HasInstance) return;

            AudioManager.Instance?.PlaySE("NodeUnlock", 0);
            ArchiveManager.Instance.UnlockNode(_currentSelection);

            UpdateTotalPoints();
            UpdateUnlockButtonState();
            RefreshAllNodes();
        }

        private void UpdateTotalPoints()
        {
            if (ArchiveManager.HasInstance && totalPointsText)
            {
                float p = ArchiveManager.Instance.CurrentPercent100;
                totalPointsText.text = $"{p:0.#}%";
            }
        }

        private void PlayDescTypewriter(string content)
        {
            StopDescTyping(resetVisible: true);
            if (descText == null) return;
            _descTypeRoutine = StartCoroutine(DescTypeRoutine(content));
        }

        private IEnumerator DescTypeRoutine(string content)
        {
            if (descText == null) yield break;

            _isTypingDesc = true;

            descText.text = content;
            descText.maxVisibleCharacters = 0;

            descText.ForceMeshUpdate();
            int totalChars = descText.textInfo.characterCount;

            if (totalChars <= 0)
            {
                descText.maxVisibleCharacters = int.MaxValue;
                _isTypingDesc = false;
                _descTypeRoutine = null;
                yield break;
            }

            float delay = Mathf.Max(0.001f, descTypeSpeed);

            for (int i = 0; i <= totalChars; i++)
            {
                descText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(delay);
            }

            descText.maxVisibleCharacters = int.MaxValue;

            _isTypingDesc = false;
            _descTypeRoutine = null;
        }

        private void StopDescTyping(bool resetVisible)
        {
            if (_descTypeRoutine != null)
            {
                StopCoroutine(_descTypeRoutine);
                _descTypeRoutine = null;
            }

            _isTypingDesc = false;

            if (resetVisible && descText != null)
                descText.maxVisibleCharacters = int.MaxValue;
        }

        private void SkipDescTyping()
        {
            if (descText == null) return;
            StopDescTyping(resetVisible: false);
            descText.maxVisibleCharacters = int.MaxValue;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!allowClickSkipDesc) return;
            if (!_isTypingDesc) return;
            if (descText == null) return;

            RectTransform rt = descText.rectTransform;
            if (rt == null) return;

            bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                rt,
                eventData.position,
                eventData.pressEventCamera
            );

            if (inside) SkipDescTyping();
        }

        private void PrepareNodeHidden(ArchiveNodeUI node)
        {
            if (node == null) return;

            CanvasGroup cg = node.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            node.transform.localScale = Vector3.one * nodeStartScale;
        }

        private IEnumerator RevealNodesSequence()
        {
            yield return new WaitForSeconds(containerAnimDuration);

            for (int i = 0; i < _spawnedNodes.Count; i++)
            {
                var node = _spawnedNodes[i];
                if (node == null) continue;

                StartCoroutine(RevealSingleNode(node));

                if (nodeRevealInterval > 0f) yield return new WaitForSeconds(nodeRevealInterval);
                else yield return null;
            }

            _revealRoutine = null;
        }

        private IEnumerator RevealSingleNode(ArchiveNodeUI node)
        {
            if (node == null) yield break;

            CanvasGroup cg = node.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                node.transform.localScale = Vector3.one;
                yield break;
            }

            cg.interactable = false;
            cg.blocksRaycasts = false;

            float duration = Mathf.Max(0.01f, nodeFadeDuration);
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                cg.alpha = Mathf.Lerp(0f, 1f, p);

                float scale = Mathf.Lerp(nodeStartScale, 1f, p);
                node.transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            cg.alpha = 1f;
            node.transform.localScale = Vector3.one;

            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        private void StopNodeReveal()
        {
            if (_revealRoutine != null)
            {
                StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
        }

        private void SetCostTotalVisible(bool visible)
        {
            if (costText != null) costText.gameObject.SetActive(visible);
            if (totalPointsText != null) totalPointsText.gameObject.SetActive(visible);

            if (costLabelObject != null) costLabelObject.SetActive(visible);
            if (totalLabelObject != null) totalLabelObject.SetActive(visible);
        }
    }
}
