using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Core;
using IronIvy.Data;

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

        // Fallback hide nếu vô tình set hudRoot = gameObject
        private CanvasGroup _softHideGroup;

        // Subscribe retry nếu ListenManager spawn trễ
        private bool _subscribedToListen;
        private int _subscribeRetryFrames = 60; // retry ~1s ở 60fps

        [Header("Reaction Presenter (HUD)")]
        [Tooltip("Root của reaction presenter (optional). Nếu null thì vẫn có thể enable/disable reactionImage.")]
        public GameObject reactionRoot;

        [Tooltip("Image hiển thị emoji/sprite reaction của animal trên HUD.")]
        public Image reactionImage;

        [Header("Reaction Tuning (Fallback)")]
        [Tooltip("Số hit liên tiếp (combo) để kích hoạt Happy. Ví dụ 4 nghĩa là hit 4 nhịp liên tục thì Happy bật.")]
        [Min(1)] public int reactionHappyStreakThresholdFallback = 4;

        [Tooltip("Miss reaction (Sad/Angry) giữ trong bao lâu trước khi tự về Neutral. Tính bằng giây.")]
        [Min(0f)] public float reactionMissSecondsFallback = 0.55f;

        [Tooltip("Happy giữ trong bao lâu trước khi tự về Neutral. Tính bằng giây.")]
        [Min(0f)] public float reactionHappyHoldSecondsFallback = 0.65f;

        [Tooltip("Nếu người chơi không hit tiếp trong thời gian này, streak sẽ decay về 0 (combo không bị 'treo'). Tính bằng giây.")]
        [Min(0f)] public float reactionStreakDecaySecondsFallback = 1.25f;

        [Tooltip("Nếu bật: mỗi lần Miss sẽ luân phiên Sad rồi Angry (đa dạng cảm xúc). Nếu tắt: luôn Sad.")]
        public bool reactionAlternateSadAngryFallback = false;

        [Tooltip("Nếu bật: reaction sẽ tự quay về Neutral sau thời gian hold. Nếu tắt: giữ state cho tới khi có event khác.")]
        public bool reactionAutoReturnToNeutral = true;

        [Header("Reaction Zoom")]
        [Tooltip("Scale gốc của reactionImage. Nếu để 0 thì auto lấy scale hiện tại của Image.")]
        public float reactionBaseScale = 0f;

        [Tooltip("Khi state != Neutral, emoji sẽ scale tới giá trị này (1.12 = phóng to 12% so với base).")]
        [Min(1f)] public float reactionActiveScale = 1.12f;

        [Tooltip("Tốc độ lerp scale. Càng lớn thì zoom càng nhanh.")]
        [Min(0f)] public float reactionZoomLerpSpeed = 14f;

        // Reaction context
        private AnimalDefinition _reactionActiveDefinition;

        // Delta hit/miss tracking
        private int _reactionLastHit;
        private int _reactionLastMiss;

        // State machine
        private enum _HUDReactionState { Neutral, Sad, Angry, Happy }
        private _HUDReactionState _reactionState = _HUDReactionState.Neutral;

        private int _reactionStreak;
        private float _reactionLastHitTime;
        private float _reactionStateUntil;
        private bool _reactionFlip;

        // Cached tuning (from definition or fallback)
        private int _reactionHappyThreshold;
        private float _reactionMissSeconds;
        private float _reactionHappyHoldSeconds;
        private float _reactionDecaySeconds;
        private bool _reactionAlternateSadAngry;

        // Cached sprites (from definition)
        private Sprite _sprNeutral;
        private Sprite _sprSad;
        private Sprite _sprAngry;
        private Sprite _sprHappy;

        // Zoom runtime
        private Vector3 _reactionBaseScaleV;
        private Vector3 _reactionTargetScaleV;
        private Vector3 _reactionCurrentScaleV;

        private void Awake()
        {
            if (hudRoot == null)
            {
                if (transform.childCount > 0)
                    hudRoot = transform.GetChild(0).gameObject;
            }

            EnsureSoftHideGroupIfNeeded();

            InitReactionScaleBase();
            ReactionPresenter_ResetHard();
        }

        private void Start()
        {
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

            ReactionPresenter_ResetSoft();
        }

        private void HandleUpdatePayload(ListenManager.RhythmHUDUpdatePayload payload)
        {
            if (payload == null) return;

            UpdateHitMiss(payload.hit, payload.miss);
            SetTrust01(payload.trust01);

            if (!useTimelineProgress)
                UpdateProgress(payload.progress01);

            SetStatus(payload.statusText, payload.statusPositive);

            ReactionPresenter_HandleHudUpdate(payload);
        }

        private void HandleHidePayload()
        {
            Hide();
            ReactionPresenter_ResetHard();
        }

        private void Update()
        {
            // Retry subscribe nếu ListenManager spawn trễ
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

            ReactionPresenter_Tick();
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

            ReactionPresenter_ResetHard();
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

        public void SetReactionPresenterAnimal(AnimalDefinition definition)
        {
            _reactionActiveDefinition = definition;
            ReactionPresenter_CacheFromDefinition(_reactionActiveDefinition);
            ReactionPresenter_ResetSoft();
        }

        public void ClearReactionPresenterAnimal()
        {
            _reactionActiveDefinition = null;
            ReactionPresenter_ResetHard();
        }

        private void InitReactionScaleBase()
        {
            if (reactionImage == null) return;

            float baseS = reactionBaseScale > 0f ? reactionBaseScale : reactionImage.transform.localScale.x;
            _reactionBaseScaleV = Vector3.one * baseS;
            _reactionCurrentScaleV = _reactionBaseScaleV;
            _reactionTargetScaleV = _reactionBaseScaleV;
            reactionImage.transform.localScale = _reactionBaseScaleV;
        }

        private void ReactionPresenter_ResetHard()
        {
            _reactionLastHit = 0;
            _reactionLastMiss = 0;

            _reactionStreak = 0;
            _reactionLastHitTime = 0f;
            _reactionStateUntil = 0f;
            _reactionFlip = false;

            _reactionState = _HUDReactionState.Neutral;

            ReactionPresenter_ApplySprite(null);
            ReactionPresenter_SetRootVisible(false);
            ReactionPresenter_SetScaleTarget(false);
            ReactionPresenter_TickScale(true);
        }

        private void ReactionPresenter_ResetSoft()
        {
            _reactionLastHit = 0;
            _reactionLastMiss = 0;

            _reactionStreak = 0;
            _reactionLastHitTime = Time.time;
            _reactionStateUntil = 0f;
            _reactionFlip = false;

            _reactionState = _HUDReactionState.Neutral;

            var neutral = _sprNeutral;
            ReactionPresenter_ApplySprite(neutral);
            ReactionPresenter_SetRootVisible(neutral != null);
            ReactionPresenter_SetScaleTarget(false);
        }

        private void ReactionPresenter_CacheFromDefinition(AnimalDefinition def)
        {
            // Defaults from inspector
            _reactionHappyThreshold = Mathf.Max(1, reactionHappyStreakThresholdFallback);
            _reactionMissSeconds = Mathf.Max(0f, reactionMissSecondsFallback);
            _reactionHappyHoldSeconds = Mathf.Max(0f, reactionHappyHoldSecondsFallback);
            _reactionDecaySeconds = Mathf.Max(0f, reactionStreakDecaySecondsFallback);
            _reactionAlternateSadAngry = reactionAlternateSadAngryFallback;

            _sprNeutral = null;
            _sprSad = null;
            _sprAngry = null;
            _sprHappy = null;

            if (def == null) return;

            // Sprite pack from definition
            _sprNeutral = def.reactionVisuals.neutral;
            _sprSad = def.reactionVisuals.sad;
            _sprAngry = def.reactionVisuals.angry;
            _sprHappy = def.reactionVisuals.happy;

            // Tuning from definition (if > 0)
            if (def.reactionVisuals.happyStreakThreshold > 0)
                _reactionHappyThreshold = def.reactionVisuals.happyStreakThreshold;

            if (def.reactionVisuals.missReactionSeconds > 0f)
                _reactionMissSeconds = def.reactionVisuals.missReactionSeconds;

            if (def.reactionVisuals.happyHoldSeconds > 0f)
                _reactionHappyHoldSeconds = def.reactionVisuals.happyHoldSeconds;

            if (def.reactionVisuals.streakDecaySeconds > 0f)
                _reactionDecaySeconds = def.reactionVisuals.streakDecaySeconds;

            _reactionAlternateSadAngry = def.reactionVisuals.alternateSadAngry;
        }

        private void ReactionPresenter_HandleHudUpdate(ListenManager.RhythmHUDUpdatePayload payload)
        {
            // Chưa inject definition thì không render reaction
            if (_reactionActiveDefinition == null) return;

            int dh = payload.hit - _reactionLastHit;
            int dm = payload.miss - _reactionLastMiss;

            _reactionLastHit = payload.hit;
            _reactionLastMiss = payload.miss;

            if (dm > 0) ReactionPresenter_OnMiss();
            else if (dh > 0) ReactionPresenter_OnHit();
            else
            {
                // Shield / Rest / tick không scorable -> ignore
            }
        }

        private void ReactionPresenter_OnHit()
        {
            _reactionStreak++;
            _reactionLastHitTime = Time.time;

            if (_reactionStreak >= _reactionHappyThreshold)
            {
                _reactionState = _HUDReactionState.Happy;
                _reactionStateUntil = (_reactionHappyHoldSeconds > 0f) ? (Time.time + _reactionHappyHoldSeconds) : 0f;

                var sprite = _sprHappy != null ? _sprHappy : _sprNeutral;
                ReactionPresenter_ApplySprite(sprite);
                ReactionPresenter_SetRootVisible(sprite != null);

                ReactionPresenter_SetScaleTarget(true);
            }
            else
            {
                // Chưa đủ streak thì giữ neutral để khỏi nhấp nháy
                if (_reactionState != _HUDReactionState.Happy)
                {
                    _reactionState = _HUDReactionState.Neutral;
                    _reactionStateUntil = 0f;

                    ReactionPresenter_ApplySprite(_sprNeutral);
                    ReactionPresenter_SetRootVisible(_sprNeutral != null);

                    ReactionPresenter_SetScaleTarget(false);
                }
            }
        }

        private void ReactionPresenter_OnMiss()
        {
            _reactionStreak = 0;

            _HUDReactionState missState = _HUDReactionState.Sad;
            Sprite missSprite = _sprSad;

            if (_reactionAlternateSadAngry)
            {
                _reactionFlip = !_reactionFlip;
                if (!_reactionFlip)
                {
                    missState = _HUDReactionState.Angry;
                    missSprite = _sprAngry;
                }
            }

            // Fallback nếu thiếu sprite
            if (missSprite == null)
            {
                missSprite = _sprSad != null ? _sprSad : (_sprNeutral != null ? _sprNeutral : null);
                if (missSprite == _sprNeutral) missState = _HUDReactionState.Neutral;
            }

            _reactionState = missState;
            _reactionStateUntil = (_reactionMissSeconds > 0f) ? (Time.time + _reactionMissSeconds) : 0f;

            ReactionPresenter_ApplySprite(missSprite);
            ReactionPresenter_SetRootVisible(missSprite != null);

            ReactionPresenter_SetScaleTarget(missSprite != null && missState != _HUDReactionState.Neutral);
        }

        private void ReactionPresenter_Tick()
        {
            if (_reactionActiveDefinition == null)
            {
                ReactionPresenter_TickScale(false);
                return;
            }

            if (reactionAutoReturnToNeutral)
            {
                // Decay streak nếu nguội
                if (_reactionStreak > 0 && _reactionDecaySeconds > 0f)
                {
                    if (Time.time - _reactionLastHitTime > _reactionDecaySeconds)
                        _reactionStreak = 0;
                }

                // Hết timer state -> về neutral
                if (_reactionState != _HUDReactionState.Neutral && _reactionStateUntil > 0f && Time.time >= _reactionStateUntil)
                {
                    _reactionState = _HUDReactionState.Neutral;
                    _reactionStateUntil = 0f;

                    ReactionPresenter_ApplySprite(_sprNeutral);
                    ReactionPresenter_SetRootVisible(_sprNeutral != null);
                    ReactionPresenter_SetScaleTarget(false);
                }
            }

            ReactionPresenter_TickScale(false);
        }

        private void ReactionPresenter_ApplySprite(Sprite sprite)
        {
            if (reactionImage != null)
                reactionImage.sprite = sprite;
        }

        private void ReactionPresenter_SetRootVisible(bool visible)
        {
            if (reactionRoot != null)
                reactionRoot.SetActive(visible);
            else
            {
                if (reactionImage != null)
                    reactionImage.enabled = visible;
            }
        }

        private void ReactionPresenter_SetScaleTarget(bool isActive)
        {
            if (reactionImage == null) return;

            if (_reactionBaseScaleV == Vector3.zero)
                InitReactionScaleBase();

            float baseS = _reactionBaseScaleV.x;
            float targetS = isActive ? (reactionActiveScale * baseS) : baseS;
            _reactionTargetScaleV = Vector3.one * targetS;
        }

        private void ReactionPresenter_TickScale(bool snap)
        {
            if (reactionImage == null) return;

            if (_reactionBaseScaleV == Vector3.zero)
                InitReactionScaleBase();

            if (snap || reactionZoomLerpSpeed <= 0f)
            {
                _reactionCurrentScaleV = _reactionTargetScaleV;
                reactionImage.transform.localScale = _reactionCurrentScaleV;
                return;
            }

            _reactionCurrentScaleV = Vector3.Lerp(
                _reactionCurrentScaleV,
                _reactionTargetScaleV,
                reactionZoomLerpSpeed * Time.deltaTime
            );

            reactionImage.transform.localScale = _reactionCurrentScaleV;
        }
    }
}
