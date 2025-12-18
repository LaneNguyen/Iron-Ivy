using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using TMPro;
using UnityEngine;

namespace IronIvy.UI
{
    public class PlantRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts")]
        [Tooltip("Title để designer tự set trong Editor, code không động vào")]
        public TextMeshProUGUI titleText;

        [Tooltip("Chỉ hiển thị số trust (vd: 85). Label / % để object khác lo")]
        public TextMeshProUGUI trustText;

        [Tooltip("Text hiển thị số Perfect (hit)")]
        public TextMeshProUGUI perfectText;

        [Tooltip("Text hiển thị số Miss")]
        public TextMeshProUGUI missText;

        [Header("Reward Message")]
        public TextMeshProUGUI rewardMessageText;
        public string hasRewardMessage = "Bạn nhận được";
        public string noRewardMessage = "Bạn không thu được gì...";

        [Header("Rewards List")]
        public Transform rewardContainer;
        public GameObject itemSlotPrefab;

        [Header("Trust Number Animation")]
        public float trustAnimDuration = 0.6f;
        public float trustStartScale = 0.6f;

        private Coroutine _trustAnimRoutine;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);

            if (trustText != null)
                trustText.transform.localScale = Vector3.one;
        }

        public void Show(Dictionary<FoodItem, int> rewards, int hit, int miss, float trust)
        {
            if (root == null) root = gameObject;
            gameObject.SetActive(true);
            root.SetActive(true);

            if (perfectText != null) perfectText.text = hit.ToString();
            if (missText != null) missText.text = miss.ToString();

            int trustPercent = Mathf.RoundToInt(trust * 100f);
            PlayTrustNumberAnim(trustPercent);

            bool hasReward = false;
            if (rewards != null)
            {
                foreach (var kvp in rewards)
                {
                    if (kvp.Key == null) continue;
                    if (kvp.Value <= 0) continue;
                    hasReward = true;
                    break;
                }
            }

            if (rewardMessageText != null)
                rewardMessageText.text = hasReward ? hasRewardMessage : noRewardMessage;

            if (rewardContainer && itemSlotPrefab)
            {
                for (int i = rewardContainer.childCount - 1; i >= 0; i--)
                    Destroy(rewardContainer.GetChild(i).gameObject);

                if (hasReward && rewards != null)
                {
                    foreach (var kvp in rewards)
                    {
                        FoodItem item = kvp.Key;
                        int amount = kvp.Value;

                        if (item == null || amount <= 0) continue;

                        GameObject slotObj = Instantiate(itemSlotPrefab, rewardContainer);

                        var uiSlot = slotObj.GetComponent<UIItemSlot>();
                        if (uiSlot != null)
                            uiSlot.Setup(item.icon, amount);
                        else
                        {
                            var txt = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                            if (txt != null) txt.text = $"{item.displayName} x{amount}";
                        }
                    }
                }
            }
        }

        private void PlayTrustNumberAnim(int targetValue)
        {
            if (trustText == null) return;

            if (_trustAnimRoutine != null)
                StopCoroutine(_trustAnimRoutine);

            _trustAnimRoutine = StartCoroutine(TrustNumberAnimRoutine(targetValue));
        }

        private IEnumerator TrustNumberAnimRoutine(int targetValue)
        {
            float t = 0f;

            trustText.transform.localScale = Vector3.one * trustStartScale;
            trustText.text = "0%";

            while (t < trustAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / trustAnimDuration);

                int value = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, p));
                trustText.text = value.ToString() + "%";

                float scale = Mathf.Lerp(trustStartScale, 1f, p);
                trustText.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            trustText.text = targetValue.ToString() + "%";
            trustText.transform.localScale = Vector3.one;
            _trustAnimRoutine = null;
        }

        public void Hide()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        // Button Close -> gọi thẳng event hub, bỏ phụ thuộc UIManager
        public void OnClickClose()
        {
            Hide();

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmResultClosed();
        }

        // compat: nhận payload từ ListenManager observer
        public void ShowPlantRhythmResult(ListenManager.RhythmPlantResultPayload payload)
        {
            if (payload == null)
                return;

            Show(payload.rewards, payload.hit, payload.miss, payload.trust01);
        }

    }
}
