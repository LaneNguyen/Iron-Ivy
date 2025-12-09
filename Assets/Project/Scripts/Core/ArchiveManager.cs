using UnityEngine;
using System;
using System.Collections.Generic;
using IronIvy.Data;

namespace IronIvy.Core
{
    public class ArchiveManager : BaseManager<ArchiveManager>
    {
        [Header("Config")]
        public float maxArchivePoints = 1000f;
        public List<ArchiveNodeDefinition> allNodes;

        [Header("Plant System Integration")]
        public List<PlantDefinition> startingPlants; // seed mặc định lúc đầu

        // runtime
        private List<PlantDefinition> _unlockedPlants = new List<PlantDefinition>();

        [Header("Debug Info")]
        public float currentPoints = 0f;                  // điểm thô
        public List<string> unlockedNodeIDs = new List<string>(); // node đã mở

        // % dạng 0–1 cho UI
        public float CurrentPercent => Mathf.Clamp01(
            maxArchivePoints > 0f ? currentPoints / maxArchivePoints : 0f
        );

        // event nội bộ (nếu script khác muốn nghe)
        public event Action<float> OnPointsChanged; // float = 0–1

        protected override void Awake()
        {
            base.Awake();
            // lúc start game rebuild lại list seed
            RebuildUnlockedPlants();
        }

        // ================= PUBLIC API =================

        // cộng điểm archive (dùng cho hệ thống mới)
        public void AddArchivePoints(float amount)
        {
            currentPoints += amount;
            currentPoints = Mathf.Clamp(currentPoints, 0f, maxArchivePoints);

            Debug.Log($"[Archive] +{amount} pts, total = {currentPoints}/{maxArchivePoints}");

            NotifyArchiveChanged();
        }

        // API cũ: ClickAnimal / ClickPlant vẫn đang gọi AddProgress
        public void AddProgress(float amount)
        {
            // để tương thích ngược
            AddArchivePoints(amount);
        }

        public bool IsNodeUnlocked(string nodeID) => unlockedNodeIDs.Contains(nodeID);

        public void UnlockNode(ArchiveNodeDefinition node)
        {
            if (node == null) return;
            if (IsNodeUnlocked(node.id)) return;

            if (currentPoints < node.costToUnlock)
            {
                Debug.Log($"[Archive] Not enough points for node {node.id}");
                return;
            }

            currentPoints -= node.costToUnlock;
            unlockedNodeIDs.Add(node.id);

            ApplyReward(node);

            Debug.Log($"[Archive] Unlocked Node: {node.title}");

            // sau khi trừ điểm + thưởng -> update UI
            NotifyArchiveChanged();

            // auto save nếu có SaveLoad
            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.SaveGame();
        }

        // ================= REWARD =================

        private void ApplyReward(ArchiveNodeDefinition node)
        {
            switch (node.rewardType)
            {
                case ArchiveRewardType.MaxEnergy:
                    if (EnergyManager.HasInstance)
                        EnergyManager.Instance.UpgradeMaxEnergy(node.rewardValue);
                    break;

                case ArchiveRewardType.NewSeed:
                    if (node.rewardObject is PlantDefinition p)
                    {
                        UnlockPlant(p);
                    }
                    else
                    {
                        Debug.LogError($"[Archive] Node {node.id} missing PlantDefinition reward");
                    }
                    break;

                // nếu có loại reward khác thì bổ sung thêm case ở đây
            }
        }

        // ================= PLANT / SEED FLOW =================

        private void UnlockPlant(PlantDefinition plant)
        {
            if (plant == null) return;

            if (!_unlockedPlants.Contains(plant))
            {
                _unlockedPlants.Add(plant);
                Debug.Log($"[Archive] NEW SEED UNLOCKED: {plant.displayName}");
            }
        }

        // dùng cho PlantRhythmStartPanel / hệ thống chọn seed
        public List<PlantDefinition> GetAvailablePlants()
        {
            // trả copy cho an toàn
            return new List<PlantDefinition>(_unlockedPlants);
        }

        // rebuild lại seed runtime từ:
        // - startingPlants
        // - list unlockedNodeIDs (node reward là NewSeed)
        private void RebuildUnlockedPlants()
        {
            _unlockedPlants.Clear();

            if (startingPlants != null && startingPlants.Count > 0)
                _unlockedPlants.AddRange(startingPlants);

            if (unlockedNodeIDs == null || unlockedNodeIDs.Count == 0)
                return;

            if (allNodes == null || allNodes.Count == 0)
                return;

            foreach (var nodeID in unlockedNodeIDs)
            {
                if (string.IsNullOrEmpty(nodeID)) continue;

                var node = allNodes.Find(n => n != null && n.id == nodeID);
                if (node == null) continue;

                if (node.rewardType == ArchiveRewardType.NewSeed &&
                    node.rewardObject is PlantDefinition p)
                {
                    if (!_unlockedPlants.Contains(p))
                        _unlockedPlants.Add(p);
                }
            }
        }

        // ================= LOAD / SAVE HOOK =================

        // load điểm + node IDs
        public void LoadState(float savedPoints, List<string> savedNodeIDs)
        {
            currentPoints = savedPoints;

            unlockedNodeIDs.Clear();
            if (savedNodeIDs != null && savedNodeIDs.Count > 0)
                unlockedNodeIDs.AddRange(savedNodeIDs);

            RebuildUnlockedPlants();

            // bắn lại % cho UI sau khi load
            NotifyArchiveChanged();
        }

        // overload cũ chỉ có điểm
        public void LoadState(float savedPoints)
        {
            LoadState(savedPoints, null);
        }

        // UI hay dùng GetPercent, trả về 0–1
        public float GetPercent()
        {
            return CurrentPercent;
        }

        // ================= INTERNAL HELPERS =================

        // chỗ này gom lại logic bắn event + ListenManager
        private void NotifyArchiveChanged()
        {
            float percent01 = CurrentPercent;

            // event nội bộ nếu có ai đăng ký
            OnPointsChanged?.Invoke(percent01);

            // bắn ra ListenManager để MainGameUIPanel nhận được
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseArchiveChanged(percent01);
            }
        }
    }
}
