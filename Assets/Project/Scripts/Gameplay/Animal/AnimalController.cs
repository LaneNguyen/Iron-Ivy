using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;
using IronIvy.Core;
using IronIvy.UI;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Gameplay.Animals
{
    // controller chính cho animal
    // - wander trong zone
    // - ambient sound khi player lại gần
    // - curious nhìn player 1 lúc
    // - optional: hook vào animal rhythm minigame
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

        // zone con thực tế (spawn)
        public AnimalSpawnZone CurrentZone { get; private set; }

        // zone root (group), nếu không có thì chính là CurrentZone
        public AnimalSpawnZone RootZone { get; private set; }

        public AnimalDefinition Definition => definition;
        public AnimalVisibilityController Visibility => visibility;

        private Vector3 _anchorPosition;
        private Coroutine _wanderRoutine;
        private int _speedParamHash = -1;

        // player ref dùng cho ambient sound, curious, minigame
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

        // --------------------------------------------------
        // Minigame (Animal Rhythm)
        // --------------------------------------------------

        [Header("Minigame")]
        [SerializeField] private bool enableRhythmMinigame = false;

        [SerializeField] private MinigameInteractionPanel interactionPanel;
        [SerializeField] private ClickAnimalRhythmMinigame animalMinigame;

        [Header("Interaction / Minigame")]
        [SerializeField] private float interactionRadius = 3f;
        [SerializeField] private string playerTag = "Player";

        [SerializeField] private bool oneShotMinigame = false;

        private bool _hasPlayedMinigame;
        private bool _playerInRangeForMinigame;

        private float _interactionRadiusSqr;

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

        private void OnEnable()
        {
            // trường hợp drop thú vào scene test tay, không đi qua AnimalManager
            if (definition != null && CurrentZone == null && RootZone == null)
            {
                _anchorPosition = transform.position;
                SetupAgentFromDefinition();

                if (visibility != null)
                    visibility.ResetFadeImmediate();

                if (_wanderRoutine != null)
                    StopCoroutine(_wanderRoutine);

                ResolvePlayerTransform();
                SetupAmbientState();
                SetupCuriousState();
                SetupMinigameRefs();

                _state = AnimalState.Wandering;
                _wanderRoutine = StartCoroutine(WanderRoutine());
            }
            else
            {
                // nếu spawn qua manager thì Init đã lo phần setup chính
                ResolvePlayerTransform();
                SetupAmbientState();
                SetupCuriousState();
                SetupMinigameRefs();
            }
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

            // neu animal bi disable thi nho tat luon panel interaction neu dang refer den no
            if (interactionPanel != null)
            {
                interactionPanel.HideIfCurrentAnimal(this);
            }
        }

        private void Update()
        {
            // update speed param cho anim
            if (animator != null && agent != null && _speedParamHash != -1)
            {
                animator.SetFloat(_speedParamHash, agent.velocity.magnitude);
            }

            // ambient sound
            HandleAmbientSound();

            // minigame proximity (ở đây thay cho OnTrigger)
            HandleMinigameProximity();

            // curious nhìn player
            HandleCuriousBehaviour();
        }

        // gọi bởi AnimalManager mỗi lần spawn
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

            ResolvePlayerTransform();
            SetupAmbientState();
            SetupCuriousState();
            SetupMinigameRefs();

            _state = AnimalState.Wandering;
            _wanderRoutine = StartCoroutine(WanderRoutine());
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
        }

        // --------------------------------------------------
        // Setup helpers
        // --------------------------------------------------

        private void SetupAnimatorParamHashes()
        {
            if (animator == null) return;

            // auto tìm param speed nếu để trống
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

        private void ResolvePlayerTransform()
        {
            // nếu có AnimalManager thì ưu tiên playerRef trên đó
            if (AnimalManager.HasInstance && AnimalManager.Instance.playerTransform != null)
            {
                _player = AnimalManager.Instance.playerTransform;
                return;
            }

            // fallback test tay, tìm theo tag
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go != null)
                    _player = go.transform;
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
            _nextCuriousCheckTime = Time.time + Random.Range(0.5f, definition.curiousCheckInterval);
        }

        private void SetupMinigameRefs()
        {
            if (!enableRhythmMinigame) return;

            _interactionRadiusSqr = interactionRadius * interactionRadius;

            // auto find interaction panel
            if (interactionPanel == null)
                interactionPanel = MinigameInteractionPanel.Instance ??
                                   GameObject.FindObjectOfType<MinigameInteractionPanel>(true);

            // auto find click animal minigame controller trong scene
            if (animalMinigame == null)
                animalMinigame = GameObject.FindObjectOfType<ClickAnimalRhythmMinigame>(true);
        }

        // --------------------------------------------------
        // Wander behaviour
        // --------------------------------------------------

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
                        agent.SetDestination(target);
                }

                // đợi tới khi tới nơi hoặc path pending xong
                while (agent != null && agent.isOnNavMesh &&
                       (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f))
                {
                    yield return null;
                }

                // tới nơi thì idle anim random
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

        // --------------------------------------------------
        // Ambient sound behaviour
        // --------------------------------------------------

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
                // player ra khỏi vùng âm thanh, reset flag
                _playerIsNearForAmbient = false;
                return;
            }

            if (!_playerIsNearForAmbient)
            {
                // lần đầu player vào vùng, random thời gian kêu
                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
                _playerIsNearForAmbient = true;
            }

            if (Time.time < _ambientNextTime)
                return;

            // tới giờ kêu 1 phát
            PlayAmbientClip();

            // schedule lần sau
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

            // gọi qua AudioManager để giữ setting volume, mute, v.v.
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlaySEAtPosition(clip, transform.position);
        }

        // --------------------------------------------------
        // Curious look at player
        // --------------------------------------------------

        private void HandleCuriousBehaviour()
        {
            if (definition == null) return;
            if (_player == null) return;
            if (_curiousRadiusSqr <= 0f) return;
            if (definition.curiousChancePerCheck <= 0f) return;

            if (_isCurious)
            {
                // đang curious thì quay mặt dần về phía player
                Vector3 dir = _player.position - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        Time.deltaTime * 3f
                    );
                }

                if (Time.time >= _curiousEndTime)
                    EndCurious();

                return;
            }

            // chỉ check theo interval, tránh random mỗi frame
            if (Time.time < _nextCuriousCheckTime)
                return;

            float interval = Mathf.Max(0.5f, definition.curiousCheckInterval);
            _nextCuriousCheckTime = Time.time + interval;

            // quá xa thì bỏ qua
            Vector3 diffCur = _player.position - transform.position;
            float sqrDistCur = diffCur.sqrMagnitude;
            if (sqrDistCur > _curiousRadiusSqr)
                return;

            // random xác suất nhỏ
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

            // random thời gian đứng nhìn player
            float min = Mathf.Max(0.5f, definition.curiousMinDuration);
            float max = Mathf.Max(min, definition.curiousMaxDuration);
            _curiousEndTime = Time.time + Random.Range(min, max);

            if (animator != null && !string.IsNullOrEmpty(definition.curiousAnimTrigger))
            {
                animator.SetTrigger(definition.curiousAnimTrigger);
            }
        }

        private void EndCurious()
        {
            _isCurious = false;
            _state = AnimalState.Wandering;

            // hết curious thì quay lại wander
            if (_wanderRoutine == null && definition != null && agent != null)
                _wanderRoutine = StartCoroutine(WanderRoutine());
        }

        // --------------------------------------------------
        // Minigame: Animal Rhythm proximity
        // --------------------------------------------------

        private void HandleMinigameProximity()
        {
            if (!enableRhythmMinigame) return;
            if (_hasPlayedMinigame && oneShotMinigame) return;
            if (_player == null || interactionPanel == null || animalMinigame == null) return;

            float sqrDist = (_player.position - transform.position).sqrMagnitude;

            if (sqrDist <= _interactionRadiusSqr)
            {
                if (!_playerInRangeForMinigame)
                {
                    _playerInRangeForMinigame = true;
                    // show panel hỏi có chơi minigame không
                    interactionPanel.ShowForAnimal(this, animalMinigame);
                }
            }
            else
            {
                if (_playerInRangeForMinigame)
                {
                    _playerInRangeForMinigame = false;
                    interactionPanel.HideIfCurrentAnimal(this);
                }
            }
        }

        private void UpdateMinigameInteraction()
        {
            // neu khong bat minigame thi bo qua
            if (!enableRhythmMinigame || interactionPanel == null || animalMinigame == null)
                return;

            // neu one-shot va da choi roi thi thoi
            if (_hasPlayedMinigame && oneShotMinigame)
                return;

            // chua tim duoc player
            if (_player == null)
                return;

            // check khoang cach giua player va animal
            float distSqr = (transform.position - _player.position).sqrMagnitude;
            bool inRangeNow = distSqr <= _interactionRadiusSqr;

            if (inRangeNow != _playerInRangeForMinigame)
            {
                _playerInRangeForMinigame = inRangeNow;

                if (_playerInRangeForMinigame)
                {
                    // player vua buoc vao vung => show panel
                    interactionPanel.ShowForAnimal(this, animalMinigame);
                }
                else
                {
                    // player ra khoi vung => hide neu dang la con nay
                    interactionPanel.HideIfCurrentAnimal(this);
                }
            }
        }

        // hàm này sẽ được gọi từ minigame khi muốn khóa one-shot
        public void MarkMinigamePlayed()
        {
            _hasPlayedMinigame = true;
            // khong despawn o day nua, cho RewardPanel goi sau khi user bam OK
        }

        // public API de despawn sau minigame (goi tu RewardPanel)
        public void DespawnAfterMinigame()
{
    if (!oneShotMinigame)
    {
        if (interactionPanel != null)
            interactionPanel.HideIfCurrentAnimal(this);
        return;
    }

    if (interactionPanel != null)
        interactionPanel.HideIfCurrentAnimal(this);

    if (AnimalManager.HasInstance)
    {
        AnimalManager.Instance.DespawnAnimalWithFade(this);
    }
    else
    {
        // dev mode, khong co manager thi tat luon
        gameObject.SetActive(false);
    }
}

    }
}
