using System.Collections;
using System.Collections.Generic;
using IronIvy.Data;
using UnityEngine;

namespace IronIvy.Gameplay
{
    // plot này chỉ lo phần visual cho cây
    // - không xử lý UI
    // - UI bấm trồng để PlantArea lo
    public class PlantPlot : MonoBehaviour
    {
        [Header("Setup")]
        public Transform cropRoot;
        public float hiddenDepth = -2.0f;
        public float animDuration = 0.5f;

        [Header("Visual References")]
        public GameObject emptyVisual; // visual đất trống ban đầu

        [Header("Highlight")]
        public Renderer[] highlightRenderers;
        public Color highlightColor = new Color(1f, 1f, 1f, 1f);
        public float highlightIntensity = 1.5f;

        [Header("Preview (Ghost)")]
        public Transform previewRoot;                 // nếu null sẽ auto tạo dưới cropRoot
        public Material previewMaterial;              // material trong suốt (URP Transparent / Unlit Transparent)
        [Range(0f, 1f)] public float previewAlpha = 0.35f;
        public int previewStageIndex = 2;             // prefab 3 => index 2 (0-based)

        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private List<GameObject> _spawnedStages = new List<GameObject>();
        private int _currentStageIndex = -1;

        private GameObject _previewObj;
        private List<Renderer> _previewRenderers = new List<Renderer>();

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        // --- PUBLIC API ---

        public void SetHighlighted(bool highlighted)
        {
            if (highlightRenderers == null) return;

            foreach (var r in highlightRenderers)
            {
                if (!r) continue;

                r.GetPropertyBlock(_mpb);

                if (highlighted)
                    _mpb.SetColor(EmissionColor, highlightColor * highlightIntensity);
                else
                    _mpb.SetColor(EmissionColor, Color.black);

                r.SetPropertyBlock(_mpb);
            }
        }

        public void SetPreviewPlant(PlantDefinition plant)
        {
            // preview "cây mờ" để người chơi nhìn trước sẽ trồng gì
            // không ảnh hưởng logic trồng thật
            ClearPreview();

            if (plant == null || plant.stages == null || plant.stages.Count == 0) return;

            EnsurePreviewRoot();

            int idx = Mathf.Clamp(previewStageIndex, 0, plant.stages.Count - 1);
            var prefab = plant.stages[idx].prefab;
            if (!prefab) return;

            _previewObj = Instantiate(prefab, previewRoot);
            _previewObj.transform.localPosition = Vector3.zero;
            _previewObj.transform.localRotation = Quaternion.identity;

            _previewRenderers.Clear();
            _previewObj.GetComponentsInChildren(true, _previewRenderers);

            ApplyPreviewVisual();
        }

        public void ClearPreview()
        {
            if (_previewObj) Destroy(_previewObj);
            _previewObj = null;
            _previewRenderers.Clear();
        }

        public void InitializePlant(PlantDefinition plant)
        {
            // setup plot với cây mới
            // - ẩn đất trống
            // - clear stage cũ
            // - clear preview (khi đã chốt trồng thật)
            ClearPreview();

            if (emptyVisual) emptyVisual.SetActive(false);
            Cleanup();

            if (plant == null || plant.stages == null) return;

            for (int i = 0; i < plant.stages.Count; i++)
            {
                var prefab = plant.stages[i].prefab;
                if (prefab)
                {
                    GameObject go = Instantiate(prefab, cropRoot);

                    // stage đầu để ngay vị trí chuẩn, mấy stage sau giấu xuống dưới
                    if (i == 0) go.transform.localPosition = Vector3.zero;
                    else go.transform.localPosition = new Vector3(0, hiddenDepth, 0);

                    go.transform.localRotation = Quaternion.identity;
                    _spawnedStages.Add(go);
                }
                else
                {
                    // vẫn giữ slot để index không lệch
                    _spawnedStages.Add(null);
                }
            }

            _currentStageIndex = 0;
        }

        public void TransitionToStage(int targetStageIndex)
        {
            // chuyển visual từ stage cũ sang stage mới
            if (targetStageIndex < 0 || targetStageIndex >= _spawnedStages.Count) return;
            if (targetStageIndex == _currentStageIndex) return;

            StartCoroutine(AnimateTransition(_currentStageIndex, targetStageIndex));
            _currentStageIndex = targetStageIndex;
        }

        public void Cleanup()
        {
            // clear toàn bộ cây đang có trên plot
            foreach (var go in _spawnedStages)
            {
                if (go) Destroy(go);
            }

            _spawnedStages.Clear();
            _currentStageIndex = -1;

            // clear preview luôn cho chắc
            ClearPreview();

            // show lại đất trống nếu có
            if (emptyVisual) emptyVisual.SetActive(true);
        }

        public void PlayDisappearVFX(GameObject vfxPrefab)
        {
            // gọi vfx biến mất tại stage hiện tại
            if (_currentStageIndex >= 0 && _currentStageIndex < _spawnedStages.Count)
            {
                var go = _spawnedStages[_currentStageIndex];
                if (go && vfxPrefab)
                {
                    Instantiate(vfxPrefab, go.transform.position, Quaternion.identity);
                }
            }
        }

        // --- PREVIEW INTERNAL ---

        private void EnsurePreviewRoot()
        {
            if (previewRoot != null) return;

            Transform parent = (cropRoot != null) ? cropRoot : transform;
            var go = new GameObject("PreviewRoot");
            go.transform.SetParent(parent, false);
            previewRoot = go.transform;
        }

        private void ApplyPreviewVisual()
        {
            // mục tiêu:
            // - dùng previewMaterial (nếu có) để đảm bảo trong suốt
            // - set alpha nhẹ qua property block (nếu shader có _BaseColor hoặc _Color)
            for (int i = 0; i < _previewRenderers.Count; i++)
            {
                var r = _previewRenderers[i];
                if (!r) continue;

                if (previewMaterial != null)
                {
                    r.material = previewMaterial; // dùng instance cho preview, an toàn
                }
                // set alpha bằng property block cho từng renderer
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);

                Color c = Color.white;

                if (r.sharedMaterial != null)
                {
                    if (r.sharedMaterial.HasProperty("_BaseColor"))
                        c = r.sharedMaterial.GetColor("_BaseColor");
                    else if (r.sharedMaterial.HasProperty("_Color"))
                        c = r.sharedMaterial.GetColor("_Color");
                }

                c.a = previewAlpha;

                if (r.sharedMaterial != null)
                {
                    if (r.sharedMaterial.HasProperty("_BaseColor"))
                        mpb.SetColor("_BaseColor", c);

                    if (r.sharedMaterial.HasProperty("_Color"))
                        mpb.SetColor("_Color", c);
                }

                r.SetPropertyBlock(mpb);
            }
        }

        // --- STAGE ANIMATION ---

        // animate đổi stage
        // - stage cũ trượt xuống dưới
        // - stage mới trồi lên
        private IEnumerator AnimateTransition(int oldIndex, int newIndex)
        {
            GameObject oldObj = (oldIndex >= 0 && oldIndex < _spawnedStages.Count) ? _spawnedStages[oldIndex] : null;
            GameObject newObj = (newIndex >= 0 && newIndex < _spawnedStages.Count) ? _spawnedStages[newIndex] : null;

            float timer = 0f;
            Vector3 zeroPos = Vector3.zero;
            Vector3 hiddenPos = new Vector3(0, hiddenDepth, 0);

            while (timer < animDuration)
            {
                timer += Time.deltaTime;
                float t = timer / animDuration;

                // ease nhẹ cho mượt mượt
                t = t * t * (3f - 2f * t);

                if (oldObj)
                    oldObj.transform.localPosition = Vector3.Lerp(zeroPos, hiddenPos, t);

                if (newObj)
                    newObj.transform.localPosition = Vector3.Lerp(hiddenPos, zeroPos, t);

                yield return null;
            }

            if (oldObj) oldObj.transform.localPosition = hiddenPos;
            if (newObj) newObj.transform.localPosition = zeroPos;
        }
    }
}
