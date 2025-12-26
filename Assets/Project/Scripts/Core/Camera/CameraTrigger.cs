﻿using UnityEngine;
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
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            if (!CameraManager.HasInstance)
            {
                Debug.LogWarning("[CameraTrigger] CameraManager chua san sang.");
                return;
            }

            if (targetCamera != null)
            {
                CameraManager.Instance.SwitchCamera(targetCamera);
            }
            else if (!string.IsNullOrWhiteSpace(targetCameraID))
            {
                CameraManager.Instance.SwitchCamera(targetCameraID);
            }
            else
            {
                Debug.LogWarning($"[CameraTrigger] Chua cau hinh dich den cho {name}");
                return;
            }

            if (resyncThirdPersonAfterSwitch)
            {
                var tps = other.GetComponent<IronIvy.Gameplay.PlayerThirdPersonController>();
                if (tps == null)
                {
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
            if (targetCamera != null && !string.IsNullOrWhiteSpace(targetCameraID))
            {
                Debug.LogWarning($"[CameraTrigger] {name}: Dang set ca targetCamera va targetCameraID. Uu tien targetCamera.");
            }
        }
#endif
    }
}
