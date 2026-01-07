using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IronIvy.Core
{
    public class IntroUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject timelineCanvasRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Fade")]
        [SerializeField] private float fadeInTime = 0.2f;
        [SerializeField] private float fadeOutTime = 0.2f;

        [Header("Skip")]
        [SerializeField] private Button skipButton;
        [SerializeField] private bool enableSkip = true;

        private Coroutine _fadeRoutine;

        private void Reset()
        {
            timelineCanvasRoot = gameObject;
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            skipButton = GetComponentInChildren<Button>(true);
        }

        private void Awake()
        {
            if (timelineCanvasRoot == null) timelineCanvasRoot = gameObject;
            if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);

            // Canvas luôn active để luôn nghe event (anti "blocked by inactive GO")
            timelineCanvasRoot.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
                skipButton.onClick.AddListener(OnSkipClicked);

                // enable/disable visual
                skipButton.gameObject.SetActive(enableSkip);

                // extra safety: mặc định không interact khi alpha = 0
                skipButton.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (!ListenManager.HasInstance) return;

            ListenManager.Instance.OnTimelineCanvasShowRequested += HandleShowRequested;
            ListenManager.Instance.OnTimelineCanvasHideRequested += HandleHideRequested;
        }

        private void OnDisable()
        {
            if (!ListenManager.HasInstance) return;

            ListenManager.Instance.OnTimelineCanvasShowRequested -= HandleShowRequested;
            ListenManager.Instance.OnTimelineCanvasHideRequested -= HandleHideRequested;
        }

        private void HandleShowRequested()
        {
            // Safety: nếu ai đó disable root thì bật lại
            if (timelineCanvasRoot != null && !timelineCanvasRoot.activeSelf)
                timelineCanvasRoot.SetActive(true);

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            // Khi show, bật interact/raycast để skip click được
            _fadeRoutine = StartCoroutine(FadeTo(1f, fadeInTime, blockRaycasts: true));

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(enableSkip);
                skipButton.interactable = enableSkip;
            }
        }

        private void HandleHideRequested()
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            // Khi hide, khóa skip ngay lập tức để tránh late click
            if (skipButton != null)
            {
                skipButton.interactable = false;
                // giữ object active/inactive theo enableSkip cũng được, nhưng interact=false là đủ an toàn
            }

            // Không disable root nữa, chỉ fade về 0 và tắt raycast
            _fadeRoutine = StartCoroutine(FadeTo(0f, fadeOutTime, blockRaycasts: false));
        }

        private void OnSkipClicked()
        {
            if (!enableSkip) return;

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseIntroSkipRequested();
        }

        private IEnumerator FadeTo(float target, float duration, bool blockRaycasts)
        {
            if (canvasGroup == null)
                yield break;

            canvasGroup.interactable = blockRaycasts;
            canvasGroup.blocksRaycasts = blockRaycasts;

            float start = canvasGroup.alpha;
            duration = Mathf.Max(0.01f, duration);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                canvasGroup.alpha = Mathf.Lerp(start, target, p);
                yield return null;
            }

            canvasGroup.alpha = target;

            // Sau khi hide xong: đảm bảo click không lọt
            if (Mathf.Approximately(target, 0f))
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _fadeRoutine = null;
        }
    }
}
