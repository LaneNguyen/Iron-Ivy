using UnityEngine;
using System.Collections.Generic;
using IronIvy.Interfaces;
using IronIvy.Data;

namespace IronIvy.Gameplay.Rhythm
{
    /// Base cho all rhythm mini-games: quản lý playlist pattern & chấm hit
    public abstract class RhythmMinigameBase : MonoBehaviour, IMinigame
    {
        [Tooltip("Pattern hiện tại (chạy theo playlist).")]
        public RhythmPattern pattern;

        public bool IsRunning { get; private set; }

        protected float beatInterval;
        protected float lastBeatTime;
        protected int seqIndex;
        protected float trust;

        // Playlist
        protected List<RhythmPattern> playlist = new List<RhythmPattern>();
        protected int playlistIndex;

        public virtual void StartGame()
        {
            // Step 1: nhận danh sách pattern từ con
            playlist.Clear();
            BuildPatternPlaylist(playlist);
            if (playlist.Count == 0) { Debug.LogWarning("[Rhythm] No patterns."); return; }

            // Step 2: bắt đầu ở pattern đầu
            playlistIndex = 0;
            pattern = playlist[playlistIndex];

            // Step 3: chuẩn bị pattern (tempo, index)
            PreparePattern();

            // Step 4: bật game
            IsRunning = true;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStarted();
        }

        public virtual void StopGame()
        {
            IsRunning = false;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStopped();
        }

        protected virtual void PreparePattern()
        {
            // Step A: tính khoảng beat từ BPM
            beatInterval = 60f / Mathf.Max(1, pattern.bpm);
            // Step B: reset index step cho pattern hiện tại
            seqIndex = 0;
            // Step C: mốc beat đầu
            lastBeatTime = Time.time;
        }

        protected virtual void Update()
        {
            if (!IsRunning || pattern == null) return;

            // Step 5: tick beat để hiển thị cue
            if (Time.time - lastBeatTime >= beatInterval)
            {
                lastBeatTime = Time.time;
                OnBeat();
            }

            // Step 6: input =>  chấm điểm
            if (Input.GetKeyDown(KeyCode.Space))
            {
                JudgeTap();
            }
        }

        protected virtual void OnBeat() { /* con tự pulse visuals */ }

        protected void JudgeTap()
        {
            if (seqIndex >= pattern.sequence.Length) return;

            var step = pattern.sequence[seqIndex];
            float dt = Mathf.Abs(Time.time - lastBeatTime);
            bool good = dt <= pattern.hitWindowSeconds;

            OnStepJudged(step, good); // con quyết định VFX/Anim/Trust

            seqIndex++;
            // Pattern xong => chuyển đoạn hoặc kết playlist
            if (seqIndex >= pattern.sequence.Length)
            {
                if (!NextPattern()) OnPlaylistComplete();
            }
        }

        /// Chuyển sang pattern kế tiếp, return false nếu hết
        protected bool NextPattern()
        {
            playlistIndex++;
            if (playlistIndex >= playlist.Count) return false;
            pattern = playlist[playlistIndex];
            PreparePattern();
            return true;
        }

        protected abstract void OnStepJudged(RhythmPattern.Step step, bool good);
        protected abstract void OnPlaylistComplete();
        /// Con build playlist + áp dụng playback mode (Single/Sequential/Shuffle)
        protected abstract void BuildPatternPlaylist(List<RhythmPattern> outList);
    }
}