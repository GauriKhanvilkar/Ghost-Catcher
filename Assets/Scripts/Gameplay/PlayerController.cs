using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Handles player movement and interaction input.
    /// Works with both keyboard (WASD) and on-screen joystick via Unity Input System.
    /// Attach to the Player prefab alongside PhotonView.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PhotonView))]
    public class PlayerController : MonoBehaviourPun
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject     interactPrompt;   // [E] label
        [SerializeField] private GameObject     scanPrompt;       // [F] label
        [SerializeField] private GameObject     blendPrompt;      // [B] label
        [SerializeField] private GameObject     nameTagObject;

        // Components
        private Rigidbody2D   rb;
        private PlayerNetwork playerNetwork;
        private BlendAbility  blendAbility;

        // Input
        private Vector2 moveInput;
        private bool    isMovementLocked; // true while blended

        // Nearby interactables
        private SabotageObject nearbyObject;
        private PlayerNetwork  nearbyPlayer;

        // ─── Unity Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            rb            = GetComponent<Rigidbody2D>();
            playerNetwork = GetComponent<PlayerNetwork>();
            blendAbility  = GetComponent<BlendAbility>();

            // Configure Rigidbody for top-down (no gravity)
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        private void Start()
        {
            // Hide prompts initially
            SetPromptVisible(interactPrompt, false);
            SetPromptVisible(scanPrompt,     false);
            SetPromptVisible(blendPrompt,    false);

            // Only control local player
            if (!photonView.IsMine)
            {
                enabled = false;
                return;
            }

            // Subscribe to blend state changes
            if (blendAbility != null)
                blendAbility.OnBlendStateChanged += OnBlendStateChanged;

            // Set up camera to follow this player
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                    follow.SetTarget(transform);
            }
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            HandleInteractInput();
            UpdatePrompts();
        }

        /// <summary>
        /// Cancels any active hold-to-interact if the player moves away
        /// from the object mid-interaction.
        /// </summary>
        private void HandleInteractInput()
        {
            if (nearbyObject == null) return;

            // Cancel interaction if player starts moving while holding
            if (moveInput.sqrMagnitude > 0.1f)
                nearbyObject.CancelInteraction();
        }

        private void FixedUpdate()
        {
            if (!photonView.IsMine) return;

            if (!isMovementLocked)
                rb.linearVelocity = moveInput * moveSpeed;
            else
                rb.linearVelocity = Vector2.zero;
        }

        // ─── Input System Callbacks ──────────────────────────────────────────
        // These are called by Unity's Player Input component

        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        public void OnInteract(InputValue value)
        {
            if (!value.isPressed) return;
            TryInteract();
        }

        public void OnScan(InputValue value)
        {
            if (!value.isPressed) return;
            TryScan();
        }

        public void OnBlend(InputValue value)
        {
            if (!value.isPressed) return;
            TryBlend();
        }

        // ─── Interaction Logic ───────────────────────────────────────────────

        private void TryInteract()
        {
            if (nearbyObject == null) return;

            bool isGhost = RoleManager.Instance != null && RoleManager.Instance.IsGhost;

            if (isGhost && nearbyObject.IsNormal)
                nearbyObject.StartSabotage();
            else if (!isGhost && nearbyObject.IsSabotaged)
                nearbyObject.StartRepair();
        }

        private void TryScan()
        {
            if (RoleManager.Instance != null && RoleManager.Instance.IsGhost) return;

            // Try scan on nearby player first
            if (nearbyPlayer != null)
            {
                GetComponent<ScanAbility>()?.TryScanPlayer(nearbyPlayer);
                return;
            }

            // Try scan on nearby object (could be Ghost blended as prop)
            if (nearbyObject != null)
                GetComponent<ScanAbility>()?.TryScanObject(nearbyObject);
        }

        private void TryBlend()
        {
            if (RoleManager.Instance == null || !RoleManager.Instance.IsGhost) return;

            blendAbility?.ToggleBlend();
        }

        // ─── Prompts ─────────────────────────────────────────────────────────

        private void UpdatePrompts()
        {
            bool isGhost  = RoleManager.Instance != null && RoleManager.Instance.IsGhost;
            bool isHunter = !isGhost;

            // Interact prompt [E]
            bool showInteract = nearbyObject != null &&
                                ((isGhost && nearbyObject.IsNormal) ||
                                 (isHunter && nearbyObject.IsSabotaged));
            SetPromptVisible(interactPrompt, showInteract);

            // Scan prompt [F] — hunters only, near a player or object
            bool showScan = isHunter && (nearbyPlayer != null || nearbyObject != null);
            SetPromptVisible(scanPrompt, showScan);

            // Blend prompt [B] — ghost only, in range of sabotage object, not already blended
            bool showBlend = isGhost && nearbyObject != null &&
                             (blendAbility == null || !blendAbility.IsBlended);
            SetPromptVisible(blendPrompt, showBlend);
        }

        private void SetPromptVisible(GameObject prompt, bool visible)
        {
            if (prompt != null)
                prompt.SetActive(visible);
        }

        // ─── Blend State ─────────────────────────────────────────────────────

        private void OnBlendStateChanged(bool isBlended)
        {
            isMovementLocked = isBlended;
        }

        // ─── Trigger Detection ───────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Constants.TAG_SABOTAGE_OBJECT))
                nearbyObject = other.GetComponent<SabotageObject>();

            if (other.CompareTag(Constants.TAG_PLAYER))
            {
                var pn = other.GetComponent<PlayerNetwork>();
                if (pn != null && !pn.photonView.IsMine)
                    nearbyPlayer = pn;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag(Constants.TAG_SABOTAGE_OBJECT))
                nearbyObject = null;

            if (other.CompareTag(Constants.TAG_PLAYER))
            {
                var pn = other.GetComponent<PlayerNetwork>();
                if (pn != null && nearbyPlayer == pn)
                    nearbyPlayer = null;
            }
        }
    }
}
