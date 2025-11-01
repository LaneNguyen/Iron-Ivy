using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace IronIvy.Systems.Camera
{
    public class CameraManager : BaseManager<CameraManager>
    {
        [Serializable]
        public class CameraEntry
        {
            public string id;
            public CinemachineCamera camera;
        }

        [Header("Danh sách camera quản lý")]
        [SerializeField] private List<CameraEntry> cameras = new List<CameraEntry>();

        [Header("Cấu hình mặc định")]
        [SerializeField] private CinemachineCamera defaultCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 5;

        [Header("Fade (tùy chọn)")]
        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private float fadeDuration = 0.2f;

        public event Action<CinemachineCamera, CinemachineCamera> OnCameraChanged;

        private readonly Dictionary<string, CinemachineCamera> _cameraMap =
            new Dictionary<string, CinemachineCamera>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<CinemachineCamera> _history = new Stack<CinemachineCamera>();

        public CinemachineCamera CurrentCamera { get; private set; }
        private Coroutine _fadeRoutine;

        protected override void Awake()
        {
            if (!CheckInstance()) return;
            base.Awake();

            BuildCameraMap();
            ApplyInitialPriorities();
        }

        private void OnDestroy()
        {
            if (HasInstance && Instance == this)
            {
            }
        }

        private void BuildCameraMap()
        {
            _cameraMap.Clear();
            foreach (var e in cameras)
            {
                if (e == null || e.camera == null || string.IsNullOrWhiteSpace(e.id)) continue;
                if (_cameraMap.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[CameraManager] Trùng ID: {e.id}");
                    continue;
                }
                _cameraMap.Add(e.id, e.camera);
            }

            if (defaultCamera == null && cameras.Count > 0)
                defaultCamera = cameras[0].camera;
        }

        private void ApplyInitialPriorities()
        {
            foreach (var cam in _cameraMap.Values)
                if (cam != null) cam.Priority = inactivePriority;

            if (defaultCamera != null)
            {
                defaultCamera.Priority = activePriority;
                CurrentCamera = defaultCamera;
            }
        }

        // ========= API =========
        public void SwitchCamera(string id)
        {
            if (!_cameraMap.TryGetValue(id, out var cam) || cam == null)
            {
                Debug.LogWarning($"[CameraManager] Không tìm thấy camera ID: {id}");
                return;
            }
            SwitchCamera(cam);
        }

        public void SwitchCamera(CinemachineCamera targetCam)
        {
            if (targetCam == null || targetCam == CurrentCamera) return;
            if (CurrentCamera != null) _history.Push(CurrentCamera);
            InternalSwitch(CurrentCamera, targetCam);
        }

        public void RestorePreviousCamera()
        {
            while (_history.Count > 0)
            {
                var prev = _history.Pop();
                if (prev != null)
                {
                    InternalSwitch(CurrentCamera, prev);
                    return;
                }
            }
        }

        private void InternalSwitch(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (oldCam != null) oldCam.Priority = inactivePriority;
            if (newCam != null) newCam.Priority = activePriority;

            var oldRef = CurrentCamera;
            CurrentCamera = newCam;
            OnCameraChanged?.Invoke(oldRef, newCam);

            if (fadeCanvas != null)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _fadeRoutine = StartCoroutine(FadeBlink());
            }
        }

        private IEnumerator FadeBlink()
        {
            fadeCanvas.gameObject.SetActive(true);
            yield return FadeTo(1f, fadeDuration);
            yield return FadeTo(0f, fadeDuration);
            fadeCanvas.gameObject.SetActive(false);
            _fadeRoutine = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = fadeCanvas.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            fadeCanvas.alpha = target;
        }
    }
}
