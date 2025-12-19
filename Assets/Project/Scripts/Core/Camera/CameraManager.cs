using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Core;
using IronIvy.Interfaces;
using IronIvy.Data;

namespace IronIvy.Systems.Camera
{
    // camera manager tổng cho game
    // - quản lý list CinemachineCamera
    // - switch camera theo id
    // - handle minigame camera (plant, animal) + pause world
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
            public Transform lookAt;      // default object để camera nhìn
            public Transform follow;      // default object để camera follow

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

        [Header("Animal orbit default (fallback)")]
        [SerializeField] private float animalOrbitDistance = 7f;
        [SerializeField] private float animalOrbitRotateSpeed = 25f;
        [SerializeField] private float animalOrbitHeight = 2.5f;

        [Header("Animal LookAt default (fallback)")]
        [SerializeField] private float animalLookAtHeight = 1.2f;

        [Header("Animal Focus Alpha (optional)")]
        [SerializeField] private FocusAlphaFader focusAlphaFader;

        // state chung cho toàn hệ thống camera
        private readonly Dictionary<string, CinemachineCamera> _cameraMap =
            new Dictionary<string, CinemachineCamera>(StringComparer.OrdinalIgnoreCase);

        private readonly Stack<CinemachineCamera> _history = new Stack<CinemachineCamera>();

        public CinemachineCamera CurrentCamera { get; private set; }
        private Coroutine _fadeRoutine;

        // state cho minigame
        private MinigameCameraProfile _currentMinigameProfile;
        private bool _hasActiveMinigame;

        // lưu lại camera trước khi vào minigame để tránh drift
        private CinemachineCamera _previousMinigameCamera;
        private float _previousMinigameFov;
        private bool _hasPreviousMinigameCamera;

        // animal orbit runtime
        private Transform _animalFocus;
        private float _animalOrbitAngle;
        private bool _isAnimalOrbitActive;

        // tuning runtime (Option 1)
        private AnimalCameraTuning _animalTuningRuntime;

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

        private void LateUpdate()
        {
            // nếu đang orbit animal thì update xoay camera quanh nó
            if (_isAnimalOrbitActive &&
                _animalFocus != null &&
                animalProfile != null &&
                animalProfile.virtualCamera != null)
            {
                float speed = (_animalTuningRuntime != null && _animalTuningRuntime.rotateSpeed > 0f)
                    ? _animalTuningRuntime.rotateSpeed
                    : animalOrbitRotateSpeed;

                _animalOrbitAngle += speed * Time.unscaledDeltaTime;
                UpdateAnimalOrbitCameraPosition();
            }
        }

        // build map id -> camera cho dễ gọi
        private void BuildCameraMap()
        {
            _cameraMap.Clear();

            foreach (var e in cameras)
            {
                if (e == null || e.camera == null || string.IsNullOrWhiteSpace(e.id)) continue;
                if (!_cameraMap.ContainsKey(e.id))
                    _cameraMap.Add(e.id, e.camera);
            }

            // nếu chưa set default thì lấy camera đầu tiên
            if (defaultCamera == null && cameras.Count > 0)
                defaultCamera = cameras[0].camera;
        }

        // set priority ban đầu
        private void ApplyInitialPriorities()
        {
            foreach (var cam in _cameraMap.Values)
            {
                if (cam != null) cam.Priority = inactivePriority;
            }

            if (defaultCamera != null)
            {
                defaultCamera.Priority = activePriority;
                CurrentCamera = defaultCamera;
            }
        }

        public void SwitchCamera(string id)
        {
            if (_cameraMap.TryGetValue(id, out var cam) && cam != null)
                SwitchCamera(cam);
        }

        public void SwitchCamera(CinemachineCamera targetCam)
        {
            // đổi sang camera khác
            if (targetCam == null || targetCam == CurrentCamera) return;

            if (CurrentCamera != null)
                _history.Push(CurrentCamera);

            InternalSwitch(CurrentCamera, targetCam);
        }

        public void RestorePreviousCamera()
        {
            // pop lịch sử camera để quay lại
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
            if (oldCam != null)
                oldCam.Priority = inactivePriority;

            if (newCam != null)
                newCam.Priority = activePriority;

            var oldRef = CurrentCamera;
            CurrentCamera = newCam;

            OnCameraChanged?.Invoke(oldRef, newCam);

            // blink nhẹ khi chuyển camera cho đỡ gắt
            // (fadeCanvas là một CanvasGroup phủ màn hình, alpha lên 1 rồi về 0 nhanh để làm transition)
            if (fadeCanvas != null)
            {
                if (_fadeRoutine != null)
                    StopCoroutine(_fadeRoutine);

                _fadeRoutine = StartCoroutine(FadeBlink());
            }
        }

        // camera cho plant minigame
        // - camera đứng yên tại vị trí đặt trong scene
        // - chỉ xoay LookAt về chậu cây
        // - pause world (trừ minigame + UI)
        public void ApplyPlantMinigameProfile(Transform lookAtTarget)
        {
            if (plantProfile == null || plantProfile.virtualCamera == null) return;

            if (!_hasActiveMinigame)
                SaveCurrentStateAsPrevious();

            var vcam = plantProfile.virtualCamera;

            // bỏ Follow để camera không chạy lung tung
            vcam.Follow = null;

            // chỉ update LookAt
            vcam.LookAt = lookAtTarget;

            vcam.Priority = activePriority + 5;
            if (plantProfile.fov > 0f)
                vcam.Lens.FieldOfView = plantProfile.fov;

            _currentMinigameProfile = plantProfile;
            _hasActiveMinigame = true;

            _isAnimalOrbitActive = false;
            _animalFocus = null;
            _animalTuningRuntime = null;

            // chắc chắn tắt focus alpha (nếu đang bật từ animal)
            if (focusAlphaFader != null)
                focusAlphaFader.Deactivate();

            InternalSwitch(CurrentCamera, vcam);
            PauseWorldForMinigame(lookAtTarget);
        }

        // logic camera animal + minigame dùng chung
        // - focus vào con thú
        // - xoay camera orbit quanh animal
        public void ApplyAnimalMinigameProfile(Transform focusTarget)
        {
            if (animalProfile == null || animalProfile.virtualCamera == null) return;

            // nếu không có target cụ thể thì xài profile chung
            if (focusTarget == null)
            {
                // PATCH: nếu vào animal mode mà không có focus target,
                // thì đảm bảo tắt focusAlphaFader để tránh kẹt mờ từ lần trước
                if (focusAlphaFader != null)
                    focusAlphaFader.Deactivate();

                _isAnimalOrbitActive = false;
                _animalFocus = null;
                _animalTuningRuntime = null;

                ApplyMinigameProfile(animalProfile, null);
                return;
            }

            // nếu đã ở trong chế độ animal rồi thì chỉ update lại focus
            if (_hasActiveMinigame && _currentMinigameProfile == animalProfile)
            {
                _animalFocus = focusTarget;
                _animalTuningRuntime = BuildAnimalTuningFromDefinition(_animalFocus);

                InitAnimalOrbitAngleFromCurrentCamera();
                UpdateAnimalOrbitCameraPosition();
                _isAnimalOrbitActive = true;

                // bật focus alpha theo con animal mới
                if (focusAlphaFader != null)
                    focusAlphaFader.Activate(_animalFocus);

                return;
            }

            if (!_hasActiveMinigame)
                SaveCurrentStateAsPrevious();

            _animalFocus = focusTarget;
            _animalTuningRuntime = BuildAnimalTuningFromDefinition(_animalFocus);

            InitAnimalOrbitAngleFromSource(_previousMinigameCamera != null ? _previousMinigameCamera : CurrentCamera);
            UpdateAnimalOrbitCameraPosition();

            var vcam = animalProfile.virtualCamera;
            vcam.Priority = activePriority + 5;

            if (animalProfile.fov > 0f)
                vcam.Lens.FieldOfView = animalProfile.fov;

            _currentMinigameProfile = animalProfile;
            _hasActiveMinigame = true;
            _isAnimalOrbitActive = true;

            // bật focus alpha khi vào animal camera
            if (focusAlphaFader != null)
                focusAlphaFader.Activate(_animalFocus);

            InternalSwitch(CurrentCamera, vcam);
            PauseWorldForMinigame(focusTarget);
        }

        public void ApplyAnimalMinigameProfile() => ApplyAnimalMinigameProfile(null);

        // thoát minigame, trả camera + world về normal
        public void RestoreMinigameCamera()
        {
            if (!_hasActiveMinigame && !_worldPaused) return;

            // tắt focus alpha trước để tránh kẹt mờ
            if (focusAlphaFader != null)
                focusAlphaFader.Deactivate();

            // 1. resume world trước
            ResumeWorldFromMinigame();

            // 2. hạ priority của camera minigame
            if (_currentMinigameProfile != null && _currentMinigameProfile.virtualCamera != null)
            {
                _currentMinigameProfile.virtualCamera.Priority = inactivePriority;
            }

            // 3. nếu có camera trước đó (ví dụ TPS cam) thì switch lại đúng chuẩn
            if (_hasPreviousMinigameCamera && _previousMinigameCamera != null)
            {
                // restore FOV cũ
                _previousMinigameCamera.Lens.FieldOfView = _previousMinigameFov;

                // dùng InternalSwitch để:
                // - set priority
                // - update CurrentCamera
                // - bắn OnCameraChanged cho PlayerThirdPersonController
                var oldCam = CurrentCamera;
                InternalSwitch(oldCam, _previousMinigameCamera);

                _previousMinigameCamera = null;
                _hasPreviousMinigameCamera = false;
            }
            else
            {
                // nếu không có previous thì fallback về default + vẫn dùng SwitchCamera (có event)
                if (defaultCamera != null)
                    SwitchCamera(defaultCamera);
            }

            _currentMinigameProfile = null;
            _hasActiveMinigame = false;

            _isAnimalOrbitActive = false;
            _animalFocus = null;
            _animalTuningRuntime = null;
        }

        // helper apply profile generic cho minigame
        private void ApplyMinigameProfile(MinigameCameraProfile profile, Transform focusTarget)
        {
            if (profile == null || profile.virtualCamera == null) return;

            if (!_hasActiveMinigame)
                SaveCurrentStateAsPrevious();

            SetupMinigameCameraTarget(profile, focusTarget);

            var vcam = profile.virtualCamera;
            vcam.Priority = activePriority + 5;
            if (profile.fov > 0f)
                vcam.Lens.FieldOfView = profile.fov;

            _currentMinigameProfile = profile;
            _hasActiveMinigame = true;

            InternalSwitch(CurrentCamera, vcam);
            PauseWorldForMinigame(focusTarget);
        }

        // lưu state camera hiện tại để lúc restore không bị drift
        private void SaveCurrentStateAsPrevious()
        {
            _previousMinigameCamera = CurrentCamera != null ? CurrentCamera : FindCurrentActiveCinemachine();

            if (_previousMinigameCamera != null)
            {
                _previousMinigameFov = _previousMinigameCamera.Lens.FieldOfView;
                _hasPreviousMinigameCamera = true;
            }
        }

        // set Follow/LookAt cho camera minigame
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
                // nếu không có target truyền vào thì xài default trong profile
                Transform defaultTarget = profile.follow != null ? profile.follow : transform;
                vcam.Follow = defaultTarget;
                vcam.LookAt = defaultTarget;
            }
        }

        // tính góc orbit dựa trên vị trí camera hiện tại
        private void InitAnimalOrbitAngleFromSource(CinemachineCamera sourceCam)
        {
            if (sourceCam == null || _animalFocus == null)
            {
                _animalOrbitAngle = 180f;
                return;
            }

            Vector3 toCam = sourceCam.transform.position - _animalFocus.position;
            toCam.y = 0f;

            if (toCam.sqrMagnitude < 0.0001f)
                _animalOrbitAngle = 180f;
            else
                _animalOrbitAngle = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;
        }

        private void InitAnimalOrbitAngleFromCurrentCamera()
        {
            InitAnimalOrbitAngleFromSource(CurrentCamera);
        }

        // cập nhật vị trí camera orbit quanh animal
        // - orbit quanh pivot chân cho ổn định
        // - nhưng LookAt lên cao để nhìn vào "đầu"
        // - tránh Cinemachine LookAt đánh nhau với transform.LookAt
        private void UpdateAnimalOrbitCameraPosition()
        {
            if (_animalFocus == null ||
                animalProfile == null ||
                animalProfile.virtualCamera == null) return;

            var cam = animalProfile.virtualCamera;

            float orbitDist = (_animalTuningRuntime != null && _animalTuningRuntime.orbitDistance > 0f)
                ? _animalTuningRuntime.orbitDistance
                : animalOrbitDistance;

            float orbitH = (_animalTuningRuntime != null && _animalTuningRuntime.orbitHeight > 0f)
                ? _animalTuningRuntime.orbitHeight
                : animalOrbitHeight;

            float lookH = (_animalTuningRuntime != null && _animalTuningRuntime.lookAtHeight > 0f)
                ? _animalTuningRuntime.lookAtHeight
                : animalLookAtHeight;

            float rad = _animalOrbitAngle * Mathf.Deg2Rad;

            Vector3 footPivot = _animalFocus.position;
            Vector3 lookPoint = footPivot + Vector3.up * lookH;

            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitDist;
            Vector3 pos = footPivot + offset + Vector3.up * orbitH;

            cam.transform.position = pos;

            // orbit mode: ko follow
            cam.Follow = null;

            // để 1 nơi aim thôi
            cam.LookAt = null;
            cam.transform.LookAt(lookPoint);
        }

        // ===== Option 1: lấy tuning từ AnimalDefinition =====
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

            var def = TryFindAnimalDefinitionOnAnimal(animalRoot);
            if (def == null) return tuning;

            // nếu field trong definition để 0 thì coi như "ko override"
            if (def.cameraOrbitDistance > 0f) tuning.orbitDistance = def.cameraOrbitDistance;
            if (def.cameraOrbitHeight > 0f) tuning.orbitHeight = def.cameraOrbitHeight;
            if (def.cameraLookAtHeight > 0f) tuning.lookAtHeight = def.cameraLookAtHeight;
            if (def.cameraOrbitRotateSpeed > 0f) tuning.rotateSpeed = def.cameraOrbitRotateSpeed;

            return tuning;
        }

        private AnimalDefinition TryFindAnimalDefinitionOnAnimal(Transform animalRoot)
        {
            // scan MonoBehaviour quanh animal, tìm field/property có chữ "definition"
            // rồi check đúng type AnimalDefinition
            var comps = animalRoot.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;

                var type = c.GetType();

                // fields
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    var fi = fields[f];
                    if (fi == null) continue;
                    if (!fi.Name.ToLowerInvariant().Contains("definition")) continue;

                    object val = fi.GetValue(c);
                    if (val is AnimalDefinition ad) return ad;
                }

                // properties
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int p = 0; p < props.Length; p++)
                {
                    var pi = props[p];
                    if (pi == null) continue;
                    if (!pi.CanRead) continue;
                    if (!pi.Name.ToLowerInvariant().Contains("definition")) continue;

                    try
                    {
                        object val = pi.GetValue(c, null);
                        if (val is AnimalDefinition ad) return ad;
                    }
                    catch
                    {
                        // ignore getter crash
                    }
                }
            }

            return null;
        }

        // tìm camera Cinemachine đang active nhất trong scene
        private CinemachineCamera FindCurrentActiveCinemachine()
        {
            var all = UnityEngine.Object.FindObjectsOfType<CinemachineCamera>(true);
            CinemachineCamera best = null;
            int bestPriority = int.MinValue;

            foreach (var cam in all)
            {
                if (cam &&
                    cam.isActiveAndEnabled &&
                    cam.Priority > bestPriority)
                {
                    bestPriority = cam.Priority;
                    best = cam;
                }
            }

            return best;
        }

        // pause world khi chơi minigame
        // - disable hầu hết MonoBehaviour
        // - chừa lại camera, audio, UI, minigame
        private void PauseWorldForMinigame(Transform minigameRoot)
        {
            if (_worldPaused) return;

            _pausedBehaviours.Clear();

            var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
            foreach (var b in allBehaviours)
            {
                if (b == null || !b.enabled) continue;
                if (IsAllowedDuringMinigame(b, minigameRoot)) continue;

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
                if (b != null) b.enabled = true;
            }

            _pausedBehaviours.Clear();
            _worldPaused = false;
        }

        // note quan trọng
        // - hàm này bảo vệ mấy component Cinemachine, Audio, UI không bị tắt oan
        // - thêm gì liên quan minigame thì check ở đây
        private bool IsAllowedDuringMinigame(Behaviour behaviour, Transform minigameRoot)
        {
            if (behaviour == null) return true;

            if (behaviour is CameraManager) return true;
            if (behaviour is FocusAlphaFader) return true;

            if (behaviour is CinemachineBrain) return true;
            if (behaviour is CinemachineCamera) return true;
            if (behaviour is AudioManager) return true;

            // bảo vệ các component con (Composer, Transposer...) trên cùng object Camera
            if (behaviour.GetComponent<CinemachineCamera>() != null) return true;

            if (behaviour.GetComponent<IMinigame>() != null) return true;

            // giữ lại các script nằm trong cây minigame
            if (minigameRoot != null && behaviour.transform.IsChildOf(minigameRoot)) return true;

            // giữ lại UI + EventSystem
            if (behaviour.GetComponentInParent<Canvas>() != null) return true;
            if (behaviour.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) return true;

            return false;
        }

        // fade blink nhanh khi đổi camera
        private IEnumerator FadeBlink()
        {
            if (fadeCanvas == null) yield break;

            fadeCanvas.gameObject.SetActive(true);

            yield return FadeTo(1f, fadeDuration);
            yield return FadeTo(0f, fadeDuration);

            fadeCanvas.gameObject.SetActive(false);
            _fadeRoutine = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (fadeCanvas == null) yield break;

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
