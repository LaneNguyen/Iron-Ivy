using UnityEngine;

namespace IronIvy.Core
{
    public class MinigameCameraManager : BaseManager<MinigameCameraManager>
    {
        [System.Serializable] public class CameraProfile
        {
            public string id;
            public Transform lookAt;
            public Transform follow;
            public float fov = 40f;
        }

        public CameraProfile plantProfile;
        public CameraProfile animalProfile;

        public void ApplyPlantProfile() { /* TODO: swap Cinemachine or main cam */ }
        public void ApplyAnimalProfile() { /* TODO: swap Cinemachine or main cam */ }
    }
}
