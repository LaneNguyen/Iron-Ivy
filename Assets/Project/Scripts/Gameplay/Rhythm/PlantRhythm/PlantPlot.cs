using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using IronIvy.Data;

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

        private List<GameObject> _spawnedStages = new List<GameObject>();
        private int _currentStageIndex = -1;

        // --- PUBLIC API ---

        public void InitializePlant(PlantDefinition plant)
        {
            // setup plot với cây mới
            // - ẩn đất trống
            // - clear stage cũ
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
