using System.Collections.Generic;
using UnityEngine;
using IronIvy.UI;
using IronIvy.Gameplay.Interaction;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Gameplay
{
    // Kế thừa IMinigame để không bị CameraManager tắt khi chạy game
    public class PlantArea : MonoBehaviour
    {
        [Header("Config")]
        public List<PlantPlot> plots = new List<PlantPlot>();

        [Header("Refs")]
        public PlantRhythmStartPanel startPanel;
        public ClickPlantRhythmMinigame minigameSystem;
        public InteractionTrigger interactionTrigger;

        private void Start()
        {
            if (!startPanel) startPanel = FindObjectOfType<PlantRhythmStartPanel>(true);
            if (!minigameSystem) minigameSystem = FindObjectOfType<ClickPlantRhythmMinigame>(true);
            if (!interactionTrigger) interactionTrigger = GetComponent<InteractionTrigger>();
        }

        private void Update()
        {
            if (minigameSystem != null && minigameSystem.IsRunning && interactionTrigger != null)
            {
                interactionTrigger.ForceHidePrompt();
            }
        }

        public void OnInteractPressed()
        {
            if (minigameSystem != null && minigameSystem.IsRunning) return;
            if (startPanel) startPanel.ShowForArea(this);
        }
    }
}