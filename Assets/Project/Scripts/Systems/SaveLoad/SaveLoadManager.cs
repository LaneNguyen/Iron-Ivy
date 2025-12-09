using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Cần cái này để xử lý chuỗi

namespace IronIvy.Core
{
    public class SaveLoadManager : BaseManager<SaveLoadManager>
    {
        const string KEY_ARCHIVE_POINTS = "ironivy.archive.points";
        const string KEY_ARCHIVE_NODES = "ironivy.archive.nodes"; // Key lưu danh sách node
        const string KEY_ENERGY_CUR = "ironivy.energy.cur";
        const string KEY_ENERGY_MAX = "ironivy.energy.max";

        public void SaveGame()
        {
            SaveAll();
            Debug.Log("[SaveLoadManager] Game Saved!");
        }

        public void SaveAll()
        {
            // 1. Save Archive
            if (ArchiveManager.HasInstance)
            {
                PlayerPrefs.SetFloat(KEY_ARCHIVE_POINTS, ArchiveManager.Instance.currentPoints);
                
                // [FIX] Convert List<string> thành 1 chuỗi để lưu (VD: "node1,node2,node3")
                string nodesString = string.Join(",", ArchiveManager.Instance.unlockedNodeIDs);
                PlayerPrefs.SetString(KEY_ARCHIVE_NODES, nodesString);
            }

            // 2. Save Energy
            if (EnergyManager.HasInstance)
            {
                PlayerPrefs.SetInt(KEY_ENERGY_CUR, EnergyManager.Instance.Current);
                PlayerPrefs.SetInt(KEY_ENERGY_MAX, EnergyManager.Instance.MaxEnergy);
            }

            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            // 1. Load Energy
            if (EnergyManager.HasInstance)
            {
                int defaultMax = 6; 
                int savedMax = PlayerPrefs.GetInt(KEY_ENERGY_MAX, defaultMax);
                int savedCur = PlayerPrefs.GetInt(KEY_ENERGY_CUR, savedMax);

                // Gọi hàm set data mới trong EnergyManager
                EnergyManager.Instance.SetLoadedData(savedCur, savedMax);
            }

            // 2. Load Archive
            if (ArchiveManager.HasInstance)
            {
                float points = PlayerPrefs.GetFloat(KEY_ARCHIVE_POINTS, 0);
                
                // [FIX] Load chuỗi node và tách ra thành List
                List<string> loadedNodes = new List<string>();
                string nodesString = PlayerPrefs.GetString(KEY_ARCHIVE_NODES, "");
                
                if (!string.IsNullOrEmpty(nodesString))
                {
                    loadedNodes = nodesString.Split(',').ToList();
                }

                // Đẩy data vào ArchiveManager để nó tự Rebuild
                ArchiveManager.Instance.LoadState(points, loadedNodes);
            }
        }
    }
}