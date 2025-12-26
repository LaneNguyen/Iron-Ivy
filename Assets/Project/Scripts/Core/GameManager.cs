using IronIvy.Systems.Camera;
using UnityEngine;

namespace IronIvy.Core
{
    public class GameManager : BaseManager<GameManager>
    {
        [Header("Init Flow")]
        [SerializeField] private bool autoInitOnStart = false;

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

        private bool _coreInited;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (autoInitOnStart)
                InitGameplayCore(isNewGame: false);
        }

        public void InitGameplayCore(bool isNewGame)
        {
            if (_coreInited) return;
            _coreInited = true;

            // New Game: nếu em muốn luôn full energy khi bắt đầu new game
            if (isNewGame && energy)
                energy.ResetDaily(); // optional

            // Zone phụ thuộc Archive %
            if (zone && archive)
                zone.InitAtArchive(archive.CurrentPercent100);

            // Sync UI bằng event
            if (ListenManager.HasInstance)
            {
                if (energy) ListenManager.Instance.RaiseEnergyChanged(energy.Current);
                if (archive) ListenManager.Instance.RaiseArchiveChanged(archive.CurrentPercent);
                if (inventory) ListenManager.Instance.RaiseInventoryChanged();

                ListenManager.Instance.RaiseSystemsReady();
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

        }
    }
}
