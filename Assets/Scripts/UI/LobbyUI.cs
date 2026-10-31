using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

namespace GhostCatcher
{
    /// <summary>
    /// Manages the Lobby scene — shows connected players, ready states,
    /// and allows the host to start the game.
    /// Attach to a Canvas GameObject in the Lobby scene.
    /// </summary>
    public class LobbyUI : MonoBehaviourPunCallbacks
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerEntryPrefab;

        // Tracks spawned player entry UI rows
        private Dictionary<int, GameObject> playerEntries = new Dictionary<int, GameObject>();
        private bool isStartingGame;
        private bool isLeavingRoom;

        private void Start()
        {
            // Show room code
            roomCodeText.text = $"Room Code: <b>{PhotonNetwork.CurrentRoom.Name}</b>";

            // Only host sees Start button
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            startGameButton.onClick.AddListener(OnStartGameClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);

            RefreshPlayerList();
            UpdateStartButton();
        }

        // ─── Button Handlers ─────────────────────────────────────────────────

        private void OnStartGameClicked()
        {
            if (isStartingGame || isLeavingRoom) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (!IsJoinedInRoom()) return;

            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            if (playerCount < Constants.MIN_PLAYERS_TO_START)
            {
                SetStatus($"Need at least {Constants.MIN_PLAYERS_TO_START} players to start.");
                return;
            }

            isStartingGame = true;
            LockLobbyButtons();
            SetStatus("Starting game...");

            PhotonNetwork.CurrentRoom.IsOpen = false;

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { Constants.ROOM_GAME_STARTED, true },
                { Constants.ROOM_GAME_PHASE,   Constants.PHASE_GAME }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

            PhotonNetwork.LoadLevel(Constants.SCENE_GAME);
        }

        private void OnLeaveClicked()
        {
            if (isLeavingRoom) return;

            isLeavingRoom = true;
            isStartingGame = false;
            LockLobbyButtons();
            SetStatus("Leaving...");

            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LeaveRoom();
            else if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
        }

        private static bool IsJoinedInRoom()
        {
            return PhotonNetwork.IsConnected
                && PhotonNetwork.InRoom
                && PhotonNetwork.CurrentRoom != null
                && PhotonNetwork.NetworkClientState == ClientState.Joined;
        }

        private void LockLobbyButtons()
        {
            if (startGameButton != null)
                startGameButton.interactable = false;
            if (leaveButton != null)
                leaveButton.interactable = false;
        }

        // ─── Player List ─────────────────────────────────────────────────────

        private void RefreshPlayerList()
        {
            // Clear existing entries
            foreach (var entry in playerEntries.Values)
                Destroy(entry);
            playerEntries.Clear();

            // Rebuild from current player list
            foreach (var player in PhotonNetwork.PlayerList)
                AddPlayerEntry(player);

            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void AddPlayerEntry(Player player)
        {
            if (playerEntries.ContainsKey(player.ActorNumber)) return;

            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            playerEntries[player.ActorNumber] = entry;

            // Set name label
            TMP_Text nameLabel = entry.GetComponentInChildren<TMP_Text>();
            if (nameLabel != null)
            {
                string hostTag = player.IsMasterClient ? " 👑" : "";
                string youTag  = player.IsLocal ? " (You)" : "";
                nameLabel.text = $"{player.NickName}{hostTag}{youTag}";
            }
        }

        private void RemovePlayerEntry(Player player)
        {
            if (playerEntries.TryGetValue(player.ActorNumber, out GameObject entry))
            {
                Destroy(entry);
                playerEntries.Remove(player.ActorNumber);
            }
        }

        private void UpdatePlayerCount()
        {
            int current = PhotonNetwork.CurrentRoom.PlayerCount;
            int max     = (int)PhotonNetwork.CurrentRoom.MaxPlayers;
            playerCountText.text = $"Players: {current}/{max}";
        }

        private void UpdateStartButton()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (isStartingGame || isLeavingRoom) return;
            if (!IsJoinedInRoom()) return;

            bool canStart = PhotonNetwork.CurrentRoom.PlayerCount >= Constants.MIN_PLAYERS_TO_START;
            startGameButton.interactable = canStart;
            SetStatus(canStart
                ? "Ready to start!"
                : $"Waiting for players... ({PhotonNetwork.CurrentRoom.PlayerCount}/{Constants.MIN_PLAYERS_TO_START} minimum)");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        // ─── PUN Callbacks ───────────────────────────────────────────────────

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            AddPlayerEntry(newPlayer);
            UpdatePlayerCount();
            UpdateStartButton();
            SetStatus($"{newPlayer.NickName} joined!");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            RemovePlayerEntry(otherPlayer);
            UpdatePlayerCount();
            UpdateStartButton();
            SetStatus($"{otherPlayer.NickName} left.");
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            // If original host left, new host gets start button
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            RefreshPlayerList(); // Refresh crown icon
        }

        public override void OnLeftRoom()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_MENU);
        }
    }
}
