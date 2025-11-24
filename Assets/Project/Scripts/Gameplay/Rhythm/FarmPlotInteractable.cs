using UnityEngine;
using IronIvy.Gameplay;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Core;

// script đơn giản để cho player bấm tương tác vào farm plot
[RequireComponent(typeof(Collider))]
public class FarmPlotInteractable : MonoBehaviour, IInteractable
{
    [Header("PlantMinigame đang có trong scene")]
    public PlantRhythmMinigame plantMinigame;

    [Header("Optional năng lượng để chơi")]
    public int energyCost = 1;

    // prompt cho hệ interact
    public string Prompt => "Plant (RMB)";

    // vị trí interact trong world
    public Vector3 WorldPosition => transform.position;

    public void Interact(GameObject interactor)
    {
        if (plantMinigame == null)
        {
            Debug.LogWarning("FarmPlotInteractable missing plantMinigame ref");
            return;
        }

        // nếu minigame đang chạy thì không mở lại
        if (plantMinigame.IsRunning)
            return;

        // optional: nếu có hệ năng lượng thì trừ ở đây
        // if (!EnergyManager.Instance.TrySpend(energyCost)) return;

        plantMinigame.StartGame();
    }

    void Reset()
    {
        // đảm bảo collider để raycast trúng
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }
}
