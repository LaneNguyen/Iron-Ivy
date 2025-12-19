using UnityEngine;
using Unity.Cinemachine;

namespace IronIvy.Systems.Camera
{
    [RequireComponent(typeof(Collider))]
    public class CameraTrigger : MonoBehaviour
    {
        [Header("Cau hinh chung")]
        [Tooltip("Tag cua nhan vat se kich hoat trigger")]
        [SerializeField] private string playerTag = "Player";

        [Header("Dich den (chon 1 trong 2)")]
        [Tooltip("Chon truc tiep CinemachineCamera dich den (neu de trong se dung ID)")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Tooltip("Hoac dien ID camera da dang ky trong CameraManager")]
        [SerializeField] private string targetCameraID;

        [Header("Tuy chon")]
        [Tooltip("Neu bat, khi roi khoi vung se khoi phuc camera cu")]
        [SerializeField] private bool restoreOnExit = true;

        [Tooltip("Neu bat, sau khi chuyen sang che do third person se dong bo lai goc nhin de tranh bi giat")]
        [SerializeField] private bool resyncThirdPersonAfterSwitch = true;

        private void Reset()
        {
            // Dat collider thanh trigger de khong chan vat ly
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Chi xu ly khi doi tuong mang tag chi dinh
            if (!other.CompareTag(playerTag)) return;

            if (!CameraManager.HasInstance)
            {
                Debug.LogWarning("[CameraTrigger] CameraManager chua san sang.");
                return;
            }

            // 1) Doi camera theo tham chieu neu co
            if (targetCamera != null)
            {
                CameraManager.Instance.SwitchCamera(targetCamera);
            }
            // 2) Neu khong co tham chieu thi thu doi theo ID
            else if (!string.IsNullOrWhiteSpace(targetCameraID))
            {
                CameraManager.Instance.SwitchCamera(targetCameraID);
            }
            else
            {
                Debug.LogWarning($"[CameraTrigger] Chua cau hinh dich den cho {name}");
                return;
            }

            // Dong bo goc nhin third person neu co controller phu hop
            // Muc dich: khi vua doi sang camera third person, dong bo yaw/pitch theo pivot de tranh nhay goc
            if (resyncThirdPersonAfterSwitch)
            {
                // Tim controller tren doi tuong Player vua vao vung
                var tps = other.GetComponent<IronIvy.Gameplay.PlayerThirdPersonController>();
                if (tps == null)
                {
                    // Thu tim trong con cua Player (neu Animator hoac controller nam o child)
                    tps = other.GetComponentInChildren<IronIvy.Gameplay.PlayerThirdPersonController>(true);
                }

                if (tps != null && tps.enabled)
                {
                    tps.ResyncCameraAnglesFromPivot();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (!restoreOnExit || !CameraManager.HasInstance) return;

            CameraManager.Instance.RestorePreviousCamera();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Nhac nho nhe: nen chi dung 1 trong 2 cach chon camera
            if (targetCamera != null && !string.IsNullOrWhiteSpace(targetCameraID))
            {
                Debug.LogWarning($"[CameraTrigger] {name}: Dang set ca targetCamera va targetCameraID. Uu tien targetCamera.");
            }
        }
#endif
    }
}