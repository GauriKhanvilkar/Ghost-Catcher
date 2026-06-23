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
        [SerializeField] private TMP_Text       nameTagText;
        [SerializeField] private GameObject     nameTagObject;

        [Header("Sprites")]
        [SerializeField] private Sprite defaultSprite;

        // Blend state — synced over network
        private bool   isBlended;
        private Sprite blendedSprite;
        private Color  originalColor;

        // Smoothed network position
        private Vector3 networkPosition;
        private float   networkLag;

        private void Awake()
        {
            originalColor = bodyRenderer.color;
            networkPosition = transform.position;
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
            // Smooth remote player movement
            if (!photonView.IsMine)
                transform.position = Vector3.MoveTowards(
                    transform.position, networkPosition, Time.deltaTime * 10f);
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
            isBlended = blended;

            if (blended)
            {
                // Switch to prop sprite
                Sprite propSprite = PropSpriteRegistry.GetSprite(spriteIndex);
                if (propSprite != null)
                    bodyRenderer.sprite = propSprite;

                // Hide name tag while blended
                if (nameTagObject != null)
                    nameTagObject.SetActive(false);
            }
            else
            {
                // Restore original sprite
                bodyRenderer.sprite = defaultSprite;
                bodyRenderer.color  = originalColor;

                // Restore name tag
                if (nameTagObject != null)
                    nameTagObject.SetActive(true);

                // Play shimmer effect
                StartCoroutine(ShimmerEffect());
            }
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
                stream.SendNext(transform.localScale); // Flip for facing direction
            }
            else
            {
                networkPosition      = (Vector3)stream.ReceiveNext();
                transform.localScale = (Vector3)stream.ReceiveNext();

                // Estimate lag
                networkLag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
                networkPosition += (Vector3)((Vector2)transform.localScale.normalized * networkLag);
            }
        }
    }
}
