using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

namespace GhostCatcher
{
    /// <summary>
    /// Attach to any on-screen action button.
    /// Finds the local PlayerController at runtime and calls the correct method.
    /// No need to drag PlayerController into Inspector — it is spawned at runtime.
    /// </summary>
    public class MobileButton : MonoBehaviour
    {
        public enum ButtonType { Interact, Scan, Blend }

        [SerializeField] private ButtonType buttonType;

        private Button           button;
        private PlayerController targetPlayer;

        private void Start()
        {
            button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnButtonClicked);
        }

        private void Update()
        {
            // Keep trying to find the local player until found
            if (targetPlayer == null)
                FindLocalPlayer();
        }

        private void OnButtonClicked()
        {
            if (targetPlayer == null)
            {
                FindLocalPlayer();
                if (targetPlayer == null) return;
            }

            switch (buttonType)
            {
                case ButtonType.Interact: targetPlayer.OnInteractButton(); break;
                case ButtonType.Scan:     targetPlayer.OnScanButton();     break;
                case ButtonType.Blend:    targetPlayer.OnBlendButton();    break;
            }
        }

        private void FindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var pv = pc.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    targetPlayer = pc;
                    break;
                }
            }
        }
    }
}
