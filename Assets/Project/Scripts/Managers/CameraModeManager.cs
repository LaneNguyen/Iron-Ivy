// CameraModeManager.cs
using UnityEngine;
using Unity.Cinemachine;
using StarterAssets;
using IronIvy.Gameplay;

public enum CameraMode { Iso, TPS }

public class CameraModeManager : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera isoCam;
    [SerializeField] private CinemachineCamera tpsCam;

    [Header("Controllers")]
    [Tooltip("Controller isometric")]
    [SerializeField] private IsoPlayerController isoController;
    [Tooltip("Controller TPS")]
    [SerializeField] private ThirdPersonController tpsController;

    [Header("TPS Mouse Look (đặt trên TPS vcam)")]
    [Tooltip("CinemachineInputAxisController hoặc component tương đương để đọc look.x/look.y")]
    [SerializeField] private Behaviour tpsLookInputProvider;

    [Header("Priority")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnTPS = true;
    [SerializeField] private bool lockCursorOnIso = false;

    [Header("State")]
    [SerializeField] private CameraMode current = CameraMode.Iso;

    void Start() => ApplyMode(current);

    public void SetMode(CameraMode mode)
    {
        if (current == mode) return;
        current = mode;
        ApplyMode(current);
    }

    public void ToggleMode() => SetMode(current == CameraMode.Iso ? CameraMode.TPS : CameraMode.Iso);

    private void ApplyMode(CameraMode mode)
    {
        bool useTPS = (mode == CameraMode.TPS);

        if (isoCam) isoCam.Priority = useTPS ? inactivePriority : activePriority;
        if (tpsCam) tpsCam.Priority = useTPS ? activePriority : inactivePriority;

        if (isoController) isoController.enabled = !useTPS;
        if (tpsController) tpsController.enabled = useTPS;

        if (tpsLookInputProvider) tpsLookInputProvider.enabled = useTPS;

        bool needLock = useTPS ? lockCursorOnTPS : lockCursorOnIso;
        Cursor.lockState = needLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !needLock;
    }

    //Nếu dùng PlayerInput thì không cần đổi map
        // note: ThirdPersonController đọc StarterAssetsInputs, Iso đọc InputActionReference riêng
        // tránh double input bằng cách chỉ enable đúng controller ở bước 2

    // Gợi ý fix khi thấy lạ
    // note: nếu TPS không xoay được, kiểm tra Cinemachine Input Axis Controller đã map look.x look.y
    // note: nếu Iso không đi được, kiểm tra moveAction trong IsoPlayerController

    //        using UnityEngine;

    //public class CameraModeZone : MonoBehaviour
    //    {1 chu
    //        public CameraMode mode = CameraMode.TPS;

    //        private void OnTriggerEnter(Collider other)
    //        {
    //            var mgr = FindFirstObjectByType<CameraModeManager>();
    //            if (mgr != null && other.CompareTag("Player"))
    //                mgr.SetMode(mode);
    //        }
    //    }
}
