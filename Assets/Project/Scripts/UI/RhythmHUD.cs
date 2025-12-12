using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.UI
{
    // HUD đơn giản cho rhythm
    // - title minigame
    // - hint text (CLICK / HOLD)
    // - trust slider + progress slider
    // - status + icon màu
    // - hit / miss
    // mấy hàm cũ cho engine V3/V4 vẫn giữ lại để không lỗi compile
    public class RhythmHUD : MonoBehaviour
    {
        [Header("Root")]
        public GameObject hudRoot;

        [Header("Title")]
        public TextMeshProUGUI titleText;

        [Header("Hint")]
        [Tooltip("Text hướng dẫn đơn giản (CLICK / HOLD...)")]
        public TextMeshProUGUI hintText;

        [Header("Trust & Progress")]
        public Slider trustSlider;
        public Slider progressSlider;

        [Header("Timeline Progress (Beat Count)")]
        [Tooltip("Nếu bật thì thanh chạy liên tục từ 0 -> 1 dựa trên tổng số beat, không phụ thuộc hit")]
        public bool useTimelineProgress = false;

        [Tooltip("Tổng số beat của màn, để map thành 100% timeline. Có thể set tay hoặc set từ code.")]
        public int totalBeatsForTimeline = 0;

        [Tooltip("Thời gian 1 beat (giây). Nếu để 0 thì nên gọi config từ code.")]
        public float secondsPerBeat = 0.5f;

        // internal cho timeline dựa trên beat
        float _timelineDuration;   // tổng thời gian round = totalBeats * secondsPerBeat
        float _timelineElapsed;    // thời gian đã trôi qua
        bool _timelinePlaying;     // flag đang chạy timeline

        [Header("Progress Lerp")]
        [Tooltip("Tốc độ lerp progress (giá trị càng cao càng bám sát target nhanh)")]
        public float progressLerpSpeed = 5f;

        [Header("Status")]
        public TextMeshProUGUI statusText;
        public Image statusIcon;
        public Color successColor = Color.green;
        public Color failColor = Color.red;

        [Header("Hit / Miss")]
        public TextMeshProUGUI hitText;
        public TextMeshProUGUI missText;

        // engine rhythm cũ, vẫn giữ reference cho ai còn dùng
        private RhythmMinigameBase current;

        // internal progress state cho smooth bar (mode cũ: theo target)
        float _currentProgress01;
        float _targetProgress01;

        private void OnEnable()
        {
            // đã chuyển qua ListenManager
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnMinigameStarted += OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped += OnMinigameStopped;
            }
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnMinigameStarted -= OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped -= OnMinigameStopped;
            }
        }

        private void Update()
        {
            if (progressSlider == null) return;

            // nếu bật timeline progress: thanh chạy liên tục theo tổng thời gian (tổng số beat quy đổi)
            if (useTimelineProgress && _timelinePlaying && _timelineDuration > 0f)
            {
                _timelineElapsed += Time.deltaTime;

                // map thời gian 0..duration sang 0..1
                _currentProgress01 = Mathf.Clamp01(_timelineElapsed / _timelineDuration);
            }
            else
            {
                // mode cũ: lerp progress slider mượt mượt từ current -> target
                // MoveTowards cho ổn định, không bị overshoot
                _currentProgress01 = Mathf.MoveTowards(
                    _currentProgress01,
                    _targetProgress01,
                    progressLerpSpeed * Time.deltaTime
                );
            }

            progressSlider.value = _currentProgress01;
        }

        // ListenManager callbacks

        private void OnMinigameStarted()
        {
            // thử tìm engine cũ nếu có (animal / plant bản trước)
            if (current == null)
                current = FindObjectOfType<RhythmMinigameBase>();

            if (hudRoot != null)
                hudRoot.SetActive(true);

            // nếu có reference minigame thì lấy tên nó làm title
            if (titleText != null && current != null)
                titleText.text = current.name;
        }

        private void OnMinigameStopped()
        {
            // không auto tắt HUD ở đây
            // engine mới tự gọi ResetHUD khi cần

            // stop timeline luôn cho chắc
            _timelinePlaying = false;
        }

        // bind thủ công cho engine cũ nếu không muốn rely ListenManager
        public void BindMinigame(RhythmMinigameBase minigame)
        {
            current = minigame;

            if (hudRoot != null)
                hudRoot.SetActive(true);

            if (titleText != null && minigame != null)
                titleText.text = minigame.name;
        }

        // set hint text, ví dụ: CLICK (LMB) / HOLD (LMB)
        public void SetKeyHints(IList<string> hints)
        {
            if (hintText == null) return;

            if (hints == null || hints.Count == 0)
            {
                hintText.text = string.Empty;
                return;
            }

            // gộp vài hint lại cho gọn
            hintText.text = string.Join(" / ", hints);
        }

        public void SetTrust01(float value01)
        {
            if (trustSlider == null) return;
            trustSlider.value = Mathf.Clamp01(value01);
        }

        // đây là API được minigame gọi mỗi beat / mỗi step (mode cũ)
        // giờ mình không set thẳng slider nữa mà chỉ update target
        public void SetProgress(float value01)
        {
            // nếu đang dùng timeline thì bỏ qua, để thanh chỉ chạy theo tổng thời gian
            if (useTimelineProgress)
                return;

            value01 = Mathf.Clamp01(value01);
            _targetProgress01 = value01;
        }

        // giữ cho tương thích engine cũ
        public void SetHoldVisual(float value01)
        {
            // trước đây dùng cho vòng hold chung, giờ target tự lo nên để trống
        }

        public void SetStatus(string message, bool isSuccess)
        {
            if (statusText != null)
                statusText.text = message;

            if (statusIcon != null)
                statusIcon.color = isSuccess ? successColor : failColor;
        }

        public void SetHitMiss(int hit, int miss)
        {
            if (hitText != null)
                hitText.text = hit.ToString();

            if (missText != null)
                missText.text = miss.ToString();
        }

        // preview beat window cũ, giờ không xài nữa
        public void SetBeatWindow(float center01, float halfWidth01)
        {
            // no-op, để cho code cũ compile
        }

        // phase 0..1 + inWindow cho hệ cursor cũ trên HUD
        public void SetBeatPhase(float phase, bool inWindow)
        {
            // no-op
        }

        // highlight 1 key slot (engine cũ)
        public void PulseKey(int index)
        {
            // no-op
        }

        public void ClearPulseKey(int index)
        {
            // no-op
        }

        // helper cho engine mới

        // set title tay cho minigame click-based
        public void SetMinigameTitle(string title)
        {
            if (titleText == null) return;
            titleText.text = title;
        }

        // wrapper cho progress
        public void UpdateProgress(float value01)
        {
            SetProgress(value01);
        }

        // wrapper cho hit/miss
        public void UpdateHitMiss(int hit, int miss)
        {
            SetHitMiss(hit, miss);
        }

        // config timeline theo beat
        // ví dụ: tổng 32 beat, mỗi beat 0.5s => duration = 16s
        public void ConfigureTimelineByBeats(int totalBeats, float beatSeconds)
        {
            if (totalBeats <= 0) totalBeats = 1;
            if (beatSeconds <= 0f) beatSeconds = 0.1f;

            totalBeatsForTimeline = totalBeats;
            secondsPerBeat = beatSeconds;
            _timelineDuration = totalBeatsForTimeline * secondsPerBeat;
        }

        // config timeline nếu đã biết sẵn tổng thời lượng round (giây)
        public void ConfigureTimelineByDuration(float durationSeconds)
        {
            if (durationSeconds <= 0f) durationSeconds = 0.1f;
            _timelineDuration = durationSeconds;
        }

        // được gọi từ minigame khi bắt đầu round
        public void StartTimeline()
        {
            _timelineElapsed = 0f;
            _timelinePlaying = true;

            // reset bar cho chắc
            _currentProgress01 = 0f;
            if (progressSlider != null)
                progressSlider.value = 0f;
        }

        // được gọi từ minigame khi kết thúc round
        public void StopTimeline()
        {
            _timelinePlaying = false;
        }

        // reset HUD về state default
        // - tắt root
        // - clear text
        public void ResetHUD()
        {
            if (hudRoot != null)
                hudRoot.SetActive(false);

            SetKeyHints(null);
            SetTrust01(0f);

            // reset progress internal
            _currentProgress01 = 0f;
            _targetProgress01 = 0f;

            // reset timeline
            _timelineElapsed = 0f;
            _timelineDuration = 0f;
            _timelinePlaying = false;

            if (progressSlider != null)
                progressSlider.value = 0f;

            SetStatus(string.Empty, true);
            SetHitMiss(0, 0);
        }
    }
}
