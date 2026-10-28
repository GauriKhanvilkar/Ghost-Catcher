using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// In-game HUD. Shows timer, sabotage count, scan cooldown, blend cooldown.
    /// Subscribes to GameManager and local player ability events.
    /// Attach to the HUD Canvas in the Game scene.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Timer")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Slider   timerFill;

        [Header("Sabotage Counter")]
        [SerializeField] private TMP_Text sabotageText;
        [SerializeField] private Image[]  sabotageIcons; // 3 icons, fill red when sabotaged

        [Header("Role Display")]
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private Image    roleBadge;
        [SerializeField] private Color    ghostColor  = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color    hunterColor = new Color(0.2f, 0.5f, 1f);

        [Header("Scan Cooldown (Hunter only)")]
        [SerializeField] private GameObject scanCooldownGroup;
        [SerializeField] private Slider     scanCooldownFill;
        [SerializeField] private TMP_Text   scanCooldownText;

        [Header("Blend Cooldown (Ghost only)")]
        [SerializeField] private GameObject blendCooldownGroup;
        [SerializeField] private Slider     blendCooldownFill;
        [SerializeField] private TMP_Text   blendCooldownText;
        [SerializeField] private Slider     blendDurationFill;

        [Header("Sabotage Alert")]
        [SerializeField] private GameObject sabotageAlertPanel;
        [SerializeField] private TMP_Text   sabotageAlertText;

        [Header("Mobile Buttons")]
        [SerializeField] private GameObject interactButton;   // always visible
        [SerializeField] private GameObject scanButton;       // Hunter only
        [SerializeField] private GameObject blendButton;      // Ghost only

        private float gameDuration;
        private ScanAbility  scanAbility;
        private BlendAbility blendAbility;
        private int lastTimerSecond = -1;

        private void Start()
        {
            gameDuration = Constants.GAME_DURATION;

            // Subscribe to GameManager events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTimerUpdated        += UpdateTimer;
                GameManager.Instance.OnSabotageCountChanged += UpdateSabotageCount;
                GameManager.Instance.OnGameOver            += _ => gameObject.SetActive(false);
            }

            // Subscribe to RoleManager
            if (RoleManager.Instance != null)
                RoleManager.Instance.OnRoleAssigned += SetupForRole;

            sabotageAlertPanel?.SetActive(false);
        }

        private void Update()
        {
            UpdateCooldownBars();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTimerUpdated         -= UpdateTimer;
                GameManager.Instance.OnSabotageCountChanged -= UpdateSabotageCount;
            }
        }

        // ─── Role Setup ──────────────────────────────────────────────────────

        private void SetupForRole(string role)
        {
            bool isGhost = role == Constants.ROLE_GHOST;

            // Role badge
            roleText.text   = isGhost ? "👻 GHOST" : "🔵 HUNTER";
            roleBadge.color = isGhost ? ghostColor : hunterColor;

            // Show relevant cooldown groups
            scanCooldownGroup?.SetActive(!isGhost);
            blendCooldownGroup?.SetActive(isGhost);

            // Show relevant mobile buttons
            scanButton?.SetActive(!isGhost);   // Hunters get Scan
            blendButton?.SetActive(isGhost);   // Ghost gets Blend
            interactButton?.SetActive(true);   // Everyone gets Interact

            // Find ability components on local player
            foreach (var pv in PhotonNetwork.PhotonViews)
            {
                if (!pv.IsMine) continue;
                
                // Make sure this is actually the Player, not the GameManager/RoleManager
                var playerController = pv.GetComponent<PlayerController>();
                if (playerController == null) continue;

                scanAbility  = pv.GetComponent<ScanAbility>();
                blendAbility = pv.GetComponent<BlendAbility>();

                if (blendAbility != null)
                    blendAbility.OnBlendStateChanged += OnBlendStateChanged;
                    
                break; // Found the player, we can stop searching
            }
        }

        // ─── Timer ───────────────────────────────────────────────────────────

        private void UpdateTimer(float remaining)
        {
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes}:{seconds:00}";

            if (timerFill != null)
                timerFill.value = remaining / gameDuration;

            // Red timer when under 30 seconds
            if (timerText != null)
                timerText.color = remaining < 30f ? Color.red : Color.white;

            int second = Mathf.CeilToInt(remaining);
            if (remaining < 30f && remaining > 0f && second != lastTimerSecond)
            {
                lastTimerSecond = second;
                AudioManager.Instance?.PlayTimerTick();
            }
        }

        // ─── Sabotage Count ──────────────────────────────────────────────────

        private void UpdateSabotageCount(int count)
        {
            sabotageText.text = $"Sabotages: {count}/{Constants.SABOTAGES_TO_WIN}";

            // Fill icons
            for (int i = 0; i < sabotageIcons.Length; i++)
            {
                if (sabotageIcons[i] != null)
                    sabotageIcons[i].color = i < count
                        ? new Color(1f, 0.3f, 0.3f)
                        : new Color(0.3f, 0.3f, 0.3f);
            }

            // Show alert
            ShowSabotageAlert(count);
        }

        private void ShowSabotageAlert(int count)
        {
            if (sabotageAlertPanel == null) return;
            sabotageAlertPanel.SetActive(true);
            sabotageAlertText.text = count >= Constants.SABOTAGES_TO_WIN
                ? "⚠️ FINAL SABOTAGE!"
                : $"⚠️ SABOTAGE! ({count}/{Constants.SABOTAGES_TO_WIN})";

            CancelInvoke(nameof(HideAlert));
            Invoke(nameof(HideAlert), 3f);
        }

        private void HideAlert() => sabotageAlertPanel?.SetActive(false);

        // ─── Cooldown Bars ───────────────────────────────────────────────────

        private void UpdateCooldownBars()
        {
            // Scan cooldown
            if (scanAbility != null && scanCooldownFill != null)
            {
                float t = scanAbility.CooldownRemaining > 0f 
                    ? 1f - Mathf.Clamp01(scanAbility.CooldownRemaining / Constants.SCAN_COOLDOWN) 
                    : 0f;
                scanCooldownFill.value = t;
                
                if (scanCooldownText != null)
                    scanCooldownText.text = scanAbility.CooldownRemaining > 0f
                        ? $"{scanAbility.CooldownRemaining:F1}s"
                        : "READY";
            }

            // Blend cooldown
            if (blendAbility != null && blendCooldownFill != null)
            {
                float t = blendAbility.CooldownRemaining > 0f 
                    ? 1f - Mathf.Clamp01(blendAbility.CooldownRemaining / Constants.BLEND_COOLDOWN) 
                    : 0f;
                blendCooldownFill.value = t;
                
                if (blendCooldownText != null)
                    blendCooldownText.text = blendAbility.CooldownRemaining > 0f
                        ? $"{blendAbility.CooldownRemaining:F1}s"
                        : "READY";

                // Blend duration bar
                if (blendDurationFill != null)
                {
                    float d = 1f - Mathf.Clamp01(blendAbility.DurationRemaining / Constants.BLEND_DURATION);
                    blendDurationFill.value = blendAbility.IsBlended ? d : 0f;
                }
            }
        }

        private void OnBlendStateChanged(bool isBlended)
        {
            // Intentionally left blank: user requested to keep original background color.
            // You can add other UI effects here later if needed.
        }
    }
}
