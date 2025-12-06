using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.UI
{
    /// <summary>
    /// HUD tối giản cho rhythm:
    /// - Title
    /// - Hint text (CLICK / HOLD)
    /// - Trust slider
    /// - Progress slider
    /// - Status + Hit/Miss
    /// - Beat window preview cho engine cũ (giờ bỏ dùng)
    /// </summary>
    public class RhythmHUD : MonoBehaviour
    {
        [Header("Root")]
        public GameObject hudRoot;

        [Header("Title")]
        public TextMeshProUGUI titleText;

        [Header("Hint")]
        [Tooltip("Text đơn giản để show hướng dẫn (CLICK / HOLD) nếu muốn.")]
        public TextMeshProUGUI hintText;

        [Header("Trust & Progress")]
        public Slider trustSlider;
        public Slider progressSlider;

        [Header("Status")]
        public TextMeshProUGUI statusText;
        public Image statusIcon;
        public Color successColor = Color.green;
        public Color failColor = Color.red;

        [Header("Hit / Miss")]
        public TextMeshProUGUI hitText;
        public TextMeshProUGUI missText;

        // Giữ reference của engine cũ (V3/V4) để Bind nếu cần
        private RhythmMinigameBase current;

        private void OnEnable()
        {
            // Đã đổi EventBus -> ListenManager
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnMinigameStarted += OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped += OnMinigameStopped;
            }
        }

        private void OnDisable()
        {
            // Đã đổi EventBus -> ListenManager
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnMinigameStarted -= OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped -= OnMinigameStopped;
            }
        }

        //=====================================================
        //  ListenManager callbacks
        //=====================================================

        private void OnMinigameStarted()
        {
            // tìm engine V4 nếu có (Animal / Plant cũ)
            if (current == null)
                current = FindObjectOfType<RhythmMinigameBase>();

            if (hudRoot != null)
                hudRoot.SetActive(true);

            // nếu current khác null thì dùng tên đó, còn không thì giữ lại title đã set sẵn
            if (titleText != null && current != null)
                titleText.text = current.name;
        }

        private void OnMinigameStopped()
        {
            // không auto tắt HUD, để engine mới tự quyết
            // nếu muốn tắt thì uncomment:
            // if (hudRoot != null)
            //     hudRoot.SetActive(false);
        }

        /// <summary>
        /// Cho phép gán minigame thủ công (nếu không muốn rely ListenManager).
        /// </summary>
        public void BindMinigame(RhythmMinigameBase minigame)
        {
            current = minigame;

            if (hudRoot != null)
                hudRoot.SetActive(true);

            if (titleText != null && minigame != null)
                titleText.text = minigame.name;
        }

        /// <summary>
        /// Set hint text đơn giản. Engine mới có thể truyền "CLICK (LMB)" / "HOLD (LMB)".
        /// </summary>
        public void SetKeyHints(IList<string> hints)
        {
            if (hintText == null) return;

            if (hints == null || hints.Count == 0)
            {
                hintText.text = string.Empty;
                return;
            }

            // gộp mấy hint lại cho lẹ
            hintText.text = string.Join(" / ", hints);
        }

        public void SetTrust01(float value01)
        {
            if (trustSlider == null) return;
            trustSlider.value = Mathf.Clamp01(value01);
        }

        public void SetProgress(float value01)
        {
            if (progressSlider == null) return;
            progressSlider.value = Mathf.Clamp01(value01);
        }

        public void SetHoldVisual(float value01)
        {
            // chỗ này trước là fill amount của vòng hold, giờ bỏ visual nên để trống
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

        /// <summary>
        /// Preview vùng hot window trên vòng (engine V4 dùng). Giờ bỏ visual.
        /// </summary>
        public void SetBeatWindow(float center01, float halfWidth01)
        {
            // bỏ trống, giờ không dùng vòng beat chung nữa
        }

        /// <summary>
        /// Phase 0..1 + đang ở trong window. Trước dùng để quay BeatCursor trên HUD.
        /// Giờ BeatCursor đã chuyển sang target riêng nên bỏ trống.
        /// </summary>
        public void SetBeatPhase(float phase, bool inWindow)
        {
            // no-op, giữ cho code cũ compile
        }

        /// <summary>
        /// Highlight 1 key slot (engine cũ). Giờ không xài keySlots nên bỏ trống.
        /// </summary>
        public void PulseKey(int index)
        {
            // no-op
        }

        /// <summary>
        /// Clear highlight key.
        /// </summary>
        public void ClearPulseKey(int index)
        {
            // no-op
        }

        //=====================================================
        //  Convenience helpers cho engine mới
        //=====================================================

        // set title tay cho minigame, dung cho Click-based rhythm moi
        public void SetMinigameTitle(string title)
        {
            if (titleText == null) return;
            titleText.text = title;
        }

        // wrapper cho progress, giu ten de code moi doc de hon
        public void UpdateProgress(float value01)
        {
            SetProgress(value01);
        }

        // wrapper cho hit/miss
        public void UpdateHitMiss(int hit, int miss)
        {
            SetHitMiss(hit, miss);
        }

        // reset nhanh HUD ve trang thai mac dinh
        public void ResetHUD()
        {
            if (hudRoot != null)
                hudRoot.SetActive(false);

            SetKeyHints(null);
            SetTrust01(0f);
            SetProgress(0f);
            SetStatus(string.Empty, true);
            SetHitMiss(0, 0);
        }
    }
}