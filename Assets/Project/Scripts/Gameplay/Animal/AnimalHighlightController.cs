using UnityEngine;

namespace IronIvy.Gameplay.Animals
{
    public class AnimalHighlightController : MonoBehaviour
    {
        [Header("Renderer dùng Unity Toon Shader")]
        [SerializeField] private Renderer[] renderers;

        [Header("Outline properties")]
        [SerializeField] private string outlineWidthProperty = "_Outline_Width"; // Check kỹ tên này trong Shader của bạn
        [SerializeField] private string outlineColorProperty = "_Outline_Color";

        [Header("Settings")]
        [SerializeField] private float offWidth = 0f;
        [SerializeField] private float onWidth = 0.2f; // [Lưu ý] Toon Shader thường cần số to hơn 0.02, hãy thử 0.5 hoặc 1.0 tùy scale model
        [SerializeField] private bool changeColorOnHighlight = true;
        [SerializeField] private Color highlightColor = Color.yellow;

        private MaterialPropertyBlock _mpb;
        private float _defaultWidth;
        private Color _defaultColor;
        private bool _hasDefaultColor;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            _mpb = new MaterialPropertyBlock();
            
            // Lấy giá trị mặc định để backup
            if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
            {
                var mat = renderers[0].sharedMaterial;
                if (mat.HasProperty(outlineWidthProperty)) _defaultWidth = mat.GetFloat(outlineWidthProperty);
                if (mat.HasProperty(outlineColorProperty)) {
                    _defaultColor = mat.GetColor(outlineColorProperty);
                    _hasDefaultColor = true;
                }
            }
        }

        // [FIX] Đổi tên thành SetHighlight để khớp với AnimalController
        public void SetHighlight(bool on)
        {
            if (renderers == null || renderers.Length == 0) return;
// Lazy Init ở đây nếu chưa có thì tạo mới ngay lập tức
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            float targetWidth = on ? onWidth : offWidth;
            Color targetColor = (on && changeColorOnHighlight) ? highlightColor : (_hasDefaultColor ? _defaultColor : Color.white);

            foreach (var r in renderers)
            {
                if (r == null) continue;
                // đảm bảo _mpb chắc chắn không null
                r.GetPropertyBlock(_mpb);
                if (!string.IsNullOrEmpty(outlineWidthProperty))
                    _mpb.SetFloat(outlineWidthProperty, targetWidth);
                
                // Set Width
                _mpb.SetFloat(outlineWidthProperty, targetWidth);
                
                
                // Set Color (nếu có)
                if (_hasDefaultColor) _mpb.SetColor(outlineColorProperty, targetColor);

                r.SetPropertyBlock(_mpb);
            }
        }
        
        // Context menu để test nhanh trong Editor
        [ContextMenu("Test ON")] void TestOn() => SetHighlight(true);
        [ContextMenu("Test OFF")] void TestOff() => SetHighlight(false);
    }
}