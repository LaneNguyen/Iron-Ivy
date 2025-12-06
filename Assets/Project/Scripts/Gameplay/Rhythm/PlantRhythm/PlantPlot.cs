using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using IronIvy.Data;

namespace IronIvy.Gameplay
{
    // PlantPlot MỚI: Chỉ lo visual, KHÔNG lo tương tác UI (UI do PlantArea lo)
    public class PlantPlot : MonoBehaviour
    {
        [Header("Setup")]
        public Transform cropRoot;
        public float hiddenDepth = -2.0f;
        public float animDuration = 0.5f;

        [Header("Visual References")]
        public GameObject emptyVisual; // Đất trống

        private List<GameObject> _spawnedStages = new List<GameObject>();
        private int _currentStageIndex = -1;

        // --- PUBLIC API ---

        public void InitializePlant(PlantDefinition plant)
        {
            if (emptyVisual) emptyVisual.SetActive(false); // Ẩn đất trống
            Cleanup();

            if (plant == null || plant.stages == null) return;

            for (int i = 0; i < plant.stages.Count; i++)
            {
                var prefab = plant.stages[i].prefab;
                if (prefab)
                {
                    GameObject go = Instantiate(prefab, cropRoot);
                    if (i == 0) go.transform.localPosition = Vector3.zero;
                    else go.transform.localPosition = new Vector3(0, hiddenDepth, 0);
                    
                    go.transform.localRotation = Quaternion.identity;
                    _spawnedStages.Add(go);
                }
                else _spawnedStages.Add(null);
            }
            _currentStageIndex = 0;
        }

        public void TransitionToStage(int targetStageIndex)
        {
            if (targetStageIndex < 0 || targetStageIndex >= _spawnedStages.Count) return;
            if (targetStageIndex == _currentStageIndex) return;
            StartCoroutine(AnimateTransition(_currentStageIndex, targetStageIndex));
            _currentStageIndex = targetStageIndex;
        }

        public void Cleanup()
        {
            foreach (var go in _spawnedStages) if (go) Destroy(go);
            _spawnedStages.Clear();
            _currentStageIndex = -1;
            if (emptyVisual) emptyVisual.SetActive(true); // Hiện lại đất trống
        }

        public void PlayDisappearVFX(GameObject vfxPrefab)
        {
            if (_currentStageIndex >= 0 && _currentStageIndex < _spawnedStages.Count)
            {
                var go = _spawnedStages[_currentStageIndex];
                if (go && vfxPrefab) Instantiate(vfxPrefab, go.transform.position, Quaternion.identity);
            }
        }

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
                t = t * t * (3f - 2f * t); 

                if (oldObj) oldObj.transform.localPosition = Vector3.Lerp(zeroPos, hiddenPos, t);
                if (newObj) newObj.transform.localPosition = Vector3.Lerp(hiddenPos, zeroPos, t);
                yield return null;
            }
            if (oldObj) oldObj.transform.localPosition = hiddenPos;
            if (newObj) newObj.transform.localPosition = zeroPos;
        }
    }
}