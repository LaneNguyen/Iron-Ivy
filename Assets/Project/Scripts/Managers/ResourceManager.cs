using UnityEngine;
using System;

namespace IronIvy.Managers
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public event Action<int> OnScrapChanged;

        [SerializeField] private int scrap;
        public int Scrap => scrap;

        private void Awake()
        {
            if (Instance != this && Instance != null) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        public void AddScrap(int amount)
        {
            scrap = Mathf.Max(0, scrap + amount);
            OnScrapChanged?.Invoke(scrap);
        }

        public bool TrySpendScrap(int amount)
        {
            if (scrap < amount) return false;
            scrap -= amount;
            OnScrapChanged?.Invoke(scrap);
            return true;
        }
    }
}