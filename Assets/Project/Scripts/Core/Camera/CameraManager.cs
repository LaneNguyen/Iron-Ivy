﻿using System;
using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.Interfaces;
using Unity.Cinemachine;
using UnityEngine;

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

        [Serializable]
        public class MinigameCameraProfile
        {
            public string id;
            public CinemachineCamera virtualCamera;

            [Header("Targeting")]
            public Transform lookAt;
            public Transform follow;

            [Header("Offset Settings")]
            public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

            [Header("Lens")]
            public float fov = 40f;
        }

        [Serializable]
        private class AnimalCameraTuning
        {
            public float orbitDistance;
            public float orbitHeight;
            public float lookAtHeight;
            public float rotateSpeed;
        }

        public enum CameraPhase { IntroLocked, Gameplay }

        [Header("Phase Lock")]
        [SerializeField] private CameraPhase phase = CameraPhase.IntroLocked;

        [Tooltip("Trong IntroLocked, chỉ những cameraId trong list này mới được switch (case-insensitive).")]
        [SerializeField] private List<string> introAllowedCameraIds = new List<string> { "IntroCam", "EstablishCam" };

        [Header("Debug")]
        [SerializeField] private bool logSwitches = true;
        [SerializeField] private bool traceThirdCameraRequests = true;

        [Header("Danh sách camera zone / hệ thống")]
        [SerializeField] private List<CameraEntry> cameras = new List<CameraEntry>();

        [Header("Cấu hình mặc định")]
        [SerializeField] private CinemachineCamera defaultCamera;

        [Header("Startup Camera (anti flash)")]
        [SerializeField] private string startupCameraId = "EstablishCam";

        [Header("Fade (tùy chọn)")]
        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private float fadeDuration = 0.2f;

        [Header("Minigame profiles")]
        public MinigameCameraProfile plantProfile;
        public MinigameCameraProfile animalProfile;

        [Header("Animal orbit default (fallback)")]
        [SerializeField] private float animalOrbitDistance = 7f;
        [SerializeField] private float animalOrbitRotateSpeed = 25f;
        [SerializeField] private float animalOrbitHeight = 2.5f;

        [Header("Animal LookAt default (fallback)")]
        [SerializeField] private float animalLookAtHeight = 1.2f;

        [Header("Animal Focus Alpha (optional)")]
        [SerializeField] private FocusAlphaFader focusAlphaFader;

        private readonly Dictionary<string, CinemachineCamera> _cameraMap =
            new Dictionary<string, CinemachineCamera>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<CinemachineCamera, string> _reverseMap =
            new Dictionary<CinemachineCamera, string>();

        private readonly Stack<CinemachineCamera> _history = new Stack<CinemachineCamera>();

        private HashSet<string> _introAllowSet;

        public CinemachineCamera CurrentCamera { get; private set; }
        private Coroutine _fadeRoutine;

        private MinigameCameraProfile _currentMinigameProfile;
        private bool _hasActiveMinigame;

        private CinemachineCamera _previousMinigameCamera;
        private float _previousMinigameFov;
        private bool _hasPreviousMinigameCamera;

        private Transform _animalFocus;
        private float _animalOrbitAngle;
        private bool _isAnimalOrbitActive;
        private AnimalCameraTuning _animalTuningRuntime;

        private readonly List<Behaviour> _pausedBehaviours = new List<Behaviour>();
        private bool _worldPaused;
        public bool IsWorldPaused => _worldPaused;

        public event Action<CinemachineCamera, CinemachineCamera> OnCameraChanged;

        protected override void Awake()
        {
            if (!CheckInstance()) return;

            base.Awake();

            _introAllowSet = new HashSet<string>(introAllowedCameraIds, StringComparer.OrdinalIgnoreCase);

            BuildCameraMap();

            // Startup anti-flash: ưu tiên startupCameraId làm default
            if (!string.IsNullOrWhiteSpace(startupCameraId) &&
                _cameraMap.TryGetValue(startupCameraId, out var startupCam) &&
                startupCam != null)
            {
                defaultCamera = startupCam;
            }

            // Winner-take-all: tắt hết, bật default
            DisableAllCamerasInternal();

            if (defaultCamera != null)
            {
                defaultCamera.enabled = true;
                CurrentCamera = defaultCamera;
            }

            if (logSwitches)
                Debug.Log($"[CameraManager] Awake done. phase={phase} default={NameOf(defaultCamera)} Current={NameOf(CurrentCamera)} mapCount={_cameraMap.Count}");
        }

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.OnCameraSwitchRequested += HandleCameraSwitchRequested;
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.OnCameraSwitchRequested -= HandleCameraSwitchRequested;
        }

        private void LateUpdate()
        {
            if (_isAnimalOrbitActive && _animalFocus != null && animalProfile?.virtualCamera != null)
            {
                float speed = (_animalTuningRuntime != null && _animalTuningRuntime.rotateSpeed > 0f)
                    ? _animalTuningRuntime.rotateSpeed
                    : animalOrbitRotateSpeed;

                _animalOrbitAngle += speed * Time.unscaledDeltaTime;
                UpdateAnimalOrbitCameraPosition();
            }
        }

        public void SetPhaseIntroLocked()
        {
            phase = CameraPhase.IntroLocked;
            if (_introAllowSet == null) _introAllowSet = new HashSet<string>(introAllowedCameraIds, StringComparer.OrdinalIgnoreCase);
            if (logSwitches) Debug.Log("[CameraManager] Phase -> IntroLocked");
        }

        public void SetPhaseGameplay()
        {
            phase = CameraPhase.Gameplay;
            if (logSwitches) Debug.Log("[CameraManager] Phase -> Gameplay");
        }

        private bool IsAllowedInCurrentPhase(string cameraId)
        {
            if (phase == CameraPhase.Gameplay) return true;
            if (_introAllowSet == null) _introAllowSet = new HashSet<string>(introAllowedCameraIds, StringComparer.OrdinalIgnoreCase);
            return _introAllowSet.Contains(cameraId);
        }

        private static bool LooksLikeThird(string cameraId)
        {
            if (string.IsNullOrWhiteSpace(cameraId)) return false;
            return cameraId.Equals("3rdcamera", StringComparison.OrdinalIgnoreCase)
                   || cameraId.IndexOf("3rd", StringComparison.OrdinalIgnoreCase) >= 0
                   || cameraId.IndexOf("third", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetIdOf(CinemachineCamera cam)
        {
            if (cam == null) return null;
            if (_reverseMap.TryGetValue(cam, out var id)) return id;

            foreach (var kv in _cameraMap)
                if (kv.Value == cam) return kv.Key;

            return cam.name;
        }

        private bool RejectIfLocked(string cameraId, string entryPointTag)
        {
            if (string.IsNullOrWhiteSpace(cameraId)) return false;

            if (!IsAllowedInCurrentPhase(cameraId))
            {
                if (traceThirdCameraRequests && LooksLikeThird(cameraId))
                {
                    Debug.LogWarning($"[CameraManager][LOCK][{entryPointTag}] Reject '{cameraId}' phase={phase} Current={NameOf(CurrentCamera)}\n{Environment.StackTrace}");
                }
                else
                {
                    Debug.LogWarning($"[CameraManager][LOCK][{entryPointTag}] Reject '{cameraId}' phase={phase} Current={NameOf(CurrentCamera)}");
                }
                return true;
            }

            return false;
        }

        private void BuildCameraMap()
        {
            _cameraMap.Clear();
            _reverseMap.Clear();

            foreach (var e in cameras)
            {
                if (e == null || e.camera == null || string.IsNullOrWhiteSpace(e.id)) continue;

                if (!_cameraMap.ContainsKey(e.id))
                    _cameraMap.Add(e.id, e.camera);

                if (!_reverseMap.ContainsKey(e.camera))
                    _reverseMap.Add(e.camera, e.id);
            }
        }

        private void DisableAllCamerasInternal()
        {
            foreach (var cam in _cameraMap.Values)
            {
                if (cam == null) continue;
                cam.enabled = false;
            }
        }

        public void SwitchCamera(string id, bool pushHistory = true)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            if (RejectIfLocked(id, "SwitchCamera(string)")) return;

            if (_cameraMap.TryGetValue(id, out var cam) && cam != null)
            {
                SwitchCamera(cam, pushHistory);
            }
            else
            {
                Debug.LogWarning($"[CameraManager] SwitchCamera hit=False id='{id}'");
            }
        }

        public void SwitchCamera(CinemachineCamera targetCam, bool pushHistory = true)
        {
            if (targetCam == null || targetCam == CurrentCamera) return;

            string targetId = GetIdOf(targetCam);
            if (RejectIfLocked(targetId, "SwitchCamera(CamRef)")) return;

            if (pushHistory && CurrentCamera != null)
                _history.Push(CurrentCamera);

            InternalSwitch(CurrentCamera, targetCam, targetId);
        }

        private void HandleCameraSwitchRequested(ListenManager.CameraSwitchRequestPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.cameraId)) return;

            if (logSwitches)
                Debug.Log($"[CameraManager] OnCameraSwitchRequested id='{payload.cameraId}' pushHistory={payload.pushHistory} phase={phase}");

            if (RejectIfLocked(payload.cameraId, "Event")) return;

            SwitchCamera(payload.cameraId, payload.pushHistory);
        }

        public void RestorePreviousCamera()
        {
            while (_history.Count > 0)
            {
                var prev = _history.Pop();
                if (prev != null)
                {
                    string prevId = GetIdOf(prev);
                    if (RejectIfLocked(prevId, "RestorePrevious")) return;

                    InternalSwitch(CurrentCamera, prev, prevId);
                    return;
                }
            }
        }

        private void InternalSwitch(CinemachineCamera oldCam, CinemachineCamera newCam, string newCamId)
        {
            if (RejectIfLocked(newCamId, "InternalSwitch")) return;
            if (newCam == null) return;

            // Winner-take-all: disable all, enable target
            DisableAllCamerasInternal();
            newCam.enabled = true;

            var oldRef = CurrentCamera;
            CurrentCamera = newCam;

            OnCameraChanged?.Invoke(oldRef, newCam);

            if (logSwitches)
                Debug.Log($"[CameraManager] InternalSwitch {NameOf(oldRef)} -> {NameOf(newCam)} (enabled toggle)");

            if (fadeCanvas != null)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _fadeRoutine = StartCoroutine(FadeBlink());
            }
        }

        private IEnumerator FadeBlink()
        {
            float half = Mathf.Max(0.01f, fadeDuration * 0.5f);
            fadeCanvas.gameObject.SetActive(true);

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / half));
                yield return null;
            }
            fadeCanvas.alpha = 1f;

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / half));
                yield return null;
            }

            fadeCanvas.alpha = 0f;
            fadeCanvas.gameObject.SetActive(false);
            _fadeRoutine = null;
        }

        // ====== Minigame & Animal Orbit (giữ nguyên concept) ======

        public void ApplyPlantMinigameProfile(Transform lookAtTarget)
        {
            if (plantProfile?.virtualCamera == null) return;

            if (!_hasActiveMinigame) SaveCurrentStateAsPrevious();

            var vcam = plantProfile.virtualCamera;
            vcam.Follow = null;
            vcam.LookAt = lookAtTarget;

            _currentMinigameProfile = plantProfile;
            _hasActiveMinigame = true;
            
            _isAnimalOrbitActive = false;

            _animalFocus = null;           // Quan trọng nhất để ẩn icon
            _animalTuningRuntime = null;
            if (focusAlphaFader != null) focusAlphaFader.Deactivate();

            InternalSwitch(CurrentCamera, vcam, GetIdOf(vcam));
            PauseWorldForMinigame(lookAtTarget);
        }

        public void ApplyAnimalMinigameProfile(Transform focusTarget)
        {
            if (animalProfile?.virtualCamera == null) return;

            if (focusTarget == null)
            {
                if (focusAlphaFader != null) focusAlphaFader.Deactivate();
                _isAnimalOrbitActive = false;
                ApplyMinigameProfile(animalProfile, null);
                return;
            }

            if (_hasActiveMinigame && _currentMinigameProfile == animalProfile)
            {
                _animalFocus = focusTarget;
                _animalTuningRuntime = BuildAnimalTuningFromDefinition(_animalFocus);
                InitAnimalOrbitAngleFromCurrentCamera();
                UpdateAnimalOrbitCameraPosition();
                _isAnimalOrbitActive = true;
                if (focusAlphaFader != null) focusAlphaFader.Activate(_animalFocus);
                return;
            }

            if (!_hasActiveMinigame) SaveCurrentStateAsPrevious();

            _animalFocus = focusTarget;
            _animalTuningRuntime = BuildAnimalTuningFromDefinition(_animalFocus);
            InitAnimalOrbitAngleFromSource(_previousMinigameCamera != null ? _previousMinigameCamera : CurrentCamera);
            UpdateAnimalOrbitCameraPosition();

            var vcam = animalProfile.virtualCamera;

            if (animalProfile.fov > 0f)
                vcam.Lens.FieldOfView = animalProfile.fov;
            _currentMinigameProfile = animalProfile;
            _hasActiveMinigame = true;
            _isAnimalOrbitActive = true;

            if (focusAlphaFader != null) focusAlphaFader.Activate(_animalFocus);

            InternalSwitch(CurrentCamera, vcam, GetIdOf(vcam));
            PauseWorldForMinigame(focusTarget);
        }

        public void RestoreMinigameCamera()
        {
            if (!_hasActiveMinigame && !_worldPaused) return;

            if (focusAlphaFader != null) focusAlphaFader.Deactivate();

            ResumeWorldFromMinigame();

            if (_hasPreviousMinigameCamera && _previousMinigameCamera != null)
            {
                _previousMinigameCamera.Lens.FieldOfView = _previousMinigameFov;
                InternalSwitch(CurrentCamera, _previousMinigameCamera, GetIdOf(_previousMinigameCamera));
                _previousMinigameCamera = null;
                _hasPreviousMinigameCamera = false;
            }
            else if (defaultCamera != null) SwitchCamera(defaultCamera);

            _currentMinigameProfile = null;
            _hasActiveMinigame = false;
            _isAnimalOrbitActive = false;
        }

        private void ApplyMinigameProfile(MinigameCameraProfile profile, Transform focusTarget)
        {
            if (profile?.virtualCamera == null) return;
            if (!_hasActiveMinigame) SaveCurrentStateAsPrevious();

            SetupMinigameCameraTarget(profile, focusTarget);

            var vcam = profile.virtualCamera;

            _currentMinigameProfile = profile;
            _hasActiveMinigame = true;

            InternalSwitch(CurrentCamera, vcam, GetIdOf(vcam));
            PauseWorldForMinigame(focusTarget);
        }

        private void SaveCurrentStateAsPrevious()
        {
            _previousMinigameCamera = CurrentCamera != null ? CurrentCamera : defaultCamera;
            if (_previousMinigameCamera != null)
            {
                _previousMinigameFov = _previousMinigameCamera.Lens.FieldOfView;
                _hasPreviousMinigameCamera = true;
            }
        }

        private void SetupMinigameCameraTarget(MinigameCameraProfile profile, Transform focusTarget)
        {
            var vcam = profile.virtualCamera;
            if (vcam == null) return;

            if (focusTarget != null)
            {
                vcam.Follow = focusTarget;
                vcam.LookAt = focusTarget;
            }
            else
            {
                Transform defaultTarget = profile.follow != null ? profile.follow : transform;
                vcam.Follow = defaultTarget;
                vcam.LookAt = defaultTarget;
            }
        }

        private void InitAnimalOrbitAngleFromSource(CinemachineCamera sourceCam)
        {
            if (sourceCam == null || _animalFocus == null) { _animalOrbitAngle = 180f; return; }

            Vector3 toCam = sourceCam.transform.position - _animalFocus.position;
            toCam.y = 0f;

            if (toCam.sqrMagnitude < 0.0001f) _animalOrbitAngle = 180f;
            else _animalOrbitAngle = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;
        }

        private void InitAnimalOrbitAngleFromCurrentCamera() => InitAnimalOrbitAngleFromSource(CurrentCamera);

        private void UpdateAnimalOrbitCameraPosition()
        {
            if (_animalFocus == null || animalProfile?.virtualCamera == null) return;

            var cam = animalProfile.virtualCamera;
            float orbitDist = (_animalTuningRuntime != null && _animalTuningRuntime.orbitDistance > 0f) ? _animalTuningRuntime.orbitDistance : animalOrbitDistance;
            float orbitH = (_animalTuningRuntime != null && _animalTuningRuntime.orbitHeight > 0f) ? _animalTuningRuntime.orbitHeight : animalOrbitHeight;
            float lookH = (_animalTuningRuntime != null && _animalTuningRuntime.lookAtHeight > 0f) ? _animalTuningRuntime.lookAtHeight : animalLookAtHeight;

            float rad = _animalOrbitAngle * Mathf.Deg2Rad;
            Vector3 footPivot = _animalFocus.position;
            Vector3 lookPoint = footPivot + Vector3.up * lookH;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitDist;
            Vector3 pos = footPivot + offset + Vector3.up * orbitH;

            cam.transform.position = pos;
            cam.Follow = null;
            cam.LookAt = null;
            cam.transform.LookAt(lookPoint);
        }

        private AnimalCameraTuning BuildAnimalTuningFromDefinition(Transform animalRoot)
        {
            var tuning = new AnimalCameraTuning()
            {
                orbitDistance = animalOrbitDistance,
                orbitHeight = animalOrbitHeight,
                lookAtHeight = animalLookAtHeight,
                rotateSpeed = animalOrbitRotateSpeed
            };

            if (animalRoot == null) return tuning;

            var ctrl = animalRoot.GetComponentInParent<IronIvy.Gameplay.Animals.AnimalController>();
            var def = ctrl?.Definition;
            if (def == null) return tuning;

            if (def.cameraOrbitDistance > 0f) tuning.orbitDistance = def.cameraOrbitDistance;
            if (def.cameraOrbitHeight > 0f) tuning.orbitHeight = def.cameraOrbitHeight;
            if (def.cameraLookAtHeight > 0f) tuning.lookAtHeight = def.cameraLookAtHeight;
            if (def.cameraOrbitRotateSpeed > 0f) tuning.rotateSpeed = def.cameraOrbitRotateSpeed;

            return tuning;
        }

        private void PauseWorldForMinigame(Transform minigameRoot)
        {
            if (_worldPaused) return;
            _pausedBehaviours.Clear();

            // Sử dụng MonoBehaviour để quét chính xác các script logic
            var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
            foreach (var b in allBehaviours)
            {
                if (b == null || !b.enabled) continue;

                // Kiểm tra xem script này có được phép chạy tiếp không
                if (IsAllowedDuringMinigame(b, minigameRoot)) continue;

                b.enabled = false;
                _pausedBehaviours.Add(b);
            }

            _worldPaused = true;
        }
        private void ResumeWorldFromMinigame()
        {
            if (!_worldPaused) return;
            foreach (var b in _pausedBehaviours) { if (b != null) b.enabled = true; }
            _pausedBehaviours.Clear();
            _worldPaused = false;
        }

        private bool IsAllowedDuringMinigame(Behaviour behaviour, Transform minigameRoot)
        {
            if (behaviour == null) return true;

            // === Core managers / camera / audio ===
            if (behaviour is CameraManager || behaviour is FocusAlphaFader) return true;
            if (behaviour is CinemachineBrain || behaviour is CinemachineCamera) return true;
            if (behaviour is AudioManager) return true;

            // === UI input pipeline MUST stay alive ===
            // EventSystem + its Input Module (StandaloneInputModule / InputSystemUIInputModule)
            if (behaviour is UnityEngine.EventSystems.EventSystem) return true;
            if (behaviour is UnityEngine.EventSystems.BaseInputModule) return true;

            // Keep raycasters alive (GraphicRaycaster is MonoBehaviour)
            if (behaviour is UnityEngine.UI.GraphicRaycaster) return true;

            // Keep Selectable (Button, Toggle, Slider...) scripts alive (Selectable is MonoBehaviour)
            if (behaviour is UnityEngine.UI.Selectable) return true;

            // Keep any behaviour under a Canvas alive (safe for UI)
            if (behaviour is Canvas) return true;
            if (behaviour.GetComponentInParent<Canvas>() != null) return true;

            // === Cinemachine helper components on same object ===
            if (behaviour.GetComponent<CinemachineCamera>() != null) return true;

            // === Minigame logic ===
            if (behaviour.GetComponent<IMinigame>() != null) return true;

            // Keep scripts on focused target (animal/plant root)
            if (minigameRoot != null && behaviour.transform.IsChildOf(minigameRoot)) return true;

            return false;
        }

        private static string NameOf(CinemachineCamera cam) => cam == null ? "null" : cam.name;
    }
}
