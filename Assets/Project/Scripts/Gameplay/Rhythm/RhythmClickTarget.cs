using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace IronIvy.Gameplay.Rhythm
{
    // script xử lý 1 cái target để bấm (tap/hold)
    // đặt trên prefab UI
    public class RhythmClickTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("UI")]
        public Image cooldownCircle;
        public Image holdFillCircle;
        public TextMeshProUGUI centerText;

        [Header("Beat Cursor (optional)")]
        public RectTransform beatCursor;
        public float cursorRotationOffsetDeg = 0f;
        public bool cursorClockwise = true;

        [Header("Config (runtime)")]
        public bool isHold;                     // step này là hold hay không
        public float beatDuration = 1f;         // thời gian hết 1 beat
        public float holdRequiredSeconds = 0.4f;
        public bool autoDestroyOnResolve = true;

        private Action<bool> onResolved;        // callback hit/miss
        private float timer = 0f;               // chạy beat
        private float holdTimer = 0f;           // thời gian đang giữ
        private bool pointerDown = false;
        private bool resolved = false;

        // setup trước khi beat chạy
        public void Setup(bool isHoldStep, float beatDur, float holdSec, string label, Action<bool> resolvedCallback)
        {
            isHold = isHoldStep;
            beatDuration = Mathf.Max(0.01f, beatDur);
            holdRequiredSeconds = Mathf.Max(0.01f, holdSec);
            onResolved = resolvedCallback;

            if (centerText != null)
                centerText.text = label;

            // cooldown circle chạy từ 1 về 0
            if (cooldownCircle != null)
            {
                cooldownCircle.type = Image.Type.Filled;
                cooldownCircle.fillMethod = Image.FillMethod.Radial360;
                cooldownCircle.fillOrigin = (int)Image.Origin360.Top;
                cooldownCircle.fillAmount = 1f;
            }

            // hold fill: tắt ban đầu
            if (holdFillCircle != null)
            {
                holdFillCircle.type = Image.Type.Filled;
                holdFillCircle.fillMethod = Image.FillMethod.Radial360;
                holdFillCircle.fillOrigin = (int)Image.Origin360.Top;
                holdFillCircle.fillAmount = 0f;
                holdFillCircle.enabled = false;   // không vẽ gì khi chưa giữ
            }

            timer = 0f;
            holdTimer = 0f;
            pointerDown = false;
            resolved = false;
        }

        private void Update()
        {
            if (resolved) return;

            timer += Time.deltaTime;
            float t01 = Mathf.Clamp01(timer / Mathf.Max(beatDuration, 0.0001f));

            // cooldown chạy ngược
            if (cooldownCircle != null)
                cooldownCircle.fillAmount = 1f - t01;

            // cursor quay theo phase
            if (beatCursor != null)
            {
                float dir = cursorClockwise ? -1f : 1f;
                float angle = dir * t01 * 360f + cursorRotationOffsetDeg;
                beatCursor.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // hết beat mà chưa resolve thì tính là miss
            if (timer >= beatDuration && !resolved)
            {
                Resolve(false);
                return;
            }

            // xử lý hold khi đang giữ
            if (isHold && pointerDown && !resolved)
            {
                holdTimer += Time.deltaTime;

                if (holdFillCircle != null)
                {
                    if (!holdFillCircle.enabled)
                        holdFillCircle.enabled = true;

                    float hold01 = Mathf.Clamp01(holdTimer / Mathf.Max(holdRequiredSeconds, 0.0001f));
                    holdFillCircle.fillAmount = hold01;
                }

                if (holdTimer >= holdRequiredSeconds)
                {
                    Resolve(true);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (resolved) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (isHold)
            {
                pointerDown = true;

                // bật vòng trắng khi người chơi bắt đầu giữ
                if (holdFillCircle != null && !holdFillCircle.enabled)
                {
                    holdFillCircle.enabled = true;
                    holdFillCircle.fillAmount = 0f;
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pointerDown = false;
            // không reset để người chơi thấy mình giữ được bao nhiêu
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (resolved) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (!isHold)
            {
                // tap thì click là xong luôn
                Resolve(true);
            }
        }

        private void Resolve(bool hit)
        {
            if (resolved) return;
            resolved = true;

            try
            {
                onResolved?.Invoke(hit);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (autoDestroyOnResolve)
            {
                Destroy(gameObject);
            }
            else
            {
                if (cooldownCircle != null) cooldownCircle.raycastTarget = false;
                if (centerText != null) centerText.raycastTarget = false;
            }
        }
    }
}
