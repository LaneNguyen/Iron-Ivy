using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;
using IronIvy.Core;
using IronIvy.UI;
using IronIvy.Gameplay.Rhythm;

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

        [Header("Highlight (Unity Toon Shader)")]
        [SerializeField] private AnimalHighlightController highlightController;

        [Header("Minigame Integration")]
        [SerializeField] private bool enableRhythmMinigame = false;
        [SerializeField] private ClickAnimalRhythmMinigame animalMinigame;
        [SerializeField] private MinigameInteractionPanel interactionPanel;
        [SerializeField] private bool oneShotMinigame = false;
        

        [Header("Animator params")]
        public string speedParam = "speed";
        public string idleStateName = "idle";
        public string eatTrigger = "eat";
        public string jumpTrigger = "jump";

        private bool _hasPlayedMinigame;

        // zone con thực tế (spawn)
        public AnimalSpawnZone CurrentZone { get; private set; }

        // zone root (group), nếu không có thì chính là CurrentZone
        public AnimalSpawnZone RootZone { get; private set; }

        public AnimalDefinition Definition => definition;
        public AnimalVisibilityController Visibility => visibility;

        private Vector3 _anchorPosition;
        private Coroutine _wanderRoutine;
        private int _speedParamHash = -1;

        // player ref
        private Transform _player;

        // ambient & curious vars
        private bool _hasAmbientConfig;
        private bool _playerIsNearForAmbient;
        private float _ambientNextTime;
        private float _ambientSoundRadiusSqr;
        private AnimalState _state = AnimalState.Wandering;
        private bool _isCurious;
        private float _curiousRadiusSqr;
        private float _nextCuriousCheckTime;
        private float _curiousEndTime;
        private bool _playerInRangeForMinigame;
        private float _interactionRadiusSqr;

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

            // auto lấy highlight controller nếu quên gán
            if (highlightController == null)
                highlightController = GetComponentInChildren<AnimalHighlightController>();
        }

        private void OnEnable()
        {
            // Case thả tay prefab vào scene
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
                SetupMinigameRefs();

                _state = AnimalState.Wandering;
                _wanderRoutine = StartCoroutine(WanderRoutine());
            }
            else
            {
                // Case spawn qua AnimalManager
                SetupAmbientState();
                SetupCuriousState();
                SetupMinigameRefs();
            }

            // đảm bảo vào lại scene thì không bị sáng outline
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

            if (interactionPanel != null)
                interactionPanel.HideIfCurrentAnimal(this);

            // tắt highlight khi disable
            SetHighlighted(false);
        }

        private void Update()
        {
            // update speed param cho anim
            if (animator != null && agent != null && _speedParamHash != -1)
                animator.SetFloat(_speedParamHash, agent.velocity.magnitude);

            HandleAmbientSound();
            // HandleMinigameProximity();   // check khoảng cách + bật/tắt outline + panel
            HandleCuriousBehaviour();
        }

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


            SetupAmbientState();
            SetupCuriousState();
            SetupMinigameRefs();

            _state = AnimalState.Wandering;
            _wanderRoutine = StartCoroutine(WanderRoutine());

            // khi spawn mới thì tắt highlight
            SetHighlighted(false);
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

            // tắt highlight khi despawn
            SetHighlighted(false);
        }

        // Public API cho highlight (Toon shader)

        public void SetHighlighted(bool on)
        {
            if (highlightController != null)
                highlightController.SetHighlight(on);
        }

        // Feeding Logic (sync với InventoryManager / FoodItem)

        public bool TryFeed(FoodItem food)
        {
            if (food == null)
                return false;

            if (!InventoryManager.HasInstance)
            {
                Debug.LogWarning("[AnimalController] Missing InventoryManager!");
                return false;
            }

            // Consume 1 unit, InventoryManager.Consume tự check số lượng
            bool consumed = InventoryManager.Instance.Consume(food, 1);

            if (consumed)
            {
                // play anim ăn
                if (animator != null && !string.IsNullOrEmpty(eatTrigger))
                    animator.SetTrigger(eatTrigger);

                // TODO: sau này có hệ thống trust/mood thì hook vào đây
                Debug.Log($"[AnimalController] Feed success: {food.displayName}");

                // TODO: spawn VFX trái tim / particle nếu cần

                return true;
            }
            else
            {
                Debug.Log("[AnimalController] Not enough food in inventory!");
                // TODO: show UI feedback "Need more food"
                return false;
            }
        }

        // Setup helpers

        private void SetupAnimatorParamHashes()
        {
            if (animator == null)
                return;

            // tìm param speed nếu chưa set, hơi thủ công tí cho hợp vibe newbie
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
            if (agent == null || definition == null)
                return;

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

            if (definition == null ||
                definition.ambientClips == null ||
                definition.ambientClips.Length == 0 ||
                definition.ambientSoundRadius <= 0f)
                return;

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

            if (definition == null ||
                definition.curiousRadius <= 0f ||
                definition.curiousChancePerCheck <= 0f ||
                definition.curiousCheckInterval <= 0f)
                return;

            _curiousRadiusSqr = definition.curiousRadius * definition.curiousRadius;
            _nextCuriousCheckTime = Time.time + Random.Range(0.5f, definition.curiousCheckInterval);
        }

        private void SetupMinigameRefs()
        {
            if (!enableRhythmMinigame)
                return;



            if (interactionPanel == null)
            {
                interactionPanel = MinigameInteractionPanel.Instance ??
                                   GameObject.FindObjectOfType<MinigameInteractionPanel>(true);
            }

            if (animalMinigame == null)
            {
                animalMinigame = GameObject.FindObjectOfType<ClickAnimalRhythmMinigame>(true);
            }
        }

        // Wander & Ambient

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
            if (animator == null)
                return;

            int roll = Random.Range(0, 3);
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

        private void HandleAmbientSound()
        {
            if (!_hasAmbientConfig || _player == null || definition == null)
                return;

            Vector3 diff = _player.position - transform.position;
            float sqrDist = diff.sqrMagnitude;
            bool isNear = sqrDist <= _ambientSoundRadiusSqr;

            if (!isNear)
            {
                _playerIsNearForAmbient = false;
                return;
            }

            if (!_playerIsNearForAmbient)
            {
                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
                _playerIsNearForAmbient = true;
            }

            if (Time.time < _ambientNextTime)
                return;

            PlayAmbientClip();

            {
                float min = Mathf.Max(0.1f, definition.ambientMinInterval);
                float max = Mathf.Max(min, definition.ambientMaxInterval);
                _ambientNextTime = Time.time + Random.Range(min, max);
            }
        }

        private void PlayAmbientClip()
        {
            if (definition == null || definition.ambientClips == null || definition.ambientClips.Length == 0)
                return;

            var clips = definition.ambientClips;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip && AudioManager.HasInstance)
                AudioManager.Instance.PlaySEAtPosition(clip, transform.position);
        }

        // Curious

        private void HandleCuriousBehaviour()
        {
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
            if (_isCurious)
                return;

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

        public void OnInteractPressed()
        {
            if (!enableRhythmMinigame) return;
            if (_hasPlayedMinigame && oneShotMinigame) return;

            // Tìm panel trong scene ngay tại thời điểm bấm nút
            var panel = FindObjectOfType<MinigameInteractionPanel>(true); // true để tìm cả object đang ẩn

            if (panel != null)
            {
                // Fallback tìm minigame system nếu chưa gán
                if (animalMinigame == null) animalMinigame = FindObjectOfType<ClickAnimalRhythmMinigame>();
                
                panel.ShowForAnimal(this, animalMinigame);
            }
            else
            {
                Debug.LogWarning("Không tìm thấy MinigameInteractionPanel trong Scene");
            }
        }

        // Hàm này gắn vào Event "On Toggle Highlight" của InteractionTrigger
        public void SetHighlightState(bool state)
        {
            if (_hasPlayedMinigame && oneShotMinigame) 
            {
                if(highlightController) highlightController.SetHighlight(false);
                return;
            }

            if (highlightController) highlightController.SetHighlight(state);
        }

        // được minigame gọi sau khi player confirm chơi xong 1 lần
        public void MarkMinigamePlayed()
        {
            _hasPlayedMinigame = true;

            if (oneShotMinigame)
            {
                // one-shot: tắt highlight luôn, sau đó đợi RewardPanel quyết định despawn
                SetHighlighted(false);
            }
        }

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

            // despawn -> OnDisable cũng sẽ tắt highlight
            if (AnimalManager.HasInstance)
                AnimalManager.Instance.DespawnAnimalWithFade(this);
            else
                gameObject.SetActive(false);
        }
    }
}
