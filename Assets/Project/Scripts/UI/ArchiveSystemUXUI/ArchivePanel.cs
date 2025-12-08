using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;

namespace IronIvy.UI
{
    public class ArchivePanel : MonoBehaviour
    {
        // ... (Giữ nguyên các biến khai báo cũ: nodeContainer, detailPanel...)
        [Header("Container")]
        public Transform nodeContainer; 
        public ArchiveNodeUI nodePrefab; 

        [Header("Detail Section")]
        public GameObject detailPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI costText;
        public Button unlockButton;
        public TextMeshProUGUI unlockButtonLabel; // Label con của nút unlock

        [Header("Global")]
        public TextMeshProUGUI totalPointsText;
        public Button closeButton; // [NEW] Nút tắt panel

        private List<ArchiveNodeUI> _spawnedNodes = new List<ArchiveNodeUI>();
        private ArchiveNodeDefinition _currentSelection;

        private void Awake()
        { // Tắt Panel lúc đầu game
        //gameObject.SetActive(false);
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RebuildTree();
            UpdateTotalPoints();
            if (detailPanel) detailPanel.SetActive(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            // Gọi UIManager để Fade Out rồi mới tắt
            if (UIManager.HasInstance)
            {
                UIManager.Instance.CloseArchiveUI();
            }
            else
            {
                Hide(); // Fallback nếu không có UIManager
            }
        }

        // ... (Giữ nguyên logic RebuildTree, SelectNode, UpdateUnlockButtonState...)
        
        private void RebuildTree()
        {
            foreach (Transform child in nodeContainer) Destroy(child.gameObject);
            _spawnedNodes.Clear();

            if (ArchiveManager.HasInstance && ArchiveManager.Instance.allNodes != null)
            {
                foreach (var nodeData in ArchiveManager.Instance.allNodes)
                {
                    ArchiveNodeUI newNode = Instantiate(nodePrefab, nodeContainer);
                    newNode.Setup(nodeData, this);
                    _spawnedNodes.Add(newNode);
                }
            }
        }

        public void SelectNode(ArchiveNodeDefinition node)
        {
            _currentSelection = node;
            if (detailPanel) detailPanel.SetActive(true);

            if (titleText) titleText.text = node.title;
            if (descText) descText.text = node.description;
            
            UpdateUnlockButtonState();
        }

        private void UpdateUnlockButtonState()
        {
            if (_currentSelection == null) return;

            bool isUnlocked = ArchiveManager.Instance.IsNodeUnlocked(_currentSelection.id);
            bool canAfford = ArchiveManager.Instance.currentPoints >= _currentSelection.costToUnlock;

            if (costText) costText.text = isUnlocked ? "UNLOCKED" : $"{_currentSelection.costToUnlock} Data";

            if (unlockButton)
            {
                unlockButton.interactable = !isUnlocked && canAfford;
                if (isUnlocked) 
                {
                    if (unlockButtonLabel) unlockButtonLabel.text = "Đã sở hữu";
                }
                else 
                {
                    if (unlockButtonLabel) unlockButtonLabel.text = canAfford ? "MỞ KHÓA" : "THIẾU DATA";
                    if (canAfford)
                    {
                        unlockButton.onClick.RemoveAllListeners();
                        unlockButton.onClick.AddListener(OnUnlockClicked);
                    }
                }
            }
        }

        private void OnUnlockClicked()
        {
            if (_currentSelection == null) return;
            ArchiveManager.Instance.UnlockNode(_currentSelection);
            
            UpdateTotalPoints();
            UpdateUnlockButtonState();
            foreach (var ui in _spawnedNodes) ui.RefreshVisual();
        }

        private void UpdateTotalPoints()
        {
            if (ArchiveManager.HasInstance && totalPointsText)
            {
                totalPointsText.text = $"Total Data: {ArchiveManager.Instance.currentPoints}";
            }
        }
    }
}