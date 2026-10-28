using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Handles player movement and interaction input.
    /// Keyboard: WASD / Arrow keys
    /// Mobile:   Joystick calls SetMoveInput(); Interact is hold-to-use.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PhotonView))]
    public class PlayerController : MonoBehaviourPun
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private GameObject     interactPrompt;
        [SerializeField] private GameObject     scanPrompt;
        [SerializeField] private GameObject     blendPrompt;
        [SerializeField] private GameObject     nameTagObject;

        [Header("Walk")]
        [SerializeField] private float walkBobAmplitude = 0.02f;
        [SerializeField] private float walkBobSpeed     = 10f;

        private Rigidbody2D   rb;
        private PlayerNetwork playerNetwork;
        private BlendAbility  blendAbility;

        private Vector2 moveInput;
        private Vector2 joystickInput;
        private bool    isMovementLocked;
        private bool    mobileInteractHeld;
        private Transform bodyTransform;
        private Vector3   bodyBaseLocalPos;
        private float     walkBobTime;
        private SpriteRenderer holdBarRenderer;

        private SabotageObject nearbyObject;
        private PlayerNetwork  nearbyPlayer;
        private Transform      nearbyProp;

        public float InteractHoldProgress => nearbyObject != null ? nearbyObject.HoldProgress : 0f;

        private void Awake()
        {
            rb            = GetComponent<Rigidbody2D>();
            playerNetwork = GetComponent<PlayerNetwork>();
            blendAbility  = GetComponent<BlendAbility>();

            rb.gravityScale   = 0f;
            rb.freezeRotation = true;

            if (spriteRenderer != null)
            {
                bodyTransform    = spriteRenderer.transform;
                bodyBaseLocalPos = bodyTransform.localPosition;
            }
        }

        private void Start()
        {
            SetPromptVisible(interactPrompt, false);
            SetPromptVisible(scanPrompt,     false);
            SetPromptVisible(blendPrompt,    false);

            if (!photonView.IsMine)
            {
                enabled = false;
                return;
            }

            if (blendAbility != null)
                blendAbility.OnBlendStateChanged += OnBlendStateChanged;

            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                    follow.SetTarget(transform);
            }

            CreateHoldBar();
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            ReadMoveInput();
            HandleActionInput();
            HandleInteractHold();
            UpdatePrompts();
            UpdateHoldBar();
            UpdateWalkVisuals();
        }

        private void ReadMoveInput()
        {
            Vector2 keyboardInput = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                float h = 0f, v = 0f;
                if (kb.dKey.isPressed  || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.aKey.isPressed  || kb.leftArrowKey.isPressed)  h -= 1f;
                if (kb.wKey.isPressed  || kb.upArrowKey.isPressed)    v += 1f;
                if (kb.sKey.isPressed  || kb.downArrowKey.isPressed)  v -= 1f;
                keyboardInput = new Vector2(h, v).normalized;
            }

            moveInput = keyboardInput.sqrMagnitude > 0.01f ? keyboardInput : joystickInput;
        }

        private void HandleActionInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.fKey.wasPressedThisFrame) TryScan();
            if (kb.bKey.wasPressedThisFrame) TryBlend();
        }

        private void HandleInteractHold()
        {
            bool keyboardHeld = Keyboard.current != null && Keyboard.current.eKey.isPressed;
            bool holding      = keyboardHeld || mobileInteractHeld;
            bool moving       = moveInput.sqrMagnitude > 0.1f;

            if (nearbyObject == null || moving || !holding)
            {
                nearbyObject?.CancelInteraction();
                return;
            }

            bool isGhost = RoleManager.Instance != null && RoleManager.Instance.IsGhost;
            if (isGhost && nearbyObject.IsNormal)
                nearbyObject.StartSabotage();
            else if (!isGhost && nearbyObject.IsSabotaged)
                nearbyObject.StartRepair();
        }

        private void FixedUpdate()
        {
            if (!photonView.IsMine) return;

            if (!isMovementLocked)
                rb.linearVelocity = moveInput * moveSpeed;
            else
                rb.linearVelocity = Vector2.zero;
        }

        public void OnInteractButton() => mobileInteractHeld = true;
        public void OnScanButton()     => TryScan();
        public void OnBlendButton()    => TryBlend();
        public void SetInteractHeld(bool held)
        {
            mobileInteractHeld = held;
            if (!held)
                nearbyObject?.CancelInteraction();
        }

        public void SetMoveInput(Vector2 input) => joystickInput = input;

        private void TryScan()
        {
            if (RoleManager.Instance != null && RoleManager.Instance.IsGhost) return;

            var scan = GetComponent<ScanAbility>();
            if (scan == null) return;

            if (nearbyPlayer != null && !nearbyPlayer.IsBlended)
            {
                scan.TryScanPlayer(nearbyPlayer);
                return;
            }

            Vector3? scanPoint = null;
            if (nearbyPlayer != null && nearbyPlayer.IsBlended)
                scanPoint = nearbyPlayer.transform.position;
            else if (nearbyProp != null)
                scanPoint = nearbyProp.position;
            else
            {
                var prop = FindNearestProp();
                if (prop != null)
                    scanPoint = prop.position;
                else if (nearbyObject != null)
                    scanPoint = nearbyObject.transform.position;
            }

            if (scanPoint.HasValue)
                scan.TryScanAtPosition(scanPoint.Value);
        }

        private void TryBlend()
        {
            if (RoleManager.Instance == null || !RoleManager.Instance.IsGhost) return;
            blendAbility?.ToggleBlend();
        }

        private void UpdatePrompts()
        {
            bool isGhost  = RoleManager.Instance != null && RoleManager.Instance.IsGhost;
            bool isHunter = !isGhost;

            bool showInteract = nearbyObject != null &&
                                ((isGhost && nearbyObject.IsNormal) ||
                                 (isHunter && nearbyObject.IsSabotaged));
            SetPromptVisible(interactPrompt, showInteract);

            bool nearScanTarget = nearbyPlayer != null || nearbyProp != null || nearbyObject != null || FindNearestProp() != null;
            SetPromptVisible(scanPrompt, isHunter && nearScanTarget);

            bool showBlend = isGhost && nearbyObject != null &&
                             (blendAbility == null || !blendAbility.IsBlended);
            SetPromptVisible(blendPrompt, showBlend);
        }

        private void SetPromptVisible(GameObject prompt, bool visible)
        {
            if (prompt != null)
                prompt.SetActive(visible);
        }

        private void OnBlendStateChanged(bool isBlended)
        {
            isMovementLocked = isBlended;
            if (isBlended && bodyTransform != null)
                bodyTransform.localPosition = bodyBaseLocalPos;
        }

        private void UpdateWalkVisuals()
        {
            if (spriteRenderer == null) return;

            if (!isMovementLocked && Mathf.Abs(moveInput.x) > 0.05f)
            {
                bool flipX = moveInput.x < 0f;
                spriteRenderer.flipX = flipX;
                var outline = playerNetwork != null ? playerNetwork.OutlineRenderer : null;
                if (outline != null)
                    outline.flipX = flipX;
            }

            if (bodyTransform == null) return;

            bool walking = !isMovementLocked && moveInput.sqrMagnitude > 0.01f;
            if (walking)
            {
                walkBobTime += Time.deltaTime * walkBobSpeed;
                float bob = Mathf.Abs(Mathf.Sin(walkBobTime)) * walkBobAmplitude;
                bodyTransform.localPosition = bodyBaseLocalPos + Vector3.up * bob;
            }
            else
            {
                walkBobTime = 0f;
                bodyTransform.localPosition = Vector3.Lerp(
                    bodyTransform.localPosition, bodyBaseLocalPos, Time.deltaTime * 12f);
            }
        }

        private void CreateHoldBar()
        {
            var bar = new GameObject("InteractHoldBar");
            bar.transform.SetParent(transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            holdBarRenderer = bar.AddComponent<SpriteRenderer>();
            holdBarRenderer.sprite = CreateWhiteSprite();
            holdBarRenderer.color  = new Color(1f, 0.85f, 0.2f, 0.9f);
            holdBarRenderer.sortingOrder = 8;
            holdBarRenderer.enabled = false;
        }

        private void UpdateHoldBar()
        {
            if (holdBarRenderer == null) return;

            float progress = InteractHoldProgress;
            bool show = progress > 0.02f && progress < 0.99f;
            holdBarRenderer.enabled = show;
            if (!show) return;

            holdBarRenderer.transform.localScale = new Vector3(progress * 0.6f, 0.04f, 1f);
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        }

        private Transform FindNearestProp()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Constants.INTERACT_RANGE);
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag(Constants.TAG_PROP)) continue;
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearest     = hit.transform;
                    nearestDist = dist;
                }
            }

            return nearest;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(Constants.TAG_SABOTAGE_OBJECT))
                nearbyObject = other.GetComponent<SabotageObject>();

            if (other.CompareTag(Constants.TAG_PROP))
                nearbyProp = other.transform;

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
            {
                var obj = other.GetComponent<SabotageObject>();
                if (obj == nearbyObject)
                {
                    nearbyObject?.CancelInteraction();
                    nearbyObject = null;
                }
            }

            if (other.CompareTag(Constants.TAG_PROP) && nearbyProp == other.transform)
                nearbyProp = null;

            if (other.CompareTag(Constants.TAG_PLAYER))
            {
                var pn = other.GetComponent<PlayerNetwork>();
                if (pn != null && nearbyPlayer == pn)
                    nearbyPlayer = null;
            }
        }
    }
}
