using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Gameplay.Animals;
using IronIvy.Data;
using System.Collections;
using IronIvy.Core;

namespace IronIvy.UI
{
    public class AnimalRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts (Title / Name)")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI animalNameText;

        [Header("Trust / Success")]
        public TextMeshProUGUI trustText;

        [Header("Hit / Miss (separate)")]
        public TextMeshProUGUI perfectText;
        public TextMeshProUGUI missText;

        [Header("Archive")]
        public TextMeshProUGUI archiveGainText;
        public float archiveAnimDuration = 0.6f;
        public float archiveStartScale = 0.6f;

        [Header("Reward Message + Loot Slots")]
        public TextMeshProUGUI rewardMessageText;
        public string hasRewardMessage = "Bạn nhận được";
        public string noRewardMessage = "Bạn không thu được gì...";
        public Transform rewardContainer;
        public GameObject itemSlotPrefab;

        [Header("Trust Number Animation")]
        public float trustAnimDuration = 0.6f;
        public float trustStartScale = 0.6f;

        [Header("Icon (optional)")]
        public Image animalIcon;

        private AnimalController _currentAnimal;
        private Coroutine _trustAnimRoutine;
        private Coroutine _archiveAnimRoutine;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.OnRhythmAnimalResult += ShowAnimalRhythmResult;
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.OnRhythmAnimalResult -= ShowAnimalRhythmResult;
        }

        public void ShowAnimalRhythmResult(ListenManager.RhythmAnimalResultPayload payload)
        {
            if (payload == null) return;

            ShowAnimalRhythmResult(
                payload.animal,
                payload.successRatio,
                payload.archiveGained,
                payload.lootItem,
                payload.lootCount,
                payload.hit,
                payload.miss
            );
        }

        public void ShowAnimalRhythmResult(
            AnimalController animal,
            float successRatio,
            float archiveGained,
            FoodItem lootItem,
            int lootCount,
            int hitCount,
            int missCount)
        {
            if (root == null) root = gameObject;
            _currentAnimal = animal;

            root.SetActive(true);

            if (animalNameText != null && animal != null && animal.Definition != null)
                animalNameText.text = animal.Definition.displayName;

            if (perfectText != null) perfectText.text = hitCount.ToString();
            if (missText != null) missText.text = missCount.ToString();

            int percent = Mathf.RoundToInt(successRatio * 100f);

            if (_trustAnimRoutine != null) StopCoroutine(_trustAnimRoutine);
            _trustAnimRoutine = StartCoroutine(AnimateTrustThenArchive(percent, archiveGained));

            bool hasReward = (lootItem != null && lootCount > 0);
            if (rewardMessageText != null)
                rewardMessageText.text = hasReward ? hasRewardMessage : noRewardMessage;

            if (rewardContainer != null && itemSlotPrefab != null)
            {
                foreach (Transform child in rewardContainer) Destroy(child.gameObject);

                if (hasReward)
                {
                    GameObject slotObj = Instantiate(itemSlotPrefab, rewardContainer);
                    var uiSlot = slotObj.GetComponent<UIItemSlot>();
                    if (uiSlot != null) uiSlot.Setup(lootItem.icon, lootCount);
                }
            }

            if (animalIcon != null)
            {
                if (animal != null && animal.Definition != null && animal.Definition.icon != null)
                {
                    animalIcon.sprite = animal.Definition.icon;
                    animalIcon.enabled = true;
                }
                else animalIcon.enabled = false;
            }
        }

        private IEnumerator AnimateTrustThenArchive(int trustPercent, float archiveValue)
        {
            if (trustText != null)
            {
                float t = 0f;
                while (t < trustAnimDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / trustAnimDuration);
                    trustText.text = Mathf.RoundToInt(Mathf.Lerp(0f, trustPercent, p)).ToString() + "%";
                    trustText.transform.localScale = Vector3.one * Mathf.Lerp(trustStartScale, 1f, p);
                    yield return null;
                }
                trustText.text = trustPercent.ToString() + "%";
            }

            if (archiveGainText != null)
            {
                if (_archiveAnimRoutine != null) StopCoroutine(_archiveAnimRoutine);
                _archiveAnimRoutine = StartCoroutine(AnimateArchive(archiveValue));
            }
        }

        private IEnumerator AnimateArchive(float archiveValue)
        {
            float t = 0f;
            while (t < archiveAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / archiveAnimDuration);
                archiveGainText.text = "+" + Mathf.RoundToInt(Mathf.Lerp(0f, archiveValue, p)).ToString();
                archiveGainText.transform.localScale = Vector3.one * Mathf.Lerp(archiveStartScale, 1f, p);
                yield return null;
            }
            archiveGainText.text = "+" + Mathf.RoundToInt(archiveValue).ToString();
        }

        public void OnConfirmButton()
        {
            // 1) đóng panel trước
            root.SetActive(false);

            // 2) chỉ lúc này mới despawn + spawn vfx (Success / Despawn) dựa trên trust đã queue
            if (_currentAnimal != null)
                _currentAnimal.ExecuteQueuedDespawnAfterMinigame();

            _currentAnimal = null;

            // 3) bắn event để hệ thống bật lại ambience/bgm môi trường
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmResultClosed();
        }
    }
}
