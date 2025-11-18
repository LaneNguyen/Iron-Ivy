using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.UI
{
    /// <summary>
    /// Rhythm HUD V3
    /// - Title
    /// - Key hints (Tap/Hold)
    /// - Trust slider
    /// - Progress slider
    /// - Beat circle (phase + inWindow)
    /// - Hit/Miss counter
    /// - PulseKey trên nhịp
    /// </summary>
    public class RhythmHUD : MonoBehaviour
    {
        [Header("Root Panel")]
        public GameObject hudRoot;

        [Header("Title")]
        public TextMeshProUGUI titleText;

        [Header("Key Hint Slots")]
        public List<TextMeshProUGUI> keySlots = new List<TextMeshProUGUI>();

        [Header("Trust Slider")]
        public Slider trustSlider;

        [Header("Progress Slider")]
        public Slider progressSlider;

        [Header("Beat Circle")]
        public Image beatCircle;
        public Color beatNormalColor = Color.white;
        public Color beatHotColor = new Color(1f, 0.8f, 0.4f);

        [Header("Status Text")]
        public TextMeshProUGUI statusText;
        public Image statusIcon;
        public Color successColor = Color.green;
        public Color failColor = Color.red;

        [Header("Hit / Miss Counter")]
        public TextMeshProUGUI hitText;
        public TextMeshProUGUI missText;

        [Header("Pulse Key Settings")]
        public float pulseScale = 1.2f;
        public float pulseDuration = 0.1f;
        public Color pulseColor = new Color(1f, 0.85f, 0.4f);

        private RhythmMinigameBase current;
        private Dictionary<int, Vector3> baseScale = new();
        private Dictionary<int, Color> baseColor = new();
        private Coroutine[] pulseCo;

        private void Awake()
        {
            if (hudRoot != null)
                hudRoot.SetActive(false);

            // Cache scale + color
            for (int i = 0; i < keySlots.Count; i++)
            {
                baseScale[i] = keySlots[i].transform.localScale;
                baseColor[i] = keySlots[i].color;
            }

            if (beatCircle != null)
            {
                beatCircle.type = Image.Type.Filled;
                beatCircle.fillMethod = Image.FillMethod.Radial360;
                beatCircle.fillAmount = 0f;
                beatCircle.color = beatNormalColor;
            }
        }

        private void OnEnable()
        {
            EventBus.Instance.OnMinigameStarted += OnMinigameStarted;
            EventBus.Instance.OnMinigameStopped += OnMinigameStopped;
        }

        private void OnDisable()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.OnMinigameStarted -= OnMinigameStarted;
                EventBus.Instance.OnMinigameStopped -= OnMinigameStopped;
            }
        }

        private void OnMinigameStarted()
        {
            current = FindObjectOfType<RhythmMinigameBase>();

            if (hudRoot != null)
                hudRoot.SetActive(true);

            if (titleText != null && current != null)
                titleText.text = current.name;
        }

        private void OnMinigameStopped()
        {
            if (hudRoot != null)
                hudRoot.SetActive(false);

            current = null;
        }

        public void BindMinigame(RhythmMinigameBase m)
        {
            current = m;
            if (hudRoot != null)
                hudRoot.SetActive(true);

            if (titleText != null)
                titleText.text = m.name;
        }

        // ---------- UPDATE HUD ELEMENTS ----------

        public void SetKeyHints(IList<string> hints)
        {
            for (int i = 0; i < keySlots.Count; i++)
            {
                if (keySlots[i] == null) continue;

                if (hints != null && i < hints.Count)
                    keySlots[i].text = hints[i];
                else
                    keySlots[i].text = "";
            }
        }

        public void SetTrust01(float v)
        {
            if (trustSlider != null)
                trustSlider.value = Mathf.Clamp01(v);
        }

        public void SetProgress(float v)
        {
            if (progressSlider != null)
                progressSlider.value = Mathf.Clamp01(v);
        }

        public void SetStatus(string msg, bool good)
        {
            if (statusText != null)
                statusText.text = msg;

            if (statusIcon != null)
                statusIcon.color = good ? successColor : failColor;
        }

        public void SetBeatPhase(float phase, bool inWindow)
        {
            if (beatCircle == null) return;

            beatCircle.fillAmount = Mathf.Clamp01(phase);
            beatCircle.color = inWindow ? beatHotColor : beatNormalColor;
        }

        public void SetHitMiss(int hit, int miss)
        {
            if (hitText != null) hitText.text = "Hit: " + hit;
            if (missText != null) missText.text = "Miss: " + miss;
        }

        // ---------- PULSE KEY ----------

        public void PulseKey(int index)
        {
            if (keySlots == null || index < 0 || index >= keySlots.Count) return;

            if (pulseCo == null || pulseCo.Length != keySlots.Count)
                pulseCo = new Coroutine[keySlots.Count];

            if (pulseCo[index] != null)
                StopCoroutine(pulseCo[index]);

            pulseCo[index] = StartCoroutine(PulseRoutine(index));
        }

        private IEnumerator PulseRoutine(int index)
        {
            var slot = keySlots[index];
            if (slot == null) yield break;

            Vector3 s = baseScale[index];
            Color c = baseColor[index];

            // Pulse
            slot.transform.localScale = s * pulseScale;
            slot.color = pulseColor;

            yield return new WaitForSeconds(pulseDuration);

            slot.transform.localScale = s;
            slot.color = c;

            pulseCo[index] = null;
        }
    }
}
