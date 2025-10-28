using UnityEngine;
using System.Collections.Generic;

namespace IronIvy.Managers
{
    using IronIvy.Core;

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private readonly List<ITickable> _tickables = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(ITickable t) { if (!_tickables.Contains(t)) _tickables.Add(t); }
        public void Unregister(ITickable t) { _tickables.Remove(t); }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _tickables.Count; i++)
                _tickables[i].Tick(dt);
        }
    }
}