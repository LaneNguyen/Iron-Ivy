using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Core;
using IronIvy.Interfaces;

namespace IronIvy.Systems.Camera
{
    // CameraManager tổng cho game:
    // - Quản lý chuyển đổi camera zone (iso / TPS / cutscene...)
    // - Quản lý luôn camera minigame (plant / animal) + pause world tạm thời
    public class CameraManager : BaseManager<CameraManager>
    {
        [Serializable]
        public class CameraEntry
        {
            [Header("ID dùng để switch")]
            public string id;
            public CinemachineCamera camera;
        }

        [Serializable]
        public class MinigameCameraProfile
        {
            [Header("Id (optional)")]
            public string id;

            [Header("Camera")]
            public CinemachineCamera virtualCamera;

            [Header("Look/Follow override")]
            public Transform lookAt;
            public Transform follow;

            [Header("Lens")]
            [Tooltip("FOV minigame, <= 0 thì giữ nguyên FOV hiện tại")]
            public float fov = 40f;
        }

        [Header("Danh sách camera zone / hệ thống")]
        [SerializeField] private List<CameraEntry> cameras = new List<CameraEntry>();

        [Header("Cấu hình mặc định")]
        [SerializeField] private CinemachineCamera defaultCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 5;

        [Header("Fade (tùy chọn)")]
        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private float fadeDuration = 0.2f;

        [Header("Minigame profiles")]
        public MinigameCameraProfile plantProfile;
        public MinigameCameraProfile animalProfile;

        [Header("Animal orbit settings")]
        [Tooltip("Khoảng cách từ camera đến animal (bán kính quỹ đạo).")]
        [SerializeField] private float animalOrbitDistance = 7f;

        [Tooltip("Tốc độ xoay quỹ đạo quanh animal (độ / giây).")]
        [SerializeField] private float animalOrbitRotateSpeed = 25f;

        // chiều cao camera so với animal, giữ internal cho đỡ rối inspector
        private const float AnimalOrbitHeight = 2.5f;

        // === Runtime state chung ===
        private readonly Dictionary<string, CinemachineCamera> _cameraMap =
            new Dictionary<string, CinemachineCamera>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<CinemachineCamera> _history = new Stack<CinemachineCamera>();

        public CinemachineCamera CurrentCamera { get; private set; }
        private Coroutine _fadeRoutine;

        // === Runtime state cho minigame ===
        private MinigameCameraProfile _currentMinigameProfile;
        private bool _hasActiveMinigame;

        private CinemachineCamera _previousMinigameCamera;
        private float _previousMinigameFov;
        private bool _hasPreviousMinigameCamera;

        // Animal orbit runtime
        private Transform _animalFocus;
        private float _animalOrbitAngle;
        private bool _isAnimalOrbitActive;

        // pause world
        private readonly List<Behaviour> _pausedBehaviours = new List<Behaviour>();
        private bool _worldPaused;
        public bool IsWorldPaused => _worldPaused;

        public event Action<CinemachineCamera, CinemachineCamera> OnCameraChanged;

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
                // nothing special hiện tại
            }
        }

        private void LateUpdate()
        {
            // update quỹ đạo camera animal khi đang chơi minigame animal
            if (_isAnimalOrbitActive && _animalFocus != null && animalProfile != null && animalProfile.virtualCamera != null)
            {
                _animalOrbitAngle += animalOrbitRotateSpeed * Time.unscaledDeltaTime;
                UpdateAnimalOrbitCameraPosition();
            }
        }

        //========================
        // BUILD MAP & PRIORITY
        //========================

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

        // ========= API: SWITCH ZONE CAMERA =========

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

        // ========= API: MINIGAME =============

        // Plant minigame: gọi khi start
        public void ApplyPlantMinigameProfile(Transform focusTarget)
        {
            ApplyMinigameProfile(plantProfile, focusTarget);
        }

        // Animal minigame: gọi khi start (quỹ đạo xoay quanh animal)
        public void ApplyAnimalMinigameProfile(Transform focusTarget)
        {
            // nếu chưa có profile thì thôi
            if (animalProfile == null || animalProfile.virtualCamera == null)
            {
                Debug.LogWarning("[CameraManager] Animal minigame profile hoặc virtualCamera chưa set.");
                return;
            }

            if (focusTarget == null)
            {
                Debug.LogWarning("[CameraManager] Animal minigame không có focusTarget, fallback về logic chung.");
                ApplyMinigameProfile(animalProfile, null);
                return;
            }

            // nếu đang xài profile animal rồi thì chỉ cập nhật focus + reset quỹ đạo
            if (_hasActiveMinigame && _currentMinigameProfile == animalProfile)
            {
                _animalFocus = focusTarget;
                InitAnimalOrbitAngleFromCurrentCamera();
                UpdateAnimalOrbitCameraPosition();
                _isAnimalOrbitActive = true;
                return;
            }

            // lưu camera đang active nhất (global) để lát còn restore
            if (!_hasPreviousMinigameCamera)
            {
                _previousMinigameCamera = CurrentCamera != null ? CurrentCamera : FindCurrentActiveCinemachine();
                if (_previousMinigameCamera != null)
                {
                    _previousMinigameFov = _previousMinigameCamera.Lens.FieldOfView;
                    _hasPreviousMinigameCamera = true;
                }
            }

            _animalFocus = focusTarget;
            InitAnimalOrbitAngleFromCurrentCamera();
            UpdateAnimalOrbitCameraPosition();

            var vcam = animalProfile.virtualCamera;

            // boost priority > zone camera một chút để Cinemachine chọn nó
            vcam.Priority = activePriority + 5;

            // set FOV nếu có cấu hình
            if (animalProfile.fov > 0f)
                vcam.Lens.FieldOfView = animalProfile.fov;

            _currentMinigameProfile = animalProfile;
            _hasActiveMinigame = true;
            _isAnimalOrbitActive = true;

            // lưu camera zone hiện tại vào history để RestorePreviousCamera vẫn hoạt động
            if (CurrentCamera != null && CurrentCamera != vcam)
                _history.Push(CurrentCamera);

            // cho hệ thống switch chính biết là đang dùng camera này
            InternalSwitch(CurrentCamera, vcam);

            // pause world, chỉ chừa UI + minigame
            PauseWorldForMinigame(focusTarget);
        }

        // overload cho code cũ không truyền transform
        public void ApplyPlantMinigameProfile()
        {
            ApplyPlantMinigameProfile(null);
        }

        public void ApplyAnimalMinigameProfile()
        {
            ApplyAnimalMinigameProfile(null);
        }

        // gọi khi minigame kết thúc
        public void RestoreMinigameCamera()
        {
            // cho call nhiều lần cũng không sao
            if (!_hasActiveMinigame && !_worldPaused)
                return;

            ResumeWorldFromMinigame();

            if (_currentMinigameProfile != null && _currentMinigameProfile.virtualCamera != null)
            {
                // hạ priority về inactive để không bị giữ camera
                _currentMinigameProfile.virtualCamera.Priority = inactivePriority;
            }

            if (_hasPreviousMinigameCamera && _previousMinigameCamera != null)
            {
                _previousMinigameCamera.Lens.FieldOfView = _previousMinigameFov;
                // dùng hệ thống switch/history để quay lại camera cũ
                SwitchCamera(_previousMinigameCamera);
            }

            _currentMinigameProfile = null;
            _hasActiveMinigame = false;
            _hasPreviousMinigameCamera = false;

            _isAnimalOrbitActive = false;
            _animalFocus = null;
        }

        // ---- logic chung cho plant (và fallback animal) ----

        private void ApplyMinigameProfile(MinigameCameraProfile profile, Transform focusTarget)
        {
            if (profile == null || profile.virtualCamera == null)
            {
                Debug.LogWarning("[CameraManager] Minigame profile hoặc virtualCamera chưa set.");
                return;
            }

            // đang xài profile này rồi thì chỉ update target thôi
            if (_hasActiveMinigame && _currentMinigameProfile == profile)
            {
                SetupMinigameCameraTarget(profile, focusTarget);
                return;
            }

            // lưu camera đang active nhất (global)
            if (!_hasPreviousMinigameCamera)
            {
                _previousMinigameCamera = CurrentCamera != null ? CurrentCamera : FindCurrentActiveCinemachine();
                if (_previousMinigameCamera != null)
                {
                    _previousMinigameFov = _previousMinigameCamera.Lens.FieldOfView;
                    _hasPreviousMinigameCamera = true;
                }
            }

            SetupMinigameCameraTarget(profile, focusTarget);

            var vcam = profile.virtualCamera;

            // đẩy priority vcam minigame lên cao hơn hệ thống zone (dùng activePriority + 5 cho chắc)
            vcam.Priority = activePriority + 5;

            // set FOV nếu có cấu hình
            if (profile.fov > 0f)
                vcam.Lens.FieldOfView = profile.fov;

            _currentMinigameProfile = profile;
            _hasActiveMinigame = true;

            // dùng stack/history để biết camera trước đó
            if (CurrentCamera != null && CurrentCamera != vcam)
                _history.Push(CurrentCamera);

            InternalSwitch(CurrentCamera, vcam);

            // pause world, chỉ chừa UI + minigame
            PauseWorldForMinigame(focusTarget);
        }

        private void SetupMinigameCameraTarget(MinigameCameraProfile profile, Transform focusTarget)
        {
            var vcam = profile.virtualCamera;
            if (vcam == null) return;

            Transform follow = profile.follow != null ? profile.follow : focusTarget;
            Transform lookAt = profile.lookAt != null ? profile.lookAt : focusTarget;

            vcam.Follow = follow;
            vcam.LookAt = lookAt;
        }

        // ===== Animal orbit helper =====

        private void InitAnimalOrbitAngleFromCurrentCamera()
        {
            var sourceCam = _previousMinigameCamera != null
                ? _previousMinigameCamera
                : (CurrentCamera != null ? CurrentCamera : FindCurrentActiveCinemachine());

            if (sourceCam == null || _animalFocus == null)
            {
                // default nhìn theo trục -Z
                _animalOrbitAngle = 180f;
                return;
            }

            Vector3 toCam = sourceCam.transform.position - _animalFocus.position;
            toCam.y = 0f;

            if (toCam.sqrMagnitude < 0.0001f)
            {
                _animalOrbitAngle = 180f;
            }
            else
            {
                // atan2(z, x) để lấy góc quanh trục Y
                _animalOrbitAngle = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;
            }
        }

        private void UpdateAnimalOrbitCameraPosition()
        {
            if (_animalFocus == null || animalProfile == null || animalProfile.virtualCamera == null)
                return;

            var cam = animalProfile.virtualCamera;

            float rad = _animalOrbitAngle * Mathf.Deg2Rad;
            Vector3 center = _animalFocus.position;

            // XZ-plane orbit
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * animalOrbitDistance;

            Vector3 pos = center + offset + Vector3.up * AnimalOrbitHeight;

            cam.transform.position = pos;
            cam.Follow = null;
            cam.transform.LookAt(center);
            cam.LookAt = _animalFocus;
        }

        private CinemachineCamera FindCurrentActiveCinemachine()
        {
            var all = UnityEngine.Object.FindObjectsOfType<CinemachineCamera>(true);
            CinemachineCamera best = null;
            int bestPriority = int.MinValue;

            foreach (var cam in all)
            {
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.Priority > bestPriority)
                {
                    bestPriority = cam.Priority;
                    best = cam;
                }
            }

            return best;
        }

        // ========= PAUSE / RESUME WORLD cho minigame =========

        private void PauseWorldForMinigame(Transform minigameRoot)
        {
            if (_worldPaused) return;

            _pausedBehaviours.Clear();

            // pause hầu hết systems, chừa minigame + UI + Audio + Camera
            var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);

            foreach (var b in allBehaviours)
            {
                if (b == null) continue;
                if (!b.enabled) continue;

                if (IsAllowedDuringMinigame(b, minigameRoot))
                    continue;

                b.enabled = false;
                _pausedBehaviours.Add(b);
            }

            _worldPaused = true;
        }

        private void ResumeWorldFromMinigame()
        {
            if (!_worldPaused) return;

            foreach (var b in _pausedBehaviours)
            {
                if (b == null) continue;
                b.enabled = true;
            }

            _pausedBehaviours.Clear();
            _worldPaused = false;
        }

        private bool IsAllowedDuringMinigame(Behaviour behaviour, Transform minigameRoot)
        {
            if (behaviour == null) return true;

            // không tự disable chính mình
            if (behaviour is CameraManager)
                return true;

            // 🎥 WHITELIST CAMERA
            // Main Camera CinemachineBrain
            if (behaviour is CinemachineBrain)
                return true;

            // Tất cả virtual camera Cinemachine
            if (behaviour is CinemachineCamera)
                return true;

            // AudioManager để chạy BGM / SFX
            if (behaviour is AudioManager)
                return true;

            // object có IMinigame thì cho chạy
            if (behaviour.GetComponent<IMinigame>() != null)
                return true;

            // mọi thứ nằm dưới gốc minigameRoot thì cho chơi
            if (minigameRoot != null && behaviour.transform.IsChildOf(minigameRoot))
                return true;

            // UI thì không pause
            if (behaviour.GetComponentInParent<Canvas>() != null)
                return true;

            // EventSystem
            if (behaviour.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                return true;

            // còn lại (npc, animal, day cycle...) => cho pause
            return false;
        }

        // ========= FADING =========

        private IEnumerator FadeBlink()
        {
            if (fadeCanvas == null)
                yield break;

            fadeCanvas.gameObject.SetActive(true);
            yield return FadeTo(1f, fadeDuration);
            yield return FadeTo(0f, fadeDuration);
            fadeCanvas.gameObject.SetActive(false);
            _fadeRoutine = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (fadeCanvas == null)
                yield break;

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
