using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronIvy.Systems.Camera
{
    // FocusAlphaFader (Unified)
    // - Mode A: Quét collider theo layer trong radius, fade bằng MPB cho Renderer.
    //    + Nếu material Transparent: fade alpha
    //    + Nếu material Opaque (vd TreeWind2): dim RGB để giả lập "mờ"
    // - Mode B: Set global shader params để shadergraph/terrain trees tự fade theo radius (instancing friendly)
    public class FocusAlphaFader : MonoBehaviour
    {
        [Header("Shared Settings")]
        public float blurRadius = 8f;
        [Range(0.05f, 1f)] public float fadedAlpha = 0.25f;
        public float fadeDuration = 0.25f;

        [Header("Mode A - Renderer Scan (Props/Environment)")]
        public bool enableRendererScan = true;
        public LayerMask targetLayers;
        public string urpBaseColorProp = "_BaseColor";
        public string colorProp = "_Color";

        [Header("Opaque Fallback (for shaders like TreeWind2)")]
        [Range(0.05f, 1f)] public float opaqueDimMultiplier = 0.35f;

        [Header("Mode B - Shader Globals (Terrain Trees / ShaderGraph)")]
        public bool enableShaderGlobals = true;
        public string focusEnabledProp = "_FocusEnabled";
        public string focusPosProp = "_FocusPos";
        public string focusRadiusProp = "_FocusRadius";
        public string focusAlphaProp = "_FocusAlpha";

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private bool _active;
        private Transform _focusTarget;

        private readonly List<Renderer> _targets = new List<Renderer>();

        // Cache gốc per renderer
        private class OriginalState
        {
            public string propName;
            public Color color;
            public bool usesAlphaFade; // true: lerp alpha, false: dim RGB
        }

        private readonly Dictionary<Renderer, OriginalState> _original = new Dictionary<Renderer, OriginalState>();
        private MaterialPropertyBlock _mpb;
        private Coroutine _routine;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            // ensure globals off at start
            if (enableShaderGlobals)
                Shader.SetGlobalFloat(focusEnabledProp, 0f);
        }

        private void OnDisable()
        {
            RestoreImmediate();
            SetGlobalsOff();
            _active = false;
            _focusTarget = null;
        }

        public void Activate(Transform focusTarget)
        {
            if (_active) return;
            if (focusTarget == null) return;

            _focusTarget = focusTarget;
            _active = true;

            if (enableShaderGlobals)
                SetGlobalsOn(_focusTarget.position);

            if (enableRendererScan)
            {
                CollectTargets();
                if (_routine != null) StopCoroutine(_routine);
                _routine = StartCoroutine(FadeTo(fadedAlpha));
            }

            if (logDebug)
                Debug.Log("[FocusAlphaFader] Activated.");
        }

        public void Deactivate()
        {
            if (!_active) return;

            if (enableShaderGlobals)
                SetGlobalsOff();

            if (enableRendererScan)
            {
                if (_routine != null) StopCoroutine(_routine);
                _routine = StartCoroutine(FadeBack());
            }
            else
            {
                _active = false;
                _focusTarget = null;
            }

            if (logDebug)
                Debug.Log("[FocusAlphaFader] Deactivate requested.");
        }

        // =========================
        // Shader Globals (Terrain Trees / ShaderGraph)
        // =========================
        private void SetGlobalsOn(Vector3 pos)
        {
            Shader.SetGlobalFloat(focusEnabledProp, 1f);
            Shader.SetGlobalVector(focusPosProp, new Vector4(pos.x, pos.y, pos.z, 1f));
            Shader.SetGlobalFloat(focusRadiusProp, Mathf.Max(0.01f, blurRadius));
            Shader.SetGlobalFloat(focusAlphaProp, Mathf.Clamp01(fadedAlpha));

            if (logDebug)
                Debug.Log($"[FocusAlphaFader] Globals ON pos={pos} r={blurRadius} a={fadedAlpha}");
        }

        private void SetGlobalsOff()
        {
            Shader.SetGlobalFloat(focusEnabledProp, 0f);

            if (logDebug)
                Debug.Log("[FocusAlphaFader] Globals OFF");
        }

        // =========================
        // Renderer Scan + MPB (Props/Env)
        // =========================
        private void CollectTargets()
        {
            _targets.Clear();
            _original.Clear();

            if (_focusTarget == null) return;

            Vector3 center = _focusTarget.position;
            float r = Mathf.Max(0.1f, blurRadius);

            // cần collider để quét
            var cols = Physics.OverlapSphere(center, r, targetLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null) continue;

                var rends = col.GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < rends.Length; k++)
                {
                    var rend = rends[k];
                    if (rend == null) continue;
                    if (_original.ContainsKey(rend)) continue;

                    if (!TryGetColorProperty(rend, out var propName)) continue;

                    var mat = rend.sharedMaterial;
                    if (mat == null) continue;

                    Color baseColor = mat.GetColor(propName);

                    bool alphaWorks = MaterialSupportsTransparency(mat);
                    // Với shader Opaque (vd TreeWind2), ta dim RGB thay vì alpha
                    var state = new OriginalState
                    {
                        propName = propName,
                        color = baseColor,
                        usesAlphaFade = alphaWorks
                    };

                    _original[rend] = state;
                    _targets.Add(rend);
                }
            }

            if (logDebug)
                Debug.Log($"[FocusAlphaFader] Collected renderers={_targets.Count} radius={r}");
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fadeDuration);

                for (int i = 0; i < _targets.Count; i++)
                {
                    var r = _targets[i];
                    if (r == null) continue;
                    if (!_original.TryGetValue(r, out var orig)) continue;

                    ApplyFade(r, orig, p, targetAlpha);
                }

                yield return null;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                var r = _targets[i];
                if (r == null) continue;
                if (!_original.TryGetValue(r, out var orig)) continue;

                ApplyFade(r, orig, 1f, targetAlpha);
            }

            _routine = null;
        }

        private IEnumerator FadeBack()
        {
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fadeDuration);

                // pBack: 0 -> 1 (từ faded -> original)
                for (int i = 0; i < _targets.Count; i++)
                {
                    var r = _targets[i];
                    if (r == null) continue;
                    if (!_original.TryGetValue(r, out var orig)) continue;

                    ApplyUnfade(r, orig, p);
                }

                yield return null;
            }

            RestoreImmediate();

            _active = false;
            _focusTarget = null;
            _routine = null;
        }

        private void RestoreImmediate()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var r = _targets[i];
                if (r == null) continue;
                if (!_original.TryGetValue(r, out var orig)) continue;

                SetColor(r, orig.propName, orig.color);
            }

            _targets.Clear();
            _original.Clear();
        }

        private void ApplyFade(Renderer r, OriginalState orig, float p, float targetAlpha)
        {
            if (orig.usesAlphaFade)
            {
                // Transparent: lerp alpha
                float a = Mathf.Lerp(orig.color.a, targetAlpha, p);
                var c = orig.color;
                c.a = a;
                SetColor(r, orig.propName, c);
            }
            else
            {
                // Opaque (vd TreeWind2): dim RGB, giữ alpha gốc
                float mul = Mathf.Lerp(1f, Mathf.Clamp01(opaqueDimMultiplier), p);
                Color c = orig.color;
                c.r *= mul;
                c.g *= mul;
                c.b *= mul;
                // giữ c.a nguyên
                SetColor(r, orig.propName, c);
            }
        }

        private void ApplyUnfade(Renderer r, OriginalState orig, float pBack)
        {
            // pBack: 0 -> 1
            if (orig.usesAlphaFade)
            {
                // from fadedAlpha back to orig.alpha
                float a = Mathf.Lerp(fadedAlpha, orig.color.a, pBack);
                var c = orig.color;
                c.a = a;
                SetColor(r, orig.propName, c);
            }
            else
            {
                // from dim back to orig RGB
                float mul = Mathf.Lerp(Mathf.Clamp01(opaqueDimMultiplier), 1f, pBack);
                Color c = orig.color;
                c.r *= mul;
                c.g *= mul;
                c.b *= mul;
                SetColor(r, orig.propName, c);
            }
        }

        private bool TryGetColorProperty(Renderer r, out string propName)
        {
            propName = null;
            if (r == null) return false;

            var m = r.sharedMaterial;
            if (m == null) return false;

            if (!string.IsNullOrEmpty(urpBaseColorProp) && m.HasProperty(urpBaseColorProp))
            {
                propName = urpBaseColorProp;
                return true;
            }

            if (!string.IsNullOrEmpty(colorProp) && m.HasProperty(colorProp))
            {
                propName = colorProp;
                return true;
            }

            return false;
        }

        private void SetColor(Renderer r, string propName, Color c)
        {
            if (r == null) return;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(propName, c);
            r.SetPropertyBlock(_mpb);
        }

        private bool MaterialSupportsTransparency(Material mat)
        {
            if (mat == null) return false;

            // Heuristic: Transparent materials typically have higher renderQueue and/or RenderType tag.
            // TreeWind2 has RenderType=Opaque and Queue=Geometry, so it will return false => dim RGB.
            string rt = mat.GetTag("RenderType", false, "");
            if (!string.IsNullOrEmpty(rt) && rt.ToLowerInvariant().Contains("transparent"))
                return true;

            if (mat.renderQueue >= 2500) // Transparent is usually 3000
                return true;

            return false;
        }
    }
}
