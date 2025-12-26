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

        public bool HasSaveData()
        {
            return PlayerPrefs.HasKey(KEY_ARCHIVE_POINTS)
                || PlayerPrefs.HasKey(KEY_ARCHIVE_NODES)
                || PlayerPrefs.HasKey(KEY_ENERGY_MAX)
                || PlayerPrefs.HasKey(KEY_ENERGY_CUR);
        }

        public void DeleteSaveData()
        {
            PlayerPrefs.DeleteKey(KEY_ARCHIVE_POINTS);
            PlayerPrefs.DeleteKey(KEY_ARCHIVE_NODES);
            PlayerPrefs.DeleteKey(KEY_ENERGY_CUR);
            PlayerPrefs.DeleteKey(KEY_ENERGY_MAX);
            PlayerPrefs.Save();

            Debug.Log("[SaveLoad] Deleted save data (Archive/Energy).");
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

        // treatMissingAsNewGame: nếu chưa có save -> energy full, archive empty
        public void LoadAll(bool treatMissingAsNewGame = true)
        {
            bool hasSave = HasSaveData();

            // 1) Load Archive trước
            if (ArchiveManager.HasInstance)
            {
                float points = (hasSave)
                    ? PlayerPrefs.GetFloat(KEY_ARCHIVE_POINTS, 0f)
                    : 0f;

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
                {
                    // New profile / no save: full energy
                    savedCur = savedMax;
                }
                else
                {
                    // Có save: lấy đúng value (kể cả 0 nếu người chơi thật sự hết energy)
                    // Nếu key cur không tồn tại thì default = max
                    if (PlayerPrefs.HasKey(KEY_ENERGY_CUR))
                        savedCur = PlayerPrefs.GetInt(KEY_ENERGY_CUR, savedMax);
                    else
                        savedCur = savedMax;
                }

                EnergyManager.Instance.SetLoadedData(savedCur, savedMax);
            }
        }
    }
}
