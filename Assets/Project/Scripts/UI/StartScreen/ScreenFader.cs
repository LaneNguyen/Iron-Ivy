using UnityEngine;
using System.Collections;

namespace IronIvy.UI
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.5f; // Giảm xuống 0.5s để flash nhanh/mượt hơn

        private bool isFading = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(transform.root.gameObject);
            }
            else Destroy(gameObject);
        }

        public IEnumerator FadeOut()
        {
            // Nếu đang chạy fade thì chờ hoặc reset
            isFading = true;
            canvasGroup.blocksRaycasts = true;
            
            float startAlpha = canvasGroup.alpha;
            float timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1, timer / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1;
            isFading = false;
        }

        public IEnumerator FadeIn()
        {
            isFading = true;
            float startAlpha = canvasGroup.alpha;
            float timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, timer / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            isFading = false;
        }
    }
}