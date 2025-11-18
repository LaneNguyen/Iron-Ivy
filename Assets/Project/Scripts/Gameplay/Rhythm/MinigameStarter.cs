using UnityEngine;
using IronIvy.Gameplay.Rhythm;

public class MinigameStarter : MonoBehaviour
{
    public PlantRhythmMinigame plantMinigamePrefab;  // Prefab
    public Transform spawnParent;  // optional

    public void StartPlantMinigame()
    {
        // Tạo instance real trong scene
        var instance = Instantiate(plantMinigamePrefab, spawnParent);

        // Start game trên instance
        instance.StartGame();
    }
}
