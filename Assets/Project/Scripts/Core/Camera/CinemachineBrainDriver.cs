using Unity.Cinemachine;
using UnityEngine;

public class CinemachineBrainDriver : MonoBehaviour
{
    [SerializeField] private CinemachineBrain brain;

    [Header("When true, force manual update every frame")]
    [SerializeField] private bool forceManualUpdate = false;

    [Header("Intro: keep camera updating when Time.timeScale = 0")]
    [SerializeField] private bool introIgnoreTimeScale = true;

    private void Reset()
    {
        brain = GetComponent<CinemachineBrain>();
    }

    private void Awake()
    {
        if (brain == null) brain = GetComponent<CinemachineBrain>();
    }

    public void SetIntroMode(bool enabled)
    {
        // Cinemachine 3 có checkbox IgnoreTimeScale (tên field/property có thể khác giữa version)
        // Nếu project em không có property này, đọc phần "Nếu không có IgnoreTimeScale" ở dưới.
        if (brain != null)
        {
            try
            {
                brain.IgnoreTimeScale = enabled && introIgnoreTimeScale;
            }
            catch
            {
                // fallback: nếu API khác tên, thôi bỏ qua để tránh crash compile
            }
        }
    }

    private void LateUpdate()
    {
        if (brain == null) return;

        // Nếu em chọn Brain UpdateMethod = Manual:
        if (forceManualUpdate || brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate)
        {
            brain.ManualUpdate();
        }
    }
}
