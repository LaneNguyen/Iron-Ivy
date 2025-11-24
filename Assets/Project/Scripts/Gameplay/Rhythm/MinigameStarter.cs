using UnityEngine;
using IronIvy.Gameplay.Rhythm;

// script này chỉ để spawn minigame plant lên scene rồi start
public class MiniggameStarter : MonoBehaviour
{
    public PlantRhythmMinigame plantMinigamePrefab;
    public Transform spawnParent;

    public void StartPlantMinigame()
    {
        var instance = Instantiate(plantMinigamePrefab, spawnParent);
        instance.StartGame();
    }
}
