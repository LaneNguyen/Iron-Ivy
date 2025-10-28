using UnityEngine;

namespace IronIvy.Core
{

    public interface ITickable
    {
        void Tick(float deltaTime);
    }
}
