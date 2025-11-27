using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;

namespace IronIvy.Gameplay.Animals
{
    public class AnimalController : MonoBehaviour
    {
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

                _wanderRoutine = StartCoroutine(WanderRoutine());
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

        private void Update()
        {
            if (animator != null && agent != null && _speedParamHash != -1)
            {
                animator.SetFloat(_speedParamHash, agent.velocity.magnitude);
            }
        }

        private void OnDisable()
        {
            if (_wanderRoutine != null)
            {
                StopCoroutine(_wanderRoutine);
                _wanderRoutine = null;
            }
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
    }
}
