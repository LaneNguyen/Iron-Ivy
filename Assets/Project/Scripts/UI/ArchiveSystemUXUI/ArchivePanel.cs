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
        public float descTypeSpeed = 0.02f;
        public bool allowClickSkipDesc = true;

        [Header("Node Reveal Animation")]
        public float containerAnimDuration = 0.3f;
        public float nodeRevealInterval = 0.04f;
        public float nodeFadeDuration = 0.2f;
        public float nodeStartScale = 0.9f;

        [Header("Optional Labels (auto hide when unlocked)")]
        public GameObject costLabelObject;   // chữ ghi chú cho cost
        public GameObject totalLabelObject;  // chữ ghi chú cho total

        private List<ArchiveNodeUI> _spawnedNodes = new List<ArchiveNodeUI>();
        private ArchiveNodeDefinition _currentSelection;

        private Coroutine _descTypeRoutine;
        private bool _isTypingDesc;

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
            // đóng đúng flow (để UIManager bật lại main panel)
            if (UIManager.HasInstance)
            {
                UIManager.Instance.CloseArchiveUI();
                return;
            }

            // fallback nếu thiếu UIManager
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
            if (!ArchiveManager.HasInstance) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_currentSelection.id);
            SetCostTotalVisible(!isUnlocked);

            if (costText != null) costText.gameObject.SetActive(!isUnlocked);
            if (totalPointsText != null) totalPointsText.gameObject.SetActive(!isUnlocked);

            if (costText)
            {
                if (isUnlocked) costText.gameObject.SetActive(false);
                else
                {
                    costText.gameObject.SetActive(true);
                    costText.text = $"{_currentSelection.costToUnlock}%";
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
                    // map reason -> text dễ hiểu
                    if (reason.StartsWith("Need parent")) unlockButtonLabel.text = "CẦN MỞ NODE TRƯỚC";
                    else if (reason.Contains("Duplicate id")) unlockButtonLabel.text = "LỖI ID (TRÙNG)";
                    else if (reason == "Not enough points") unlockButtonLabel.text = "THIẾU DATA";
                    else unlockButtonLabel.text = "KHÔNG THỂ MỞ";
                }
            }

            if (canUnlock)
                unlockButton.onClick.AddListener(OnUnlockClicked);
        }

        private void OnUnlockClicked()
        {
            if (_currentSelection == null) return;
            if (!ArchiveManager.HasInstance) return;

            bool ok = ArchiveManager.Instance.UnlockNode(_currentSelection);

            UpdateTotalPoints();
            UpdateUnlockButtonState();

            for (int i = 0; i < _spawnedNodes.Count; i++)
            {
                if (_spawnedNodes[i] != null)
                    _spawnedNodes[i].RefreshVisual();
            }

            if (!ok)
                Debug.Log("[ArchivePanel] Unlock failed (parent/points/duplicate id).");
        }

        private void UpdateTotalPoints()
        {
            if (ArchiveManager.HasInstance && totalPointsText)
            {
                totalPointsText.text = $"{ArchiveManager.Instance.currentPoints}%";
            }
        }

        // ===== typewriter (giữ nguyên) =====

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

        // ===== node reveal (giữ nguyên) =====

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
