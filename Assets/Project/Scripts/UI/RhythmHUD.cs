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

        public void SetProgress(float value01)
        {
            if (progressSlider == null) return;
            progressSlider.value = Mathf.Clamp01(value01);
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

        // reset HUD về state default
        // - tắt root
        // - clear text
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
