using UnityEngine;

namespace GhostCatcher
{
    /// <summary>
    /// Smoothly follows a target transform with camera bounds.
    /// Attach to the Main Camera in the Game scene.
    /// 
    /// Setup: Create an empty GameObject "CameraBounds", add a BoxCollider2D (set as Trigger),
    /// resize it to cover your entire map, and drag it into the Bounds Collider field.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset    = new Vector3(0, 0, -10f);

        [Header("Camera Bounds")]
        [Tooltip("Drag a BoxCollider2D that covers your map area. Leave empty for no bounds.")]
        [SerializeField] private BoxCollider2D boundsCollider;

        [Header("Manual Bounds (used if no Bounds Collider)")]
        [SerializeField] private bool useManualBounds = false;
        [SerializeField] private float minX = -20f;
        [SerializeField] private float maxX =  20f;
        [SerializeField] private float minY = -15f;
        [SerializeField] private float maxY =  15f;

        private Transform target;
        private Camera    cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void SetTarget(Transform t) => target = t;

        private void LateUpdate()
        {
            if (target == null) return;

            // Smooth follow
            Vector3 desired = target.position + offset;
            Vector3 smoothed = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);

            // Clamp within bounds
            smoothed = ClampToBounds(smoothed);

            transform.position = smoothed;
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            if (cam == null) return pos;

            // Camera half-size in world units
            float camHalfHeight = cam.orthographicSize;
            float camHalfWidth  = camHalfHeight * cam.aspect;

            float boundsMinX, boundsMaxX, boundsMinY, boundsMaxY;

            if (boundsCollider != null)
            {
                // Get bounds from the BoxCollider2D
                Bounds b = boundsCollider.bounds;
                boundsMinX = b.min.x;
                boundsMaxX = b.max.x;
                boundsMinY = b.min.y;
                boundsMaxY = b.max.y;
            }
            else if (useManualBounds)
            {
                boundsMinX = minX;
                boundsMaxX = maxX;
                boundsMinY = minY;
                boundsMaxY = maxY;
            }
            else
            {
                return pos; // No bounds set
            }

            // Clamp so the camera edges don't go past the map edges
            pos.x = Mathf.Clamp(pos.x, boundsMinX + camHalfWidth,  boundsMaxX - camHalfWidth);
            pos.y = Mathf.Clamp(pos.y, boundsMinY + camHalfHeight, boundsMaxY - camHalfHeight);

            return pos;
        }
    }
}
