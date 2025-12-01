using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IronIvy.Gameplay.Animals
{
    public enum AnimalZoneMode
    {
        SingleZone,      // 1 zone don le
        ParentGroup      // parent gom nhieu zone con
    }

    public class AnimalSpawnZone : MonoBehaviour
    {
        [Header("Mode")]
        [Tooltip("SingleZone = dung chinh object nay. ParentGroup = dung cac zone con ben duoi.")]
        public AnimalZoneMode mode = AnimalZoneMode.SingleZone;

        [Tooltip("Neu la ParentGroup thi tu dong tim AnimalSpawnZone con trong children.")]
        public bool autoCollectChildZones = true;

        [Tooltip("Danh sach zone con, chi dung neu mode = ParentGroup.")]
        public AnimalSpawnZone[] childZones;

        [Header("Zone config")]
        [Tooltip("Ban kinh zone tinh tu vi tri object nay (cho mode SingleZone).")]
        public float radius = 10f;

        [Tooltip("Neu co spawnPoints thi uu tien dung, neu khong thi random quanh radius.")]
        public Transform[] spawnPoints;

        [Header("Ground & NavMesh snap")]
        [Tooltip("Bat raycast tu tren xuong tim mat dat that su.")]
        public bool useGroundRaycast = true;

        [Tooltip("Layer nao duoc xem la mat dat (Ground, Terrain, ...). Nen bo Water ra.")]
        public LayerMask groundLayerMask;

        [Tooltip("Do cao bat dau ray tu tren xuong.")]
        public float raycastHeight = 30f;

        [Tooltip("Khoang cach ray xuong duoi, de du dai bang chieu cao map.")]
        public float raycastDownDistance = 80f;

        [Tooltip("Co snap vao NavMesh quanh diem ground khong.")]
        public bool snapToNavMesh = true;

        [Tooltip("Ban kinh sample NavMesh quanh diem ground.")]
        public float navMeshSampleDistance = 3f;

        [Tooltip("Nhoi Y len 1 chut de chan khong dinh xuyen mat dat.")]
        public float groundYOffset = 0.05f;

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

        // zone thuc su dung de spawn (neu la parent thi random 1 zone con)
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

        // khoang cach tu player toi zone (neu parent => lay child gan nhat)
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

        // diem random "thô" trong zone (chua ground, chua navmesh)
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

        // diem spawn da snap xuong mat dat + navmesh (day la diem AnimalManager nen dung)
        public Vector3 GetSafeSpawnPosition()
        {
            // 1. lay XZ base tu zone / spawnPoint
            Vector3 basePos = GetRandomPointInside();

            // ---------- STEP 1: tim ground chinh xac ----------
            Vector3 groundPos = basePos; // se cap nhat Y sau

            if (useGroundRaycast)
            {
                // ray tu tren xuong
                Vector3 top = basePos + Vector3.up * raycastHeight;
                float totalDist = raycastHeight + raycastDownDistance;

                RaycastHit hit;
                bool gotHit = false;

                LayerMask mask = (groundLayerMask.value != 0) ? groundLayerMask : ~0;

                if (Physics.Raycast(top, Vector3.down, out hit, totalDist, mask, QueryTriggerInteraction.Ignore))
                {
                    groundPos = hit.point;
                    gotHit = true;
                }
                else
                {
                    // fallback: ray tu duoi len (phong truong hop basePos dang o duoi mat dat mot chut)
                    Vector3 bottom = basePos - Vector3.up * raycastDownDistance;
                    if (Physics.Raycast(bottom, Vector3.up, out hit, totalDist, mask, QueryTriggerInteraction.Ignore))
                    {
                        groundPos = hit.point;
                        gotHit = true;
                    }
                }

                if (!gotHit)
                {
                    // neu khong tim duoc mat dat thi cu dung basePos, truong hop xau thoi
                    groundPos = basePos;
                }
            }

            // --------- STEP 2: snap vao NavMesh NHUNG khong cho keo xuong qua sau ----------
            Vector3 finalPos = groundPos;

            if (snapToNavMesh)
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(groundPos, out navHit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    finalPos = navHit.position;

                    // neu navmesh o duoi mat dat qua nhieu thi giu Y cua ground, chi lay XZ cua navmesh
                    float maxDrop = 0.5f;  // nguong cho phep keo xuong
                    if (finalPos.y < groundPos.y - maxDrop)
                    {
                        finalPos.y = groundPos.y;
                    }
                }
            }

            // --------- STEP 3: nhoi them len 1 chut ----------
            finalPos.y += groundYOffset;

            return finalPos;
        }


        private void OnDrawGizmosSelected()
        {
            // ve radius cua chinh zone
            Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.35f);
            Gizmos.DrawSphere(transform.position, radius);

            // ve zone con neu la group
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
