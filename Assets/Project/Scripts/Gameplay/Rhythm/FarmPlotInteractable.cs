using UnityEngine;
using IronIvy.Gameplay;           
using IronIvy.Gameplay.Rhythm;     
using IronIvy.Core;    

[RequireComponent(typeof(Collider))]
public class FarmPlotInteractable : MonoBehaviour, IInteractable
{
    [Header("Hook to Plant Minigame runner in scene")]
    public PlantRhythmMinigame plantMinigame;

    [Header("Optional: Energy cost to start")]
    public int energyCost = 1;

    // IInteractable implementation
    public string Prompt => "Plant (RMB)";
    public Vector3 WorldPosition => transform.position;

    public void Interact(GameObject interactor)
    {
        if (plantMinigame == null)
        {
            Debug.LogWarning("[FarmPlotInteractable] Missing PlantRhythmMinigame reference.");
            return;
        }

        if (plantMinigame.IsRunning)
            return;

        // Optional: trừ năng lượng
        // if (!EnergyManager.Instance.TrySpend(energyCost)) return;

        plantMinigame.StartGame();
    }

    void Reset()
    {
        // đảm bảo có collider để raycast trúng
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }
}
