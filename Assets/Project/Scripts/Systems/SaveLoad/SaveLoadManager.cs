using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace IronIvy.Core
{
    public class SaveLoadManager : BaseManager<SaveLoadManager>
    {
        const string KEY_ARCHIVE_POINTS = "ironivy.archive.points";
        const string KEY_ARCHIVE_NODES  = "ironivy.archive.nodes";
        const string KEY_ENERGY_CUR     = "ironivy.energy.cur";
        const string KEY_ENERGY_MAX     = "ironivy.energy.max";
        
        // Key cho vị trí Player
        const string KEY_PLAYER_X       = "ironivy.player.pos.x";
        const string KEY_PLAYER_Y       = "ironivy.player.pos.y";
        const string KEY_PLAYER_Z       = "ironivy.player.pos.z";

        public bool HasSaveData()
        {
            return PlayerPrefs.HasKey(KEY_ARCHIVE_POINTS)
                || PlayerPrefs.HasKey(KEY_ARCHIVE_NODES)
                || PlayerPrefs.HasKey(KEY_ENERGY_MAX)
                || PlayerPrefs.HasKey(KEY_ENERGY_CUR)
                || PlayerPrefs.HasKey(KEY_PLAYER_X); // Kiểm tra thêm vị trí
        }

        public void DeleteSaveData()
        {
            PlayerPrefs.DeleteKey(KEY_ARCHIVE_POINTS);
            PlayerPrefs.DeleteKey(KEY_ARCHIVE_NODES);
            PlayerPrefs.DeleteKey(KEY_ENERGY_CUR);
            PlayerPrefs.DeleteKey(KEY_ENERGY_MAX);
            PlayerPrefs.DeleteKey(KEY_PLAYER_X); // Xóa tọa độ
            PlayerPrefs.DeleteKey(KEY_PLAYER_Y);
            PlayerPrefs.DeleteKey(KEY_PLAYER_Z);
            PlayerPrefs.Save();

            Debug.Log("[SaveLoad] Deleted all save data including position.");
        }

        // Hàm mới để lưu vị trí Vector3
        public void SavePlayerPosition(Vector3 position)
        {
            PlayerPrefs.SetFloat(KEY_PLAYER_X, position.x);
            PlayerPrefs.SetFloat(KEY_PLAYER_Y, position.y);
            PlayerPrefs.SetFloat(KEY_PLAYER_Z, position.z);
            PlayerPrefs.Save();
        }

        // Hàm mới để lấy vị trí đã lưu
        public Vector3 GetSavedPlayerPosition(Vector3 defaultPos)
        {
            if (!PlayerPrefs.HasKey(KEY_PLAYER_X)) return defaultPos;

            return new Vector3(
                PlayerPrefs.GetFloat(KEY_PLAYER_X),
                PlayerPrefs.GetFloat(KEY_PLAYER_Y),
                PlayerPrefs.GetFloat(KEY_PLAYER_Z)
            );
        }

        public void SaveGame()
        {
            SaveAll();
            Debug.Log("[SaveLoadManager] Game Saved!");
        }

        public void SaveAll()
        {
            if (ArchiveManager.HasInstance)
            {
                PlayerPrefs.SetFloat(KEY_ARCHIVE_POINTS, ArchiveManager.Instance.currentPoints);
                string nodesString = string.Join(",", ArchiveManager.Instance.unlockedNodeIDs);
                PlayerPrefs.SetString(KEY_ARCHIVE_NODES, nodesString);
            }

            if (EnergyManager.HasInstance)
            {
                PlayerPrefs.SetInt(KEY_ENERGY_CUR, EnergyManager.Instance.Current);
                PlayerPrefs.SetInt(KEY_ENERGY_MAX, EnergyManager.Instance.MaxEnergy);
            }

            PlayerPrefs.Save();
        }

        public void LoadAll(bool treatMissingAsNewGame = true)
        {
            bool hasSave = HasSaveData();

            // 1) Load Archive
            if (ArchiveManager.HasInstance)
            {
                float points = (hasSave) ? PlayerPrefs.GetFloat(KEY_ARCHIVE_POINTS, 0f) : 0f;
                List<string> loadedNodes = new List<string>();
                string nodesString = (hasSave) ? PlayerPrefs.GetString(KEY_ARCHIVE_NODES, "") : "";
                if (!string.IsNullOrEmpty(nodesString))
                    loadedNodes = nodesString.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();

                ArchiveManager.Instance.LoadState(points, loadedNodes);
            }

            // 2) Load Energy
            if (EnergyManager.HasInstance)
            {
                int defaultMax = 6;
                int savedMax = PlayerPrefs.GetInt(KEY_ENERGY_MAX, defaultMax);
                int savedCur;

                if (!hasSave && treatMissingAsNewGame)
                    savedCur = savedMax;
                else
                    savedCur = PlayerPrefs.GetInt(KEY_ENERGY_CUR, savedMax);

                EnergyManager.Instance.SetLoadedData(savedCur, savedMax);
            }
        }
    }
}