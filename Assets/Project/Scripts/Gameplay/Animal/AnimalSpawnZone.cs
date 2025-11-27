using System.Collections.Generic;
using UnityEngine;

namespace IronIvy.Gameplay.Animals
{
    public enum AnimalZoneMode
    {
        SingleZone,
        ParentGroup
    }

    public class AnimalSpawnZone : MonoBehaviour
    {
        [Header("Mode")]
        public AnimalZoneMode mode = AnimalZoneMode.SingleZone;
        public bool autoCollectChildZones = true;
        public AnimalSpawnZone[] childZones;

        [Header("Zone config")]
        public float radius = 10f;
        public Transform[] spawnPoints;

        [Header("Runtime info")]
        [HideInInspector]
        public int currentCount;

        private void OnValidate()
        {
            if (mode == AnimalZoneMode.ParentGroup && autoCollectChildZones)
            {
                CollectChildZones();
            }
        }

        private void CollectChildZones()
        {
            var list = new List<AnimalSpawnZone>();
            var all = GetComponentsInChildren<AnimalSpawnZone>(includeInactive: true);
            foreach (var z in all)
            {
                if (z == this) continue;
                if (z.mode == AnimalZoneMode.SingleZone)
                {
                    list.Add(z);
                }
            }
            childZones = list.ToArray();
        }

        // zone thuc su dung de spawn
        public AnimalSpawnZone GetRandomConcreteZone()
        {
            if (mode == AnimalZoneMode.ParentGroup &&
                childZones != null &&
                childZones.Length > 0)
            {
                return childZones[Random.Range(0, childZones.Length)];
            }

            return this;
        }

        // khoang cach tu player den zone (neu la parent => lay zone con gan nhat)
        public float GetDistanceTo(Transform target)
        {
            if (target == null) return float.MaxValue;

            if (mode == AnimalZoneMode.ParentGroup &&
                childZones != null &&
                childZones.Length > 0)
            {
                float min = float.MaxValue;
                foreach (var z in childZones)
                {
                    if (z == null) continue;
                    float d = Vector3.Distance(target.position, z.transform.position);
                    if (d < min) min = d;
                }
                return min;
            }

            return Vector3.Distance(target.position, transform.position);
        }

        public Vector3 GetRandomPointInside()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return p ? p.position : transform.position;
            }

            Vector2 circle = Random.insideUnitCircle * radius;
            return transform.position + new Vector3(circle.x, 0f, circle.y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.35f);
            Gizmos.DrawSphere(transform.position, radius);

            if (mode == AnimalZoneMode.ParentGroup && childZones != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var z in childZones)
                {
                    if (z == null) continue;
                    Gizmos.DrawWireSphere(z.transform.position, z.radius);
                }
            }
        }
    }
}
