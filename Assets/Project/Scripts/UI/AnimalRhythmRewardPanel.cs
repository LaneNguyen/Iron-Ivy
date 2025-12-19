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

        [Header("Icon (animal)")]
        public Image animalIcon;

        [Header("Debug")]
        [SerializeField] private bool logIconDebug = true;

        private AnimalController _currentAnimal;
        private Coroutine _trustAnimRoutine;
        private Coroutine _archiveAnimRoutine;

        private void Awake()
        {
            if (root == null) root = gameObject;

            // auto-find nhẹ nếu quên assign (không bắt buộc)
            if (animalIcon == null)
            {
                var imgs = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < imgs.Length; i++)
                {
                    if (imgs[i] != null && imgs[i].name.ToLowerInvariant().Contains("animal") && imgs[i].name.ToLowerInvariant().Contains("icon"))
                    {
                        animalIcon = imgs[i];
                        break;
                    }
                }
            }

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

            // mở panel + fill các số liệu
            ShowAnimalRhythmResult(
                payload.animal,
                payload.successRatio,
                payload.archiveGained,
                payload.lootItem,
                payload.lootCount,
                payload.hit,
                payload.miss
            );

            // --- ICON: ưu tiên snapshot từ payload (đã chụp từ AnimalDefinition.icon ở ListenManager) ---
            Sprite icon = payload.animalIcon;

            if (icon == null && payload.animalDefinition != null)
                icon = payload.animalDefinition.icon;

            if (icon == null && payload.animal != null && payload.animal.Definition != null)
                icon = payload.animal.Definition.icon;

            ApplyAnimalIcon(icon, payload);

            // name snapshot (chống animal despawn trước khi UI ăn event)
            if (animalNameText != null && !string.IsNullOrEmpty(payload.animalDisplayName))
                animalNameText.text = payload.animalDisplayName;
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

            // reset icon mỗi lần mở để tránh giữ state cũ
            ApplyAnimalIcon(null, null);

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
        }

        private void ApplyAnimalIcon(Sprite sprite, ListenManager.RhythmAnimalResultPayload payload)
        {
            if (animalIcon == null)
            {
                if (logIconDebug)
                    Debug.LogWarning("[AnimalRhythmRewardPanel] animalIcon reference is NULL. Assign it in prefab/panel.");
                return;
            }

            // detect assign nhầm: animalIcon nằm dưới rewardContainer => slot UI có thể override
            if (rewardContainer != null && animalIcon.transform.IsChildOf(rewardContainer))
            {
                Debug.LogWarning("[AnimalRhythmRewardPanel] animalIcon is under rewardContainer. This is likely WRONG reference (item slot may override). Please assign a dedicated Image for animal icon.");
            }

            if (sprite != null)
            {
                animalIcon.sprite = sprite;
                animalIcon.enabled = true;

                // fix “set rồi nhưng không thấy”
                if (!animalIcon.gameObject.activeInHierarchy)
                    animalIcon.gameObject.SetActive(true);

                var c = animalIcon.color;
                if (c.a <= 0.01f) c.a = 1f;
                animalIcon.color = c;

                var rt = animalIcon.rectTransform;
                if (rt != null && rt.localScale.sqrMagnitude < 0.0001f)
                    rt.localScale = Vector3.one;

                var cg = animalIcon.GetComponentInParent<CanvasGroup>();
                if (cg != null && cg.alpha <= 0.01f)
                    cg.alpha = 1f;
            }
            else
            {
                animalIcon.sprite = null;
                animalIcon.enabled = false;
            }

            if (logIconDebug)
            {
                string name = payload != null ? payload.animalDisplayName : "(no payload)";
                string src =
                    (payload != null && payload.animalIcon != null) ? "payload.animalIcon" :
                    (payload != null && payload.animalDefinition != null && payload.animalDefinition.icon != null) ? "payload.animalDefinition.icon" :
                    "payload.animal.Definition.icon";

                Debug.Log($"[AnimalRhythmRewardPanel] ApplyAnimalIcon name='{name}' sprite={(sprite != null ? sprite.name : "NULL")} source={src} iconGO={animalIcon.gameObject.name} enabled={animalIcon.enabled} alpha={animalIcon.color.a}");
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
            root.SetActive(false);

            if (_currentAnimal != null)
                _currentAnimal.ExecuteQueuedDespawnAfterMinigame();

            _currentAnimal = null;

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmResultClosed();
        }
    }
}
