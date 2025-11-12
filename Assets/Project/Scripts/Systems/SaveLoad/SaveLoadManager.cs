using UnityEngine;

namespace IronIvy.Core
{
    public class SaveLoadManager : BaseManager<SaveLoadManager>
    {
        const string KEY_ARCHIVE = "ironivy.archive";
        const string KEY_ENERGY = "ironivy.energy";

        public void SaveAll()
        {
            PlayerPrefs.SetFloat(KEY_ARCHIVE, ArchiveManager.Instance.CurrentPercent);
            PlayerPrefs.SetInt(KEY_ENERGY, EnergyManager.Instance.Current);
            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            if (PlayerPrefs.HasKey(KEY_ARCHIVE))
                ArchiveManager.Instance.CurrentPercent = PlayerPrefs.GetFloat(KEY_ARCHIVE, 0);
        }
    }
}
