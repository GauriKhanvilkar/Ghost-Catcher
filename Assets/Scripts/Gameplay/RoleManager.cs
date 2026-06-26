using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace GhostCatcher
{
    /// <summary>
    /// Manages role assignment (Ghost/Hunter) at game start.
    /// Only runs logic on the Master Client (host).
    /// Roles are stored in Photon Player Custom Properties.
    /// </summary>
    public class RoleManager : MonoBehaviourPunCallbacks
    {
        public static RoleManager Instance { get; private set; }

        // Local player's role — set after assignment
        public string LocalPlayerRole { get; private set; } = "";
        public bool IsGhost  => LocalPlayerRole == Constants.ROLE_GHOST;
        public bool IsHunter => LocalPlayerRole == Constants.ROLE_HUNTER;

        // Event fired when this client's role is confirmed
        public System.Action<string> OnRoleAssigned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Guard: must be in a room and have a PhotonView
            if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[RoleManager] Not in a Photon room — skipping role assignment.");
                return;
            }

            if (photonView == null)
            {
                Debug.LogError("[RoleManager] Missing PhotonView component! " +
                               "Add a PhotonView to the RoleManager GameObject.");
                return;
            }

            // Small delay so all clients finish loading before RPCs are sent
            if (PhotonNetwork.IsMasterClient)
                Invoke(nameof(AssignRoles), 0.5f);
        }

        // ─── Role Assignment (Host Only) ─────────────────────────────────────

        private void AssignRoles()
        {
            Player[] players = PhotonNetwork.PlayerList;

            // Pick one random Ghost
            int ghostIndex = Random.Range(0, players.Length);

            for (int i = 0; i < players.Length; i++)
            {
                string role = (i == ghostIndex) ? Constants.ROLE_GHOST : Constants.ROLE_HUNTER;

                // Send each player their role privately via RPC
                photonView.RPC(nameof(RPC_ReceiveRole), players[i], role);
            }

            Debug.Log($"[RoleManager] Roles assigned. Ghost: {players[ghostIndex].NickName}");
        }

        // ─── RPC — Receive Role ──────────────────────────────────────────────

        [PunRPC]
        private void RPC_ReceiveRole(string role)
        {
            LocalPlayerRole = role;

            // Store in Player Custom Properties so others can verify via Scan
            var props = new ExitGames.Client.Photon.Hashtable
            {
                { Constants.PLAYER_ROLE, role }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            Debug.Log($"[RoleManager] My role: {role}");
            OnRoleAssigned?.Invoke(role);
        }

        // ─── Utility ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the role of any player by reading their Custom Properties.
        /// Used by ScanAbility to check if scanned player is the Ghost.
        /// </summary>
        public static string GetRoleOf(Player player)
        {
            if (player.CustomProperties.TryGetValue(Constants.PLAYER_ROLE, out object role))
                return role.ToString();
            return "";
        }
    }
}
