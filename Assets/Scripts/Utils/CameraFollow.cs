using UnityEngine;

namespace GhostCatcher
{
    /// <summary>
    /// Smoothly follows a target transform.
    /// Attach to the Main Camera in the Game scene.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset    = new Vector3(0, 0, -10f);

        private Transform target;

        public void SetTarget(Transform t) => target = t;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired  = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }
    }
}
