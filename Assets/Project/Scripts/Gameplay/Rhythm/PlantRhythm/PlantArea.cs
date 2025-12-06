using System.Collections.Generic;
using UnityEngine;
using IronIvy.UI;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Gameplay
{
    // Quản lý một cụm các ô đất (Plot)
    // Xử lý Trigger interaction
    public class PlantArea : MonoBehaviour
    {
        [Header("Config")]
        public List<PlantPlot> plots = new List<PlantPlot>();
        public KeyCode interactKey = KeyCode.F;
        public string playerTag = "Player";

        [Header("UI Interaction")]
        [Tooltip("Object UI WorldSpace hiện lên (ví dụ icon 'F')")]
        public GameObject interactPrompt;

        [Header("Refs")]
        public PlantRhythmStartPanel startPanel;
        public ClickPlantRhythmMinigame minigameSystem;

        private bool _isPlayerInZone;

        private void Start()
        {
            if (interactPrompt) interactPrompt.SetActive(false);
            if (!startPanel) startPanel = FindObjectOfType<PlantRhythmStartPanel>(true);
            if (!minigameSystem) minigameSystem = FindObjectOfType<ClickPlantRhythmMinigame>(true);
        }

        private void Update()
        {
            // Nếu minigame đang chạy thì không cho tương tác
            if (minigameSystem != null && minigameSystem.IsRunning)
            {
                if (interactPrompt.activeSelf) interactPrompt.SetActive(false);
                return;
            }

            if (_isPlayerInZone)
            {
                if (interactPrompt && !interactPrompt.activeSelf) interactPrompt.SetActive(true);

                if (Input.GetKeyDown(interactKey))
                {
                    OpenSelectionPanel();
                }
            }
            else
            {
                if (interactPrompt && interactPrompt.activeSelf) interactPrompt.SetActive(false);
            }
        }

        private void OpenSelectionPanel()
        {
            if (startPanel)
            {
                // Truyền Area này vào panel để panel biết có bao nhiêu plot
                startPanel.ShowForArea(this);
            }
        }

        // --- Physics Trigger ---
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _isPlayerInZone = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _isPlayerInZone = false;
            }
        }
    }
}