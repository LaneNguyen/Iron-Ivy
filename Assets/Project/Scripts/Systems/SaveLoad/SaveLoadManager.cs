using UnityEngine;

namespace IronIvy.Core
{
    public class SaveLoadManager : BaseManager<SaveLoadManager>
    {
        const string KEY_ARCHIVE = "ironivy.archive";
        const string KEY_ENERGY = "ironivy.energy";

        // PUBLIC API
        // ArchiveTree gọi hàm này khi interact
        public void SaveGame()
        {
            SaveAll();
            Debug.Log("[SaveLoadManager] Game Saved successfully!");
        }

        public void SaveAll()
        {
            // Lưu Archive Percent
            if (ArchiveManager.HasInstance)
            {
                // Lấy % hiện tại (Read-only property) để lưu
                float currentPercent = ArchiveManager.Instance.CurrentPercent;
                PlayerPrefs.SetFloat(KEY_ARCHIVE, currentPercent);
            }

            // Lưu Energy hiện tại (nếu cần load lại đúng mức năng lượng đó)
            if (EnergyManager.HasInstance)
            {
                PlayerPrefs.SetInt(KEY_ENERGY, EnergyManager.Instance.Current);
            }

            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            // Load Archive Data
            if (PlayerPrefs.HasKey(KEY_ARCHIVE) && ArchiveManager.HasInstance)
            {
                float savedPercent = PlayerPrefs.GetFloat(KEY_ARCHIVE, 0);
                
                // Vì ArchiveManager mới chạy theo Points chứ không phải %,
                // ta cần quy đổi % đã lưu thành Points.
                // Points = (Percent / 100) * MaxPoints
                float maxPoints = ArchiveManager.Instance.maxArchivePoints;
                float estimatedPoints = (savedPercent / 100f) * maxPoints;

                // Gọi hàm LoadState (Compatibility Layer) mà chúng ta đã thêm vào ArchiveManager
                ArchiveManager.Instance.LoadState(estimatedPoints);
            }
            
            // Note: Energy thường reset theo ngày (ResetDaily), nên có thể không cần Load lại Energy
            // trừ khi bạn muốn lưu chính xác state giữa ngày.
        }
    }
}