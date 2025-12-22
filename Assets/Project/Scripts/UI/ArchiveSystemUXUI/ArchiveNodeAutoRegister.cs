using UnityEngine;

namespace IronIvy.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ArchiveNodeUI))]
    public class ArchiveNodeAutoRegister : MonoBehaviour
    {
        private ArchivePanel _panel;
        private ArchiveNodeUI _node;

        private void Awake()
        {
            _node = GetComponent<ArchiveNodeUI>();
            _panel = GetComponentInParent<ArchivePanel>(true);
        }

        private void OnEnable()
        {
            if (_panel == null)
                _panel = GetComponentInParent<ArchivePanel>(true);

            if (_panel != null && _node != null)
                _panel.RegisterNode(_node);
        }

        private void OnDisable()
        {
            if (_panel != null && _node != null)
                _panel.UnregisterNode(_node);
        }
    }
}
