using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IronIvy.Core
{
    public class UIManager : BaseManager<UIManager>
    {
        public TextMeshProUGUI energyText;
        public TextMeshProUGUI archiveText;
        public Button contextButton;

        public void InitHUD(int energy, float archive)
        {
            SetEnergy(energy);
            SetArchive(archive);
            EventBus.Instance.OnEnergyChanged += SetEnergy;
            EventBus.Instance.OnArchiveChanged += SetArchive;
        }

        void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnergyChanged -= SetEnergy;
                EventBus.Instance.OnArchiveChanged -= SetArchive;
            }
        }

        void SetEnergy(int v)   { if (energyText) energyText.text = $"Energy: {v}"; }
        void SetArchive(float v){ if (archiveText) archiveText.text = $"Archive: {v:0}%"; }
    }
}
