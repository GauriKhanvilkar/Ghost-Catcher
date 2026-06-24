using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

namespace GhostCatcher
{
    /// <summary>
    /// Drives the Main Menu UI — player name, create room, join room.
    /// Attach to a Canvas GameObject in the MainMenu scene.
    /// Requires NetworkManager to be present in the scene.
    /// </summary>
    public class MainMenuUI : MonoBehaviourPunCallbacks
    {
        [Header("Panels")]
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject errorPanel;

        [Header("Menu Panel")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("Error Panel")]
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button errorCloseButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text statusText;

        private void Awake()
        {
            // Show connecting panel until Photon is ready
            ShowPanel(connectingPanel);
        }

        private void Start()
        {
            // Load saved player name
            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(savedName))
                playerNameInput.text = savedName;

            // Hook up buttons
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            errorCloseButton.onClick.AddListener(() => ShowPanel(menuPanel));

            // Subscribe to NetworkManager events
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Event_ConnectedToMaster  += HandleConnected;
                NetworkManager.Instance.Event_ConnectionFailed   += HandleConnectionFailed;
                NetworkManager.Instance.Event_JoinRoomFailed     += HandleJoinFailed;
                NetworkManager.Instance.Event_CreateRoomFailed   += HandleCreateFailed;
            }

            // Only skip connecting screen on scene reload
            // (e.g., returning from Lobby — Photon is already fully connected)
            if (PhotonNetwork.IsConnected && PhotonNetwork.InLobby)
                HandleConnected();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Event_ConnectedToMaster  -= HandleConnected;
                NetworkManager.Instance.Event_ConnectionFailed   -= HandleConnectionFailed;
                NetworkManager.Instance.Event_JoinRoomFailed     -= HandleJoinFailed;
                NetworkManager.Instance.Event_CreateRoomFailed   -= HandleCreateFailed;
            }
        }

        // ─── Button Handlers ─────────────────────────────────────────────────

        private void OnCreateRoomClicked()
        {
            if (!ValidateName()) return;

            string code = roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                // Auto-generate a 4-letter room code
                code = GenerateRoomCode();
                roomCodeInput.text = code;
            }

            SetStatus($"Creating room {code}...");
            SetButtonsInteractable(false);
            NetworkManager.Instance.CreateRoom(code);
        }

        private void OnJoinRoomClicked()
        {
            if (!ValidateName()) return;

            string code = roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                ShowError("Please enter a room code to join.");
                return;
            }

            SetStatus($"Joining room {code}...");
            SetButtonsInteractable(false);
            NetworkManager.Instance.JoinRoom(code);
        }

        // ─── NetworkManager Event Handlers ───────────────────────────────────

        private void HandleConnected()
        {
            ShowPanel(menuPanel);
            SetStatus("Connected ✓");
        }

        private void HandleConnectionFailed(string reason)
        {
            ShowError($"Connection failed:\n{reason}");
            ShowPanel(menuPanel);
        }

        private void HandleJoinFailed(string reason)
        {
            SetButtonsInteractable(true);
            ShowError($"Could not join room.\n{reason}");
        }

        private void HandleCreateFailed(string reason)
        {
            SetButtonsInteractable(true);
            ShowError($"Could not create room.\n{reason}");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private bool ValidateName()
        {
            string name = playerNameInput.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowError("Please enter your name.");
                return false;
            }
            PlayerPrefs.SetString("PlayerName", name);
            NetworkManager.Instance.SetPlayerName(name);
            return true;
        }

        private string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[4];
            for (int i = 0; i < 4; i++)
                code[i] = chars[Random.Range(0, chars.Length)];
            return new string(code);
        }

        private void ShowPanel(GameObject panel)
        {
            connectingPanel.SetActive(panel == connectingPanel);
            menuPanel.SetActive(panel == menuPanel);
            errorPanel.SetActive(panel == errorPanel);
        }

        private void ShowError(string message)
        {
            errorText.text = message;
            ShowPanel(errorPanel);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            createRoomButton.interactable = interactable;
            joinRoomButton.interactable   = interactable;
        }
    }
}
