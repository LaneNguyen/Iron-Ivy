using UnityEngine;
using UnityEngine.EventSystems;

namespace IronIvy.UI
{
    public class ArchiveZoomPan : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        [Header("Refs")]
        public RectTransform viewport; // khung nhìn (Mask area)
        public RectTransform content;  // thứ sẽ zoom/pan (TreeBackground + NodesContainer)

        [Header("Zoom")]
        public float minScale = 0.6f;
        public float maxScale = 2.2f;
        public float zoomSpeed = 0.15f;

        [Header("Pan")]
        public float panSpeed = 1.0f;

        [Header("Clamp")]
        public bool clampToViewport = true;
        public float clampPadding = 40f;

        private Vector2 _dragStartLocal;
        private Vector2 _contentStartPos;

        private void Reset()
        {
            // auto fill nếu quên
            viewport = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (viewport == null) viewport = GetComponent<RectTransform>();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (viewport == null || content == null) return;

            float wheel = eventData.scrollDelta.y;
            if (Mathf.Abs(wheel) < 0.001f) return;

            float oldScale = content.localScale.x;
            float targetScale = Mathf.Clamp(oldScale * (1f + wheel * zoomSpeed), minScale, maxScale);
            if (Mathf.Abs(targetScale - oldScale) < 0.0001f) return;

            // Zoom around mouse position:
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventData.pressEventCamera, out Vector2 mouseInViewport);

            // Convert that viewport-local point to content-local BEFORE scaling:
            Vector2 pivot = content.pivot;
            Vector2 contentPos = content.anchoredPosition;
            float scaleBefore = oldScale;

            // Find the point in content space that currently lies under the mouse:
            Vector2 localPointInContent = (mouseInViewport - contentPos) / scaleBefore;

            // Apply scale:
            content.localScale = new Vector3(targetScale, targetScale, 1f);

            // After scaling, keep that same content point under mouse:
            Vector2 newContentPos = mouseInViewport - localPointInContent * targetScale;
            content.anchoredPosition = newContentPos;

            ClampContent();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (viewport == null || content == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventData.pressEventCamera, out _dragStartLocal);

            _contentStartPos = content.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (viewport == null || content == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, eventData.pressEventCamera, out Vector2 curLocal);

            Vector2 delta = (curLocal - _dragStartLocal) * panSpeed;
            content.anchoredPosition = _contentStartPos + delta;

            ClampContent();
        }

        private void ClampContent()
        {
            if (!clampToViewport) return;
            if (viewport == null || content == null) return;

            Vector2 vpSize = viewport.rect.size;
            Vector2 ctSize = content.rect.size * content.localScale.x;

            // If content smaller than viewport, keep centered
            Vector2 min = (vpSize - ctSize) * 0.5f;
            Vector2 max = (ctSize - vpSize) * 0.5f;

            // anchoredPosition is centered-like when pivots are 0.5,0.5
            // recommended: set pivot of viewport/content = (0.5,0.5)
            Vector2 pos = content.anchoredPosition;

            float pad = Mathf.Max(0f, clampPadding);

            if (ctSize.x <= vpSize.x)
                pos.x = 0f;
            else
                pos.x = Mathf.Clamp(pos.x, -max.x + pad, max.x - pad);

            if (ctSize.y <= vpSize.y)
                pos.y = 0f;
            else
                pos.y = Mathf.Clamp(pos.y, -max.y + pad, max.y - pad);

            content.anchoredPosition = pos;
        }

        // Optional helper for buttons +/- reset
        public void SetScale(float scale)
        {
            if (content == null) return;
            float s = Mathf.Clamp(scale, minScale, maxScale);
            content.localScale = new Vector3(s, s, 1f);
            ClampContent();
        }

        public void ResetView()
        {
            if (content == null) return;
            content.localScale = Vector3.one;
            content.anchoredPosition = Vector2.zero;
            ClampContent();
        }
    }
}
