using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Handles player movement and interaction input.
    /// Keyboard: WASD / Arrow keys (direct Input.GetAxisRaw)
    /// Mobile:   Joystick calls SetMoveInput() directly.
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
        private Vector2 joystickInput;   // set by MobileJoystick.cs
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

            // ── Keyboard input (new Input System) ─────────────────────────
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = 0f, v = 0f;
            if (kb.dKey.isPressed  || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed  || kb.leftArrowKey.isPressed)  h -= 1f;
            if (kb.wKey.isPressed  || kb.upArrowKey.isPressed)    v += 1f;
            if (kb.sKey.isPressed  || kb.downArrowKey.isPressed)  v -= 1f;

            Vector2 keyboardInput = new Vector2(h, v).normalized;

            // Combine keyboard + joystick (whichever is non-zero wins)
            moveInput = keyboardInput.sqrMagnitude > 0.01f ? keyboardInput : joystickInput;

            // ── Action keys ─────────────────────────────────────────────────
            if (kb.eKey.wasPressedThisFrame) TryInteract();
            if (kb.fKey.wasPressedThisFrame) TryScan();
            if (kb.bKey.wasPressedThisFrame) TryBlend();

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

        // ─── Mobile Button Callbacks ─────────────────────────────────────────
        /// <summary>Called by on-screen Interact button (UI Button OnClick).</summary>
        public void OnInteractButton() => TryInteract();

        /// <summary>Called by on-screen Scan button (UI Button OnClick).</summary>
        public void OnScanButton() => TryScan();

        /// <summary>Called by on-screen Blend button (UI Button OnClick).</summary>
        public void OnBlendButton() => TryBlend();

        /// <summary>Called every frame by MobileJoystick with the stick direction.</summary>
        public void SetMoveInput(Vector2 input) => joystickInput = input;

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
