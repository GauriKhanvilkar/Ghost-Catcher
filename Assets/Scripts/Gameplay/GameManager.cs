using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace GhostCatcher
{
    /// <summary>
    /// Central game state machine. Runs on all clients but only the
    /// Master Client (host) drives state changes.
    /// Tracks timer, sabotage count, and win/lose conditions.
    /// </summary>
    public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static GameManager Instance { get; private set; }

        // Photon Event Codes
        public const byte EVENT_GHOST_CAUGHT   = 10;
        public const byte EVENT_SABOTAGE_COUNT = 11;
        public const byte EVENT_GAME_OVER      = 12;

        [Header("Settings")]
        [SerializeField] private float gameDuration = Constants.GAME_DURATION;

        // State
        public float   TimeRemaining   { get; private set; }
        public int     SabotageCount   { get; private set; }
        public bool    GameActive       { get; private set; }
        public string  WinnerRole      { get; private set; }

        // Events
        public System.Action<float>  OnTimerUpdated;
        public System.Action<int>    OnSabotageCountChanged;
        public System.Action<string> OnGameOver;  // passes winner role

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()  => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        private void Start()
        {
            TimeRemaining = gameDuration;
            SabotageCount = 0;
            GameActive    = false;

            // Wait for role assignment before starting timer
            if (RoleManager.Instance != null)
                RoleManager.Instance.OnRoleAssigned += _ => StartGame();
            else
                StartGame(); // fallback
        }

        private void Update()
        {
            if (!GameActive) return;
            if (!PhotonNetwork.IsMasterClient) return; // Only host runs timer

            TimeRemaining -= Time.deltaTime;
            OnTimerUpdated?.Invoke(TimeRemaining);

            if (TimeRemaining <= 0f)
                TriggerGameOver(Constants.ROLE_HUNTER); // Timer ran out → Hunters win
        }

        // ─── Game Flow ───────────────────────────────────────────────────────

        private void StartGame()
        {
            GameActive    = true;
            TimeRemaining = gameDuration;
            Debug.Log("[GameManager] Game started!");
        }

        /// <summary>Called when Ghost successfully sabotages an object.</summary>
        public void RegisterSabotage()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            SabotageCount++;
            Debug.Log($"[GameManager] Sabotage count: {SabotageCount}/{Constants.SABOTAGES_TO_WIN}");

            // Broadcast updated count to all clients
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EVENT_SABOTAGE_COUNT, SabotageCount, opts, SendOptions.SendReliable);

            if (SabotageCount >= Constants.SABOTAGES_TO_WIN)
                TriggerGameOver(Constants.ROLE_GHOST);
        }

        /// <summary>Called by ScanAbility when Ghost is caught.</summary>
        public void GhostCaught()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            Debug.Log("[GameManager] Ghost caught!");
            TriggerGameOver(Constants.ROLE_HUNTER);
        }

        private void TriggerGameOver(string winnerRole)
        {
            if (!GameActive) return;
            GameActive = false;

            Debug.Log($"[GameManager] Game over! Winner: {winnerRole}");

            // Notify all clients
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(EVENT_GAME_OVER, winnerRole, opts, SendOptions.SendReliable);
        }

        // ─── Photon Event Receiver ───────────────────────────────────────────

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case EVENT_SABOTAGE_COUNT:
                    SabotageCount = (int)photonEvent.CustomData;
                    OnSabotageCountChanged?.Invoke(SabotageCount);
                    break;

                case EVENT_GAME_OVER:
                    WinnerRole = (string)photonEvent.CustomData;
                    GameActive = false;
                    OnGameOver?.Invoke(WinnerRole);
                    break;
            }
        }
    }
}
