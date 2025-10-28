using System.Linq;
using UnityEngine;

namespace IronIvy.Gameplay
{
    public class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private float radius = 2.0f;
        [SerializeField] private LayerMask interactableMask = ~0;

        public void TryInteract()
        {
            var hits = Physics.OverlapSphere(transform.position, radius, interactableMask);
            if (hits == null || hits.Length == 0) return;

            var nearest = hits
                .Select(h => h.GetComponentInParent<IInteractable>())
                .Where(i => i != null)
                .OrderBy(i => Vector3.SqrMagnitude(i.WorldPosition - transform.position))
                .FirstOrDefault();

            if (nearest != null)
                nearest.Interact(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}