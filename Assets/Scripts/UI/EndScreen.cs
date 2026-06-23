using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// End screen shown to all players when the game concludes.
    /// Reveals the Ghost's identity and shows who won.
    /// </summary>
    public class EndScreen : MonoBehaviourPunCallbacks
    {
        [Header("UI")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text   winnerText;
        [SerializeField] private TMP_Text   ghostRevealText;
        [SerializeField] private TMP_Text   countdownText;
        [SerializeField] private Image      backgroundFlash;
        [SerializeField] private Button     returnToLobbyButton;

        [Header("Colors")]
        [SerializeField] private Color ghostWinColor  = new Color(0.8f, 0.2f, 0.2f, 0.85f);
        [SerializeField] private Color hunterWinColor = new Color(0.2f, 0.4f, 0.9f, 0.85f);

        private float returnCountdown = 10f;
        private bool  isShowing;

        private void Start()
        {
            endPanel?.SetActive(false);

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += ShowEndScreen;

            returnToLobbyButton?.onClick.AddListener(ReturnToLobby);
        }

        private void Update()
        {
            if (!isShowing) return;

            returnCountdown -= Time.deltaTime;
            if (countdownText != null)
                countdownText.text = $"Returning to lobby in {Mathf.CeilToInt(returnCountdown)}s...";

            if (returnCountdown <= 0f)
                ReturnToLobby();
        }

        private void ShowEndScreen(string winnerRole)
        {
            isShowing = true;
            endPanel?.SetActive(true);

            bool ghostWon = winnerRole == Constants.ROLE_GHOST;

            // Winner announcement
            winnerText.text  = ghostWon ? "👻 GHOST WINS!" : "🔵 HUNTERS WIN!";
            winnerText.color = ghostWon ? Color.red : new Color(0.3f, 0.6f, 1f);

            // Background color
            if (backgroundFlash != null)
                backgroundFlash.color = ghostWon ? ghostWinColor : hunterWinColor;

            // Reveal Ghost identity
            string ghostName = FindGhostName();
            ghostRevealText.text = $"The Ghost was: <b>{ghostName}</b>";

            returnCountdown = 10f;
        }

        private string FindGhostName()
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (RoleManager.GetRoleOf(player) == Constants.ROLE_GHOST)
                    return player.NickName;
            }
            return "Unknown";
        }

        private void ReturnToLobby()
        {
            isShowing = false;

            if (PhotonNetwork.IsMasterClient)
            {
                // Re-open room
                PhotonNetwork.CurrentRoom.IsOpen = true;
                PhotonNetwork.LoadLevel(Constants.SCENE_LOBBY);
            }
        }

        public override void OnLeftRoom()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_MENU);
        }
    }
}
