//using TMPro;
//using UnityEngine;
//using IronIvy.Managers;
//namespace IronIvy.UI
//{
//    public class UIResourceHUD : MonoBehaviour
//    {
//        [SerializeField] private TMP_Text resourceText;

//        private void OnEnable()
//        {
//            if (ResourceManager.Instance != null)
//                ResourceManager.Instance.OnScrapChanged += UpdateText;
//            UpdateText(ResourceManager.Instance != null ? ResourceManager.Instance.Scrap : 0);
//        }

//        private void OnDisable()
//        {
//            if (ResourceManager.Instance != null)
//                ResourceManager.Instance.OnScrapChanged -= UpdateText;
//        }

//        private void UpdateText(int value)
//        {
//            if (resourceText) resourceText.text = $"Scrap: {value}";
//        }
//    }
//}