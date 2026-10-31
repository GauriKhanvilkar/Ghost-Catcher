using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace GhostCatcher
{
    /// <summary>
    /// Handles connecting to Photon servers, creating and joining rooms.
    /// Attach to a persistent GameObject in the MainMenu scene.
    /// </summary>
    public class NetworkManager : MonoBehaviourPunCallbacks
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private string gameVersion = "1.0";

        // Events the UI can subscribe to (prefixed to avoid clash with PUN callbacks)
        public System.Action Event_ConnectedToMaster;
        public System.Action<string> Event_ConnectionFailed;
        public System.Action Event_JoinedRoom;
        public System.Action<string> Event_JoinRoomFailed;
        public System.Action Event_RoomCreated;
        public System.Action<string> Event_CreateRoomFailed;

        private void Awake()
        {
            // Singleton — persist across scenes
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            PhotonNetwork.AutomaticallySyncScene = true; // Host controls scene loads
            PhotonNetwork.GameVersion = gameVersion;
            Connect();
        }

        // ─── Connection ──────────────────────────────────────────────────────

        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                Debug.Log("[NetworkManager] Already connected.");
                return;
            }
            Debug.Log("[NetworkManager] Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }

        // ─── Room Operations ─────────────────────────────────────────────────

        public void CreateRoom(string roomCode)
        {
            if (!PhotonNetwork.IsConnected) return;

            RoomOptions options = new RoomOptions
            {
                MaxPlayers = 5,
                IsVisible  = true,
                IsOpen     = true,
                CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
                {
                    { Constants.ROOM_GAME_STARTED,   false },
                    { Constants.ROOM_SABOTAGE_COUNT, 0     },
                    { Constants.ROOM_GAME_PHASE,     Constants.PHASE_LOBBY }
                },
                CustomRoomPropertiesForLobby = new string[]
                {
                    Constants.ROOM_GAME_STARTED,
                    Constants.ROOM_GAME_PHASE
                }
            };

            PhotonNetwork.CreateRoom(roomCode.ToUpper(), options);
            Debug.Log($"[NetworkManager] Creating room: {roomCode.ToUpper()}");
        }

        public void JoinRoom(string roomCode)
        {
            if (!PhotonNetwork.IsConnected) return;

            PhotonNetwork.JoinRoom(roomCode.ToUpper());
            Debug.Log($"[NetworkManager] Joining room: {roomCode.ToUpper()}");
        }

        public void LeaveRoom()
        {
            if (!PhotonNetwork.IsConnected) return;
            if (PhotonNetwork.NetworkClientState == ClientState.Leaving) return;
            if (PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
        }

        public void SetPlayerName(string playerName)
        {
            PhotonNetwork.LocalPlayer.NickName = playerName;
        }

        // ─── PUN Callbacks ───────────────────────────────────────────────────

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkManager] Connected to Master server.");
            // Join default lobby so room list is available
            PhotonNetwork.JoinLobby();
            Event_ConnectedToMaster?.Invoke();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[NetworkManager] Disconnected: {cause}");
            Event_ConnectionFailed?.Invoke(cause.ToString());
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkManager] Joined room: {PhotonNetwork.CurrentRoom.Name}");
            Event_JoinedRoom?.Invoke();

            // Load Lobby scene for all players
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel(Constants.SCENE_LOBBY);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[NetworkManager] Join room failed: {message}");
            Event_JoinRoomFailed?.Invoke(message);
        }

        public override void OnCreatedRoom()
        {
            Debug.Log("[NetworkManager] Room created successfully.");
            Event_RoomCreated?.Invoke();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[NetworkManager] Create room failed: {message}");
            Event_CreateRoomFailed?.Invoke(message);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[NetworkManager] {newPlayer.NickName} entered the room.");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[NetworkManager] {otherPlayer.NickName} left the room.");
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Debug.Log($"[NetworkManager] New Master Client: {newMasterClient.NickName}");
        }
    }
}
