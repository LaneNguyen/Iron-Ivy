using UnityEngine;

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
        public MinigameCameraManager miniCam;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            energy.ResetDaily();
            zone.InitAtArchive(archive.CurrentPercent);
            ui.InitHUD(energy.Current, archive.CurrentPercent);
        }
    }
}
