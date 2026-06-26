using UnityEngine;
using UnityEngine.EventSystems;

namespace GhostCatcher
{
    /// <summary>
    /// Simple virtual joystick for mobile controls.
    /// Attach this to the JoystickBackground Image.
    /// Drag the JoystickHandle child into the Handle field.
    /// Finds and drives PlayerController.SetMoveInput() every frame.
    /// </summary>
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform handle;        // the inner moving circle
        [SerializeField] private RectTransform background;   // the outer static circle

        [Header("Settings")]
        [SerializeField] private float handleRange = 80f;    // max pixels handle can move

        private Vector2      inputVector;
        private PlayerController targetPlayer;

        private void Start()
        {
            if (background == null)
                background = GetComponent<RectTransform>();
        }

        private void Update()
        {
            // Find local player controller if not assigned yet
            if (targetPlayer == null)
                FindLocalPlayer();

            // Send input every frame
            if (targetPlayer != null)
                targetPlayer.SetMoveInput(inputVector);
        }

        // ─── Touch Events ────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Convert screen position to local rect position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            // Clamp within circle
            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, handleRange);

            // Move the handle visual
            if (handle != null)
                handle.anchoredPosition = clampedPoint;

            // Normalize to -1..1
            inputVector = clampedPoint / handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Reset handle to center and stop movement
            inputVector = Vector2.zero;
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;

            if (targetPlayer != null)
                targetPlayer.SetMoveInput(Vector2.zero);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void FindLocalPlayer()
        {
            // Search all PlayerControllers in scene, find the local one
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var pv = pc.GetComponent<Photon.Pun.PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    targetPlayer = pc;
                    break;
                }
            }
        }
    }
}
