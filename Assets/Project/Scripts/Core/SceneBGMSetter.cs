using UnityEngine;

public class SceneBGMSetter : MonoBehaviour
{
    [SerializeField] private string sceneBgmName;

    private void Start()
    {
        if (!string.IsNullOrEmpty(sceneBgmName) && AudioManager.Instance != null)
            AudioManager.Instance.RequestSceneDefaultBGM(sceneBgmName);
    }
}
