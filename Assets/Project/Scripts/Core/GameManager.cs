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
            // 1) Core init (giữ như cũ)
            if (energy) energy.ResetDaily();

            if (zone)
                zone.InitAtArchive(archive ? archive.CurrentPercent : 0f);

            // 2) UI không init trực tiếp nữa
            // UI sẽ tự update bằng event
            if (ListenManager.HasInstance)
            {
                if (energy)
                    ListenManager.Instance.RaiseEnergyChanged(energy.Current);

                if (archive)
                    ListenManager.Instance.RaiseArchiveChanged(archive.CurrentPercent);

                if (inventory)
                    ListenManager.Instance.RaiseInventoryChanged();
            }

            // 3) Báo hệ thống sẵn sàng (UIManager sẽ inject spawnArea, bind event, v.v.)
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseSystemsReady();
            }
        }
    }
}
