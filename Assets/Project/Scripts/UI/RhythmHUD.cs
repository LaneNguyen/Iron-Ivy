using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Core;

namespace IronIvy.UI
{
    public class RhythmHUD : MonoBehaviour
    {
        [Header("Root")]
        public GameObject hudRoot;

        [Header("Title")]
        public TextMeshProUGUI titleText;

        [Header("Hint")]
        public TextMeshProUGUI hintText;

        [Header("Trust & Progress")]
        public Slider trustSlider;
        public Slider progressSlider;

        [Header("Timeline Progress (Beat Count)")]
        public bool useTimelineProgress = false;
        public int totalBeatsForTimeline = 0;
        public float secondsPerBeat = 0.5f;

        float _timelineDuration;
        float _timelineElapsed;
        bool _timelinePlaying;

        [Header("Progress Lerp")]
        public float progressLerpSpeed = 5f;

        [Header("Status")]
        public TextMeshProUGUI statusText;
        public Image statusIcon;
        public Color successColor = Color.green;
        public Color failColor = Color.red;

        [Header("Hit / Miss")]
        public TextMeshProUGUI hitText;
        public TextMeshProUGUI missText;

        private RhythmMinigameBase current;

        float _currentProgress01;
        float _targetProgress01;

        // fallback hide mềm nếu em lỡ set hudRoot = gameObject
        private CanvasGroup _softHideGroup;

        // NEW: subscribe retry state
        private bool _subscribedToListen;
        private int _subscribeRetryFrames = 60; // retry ~1s ở 60fps

        private void Awake()
        {
            if (hudRoot == null)
            {
                if (transform.childCount > 0)
                    hudRoot = transform.GetChild(0).gameObject;
            }

            EnsureSoftHideGroupIfNeeded();
        }

        private void Start()
        {
            // NEW: đề phòng ListenManager spawn sau
            TrySubscribeListen();
        }

        private void EnsureSoftHideGroupIfNeeded()
        {
            if (hudRoot == gameObject)
            {
                _softHideGroup = GetComponent<CanvasGroup>();
                if (_softHideGroup == null)
                    _softHideGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            // NEW: không phụ thuộc timing OnEnable nữa
            TrySubscribeListen();
        }

        private void OnDisable()
        {
            UnsubscribeListen();
        }

        private void TrySubscribeListen()
        {
            if (_subscribedToListen) return;
            if (!ListenManager.HasInstance) return;

            ListenManager.Instance.OnRhythmHUDShow += HandleShowPayload;
            ListenManager.Instance.OnRhythmHUDUpdate += HandleUpdatePayload;
            ListenManager.Instance.OnRhythmHUDHide += HandleHidePayload;

            _subscribedToListen = true;
            // Debug.Log("[RhythmHUD] Subscribed to ListenManager");
        }

        private void UnsubscribeListen()
        {
            if (!_subscribedToListen) return;
            if (!ListenManager.HasInstance) { _subscribedToListen = false; return; }

            ListenManager.Instance.OnRhythmHUDShow -= HandleShowPayload;
            ListenManager.Instance.OnRhythmHUDUpdate -= HandleUpdatePayload;
            ListenManager.Instance.OnRhythmHUDHide -= HandleHidePayload;

            _subscribedToListen = false;
        }

        private void HandleShowPayload(ListenManager.RhythmHUDShowPayload payload)
        {
            if (payload == null) return;

            Show();
            SetMinigameTitle(payload.title);

            if (payload.useTimeline)
            {
                useTimelineProgress = true;
                ConfigureTimelineByBeats(payload.totalBeatsTimeline, payload.beatDuration);
                StartTimeline();
            }
            else
            {
                useTimelineProgress = false;
                StopTimeline();
            }

            UpdateHitMiss(0, 0);
            SetTrust01(0);
            SetProgress(0);
        }

        private void HandleUpdatePayload(ListenManager.RhythmHUDUpdatePayload payload)
        {
            if (payload == null) return;

            UpdateHitMiss(payload.hit, payload.miss);
            SetTrust01(payload.trust01);

            if (!useTimelineProgress)
                UpdateProgress(payload.progress01);

            SetStatus(payload.statusText, payload.statusPositive);
        }

        private void HandleHidePayload()
        {
            Hide();
        }

        private void Update()
        {
            // NEW: retry subscribe vài frame đầu nếu ListenManager spawn trễ
            if (!_subscribedToListen && _subscribeRetryFrames > 0)
            {
                _subscribeRetryFrames--;
                TrySubscribeListen();
            }

            if (progressSlider == null) return;

            if (useTimelineProgress && _timelinePlaying && _timelineDuration > 0f)
            {
                _timelineElapsed += Time.deltaTime;
                _currentProgress01 = Mathf.Clamp01(_timelineElapsed / _timelineDuration);
            }
            else
            {
                _currentProgress01 = Mathf.MoveTowards(
                    _currentProgress01,
                    _targetProgress01,
                    progressLerpSpeed * Time.deltaTime
                );
            }

            progressSlider.value = _currentProgress01;
        }

        public void Show()
        {
            EnsureSoftHideGroupIfNeeded();

            if (hudRoot == null)
            {
                if (_softHideGroup != null)
                {
                    _softHideGroup.alpha = 1f;
                    _softHideGroup.blocksRaycasts = true;
                    _softHideGroup.interactable = true;
                }
                return;
            }

            if (hudRoot == gameObject)
            {
                _softHideGroup.alpha = 1f;
                _softHideGroup.blocksRaycasts = true;
                _softHideGroup.interactable = true;
            }
            else
            {
                hudRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            EnsureSoftHideGroupIfNeeded();

            if (hudRoot == null)
            {
                if (_softHideGroup != null)
                {
                    _softHideGroup.alpha = 0f;
                    _softHideGroup.blocksRaycasts = false;
                    _softHideGroup.interactable = false;
                }
                _timelinePlaying = false;
                return;
            }

            if (hudRoot == gameObject)
            {
                _softHideGroup.alpha = 0f;
                _softHideGroup.blocksRaycasts = false;
                _softHideGroup.interactable = false;
            }
            else
            {
                hudRoot.SetActive(false);
            }

            _timelinePlaying = false;
        }

        public void ResetHUD()
        {
            Hide();
            SetKeyHints(null);
            SetTrust01(0f);
            _currentProgress01 = 0f;
            _targetProgress01 = 0f;
            _timelineElapsed = 0f;
            _timelineDuration = 0f;
            _timelinePlaying = false;
            if (progressSlider != null) progressSlider.value = 0f;
            SetStatus(string.Empty, true);
            SetHitMiss(0, 0);
        }

        public void BindMinigame(RhythmMinigameBase minigame)
        {
            current = minigame;
            Show();
        }

        public void SetMinigameTitle(string title)
        {
            if (titleText == null) return;
            titleText.text = title;
        }

        public void SetKeyHints(IList<string> hints)
        {
            if (hintText == null) return;
            if (hints == null || hints.Count == 0)
            {
                hintText.text = string.Empty;
                return;
            }
            hintText.text = string.Join(" / ", hints);
        }

        public void SetTrust01(float value01)
        {
            if (trustSlider == null) return;
            trustSlider.value = Mathf.Clamp01(value01);
        }

        public void SetProgress(float value01)
        {
            value01 = Mathf.Clamp01(value01);
            _targetProgress01 = value01;
        }

        public void UpdateProgress(float value01) => SetProgress(value01);
        public void UpdateHitMiss(int hit, int miss) => SetHitMiss(hit, miss);

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

        public void ConfigureTimelineByBeats(int totalBeats, float beatSeconds)
        {
            if (totalBeats <= 0) totalBeats = 1;
            if (beatSeconds <= 0f) beatSeconds = 0.1f;

            totalBeatsForTimeline = totalBeats;
            secondsPerBeat = beatSeconds;
            _timelineDuration = totalBeatsForTimeline * secondsPerBeat;
        }

        public void ConfigureTimelineByDuration(float durationSeconds)
        {
            if (durationSeconds <= 0f) durationSeconds = 0.1f;
            _timelineDuration = durationSeconds;
        }

        public void StartTimeline()
        {
            _timelineElapsed = 0f;
            _timelinePlaying = true;
            _currentProgress01 = 0f;
            if (progressSlider != null)
                progressSlider.value = 0f;
        }

        public void StopTimeline()
        {
            _timelinePlaying = false;
        }

        public void SetBeatWindow(float center01, float halfWidth01) { }
        public void SetBeatPhase(float phase, bool inWindow) { }
        public void PulseKey(int index) { }
        public void ClearPulseKey(int index) { }
        public void SetHoldVisual(float value01) { }
    }
}