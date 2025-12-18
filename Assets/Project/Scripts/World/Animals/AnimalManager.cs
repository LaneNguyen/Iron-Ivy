using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;
using IronIvy.Gameplay.Animals;

namespace IronIvy.Core
{
    public class AnimalManager : BaseManager<AnimalManager>
    {
        [Header("Legacy encounter (zone1/zone2)")]
        public List<AnimalDefinition> zone1Animals = new List<AnimalDefinition>();
        public List<AnimalDefinition> zone2Animals = new List<AnimalDefinition>();
        public AnimalDefinition Today { get; private set; }

        public void RerollTodayEncounter()
        {
            var list = (ZoneManager.HasInstance && ZoneManager.Instance.CurrentZone == Zone.Zone2)
                ? zone2Animals
                : zone1Animals;

            if (list != null && list.Count > 0)
                Today = list[Random.Range(0, list.Count)];
            else
                Today = null;
        }

        [System.Serializable]
        public class AnimalSpawnEntry
        {
            public AnimalDefinition definition;
            public float weight = 1f;
        }

        [System.Serializable]
        public class ZoneSpawnConfig
        {
            public AnimalSpawnZone zone;
            public int maxAnimalsInZone = 5;
            public AnimalSpawnEntry[] animals;
        }

        [Header("Definitions & Zones")]
        public AnimalDefinition[] allDefinitions;
        public List<ZoneSpawnConfig> zoneConfigs = new List<ZoneSpawnConfig>();

        [Header("Spawn settings (global)")]
        public int maxTotalAnimals = 40;
        public int initialPoolPerAnimal = 3;

        public Transform playerTransform;
        public float spawnCheckRadius = 30f;
        public float despawnRadius = 40f;
        public float spawnCheckInterval = 2f;

        [Header("Spawn sanity")]
        [Tooltip("Không spawn animal quá gần player (đỡ xuất hiện ngay dưới chân).")]
        public float spawnMinDistanceFromPlayer = 10f;

        [Tooltip("Số lần thử random điểm spawn trong zone để tìm điểm đủ xa player.")]
        public int spawnPickTries = 12;

        [Header("Despawn Fade Fallback (when no AnimalVisibilityController)")]
        public float fallbackFadeDuration = 0.35f;
        [Range(0.5f, 1f)] public float fallbackEndScale = 0.85f;

        private readonly Dictionary<AnimalDefinition, Queue<AnimalController>> _pools =
            new Dictionary<AnimalDefinition, Queue<AnimalController>>();

        private readonly Dictionary<AnimalDefinition, int> _activeCountPerDefinition =
            new Dictionary<AnimalDefinition, int>();

        private readonly List<AnimalController> _activeAnimals =
            new List<AnimalController>();

        private readonly HashSet<AnimalController> _despawning =
            new HashSet<AnimalController>();

        private float _checkTimer;
        private bool _initialized;

        private void OnEnable()
        {
            InitIfNeeded();

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnDayEnded += HandleDayEnded;
            }
        }

        private void Start()
        {
            InitIfNeeded();
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnDayEnded -= HandleDayEnded;
            }
        }

        private void InitIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            BuildPools();
        }

        private void BuildPools()
        {
            _pools.Clear();
            _activeCountPerDefinition.Clear();

            if (allDefinitions == null) return;

            foreach (var def in allDefinitions)
            {
                if (def == null) continue;

                _pools[def] = new Queue<Gameplay.Animals.AnimalController>();
                _activeCountPerDefinition[def] = 0;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (playerTransform == null) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;

            _checkTimer = spawnCheckInterval;
            UpdateZonesAroundPlayer();
        }

        private void UpdateZonesAroundPlayer()
        {
            if (zoneConfigs == null) return;

            foreach (var config in zoneConfigs)
            {
                if (config == null || config.zone == null) continue;

                float dist = config.zone.GetDistanceTo(playerTransform);

                if (dist <= spawnCheckRadius)
                {
                    TrySpawnInZone(config);
                }
                else if (dist >= despawnRadius)
                {
                    DespawnZone(config.zone);
                }
            }
        }

        private void TrySpawnInZone(ZoneSpawnConfig config)
        {
            var rootZone = config.zone;
            if (rootZone == null) return;

            if (rootZone.currentCount >= config.maxAnimalsInZone) return;
            if (_activeAnimals.Count >= maxTotalAnimals) return;

            var candidates = GetSpawnableEntries(config.animals);
            if (candidates.Count == 0) return;

            AnimalDefinition chosenDef = RollAnimalDefinition(candidates);
            if (chosenDef == null) return;

            AnimalSpawnZone spawnZone = rootZone.GetRandomConcreteZone();
            if (spawnZone == null) spawnZone = rootZone;

            // NEW: pick spawn pos that is not too close to player
            if (!TryPickSpawnPositionAvoidPlayer(spawnZone, out Vector3 spawnPos))
            {
                // không tìm được điểm hợp lệ -> skip lần này
                return;
            }

            var controller = GetFromPool(chosenDef, spawnPos, spawnZone, rootZone);
            if (controller == null) return;

            rootZone.currentCount++;

            int count;
            _activeCountPerDefinition.TryGetValue(chosenDef, out count);
            _activeCountPerDefinition[chosenDef] = count + 1;

            _activeAnimals.Add(controller);
        }

        private bool TryPickSpawnPositionAvoidPlayer(AnimalSpawnZone spawnZone, out Vector3 finalPos)
        {
            finalPos = spawnZone.transform.position;

            float minDist = Mathf.Max(0f, spawnMinDistanceFromPlayer);
            float minDistSqr = minDist * minDist;

            Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.positiveInfinity;

            for (int i = 0; i < Mathf.Max(1, spawnPickTries); i++)
            {
                Vector3 p = spawnZone.GetRandomPointInside();

                // if too close, try again
                if (playerTransform != null)
                {
                    Vector3 d = p - playerPos;
                    d.y = 0f;
                    if (d.sqrMagnitude < minDistSqr)
                        continue;
                }

                // snap to navmesh
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    finalPos = hit.position;
                    return true;
                }
            }

            return false;
        }

        private List<AnimalSpawnEntry> GetSpawnableEntries(AnimalSpawnEntry[] entries)
        {
            var list = new List<AnimalSpawnEntry>();
            if (entries == null) return list;

            foreach (var e in entries)
            {
                if (e == null || e.definition == null) continue;

                int current = 0;
                _activeCountPerDefinition.TryGetValue(e.definition, out current);

                if (e.definition.maxCountGlobal > 0 && current >= e.definition.maxCountGlobal)
                    continue;

                list.Add(e);
            }

            return list;
        }

        private AnimalDefinition RollAnimalDefinition(List<AnimalSpawnEntry> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += Mathf.Max(0.01f, candidates[i].weight);

            float r = Random.value * totalWeight;
            float acc = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                acc += Mathf.Max(0.01f, candidates[i].weight);
                if (r <= acc)
                    return candidates[i].definition;
            }

            return candidates[Random.Range(0, candidates.Count)].definition;
        }

        private Gameplay.Animals.AnimalController GetFromPool(
            AnimalDefinition def,
            Vector3 position,
            Gameplay.Animals.AnimalSpawnZone spawnZone,
            Gameplay.Animals.AnimalSpawnZone rootZone)
        {
            if (def == null) return null;

            if (!_pools.TryGetValue(def, out var queue))
            {
                queue = new Queue<Gameplay.Animals.AnimalController>();
                _pools[def] = queue;
                _activeCountPerDefinition[def] = 0;
            }

            Gameplay.Animals.AnimalController ctrl = null;

            if (queue.Count > 0)
            {
                ctrl = queue.Dequeue();
            }
            else
            {
                if (def.prefab == null)
                {
                    Debug.LogWarning($"AnimalManager: prefab null tren AnimalDefinition {def.id}");
                    return null;
                }

                var obj = Instantiate(def.prefab, position, Quaternion.identity);
                ctrl = obj.GetComponent<Gameplay.Animals.AnimalController>();
                if (ctrl == null)
                    ctrl = obj.AddComponent<Gameplay.Animals.AnimalController>();
            }

            var go = ctrl.gameObject;
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;

            // IMPORTANT: clear despawning marker if any, before enabling again
            _despawning.Remove(ctrl);

            go.SetActive(true);

            // IMPORTANT: Init resets pooled states now
            ctrl.Init(def, spawnZone, rootZone);

            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
                agent.Warp(position);

            var visibility = ctrl.Visibility;
            if (visibility != null)
                visibility.PlaySpawnVFX();

            return ctrl;
        }

        public void DespawnZone(AnimalSpawnZone zone)
        {
            if (zone == null) return;

            for (int i = _activeAnimals.Count - 1; i >= 0; i--)
            {
                var ctrl = _activeAnimals[i];
                if (ctrl != null && ctrl.RootZone == zone)
                    DespawnAnimalWithFade(ctrl);
            }
        }

        public void DespawnAnimalWithFade(AnimalController controller)
        {
            if (controller == null) return;
            if (_despawning.Contains(controller)) return;

            _despawning.Add(controller);

            var visibility = controller.Visibility;
            if (visibility != null)
            {
                visibility.PlayDespawnVFXAndFadeOut(() =>
                {
                    InternalDespawn(controller);
                });
            }
            else
            {
                StartCoroutine(FallbackFadeOutThenDespawn(controller));
            }
        }

        private IEnumerator FallbackFadeOutThenDespawn(AnimalController controller)
        {
            if (controller == null) yield break;

            var go = controller.gameObject;
            if (go == null) yield break;

            float dur = Mathf.Max(0.05f, fallbackFadeDuration);

            Vector3 startScale = go.transform.localScale;
            Vector3 endScale = startScale * Mathf.Clamp(fallbackEndScale, 0.5f, 1f);

            var renderers = go.GetComponentsInChildren<Renderer>(true);

            var mats = new List<Material>();
            var matBaseColors = new List<Color>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var shared = r.materials;
                for (int m = 0; m < shared.Length; m++)
                {
                    var mat = shared[m];
                    if (mat == null) continue;

                    if (mat.HasProperty("_Color"))
                    {
                        mats.Add(mat);
                        matBaseColors.Add(mat.color);
                    }
                }
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);

                go.transform.localScale = Vector3.Lerp(startScale, endScale, p);

                for (int i = 0; i < mats.Count; i++)
                {
                    var c = matBaseColors[i];
                    c.a = Mathf.Lerp(matBaseColors[i].a, 0f, p);
                    mats[i].color = c;
                }

                yield return null;
            }

            go.transform.localScale = startScale;

            for (int i = 0; i < mats.Count; i++)
                mats[i].color = matBaseColors[i];

            InternalDespawn(controller);
        }

        private void InternalDespawn(AnimalController controller)
        {
            if (controller == null) return;

            var def = controller.Definition;
            var rootZone = controller.RootZone;

            controller.OnDespawn();
            controller.gameObject.SetActive(false);

            _activeAnimals.Remove(controller);
            _despawning.Remove(controller);

            if (rootZone != null)
                rootZone.currentCount = Mathf.Max(0, rootZone.currentCount - 1);

            if (def != null)
            {
                int count;
                _activeCountPerDefinition.TryGetValue(def, out count);
                _activeCountPerDefinition[def] = Mathf.Max(0, count - 1);

                if (!_pools.TryGetValue(def, out var queue))
                {
                    queue = new Queue<AnimalController>();
                    _pools[def] = queue;
                }

                queue.Enqueue(controller);
            }
        }

        private void HandleDayEnded()
        {
            for (int i = _activeAnimals.Count - 1; i >= 0; i--)
            {
                var ctrl = _activeAnimals[i];
                if (ctrl != null)
                    DespawnAnimalWithFade(ctrl);
            }
        }
    }
}
