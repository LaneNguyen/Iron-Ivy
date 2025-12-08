using UnityEngine;
using System;
using System.Collections.Generic;
using IronIvy.Data;

namespace IronIvy.Core
{
    // manager cho hệ thống Archive (cây ký ức)
    // - giữ điểm archive tổng
    // - mở khóa node + apply reward
    // - quản lý list plant đã unlock
    public class ArchiveManager : BaseManager<ArchiveManager>
    {
        [Header("Config")]
        // giới hạn điểm tối đa để tính % tiến độ
        public float maxArchivePoints = 1000f;

        // list tất cả node trong cây ký ức
        public List<ArchiveNodeDefinition> allNodes;

        [Header("Plant System Integration")]
        // mấy giống cây có sẵn từ đầu game
        public List<PlantDefinition> startingPlants;

        // cache lại list cây đã mở khóa để lấy cho nhanh
        private List<PlantDefinition> _unlockedPlants = new List<PlantDefinition>();

        [Header("Debug Info")]
        public float currentPoints = 0f;

        // lưu id mấy node đã mở rồi
        public List<string> unlockedNodeIDs = new List<string>();

        // tính % tiến độ, clamp lại cho chắc
        public float CurrentPercent => Mathf.Clamp01(currentPoints / maxArchivePoints) * 100f;

        // event báo cho UI biết khi điểm thay đổi
        public event Action<float> OnPointsChanged;

        protected override void Awake()
        {
            base.Awake();

            // build lại list cây đã unlock lúc start game
            RebuildUnlockedPlants();
        }

        // api cộng trừ điểm archive

        public void AddArchivePoints(float amount)
        {
            // check input <= 0 thì thôi không cộng
            if (amount <= 0) return;

            currentPoints += amount;

            // log nhanh xem có cộng đúng không
            Debug.Log($"[Archive] Da cong them {amount} diem. Tong hien tai: {currentPoints}");

            CheckWorldThresholds();

            // báo UI update thanh progress
            OnPointsChanged?.Invoke(currentPoints);

            // báo ListenManager nếu có
            // để nó phát event archive cho chỗ khác dùng
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseArchiveChanged(CurrentPercent / 100f);
            }
        }

        public bool TrySpendPoints(float cost)
        {
            // check đủ điểm mới trừ
            if (currentPoints >= cost)
            {
                currentPoints -= cost;

                // trừ xong cũng phải báo UI update lại
                OnPointsChanged?.Invoke(currentPoints);

                return true;
            }

            return false;
        }

        // logic mở khóa node

        public bool IsNodeUnlocked(string nodeID)
        {
            return unlockedNodeIDs.Contains(nodeID);
        }

        public void UnlockNode(ArchiveNodeDefinition node)
        {
            // check xem node này mở chưa
            if (IsNodeUnlocked(node.id))
            {
                Debug.Log("Cai node nay mo roi ma?");
                return;
            }

            // thử trừ điểm, nếu ok mới add vào list unlocked
            if (TrySpendPoints(node.costToUnlock))
            {
                unlockedNodeIDs.Add(node.id);
                ApplyReward(node);
                Debug.Log($"Da mo khoa node: {node.title}");
            }
        }

        // xử lý reward khi unlock node
        private void ApplyReward(ArchiveNodeDefinition node)
        {
            switch (node.rewardType)
            {
                case ArchiveRewardType.MaxEnergy:
                    // tăng max energy cho player
                    if (EnergyManager.HasInstance)
                        EnergyManager.Instance.UpgradeMaxEnergy(node.rewardValue);
                    break;

                case ArchiveRewardType.NewSeed:
                    // thêm giống cây mới vào hệ thống
                    if (node.rewardObject is PlantDefinition newPlant)
                    {
                        UnlockPlant(newPlant);
                    }
                    break;

                case ArchiveRewardType.ZoneUnlock:
                    // chỗ này để dành cho việc mở zone, map mới
                    // chưa làm nên tạm để trống
                    break;
            }
        }

        // plant management

        public List<PlantDefinition> GetAvailablePlants()
        {
            // trả về bản copy để ngoài kia không sửa trực tiếp list trong manager
            return new List<PlantDefinition>(_unlockedPlants);
        }

        private void UnlockPlant(PlantDefinition plant)
        {
            // check coi đã unlock chưa, chưa có thì add vào
            if (!_unlockedPlants.Contains(plant))
            {
                _unlockedPlants.Add(plant);
                Debug.Log($"[Archive] Da mo khoa giong cay moi: {plant.displayName}");
            }
        }

        private void RebuildUnlockedPlants()
        {
            // tính toán lại list cây dựa trên:
            // - startingPlants
            // - các node đã unlock có reward là NewSeed
            _unlockedPlants.Clear();

            if (startingPlants != null)
                _unlockedPlants.AddRange(startingPlants);

            // quét qua mấy node đã unlock xem có giống cây nào không
            foreach (var nodeID in unlockedNodeIDs)
            {
                var node = allNodes.Find(n => n.id == nodeID);
                if (node != null &&
                    node.rewardType == ArchiveRewardType.NewSeed &&
                    node.rewardObject is PlantDefinition p)
                {
                    // check duplicate cho chắc
                    if (!_unlockedPlants.Contains(p))
                        _unlockedPlants.Add(p);
                }
            }
        }

        // world events / threshold theo % archive
        private void CheckWorldThresholds()
        {
            // float p = CurrentPercent
            // chỗ này để dành trigger cutscene hoặc update zone sau này
        }

        public float GetPercent()
        {
            return CurrentPercent;
        }

        // alias cho AddArchivePoints cho code chỗ khác đọc dễ hơn
        public void AddProgress(float amount)
        {
            AddArchivePoints(amount);
        }

        // load dữ liệu archive từ save
        public void LoadState(float savedPoints)
        {
            currentPoints = savedPoints;

            // load xong cũng cần báo UI update lại
            OnPointsChanged?.Invoke(currentPoints);

            // check lại toàn bộ node
            foreach (var node in allNodes)
            {
                if (!IsNodeUnlocked(node.id) && node.costToUnlock <= 0)
                {
                    // chỗ này có thể thêm logic auto unlock node free
                    // hiện tại để trống cho dễ kiểm soát sau
                }
            }

            // tính lại list cây cho đồng bộ với state mới
            RebuildUnlockedPlants();
        }
    }
}
