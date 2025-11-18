using UnityEngine;
using System.Collections.Generic;
using IronIvy.Interfaces;
using IronIvy.Data;

namespace IronIvy.Gameplay.Rhythm
{
    /// <summary>
    /// Rhythm Engine V3
    /// - Kết hợp Step engine cũ (OnStepJudged) + Beat engine mới
    /// - Mỗi pattern gồm nhiều Step
    /// - Mỗi Step có nhiều beats
    /// - Scoring dựa theo BEAT (Hit/Miss)
    /// - Step vẫn dùng để drive animation
    /// - Biến trust dùng chung cho các minigame con (Plant/Animal...)
    /// </summary>
    public abstract class RhythmMinigameBase : MonoBehaviour, IMinigame
    {
        [Tooltip("Pattern đang được chơi.")]
        public RhythmPattern pattern;

        /// <summary>
        /// true khi minigame đang chạy.
        /// </summary>
        public bool IsRunning { get; private set; }

        // Timing
        protected float beatInterval;
        protected float lastBeatTime;

        // Step engine (index trong pattern.sequence)
        protected int seqIndex = 0;

        // Beat engine
        protected int beatIndex;            // beat hiện tại trong cả pattern
        protected int totalBeats;           // tổng beat của pattern
        protected int beatsInCurrentStep;   // beat đã đi trong step hiện tại
        protected int beatsHit;             // tổng beat hit tốt
        protected int beatsMiss;            // tổng beat bỏ lỡ hoặc hit sai

        // Điểm tin tưởng / score tổng cho minigame
        protected float trust;

        // Playlist pattern (nhiều phase)
        protected List<RhythmPattern> playlist = new List<RhythmPattern>();
        protected int playlistIndex = 0;

        public virtual void StartGame()
        {
            playlist.Clear();
            BuildPatternPlaylist(playlist);

            if (playlist.Count == 0)
            {
                Debug.LogWarning("[Rhythm] No pattern in playlist.");
                return;
            }

            playlistIndex = 0;
            pattern = playlist[0];

            PreparePattern();

            IsRunning = true;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStarted();
        }

        public virtual void StopGame()
        {
            IsRunning = false;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStopped();
        }

        protected virtual void OnDisable()
        {
            if (IsRunning)
                StopGame();
        }

        /// <summary>
        /// Chuẩn bị pattern hiện tại:
        /// - Tính beatInterval từ BPM
        /// - Reset seqIndex, beatIndex
        /// - Tính tổng số beat
        /// </summary>
        protected virtual void PreparePattern()
        {
            beatInterval = 60f / Mathf.Max(1, pattern.bpm);
            lastBeatTime = Time.time;

            seqIndex = 0;

            ComputeTotalBeats();
            beatsInCurrentStep = 0;
        }

        /// <summary>
        /// Tính tổng số beat từ tất cả Step.
        /// </summary>
        protected void ComputeTotalBeats()
        {
            totalBeats = 0;
            if (pattern.sequence != null)
            {
                foreach (var st in pattern.sequence)
                    totalBeats += Mathf.Max(1, st.beats);
            }

            beatIndex = 0;
            beatsHit = 0;
            beatsMiss = 0;
        }

        protected virtual void Update()
        {
            if (!IsRunning || pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
                return;

            float dtBeat = Time.time - lastBeatTime;

            // Beat mới
            if (dtBeat >= beatInterval)
            {
                // Auto-miss: nếu vẫn còn beats trong step mà beat này chưa được hit
                if (beatsInCurrentStep < pattern.sequence[seqIndex].beats)
                {
                    beatsMiss++;
                    OnBeatMissed();
                }

                beatsInCurrentStep++;
                beatIndex++;

                lastBeatTime = Time.time;
                dtBeat = 0f;

                // Hết beats trong step → chuyển step
                if (beatsInCurrentStep >= pattern.sequence[seqIndex].beats)
                {
                    beatsInCurrentStep = 0;
                    seqIndex++;

                    if (seqIndex >= pattern.sequence.Length)
                    {
                        // Hết pattern
                        if (!NextPattern())
                        {
                            OnPlaylistComplete();
                            StopGame();
                            return;
                        }
                    }
                }

                OnBeat();
            }

            // Phase = 0..1 trong 1 beat
            float safeInterval = Mathf.Max(beatInterval, 0.0001f);
            float phase = Mathf.Clamp01(dtBeat / safeInterval);

            // Cửa sổ an toàn để bấm (theo logic scoring)
            bool inWindow = Mathf.Abs(dtBeat) <= pattern.hitWindowSeconds;

            OnBeatProgress(phase, inWindow);

            // Input tap
            if (Input.GetKeyDown(KeyCode.Space))
            {
                JudgeTap();
            }
        }

        /// <summary>
        /// Gọi khi người chơi bấm Space:
        /// - Kiểm tra có trong hit window không
        /// - Cập nhật beatsHit / beatsMiss
        /// - Gọi OnBeatHit / OnBeatMissed
        /// - Gọi OnStepJudged để con xử lý anim/trust theo step hiện tại
        /// </summary>
        protected void JudgeTap()
        {
            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
                return;

            // Bảo vệ seqIndex
            int safeIndex = Mathf.Clamp(seqIndex, 0, pattern.sequence.Length - 1);
            var step = pattern.sequence[safeIndex];

            float dt = Mathf.Abs(Time.time - lastBeatTime);
            bool good = dt <= pattern.hitWindowSeconds;

            if (good)
            {
                beatsHit++;
                OnBeatHit();
            }
            else
            {
                beatsMiss++;
                OnBeatMissed();
            }

            // Step judgement cho minigame con
            OnStepJudged(step, good);
        }

        /// <summary>
        /// Chuyển sang pattern tiếp theo trong playlist.
        /// </summary>
        protected bool NextPattern()
        {
            playlistIndex++;
            if (playlistIndex >= playlist.Count)
                return false;

            pattern = playlist[playlistIndex];
            PreparePattern();
            return true;
        }

        // ===== Hooks cho minigame con override =====

        /// <summary>
        /// Gọi mỗi khi vào 1 beat mới.
        /// </summary>
        protected virtual void OnBeat() { }

        /// <summary>
        /// Gọi mỗi frame giữa 2 beat.
        /// phase: 0..1 trong 1 beat.
        /// inWindow: true nếu đang trong khoảng an toàn để bấm.
        /// </summary>
        protected virtual void OnBeatProgress(float phase, bool inWindow) { }

        /// <summary>
        /// Gọi khi bấm đúng nhịp.
        /// </summary>
        protected virtual void OnBeatHit() { }

        /// <summary>
        /// Gọi khi bấm sai hoặc bỏ lỡ beat.
        /// </summary>
        protected virtual void OnBeatMissed() { }

        /// <summary>
        /// Xử lý theo từng Step (config trong ScriptableObject).
        /// Minigame con dùng step.type, step.beats để quyết định anim/trust.
        /// </summary>
        protected abstract void OnStepJudged(RhythmPattern.Step step, bool good);

        /// <summary>
        /// Gọi khi playlist hoàn thành (tất cả pattern đã chơi xong).
        /// </summary>
        protected abstract void OnPlaylistComplete();

        /// <summary>
        /// Minigame con build playlist tùy theo config (Single/Sequential/Shuffle).
        /// </summary>
        protected abstract void BuildPatternPlaylist(List<RhythmPattern> list);
    }
}
