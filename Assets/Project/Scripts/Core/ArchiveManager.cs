using System;
using System.Collections.Generic;
using IronIvy.Data;
using UnityEngine;

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

        // Current progress (0..1). 1.0 means đạt 100% archive progress.
        public float CurrentPercent => Mathf.Clamp01(maxArchivePoints > 0f ? currentPoints / maxArchivePoints : 0f);

        // Helper: progress in percent (0..100)
        public float CurrentPercent100 => CurrentPercent * 100f;

        public event Action<float> OnPointsChanged;
        public event Action<string> OnNodeUnlocked;

        private Dictionary<string, int> _idCount = new Dictionary<string, int>();

        protected override void Awake()
        {
            base.Awake();
            RebuildIdCountCache();
            RebuildUnlockedPlants();
        }

        public void InitCore()
        {
            NotifyArchiveChanged();
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

            Debug.Log($"[Archive] +{amount} pts, total = {currentPoints}/{maxArchivePoints} ({CurrentPercent100:0.#}%)");
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

            if (node.requiredParent != null)
            {
                if (string.IsNullOrEmpty(node.requiredParent.id))
                {
                    reason = "Parent id empty";
                    return false;
                }

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

            float requiredPercent = Mathf.Clamp(node.costToUnlock, 0f, 100f);
            if (CurrentPercent100 + 0.0001f < requiredPercent)
            {
                reason = "Not enough progress";
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

            unlockedNodeIDs.Add(node.id);
            OnNodeUnlocked?.Invoke(node.id);

            ApplyReward(node);

            Debug.Log($"[Archive] Unlocked Node: {node.title} (Progress stays at {CurrentPercent100:0.#}%)");
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

        // =========================
        // DEBUG / CHEAT
        // =========================

        public void SetProgressPercent100(bool save = true)
        {
            currentPoints = Mathf.Clamp(maxArchivePoints, 0f, maxArchivePoints);

            Debug.Log($"[Archive] DEBUG: Set progress to 100% ({currentPoints}/{maxArchivePoints})");
            NotifyArchiveChanged();

            if (save && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.SaveGame();
        }

        public void SetProgressPercent(float percent01, bool save = true)
        {
            float p = Mathf.Clamp01(percent01);
            currentPoints = Mathf.Clamp(p * Mathf.Max(0f, maxArchivePoints), 0f, maxArchivePoints);

            Debug.Log($"[Archive] DEBUG: Set progress to {p * 100f:0.#}% ({currentPoints}/{maxArchivePoints})");
            NotifyArchiveChanged();

            if (save && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.SaveGame();
        }
    }
}
