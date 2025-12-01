using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;
using IronIvy.Core;

namespace IronIvy.Gameplay.Animals
{
    public class AnimalController : MonoBehaviour
    {
        private enum AnimalState
        {
            Wandering,
            Curious
        }

        [Header("Refs")]
        [SerializeField] private AnimalDefinition definition;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimalVisibilityController visibility;

        [Header("Animator params")]
        public string speedParam = "speed";
        public string idleStateName = "idle";
        public string eatTrigger = "eat";
        public string jumpTrigger = "jump";

        public AnimalSpawnZone CurrentZone { get; private set; }  // zone con thuc te
        public AnimalSpawnZone RootZone { get; private set; }     // parent group (neu co)
        public AnimalDefinition Definition => definition;
        public AnimalVisibilityController Visibility => visibility;

        private Vector3 _anchorPosition;
        private Coroutine _wanderRoutine;
        private int _speedParamHash = -1;

        // player ref de dung cho sound + curious
        private Transform _player;

        // ambient sound state
        private bool _hasAmbientConfig;
        private bool _playerIsNearForAmbient;
        private float _ambientNextTime;
        private float _ambientSoundRadiusSqr;

        // curious state
        private AnimalState _state = AnimalState.Wandering;
        private bool _isCurious;
        private float _curiousRadiusSqr;
        private float _nextCuriousCheckTime;
        private float _curiousEndTime;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            visibility = GetComponentInChildren<AnimalVisibilityController>();
        }

        private void Awake()
        {
            SetupAnimatorParamHashes();
        }

        private void SetupAnimatorParamHashes()
        {
            if (animator == null) return;

            if (string.IsNullOrEmpty(speedParam))
            {
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Float &&
                        p.name == "Speed")
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
            {
                _speedParamHash = Animator.StringToHash(speedParam);
            }
        }

        private void OnEnable()
        {
            // truong hop drop thu vao scene test tay, khong di qua AnimalManager
            if (definition != null && CurrentZone == null && RootZone == null)
            {
                _anchorPosition = transform.position;
                SetupAgentFromDefinition();

                if (visibility != null)
                    visibility.ResetFadeImmediate();

                if (_wanderRoutine != null)
                    StopCoroutine(_wanderRoutine);

                // set up player + behaviour nho nho
                ResolvePlayerTransform();
                SetupAmbientState();
                SetupCuriousState();

                _state = AnimalState.Wandering;
                _wanderRoutine = StartCoroutine(WanderRoutine());
            }
            else
            {
                // neu spawn qua manager thi Init se lo phan setup
                ResolvePlayerTransform();
                SetupAmbientState();
                SetupCuriousState();
            }
        }

        // Init duoc goi boi AnimalManager moi lan spawn
        public void Init(AnimalDefinition def, AnimalSpawnZone zone, AnimalSpawnZone rootZone)
        {
            if (def != null)
                definition = def;

            CurrentZone = zone;
            RootZone = rootZone != null ? rootZone : zone;

            _anchorPosition = (zone != null) ? zone.transform.position : transform.position;

            SetupAgentFromDefinition();

            if (visibility != null)
                visibility.ResetFadeImmediate();

            if (_wanderRoutine != null)
                StopCoroutine(_wanderRoutine);

            // lay player tu AnimalManager hoac fallback tag
            ResolvePlayerTransform();
            SetupAmbientState();
            SetupCuriousState();

            _state = AnimalState.Wandering;
            _wanderRoutine = StartCoroutine(WanderRoutine());
        }

        private void SetupAgentFromDefinition()
        {
            if (agent == null || definition == null) return;

            agent.speed = Mathf.Max(0.1f, definition.walkSpeed);
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.25f;
        }

        private void ResolvePlayerTransform()
        {
            // co manager thi uu tien dung playerRef tren do
            if (AnimalManager.HasInstance && AnimalManager.Instance.playerTransform != null)
            {
                _player = AnimalManager.Instance.playerTransform;
                return;
            }

            // fallback truong hop test tay, tim theo tag
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null)
                {
                    _player = go.transform;
                }
            }
        }

        private void SetupAmbientState()
        {
            _hasAmbientConfig = false;
            _playerIsNearForAmbient = false;
            _ambientNextTime = 0f;
            _ambientSoundRadiusSqr = 0f;

            if (definition == null) return;
            if (definition.ambientClips == null || definition.ambientClips.Length == 0) return;
            if (definition.ambientSoundRadius <= 0f) return;

            _hasAmbientConfig = true;
            _ambientSoundRadiusSqr = definition.ambientSoundRadius * definition.ambientSoundRadius;

            // de nextTime = 0, luc player lai gan lan dau se set lai
        }

        private void SetupCuriousState()
        {
            _isCurious = false;
            _state = AnimalState.Wandering;
            _curiousRadiusSqr = 0f;
            _nextCuriousCheckTime = 0f;
            _curiousEndTime = 0f;

            if (definition == null) return;
            if (definition.curiousRadius <= 0f) return;
            if (definition.curiousChancePerCheck <= 0f) return;
            if (definition.curiousCheckInterval <= 0f) return;

            _curiousRadiusSqr = definition.curiousRadius * definition.curiousRadius;
            // check lan dau sau mot khoang nho cho random, tranh dong loat
            _nextCuriousCheckTime = Time.time + Random.Range(0.5f, definition.curiousCheckInterval);
        }

        private void Update()
        {
            if (animator != null && agent != null && _speedParamHash != -1)
            {
                animator.SetFloat(_speedParamHash, agent.velocity.magnitude);
            }

            // update sound ambient nho nho
            HandleAmbientSound();

            // update curious look at player
            HandleCuriousBehaviour();
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
        }

        public void OnDespawn()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }

            _isCurious = false;
            _state = AnimalState.Wandering;
        }

        private System.Collections.IEnumerator WanderRoutine()
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
                    {
                        agent.SetDestination(target);
                    }
                }

                while (agent != null && agent.isOnNavMesh &&
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

            int roll = Random.Range(0, 3); // 0 idle, 1 eat, 2 jump
            if (roll == 1 && !string.IsNullOrEmpty(eatTrigger))
            {
                animator.SetTrigger(eatTrigger);
            }
            else if (roll == 2 && !string.IsNullOrEmpty(jumpTrigger))
            {
                animator.SetTrigger(jumpTrigger);
            }
            else if (!string.IsNullOrEmpty(idleStateName))
            {
                animator.CrossFadeInFixedTime(idleStateName, 0.1f);
            }
        }

        // -----------------------------
        // Ambient sound behaviour
        // -----------------------------
        private void HandleAmbientSound()
        {
            if (!_hasAmbientConfig) return;
            if (_player == null) return;
            if (definition == null) return;

            Vector3 diff = _player.position - transform.position;
            float sqrDist = diff.sqrMagnitude;

            bool isNear = sqrDist <= _ambientSoundRadiusSqr;

            if (!isNear)
            {
                // player di xa khoi vung sound, stop schedule
                _playerIsNearForAmbient = false;
                return;
            }

            if (!_playerIsNearForAmbient)
            {
                // lan dau player lai gan, random thoi gian keu
                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
                _playerIsNearForAmbient = true;
            }

            if (Time.time < _ambientNextTime)
            {
                return;
            }

            // den gio keu 1 phat
            PlayAmbientClip();

            // schedule cho lan sau
            {
                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
            }
        }

        private void PlayAmbientClip()
        {
            if (definition == null) return;
            if (definition.ambientClips == null || definition.ambientClips.Length == 0) return;

            var clips = definition.ambientClips;
            int idx = Random.Range(0, clips.Length);
            AudioClip clip = clips[idx];

            if (clip == null) return;

            // goi qua audio manager de giu setting volume, mute, v.v.
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlaySEAtPosition(clip, transform.position);
            }
        }

        // -----------------------------
        // Curious look at player
        // -----------------------------
        private void HandleCuriousBehaviour()
        {
            if (definition == null) return;
            if (_player == null) return;
            if (_curiousRadiusSqr <= 0f) return;
            if (definition.curiousChancePerCheck <= 0f) return;

            if (_isCurious)
            {
                // dang dung nhin player thi xoay nhe nhe theo Y
                Vector3 dir = _player.position - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);
                }

                if (Time.time >= _curiousEndTime)
                {
                    EndCurious();
                }

                return;
            }

            // chi check curious theo interval, khong moi frame
            if (Time.time < _nextCuriousCheckTime)
            {
                return;
            }

            float interval = Mathf.Max(0.5f, definition.curiousCheckInterval);
            _nextCuriousCheckTime = Time.time + interval;

            // neu player qua xa thi bo qua
            Vector3 diff = _player.position - transform.position;
            float sqrDist = diff.sqrMagnitude;
            if (sqrDist > _curiousRadiusSqr)
            {
                return;
            }

            // random xac suat nho nho
            if (Random.value <= definition.curiousChancePerCheck)
            {
                StartCurious();
            }
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

            // random thoi gian dung nhin player
            float min = Mathf.Max(0.5f, definition.curiousMinDuration);
            float max = Mathf.Max(min, definition.curiousMaxDuration);
            _curiousEndTime = Time.time + Random.Range(min, max);

            // neu co trigger rieng cho curious thi goi
            if (animator != null && !string.IsNullOrEmpty(definition.curiousAnimTrigger))
            {
                animator.SetTrigger(definition.curiousAnimTrigger);
            }
            else
            {
                // khong co thi giu idle, de con thu dung nhin cung du
            }
        }

        private void EndCurious()
        {
            _isCurious = false;
            _state = AnimalState.Wandering;

            // sau khi nhin xong thi di dao lai
            if (_wanderRoutine == null && definition != null && agent != null)
            {
                _wanderRoutine = StartCoroutine(WanderRoutine());
            }
        }
    }
}
