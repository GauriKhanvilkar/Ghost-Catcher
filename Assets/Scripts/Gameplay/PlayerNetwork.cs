using UnityEngine;
using TMPro;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Handles network synchronization for the player:
    /// - Position and facing direction sync via PhotonTransformView
    /// - Blend state sync (sprite swap) via RPC
    /// - Name tag display
    /// Attach to the Player prefab alongside PhotonView + PhotonTransformView.
    /// </summary>
    public class PlayerNetwork : MonoBehaviourPun, IPunObservable
    {
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer outlineRenderer; // Ghost-only blue outline
        public SpriteRenderer OutlineRenderer => outlineRenderer;
        [SerializeField] private TMP_Text       nameTagText;
        [SerializeField] private GameObject     nameTagObject;

        [Header("Sprites")]
        [SerializeField] private Sprite defaultSprite;

        // Blend state — synced over network (set on ALL clients via RPC)
        public bool IsBlended { get; private set; }
        private Color  originalColor;
        private Vector3 bodyBaseScale = Vector3.one;

        [Header("Remote Interpolation")]
        [SerializeField] private float remoteSmoothTime = 0.08f;

        private Vector3 networkPosition;
        private Vector2 networkVelocity;
        private Vector3 remoteMoveVelocity;
        private Rigidbody2D rb;

        private void Awake()
        {
            originalColor = bodyRenderer.color;
            networkPosition = transform.position;
            rb = GetComponent<Rigidbody2D>();
            if (bodyRenderer != null)
                bodyBaseScale = bodyRenderer.transform.localScale;
        }

        private void Start()
        {
            // Set name tag
            if (nameTagText != null)
                nameTagText.text = photonView.Owner?.NickName ?? "Player";

            // Show outline only to local Ghost player
            if (outlineRenderer != null)
                outlineRenderer.enabled = false;

            if (photonView.IsMine)
                SetupLocalPlayer();
        }

        private void Update()
        {
            if (photonView.IsMine) return;

            Vector3 target = networkPosition;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                target,
                ref remoteMoveVelocity,
                remoteSmoothTime);
        }

        private void SetupLocalPlayer()
        {
            // Local player: listen for role to set up outline
            if (RoleManager.Instance != null)
                RoleManager.Instance.OnRoleAssigned += OnRoleAssigned;
        }

        private void OnRoleAssigned(string role)
        {
            // Show blue outline only to Ghost on their own screen
            if (role == Constants.ROLE_GHOST && outlineRenderer != null)
            {
                outlineRenderer.enabled = true;
                outlineRenderer.color   = new Color(0.2f, 0.5f, 1f, 0.6f);
            }
        }

        // ─── Blend Visuals ───────────────────────────────────────────────────

        /// <summary>
        /// Called by BlendAbility to sync sprite change over network.
        /// </summary>
        [PunRPC]
        public void RPC_SetBlendSprite(bool blended, int spriteIndex)
        {
            IsBlended = blended;

            if (blended)
            {
                Sprite propSprite = PropSpriteRegistry.GetSprite(spriteIndex);
                if (propSprite != null && bodyRenderer != null)
                {
                    bodyRenderer.sprite = propSprite;
                    FitBodyScaleToSprite(propSprite);
                }

                if (nameTagObject != null)
                    nameTagObject.SetActive(false);
            }
            else
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.sprite = defaultSprite;
                    bodyRenderer.color  = originalColor;
                    bodyRenderer.transform.localScale = bodyBaseScale;
                }

                if (nameTagObject != null)
                    nameTagObject.SetActive(true);

                if (gameObject.activeInHierarchy)
                    StartCoroutine(ShimmerEffect());
            }
        }

        /// <summary>
        /// Blend sprites (Kenney tiles) are much smaller than the player art.
        /// Scale the body so the disguise matches the player's on-screen size.
        /// </summary>
        private void FitBodyScaleToSprite(Sprite sprite)
        {
            if (bodyRenderer == null || defaultSprite == null || sprite == null) return;

            float defaultSize = Mathf.Max(defaultSprite.bounds.size.x, defaultSprite.bounds.size.y);
            float spriteSize  = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (spriteSize < 0.0001f) return;

            float factor = defaultSize / spriteSize;
            bodyRenderer.transform.localScale = bodyBaseScale * factor;
        }

        private System.Collections.IEnumerator ShimmerEffect()
        {
            float duration = 0.5f;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.PingPong(elapsed * 8f, 1f);
                bodyRenderer.color = Color.Lerp(originalColor, Color.white, t);
                yield return null;
            }

            bodyRenderer.color = originalColor;
        }

        // ─── IPunObservable — Position Sync ──────────────────────────────────

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(rb != null ? rb.linearVelocity : Vector2.zero);
                stream.SendNext(bodyRenderer != null && bodyRenderer.flipX);
            }
            else
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkVelocity = (Vector2)stream.ReceiveNext();
                bool flipX      = (bool)stream.ReceiveNext();
                if (bodyRenderer != null)
                    bodyRenderer.flipX = flipX;
                if (outlineRenderer != null)
                    outlineRenderer.flipX = flipX;

                float lag = Mathf.Clamp((float)(PhotonNetwork.Time - info.SentServerTime), 0f, 0.25f);
                networkPosition += (Vector3)(networkVelocity * lag);
            }
        }
    }
}
