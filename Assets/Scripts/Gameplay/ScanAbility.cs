using System.Collections;
using UnityEngine;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Hunter-only ability. Press F to scan a nearby player or prop.
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

        public void TryScanPlayer(PlayerNetwork targetPlayer)
        {
            if (onCooldown) return;
            if (targetPlayer == null) return;

            targetPlayer.photonView.RPC(
                nameof(RPC_RequestRole),
                targetPlayer.photonView.Owner,
                photonView.ViewID);

            AudioManager.Instance?.PlayScanBeep();
            StartCooldown();
        }

        /// <summary>
        /// Scan a world position (furniture / blended ghost). Master client
        /// checks whether a blended Ghost is actually at that location.
        /// </summary>
        public void TryScanAtPosition(Vector3 worldPosition)
        {
            if (onCooldown) return;

            photonView.RPC(
                nameof(RPC_CheckBlendAtPosition),
                RpcTarget.MasterClient,
                photonView.ViewID,
                worldPosition.x,
                worldPosition.y);

            AudioManager.Instance?.PlayScanBeep();
            StartCooldown();
        }

        [PunRPC]
        private void RPC_RequestRole(int scannerViewID)
        {
            string myRole = RoleManager.Instance?.LocalPlayerRole ?? "";

            PhotonView scannerView = PhotonView.Find(scannerViewID);
            if (scannerView != null)
                scannerView.RPC(nameof(RPC_ReceiveScanResult), scannerView.Owner, myRole);
        }

        [PunRPC]
        private void RPC_CheckBlendAtPosition(int scannerViewID, float x, float y)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            Vector2 scanPos = new Vector2(x, y);
            bool ghostFound = false;

            foreach (var pv in PhotonNetwork.PhotonViewCollection)
            {
                if (pv == null) continue;

                var network = pv.GetComponent<PlayerNetwork>();
                if (network == null || !network.IsBlended) continue;
                if (pv.Owner == null) continue;
                if (RoleManager.GetRoleOf(pv.Owner) != Constants.ROLE_GHOST) continue;

                if (Vector2.Distance(pv.transform.position, scanPos) <= Constants.SCAN_RANGE)
                {
                    ghostFound = true;
                    break;
                }
            }

            PhotonView scannerView = PhotonView.Find(scannerViewID);
            if (scannerView == null) return;

            string result = ghostFound ? Constants.ROLE_GHOST : "PROP";
            scannerView.RPC(nameof(RPC_ReceiveScanResult), scannerView.Owner, result);

            if (ghostFound)
                GameManager.Instance?.GhostCaught();
        }

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

                    if (PhotonNetwork.IsMasterClient)
                        GameManager.Instance?.GhostCaught();
                    else
                        photonView.RPC(nameof(RPC_NotifyGhostCaught), RpcTarget.MasterClient);
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

        [PunRPC]
        private void RPC_NotifyGhostCaught()
        {
            GameManager.Instance?.GhostCaught();
        }

        private IEnumerator ShowPopup(string text, Color color)
        {
            if (scanResultPopup == null) yield break;

            scanResultText.text  = text;
            scanResultText.color = color;
            scanResultPopup.SetActive(true);

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
