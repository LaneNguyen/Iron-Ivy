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
        public List<PlantDefinition> startingPlants;

        private List<PlantDefinition> _unlockedPlants = new List<PlantDefinition>();

        [Header("Debug Info")]
        public float currentPoints = 0f;
        public List<string> unlockedNodeIDs = new List<string>();

        public float CurrentPercent => Mathf.Clamp01(maxArchivePoints > 0f ? currentPoints / maxArchivePoints : 0f);

        public event Action<float> OnPointsChanged;

        // cache để check trùng id
        private Dictionary<string, int> _idCount = new Dictionary<string, int>();

        protected override void Awake()
        {
            base.Awake();
            RebuildIdCountCache();
            RebuildUnlockedPlants();
        }

        private void RebuildIdCountCache()
        {
            _idCount.Clear();

            if (allNodes == null) return;

            for (int i = 0; i < allNodes.Count; i++)
            {
                var n = allNodes[i];
                if (n == null) continue;

                if (string.IsNullOrEmpty(n.id)) continue;

                if (_idCount.ContainsKey(n.id)) _idCount[n.id]++;
                else _idCount[n.id] = 1;
            }

            // log warning nếu có trùng
            foreach (var kv in _idCount)
            {
                if (kv.Value > 1)
                    Debug.LogError($"[Archive] Duplicate node id detected: '{kv.Key}' appears {kv.Value} times. Fix ScriptableObjects ids nhé, không thì mở 1 node sẽ mở lây node khác.");
            }
        }

        public void AddArchivePoints(float amount)
        {
            currentPoints += amount;
            currentPoints = Mathf.Clamp(currentPoints, 0f, maxArchivePoints);

            Debug.Log($"[Archive] +{amount} pts, total = {currentPoints}/{maxArchivePoints}");
            NotifyArchiveChanged();
        }

        public void AddProgress(float amount) => AddArchivePoints(amount);

        public bool IsNodeUnlocked(string nodeID) => unlockedNodeIDs.Contains(nodeID);

        private bool HasDuplicateId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return _idCount != null && _idCount.TryGetValue(id, out int count) && count > 1;
        }

        public bool CanUnlockNode(ArchiveNodeDefinition node, out string reason)
        {
            reason = "";

            if (node == null)
            {
                reason = "Node null";
                return false;
            }

            if (string.IsNullOrEmpty(node.id))
            {
                reason = "Node id empty";
                return false;
            }

            // chặn cứng nếu id bị trùng (đúng bug Lane mô tả)
            if (HasDuplicateId(node.id))
            {
                reason = $"Duplicate id '{node.id}' (fix ids in ArchiveNodeDefinition assets)";
                return false;
            }

            if (IsNodeUnlocked(node.id))
            {
                reason = "Already unlocked";
                return false;
            }

            // parent gating
            if (node.requiredParent != null)
            {
                if (string.IsNullOrEmpty(node.requiredParent.id))
                {
                    reason = "Parent id empty";
                    return false;
                }

                // nếu parent id cũng trùng -> chặn luôn để khỏi lộn
                if (HasDuplicateId(node.requiredParent.id))
                {
                    reason = $"Duplicate parent id '{node.requiredParent.id}' (fix ids)";
                    return false;
                }

                bool parentUnlocked = IsNodeUnlocked(node.requiredParent.id);
                if (!parentUnlocked)
                {
                    reason = $"Need parent first: {node.requiredParent.id}";
                    return false;
                }
            }

            if (currentPoints < node.costToUnlock)
            {
                reason = "Not enough points";
                return false;
            }

            return true;
        }

        public bool UnlockNode(ArchiveNodeDefinition node)
        {
            if (!CanUnlockNode(node, out string reason))
            {
                Debug.LogWarning($"[Archive] Unlock failed: {(node != null ? node.id : "NULL")} | {reason}");
                return false;
            }

            currentPoints -= node.costToUnlock;
            unlockedNodeIDs.Add(node.id);

            ApplyReward(node);

            Debug.Log($"[Archive] Unlocked Node: {node.title}");
            NotifyArchiveChanged();

            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.SaveGame();

            return true;
        }

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
                        UnlockPlant(p);
                    else
                        Debug.LogError($"[Archive] Node {node.id} missing PlantDefinition reward");
                    break;
            }
        }

        private void UnlockPlant(PlantDefinition plant)
        {
            if (plant == null) return;

            if (!_unlockedPlants.Contains(plant))
            {
                _unlockedPlants.Add(plant);
                Debug.Log($"[Archive] NEW SEED UNLOCKED: {plant.displayName}");
            }
        }

        public List<PlantDefinition> GetAvailablePlants()
        {
            return new List<PlantDefinition>(_unlockedPlants);
        }

        private void RebuildUnlockedPlants()
        {
            _unlockedPlants.Clear();

            if (startingPlants != null && startingPlants.Count > 0)
                _unlockedPlants.AddRange(startingPlants);

            if (unlockedNodeIDs == null || unlockedNodeIDs.Count == 0) return;
            if (allNodes == null || allNodes.Count == 0) return;

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

        public void LoadState(float savedPoints, List<string> savedNodeIDs)
        {
            currentPoints = savedPoints;

            unlockedNodeIDs.Clear();
            if (savedNodeIDs != null && savedNodeIDs.Count > 0)
                unlockedNodeIDs.AddRange(savedNodeIDs);

            RebuildIdCountCache();
            RebuildUnlockedPlants();
            NotifyArchiveChanged();
        }

        public float GetPercent() => CurrentPercent;

        private void NotifyArchiveChanged()
        {
            float percent01 = CurrentPercent;

            OnPointsChanged?.Invoke(percent01);

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseArchiveChanged(percent01);
        }
    }
}
