using UnityEngine;
using System.Collections.Generic;
using IronIvy.Interfaces;
using IronIvy.Data;

namespace IronIvy.Gameplay.Rhythm
{
    // Rhythm Engine v4 base
    // - Scoring theo từng beat, mỗi beat chỉ hit hoặc miss
    // - Step dùng để config pattern (Tap / Hold / Rest + số beats)
    // - Class con override mấy hook bên dưới để làm UI, anim, trust vvv
    public abstract class RhythmMinigameBase : MonoBehaviour, IMinigame
    {
        [Tooltip("Pattern hiện tại lấy từ playlist")]
        public RhythmPattern pattern;

        public bool IsRunning { get; private set; }

        // timing cho beat
        protected float beatInterval;          // thời gian 1 beat (seconds)
        protected float lastBeatTime;          // thời điểm bắt đầu beat hiện tại

        // mapping step trong pattern
        protected int currentStepIndex;        // index trong pattern.sequence
        protected int beatsIntoCurrentStep;    // đã đi qua bao nhiêu beat trong step này

        // thông tin beat cho pattern hiện tại
        protected int beatIndex;               // beat index trong pattern (0-based)
        protected int totalBeats;              // tổng beat của pattern hiện tại
        protected int beatsHit;                // số beat hit
        protected int beatsMiss;               // số beat miss

        // playlist multi pattern
        protected int playlistTotalBeats;      // tổng beat của tất cả pattern trong playlist
        protected int playlistBeatIndex;       // global beat index trong playlist

        // flag: beat hiện tại đã được judge chưa
        protected bool hasJudgedThisBeat;

        // hit window theo phase 0..1
        protected float targetCenter01;        // tâm window (0..1)
        protected float targetHalfWidth01;     // nửa chiều rộng window (0..0.5)

        // trust / score tổng
        protected float trust;

        // danh sách pattern đang chơi
        protected List<RhythmPattern> playlist = new List<RhythmPattern>();
        protected int playlistIndex = 0;

        // hold settings
        [Header("Hold Settings")]
        [Tooltip("Thời gian tối thiểu phải giữ trong window để Hold được tính là HIT")]
        public float holdRequiredSeconds = 0.25f;

        protected float holdTimer = 0f;        // thời gian đã giữ trong window cho beat hiện tại

        // life cycle basic, chỗ start/stop game
        public virtual void StartGame()
        {
            // reset các biến global cho session này
            beatsHit = 0;
            beatsMiss = 0;
            beatIndex = 0;
            trust = 0f;
            holdTimer = 0f;

            // build playlist từ class con
            playlist.Clear();
            BuildPatternPlaylist(playlist);

            if (playlist.Count == 0)
            {
                Debug.LogWarning("[Rhythm] No pattern in playlist.");
                return;
            }

            // tính tổng beat toàn playlist
            playlistTotalBeats = 0;
            for (int i = 0; i < playlist.Count; i++)
                playlistTotalBeats += CountBeatsInPattern(playlist[i]);

            playlistBeatIndex = 0;

            // lấy pattern đầu tiên trong playlist
            playlistIndex = 0;
            pattern = playlist[0];

            PreparePattern();

            IsRunning = true;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStarted();
        }

        public virtual void StopGame()
        {
            if (!IsRunning) return;
            IsRunning = false;
            IronIvy.Core.EventBus.Instance.RaiseMinigameStopped();
        }

        protected virtual void OnDisable()
        {
            if (IsRunning)
                StopGame();
        }

        // pattern / beat setup
        // count tổng số beat trong 1 pattern dựa trên sequence
        protected int CountBeatsInPattern(RhythmPattern p)
        {
            if (p == null || p.sequence == null)
                return 0;

            int total = 0;
            foreach (var st in p.sequence)
                total += Mathf.Max(1, st.beats);

            return total;
        }

        // chuẩn bị pattern hiện tại
        // - tính beatInterval từ BPM
        // - reset step/beat index
        // - set hit window cho beat đầu tiên
        protected virtual void PreparePattern()
        {
            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
            {
                Debug.LogWarning("[Rhythm] Pattern or sequence is missing.");
                return;
            }

            beatInterval = 60f / Mathf.Max(1, pattern.bpm);
            lastBeatTime = Time.time;

            currentStepIndex = 0;
            beatsIntoCurrentStep = 0;

            totalBeats = CountBeatsInPattern(pattern);

            beatIndex = 0;
            hasJudgedThisBeat = false;
            holdTimer = 0f;

            SetupBeatWindow();
            OnBeat(); // hook cho beat đầu tiên
        }

        // set hit window cho beat hiện tại, random nhẹ cho đỡ nhàm
        protected virtual void SetupBeatWindow()
        {
            float baseWidth = pattern != null ? pattern.hitWindowSeconds : 0.2f;
            float width01 = baseWidth / Mathf.Max(beatInterval, 0.0001f);

            targetHalfWidth01 = Mathf.Clamp(width01, 0.05f, 0.45f);
            targetCenter01 = Random.Range(0.2f, 0.8f);
        }

        // check xem phase có nằm trong khoảng window hay không
        protected bool IsInHitWindow(float phase)
        {
            return Mathf.Abs(phase - targetCenter01) <= targetHalfWidth01;
        }

        protected RhythmPattern.Step GetCurrentStep()
        {
            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
                return default;

            int idx = Mathf.Clamp(currentStepIndex, 0, pattern.sequence.Length - 1);
            return pattern.sequence[idx];
        }

        // mỗi lần qua 1 beat thì tăng beatsIntoCurrentStep
        // nếu đủ số beat trong step thì chuyển sang step kế
        protected void AdvanceStepByBeat()
        {
            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
                return;

            beatsIntoCurrentStep++;

            RhythmPattern.Step step = pattern.sequence[currentStepIndex];
            int stepBeats = Mathf.Max(1, step.beats);

            if (beatsIntoCurrentStep >= stepBeats)
            {
                beatsIntoCurrentStep = 0;
                currentStepIndex++;
                if (currentStepIndex >= pattern.sequence.Length)
                    currentStepIndex = pattern.sequence.Length - 1; // clamp ở step cuối, không out of range
            }
        }

        // update loop chính, xử lý beat timing + input
        protected virtual void Update()
        {
            if (!IsRunning || pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
                return;

            float now = Time.time;
            float elapsed = now - lastBeatTime;

            // 1) xử lý kết thúc beat và chuyển sang beat mới nếu đủ thời gian
            if (elapsed >= beatInterval)
            {
                RhythmPattern.Step stepAtEnd = GetCurrentStep();

                // chưa judge thì auto xử lý miss/hit theo type
                if (!hasJudgedThisBeat)
                {
                    if (stepAtEnd.type == RhythmPattern.StepType.Hold)
                    {
                        // mode Hold: check theo holdTimer / holdRequiredSeconds
                        float required = Mathf.Max(0.01f, holdRequiredSeconds);
                        bool good = holdTimer >= required;
                        hasJudgedThisBeat = true;

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

                        OnStepJudged(stepAtEnd, good);
                    }
                    else
                    {
                        // Tap / Rest mà không bấm gì thì tính là miss
                        beatsMiss++;
                        OnBeatMissed();
                        // không gọi OnStepJudged ở đây để giữ giống logic cũ
                    }
                }

                // tăng index cho pattern và playlist
                beatIndex++;
                playlistBeatIndex++;

                if (beatIndex >= totalBeats)
                {
                    // hết pattern này, thử sang pattern tiếp theo trong playlist
                    if (!NextPattern())
                    {
                        // hết playlist luôn
                        OnPlaylistComplete();
                        StopGame();
                        return;
                    }

                    // reset lại thời gian cho pattern mới
                    now = Time.time;
                    elapsed = now - lastBeatTime;
                }
                else
                {
                    // sang beat mới trong cùng pattern
                    lastBeatTime = now;
                    elapsed = 0f;
                    hasJudgedThisBeat = false;
                    holdTimer = 0f;
                    AdvanceStepByBeat();
                    SetupBeatWindow();
                    OnBeat();
                }
            }

            // 2) phase trong beat hiện tại 0..1
            float phase = Mathf.Clamp01(elapsed / Mathf.Max(beatInterval, 0.0001f));
            bool inWindow = IsInHitWindow(phase);

            OnBeatProgress(phase, inWindow);

            // 3) xử lý input theo Step hiện tại
            RhythmPattern.Step currentStep = GetCurrentStep();

            // TAP: bấm một lần đúng lúc
            if (currentStep.type == RhythmPattern.StepType.Tap)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    TryJudgeCurrentBeat(phase);
                }
            }
            // HOLD: giữ phím khi đang trong window
            else if (currentStep.type == RhythmPattern.StepType.Hold)
            {
                // chỉ tích lũy thời gian giữ khi đang ở trong window
                if (inWindow && Input.GetKey(KeyCode.Space))
                {
                    holdTimer += Time.deltaTime;
                }
            }
            // REST: không làm gì với input
        }

        // thử chấm điểm beat hiện tại cho type Tap
        // mỗi beat chỉ được judge 1 lần
        protected void TryJudgeCurrentBeat(float phase)
        {
            if (hasJudgedThisBeat) return;

            RhythmPattern.Step step = GetCurrentStep();
            if (step.type == RhythmPattern.StepType.Hold)
            {
                // step này là Hold, không judge bằng 1 lần tap
                return;
            }

            bool good = IsInHitWindow(phase);
            hasJudgedThisBeat = true;

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

            // báo cho minigame con xử lý thêm (UI, anim)
            OnStepJudged(step, good);
        }

        // chuyển sang pattern tiếp theo trong playlist nếu còn
        protected bool NextPattern()
        {
            playlistIndex++;
            if (playlistIndex >= playlist.Count)
                return false;

            pattern = playlist[playlistIndex];
            PreparePattern();
            return true;
        }

        // các hook / abstract cho class con implement

        // gọi khi vừa chuyển sang beat mới
        protected virtual void OnBeat() { }

        // gọi mỗi frame trong beat hiện tại
        // phase 0..1, inWindow cho biết đang nằm trong vùng hit
        protected virtual void OnBeatProgress(float phase, bool inWindow) { }

        // gọi khi beat được judge là HIT
        protected virtual void OnBeatHit() { }

        // gọi khi beat được judge là MISS
        protected virtual void OnBeatMissed() { }

        // minigame con xử lý anim/trust theo step hiện tại
        protected abstract void OnStepJudged(RhythmPattern.Step step, bool good);

        // gọi khi tất cả pattern trong playlist đã chơi xong
        protected abstract void OnPlaylistComplete();

        // class con build playlist Single / Sequential / Shuffle
        protected abstract void BuildPatternPlaylist(List<RhythmPattern> outList);
    }
}
