using UnityEngine;
using IronIvy.Systems.Camera;

namespace IronIvy.Core
{
    public class GameManager : BaseManager<GameManager>
    {
        public DayCycleManager dayCycle;
        public ZoneManager zone;
        public ArchiveManager archive;
        public EnergyManager energy;
        public InventoryManager inventory;
        public UIManager ui;
        public RhythmManager rhythm;
        public SaveLoadManager saveLoad;
        public AudioManager audioMgr;
        public AnimalManager animalMgr;
        public CameraManager miniCam;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            // Khởi tạo các logic game
            energy.ResetDaily();
            if (zone) zone.InitAtArchive(archive ? archive.CurrentPercent : 0);
            if (ui) ui.InitHUD(energy.Current, archive ? archive.CurrentPercent : 0);
            
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseSystemsReady();
            }
        }
    }
}