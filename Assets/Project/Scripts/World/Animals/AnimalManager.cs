using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using IronIvy.Data;
using IronIvy.Gameplay.Animals;

namespace IronIvy.Core
{
    public class AnimalManager : BaseManager<AnimalManager>
    {
        // Legacy encounter giu nguyen...
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
            public AnimalSpawnZone zone;   // co the la SingleZone hoac ParentGroup
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

        private readonly Dictionary<AnimalDefinition, Queue<AnimalController>> _pools =
            new Dictionary<AnimalDefinition, Queue<AnimalController>>();

        private readonly Dictionary<AnimalDefinition, int> _activeCountPerDefinition =
            new Dictionary<AnimalDefinition, int>();

        private readonly List<AnimalController> _activeAnimals =
            new List<AnimalController>();

        private float _checkTimer;
        private bool _initialized;

        private void OnEnable()
        {
            InitIfNeeded();

            // Đã đổi EventBus -> ListenManager
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
            // Đã đổi EventBus -> ListenManager
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

                // chi tao queue rong, khong Instantiate o day
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

                // dung ham cua zone de tinh khoang cach
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

            // dem so con cua ca nhom (neu la parent) hoac 1 zone don
            if (rootZone.currentCount >= config.maxAnimalsInZone) return;
            if (_activeAnimals.Count >= maxTotalAnimals) return;

            var candidates = GetSpawnableEntries(config.animals);
            if (candidates.Count == 0) return;

            AnimalDefinition chosenDef = RollAnimalDefinition(candidates);
            if (chosenDef == null) return;

            // chon zone con neu root la ParentGroup
            AnimalSpawnZone spawnZone = rootZone.GetRandomConcreteZone();
            if (spawnZone == null) spawnZone = rootZone;

            Vector3 spawnPos = spawnZone.GetRandomPointInside();
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 2f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            var controller = GetFromPool(chosenDef, spawnPos, spawnZone, rootZone);

            if (controller == null) return;

            rootZone.currentCount++;

            int count;
            _activeCountPerDefinition.TryGetValue(chosenDef, out count);
            _activeCountPerDefinition[chosenDef] = count + 1;

            _activeAnimals.Add(controller);
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
            {
                totalWeight += Mathf.Max(0.01f, candidates[i].weight);
            }

            float r = Random.value * totalWeight;
            float acc = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                acc += Mathf.Max(0.01f, candidates[i].weight);
                if (r <= acc)
                {
                    return candidates[i].definition;
                }
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

            // lay queue cho loai nay
            if (!_pools.TryGetValue(def, out var queue))
            {
                queue = new Queue<Gameplay.Animals.AnimalController>();
                _pools[def] = queue;
                _activeCountPerDefinition[def] = 0;
            }

            Gameplay.Animals.AnimalController ctrl = null;

            if (queue.Count > 0)
            {
                // lay tu pool co san
                ctrl = queue.Dequeue();
            }
            else
            {
                // LAN ĐẦU TIÊN: tao object NGAY TAI spawn position
                if (def.prefab == null)
                {
                    Debug.LogWarning($"AnimalManager: prefab null tren AnimalDefinition {def.id}");
                    return null;
                }

                var obj = Instantiate(def.prefab, position, Quaternion.identity);
                ctrl = obj.GetComponent<Gameplay.Animals.AnimalController>();
                if (ctrl == null)
                {
                    ctrl = obj.AddComponent<Gameplay.Animals.AnimalController>();
                }
            }

            var go = ctrl.gameObject;

            // set pos truoc, cho chac
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);

            // init zone + wander
            ctrl.Init(def, spawnZone, rootZone);

            // EP agent warp ve dung vi tri spawn (de no nap len NavMesh ngay tai day)
            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                // Warp se co gang tim NavMesh gan nhat quanh "position"
                agent.Warp(position);
            }

            // vfx spawn neu co
            var visibility = ctrl.Visibility;
            if (visibility != null)
            {
                visibility.PlaySpawnVFX();
            }

            return ctrl;
        }

        public void DespawnZone(AnimalSpawnZone zone)
        {
            if (zone == null) return;

            for (int i = _activeAnimals.Count - 1; i >= 0; i--)
            {
                var ctrl = _activeAnimals[i];
                if (ctrl != null && ctrl.RootZone == zone)
                {
                    DespawnAnimalWithFade(ctrl);
                }
            }
        }

        // Despawn 1 con animal:
        // - dùng khi: ra khỏi zone, hết ngày, HOẶC kết thúc minigame one-shot (AnimalController.DespawnAfterMinigame gọi qua)
        public void DespawnAnimalWithFade(AnimalController controller)
        {
            if (controller == null) return;

            var visibility = controller.Visibility;
            if (visibility != null)
            {
                // cho visibility lo VFX + fade, xong mới thật sự disable + trả về pool
                visibility.PlayDespawnVFXAndFadeOut(() =>
                {
                    InternalDespawn(controller);
                });
            }
            else
            {
                // fallback: không có visibility thì despawn thẳng
                InternalDespawn(controller);
            }
        }

        private void InternalDespawn(AnimalController controller)
        {
            if (controller == null) return;

            var def = controller.Definition;
            var rootZone = controller.RootZone;

            // cho con thú tự clear state (wander, anim, v.v.)
            controller.OnDespawn();

            // tắt object, để AnimalManager & pool quản lý
            controller.gameObject.SetActive(false);

            // bỏ khỏi list đang active
            _activeAnimals.Remove(controller);

            // trừ count trong zone (để sau này spawn con khác được)
            if (rootZone != null)
            {
                rootZone.currentCount = Mathf.Max(0, rootZone.currentCount - 1);
            }

            // trừ count theo definition + trả về pool
            if (def != null)
            {
                int count;
                _activeCountPerDefinition.TryGetValue(def, out count);
                _activeCountPerDefinition[def] = Mathf.Max(0, count - 1);

                Queue<AnimalController> queue;
                if (!_pools.TryGetValue(def, out queue))
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
                {
                    DespawnAnimalWithFade(ctrl);
                }
            }
        }
    }
}