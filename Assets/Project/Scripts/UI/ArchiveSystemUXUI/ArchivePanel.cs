using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using UnityEngine.EventSystems;

namespace IronIvy.UI
{
    public class ArchivePanel : MonoBehaviour, IPointerClickHandler
    {
        [Header("Container")]
        public Transform nodeContainer;
        public ArchiveNodeUI nodePrefab;

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
        [Tooltip("Tốc độ gõ chữ, số càng nhỏ càng nhanh (vd: 0.02)")]
        public float descTypeSpeed = 0.02f;

        [Tooltip("Nếu true thì click vào vùng descText sẽ skip animation")]
        public bool allowClickSkipDesc = true;

        [Header("Node Reveal Animation")]
        [Tooltip("Thời gian anim panel/container (scale-in/fade-in) trước khi node được reveal")]
        public float containerAnimDuration = 0.3f;

        [Tooltip("Delay giữa mỗi node khi reveal (cascade)")]
        public float nodeRevealInterval = 0.04f;

        [Tooltip("Thời gian fade + scale của mỗi node")]
        public float nodeFadeDuration = 0.2f;

        [Tooltip("Scale bắt đầu của node khi reveal (vd 0.9)")]
        public float nodeStartScale = 0.9f;

        private List<ArchiveNodeUI> _spawnedNodes = new List<ArchiveNodeUI>();
        private ArchiveNodeDefinition _currentSelection;

        // typewriter state
        private Coroutine _descTypeRoutine;
        private bool _isTypingDesc;

        // reveal state
        private Coroutine _revealRoutine;

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);

            if (descText != null)
                descText.maxVisibleCharacters = int.MaxValue;
        }

        public void Show()
        {
            gameObject.SetActive(true);

            StopNodeReveal();
            StopDescTyping(resetVisible: true);

            RebuildTree();
            UpdateTotalPoints();

            if (detailPanel) detailPanel.SetActive(false);

            _revealRoutine = StartCoroutine(RevealNodesSequence());
        }

        public void Hide()
        {
            StopNodeReveal();
            StopDescTyping(resetVisible: true);
            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            Hide();
        }

        private void RebuildTree()
        {
            foreach (Transform child in nodeContainer) Destroy(child.gameObject);
            _spawnedNodes.Clear();

            if (!ArchiveManager.HasInstance || ArchiveManager.Instance.allNodes == null)
                return;

            foreach (var nodeData in ArchiveManager.Instance.allNodes)
            {
                ArchiveNodeUI newNode = Instantiate(nodePrefab, nodeContainer);
                newNode.Setup(nodeData, this);
                _spawnedNodes.Add(newNode);

                PrepareNodeHidden(newNode);
            }
        }

        public void SelectNode(ArchiveNodeDefinition node)
        {
            _currentSelection = node;
            if (detailPanel) detailPanel.SetActive(true);

            if (titleText) titleText.text = node.title;

            if (descText)
                PlayDescTypewriter(node.description);

            UpdateUnlockButtonState();
        }

        private void UpdateUnlockButtonState()
        {
            if (_currentSelection == null) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_currentSelection.id);
            bool canAfford = ArchiveManager.Instance.currentPoints >= _currentSelection.costToUnlock;

            // cost show dạng %
            if (costText) costText.text = isUnlocked ? "UNLOCKED" : $"{_currentSelection.costToUnlock}%";

            if (unlockButton == null) return;

            unlockButton.interactable = !isUnlocked && canAfford;

            if (isUnlocked)
            {
                if (unlockButtonLabel) unlockButtonLabel.text = "Đã sở hữu";
                unlockButton.onClick.RemoveAllListeners();
                return;
            }

            if (unlockButtonLabel) unlockButtonLabel.text = canAfford ? "MỞ KHÓA" : "THIẾU DATA";

            unlockButton.onClick.RemoveAllListeners();
            if (canAfford)
                unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        private void OnUnlockClicked()
        {
            if (_currentSelection == null) return;

            ArchiveManager.Instance.UnlockNode(_currentSelection);

            UpdateTotalPoints();
            UpdateUnlockButtonState();

            for (int i = 0; i < _spawnedNodes.Count; i++)
            {
                if (_spawnedNodes[i] != null)
                    _spawnedNodes[i].RefreshVisual();
            }
        }

        private void UpdateTotalPoints()
        {
            if (ArchiveManager.HasInstance && totalPointsText)
            {
                // total show dạng %
                totalPointsText.text = $"{ArchiveManager.Instance.currentPoints}%";
            }
        }

        // =========================
        // TYPEWRITER
        // =========================

        private void PlayDescTypewriter(string content)
        {
            StopDescTyping(resetVisible: true);

            if (descText == null)
                return;

            _descTypeRoutine = StartCoroutine(DescTypeRoutine(content));
        }

        private IEnumerator DescTypeRoutine(string content)
        {
            if (descText == null)
                yield break;

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

            if (inside)
                SkipDescTyping();
        }

        // =========================
        // NODE REVEAL (FADE + SCALE)
        // =========================

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

                if (nodeRevealInterval > 0f)
                    yield return new WaitForSeconds(nodeRevealInterval);
                else
                    yield return null;
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
    }
}
