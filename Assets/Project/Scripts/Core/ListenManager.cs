using System;
using System.Collections.Generic;
using UnityEngine;
using IronIvy.Data;
using IronIvy.Gameplay.Animals;

namespace IronIvy.Core
{
    public class ListenManager : BaseManager<ListenManager>
    {
        // =========================
        // SYSTEM / CORE EVENTS
        // =========================
        public event Action<int> OnEnergyChanged;
        public event Action<float> OnArchiveChanged;

        public event Action OnMinigameStarted;
        public event Action OnMinigameStopped;

        public event Action OnDayEnded;
        public event Action OnTrustSuccess;

        public event Action OnInventoryChanged;
        public event Action OnArchiveOpenRequested;

        // Event quan trọng để chống Race Condition nè trời ơi má ơi má
        public event Action OnSystemsReady;

        public void RaiseEnergyChanged(int value) => OnEnergyChanged?.Invoke(value);
        public void RaiseArchiveChanged(float value) => OnArchiveChanged?.Invoke(value);
        public void RaiseMinigameStarted() => OnMinigameStarted?.Invoke();
        public void RaiseMinigameStopped() => OnMinigameStopped?.Invoke();
        public void RaiseDayEnded() => OnDayEnded?.Invoke();
        public void RaiseTrustSuccess() => OnTrustSuccess?.Invoke();
        public void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();

        public void RaiseSystemsReady()
        {
            Debug.Log("<color=green>[ListenManager] Tất cả đã vào vị trí zồi Y</color>");
            OnSystemsReady?.Invoke();
        }

        public void RaiseArchiveOpenRequested()
        {
            OnArchiveOpenRequested?.Invoke();
        }

        // =========================
        // RHYTHM UI EVENTS (OBSERVER STYLE)
        // =========================

        // HUD: show / update / hide
        public event Action<RhythmHUDShowPayload> OnRhythmHUDShow;
        public event Action<RhythmHUDUpdatePayload> OnRhythmHUDUpdate;
        public event Action OnRhythmHUDHide;

        // Result panels
        public event Action<RhythmPlantResultPayload> OnRhythmPlantResult;
        public event Action<RhythmAnimalResultPayload> OnRhythmAnimalResult;

        // Result closed (UI -> system resume)
        public event Action OnRhythmResultClosed;

        // ----- Raise helpers -----
        public void RaiseRhythmHUDShow(RhythmHUDShowPayload payload) => OnRhythmHUDShow?.Invoke(payload);
        public void RaiseRhythmHUDUpdate(RhythmHUDUpdatePayload payload) => OnRhythmHUDUpdate?.Invoke(payload);
        public void RaiseRhythmHUDHide() => OnRhythmHUDHide?.Invoke();

        public void RaiseRhythmPlantResult(RhythmPlantResultPayload payload) => OnRhythmPlantResult?.Invoke(payload);
        public void RaiseRhythmAnimalResult(RhythmAnimalResultPayload payload) => OnRhythmAnimalResult?.Invoke(payload);

        public void RaiseRhythmResultClosed() => OnRhythmResultClosed?.Invoke();

        // =========================
        // PAYLOAD TYPES
        // =========================

        [Serializable]
        public class RhythmHUDShowPayload
        {
            public string title;

            // ưu tiên manual progress01 để debug, timeline chỉ optional
            public bool useTimeline;
            public int totalBeatsTimeline;
            public float beatDuration;

            public bool showHoldUI;

            public RhythmHUDShowPayload(
                string title,
                bool useTimeline,
                int totalBeatsTimeline,
                float beatDuration,
                bool showHoldUI
            )
            {
                this.title = title;
                this.useTimeline = useTimeline;
                this.totalBeatsTimeline = totalBeatsTimeline;
                this.beatDuration = beatDuration;
                this.showHoldUI = showHoldUI;
            }
        }

        [Serializable]
        public class RhythmHUDUpdatePayload
        {
            public int hit;
            public int miss;

            public float trust01;
            public float progress01;

            public string statusText;
            public bool statusPositive;

            public float hold01;

            // debug nhẹ: để trace đúng nhịp
            public int debugBeatIndex;         // scorable beat idx (0..)
            public string debugStepType;       // Tap/Hold/Rest
            public bool debugIsHold;

            public RhythmHUDUpdatePayload(
                int hit,
                int miss,
                float trust01,
                float progress01,
                string statusText,
                bool statusPositive,
                float hold01,
                int debugBeatIndex = -1,
                string debugStepType = "",
                bool debugIsHold = false
            )
            {
                this.hit = hit;
                this.miss = miss;
                this.trust01 = trust01;
                this.progress01 = progress01;
                this.statusText = statusText;
                this.statusPositive = statusPositive;
                this.hold01 = hold01;

                this.debugBeatIndex = debugBeatIndex;
                this.debugStepType = debugStepType;
                this.debugIsHold = debugIsHold;
            }
        }

        [Serializable]
        public class RhythmPlantResultPayload
        {
            public Dictionary<FoodItem, int> rewards;
            public int hit;
            public int miss;
            public float trust01;

            public RhythmPlantResultPayload(Dictionary<FoodItem, int> rewards, int hit, int miss, float trust01)
            {
                this.rewards = rewards;
                this.hit = hit;
                this.miss = miss;
                this.trust01 = trust01;
            }
        }

        [Serializable]
        public class RhythmAnimalResultPayload
        {
            public AnimalController animal;

            // Fallbacks: giữ data UI kể cả khi AnimalController bị despawn/pooled trước khi panel consume event
            public AnimalDefinition animalDefinition;
            public string animalDisplayName;
            public Sprite animalIcon;

            public float successRatio;
            public float archiveGained;

            public FoodItem lootItem;
            public int lootCount;

            public int hit;
            public int miss;

            // Constructor cũ: giữ nguyên để không phá compile
            public RhythmAnimalResultPayload(
                AnimalController animal,
                float successRatio,
                float archiveGained,
                FoodItem lootItem,
                int lootCount,
                int hit,
                int miss
            )
            {
                this.animal = animal;
                this.successRatio = successRatio;
                this.archiveGained = archiveGained;
                this.lootItem = lootItem;
                this.lootCount = lootCount;
                this.hit = hit;
                this.miss = miss;

                // snapshot ngay lúc raise event
                this.animalDefinition = (animal != null) ? animal.Definition : null;
                this.animalDisplayName = (this.animalDefinition != null) ? this.animalDefinition.displayName : string.Empty;
                this.animalIcon = (this.animalDefinition != null) ? this.animalDefinition.icon : null;
            }

            // Optional: hệ thống có thể raise mà không cần live AnimalController
            public RhythmAnimalResultPayload(
                AnimalDefinition animalDefinition,
                float successRatio,
                float archiveGained,
                FoodItem lootItem,
                int lootCount,
                int hit,
                int miss
            )
            {
                this.animal = null;
                this.successRatio = successRatio;
                this.archiveGained = archiveGained;
                this.lootItem = lootItem;
                this.lootCount = lootCount;
                this.hit = hit;
                this.miss = miss;

                this.animalDefinition = animalDefinition;
                this.animalDisplayName = (animalDefinition != null) ? animalDefinition.displayName : string.Empty;
                this.animalIcon = (animalDefinition != null) ? animalDefinition.icon : null;
            }
        }
    }
}
