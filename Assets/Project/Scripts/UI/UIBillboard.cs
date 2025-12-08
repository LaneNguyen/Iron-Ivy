using UnityEngine;

namespace IronIvy.UI
{
    // Giúp UI WorldSpace luôn quay mặt về phía Camera
    public class UIBillboard : MonoBehaviour
    {
        private Camera _mainCam;

        private void Start()
        {
            _mainCam = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCam == null) return;

            // Quay mặt về phía camera
            transform.LookAt(transform.position + _mainCam.transform.rotation * Vector3.forward,
                             _mainCam.transform.rotation * Vector3.up);
        }
    }
}