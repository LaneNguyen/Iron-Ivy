using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronIvy.UI
{
    // Giúp UI WorldSpace luôn quay mặt về phía Camera
    public class UIBillboard : MonoBehaviour
    {
        private Camera _mainCam;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshCamera();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshCamera();
        }

        private void RefreshCamera()
        {
            // Ưu tiên camera đang render cho WorldSpace Canvas
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace && _canvas.worldCamera != null)
            {
                _mainCam = _canvas.worldCamera;
                return;
            }

            // Fallback: MainCamera tag
            _mainCam = Camera.main;

            // Fallback 2: nếu không có MainCamera, tìm camera đang active
            if (_mainCam == null)
            {
                _mainCam = FindFirstObjectByType<Camera>();
            }
        }

        private void LateUpdate()
        {
            // Scene đổi / camera swap runtime -> tự bắt lại
            if (_mainCam == null || !_mainCam.isActiveAndEnabled)
            {
                RefreshCamera();
                if (_mainCam == null) return;
            }

            // Quay mặt về phía camera (screen-aligned)
            transform.LookAt(
                transform.position + _mainCam.transform.rotation * Vector3.forward,
                _mainCam.transform.rotation * Vector3.up
            );
        }
    }
}
