using System.Collections;
using IronIvy.Core;
using IronIvy.Data;
using UnityEngine;
using UnityEngine.AI;

namespace IronIvy.Gameplay.Animals
{
    public class AnimalController : MonoBehaviour
    {
        private enum AnimalState { Wandering, Curious }

        [Header("Refs")]
        [SerializeField] private AnimalDefinition definition;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimalVisibilityController visibility;

        [Header("Highlight (Unity Toon Shader)")]
        [SerializeField] private AnimalHighlightController highlightController;

        [Header("Minigame Integration")]
        [SerializeField] private bool enableRhythmMinigame = false;
        [SerializeField] private bool oneShotMinigame = false;

        [Header("Animator params")]
        public string speedParam = "speed";
        public string idleStateName = "idle";
        public string eatTrigger = "eat";
        public string jumpTrigger = "jump";

        [Header("Interaction Lock")]
        [SerializeField] private float faceTurnSpeed = 720f;
        [SerializeField] private bool faceOnlyYaw = true;

        [Header("Curious Suppress (Patch)")]
        [SerializeField] private float suppressCuriousAfterCancel = 1.25f;

        [Header("Minigame Despawn VFX (only)")]
        [SerializeField] private Vector3 minigameDespawnVfxOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float minigameDespawnVfxLifetime = 3f;

        [Header("Success VFX Rule")]
        [Range(0f, 1f)] [SerializeField] private float successVfxTrustThreshold = 0.75f;

        [Header("Debug")]
        [SerializeField] private bool logVfx = false;

        private bool _hasPlayedMinigame;

        // queue despawn until Reward Panel close
        private bool _queuedMinigameDespawn;
        private float _queuedMinigameTrust01;

        public AnimalSpawnZone CurrentZone { get; private set; }
        public AnimalSpawnZone RootZone { get; private set; }

        public AnimalDefinition Definition => definition;
        public AnimalVisibilityController Visibility => visibility;

        private Vector3 _anchorPosition;
        private Coroutine _wanderRoutine;
        private int _speedParamHash = -1;

        private Transform _player;

        // ambient vars
        private bool _hasAmbientConfig;
        private bool _playerIsNearForAmbient;
        private float _ambientNextTime;
        private float _ambientSoundRadiusSqr;

        // curious vars
        private AnimalState _state = AnimalState.Wandering;
        private bool _isCurious;
        private float _curiousRadiusSqr;
        private float _nextCuriousCheckTime;
        private float _curiousEndTime;

        private float _curiousSuppressedUntil;

        private bool _interactionLocked;
        private bool _agentWasEnabled;
        private bool _agentWasStopped;
        private float _agentSpeed;

        private Coroutine _faceRoutine;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            visibility = GetComponentInChildren<AnimalVisibilityController>();
            highlightController = GetComponentInChildren<AnimalHighlightController>();
        }

        private void Awake()
        {
            SetupAnimatorParamHashes();

            if (highlightController == null)
                highlightController = GetComponentInChildren<AnimalHighlightController>();

            CachePlayerRef();
        }

        private void CachePlayerRef()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            _player = playerGO != null ? playerGO.transform : null;
        }

        private void OnEnable()
        {
            if (_player == null) CachePlayerRef();

            // pooled object -> bật lại phải reset state cũ
            ResetSpawnRuntimeState();

            if (definition != null && CurrentZone == null && RootZone == null)
            {
                _anchorPosition = transform.position;
                SetupAgentFromDefinition();

                if (visibility != null)
                    visibility.ResetFadeImmediate();

                if (_wanderRoutine != null)
                    StopCoroutine(_wanderRoutine);

                SetupAmbientState();
                SetupCuriousState();

                _state = AnimalState.Wandering;
                _wanderRoutine = StartCoroutine(WanderRoutine());
            }
            else
            {
                SetupAmbientState();
                SetupCuriousState();
            }

            SetHighlighted(false);
        }

        private void OnDisable()
        {
            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }

            _isCurious = false;
            _state = AnimalState.Wandering;

            HideInteractionPanelSoft();
            SetHighlighted(false);
        }

        private void Update()
        {
            if (animator != null && agent != null && _speedParamHash != -1)
                animator.SetFloat(_speedParamHash, agent.velocity.magnitude);

            HandleAmbientSound();
            HandleCuriousBehaviour();
        }

        // =========================
        // IMPORTANT: restore old public APIs (UI depends on these)
        // =========================

        // UIManager needs this (feeding)
        public bool TryFeed(FoodItem food)
        {
            if (food == null) return false;

            if (animator != null && !string.IsNullOrEmpty(eatTrigger))
                animator.SetTrigger(eatTrigger);

            // nếu sau này em muốn consume food / buff trust thì xử lý ở đây
            return true;
        }

        // UI panel + trigger need this (cancel look + unlock)
        public void CancelLookAtPlayerNow()
        {
            // thả lock, stop face coroutine, resume wander
            SetInteractionLocked(false);
        }

        // =========================

        // reset all pooling-related flags
        private void ResetSpawnRuntimeState()
        {
            _queuedMinigameDespawn = false;
            _queuedMinigameTrust01 = 0f;

            // reset minigame "already played" for new spawned instance
            _hasPlayedMinigame = false;

            // clear locks that might remain from reward flow
            _interactionLocked = false;
            StopFacingPlayer();
            UnlockAgent();

            _curiousSuppressedUntil = 0f;
        }

        public void Init(AnimalDefinition def, AnimalSpawnZone zone, AnimalSpawnZone rootZone)
        {
            if (def != null)
                definition = def;

            CurrentZone = zone;
            RootZone = rootZone != null ? rootZone : zone;
            _anchorPosition = (zone != null) ? zone.transform.position : transform.position;

            // pooled spawn reset here too
            ResetSpawnRuntimeState();

            SetupAgentFromDefinition();

            if (visibility != null)
                visibility.ResetFadeImmediate();

            if (_wanderRoutine != null)
                StopCoroutine(_wanderRoutine);

            SetupAmbientState();
            SetupCuriousState();

            _state = AnimalState.Wandering;
            _wanderRoutine = StartCoroutine(WanderRoutine());

            SetHighlighted(false);

            if (_player == null) CachePlayerRef();
        }

        public void OnDespawn()
        {
            if (agent != null && agent.isOnNavMesh)
                agent.ResetPath();

            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }

            _isCurious = false;
            _state = AnimalState.Wandering;

            HideInteractionPanelSoft();
            SetHighlighted(false);

            _queuedMinigameDespawn = false;
            _queuedMinigameTrust01 = 0f;

            _interactionLocked = false;
            StopFacingPlayer();
            UnlockAgent();
        }

        public void SetHighlighted(bool on)
        {
            if (highlightController != null)
                highlightController.SetHighlight(on);
        }

        public void OnInteractPressed()
        {
            if (!enableRhythmMinigame) return;
            if (_hasPlayedMinigame && oneShotMinigame) return;

            if (!UIManager.HasInstance)
            {
                Debug.LogWarning("[AnimalController] UIManager missing, cannot open interaction popup.");
                return;
            }

            UIManager.Instance.ShowAnimalInteraction(this);
        }

        public void SetHighlightState(bool state)
        {
            if (_hasPlayedMinigame && oneShotMinigame)
            {
                SetHighlighted(false);
                return;
            }

            SetHighlighted(state);
        }

        public void MarkMinigamePlayed()
        {
            _hasPlayedMinigame = true;

            if (oneShotMinigame)
                SetHighlighted(false);
        }

        // Queue despawn at minigame end (reward panel will execute)
        public void QueueDespawnAfterMinigame(float trust01)
        {
            _queuedMinigameDespawn = true;
            _queuedMinigameTrust01 = Mathf.Clamp01(trust01);

            SetInteractionLocked(true);
            SetHighlighted(false);

            if (logVfx && definition != null)
                Debug.Log($"[AnimalController] QueueDespawnAfterMinigame: {definition.displayName} trust={_queuedMinigameTrust01:0.00} oneShot={oneShotMinigame}");
        }

        public void ExecuteQueuedDespawnAfterMinigame()
        {
            if (!_queuedMinigameDespawn)
            {
                SetInteractionLocked(false);
                return;
            }

            float trust = _queuedMinigameTrust01;
            _queuedMinigameDespawn = false;

            if (!oneShotMinigame)
            {
                SetInteractionLocked(false);
                return;
            }

            HideInteractionPanelSoft();
            DoDespawnAfterMinigame(trust);
        }

        // backward compatible
        public void DespawnAfterMinigame() => DespawnAfterMinigame(-1f);

        public void DespawnAfterMinigame(float trust01)
        {
            QueueDespawnAfterMinigame(trust01);
            ExecuteQueuedDespawnAfterMinigame();
        }

        private void DoDespawnAfterMinigame(float trust01)
        {
            PlayMinigameDespawnVfx(trust01);

            if (AnimalManager.HasInstance)
                AnimalManager.Instance.DespawnAnimalWithFade(this);
            else
                gameObject.SetActive(false);
        }

        private void PlayMinigameDespawnVfx(float trust01)
        {
            if (definition == null) return;

            bool isSuccess = (trust01 >= 0.99f) || (trust01 >= successVfxTrustThreshold);

            GameObject prefab = null;
            if (isSuccess && definition.successVFX != null) prefab = definition.successVFX;
            else prefab = definition.despawnVfxPrefab;

            if (prefab == null)
                return;

            Vector3 pos = transform.position + minigameDespawnVfxOffset;
            var vfx = Instantiate(prefab, pos, Quaternion.identity);

            if (minigameDespawnVfxLifetime > 0f)
                Destroy(vfx, minigameDespawnVfxLifetime);
        }

        private void HideInteractionPanelSoft()
        {
            if (!UIManager.HasInstance) return;

            var p = UIManager.Instance.popup != null ? UIManager.Instance.popup.animalInteractionPanel : null;
            if (p != null && p.gameObject.activeInHierarchy)
                p.Hide();
        }

        private void SetupAnimatorParamHashes()
        {
            if (animator == null) return;

            if (string.IsNullOrEmpty(speedParam))
            {
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float && p.name == "Speed")
                    {
                        speedParam = p.name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(speedParam))
                {
                    foreach (var p in animator.parameters)
                    {
                        if (p.type == AnimatorControllerParameterType.Float)
                        {
                            speedParam = p.name;
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(speedParam))
                _speedParamHash = Animator.StringToHash(speedParam);
        }

        private void SetupAgentFromDefinition()
        {
            if (agent == null || definition == null) return;

            agent.speed = Mathf.Max(0.1f, definition.walkSpeed);
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.25f;
        }

        private void SetupAmbientState()
        {
            _hasAmbientConfig = false;
            _playerIsNearForAmbient = false;
            _ambientNextTime = 0f;
            _ambientSoundRadiusSqr = 0f;

            if (definition == null || definition.ambientSoundRadius <= 0f)
                return;

            bool hasAnyClip =
                (definition.ambientClips != null && definition.ambientClips.Length > 0) ||
                (definition.loopSfx != null);

            if (!hasAnyClip)
                return;

            _hasAmbientConfig = true;
            _ambientSoundRadiusSqr = definition.ambientSoundRadius * definition.ambientSoundRadius;
        }

        private void HandleAmbientSound()
        {
            if (!_hasAmbientConfig || _player == null || definition == null)
                return;

            Vector3 diff = _player.position - transform.position;
            bool isNear = diff.sqrMagnitude <= _ambientSoundRadiusSqr;

            if (!isNear)
            {
                _playerIsNearForAmbient = false;
                return;
            }

            if (!_playerIsNearForAmbient)
            {
                PlayAmbientClip();

                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
                _playerIsNearForAmbient = true;
                return;
            }

            if (Time.time < _ambientNextTime)
                return;

            PlayAmbientClip();

            float min2 = Mathf.Max(0.1f, definition.ambientMinInterval);
            float max2 = Mathf.Max(min2, definition.ambientMaxInterval);
            _ambientNextTime = Time.time + Random.Range(min2, max2);
        }

        private void PlayAmbientClip()
        {
            AudioClip clip = null;

            int ambientCount = (definition.ambientClips != null) ? definition.ambientClips.Length : 0;
            bool hasExtra = definition.loopSfx != null;

            int total = ambientCount + (hasExtra ? 1 : 0);
            if (total <= 0) return;

            int pick = Random.Range(0, total);

            if (pick < ambientCount)
                clip = definition.ambientClips[pick];
            else
                clip = definition.loopSfx;

            if (clip && AudioManager.HasInstance)
                AudioManager.Instance.PlaySEAtPosition(clip, transform.position);
        }

        private void SetupCuriousState()
        {
            _isCurious = false;
            _state = AnimalState.Wandering;
            _curiousRadiusSqr = 0f;
            _nextCuriousCheckTime = 0f;
            _curiousEndTime = 0f;

            if (definition == null ||
                definition.curiousRadius <= 0f ||
                definition.curiousChancePerCheck <= 0f ||
                definition.curiousCheckInterval <= 0f)
                return;

            _curiousRadiusSqr = definition.curiousRadius * definition.curiousRadius;
            _nextCuriousCheckTime = Time.time + Random.Range(0.5f, definition.curiousCheckInterval);
        }

        private void HandleCuriousBehaviour()
        {
            if (Time.time < _curiousSuppressedUntil)
                return;

            if (definition == null || _player == null || _curiousRadiusSqr <= 0f)
                return;

            if (_isCurious)
            {
                Vector3 dir = _player.position - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);
                }

                if (Time.time >= _curiousEndTime)
                    EndCurious();

                return;
            }

            if (Time.time < _nextCuriousCheckTime)
                return;

            float interval = Mathf.Max(0.5f, definition.curiousCheckInterval);
            _nextCuriousCheckTime = Time.time + interval;

            Vector3 diffCur = _player.position - transform.position;
            if (diffCur.sqrMagnitude > _curiousRadiusSqr)
                return;

            if (Random.value <= definition.curiousChancePerCheck)
                StartCurious();
        }

        private void StartCurious()
        {
            if (_isCurious) return;

            _isCurious = true;
            _state = AnimalState.Curious;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }

            float min = Mathf.Max(0.5f, definition.curiousMinDuration);
            float max = Mathf.Max(min, definition.curiousMaxDuration);
            _curiousEndTime = Time.time + Random.Range(min, max);

            if (animator != null && !string.IsNullOrEmpty(definition.curiousAnimTrigger))
                animator.SetTrigger(definition.curiousAnimTrigger);
        }

        private void EndCurious()
        {
            _isCurious = false;
            _state = AnimalState.Wandering;

            if (_wanderRoutine == null && definition != null && agent != null)
                _wanderRoutine = StartCoroutine(WanderRoutine());
        }

        private IEnumerator WanderRoutine()
        {
            while (true)
            {
                if (agent == null || definition == null)
                {
                    yield return null;
                    continue;
                }

                Vector3 target;
                if (TryGetRandomPoint(out target))
                {
                    if (agent.isOnNavMesh)
                        agent.SetDestination(target);
                }

                while (agent != null &&
                       agent.isOnNavMesh &&
                       (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f))
                {
                    yield return null;
                }

                PlayRandomIdleVariant();

                float wait = Random.Range(definition.minIdleTime, definition.maxIdleTime);
                yield return new WaitForSeconds(wait);
            }
        }

        private bool TryGetRandomPoint(out Vector3 result)
        {
            Vector3 center = _anchorPosition;
            float radius = Mathf.Max(0.1f, definition.wanderRadius);

            for (int i = 0; i < 5; i++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = center;
            return false;
        }

        private void PlayRandomIdleVariant()
        {
            if (animator == null) return;

            int roll = Random.Range(0, 3);
            if (roll == 1 && !string.IsNullOrEmpty(eatTrigger))
                animator.SetTrigger(eatTrigger);
            else if (roll == 2 && !string.IsNullOrEmpty(jumpTrigger))
                animator.SetTrigger(jumpTrigger);
            else if (!string.IsNullOrEmpty(idleStateName))
                animator.CrossFadeInFixedTime(idleStateName, 0.1f);
        }

        public void SetInteractionLocked(bool locked)
        {
            if (_interactionLocked == locked) return;
            _interactionLocked = locked;

            if (locked)
            {
                LockAgent();
                StartFacingPlayer();
                StopWanderIfAny();
            }
            else
            {
                StopFacingPlayer();
                UnlockAgent();

                _curiousSuppressedUntil = Mathf.Max(_curiousSuppressedUntil, Time.time + suppressCuriousAfterCancel);

                if (isActiveAndEnabled && _wanderRoutine == null)
                    _wanderRoutine = StartCoroutine(WanderRoutine());
            }
        }

        private void LockAgent()
        {
            if (agent == null) return;

            _agentWasEnabled = agent.enabled;
            if (!_agentWasEnabled) return;

            _agentWasStopped = agent.isStopped;
            _agentSpeed = agent.speed;

            agent.isStopped = true;
            agent.speed = 0f;

            if (agent.isOnNavMesh)
            {
                if (agent.hasPath) agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        private void UnlockAgent()
        {
            if (agent == null) return;
            if (!_agentWasEnabled) return;

            agent.speed = _agentSpeed <= 0f ? agent.speed : _agentSpeed;
            agent.isStopped = _agentWasStopped;
        }

        private void StartFacingPlayer()
        {
            if (_faceRoutine != null) StopCoroutine(_faceRoutine);
            _faceRoutine = StartCoroutine(FacePlayerLoop());
        }

        private void StopFacingPlayer()
        {
            if (_faceRoutine != null)
            {
                StopCoroutine(_faceRoutine);
                _faceRoutine = null;
            }
        }

        private IEnumerator FacePlayerLoop()
        {
            var player = _player;
            if (player == null)
            {
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                player = playerGO != null ? playerGO.transform : null;
                _player = player;
            }

            while (_interactionLocked)
            {
                if (player != null)
                {
                    Vector3 dir = player.position - transform.position;
                    if (faceOnlyYaw) dir.y = 0f;

                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, faceTurnSpeed * Time.deltaTime);
                    }
                }

                yield return null;
            }

            _faceRoutine = null;
        }

        private void StopWanderIfAny()
        {
            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }
        }
    }
}
