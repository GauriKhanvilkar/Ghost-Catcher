using System.Collections;
using UnityEngine;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Hunter-only ability. Press F to scan a nearby player or object.
    /// Reveals if target is Ghost, Hunter, or just a prop.
    /// If Ghost is found → triggers game over.
    /// </summary>
    public class ScanAbility : MonoBehaviourPun
    {
        [Header("UI")]
        [SerializeField] private GameObject scanResultPopup;
        [SerializeField] private TMPro.TMP_Text scanResultText;

        public float CooldownRemaining { get; private set; }
        private bool onCooldown;

        // ─── Public Interface ────────────────────────────────────────────────

        public void TryScanPlayer(PlayerNetwork targetPlayer)
        {
            if (onCooldown) return;
            if (targetPlayer == null) return;

            // Ask the target for their role via RPC
            targetPlayer.photonView.RPC(
                nameof(RPC_RequestRole),
                targetPlayer.photonView.Owner,
                photonView.ViewID);

            StartCooldown();
        }

        public void TryScanObject(SabotageObject targetObject)
        {
            if (onCooldown) return;

            // Check if Ghost is blended here via RPC to all
            targetObject.photonView.RPC(
                nameof(RPC_CheckBlend),
                RpcTarget.MasterClient,
                photonView.ViewID);

            StartCooldown();
        }

        // ─── RPCs ────────────────────────────────────────────────────────────

        /// <summary>Sent to the scanned player — they reply with their role.</summary>
        [PunRPC]
        private void RPC_RequestRole(int scannerViewID)
        {
            string myRole = RoleManager.Instance?.LocalPlayerRole ?? "";

            // Reply to the scanner
            PhotonView scannerView = PhotonView.Find(scannerViewID);
            if (scannerView != null)
                scannerView.RPC(nameof(RPC_ReceiveScanResult), scannerView.Owner, myRole);
        }

        /// <summary>Sent to master client to check if Ghost is blended at this object.</summary>
        [PunRPC]
        private void RPC_CheckBlend(int scannerViewID)
        {
            // Master client checks all players for blend overlap at this object's position
            // For simplicity: check if any Ghost player is within blend range of this object
            bool ghostFound = false;
            int  ghostViewID = -1;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                string role = RoleManager.GetRoleOf(player);
                if (role != Constants.ROLE_GHOST) continue;

                // Find Ghost's PlayerNetwork
                foreach (var pv in PhotonNetwork.PhotonViews)
                {
                    if (pv.Owner == player && pv.GetComponent<BlendAbility>() != null)
                    {
                        var blend = pv.GetComponent<BlendAbility>();
                        if (blend.IsBlended)
                        {
                            ghostFound  = true;
                            ghostViewID = pv.ViewID;
                        }
                        break;
                    }
                }
            }

            PhotonView scannerView = PhotonView.Find(scannerViewID);
            if (scannerView != null)
            {
                string result = ghostFound ? Constants.ROLE_GHOST : "PROP";
                scannerView.RPC(nameof(RPC_ReceiveScanResult), scannerView.Owner, result);

                if (ghostFound)
                    GameManager.Instance?.GhostCaught();
            }
        }

        /// <summary>Called back on the scanner with the result.</summary>
        [PunRPC]
        private void RPC_ReceiveScanResult(string result)
        {
            string displayText;
            Color  displayColor;

            switch (result)
            {
                case Constants.ROLE_GHOST:
                    displayText  = "👻 GHOST!";
                    displayColor = new Color(1f, 0.2f, 0.2f);

                    // Trigger Game Over — Ghost was caught!
                    if (PhotonNetwork.IsMasterClient)
                    {
                        GameManager.Instance?.GhostCaught();
                    }
                    else
                    {
                        // Tell Master Client to end the game
                        photonView.RPC(nameof(RPC_NotifyGhostCaught), RpcTarget.MasterClient);
                    }
                    break;
                case Constants.ROLE_HUNTER:
                    displayText  = "🔵 HUNTER";
                    displayColor = new Color(0.3f, 0.6f, 1f);
                    break;
                default:
                    displayText  = "📦 JUST A PROP";
                    displayColor = new Color(0.8f, 0.8f, 0.8f);
                    break;
            }

            StartCoroutine(ShowPopup(displayText, displayColor));
        }

        /// <summary>Non-host hunter tells host to end the game after catching Ghost.</summary>
        [PunRPC]
        private void RPC_NotifyGhostCaught()
        {
            GameManager.Instance?.GhostCaught();
        }

        // ─── Popup ───────────────────────────────────────────────────────────

        private IEnumerator ShowPopup(string text, Color color)
        {
            if (scanResultPopup == null) yield break;

            scanResultText.text  = text;
            scanResultText.color = color;
            scanResultPopup.SetActive(true);

            // Animate: scale up then fade out
            float duration = 2f;
            float elapsed  = 0f;
            CanvasGroup cg = scanResultPopup.GetComponent<CanvasGroup>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (cg != null)
                    cg.alpha = 1f - Mathf.Clamp01((elapsed - 1f) / 1f);
                yield return null;
            }

            scanResultPopup.SetActive(false);
            if (cg != null) cg.alpha = 1f;
        }

        // ─── Cooldown ────────────────────────────────────────────────────────

        private void StartCooldown()
        {
            StartCoroutine(CooldownTimer());
        }

        private IEnumerator CooldownTimer()
        {
            onCooldown        = true;
            CooldownRemaining = Constants.SCAN_COOLDOWN;

            while (CooldownRemaining > 0f)
            {
                CooldownRemaining -= Time.deltaTime;
                yield return null;
            }

            onCooldown        = false;
            CooldownRemaining = 0f;
        }
    }
}
